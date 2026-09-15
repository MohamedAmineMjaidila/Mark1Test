using System.IO;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace GrassFlow.Tests
{
    public static class GrassTestRunner
    {
        private static TestRunnerApi api;
        [UnityEditor.MenuItem("Tools/GrassFlow/Run correctness tests")]
        public static void Run()
        {
            // Unity's runner opens a blocking native save dialog otherwise. Let the caller save first.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("Save open scene changes before running GrassFlow tests.");
                    return;
                }
            Directory.CreateDirectory("Logs/GrassValidation");
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "Grass.Experimental.Tests" } }));
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, "Logs/GrassValidation/EditMode.xml");
                File.WriteAllText("Logs/GrassValidation/EditMode-summary.txt", $"Result: {result.TestStatus}\nPassed: {result.PassCount}\nFailed: {result.FailCount}\nSkipped: {result.SkipCount}\n");
                Debug.Log($"GrassFlow tests: {result.PassCount} passed, {result.FailCount} failed. Logs/GrassValidation/EditMode.xml");
                api.UnregisterCallbacks(this);
                Object.DestroyImmediate(api); api = null;
            }
        }
    }
}
