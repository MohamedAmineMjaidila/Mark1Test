#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

/// <summary>Explicit short Editor diagnostic; never included in players or attached to saved scenes.</summary>
public sealed class GrassSceneProbe : MonoBehaviour
{
    public GrassRenderer grass;
    public string label = "scene";
    public int warmupFrames = 120, sampleFrames = 300;
    private int frame, count, gpuCount;
    private double[] frameMs, gpuMs, submitMs;
    private readonly FrameTiming[] timing = new FrameTiming[1];
    private ulong lastTimestamp;
    private long maxAllocated;
    private void Start()
    {
        frameMs = new double[sampleFrames]; gpuMs = new double[sampleFrames]; submitMs = new double[sampleFrames];
    }
    private void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        if (frame++ < warmupFrames) return;
        frameMs[count] = Time.unscaledDeltaTime * 1000.0;
        submitMs[count] = grass.Statistics.cpuMilliseconds;
        maxAllocated = Math.Max(maxAllocated, grass.Statistics.managedBytes);
        if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0 && timing[0].frameStartTimestamp != lastTimestamp)
        { lastTimestamp = timing[0].frameStartTimestamp; gpuMs[gpuCount++] = timing[0].gpuFrameTime; }
        if (++count < sampleFrames) return;
        Array.Sort(frameMs); Array.Sort(submitMs); Array.Sort(gpuMs, 0, gpuCount);
        var data = new Report { label=label, frames=count, gpuSamples=gpuCount,
            frameMedianMs=frameMs[count/2], frameP95Ms=frameMs[(int)(count*.95)],
            gpuMedianMs=gpuCount > 0 ? gpuMs[gpuCount/2] : -1,
            submitMedianMs=submitMs[count/2], submitP95Ms=submitMs[(int)(count*.95)],
            maxGrassManagedBytes=maxAllocated, mode=grass.Mode.ToString(), profile=grass.Mode == GrassRenderer.RenderingMode.ImmutableField && grass.Experiment ? grass.Experiment.name : "Legacy",
            width=Screen.width,height=Screen.height,bufferBytes=grass.Statistics.bufferBytes,candidateTiles=grass.Statistics.candidateTiles,
            device=SystemInfo.graphicsDeviceName, targetFps=Application.targetFrameRate, vsync=QualitySettings.vSyncCount };
        Directory.CreateDirectory("Logs/GrassValidation");
        File.WriteAllText("Logs/GrassValidation/"+label+".json", JsonUtility.ToJson(data,true));
        ScreenCapture.CaptureScreenshot("Logs/GrassValidation/"+label+".png");
        Debug.Log("GrassFlow short diagnostic: " + JsonUtility.ToJson(data));
        Destroy(this);
    }
    [Serializable] private sealed class Report
    {
        public string label, mode, profile, device;
        public int frames,gpuSamples,width,height,candidateTiles,targetFps,vsync;
        public long maxGrassManagedBytes,bufferBytes;
        public double frameMedianMs,frameP95Ms,gpuMedianMs,submitMedianMs,submitP95Ms;
    }
}
#endif
