using System;
using System.Globalization;
using System.IO;
using System.Text;
using GrassFlow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Repeatable portrait camera route and native-resolution telemetry. No character/gameplay dependency.</summary>
[DefaultExecutionOrder(-100)]
public sealed class GrassBenchmark : MonoBehaviour
{
    public GrassRenderer grass;
    public Camera benchmarkCamera;
    public Terrain terrain;
    public UniversalRenderPipelineAsset benchmarkPipeline;
    public GrassExperimentProfile[] profiles;
    [Tooltip("Profile retained by the optimized-release build. If empty, use Selected Profile from the array.")]
    public GrassExperimentProfile releaseProfile;
    public GrassFieldData representativeField, denseField;
    public bool denseStress;
    public bool captureCounters;
    public int selectedProfile;
    public float walkingSpeed = 2;
    public float cameraHeight = 12.642f;
    public float cameraPitch = 21.2f;
    public float warmupSeconds = 300;
    public float measurementSeconds = 900;
    public bool autoStart;
    public bool showControls = true;

    private struct Sample
    {
        public double elapsed, frameMs, gpuMs, grassCpuMs;
        public uint lod0, lod1, lod2, overflow;
        public int tiles;
        public bool countersValid;
        public long grassGcBytes;
    }
    [Serializable]
    private sealed class RunMetadata
    {
        public string utc, unityVersion, device, gpu, graphicsApi, profile, drawPath, preset, field, completion;
        public int width, height, sampleCount, missingGpuSamples, seed;
        public float renderScale, warmupSeconds, measurementSeconds;
        public long grassBufferBytes;
        public bool denseStress, countersEnabled;
        public long maximumGrassGcBytes;
        public double frameP50, frameP95, frameP99, gpuP95, grassCpuP95;
        public string gpuTimingNote = "GPU time is whole-frame timing when available. Grass GPU pass cost requires a GPU capture; zero/missing values are unavailable, not free.";
        public string acceptance = "Requires physical S23 sustained timings, grass GPU capture and native-resolution visual review.";
    }

    private RenderPipelineAsset previousPipeline;
    private UniversalRenderPipelineAsset runtimePipeline;
    private GrassExperimentProfile runtimeProfile;
    private int sourceProfileRevision = -1;
    private GrassExperimentProfile selectedSourceProfile;
    public GrassExperimentProfile ActiveRuntimeProfile => runtimeProfile;
    private int previousTargetFps, previousVsync;
    private bool running, initialized;
    private double started;
    private Sample[] samples;
    private int sampleCount;
    private ulong previousGpuTimestamp;
    private readonly FrameTiming[] timings = new FrameTiming[1];
    private string lastExport = "No run recorded.";
    public bool Running => running;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        initialized = true;
        previousPipeline = QualitySettings.renderPipeline;
        previousTargetFps = Application.targetFrameRate; previousVsync = QualitySettings.vSyncCount;
        if (benchmarkPipeline)
        {
            runtimePipeline = Instantiate(benchmarkPipeline);
            runtimePipeline.renderScale = 1;
            QualitySettings.renderPipeline = runtimePipeline;
        }
        Application.targetFrameRate = Application.platform == RuntimePlatform.Android ? 60 : -1; QualitySettings.vSyncCount = 0;
        if (benchmarkCamera) benchmarkCamera.allowDynamicResolution = false;
        if (Application.platform == RuntimePlatform.Android)
        {
            Screen.orientation = ScreenOrientation.Portrait;
            int shortSide = Mathf.Min(Display.main.systemWidth, Display.main.systemHeight);
            int longSide = Mathf.Max(Display.main.systemWidth, Display.main.systemHeight);
            Screen.SetResolution(shortSide, longSide, FullScreenMode.FullScreenWindow);
        }
        SelectProfile(selectedProfile);
        if (autoStart) BeginRun();
    }

    public void SelectProfile(int index)
    {
        if (running) EndRun("Stopped for profile switch");
        if (profiles == null || profiles.Length == 0 || !grass) return;
        selectedProfile = Mathf.Clamp(index, 0, profiles.Length - 1);
        if (runtimeProfile) Destroy(runtimeProfile);
        runtimeProfile = Instantiate(profiles[selectedProfile]); runtimeProfile.name = profiles[selectedProfile].name;
        selectedSourceProfile = profiles[selectedProfile];
        sourceProfileRevision = selectedSourceProfile.revision;
        runtimeProfile.collectCounters = captureCounters;
        if (denseStress) { runtimeProfile.minimumDensity = 1; runtimeProfile.maxDrawDistance = 150; }
        grass.SelectExperiment(runtimeProfile);
        if (denseStress && denseField) grass.SelectField(denseField);
        else if (representativeField) grass.SelectField(representativeField);
        SetPose(0);
    }

    public void BeginRun()
    {
        if (!grass || !grass.Field || !grass.Experiment || !benchmarkCamera || running) return;
        samples = new Sample[Mathf.CeilToInt(measurementSeconds * 240) + 1024];
        sampleCount = 0; previousGpuTimestamp = 0; started = Time.realtimeSinceStartupAsDouble;
        grass.useBenchmarkTime = true;
        running = true; showControls = false;
    }

    public void SetPose(float routeTime)
    {
        if (!benchmarkCamera || !grass || !grass.Field) return;
        Vector3 origin = terrain ? terrain.transform.position : grass.transform.position;
        float routeLength = Mathf.Max(1, grass.Field.TerrainSize.z - 20);
        float z = Mathf.PingPong(routeTime * walkingSpeed, routeLength);
        Vector3 position = origin + new Vector3(grass.Field.TerrainSize.x * 0.5f, cameraHeight, z);
        if (terrain) position.y += terrain.SampleHeight(position);
        benchmarkCamera.transform.SetPositionAndRotation(position, Quaternion.Euler(cameraPitch, 0, 0));
        grass.useBenchmarkTime = true; grass.benchmarkTime = routeTime;
    }

    private void Update()
    {
        if (profiles != null && profiles.Length > 0)
        {
            selectedProfile = Mathf.Clamp(selectedProfile, 0, profiles.Length - 1);
            var source = profiles[selectedProfile];
            if (source && source != selectedSourceProfile) SelectProfile(selectedProfile);
            else if (source && source.revision != sourceProfileRevision)
            {
                if (running) EndRun("Stopped for profile edit");
                // Reuse the runtime object, so numeric edits do not replace resident field buffers.
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), runtimeProfile);
                runtimeProfile.collectCounters = captureCounters;
                if (denseStress) { runtimeProfile.minimumDensity = 1; runtimeProfile.maxDrawDistance = 150; }
                runtimeProfile.revision++;
                sourceProfileRevision = source.revision;
            }
        }
        if (!running) return;
        float elapsed = (float)(Time.realtimeSinceStartupAsDouble - started);
        // Measurement route begins at zero after warm-up, independent of compilation/startup duration.
        SetPose(elapsed < warmupSeconds ? elapsed : elapsed - warmupSeconds);
        FrameTimingManager.CaptureFrameTimings();
        if (elapsed < warmupSeconds) return;
        if (elapsed >= warmupSeconds + measurementSeconds || sampleCount >= samples.Length)
        {
            EndRun(sampleCount >= samples.Length ? "Sample capacity reached" : "Completed");
            return;
        }
        uint timingCount = FrameTimingManager.GetLatestTimings(1, timings);
        double gpu = double.NaN;
        if (timingCount > 0 && timings[0].gpuFrameTime > 0 && timings[0].frameStartTimestamp != previousGpuTimestamp)
        {
            gpu = timings[0].gpuFrameTime; previousGpuTimestamp = timings[0].frameStartTimestamp;
        }
        GrassRenderStatistics stats = grass.Statistics;
        samples[sampleCount++] = new Sample { elapsed = elapsed - warmupSeconds, frameMs = Time.unscaledDeltaTime * 1000.0,
            gpuMs = gpu, grassCpuMs = stats.cpuMilliseconds, lod0 = stats.lod0, lod1 = stats.lod1, lod2 = stats.lod2,
            overflow = stats.overflow, tiles = stats.candidateTiles, countersValid = stats.countersValid, grassGcBytes = stats.managedBytes };
    }

    public void EndRun(string completion = "Stopped manually")
    {
        if (!running) return;
        running = false; showControls = true;
        string directory = Path.Combine(Application.persistentDataPath, "GrassBenchmarks", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        var csv = new StringBuilder(sampleCount * 100);
        csv.AppendLine("elapsed_s,frame_ms,gpu_frame_ms,grass_cpu_ms,lod0,lod1,lod2,overflow,candidate_tiles,counters_valid,grass_gc_bytes");
        double[] frames = new double[sampleCount], cpus = new double[sampleCount], gpus = new double[sampleCount];
        int gpuCount = 0;
        long maxGc = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            Sample s = samples[i]; frames[i] = s.frameMs; cpus[i] = s.grassCpuMs;
            maxGc = Math.Max(maxGc, s.grassGcBytes);
            if (!double.IsNaN(s.gpuMs)) gpus[gpuCount++] = s.gpuMs;
            csv.AppendFormat(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2},{3:F4},{4},{5},{6},{7},{8},{9},{10}\n", s.elapsed, s.frameMs,
                double.IsNaN(s.gpuMs) ? "" : s.gpuMs.ToString("F4", CultureInfo.InvariantCulture), s.grassCpuMs,
                s.countersValid ? s.lod0.ToString() : "", s.countersValid ? s.lod1.ToString() : "", s.countersValid ? s.lod2.ToString() : "",
                s.countersValid ? s.overflow.ToString() : "", s.tiles, s.countersValid, s.grassGcBytes);
        }
        Array.Sort(frames); Array.Sort(cpus); Array.Sort(gpus, 0, gpuCount);
        var metadata = new RunMetadata { utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion, device = SystemInfo.deviceModel,
            gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), profile = grass.Experiment.name,
            drawPath = grass.Experiment.drawPath.ToString(), preset = grass.Experiment.algorithms.Key, field = grass.Field.name, seed = grass.Field.Seed,
            completion = completion, width = Screen.width, height = Screen.height, renderScale = runtimePipeline ? runtimePipeline.renderScale : 0,
            warmupSeconds = warmupSeconds, measurementSeconds = measurementSeconds, sampleCount = sampleCount, missingGpuSamples = sampleCount - gpuCount,
            grassBufferBytes = grass.Statistics.bufferBytes, denseStress = denseStress, countersEnabled = captureCounters, maximumGrassGcBytes = maxGc,
            frameP50 = Percentile(frames, sampleCount, 0.5), frameP95 = Percentile(frames, sampleCount, 0.95),
            frameP99 = Percentile(frames, sampleCount, 0.99), gpuP95 = Percentile(gpus, gpuCount, 0.95), grassCpuP95 = Percentile(cpus, sampleCount, 0.95) };
        File.WriteAllText(Path.Combine(directory, "frames.csv"), csv.ToString());
        File.WriteAllText(Path.Combine(directory, "run.json"), JsonUtility.ToJson(metadata, true));
        File.WriteAllText(Path.Combine(directory, "profile.json"), JsonUtility.ToJson(grass.Experiment, true));
        lastExport = directory;
        Debug.Log("GrassFlow benchmark exported: " + directory);
        samples = null;
    }

    private static double Percentile(double[] values, int count, double percentile) => count == 0 ? -1 : values[Math.Min(count - 1, (int)Math.Ceiling(count * percentile) - 1)];

    private void OnDisable()
    {
        if (!initialized) return;
        initialized = false;
        if (running) EndRun("Component disabled");
        if (grass) grass.useBenchmarkTime = false;
        if (runtimePipeline)
        {
            QualitySettings.renderPipeline = previousPipeline;
            Destroy(runtimePipeline); runtimePipeline = null;
        }
        Application.targetFrameRate = previousTargetFps; QualitySettings.vSyncCount = previousVsync;
        if (runtimeProfile) { Destroy(runtimeProfile); runtimeProfile = null; }
    }

    private void OnGUI()
    {
        if (!showControls || !grass || !grass.Experiment) return;
        float scale = Mathf.Max(1, Screen.width / 500f);
        Matrix4x4 previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        GUILayout.BeginArea(new Rect(12, 12, 460, 280), GUI.skin.box);
        GUILayout.Label("GrassFlow · Native resolution · 60 FPS target");
        GUILayout.Label(grass.Experiment.name);
        GUILayout.Label($"{Screen.width} × {Screen.height} · {SystemInfo.graphicsDeviceType}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Previous", GUILayout.Height(35))) SelectProfile((selectedProfile - 1 + profiles.Length) % profiles.Length);
        if (GUILayout.Button("Next", GUILayout.Height(35))) SelectProfile((selectedProfile + 1) % profiles.Length);
        GUILayout.EndHorizontal();
        bool nextDense = GUILayout.Toggle(denseStress, "Dense stress field (full density, 150 m)");
        bool nextCounters = GUILayout.Toggle(captureCounters, "Diagnostic GPU counts (1 Hz; use separately from timing)");
        if (nextDense != denseStress || nextCounters != captureCounters)
        { denseStress = nextDense; captureCounters = nextCounters; SelectProfile(selectedProfile); }
        if (GUILayout.Button("Start 20-minute sustained run", GUILayout.Height(45))) BeginRun();
        GUILayout.Label("Controls hide during measurement. Results export after the run.");
        GUILayout.Label(lastExport);
        GUILayout.EndArea(); GUI.matrix = previous;
    }
}
