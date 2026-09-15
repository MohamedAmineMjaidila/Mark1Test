using System;
using System.Collections.Generic;
using System.IO;
using GrassFlow;
using GrassFlow.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Creates an isolated, reproducible fixture without editing the starter scene.</summary>
public static class GrassBenchmarkTools
{
    public const string Root = "Assets/Grass/Benchmark";
    public const string ScenePath = Root + "/GrassBenchmark.unity";

    [MenuItem("Tools/GrassFlow/Create benchmark fixture")]
    public static void CreateFixture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before creating the fixture.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
        {
            Debug.Log("GrassFlow fixture already exists: " + ScenePath + ". Existing edits were preserved.");
            return;
        }
        EnsureFolder(Root); EnsureFolder(Root + "/Profiles");
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(Root + "/Terrain.asset");
        Vector4 model = new Vector4(2, 1.2f, 0.07f, 0.035f);
        if (!data)
        {
            data = new TerrainData { name = "Grass benchmark 80 × 400 m", heightmapResolution = 513, size = new Vector3(80, 12, 400) };
            float[,] heights = new float[513, 513];
            for (int z = 0; z < 513; z++)
                for (int x = 0; x < 513; x++)
                    heights[z, x] = GrassGrid.AnalyticHeight(new Vector2(x * 80f / 512, z * 400f / 512), model) / 12;
            data.SetHeights(0, 0, heights);
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Grass/Assets/Layers/Grass 06.terrainlayer");
            if (layer) data.terrainLayers = new[] { layer };
            AssetDatabase.CreateAsset(data, Root + "/Terrain.asset");
        }
        Texture2D density = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Density.asset");
        if (!density)
        {
            density = new Texture2D(256, 1024, TextureFormat.RGBA32, false, true) { name = "Paintable representative density", wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[density.width * density.height];
            for (int z = 0; z < density.height; z++)
                for (int x = 0; x < density.width; x++)
                {
                    float wx = x * 80f / (density.width - 1), wz = z * 400f / (density.height - 1);
                    float path = Mathf.Abs(wx - (40 + Mathf.Sin(wz * 0.023f) * 7));
                    float coverage = Mathf.SmoothStep(0, 0.95f, Mathf.InverseLerp(1.2f, 3.2f, path));
                    coverage *= Mathf.Lerp(0.6f, 1, Mathf.PerlinNoise(wx * 0.06f, wz * 0.06f));
                    byte value = (byte)Mathf.RoundToInt(coverage * 255);
                    pixels[z * density.width + x] = new Color32(value, value, value, 255);
                }
            density.SetPixels32(pixels); density.Apply();
            AssetDatabase.CreateAsset(density, Root + "/Density.asset");
        }
        GrassFieldData field = BakeOnce("RepresentativeField", data, density, model);
        GrassFieldData dense = BakeOnce("DenseField", data, null, model);
        var profiles = new List<GrassExperimentProfile>();
        foreach (GrassDrawPath path in Enum.GetValues(typeof(GrassDrawPath)))
            foreach (GrassShaderPreset preset in Enum.GetValues(typeof(GrassShaderPreset)))
                profiles.Add(CreateProfile(path, preset));
        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root + "/NativePipeline.asset");
        if (!pipeline)
        {
            pipeline = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"));
            pipeline.name = "Grass native benchmark pipeline"; pipeline.renderScale = 1; pipeline.msaaSampleCount = 1;
            var serializedPipeline = new SerializedObject(pipeline);
            serializedPipeline.FindProperty("m_MainLightShadowsSupported").boolValue = false;
            serializedPipeline.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            serializedPipeline.FindProperty("m_AnyShadowsSupported").boolValue = false;
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            pipeline.supportsCameraDepthTexture = false; pipeline.supportsCameraOpaqueTexture = false;
            AssetDatabase.CreateAsset(pipeline, Root + "/NativePipeline.asset");
        }
        Scene previous = SceneManager.GetActiveScene();
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(fixture);
            Terrain terrain = Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>(); terrain.name = "Benchmark terrain";
            terrain.drawInstanced = true; terrain.heightmapPixelError = 5; terrain.treeDistance = 0; terrain.detailObjectDistance = 0;
            Camera camera = new GameObject("Benchmark Camera", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.fieldOfView = 50; camera.nearClipPlane = 0.1f; camera.farClipPlane = 150;
            camera.aspect = 1080f / 2340; camera.allowDynamicResolution = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.45f, 0.64f, 0.78f);
            Light sun = new GameObject("Benchmark sun", typeof(Light)).GetComponent<Light>(); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40, -35, 0); sun.color = new Color(1, 0.96f, 0.86f); sun.intensity = 1.6f; sun.shadows = LightShadows.None;
            RenderSettings.sun = sun; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.3f, 0.35f); RenderSettings.fog = false;
            var owner = new GameObject("Grass benchmark", typeof(GrassRenderer), typeof(GrassBenchmark));
            var grass = owner.GetComponent<GrassRenderer>();
            grass.ConfigureExperimental(field, profiles[0], camera, terrain, sun); grass.densityMap = density;
            var benchmark = owner.GetComponent<GrassBenchmark>(); benchmark.grass = grass; benchmark.benchmarkCamera = camera;
            benchmark.terrain = terrain; benchmark.profiles = profiles.ToArray(); benchmark.benchmarkPipeline = pipeline;
            benchmark.representativeField = field; benchmark.denseField = dense; benchmark.SetPose(0);
            EditorSceneManager.SaveScene(fixture, ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(fixture, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"GrassFlow fixture created: {field.OccupiedCount:N0} occupied cells; {field.OccupancyBytes:N0} occupancy bytes; {profiles.Count} profiles. {ScenePath}");
    }

    private static GrassFieldData BakeOnce(string name, TerrainData terrain, Texture2D density, Vector4 model)
    {
        string path = Root + "/" + name + ".asset";
        var field = AssetDatabase.LoadAssetAtPath<GrassFieldData>(path);
        if (field) return field;
        field = ScriptableObject.CreateInstance<GrassFieldData>(); field.name = name; AssetDatabase.CreateAsset(field, path);
        GrassFieldBaker.Bake(field, terrain, density, 0.067f, 1729, 0.1f, true, model);
        return field;
    }
    private static GrassExperimentProfile CreateProfile(GrassDrawPath path, GrassShaderPreset preset)
    {
        string name = path + "_" + preset;
        string asset = Root + "/Profiles/" + name + ".asset";
        var profile = AssetDatabase.LoadAssetAtPath<GrassExperimentProfile>(asset);
        if (profile) return profile;
        profile = ScriptableObject.CreateInstance<GrassExperimentProfile>(); profile.name = name; profile.drawPath = path; profile.preset = preset;
        const string shaders = "Assets/Grass/Assets/Shaders/Experimental/";
        profile.lod0Shader = AssetDatabase.LoadAssetAtPath<Shader>(shaders + "GrassLOD0.shader");
        profile.lod1Shader = AssetDatabase.LoadAssetAtPath<Shader>(shaders + "GrassLOD1.shader");
        profile.lod2Shader = AssetDatabase.LoadAssetAtPath<Shader>(shaders + "GrassLOD2.shader");
        profile.selectionShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(shaders + "GrassSelect.compute");
        AssetDatabase.CreateAsset(profile, asset); return profile;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
