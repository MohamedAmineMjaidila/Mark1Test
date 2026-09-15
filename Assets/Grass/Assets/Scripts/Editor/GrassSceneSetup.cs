using System.IO;
using GrassFlow;
using GrassFlow.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GrassSceneSetup
{
    public static void Configure(GrassRenderer grass)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode to bake field assets."); return; }
        var so = new SerializedObject(grass);
        var terrain = so.FindProperty("terrain").objectReferenceValue as Terrain;
        if (!terrain) { Debug.LogError("Assign the terrain first.", grass); return; }
        string folder = "Assets/Grass/Scenes/" + grass.gameObject.scene.name;
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        string fieldPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + grass.name + " Field.asset");
        var field = ScriptableObject.CreateInstance<GrassFieldData>(); AssetDatabase.CreateAsset(field, fieldPath);
        // Match the old grid's cell density without altering the user's terrain dimensions or painted mask.
        int side = Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(so.FindProperty("grassCount").intValue)));
        float spacing = terrain.terrainData.size.x / side;
        GrassFieldBaker.Bake(field, terrain.terrainData, grass.densityMap, spacing, grass.bakeSeed, grass.densityThreshold);
        var profile = ScriptableObject.CreateInstance<GrassExperimentProfile>();
        profile.ApplyRecipe(GrassShaderPreset.Optimized);
        profile.name = grass.name + " Tuning";
        profile.bladeHeights = new Vector2(so.FindProperty("minGrassHeight").floatValue, so.FindProperty("maxGrassHeight").floatValue);
        profile.minimumDensity = so.FindProperty("enableDensityScaling").boolValue ? so.FindProperty("minDensity").floatValue : 1;
        profile.densityFalloffStart = Mathf.Min(so.FindProperty("densityFalloffStart").floatValue, profile.maxDrawDistance - 0.001f);
        profile.widthCompensation = so.FindProperty("widthCompensation").floatValue;
        var material = so.FindProperty("grassMaterial").objectReferenceValue as Material;
        if (material)
        {
            if (material.HasProperty("_BladeWidth")) profile.bladeWidth = material.GetFloat("_BladeWidth");
            if (material.HasProperty("_BladeHeight")) profile.bladeHeights *= material.GetFloat("_BladeHeight");
            profile.rootColor = material.GetColor("_RootColor"); profile.midColor = material.GetColor("_MidColor");
            profile.tipColor = material.GetColor("_TipColor"); profile.dryColor = material.GetColor("_DryColor");
            if (material.HasProperty("_BladeBend")) profile.naturalBend = material.GetFloat("_BladeBend");
        }
        AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + profile.name + ".asset"));
        GrassProfileCompiler.Compile(profile);
        Undo.RecordObject(grass, "Enable configurable immutable grass");
        grass.bakeCellSpacing = spacing;
        grass.ConfigureExperimental(field, profile, grass.RenderingCamera ? grass.RenderingCamera : Camera.main, terrain, RenderSettings.sun);
        so.Update(); so.FindProperty("showDebugUI").boolValue = false; so.ApplyModifiedProperties();
        EditorUtility.SetDirty(grass); EditorSceneManager.MarkSceneDirty(grass.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"GrassFlow configured: {field.Columns} x {field.Rows}, {field.OccupiedCount:N0} occupied, spacing {spacing:R}. Profile: {AssetDatabase.GetAssetPath(profile)}", grass);
    }
}
