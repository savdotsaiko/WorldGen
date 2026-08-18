using UnityEngine;

// ============================================================
// NEW STANDALONE SCRIPT — does not need to be pasted anywhere.
// Create an empty GameObject, add this component to it.
// Works in Edit Mode or Play Mode (no coroutines needed).
// Right-click the component header -> "Run Noise Scaling Benchmark".
// ============================================================
public class NoiseScalingBenchmark : MonoBehaviour
{
    [Header("Test range")]
    public int mapSize = 241; // match MapGenerator.mapChunkSize
    public int minOctaves = 1;
    public int maxOctaves = 10;
    public int runsPerOctaveCount = 5;

    [Header("Fixed noise params for this test")]
    public float scale = 150f;
    public float persistence = 0.5f;
    public float lacunarity = 2f;

    [ContextMenu("Run Noise Scaling Benchmark")]
    public void RunBenchmark()
    {
        string path = Application.dataPath + "/../NoiseScalingBenchmark.csv";
        var sw = new System.Diagnostics.Stopwatch();

        using (var writer = new System.IO.StreamWriter(path))
        {
            writer.WriteLine("octaves,run,timeMs");
            for (int oct = minOctaves; oct <= maxOctaves; oct++)
            {
                for (int r = 0; r < runsPerOctaveCount; r++)
                {
                    sw.Restart();
                    Noise.GenerateNoiseMap(mapSize, mapSize, 11 + r, scale, oct, persistence, lacunarity, Vector2.zero);
                    sw.Stop();
                    writer.WriteLine($"{oct},{r},{sw.Elapsed.TotalMilliseconds:F3}");
                }
            }
        }
        UnityEngine.Debug.Log($"Noise scaling benchmark written to {path}");
    }
}