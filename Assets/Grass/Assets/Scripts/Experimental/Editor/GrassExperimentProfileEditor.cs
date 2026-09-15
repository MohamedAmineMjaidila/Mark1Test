using UnityEditor;
using UnityEngine;

namespace GrassFlow.Editor
{
    [CustomEditor(typeof(GrassExperimentProfile))]
    public sealed class GrassExperimentProfileEditor : UnityEditor.Editor
    {
        private GrassShaderPreset recipe;
        private void OnEnable()
        {
            var profile = (GrassExperimentProfile)target;
            profile.EnsureAlgorithms();
            if (EditorUtility.IsPersistent(profile) && (!profile.algorithmsReady || !profile.lod0Shader)) profile.NotifyChanged();
        }
        public override void OnInspectorGUI()
        {
            var profile = (GrassExperimentProfile)target;
            profile.EnsureAlgorithms();
            serializedObject.Update();
            EditorGUILayout.HelpBox(EditorUtility.IsPersistent(profile)
                ? "Editing this asset persists after Play Mode. Duplicate it first to keep an A/B comparison. Algorithm dropdowns compile automatically; numeric changes update live."
                : "Runtime benchmark copy. Edit the source profile asset to persist changes; the benchmark synchronizes it and stops an active measurement.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            recipe = (GrassShaderPreset)EditorGUILayout.EnumPopup("Starting recipe", recipe);
            if (GUILayout.Button("Apply algorithms", GUILayout.Width(120)))
            {
                Undo.RecordObject(profile, "Apply grass algorithm recipe");
                profile.ApplyRecipe(recipe); EditorUtility.SetDirty(profile); serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Recipes only fill the independent dropdowns below. You can combine choices immediately afterwards. Numeric settings are preserved.", MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("drawPath"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("algorithms"), true);
            var iterator = serializedObject.GetIterator();
            bool enter = true;
            while (iterator.NextVisible(enter))
            {
                enter = false;
                if (iterator.name == "m_Script" || iterator.name == "drawPath" || iterator.name == "algorithms") continue;
                if (!IsRelevant(iterator.name, profile.algorithms)) continue;
                EditorGUILayout.PropertyField(iterator, true);
            }
            serializedObject.ApplyModifiedProperties();
            bool ready = profile.lod0Shader && profile.compiledAlgorithmKey == profile.algorithms.Key;
            EditorGUILayout.HelpBox(ready ? "Specialized shaders ready. Near/middle/far use separate programs. Shadows are currently disabled for grass."
                : "Waiting for automatic shader specialization. The Editor may pause briefly on first use of a combination.", ready ? MessageType.Info : MessageType.Warning);
            if (profile.algorithms.farLighting == GrassFarLighting.Vertex)
                EditorGUILayout.HelpBox("Far lighting runs at vertices. Fragment normal and hash choices are evaluated/approximated at vertices there; choose Far Lighting = Fragment to compare them per pixel. Specular is near-only; transmission is near/middle-only.", MessageType.None);
            if (GUILayout.Button("Open GrassFlow tuning guide")) OpenGuide();
        }
        private static bool IsRelevant(string name, GrassAlgorithms a)
        {
            switch (name)
            {
                case "specularPower": return a.specular == GrassSpecular.Power && a.diffuse != GrassDiffuse.Unlit;
                case "transmissionPower": return a.transmission == GrassTransmission.Power && a.diffuse != GrassDiffuse.Unlit;
                case "specular": return a.specular != GrassSpecular.Off && a.diffuse != GrassDiffuse.Unlit;
                case "transmission": return a.transmission != GrassTransmission.Off && a.diffuse != GrassDiffuse.Unlit;
                case "widening": return a.viewWidening;
                case "roundnessRadians": return a.normals != GrassNormal.Flat;
                case "normalStabilization": return a.stabilization != GrassStabilization.Off;
                case "skyAmbient": case "groundAmbient": return a.ambient == GrassAmbient.Hemisphere && a.diffuse != GrassDiffuse.Unlit;
                case "aoCurveWeights": return a.ambientOcclusion == GrassAO.WeightedCurves && a.diffuse != GrassDiffuse.Unlit;
                case "rootDarkness": return a.diffuse != GrassDiffuse.Unlit;
                case "waveB": case "secondWaveWeight": return a.wind == GrassWind.TwoWaves;
                case "windDirection": case "windStrength": case "windSpeed": case "gustStrength": case "flutterStrength": case "waveA": case "gustWave": case "stiffnessRange": return a.wind != GrassWind.Off;
                case "colorPatchWeight": return a.colorVariation == GrassVariation.SmoothPatchAndBlade;
                case "drynessPatchWeight": return a.dryness == GrassVariation.SmoothPatchAndBlade;
                case "patchSizeCells": return a.colorVariation == GrassVariation.SmoothPatchAndBlade || a.dryness == GrassVariation.SmoothPatchAndBlade;
                case "legacyNoiseFrequency": return a.colorVariation == GrassVariation.LegacyFragmentHash || a.dryness == GrassVariation.LegacyFragmentHash;
                default: return true;
            }
        }
        [MenuItem("Tools/GrassFlow/Open tuning guide")]
        public static void OpenGuide() { Application.OpenURL("file://" + System.IO.Path.GetFullPath("Assets/Grass/README.md")); }
    }
}
