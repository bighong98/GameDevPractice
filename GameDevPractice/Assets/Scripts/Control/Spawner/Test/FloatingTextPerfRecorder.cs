using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TH.Utils
{
    // Runs a deterministic floating text performance capture and writes summary files.
    public sealed class FloatingTextPerfRecorder : MonoBehaviour
    {
        [Header("Probe")]
        [SerializeField] private FloatingTextPlayModeProbe probe;
        [SerializeField] private bool controlProbe = true;

        [Header("Timing")]
        [SerializeField] private bool autoStartOnEnable;
        [SerializeField, Min(0f)] private float warmupSeconds = 3f;
        [SerializeField, Min(1f)] private float measureSeconds = 30f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Output")]
        [SerializeField] private string outputDirectoryName = "PerfLogs";
        [SerializeField] private string outputFilePrefix = "floating_text_perf";
        [SerializeField] private bool appendTimestamp = true;
        [SerializeField] private bool writeJson = true;
        [SerializeField] private bool appendCsv = true;
        [SerializeField] private bool logSummary = true;

        private Coroutine measureRoutine;
        private ProfilerRecorder gcAllocRecorder;

        private void OnEnable()
        {
            if (autoStartOnEnable && Application.isPlaying)
                StartCapture();
        }

        private void OnDisable()
        {
            StopCapture();
        }

        [ContextMenu("Start FloatingText Perf Capture")]
        public void StartCapture()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FloatingTextPerf] StartCapture ignored because editor is not in Play mode.", this);
                return;
            }

            if (measureRoutine != null)
                return;

            string outputDir = ResolveOutputDirectory();
            Debug.Log(
                $"[FloatingTextPerf] Capture started. warmup={warmupSeconds:0.###}s, measure={measureSeconds:0.###}s, outputDir={outputDir}",
                this);

            measureRoutine = StartCoroutine(CaptureRoutine());
        }

        [ContextMenu("Stop FloatingText Perf Capture")]
        public void StopCapture()
        {
            bool interrupted = measureRoutine != null;
            if (measureRoutine != null)
            {
                StopCoroutine(measureRoutine);
                measureRoutine = null;
            }

            if (controlProbe && probe != null)
                probe.StopProbe();

            DisposeRecorder();

            if (interrupted)
            {
                Debug.LogWarning(
                    "[FloatingTextPerf] Capture was interrupted before completion. Results were not written.",
                    this);
            }
        }

        private System.Collections.IEnumerator CaptureRoutine()
        {
            bool completed = false;
            try
            {
                if (controlProbe && probe != null)
                    probe.StartProbe();

                yield return WaitForSecondsSafe(warmupSeconds);

                DisposeRecorder();
                StartGcRecorderSafely();

                int gc0Before = GC.CollectionCount(0);
                int gc1Before = GC.CollectionCount(1);
                int gc2Before = GC.CollectionCount(2);

                int frameCount = 0;
                double totalFrameMs = 0d;
                float maxFrameMs = 0f;
                double totalGcAllocBytes = 0d;
                long maxGcAllocBytes = 0;

                float elapsed = 0f;
                while (elapsed < measureSeconds)
                {
                    yield return null;

                    float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    float frameMs = dt * 1000f;
                    elapsed += dt;
                    frameCount++;

                    totalFrameMs += frameMs;
                    if (frameMs > maxFrameMs)
                        maxFrameMs = frameMs;

                    long gcBytes = gcAllocRecorder.Valid ? gcAllocRecorder.LastValue : 0;
                    totalGcAllocBytes += gcBytes;
                    if (gcBytes > maxGcAllocBytes)
                        maxGcAllocBytes = gcBytes;
                }

                int gc0After = GC.CollectionCount(0);
                int gc1After = GC.CollectionCount(1);
                int gc2After = GC.CollectionCount(2);

                var result = new PerfResult
                {
                    capturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    unityVersion = Application.unityVersion,
                    scenePath = SceneManager.GetActiveScene().path,
                    warmupSeconds = warmupSeconds,
                    measureSeconds = measureSeconds,
                    useUnscaledTime = useUnscaledTime,
                    frameCount = frameCount,
                    avgFrameMs = frameCount > 0 ? (float)(totalFrameMs / frameCount) : 0f,
                    maxFrameMs = maxFrameMs,
                    avgGcAllocBytesPerFrame = frameCount > 0 ? totalGcAllocBytes / frameCount : 0d,
                    maxGcAllocBytesPerFrame = maxGcAllocBytes,
                    gcCollectionDeltaGen0 = gc0After - gc0Before,
                    gcCollectionDeltaGen1 = gc1After - gc1Before,
                    gcCollectionDeltaGen2 = gc2After - gc2Before
                };

                completed = TryWriteResultAndLog(result);
            }
            finally
            {
                DisposeRecorder();

                if (controlProbe && probe != null)
                    probe.StopProbe();

                measureRoutine = null;

                if (!completed)
                {
                    Debug.LogWarning(
                        "[FloatingTextPerf] Capture did not complete. Check earlier error logs.",
                        this);
                }
            }
        }

        private void StartGcRecorderSafely()
        {
            try
            {
                gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FloatingTextPerf] GC recorder is unavailable: {e.Message}", this);
            }
        }

        private bool TryWriteResultAndLog(in PerfResult result)
        {
            try
            {
                string outputDir = ResolveOutputDirectory();
                Directory.CreateDirectory(outputDir);

                string stamp = appendTimestamp
                    ? "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                    : string.Empty;
                string basePath = Path.Combine(outputDir, outputFilePrefix + stamp);

                if (writeJson)
                    File.WriteAllText(basePath + ".json", JsonUtility.ToJson(result, true), Encoding.UTF8);

                if (appendCsv)
                    AppendCsv(outputDir, result);

                if (logSummary)
                {
                    Debug.Log(
                        $"[FloatingTextPerf] frames={result.frameCount}, avgFrameMs={result.avgFrameMs:0.###}, maxFrameMs={result.maxFrameMs:0.###}, avgGC/frame={result.avgGcAllocBytesPerFrame:0.##} B, maxGC/frame={result.maxGcAllocBytesPerFrame} B, GC(gen0/gen1/gen2)={result.gcCollectionDeltaGen0}/{result.gcCollectionDeltaGen1}/{result.gcCollectionDeltaGen2}, outputDir={outputDir}",
                        this);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FloatingTextPerf] Failed to write result files: {e}", this);
                return false;
            }
        }

        private System.Collections.IEnumerator WaitForSecondsSafe(float seconds)
        {
            if (seconds <= 0f)
                yield break;

            if (useUnscaledTime)
            {
                float end = Time.unscaledTime + seconds;
                while (Time.unscaledTime < end)
                    yield return null;
            }
            else
            {
                float end = Time.time + seconds;
                while (Time.time < end)
                    yield return null;
            }
        }

        private string ResolveOutputDirectory()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, outputDirectoryName);
        }

        private void AppendCsv(string outputDir, PerfResult result)
        {
            string csvPath = Path.Combine(outputDir, outputFilePrefix + "_summary.csv");
            bool exists = File.Exists(csvPath);

            var sb = new StringBuilder(512);
            if (!exists)
            {
                sb.AppendLine("captured_at_utc,scene_path,unity_version,warmup_seconds,measure_seconds,use_unscaled_time,frame_count,avg_frame_ms,max_frame_ms,avg_gc_alloc_bytes_per_frame,max_gc_alloc_bytes_per_frame,gc_gen0_delta,gc_gen1_delta,gc_gen2_delta");
            }

            sb
                .Append(result.capturedAtUtc).Append(',')
                .Append(EscapeCsv(result.scenePath)).Append(',')
                .Append(EscapeCsv(result.unityVersion)).Append(',')
                .Append(result.warmupSeconds.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.measureSeconds.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.useUnscaledTime ? "true" : "false").Append(',')
                .Append(result.frameCount.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.avgFrameMs.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.maxFrameMs.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.avgGcAllocBytesPerFrame.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                .Append(result.maxGcAllocBytesPerFrame.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.gcCollectionDeltaGen0.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.gcCollectionDeltaGen1.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(result.gcCollectionDeltaGen2.ToString(CultureInfo.InvariantCulture))
                .AppendLine();

            File.AppendAllText(csvPath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            bool needsQuote = raw.IndexOf(',') >= 0 || raw.IndexOf('"') >= 0 || raw.IndexOf('\n') >= 0 || raw.IndexOf('\r') >= 0;
            if (!needsQuote)
                return raw;

            return '"' + raw.Replace("\"", "\"\"") + '"';
        }

        private void DisposeRecorder()
        {
            if (gcAllocRecorder.Valid)
                gcAllocRecorder.Dispose();
        }

        [Serializable]
        private struct PerfResult
        {
            public string capturedAtUtc;
            public string scenePath;
            public string unityVersion;
            public float warmupSeconds;
            public float measureSeconds;
            public bool useUnscaledTime;
            public int frameCount;
            public float avgFrameMs;
            public float maxFrameMs;
            public double avgGcAllocBytesPerFrame;
            public long maxGcAllocBytesPerFrame;
            public int gcCollectionDeltaGen0;
            public int gcCollectionDeltaGen1;
            public int gcCollectionDeltaGen2;
        }
    }
}
