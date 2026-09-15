using System;
using System.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace GrassFlow
{
    public struct GrassLodDiagnostics
    {
        public int segments, vertices;
        public uint meshIndices, argumentIndices, instances;
    }

    public struct GrassRenderStatistics
    {
        public uint lod0, lod1, lod2, overflow;
        public int candidateTiles;
        public long bufferBytes;
        public long managedBytes;
        public double cpuMilliseconds;
        public bool countersValid, readbackFailed;
    }

    /// <summary>Owned by GrassRenderer. No scene registration, global state or hidden update loop.</summary>
    public sealed class GrassFieldRenderer : IDisposable
    {
        private static readonly ProfilerMarker CpuMarker = new ProfilerMarker("GrassFlow.SelectAndSubmit");
        private readonly GrassFieldData field;
        private readonly GrassExperimentProfile profile;
        private readonly int fieldRevision;
        private readonly int[] capacities = new int[3];
        private readonly int[] segments = new int[3];
        private readonly int[] tileCounts = new int[3];
        private readonly Shader[] shaders = new Shader[3];
        private readonly Mesh[] meshes = new Mesh[3];
        private readonly Material[] materials = new Material[3];
        private readonly MaterialPropertyBlock[] properties = new MaterialPropertyBlock[3];
        private readonly GraphicsBuffer[] visible = new GraphicsBuffer[3];
        private readonly GraphicsBuffer[] arguments = new GraphicsBuffer[3];
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[][] argumentData = new GraphicsBuffer.IndirectDrawIndexedArgs[3][];
        private GraphicsBuffer occupancy, tiles, counters, legacyRecords;
        private readonly bool hasLegacyBinding;
        private readonly uint[] zeroCounters = new uint[4];
        private readonly uint[] tileIds;
        private readonly Plane[] planes = new Plane[6];
        private readonly Vector4[] frustum = new Vector4[6];
        private readonly ComputeShader compute;
        private readonly ComputeShader sourceCompute;
        private readonly CommandBuffer commands;
        private readonly GrassDrawPath path;
        private readonly int selectKernel, argsKernel;
        private readonly Action<AsyncGPUReadbackRequest> onCounters;
        private int profileRevision = -1;

        private bool disposed, counterPending;
        private float nextCounterTime;
        private GrassRenderStatistics stats;
        public GrassRenderStatistics Statistics => stats;

        public GrassFieldRenderer(GrassFieldData field, GrassExperimentProfile profile)
        {
            this.field = field; this.profile = profile;
            if (!field.IsValid(out string fieldError)) throw new ArgumentException(fieldError);
            if (!profile.IsValid(field, out string profileError)) throw new ArgumentException(profileError);
            fieldRevision = field.Revision; path = profile.drawPath;
            sourceCompute = profile.selectionShader; hasLegacyBinding = profile.UsesLegacyField;
            tileIds = new uint[field.TileCount * 3];
            onCounters = ReceiveCounters;
            try
            {
                commands = new CommandBuffer { name = "GrassFlow visible cell selection" };
                compute = Object.Instantiate(profile.selectionShader);
                compute.hideFlags = HideFlags.HideAndDontSave;
                if (profile.UsesLegacyField) compute.EnableKeyword("GRASS_LEGACY_FIELD"); else compute.DisableKeyword("GRASS_LEGACY_FIELD");
                if (profile.UsesLegacySelection) compute.EnableKeyword("GRASS_LEGACY_SELECTION"); else compute.DisableKeyword("GRASS_LEGACY_SELECTION");
                selectKernel = compute.FindKernel("SelectTiles"); argsKernel = compute.FindKernel("BuildArguments");
                occupancy = new GraphicsBuffer(GraphicsBuffer.Target.Structured, field.Words.Length, sizeof(uint));
                occupancy.SetData(field.Words);
                if (hasLegacyBinding)
                {
                    var records = field.LegacySnapshot.GetData<GrassLegacyBlade>();
                    if (records.Length != field.CellCount) throw new ArgumentException("Legacy payload length does not match field dimensions.");
                    legacyRecords = new GraphicsBuffer(GraphicsBuffer.Target.Structured, records.Length, GrassLegacyBlade.Stride);
                    legacyRecords.SetData(records);
                }
                tiles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, tileIds.Length, sizeof(uint));
                counters = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, sizeof(uint));
                counters.SetData(zeroCounters);
                stats.bufferBytes = field.OccupancyBytes + tileIds.LongLength * 4 + 16 + (legacyRecords != null ? (long)legacyRecords.count * GrassLegacyBlade.Stride : 0);
                shaders[0] = profile.lod0Shader; shaders[1] = profile.lod1Shader; shaders[2] = profile.lod2Shader;
                segments[0] = profile.lod0Segments; segments[1] = profile.lod1Segments; segments[2] = profile.lod2Segments;
                for (int lod = 0; lod < 3; lod++)
                {
                    capacities[lod] = RequiredCapacity(lod);
                    meshes[lod] = GrassBladeMesh.Create(segments[lod]);
                    materials[lod] = new Material(shaders[lod]) { name = $"GrassFlow LOD{lod} {profile.name}", hideFlags = HideFlags.HideAndDontSave };
                    properties[lod] = new MaterialPropertyBlock();
                    if (path == GrassDrawPath.DirectGrid) materials[lod].EnableKeyword("GRASS_DIRECT_GRID");
                    // A one-element inert binding keeps the direct path's unused buffer resource valid.
                    visible[lod] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, path == GrassDrawPath.CompactedIds ? capacities[lod] : 1, sizeof(uint));
                    arguments[lod] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, GraphicsBuffer.IndirectDrawIndexedArgs.size / 4, 4);
                    argumentData[lod] = new[] { new GraphicsBuffer.IndirectDrawIndexedArgs { indexCountPerInstance = meshes[lod].GetIndexCount(0),
                        startIndex = meshes[lod].GetIndexStart(0), baseVertexIndex = meshes[lod].GetBaseVertex(0), instanceCount = 0, startInstance = 0 } };
                    arguments[lod].SetData(argumentData[lod]);
                    properties[lod].SetBuffer(Ids.Occupancy, occupancy);
                    if (legacyRecords != null) properties[lod].SetBuffer("_GrassLegacyRecords", legacyRecords);
                    properties[lod].SetBuffer(Ids.Tiles, tiles);
                    properties[lod].SetBuffer(Ids.VisibleIds, visible[lod]);
                    properties[lod].SetInt(Ids.TileOffset, lod * field.TileCount);
                    stats.bufferBytes += (long)visible[lod].count * 4 + GraphicsBuffer.IndirectDrawIndexedArgs.size;
                    compute.SetBuffer(selectKernel, "_GrassVisible" + lod, visible[lod]);
                    compute.SetBuffer(argsKernel, "_GrassArgs" + lod, arguments[lod]);
                    compute.SetInt("_GrassCapacity" + lod, capacities[lod]);
                }
                compute.SetBuffer(selectKernel, Ids.Occupancy, occupancy);
                if (legacyRecords != null) compute.SetBuffer(selectKernel, "_GrassLegacyRecords", legacyRecords);
                compute.SetBuffer(selectKernel, Ids.Tiles, tiles);
                compute.SetBuffer(selectKernel, "_GrassCounters", counters);
                compute.SetBuffer(argsKernel, "_GrassCounters", counters);
                compute.SetInt("_GrassArgumentInstanceOffset", GrassIndirectLayout.InstanceCountOffset);
                ApplyProfile();
            }
            catch { Dispose(); throw; }
        }

        public GrassLodDiagnostics GetLodDiagnostics(int lod, bool readGpuArguments = false)
        {
            if (disposed || lod < 0 || lod > 2) return default;
            var args = argumentData[lod][0];
            if (readGpuArguments)
            {
                var readback = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
                arguments[lod].GetData(readback); // Explicit diagnostic only; never called by the render loop.
                args = readback[0];
            }
            return new GrassLodDiagnostics { segments = segments[lod], vertices = meshes[lod].vertexCount,
                meshIndices = meshes[lod].GetIndexCount(0), argumentIndices = args.indexCountPerInstance, instances = args.instanceCount };
        }

        private float TransitionWidth => profile.UsesLegacySelection ? 0 : Mathf.Clamp(profile.transitionWidth, 0, Mathf.Min(profile.lod0Distance, Mathf.Min(profile.lod1Distance - profile.lod0Distance, profile.maxDrawDistance - profile.lod1Distance)));
        private int RequiredCapacity(int lod)
        {
            float radius = lod == 0 ? profile.lod0Distance + TransitionWidth * 0.5f : lod == 1 ? profile.lod1Distance + TransitionWidth * 0.5f : profile.maxDrawDistance;
            return GrassGrid.Capacity(field.Columns, field.Rows, field.CellSpacing, radius + field.PlacementPadding);
        }

        public bool NeedsRebuild(GrassFieldData newField, GrassExperimentProfile newProfile)
        {
            if (disposed || newField != field || newProfile != profile || field.Revision != fieldRevision || profile.drawPath != path || sourceCompute != profile.selectionShader || hasLegacyBinding != profile.UsesLegacyField) return true;
            return path == GrassDrawPath.CompactedIds && (capacities[0] < RequiredCapacity(0) || capacities[1] < RequiredCapacity(1) || capacities[2] < RequiredCapacity(2));
        }

        private void ApplyProfile()
        {
            if (profile.UsesLegacyField) compute.EnableKeyword("GRASS_LEGACY_FIELD"); else compute.DisableKeyword("GRASS_LEGACY_FIELD");
            if (profile.UsesLegacySelection) compute.EnableKeyword("GRASS_LEGACY_SELECTION"); else compute.DisableKeyword("GRASS_LEGACY_SELECTION");
            if (profile.algorithms.height == GrassHeight.MathematicalFixture) compute.EnableKeyword("GRASS_MATH_HEIGHT");
            else compute.DisableKeyword("GRASS_MATH_HEIGHT");
            for (int lod = 0; lod < 3; lod++)
            {
                Shader shader = lod == 0 ? profile.lod0Shader : lod == 1 ? profile.lod1Shader : profile.lod2Shader;
                int count = lod == 0 ? profile.lod0Segments : lod == 1 ? profile.lod1Segments : profile.lod2Segments;
                if (shaders[lod] != shader)
                {
                    DestroyOwned(materials[lod]); shaders[lod] = shader;
                    materials[lod] = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    if (path == GrassDrawPath.DirectGrid) materials[lod].EnableKeyword("GRASS_DIRECT_GRID");
                }
                if (segments[lod] != count)
                {
                    DestroyOwned(meshes[lod]); segments[lod] = count; meshes[lod] = GrassBladeMesh.Create(count);
                    argumentData[lod][0].indexCountPerInstance = meshes[lod].GetIndexCount(0);
                    arguments[lod].SetData(argumentData[lod]);
                }
            }
            Vector4 terrainSize = new Vector4(field.TerrainSize.x, field.TerrainSize.y, field.TerrainSize.z, field.CellSpacing);
            Vector4 heightSize = new Vector4(field.HeightTexture.width, field.HeightTexture.height, 1f / field.HeightTexture.width, 1f / field.HeightTexture.height);
            Vector4 distances = new Vector4(profile.lod0Distance, profile.lod1Distance, profile.maxDrawDistance, profile.densityFalloffStart);
            Vector4 density = new Vector4(profile.minimumDensity, profile.widthCompensation, profile.maximumWidthScale, TransitionWidth);
            Vector4 blade = new Vector4(profile.bladeHeights.x, profile.bladeHeights.y, profile.bladeWidth, profile.naturalBend);
            Vector2 wind = profile.windDirection.sqrMagnitude > 1e-8f ? profile.windDirection.normalized : Vector2.right;
            Vector3 ao = Vector3.Max(Vector3.zero, profile.aoCurveWeights);
            ao = ao.x + ao.y + ao.z > 0 ? ao / (ao.x + ao.y + ao.z) : Vector3.up;
            for (int lod = 0; lod < 3; lod++)
            {
                MaterialPropertyBlock p = properties[lod];
                p.SetInt(Ids.Columns, field.Columns); p.SetInt(Ids.Rows, field.Rows);
                p.SetInt(Ids.WordsPerRow, GrassGrid.WordsPerRow(field.Columns)); p.SetInt(Ids.TileColumns, field.TileColumns); p.SetInt(Ids.Seed, field.Seed);
                p.SetTexture(Ids.Heights, field.HeightTexture); p.SetVector(Ids.TerrainSize, terrainSize);
                p.SetVector(Ids.HeightSize, heightSize); p.SetVector(Ids.HeightModel, field.AnalyticModel);
                p.SetVector(Ids.Distance, distances); p.SetVector(Ids.Density, density); p.SetVector(Ids.Blade, blade);
                p.SetVector(Ids.Wind, new Vector4(wind.x, wind.y, profile.windStrength, profile.windSpeed));
                p.SetVector(Ids.Normal, new Vector4(Mathf.Cos(profile.roundnessRadians), Mathf.Sin(profile.roundnessRadians), Mathf.Clamp01(profile.normalStabilization[lod]), profile.roundnessRadians));
                p.SetVector(Ids.RootColor, Linear(profile.rootColor)); p.SetVector(Ids.MidColor, Linear(profile.midColor));
                p.SetVector(Ids.TipColor, Linear(profile.tipColor)); p.SetVector(Ids.DryColor, Linear(profile.dryColor));
                p.SetVector(Ids.Sky, Linear(profile.skyAmbient)); p.SetVector(Ids.Ground, Linear(profile.groundAmbient));
                p.SetVector(Ids.ColorParams, new Vector4(profile.colorVariation, profile.dryness, profile.rootDarkness, profile.specular));
                p.SetVector(Ids.LightParams, new Vector4(profile.transmission, profile.transmissionPower, profile.specularPower, 0));
                p.SetVector(Ids.Ao, ao);
                p.SetVector("_GrassLegacyBakeOrigin", field.LegacyBakeOrigin);
                p.SetFloat("_GrassLegacyHeightMultiplier", profile.legacyHeightMultiplier);
                p.SetTexture("_WindMap", profile.legacyWindMap ? profile.legacyWindMap : Texture2D.whiteTexture);
                p.SetVector("_WindVelocity", profile.legacyWindVelocity);
                p.SetFloat("_WindFrequency", profile.legacyWindFrequency); p.SetFloat("_WindDisableDistance", profile.legacyWindDisableDistance);
                p.SetFloat("_WindGustStrength", profile.gustStrength); p.SetFloat("_WindFlutterStrength", profile.flutterStrength);
                p.SetFloat("_BladeWidth", profile.bladeWidth); p.SetFloat("_BladeHeight", profile.legacyHeightMultiplier);
                p.SetFloat("_BladeBend", profile.naturalBend); p.SetFloat("_CameraFacing", profile.legacyCameraFacing);
                p.SetVector("_RootColor", Linear(profile.rootColor)); p.SetVector("_MidColor", Linear(profile.midColor));
                p.SetVector("_TipColor", Linear(profile.tipColor)); p.SetVector("_DryColor", Linear(profile.dryColor));
                p.SetFloat("_DryAmount", profile.dryness); p.SetFloat("_ColorNoiseScale", profile.legacyNoiseFrequency); p.SetFloat("_ColorNoiseStrength", profile.colorVariation);
                p.SetFloat("_NormalRotationAngle", profile.roundnessRadians / Mathf.PI);
                p.SetFloat("_Translucency", profile.transmission); p.SetFloat("_TranslucencyPower", profile.transmissionPower);
                p.SetFloat("_SpecularStrength", profile.specular); p.SetFloat("_SpecularPower", profile.specularPower);
                p.SetFloat("_AmbientOcclusionStrength", 1 - profile.rootDarkness); p.SetFloat("_ShadowSoftness", profile.legacyShadowSoftness);
                p.SetVector("_GrassWaveA", profile.waveA); p.SetVector("_GrassWaveB", profile.waveB);
                p.SetVector("_GrassGustWave", profile.gustWave);
                p.SetVector("_GrassWaveParams", new Vector4(profile.secondWaveWeight, Mathf.Max(0.05f, profile.stiffnessRange.x), Mathf.Max(0.05f, profile.stiffnessRange.y), 0));
                p.SetVector("_GrassPatchParams", new Vector4(Mathf.Max(1, profile.patchSizeCells), profile.colorPatchWeight, profile.drynessPatchWeight, profile.legacyNoiseFrequency));
            }
            compute.SetVector("_GrassLegacyBakeOrigin", field.LegacyBakeOrigin);
            compute.SetFloat("_GrassLegacyHeightMultiplier", profile.legacyHeightMultiplier);
            compute.SetInt(Ids.Columns, field.Columns); compute.SetInt(Ids.Rows, field.Rows);
            compute.SetInt(Ids.WordsPerRow, GrassGrid.WordsPerRow(field.Columns)); compute.SetInt(Ids.TileColumns, field.TileColumns); compute.SetInt(Ids.Seed, field.Seed);
            compute.SetTexture(selectKernel, Ids.Heights, field.HeightTexture);
            compute.SetVector(Ids.TerrainSize, terrainSize); compute.SetVector(Ids.HeightSize, heightSize); compute.SetVector(Ids.HeightModel, field.AnalyticModel);
            compute.SetVector(Ids.Distance, distances); compute.SetVector(Ids.Density, density); compute.SetVector(Ids.Blade, blade);
            profileRevision = profile.revision;
        }

        private static Vector4 Linear(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? (Vector4)c.linear : (Vector4)c;

        public void Render(Camera camera, Vector3 origin, Light sun, float simulationTime)
        {
            if (disposed || !camera) return;
            using (CpuMarker.Auto())
            {
                long started = Stopwatch.GetTimestamp();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                if (profileRevision != profile.revision || segments[0] != profile.lod0Segments || segments[1] != profile.lod1Segments || segments[2] != profile.lod2Segments ||
                    shaders[0] != profile.lod0Shader || shaders[1] != profile.lod1Shader || shaders[2] != profile.lod2Shader) ApplyProfile();
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                for (int i = 0; i < 6; i++) frustum[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
                Vector3 cameraPosition = camera.transform.position;
                int count = SelectTiles(cameraPosition, origin);
                stats.candidateTiles = count;
                if (path == GrassDrawPath.CompactedIds)
                {
                    counters.SetData(zeroCounters);
                    if (count > 0) tiles.SetData(tileIds, 0, 0, count);
                    compute.SetVector(Ids.Camera, cameraPosition); compute.SetVector(Ids.Origin, origin); compute.SetVectorArray(Ids.Frustum, frustum);
                    commands.Clear();
                    commands.BeginSample("GrassFlow.SelectTiles");
                    if (count > 0) commands.DispatchCompute(compute, selectKernel, count, 1, 1);
                    commands.DispatchCompute(compute, argsKernel, 1, 1, 1);
                    commands.EndSample("GrassFlow.SelectTiles");
                    Graphics.ExecuteCommandBuffer(commands);
                }
                else
                {
                    for (int lod = 0; lod < 3; lod++)
                    {
                        if (tileCounts[lod] > 0) tiles.SetData(tileIds, lod * field.TileCount, lod * field.TileCount, tileCounts[lod]);
                        argumentData[lod][0].instanceCount = (uint)(tileCounts[lod] * 1024);
                        arguments[lod].SetData(argumentData[lod]);
                    }
                }
                Vector3 view = -camera.transform.forward;
                Vector3 lightDirection = sun ? -sun.transform.forward : Vector3.up;
                Vector4 lightColor = sun ? Linear(sun.color) * sun.intensity : Vector4.one;
                Bounds bounds = new Bounds(origin + field.TerrainSize * 0.5f, field.TerrainSize);
                bounds.Expand((MaximumBladeHeight + profile.bladeWidth * MaximumWidthScale + field.PlacementPadding) * 2);
                for (int lod = 0; lod < 3; lod++)
                {
                    MaterialPropertyBlock p = properties[lod];
                    p.SetVector(Ids.Camera, cameraPosition); p.SetVector(Ids.Origin, origin); p.SetVectorArray(Ids.Frustum, frustum);
                    p.SetVector(Ids.View, view); p.SetVector(Ids.SunDirection, lightDirection); p.SetVector(Ids.SunColor, lightColor);
                    p.SetVector(Ids.HalfDirection, (view + lightDirection).normalized);
                    p.SetFloat("_GrassLegacyTime", simulationTime);
                    p.SetVector(Ids.WindDetail, new Vector4(profile.gustStrength, profile.flutterStrength, simulationTime, profile.widening));
                    var renderParams = new RenderParams(materials[lod]) { camera = camera, worldBounds = bounds, matProps = p,
                        shadowCastingMode = profile.algorithms.model == GrassRendererModel.LegacyReference && profile.legacyCastShadows && (lod == 0 || lod == 1 && profile.legacyCastShadowsLod1 || lod == 2 && profile.legacyCastShadowsLod2) ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows = profile.algorithms.model == GrassRendererModel.LegacyReference, lightProbeUsage = LightProbeUsage.Off };
                    Graphics.RenderMeshIndirect(renderParams, meshes[lod], arguments[lod]);
                }
                if (path == GrassDrawPath.CompactedIds && profile.collectCounters && SystemInfo.supportsAsyncGPUReadback && !counterPending && Time.realtimeSinceStartup >= nextCounterTime)
                {
                    counterPending = true; nextCounterTime = Time.realtimeSinceStartup + Mathf.Max(0.25f, profile.counterInterval);
                    AsyncGPUReadback.Request(counters, onCounters);
                }
                stats.cpuMilliseconds = (Stopwatch.GetTimestamp() - started) * (1000.0 / Stopwatch.Frequency);
                stats.managedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            }
        }

        private float MaximumWidthScale => profile.UsesLegacySelection
            ? Mathf.LerpUnclamped(1, 1 / Mathf.Max(0.001f, profile.minimumDensity), profile.widthCompensation) * 1.001f
            : profile.maximumWidthScale;
        private float MaximumBladeHeight => profile.UsesLegacyField ? field.MaximumLegacyHeight * Mathf.Max(profile.UsesLegacySelection ? 1.5f : 1, profile.legacyHeightMultiplier) : profile.bladeHeights.y;

        private int SelectTiles(Vector3 camera, Vector3 origin)
        {
            Array.Clear(tileCounts, 0, 3);
            int count = 0;
            float radius = MaximumBladeHeight + profile.bladeWidth * MaximumWidthScale;
            float halfTransition = TransitionWidth * 0.5f;
            float tileWidth = GrassGrid.TileSide * field.CellSpacing;
            float cx = camera.x - origin.x, cz = camera.z - origin.z, distance = profile.maxDrawDistance + field.PlacementPadding;
            if (cx + distance < 0 || cz + distance < 0 || cx - distance > field.TerrainSize.x || cz - distance > field.TerrainSize.z) return 0;
            int tileRows = (field.Rows + GrassGrid.TileSide - 1) / GrassGrid.TileSide;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((cx - distance) / tileWidth), 0, field.TileColumns - 1);
            int x1 = Mathf.Clamp(Mathf.FloorToInt((cx + distance) / tileWidth), 0, field.TileColumns - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((cz - distance) / tileWidth), 0, tileRows - 1);
            int z1 = Mathf.Clamp(Mathf.FloorToInt((cz + distance) / tileWidth), 0, tileRows - 1);
            for (int tz = z0; tz <= z1; tz++)
            for (int tx = x0; tx <= x1; tx++)
            {
                GrassTile tile = field.Tiles[tz * field.TileColumns + tx];
                if (tile.occupiedCount == 0) continue;
                Bounds b = tile.bounds; b.center += origin;
                Vector3 min = b.min, max = b.max;
                float dx = Mathf.Max(Mathf.Max(min.x - camera.x, 0), camera.x - max.x);
                float dz = Mathf.Max(Mathf.Max(min.z - camera.z, 0), camera.z - max.z);
                float nearSq = dx * dx + dz * dz;
                if (profile.UsesLegacySelection)
                {
                    float dy = Mathf.Max(Mathf.Max(min.y - camera.y, 0), camera.y - max.y);
                    nearSq += dy * dy;
                }
                if (nearSq >= profile.maxDrawDistance * profile.maxDrawDistance) continue;
                b.Expand(radius * 2);
                if (!GeometryUtility.TestPlanesAABB(planes, b)) continue;
                if (path == GrassDrawPath.CompactedIds) tileIds[count] = (uint)tile.id;
                else
                {
                    float farX = Mathf.Max(Mathf.Abs(min.x - camera.x), Mathf.Abs(max.x - camera.x));
                    float farZ = Mathf.Max(Mathf.Abs(min.z - camera.z), Mathf.Abs(max.z - camera.z));
                    float farSq = farX * farX + farZ * farZ;
                    if (profile.UsesLegacySelection)
                    {
                        float farY = Mathf.Max(Mathf.Abs(min.y - camera.y), Mathf.Abs(max.y - camera.y));
                        farSq += farY * farY;
                    }
                    if (nearSq < Square(profile.lod0Distance + halfTransition)) tileIds[tileCounts[0]++] = (uint)tile.id;
                    if (farSq >= Square(profile.lod0Distance - halfTransition) && nearSq < Square(profile.lod1Distance + halfTransition)) tileIds[field.TileCount + tileCounts[1]++] = (uint)tile.id;
                    if (farSq >= Square(profile.lod1Distance - halfTransition)) tileIds[field.TileCount * 2 + tileCounts[2]++] = (uint)tile.id;
                }
                count++;
            }
            return count;
        }
        private static float Square(float value) => value * value;

        private void ReceiveCounters(AsyncGPUReadbackRequest request)
        {
            counterPending = false;
            if (disposed) return;
            stats.readbackFailed = request.hasError;
            if (request.hasError) return;
            var data = request.GetData<uint>();
            stats.lod0 = data[0]; stats.lod1 = data[1]; stats.lod2 = data[2]; stats.overflow = data[3]; stats.countersValid = true;
            if (stats.overflow != 0) UnityEngine.Debug.LogError("GrassFlow visibility buffer overflow. This configuration fails validation.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            occupancy?.Dispose(); legacyRecords?.Dispose(); tiles?.Dispose(); counters?.Dispose(); commands?.Release();
            for (int i = 0; i < 3; i++) { visible[i]?.Dispose(); arguments[i]?.Dispose(); DestroyOwned(meshes[i]); DestroyOwned(materials[i]); }
            DestroyOwned(compute);
        }
        private static void DestroyOwned(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }

        private static class Ids
        {
            public static readonly int Occupancy = Shader.PropertyToID("_GrassOccupancy"), Tiles = Shader.PropertyToID("_GrassTiles"), VisibleIds = Shader.PropertyToID("_GrassVisibleIds"),
                TileOffset = Shader.PropertyToID("_GrassTileOffset"), Columns = Shader.PropertyToID("_GrassColumns"), Rows = Shader.PropertyToID("_GrassRows"),
                WordsPerRow = Shader.PropertyToID("_GrassWordsPerRow"), TileColumns = Shader.PropertyToID("_GrassTileColumns"), Seed = Shader.PropertyToID("_GrassSeed"),
                Heights = Shader.PropertyToID("_GrassHeights"), TerrainSize = Shader.PropertyToID("_GrassTerrainSize"), HeightSize = Shader.PropertyToID("_GrassHeightTextureSize"),
                HeightModel = Shader.PropertyToID("_GrassHeightModel"), Distance = Shader.PropertyToID("_GrassDistance"), Density = Shader.PropertyToID("_GrassDensity"),
                Blade = Shader.PropertyToID("_GrassBlade"), Wind = Shader.PropertyToID("_GrassWind"), WindDetail = Shader.PropertyToID("_GrassWindDetail"),
                Normal = Shader.PropertyToID("_GrassNormal"), RootColor = Shader.PropertyToID("_GrassRootColor"), MidColor = Shader.PropertyToID("_GrassMidColor"),
                TipColor = Shader.PropertyToID("_GrassTipColor"), DryColor = Shader.PropertyToID("_GrassDryColor"), Sky = Shader.PropertyToID("_GrassSky"),
                Ground = Shader.PropertyToID("_GrassGround"), ColorParams = Shader.PropertyToID("_GrassColorParams"), LightParams = Shader.PropertyToID("_GrassLightParams"),
                Ao = Shader.PropertyToID("_GrassAoWeights"), Camera = Shader.PropertyToID("_GrassCamera"), Origin = Shader.PropertyToID("_GrassOrigin"),
                Frustum = Shader.PropertyToID("_GrassFrustum"), View = Shader.PropertyToID("_GrassViewDirection"), SunDirection = Shader.PropertyToID("_GrassSunDirection"),
                SunColor = Shader.PropertyToID("_GrassSunColor"), HalfDirection = Shader.PropertyToID("_GrassHalfDirection");
        }
    }
}
