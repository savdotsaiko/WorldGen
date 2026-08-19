using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class ViewDistanceBenchmark : MonoBehaviour
{
    public string runLabel = "viewdist_300";
    public float sampleInterval = 1f;
    public float totalDuration = 30f;
    public EndlessWorld endlessWorld;

    private const float STUTTER_THRESHOLD_MS = 50f;

    private float _timer, _elapsed;
    private readonly List<string> _rows = new();
    private float _maxFrameMs;
    private int _stutterCount;
    private float _frameTimeAccum;
    private int _frameCount;

    void Update()
    {
        float dtMs = Time.unscaledDeltaTime * 1000f;
        _elapsed += Time.unscaledDeltaTime;
        _timer += Time.unscaledDeltaTime;
        _frameTimeAccum += dtMs;
        _frameCount++;
        if (dtMs > _maxFrameMs) _maxFrameMs = dtMs;
        if (dtMs > STUTTER_THRESHOLD_MS) _stutterCount++;

        if (_timer >= sampleInterval)
        {
            LogSample();
            _timer = 0f;
        }
        if (_elapsed >= totalDuration)
        {
            WriteCSV();
            enabled = false;
        }
    }

    void LogSample()
    {
        if (endlessWorld.GetAllChunks() == null) return;
        int chunkCount = endlessWorld != null ? endlessWorld.GetAllChunks().Count : -1;
        long memBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        float avgFrameMs = _frameTimeAccum / Mathf.Max(1, _frameCount);
        _rows.Add($"{_elapsed:F1},{runLabel},{chunkCount},{avgFrameMs:F3},{_maxFrameMs:F3},{_stutterCount},{memBytes}");
    }

    void WriteCSV()
    {
        string safeLabel = runLabel.Replace(" ", "_");
        string path = Application.dataPath + $"/../ViewDistanceBenchmark_{safeLabel}.csv";
        using (var writer = new StreamWriter(path))
        {
            writer.WriteLine("elapsedSec,runLabel,chunkCount,avgFrameMs,maxFrameMsSoFar,stutterFrameCountSoFar,allocatedMemoryBytes");
            foreach (var row in _rows) writer.WriteLine(row);
        }
        Debug.Log($"View distance benchmark CSV written to {path}");
    }
}