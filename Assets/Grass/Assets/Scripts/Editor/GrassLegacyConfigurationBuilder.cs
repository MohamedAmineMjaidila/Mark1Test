using System;
using System.IO;
using System.Runtime.InteropServices;
using GrassFlow;
using GrassFlow.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Editor-only capture of the actual reference generator, not a CPU approximation of its noise.</summary>
public static class GrassLegacyConfigurationBuilder
{
    public static GrassConfiguration Capture(GrassRenderer grass, bool apply)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Save profile edits and leave Play Mode before capturing the immutable legacy field.");
        string folder = "Assets/Grass/Scenes/" + grass.gameObject.scene.name + "/Legacy";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var field = LoadOrCreate<GrassFieldData>(folder + "/Legacy Field.asset");
        BakeField(grass, field);
        var template = LoadOrCreate<GrassExperimentProfile>(folder + "/Legacy Profile Template.asset");
        CopyReferenceSettings(grass, template);
        GrassProfileCompiler.Compile(template);
        var configuration = LoadOrCreate<GrassConfiguration>(folder + "/Legacy Configuration.asset");
        configuration.profileTemplate = template; configuration.field = field;
        configuration.description = "Snapshot of this renderer's legacy generator, material, LOD and density settings. Uses immutable baked 24-byte reference records plus occupancy and compacted 4-byte visible IDs. Reference equations and wind texture are preserved; this is a compatibility baseline, not the lowest-memory optimized field.";
        EditorUtility.SetDirty(configuration); EditorUtility.SetDirty(template); AssetDatabase.SaveAssets();
        if (apply) Apply(grass, configuration);
        return configuration;
    }

    public static void Apply(GrassRenderer grass, GrassConfiguration configuration)
    {
        if (!configuration || !configuration.field || !configuration.profileTemplate) throw new ArgumentException("Configuration needs a field and profile template.");
        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(configuration));
        var working = Object.Instantiate(configuration.profileTemplate);
        working.name = grass.name + " Legacy Tuning";
        AssetDatabase.CreateAsset(working, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + working.name + ".asset"));
        GrassProfileCompiler.Compile(working);
        Undo.RecordObject(grass, "Apply full grass configuration");
        grass.ApplyConfiguration(configuration, working);
        EditorUtility.SetDirty(grass); EditorSceneManager.MarkSceneDirty(grass.gameObject.scene); AssetDatabase.SaveAssets();
    }

    public static void BakeField(GrassRenderer grass, GrassFieldData field)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Legacy field capture is Editor-only, outside Play Mode.");
        var so = new SerializedObject(grass);
        var terrain = so.FindProperty("terrain").objectReferenceValue as Terrain;
        var shader = so.FindProperty("computeShader").objectReferenceValue as ComputeShader;
        if (!terrain || !shader) throw new InvalidOperationException("The reference Terrain and generation compute shader must be assigned.");
        int side = Mathf.CeilToInt(Mathf.Sqrt(so.FindProperty("grassCount").intValue));
        int count = checked(side * side);
        float size = so.FindProperty("terrainSize").floatValue, spacing = size / side;
        var records = new GrassLegacyBlade[count];
        var generator = Object.Instantiate(shader);
        try
        {
            using (var buffer = new ComputeBuffer(count, GrassLegacyBlade.Stride))
            {
                int kernel = generator.FindKernel("CSMain");
                generator.SetBuffer(kernel, "grassDataBuffer", buffer);
                generator.SetVector("_ObjectPosition", grass.transform.position);
                generator.SetFloat("_TerrainSize", size); generator.SetInt("_GrassCount", count); generator.SetInt("_GrassPerRow", side);
                Set("_MinHeight", "minGrassHeight"); Set("_MaxHeight", "maxGrassHeight");
                Set("_ClumpScale", "clumpScale"); Set("_ClumpStrength", "clumpStrength");
                Set("_HeightScale", "heightScale"); Set("_HeightFactor", "heightFactor"); Set("_AngleVariation", "angleVariation");
                generator.SetFloat("_Time", 0);
                generator.SetTexture(kernel, "_HeightMap", terrain.terrainData.heightmapTexture);
                generator.SetVector("_TerrainSizeData", terrain.terrainData.size); generator.SetVector("_TerrainPos", terrain.transform.position);
                generator.SetTexture(kernel, "_DensityMap", grass.densityMap ? grass.densityMap : Texture2D.whiteTexture);
                generator.SetBool("_UseDensityMap", grass.densityMap); generator.SetFloat("_DensityThreshold", grass.densityThreshold);
                generator.GetKernelThreadGroupSizes(kernel, out uint groupSize, out _, out _);
                generator.Dispatch(kernel, Mathf.CeilToInt(count / (float)groupSize), 1, 1);
                buffer.GetData(records); // Explicit Editor bake, never in the frame loop.
            }
            void Set(string gpu, string property) { generator.SetFloat(gpu, so.FindProperty(property).floatValue); }
        }
        finally { Object.DestroyImmediate(generator); }

        string payloadPath = Path.ChangeExtension(AssetDatabase.GetAssetPath(field), ".bytes");
        File.WriteAllBytes(payloadPath, MemoryMarshal.AsBytes(records.AsSpan()).ToArray());
        AssetDatabase.ImportAsset(payloadPath, ImportAssetOptions.ForceSynchronousImport);
        var payload = AssetDatabase.LoadAssetAtPath<TextAsset>(payloadPath);
        int tileColumns = (side + 31) / 32;
        var tiles = new GrassTile[tileColumns * tileColumns];
        var lows = new Vector3[tiles.Length]; var highs = new Vector3[tiles.Length];
        for (int i = 0; i < tiles.Length; i++) { lows[i] = Vector3.one * float.PositiveInfinity; highs[i] = Vector3.one * float.NegativeInfinity; }
        var words = new int[GrassGrid.WordsPerRow(side) * side];
        Vector3 origin = terrain.transform.position;
        float padding = 0, maxHeight = 0; int occupied = 0;
        for (int id = 0; id < records.Length; id++)
        {
            int row = id / side, col = id % side;
            float height = Mathf.HalfToFloat((ushort)(records[id].heightWidth & 0xffff));
            if (!(height > 0.0001f)) continue;
            GrassGrid.SetOccupied(words, row, col, side); occupied++;
            int tile = row / 32 * tileColumns + col / 32; tiles[tile].occupiedCount++;
            Vector3 local = records[id].position - origin;
            lows[tile] = Vector3.Min(lows[tile], local); highs[tile] = Vector3.Max(highs[tile], local);
            padding = Mathf.Max(padding, Mathf.Abs(local.x - (col + 0.5f) * spacing), Mathf.Abs(local.z - (row + 0.5f) * spacing));
            maxHeight = Mathf.Max(maxHeight, height);
        }
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i].id = i;
            if (tiles[i].occupiedCount == 0) { tiles[i].bounds = new Bounds(); continue; }
            // Include the nominal grid footprint too, allowing a deliberate switch to cell-grid placement.
            int tx = i % tileColumns, tz = i / tileColumns;
            lows[i].x = Mathf.Min(lows[i].x, tx * 32 * spacing); lows[i].z = Mathf.Min(lows[i].z, tz * 32 * spacing);
            highs[i].x = Mathf.Max(highs[i].x, Mathf.Min((tx + 1) * 32, side) * spacing);
            highs[i].z = Mathf.Max(highs[i].z, Mathf.Min((tz + 1) * 32, side) * spacing);
            tiles[i].bounds = new Bounds((lows[i] + highs[i]) * .5f, highs[i] - lows[i]);
        }
        int resolution = terrain.terrainData.heightmapResolution;
        var heights = field.HeightTexture;
        if (!heights)
        {
            heights = new Texture2D(resolution, resolution, TextureFormat.RFloat, false, true) { name = "Legacy terrain height reference" };
            AssetDatabase.AddObjectToAsset(heights, field);
        }
        else if (heights.width != resolution) heights.Reinitialize(resolution, resolution, TextureFormat.RFloat, false);
        float[,] sourceHeights = terrain.terrainData.GetHeights(0, 0, resolution, resolution);
        var flat = new float[resolution * resolution];
        for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++) flat[y * resolution + x] = sourceHeights[y,x];
        heights.SetPixelData(flat, 0); heights.Apply(false, false); heights.filterMode = FilterMode.Bilinear; heights.wrapMode = TextureWrapMode.Clamp;
        field.SetBakedData(new Vector3(size, terrain.terrainData.size.y, size), spacing, grass.bakeSeed, words, tiles, occupied, heights, false, default,
            "Captured from original GrassCompute.CSMain; source settings=" + EditorJsonUtility.ToJson(grass), side, side);
        field.SetLegacySnapshot(payload, origin, padding, maxHeight);
        EditorUtility.SetDirty(field); EditorUtility.SetDirty(heights); AssetDatabase.SaveAssets();
        Debug.Log($"Legacy field: {occupied:N0}/{count:N0} occupied; immutable reference payload {(long)count * GrassLegacyBlade.Stride:N0} bytes.", grass);
    }

    private static void CopyReferenceSettings(GrassRenderer grass, GrassExperimentProfile p)
    {
        var so = new SerializedObject(grass);
        var material = so.FindProperty("grassMaterial").objectReferenceValue as Material;
        if (!material) throw new InvalidOperationException("Assign the reference grass material.");
        p.ApplyRecipe(GrassShaderPreset.ReferenceShading);
        p.algorithms.model = GrassRendererModel.LegacyReference;
        p.algorithms.placement = GrassPlacement.LegacySnapshot;
        p.algorithms.selection = GrassSelection.LegacyDistanceAndDensity;
        p.algorithms.wind = GrassWind.LegacyTexture;
        p.drawPath = GrassDrawPath.CompactedIds;
        p.lod0Segments = so.FindProperty("lod0Segments").intValue; p.lod1Segments = so.FindProperty("lod1Segments").intValue; p.lod2Segments = so.FindProperty("lod2Segments").intValue;
        p.lod0Distance = F("lod0Distance"); p.lod1Distance = F("lod1Distance"); p.maxDrawDistance = F("maxDrawDistance");
        p.bladeHeights = new Vector2(F("minGrassHeight"), F("maxGrassHeight"));
        p.minimumDensity = so.FindProperty("enableDensityScaling").boolValue ? F("minDensity") : 1;
        p.densityFalloffStart = F("densityFalloffStart"); p.widthCompensation = F("widthCompensation"); p.transitionWidth = 0;
        p.rootColor = material.GetColor("_RootColor"); p.midColor = material.GetColor("_MidColor"); p.tipColor = material.GetColor("_TipColor"); p.dryColor = material.GetColor("_DryColor");
        p.bladeWidth = M("_BladeWidth"); p.naturalBend = M("_BladeBend"); p.legacyHeightMultiplier = M("_BladeHeight");
        p.legacyWindMap = material.GetTexture("_WindMap") as Texture2D; p.legacyWindVelocity = material.GetVector("_WindVelocity");
        p.legacyWindFrequency = M("_WindFrequency"); p.gustStrength = M("_WindGustStrength"); p.flutterStrength = M("_WindFlutterStrength");
        p.legacyWindDisableDistance = F("windDisableDistance"); p.legacyCameraFacing = F("cameraFacing");
        p.roundnessRadians = M("_NormalRotationAngle") * Mathf.PI;
        p.transmission = M("_Translucency"); p.transmissionPower = M("_TranslucencyPower");
        p.specular = M("_SpecularStrength"); p.specularPower = M("_SpecularPower");
        p.rootDarkness = 1 - M("_AmbientOcclusionStrength"); p.legacyShadowSoftness = M("_ShadowSoftness");
        p.legacyNoiseFrequency = M("_ColorNoiseScale"); p.colorVariation = M("_ColorNoiseStrength"); p.dryness = M("_DryAmount");
        p.legacyCastShadows = so.FindProperty("castShadows").boolValue; p.legacyCastShadowsLod1 = so.FindProperty("castShadowsLOD1").boolValue; p.legacyCastShadowsLod2 = so.FindProperty("castShadowsLOD2").boolValue;
        p.NotifyChanged();
        float F(string property) => so.FindProperty(property).floatValue;
        float M(string property) => material.GetFloat(property);
    }
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
}
