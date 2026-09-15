using System;
using System.IO;
using GrassFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Explicit, synchronous Editor diagnostics. Never installed in a player or called per frame.</summary>
public static class GrassVisualValidation
{
    private const string Folder = "Logs/GrassValidation/";

    public static string CompareLegacy(GrassRenderer source, GrassConfiguration configuration)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run this comparison in Play Mode so both renders share Time.time.");
        Directory.CreateDirectory(Folder);
        var a = CopyCamera(source.RenderingCamera, "Original reference capture");
        var b = CopyCamera(source.RenderingCamera, "Immutable reference capture");
        var owner = new GameObject("Temporary legacy comparison renderer") { hideFlags = HideFlags.HideAndDontSave };
        bool enabled = source.enabled;
        try
        {
            source.enabled = false;
            owner.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var legacy = owner.AddComponent<GrassRenderer>();
            EditorUtility.CopySerialized(source, legacy);
            legacy.enabled = false; legacy.showInSceneView = false;
            var so = new SerializedObject(legacy);
            so.FindProperty("mainCamera").objectReferenceValue = a;
            so.ApplyModifiedPropertiesWithoutUndo();
            legacy.SelectLegacyReference();
            legacy.SendMessage("LateUpdate");
            using (var renderer = new GrassFieldRenderer(configuration.field, configuration.profileTemplate))
            {
                var terrain = so.FindProperty("terrain").objectReferenceValue as Terrain;
                renderer.Render(b, terrain ? terrain.transform.position : source.transform.position, RenderSettings.sun, Time.time);
                var original = Capture(a); var immutable = Capture(b);
                try
                {
                    File.WriteAllBytes(Folder + "Legacy-Original.png", original.EncodeToPNG());
                    File.WriteAllBytes(Folder + "Legacy-Immutable.png", immutable.EncodeToPNG());
                    var pixelsA = original.GetPixels32(); var pixelsB = immutable.GetPixels32();
                    double absolute = 0; int changed = 0, maximum = 0;
                    for (int i = 0; i < pixelsA.Length; i++)
                    {
                        int dr = Math.Abs(pixelsA[i].r-pixelsB[i].r), dg = Math.Abs(pixelsA[i].g-pixelsB[i].g), db = Math.Abs(pixelsA[i].b-pixelsB[i].b);
                        absolute += dr + dg + db; maximum = Math.Max(maximum, Math.Max(dr, Math.Max(dg,db)));
                        if (Math.Max(dr,Math.Max(dg,db)) > 2) changed++;
                    }
                    string report = $"Same-frame Time.time={Time.time}; {original.width}x{original.height}; RGB mean absolute error={absolute / (pixelsA.Length * 3):F6}/255; pixels with channel difference >2={changed}/{pixelsA.Length}; maximum channel error={maximum}/255";
                    File.WriteAllText(Folder + "Legacy-PixelComparison.txt",report); return report;
                }
                finally { Object.DestroyImmediate(original); Object.DestroyImmediate(immutable); }
            }
        }
        finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(a.gameObject); Object.DestroyImmediate(b.gameObject); source.enabled = enabled; }
    }

    public static string CaptureScenePreview(GrassRenderer source)
    {
        Directory.CreateDirectory(Folder);
        var camera = CopyCamera(source.RenderingCamera, "Scene preview validation");
        camera.cameraType = CameraType.SceneView;
        bool previous = source.showInSceneView;
        try
        {
            source.showInSceneView = false;
            var empty = Capture(camera);
            source.showInSceneView = true;
            var grass = Capture(camera);
            try
            {
                File.WriteAllBytes(Folder + "SceneView-NoGrass.png",empty.EncodeToPNG());
                File.WriteAllBytes(Folder + "SceneView-Grass.png",grass.EncodeToPNG());
                var a=empty.GetPixels32(); var b=grass.GetPixels32(); int changed=0;
                for (int i=0;i<a.Length;i++) if (!a[i].Equals(b[i])) changed++;
                string report = $"SceneView camera preview changed {changed}/{a.Length} pixels; own frustum and draw buffers.";
                File.WriteAllText(Folder + "SceneView-Validation.txt",report); return report;
            }
            finally { Object.DestroyImmediate(empty); Object.DestroyImmediate(grass); }
        }
        finally { source.showInSceneView = previous; source.ReleaseSceneViewPreview(camera); Object.DestroyImmediate(camera.gameObject); }
    }

    private static Camera CopyCamera(Camera source, string name)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>(); camera.CopyFrom(source);
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        if (source.TryGetComponent<UniversalAdditionalCameraData>(out var data)) EditorUtility.CopySerialized(data,go.AddComponent<UniversalAdditionalCameraData>());
        camera.targetTexture = null; camera.aspect = 1440f/2560f; camera.enabled = true;
        return camera;
    }
    private static Texture2D Capture(Camera camera)
    {
        var target = RenderTexture.GetTemporary(1440,2560,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var image = new Texture2D(target.width,target.height,TextureFormat.RGB24,false,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply(); return image;
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
    }
}
