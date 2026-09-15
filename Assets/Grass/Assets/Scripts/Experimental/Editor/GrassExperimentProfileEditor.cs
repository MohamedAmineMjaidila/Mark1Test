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
            using (new EditorGUI.DisabledScope(!EditorUtility.IsPersistent(profile)))
            {
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
            var algorithms = serializedObject.FindProperty("algorithms");
            algorithms.isExpanded = EditorGUILayout.Foldout(algorithms.isExpanded, "Algorithms", true);
            if (algorithms.isExpanded)
            {
                EditorGUILayout.PropertyField(algorithms.FindPropertyRelative("model"));
                if ((GrassRendererModel)algorithms.FindPropertyRelative("model").enumValueIndex == GrassRendererModel.LegacyReference)
                    EditorGUILayout.HelpBox("Full reference deformation, lighting, shadows and depth passes. Modular sub-algorithms do not run in this model. Choose Modular to mix those algorithms; Legacy Texture wind is available there too.", MessageType.Info);
                else
                {
                    var child = algorithms.Copy(); var end = algorithms.GetEndProperty(); child.NextVisible(true);
                    do
                    {
                        if (SerializedProperty.EqualContents(child,end)) break;
                        if (child.name != "model" && IsAlgorithmRelevant(child.name, profile))
                            EditorGUILayout.PropertyField(child, true);
                    } while (child.NextVisible(false));
                }
            }
            }
            var iterator = serializedObject.GetIterator();
            bool enter = true;
            while (iterator.NextVisible(enter))
            {
                enter = false;
                if (iterator.name == "m_Script" || iterator.name == "drawPath" || iterator.name == "algorithms") continue;
                if (!IsRelevant(iterator.name, profile)) continue;
                EditorGUILayout.PropertyField(iterator, true);
            }
            if (serializedObject.ApplyModifiedProperties())
            {
                profile.NotifyChanged(); EditorUtility.SetDirty(profile);
                EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
            }
            bool ready = profile.lod0Shader && profile.compiledAlgorithmKey == profile.algorithms.Key;
            EditorGUILayout.HelpBox(ready ? (profile.algorithms.model == GrassRendererModel.LegacyReference ? "Legacy reference shaders ready: fragment lighting on every LOD, main-light shadow reception, optional casting and depth passes." : "Specialized shaders ready. Near/middle/far use separate programs. Shadows are currently disabled for grass.")
                : "Waiting for automatic shader specialization. The Editor may pause briefly on first use of a combination.", ready ? MessageType.Info : MessageType.Warning);
            if (profile.UsesLegacySelection && profile.lod0Distance >= profile.lod1Distance)
                EditorGUILayout.HelpBox("The captured legacy distances make LOD1 unreachable. Its segment slider cannot affect this view until LOD0 Distance is smaller than LOD1 Distance. The full configuration preserves the original values deliberately.", MessageType.Warning);
            if (profile.algorithms.model == GrassRendererModel.Modular && profile.algorithms.farLighting == GrassFarLighting.Vertex)
                EditorGUILayout.HelpBox("Far lighting runs at vertices. Fragment normal and hash choices are evaluated/approximated at vertices there; choose Far Lighting = Fragment to compare them per pixel. Specular is near-only; transmission is near/middle-only.", MessageType.None);
            if (GUILayout.Button("Open GrassFlow tuning guide")) OpenGuide();
        }
        private static bool IsAlgorithmRelevant(string name, GrassExperimentProfile profile)
        {
            if (name == "height") return profile.UsesHeightSamples;
            if (profile.algorithms.diffuse == GrassDiffuse.Unlit)
                switch (name) { case "normals": case "view": case "ambient": case "transmission": case "specular": case "stabilization": case "ambientOcclusion": return false; }
            if (name == "view") return profile.algorithms.specular != GrassSpecular.Off || profile.algorithms.transmission == GrassTransmission.Power;
            return true;
        }
        private static bool IsRelevant(string name, GrassExperimentProfile profile)
        {
            var a = profile.algorithms;
            if (name == "collectCounters") return profile.drawPath == GrassDrawPath.CompactedIds;
            if (name == "counterInterval") return profile.drawPath == GrassDrawPath.CompactedIds && profile.collectCounters;
            if (name == "maximumWidthScale") return !profile.UsesLegacySelection && profile.widthCompensation > 0;
            if (name == "transitionWidth") return !profile.UsesLegacySelection;
            if (name == "bladeHeights") return !profile.UsesLegacyField;
            if (a.model == GrassRendererModel.LegacyReference)
            {
                switch (name)
                {
                    case "lod0Distance": case "lod1Distance": case "maxDrawDistance":
                    case "lod0Segments": case "lod1Segments": case "lod2Segments":
                    case "bladeWidth": case "naturalBend": case "densityFalloffStart": case "minimumDensity": case "widthCompensation":
                    case "gustStrength": case "flutterStrength": case "legacyNoiseFrequency":
                    case "rootColor": case "midColor": case "tipColor": case "dryColor": case "colorVariation": case "dryness":
                    case "roundnessRadians": case "rootDarkness": case "transmission": case "transmissionPower": case "specular": case "specularPower":
                    case "legacyWindMap": case "legacyWindVelocity": case "legacyWindFrequency": case "legacyWindDisableDistance":
                    case "legacyHeightMultiplier": case "legacyCameraFacing": case "legacyShadowSoftness": case "legacyCastShadows":
                        return true;
                    case "legacyCastShadowsLod1": case "legacyCastShadowsLod2": return profile.legacyCastShadows;
                    default: return false;
                }
            }
            if (name == "legacyHeightMultiplier") return profile.UsesLegacyField;
            if (name == "legacyWindMap" || name == "legacyWindVelocity" || name == "legacyWindFrequency" || name == "legacyWindDisableDistance") return a.wind == GrassWind.LegacyTexture;
            if (name.StartsWith("legacy") && name != "legacyNoiseFrequency") return false;
            switch (name)
            {
                case "specularPower": return a.specular == GrassSpecular.Power && a.diffuse != GrassDiffuse.Unlit;
                case "transmissionPower": return a.transmission == GrassTransmission.Power && a.diffuse != GrassDiffuse.Unlit;
                case "specular": return a.specular != GrassSpecular.Off && a.diffuse != GrassDiffuse.Unlit;
                case "transmission": return a.transmission != GrassTransmission.Off && a.diffuse != GrassDiffuse.Unlit;
                case "widening": return a.viewWidening;
                case "roundnessRadians": return a.normals != GrassNormal.Flat && a.diffuse != GrassDiffuse.Unlit;
                case "normalStabilization": return a.stabilization != GrassStabilization.Off && a.diffuse != GrassDiffuse.Unlit;
                case "skyAmbient": case "groundAmbient": return a.ambient == GrassAmbient.Hemisphere && a.diffuse != GrassDiffuse.Unlit;
                case "aoCurveWeights": return a.ambientOcclusion == GrassAO.WeightedCurves && a.diffuse != GrassDiffuse.Unlit;
                case "rootDarkness": return a.diffuse != GrassDiffuse.Unlit;
                case "gustStrength": case "flutterStrength": return a.wind != GrassWind.Off;
                case "waveB": case "secondWaveWeight": return a.wind == GrassWind.TwoWaves;
                case "stiffnessRange": return a.wind != GrassWind.Off && !(a.wind == GrassWind.LegacyTexture && profile.UsesLegacyField);
                case "windDirection": case "windStrength": case "windSpeed": case "waveA": case "gustWave": return a.wind != GrassWind.Off && a.wind != GrassWind.LegacyTexture;
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
