using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class ScatteringBenchmark : MonoBehaviour
{
    [Tooltip("Label for this run, e.g. the density value being tested. Set manually before each run.")]
    public string runLabel = "density_40";
    public float sampleInterval = 1f;
    public float totalDuration = 30f;

    private float _timer;
    private float _elapsed;
    private readonly List<string> _rows = new();
    private float _frameTimeAccum;
    private int _frameCount;

    void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        _frameTimeAccum += Time.unscaledDeltaTime;
        _frameCount++;
        _timer += Time.unscaledDeltaTime;

        if (_timer >= sampleInterval)
        {
            LogSample();
            _timer = 0f;
            _frameTimeAccum = 0f;
            _frameCount = 0;
        }

        if (_elapsed >= totalDuration)
        {
            WriteCSV();
            enabled = false;
        }
    }

    void LogSample()
    {
        int grassInstances = 0;
        var grassChunks = Object.FindObjectsByType<GrassChunk>(FindObjectsSortMode.None);
        foreach (var g in grassChunks) grassInstances += g.InstanceCount;

        int objectInstances = 0;
        var objectChunks = Object.FindObjectsByType<ObjectChunk>(FindObjectsSortMode.None);
        foreach (var o in objectChunks) objectInstances += o.SpawnedCount;

        float avgFrameMs = (_frameTimeAccum / Mathf.Max(1, _frameCount)) * 1000f;
        long memBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        _rows.Add($"{_elapsed:F1},{runLabel},{grassChunks.Length},{grassInstances},{objectChunks.Length},{objectInstances},{avgFrameMs:F3},{memBytes}");
        Debug.Log($"[Benchmark {runLabel}] t={_elapsed:F1}s grassChunks={grassChunks.Length} grassInstances={grassInstances} objectChunks={objectChunks.Length} objectInstances={objectInstances} avgFrameMs={avgFrameMs:F3} mem={memBytes}");
    }

    void WriteCSV()
    {
        string safeLabel = runLabel.Replace(" ", "_");
        string path = Application.dataPath + $"/../ScatteringBenchmark_{safeLabel}.csv";
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("elapsedSec,runLabel,grassChunkCount,grassInstanceTotal,objectChunkCount,objectInstanceTotal,avgFrameMs,allocatedMemoryBytes");
            foreach (var row in _rows) writer.WriteLine(row);
        }
        Debug.Log($"Scattering benchmark CSV written to {path}");
    }
}