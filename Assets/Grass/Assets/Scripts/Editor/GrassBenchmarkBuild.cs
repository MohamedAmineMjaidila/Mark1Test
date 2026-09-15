using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using GrassFlow;

public static class GrassBenchmarkBuild
{
    internal static bool StripExperiments;

    [MenuItem("Tools/GrassFlow/Build Android experiments")]
    public static void BuildExperiments() => Build(false);
    [MenuItem("Tools/GrassFlow/Build Android optimized release")]
    public static void BuildRelease() => Build(true);

    private static void Build(bool release)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            throw new BuildFailedException("Install Android Build Support, Android SDK/NDK tools and OpenJDK for Unity " + Application.unityVersion + " through Unity Hub.");
        GrassBenchmarkTools.CreateFixture();
        var target = NamedBuildTarget.Android;
        var oldBackend = PlayerSettings.GetScriptingBackend(target);
        var oldArchitectures = PlayerSettings.Android.targetArchitectures;
        var oldApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        bool oldDefaultApi = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
        var oldOrientation = PlayerSettings.defaultInterfaceOrientation;
        var serializedSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var resolutionScaling = serializedSettings.FindProperty("resolutionScalingMode");
        if (resolutionScaling == null) throw new BuildFailedException("Cannot locate the installed Unity resolution-scaling setting.");
        int oldScaling = resolutionScaling.intValue;
        bool oldTiming = PlayerSettings.enableFrameTimingStats;
        try
        {
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            resolutionScaling.intValue = 0; // Disabled, as serialized by this Unity version.
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.enableFrameTimingStats = true;
            StripExperiments = release;
            System.IO.Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { GrassBenchmarkTools.ScenePath }, target = BuildTarget.Android,
                locationPathName = release ? "Builds/Android/GrassFlow-optimized.apk" : "Builds/Android/GrassFlow-experiments.apk",
                // Experiments still use a non-development player so instrumentation does not invalidate timings.
                options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException(report.summary.result.ToString());
            Debug.Log($"GrassFlow build: {report.summary.outputPath}; {report.summary.totalSize:N0} bytes.");
        }
        finally
        {
            StripExperiments = false;
            PlayerSettings.SetScriptingBackend(target, oldBackend); PlayerSettings.Android.targetArchitectures = oldArchitectures;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, oldApis); PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, oldDefaultApi);
            PlayerSettings.defaultInterfaceOrientation = oldOrientation;
            serializedSettings.Update(); resolutionScaling.intValue = oldScaling; serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.enableFrameTimingStats = oldTiming;
        }
    }
}

public sealed class GrassShaderBuildFilter : IPreprocessShaders, IPreprocessComputeShaders, IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (!GrassBenchmarkBuild.StripExperiments || !shader.name.StartsWith("GrassFlow/Experimental/", StringComparison.Ordinal)) return;
        for (int i = data.Count - 1; i >= 0; i--)
            if (data[i].shaderKeywordSet.GetShaderKeywords().Any(keyword => keyword.name.StartsWith("GRASS_TEST_", StringComparison.Ordinal))) data.RemoveAt(i);
    }
    public void OnProcessComputeShader(ComputeShader shader, string kernel, IList<ShaderCompilerData> data)
    {
        if (!GrassBenchmarkBuild.StripExperiments || shader.name != "GrassSelect") return;
        for (int i = data.Count - 1; i >= 0; i--)
            if (data[i].shaderKeywordSet.GetShaderKeywords().Any(keyword => keyword.name == "GRASS_MATH_HEIGHT")) data.RemoveAt(i);
    }
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || !GrassBenchmarkBuild.StripExperiments) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var benchmark in root.GetComponentsInChildren<GrassBenchmark>(true))
            {
                benchmark.profiles = benchmark.profiles.Where(profile => profile.preset == GrassShaderPreset.Optimized).ToArray();
                benchmark.selectedProfile = 0;
            }
    }
}
