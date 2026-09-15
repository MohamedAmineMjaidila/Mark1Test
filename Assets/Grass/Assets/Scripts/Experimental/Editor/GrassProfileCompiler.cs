using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GrassFlow.Editor
{
    [InitializeOnLoad]
    public static class GrassProfileCompiler
    {
        public const string GeneratedFolder = "Assets/Grass/Generated/Shaders";
        private static readonly ConcurrentQueue<GrassExperimentProfile> Pending = new ConcurrentQueue<GrassExperimentProfile>();
        static GrassProfileCompiler()
        {
            // OnValidate can run on the loading thread. AssetDatabase work only runs on Editor update.
            GrassExperimentProfile.Validated += profile => Pending.Enqueue(profile);
            EditorApplication.update += ProcessQueue;
            EditorApplication.delayCall += CompileAll;
            Undo.undoRedoPerformed += CompileAll;
        }
        private static void ProcessQueue()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            while (Pending.TryDequeue(out var profile)) if (profile) Compile(profile);
        }
        [MenuItem("Tools/GrassFlow/Compile profile algorithms")]
        public static void CompileAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GrassExperimentProfile"))
                Compile(AssetDatabase.LoadAssetAtPath<GrassExperimentProfile>(AssetDatabase.GUIDToAssetPath(guid)));
            AssetDatabase.SaveAssets();
        }
        public static void Compile(GrassExperimentProfile profile)
        {
            if (!profile || !AssetDatabase.Contains(profile)) return;
            profile.EnsureAlgorithms();
            string key = profile.algorithms.Key;
            if (profile.compiledAlgorithmKey == key && profile.lod0Shader && profile.lod1Shader && profile.lod2Shader &&
                profile.lod0Shader.name.StartsWith("GrassFlow/Generated/", StringComparison.Ordinal)) { if (!profile.algorithmsReady) { profile.algorithmsReady = true; EditorUtility.SetDirty(profile); } return; }
            string hash = Hash128.Compute("GrassFlow-specialization-v1;" + key).ToString();
            Directory.CreateDirectory(GeneratedFolder);
            var shaders = new Shader[3];
            for (int lod = 0; lod < 3; lod++)
            {
                string path = $"{GeneratedFolder}/{hash}_LOD{lod}.shader";
                var source = new StringBuilder();
                source.AppendLine($"Shader \"GrassFlow/Generated/{hash}/LOD{lod}\" {{ SubShader {{ Tags {{ \"RenderType\"=\"Opaque\" \"Queue\"=\"Geometry\" \"RenderPipeline\"=\"UniversalPipeline\" }} Pass {{");
                source.AppendLine("Tags { \"LightMode\"=\"UniversalForward\" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM");
                source.AppendLine("#pragma target 4.5\n#pragma vertex GrassVertex\n#pragma fragment GrassFragment\n#pragma multi_compile_local _ GRASS_DIRECT_GRID");
                source.AppendLine($"#define GRASS_LOD {lod}");
                foreach (string define in profile.algorithms.Defines()) source.AppendLine("#define " + define + " 1");
                source.AppendLine("#include \"Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl\"\nENDHLSL\n} } }");
                string shaderSource = profile.algorithms.model == GrassRendererModel.LegacyReference ? GrassLegacyShaderSource.Build(hash, lod) : source.ToString();
                if (!File.Exists(path) || File.ReadAllText(path) != shaderSource)
                {
                    File.WriteAllText(path, shaderSource);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
                shaders[lod] = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (!shaders[lod] || ShaderUtil.ShaderHasError(shaders[lod])) throw new InvalidOperationException("Grass shader failed: " + path);
            }
            profile.lod0Shader = shaders[0]; profile.lod1Shader = shaders[1]; profile.lod2Shader = shaders[2];
            profile.selectionShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Grass/Assets/Shaders/Experimental/GrassSelect.compute");
            profile.compiledAlgorithmKey = key; profile.algorithmsReady = true;
            profile.revision++;
            EditorUtility.SetDirty(profile);
        }
    }
    public sealed class GrassProfileBuildPreparation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) { GrassProfileCompiler.CompileAll(); }
    }
}
