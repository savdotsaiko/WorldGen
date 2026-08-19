using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;
using System;
using System.Threading;
using System.Collections.Generic;
using UnityEngine.Serialization;
using TMPro;
public class MapGenerator : MonoBehaviour
{
    public enum DrawMode
    {
        NoiseMap,
        ColorMap,

        ContinentalnessMap,
        ErosionMap,
        PeaksValleysMap,

        RiverMask,
        ErosionMask,
        PeaksValleysMask,

        Mesh
    }
    public DrawMode drawMode;

    public const int mapChunkSize = 241;
    [Range(0, 6)]
    public int previewLevelOfDetail;

    [FormerlySerializedAs("cScale")] public float rScale;
    [FormerlySerializedAs("cOctaves")] public int rOctaves;
    [FormerlySerializedAs("cPersistence")] public float rPersistence;
    [FormerlySerializedAs("cLacunarity")] public float rLacunarity;
    [FormerlySerializedAs("continentalnessSpline")] public AnimationCurve riverMaskSpline;

    public float eScale;
    public int eOctaves;
    [Range(0, 1)] public float ePersistence;
    public float eLacunarity;
    public AnimationCurve erosionSpline;

    public float pvScale;
    public int pvOctaves;
    [Range(0, 1)] public float pvPersistence;
    public float pvLacunarity;
    public AnimationCurve peaksValleysSpline;

    public int seed;
    public Vector2 offset;
    public float heightMult;
    public AnimationCurve meshHeightCurve;
    public bool autoUpdate;
    public TerrainType[] regions;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new();

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay display = UnityEngine.Object.FindFirstObjectByType<MapDisplay>();

        switch (drawMode)
        {
            case DrawMode.Mesh:
                display.DrawMesh(
                    MeshGenerator.GenerateTerrainMesh(
                        mapData.heightMap,
                        heightMult,
                        meshHeightCurve,
                        previewLevelOfDetail
                    ),
                    TextureGenerator.TextureFromColorMap(
                        mapData.colorMap,
                        mapChunkSize,
                        mapChunkSize
                    )
                );
                break;

            // RAW noise maps
            case DrawMode.NoiseMap:
                display.DrawTexture(
                    TextureGenerator.TextureFromHeightMap(mapData.heightMap)
                );
                break;

            case DrawMode.ContinentalnessMap:
                display.DrawTexture(
                    TextureGenerator.TextureFromHeightMap(mapData.rMap)
                );
                break;

            case DrawMode.ErosionMap:
                display.DrawTexture(
                    TextureGenerator.TextureFromHeightMap(mapData.eMap)
                );
                break;

            case DrawMode.PeaksValleysMap:
                display.DrawTexture(
                    TextureGenerator.TextureFromHeightMap(mapData.pvMap)
                );
                break;

            // POST-SPLINE maps
            case DrawMode.RiverMask:
                display.DrawTexture(
                    TextureGenerator.TextureFromHeightMap(
                        ApplySpline(mapData.rMap, riverMaskSpline)
                    )
                );
                break;

            case DrawMode.ErosionMask:
                display.DrawTexture(
                    TextureGenerator.TextureFromHeightMap(
                        ApplySpline(mapData.eMap, erosionSpline)
                    )
                );
                break;

            case DrawMode.PeaksValleysMask:
                display.DrawTexture(
                    TextureGenerator.TextureFromHeightMap(
                        ApplySpline(mapData.pvMap, peaksValleysSpline)
                    )
                );
                break;

            case DrawMode.ColorMap:
                display.DrawTexture(
                    TextureGenerator.TextureFromColorMap(
                        mapData.colorMap,
                        mapChunkSize,
                        mapChunkSize
                    )
                );
                break;
        }
    }
    float[,] ApplySpline(float[,] noiseMap, AnimationCurve spline)
    {
        float[,] result = new float[mapChunkSize, mapChunkSize];

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                result[x, y] = spline.Evaluate(noiseMap[x, y]);
            }
        }

        return result;
    }
    public void RequestMapData(Vector2 centre, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(centre, callback);
        };
        new Thread(threadStart).Start();
    }

    void MapDataThread(Vector2 centre, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(centre);
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, Action<MeshData> callback, int lod)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, callback, lod);
        };
        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, Action<MeshData> callback, int lod)
    {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, heightMult, meshHeightCurve, lod);
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }
    private void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }
    MapData GenerateMapData(Vector2 centre)
    {
        AnimationCurve continentalnessCopy = new AnimationCurve(riverMaskSpline.keys);
        AnimationCurve erosionCopy = new AnimationCurve(erosionSpline.keys);
        AnimationCurve peaksValleysCopy = new AnimationCurve(peaksValleysSpline.keys);

        Vector2 sampleOffset = centre + offset;

        float[,] rMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, rScale, rOctaves, rPersistence, rLacunarity, sampleOffset);
        float[,] eMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed + 1337, eScale, eOctaves, ePersistence, eLacunarity, sampleOffset);
        float[,] pvMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed + 2674, pvScale, pvOctaves, pvPersistence, pvLacunarity, sampleOffset);

        float[,] combinedMap = CombineNoiseMaps(rMap, eMap, pvMap, continentalnessCopy, erosionCopy, peaksValleysCopy);

        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                float currentHeight = combinedMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                        break;
                    }
                }
            }
        }
        return new MapData(combinedMap, rMap, eMap, pvMap, colorMap);
    }

    float[,] CombineNoiseMaps(float[,] rMap, float[,] eMap, float[,] pvMap,
    AnimationCurve continentalnessCurve, AnimationCurve erosionCurve, AnimationCurve peaksValleysCurve)
    {
        float[,] result = new float[mapChunkSize, mapChunkSize];

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                float h = continentalnessCurve.Evaluate(rMap[x, y]) 
                   + (erosionCurve.Evaluate(eMap[x, y]))  * peaksValleysCurve.Evaluate(pvMap[x, y]);

                result[x, y] = h;
            }
        }

        //seam fix- use the min max value of the curves
        var cKeys = continentalnessCurve.keys;
        var eKeys = erosionCurve.keys;
        var pvKeys = peaksValleysCurve.keys;

        float cMax = 0f, eMax = 0f, pvMax = 0f, rMin = 0f, eMin = 0f, pvMin = 0f;
        foreach (var k in cKeys) { cMax = Mathf.Max(cMax, k.value); rMin = Mathf.Min(rMin, k.value); }
        foreach (var k in eKeys) { eMax = Mathf.Max(eMax, k.value); eMin = Mathf.Min(eMin, k.value); }
        foreach (var k in pvKeys) { pvMax = Mathf.Max(pvMax, k.value); pvMin = Mathf.Min(pvMin, k.value); }

        float globalMax = cMax + eMax * pvMax;
        float globalMin = rMin + eMin * pvMin;

        for (int y = 0; y < mapChunkSize; y++)
            for (int x = 0; x < mapChunkSize; x++)
                result[x, y] = Mathf.InverseLerp(globalMin, globalMax, result[x, y]);

        return result;
    }

    private void OnValidate()
    {
        if (rLacunarity < 1) rLacunarity = 1;
        if (eLacunarity < 1) eLacunarity = 1;
        if (pvLacunarity < 1) pvLacunarity = 1;
        if (rOctaves < 0) rOctaves = 0;
        if (eOctaves < 0) eOctaves = 0;
        if (pvOctaves < 0) pvOctaves = 0;
    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;
        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
    [ContextMenu("Export Noise Debug CSV")]
    public void ExportNoiseDebugCSV()
    {
        Vector2 centre = Vector2.zero; // change if you want a different chunk (e.g. one that straddles the river)

        AnimationCurve continentalnessCopy = new AnimationCurve(riverMaskSpline.keys);
        AnimationCurve erosionCopy = new AnimationCurve(erosionSpline.keys);
        AnimationCurve peaksValleysCopy = new AnimationCurve(peaksValleysSpline.keys);

        Vector2 sampleOffset = centre + offset;

        float[,] cMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, rScale, rOctaves, rPersistence, rLacunarity, sampleOffset);
        float[,] eMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed + 1337, eScale, eOctaves, ePersistence, eLacunarity, sampleOffset);
        float[,] pvMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed + 2674, pvScale, pvOctaves, pvPersistence, pvLacunarity, sampleOffset);

        // Replicate the EXACT normalization bounds used in CombineNoiseMaps
        // (based on spline control-point extremes, not actual sampled min/max)
        var cKeys = continentalnessCopy.keys;
        var eKeys = erosionCopy.keys;
        var pvKeys = peaksValleysCopy.keys;

        float cMax = 0f, eMax = 0f, pvMax = 0f, cMin = 0f, eMin = 0f, pvMin = 0f;
        foreach (var k in cKeys) { cMax = Mathf.Max(cMax, k.value); cMin = Mathf.Min(cMin, k.value); }
        foreach (var k in eKeys) { eMax = Mathf.Max(eMax, k.value); eMin = Mathf.Min(eMin, k.value); }
        foreach (var k in pvKeys) { pvMax = Mathf.Max(pvMax, k.value); pvMin = Mathf.Min(pvMin, k.value); }

        float globalMax = cMax + eMax * pvMax;
        float globalMin = cMin + eMin * pvMin;

        string path = Application.dataPath + "/../NoiseDebug.csv";
        using (var writer = new System.IO.StreamWriter(path))
        {
            writer.WriteLine("x,y,rawC,rawE,rawPV,splineC,splineE,splinePV,ePVproduct,hRaw,hNormalized");

            for (int y = 0; y < mapChunkSize; y++)
            {
                for (int x = 0; x < mapChunkSize; x++)
                {
                    float rawC = cMap[x, y];
                    float rawE = eMap[x, y];
                    float rawPV = pvMap[x, y];

                    float sC = continentalnessCopy.Evaluate(rawC);
                    float sE = erosionCopy.Evaluate(rawE);
                    float sPV = peaksValleysCopy.Evaluate(rawPV);

                    float product = sE * sPV;
                    float hRaw = sC + product;
                    float hNorm = Mathf.InverseLerp(globalMin, globalMax, hRaw);

                    writer.WriteLine(string.Format(
                        "{0},{1},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4}",
                        x, y, rawC, rawE, rawPV, sC, sE, sPV, product, hRaw, hNorm));
                }
            }
        }
        Debug.Log($"Noise debug CSV written to {path} ({mapChunkSize * mapChunkSize} rows)");
    }
    [ContextMenu("Benchmark Map Data Generation")]
    public void BenchmarkMapDataGeneration()
    {
        StartCoroutine(RunGenerationBenchmark());
    }

    private System.Collections.IEnumerator RunGenerationBenchmark()
    {
        int runs = 20;
        var sw = new System.Diagnostics.Stopwatch();
        var singleThreadTimes = new System.Collections.Generic.List<double>();
        var threadedTimes = new System.Collections.Generic.List<double>();

        // Single-threaded (synchronous, blocking) timing.
        // Each run uses a unique centre so every call does genuinely fresh
        // noise sampling rather than possibly benefiting from CPU caching
        // effects of repeating the exact same coordinates.
        for (int i = 0; i < runs; i++)
        {
            Vector2 centre = new Vector2(i * 240, 0);
            sw.Restart();
            MapData data = GenerateMapData(centre);
            sw.Stop();
            singleThreadTimes.Add(sw.Elapsed.TotalMilliseconds);
            yield return null;
        }

        // Threaded (wall-clock) timing: measures perceived latency from
        // request to callback firing, i.e. thread spin-up + generation +
        // waiting for the next Update() to dequeue it. This is the number
        // that matches what the player actually experiences.
        for (int i = 0; i < runs; i++)
        {
            Vector2 centre = new Vector2(i * 240, 100000); // offset so it never overlaps the single-thread test chunks
            double startTime = Time.realtimeSinceStartupAsDouble;
            bool done = false;
            double elapsedMs = 0;

            RequestMapData(centre, (data) =>
            {
                elapsedMs = (Time.realtimeSinceStartupAsDouble - startTime) * 1000.0;
                done = true;
            });

            while (!done) yield return null;
            threadedTimes.Add(elapsedMs);
        }

        WriteGenerationBenchmarkCSV(singleThreadTimes, threadedTimes);
    }

    private void WriteGenerationBenchmarkCSV(System.Collections.Generic.List<double> singleThread, System.Collections.Generic.List<double> threaded)
    {
        string path = Application.dataPath + "/../GenerationBenchmark.csv";
        using (var writer = new System.IO.StreamWriter(path))
        {
            writer.WriteLine("run,singleThreadMs,threadedMs");
            int count = Mathf.Max(singleThread.Count, threaded.Count);
            for (int i = 0; i < count; i++)
            {
                string s = i < singleThread.Count ? singleThread[i].ToString("F3") : "";
                string t = i < threaded.Count ? threaded[i].ToString("F3") : "";
                writer.WriteLine($"{i},{s},{t}");
            }
        }
        Debug.Log($"Generation benchmark CSV written to {path}");
    }


    // ---- METRIC: Triangle/vertex count per LOD level ----
    // Does NOT require play mode.
    [ContextMenu("Log Triangle Counts Per LOD")]
    public void LogTriangleCountsPerLOD()
    {
        MapData data = GenerateMapData(Vector2.zero);
        string path = Application.dataPath + "/../LODTriangleCounts.csv";

        using (var writer = new System.IO.StreamWriter(path))
        {
            writer.WriteLine("lod,vertexCount,triangleCount");
            for (int lod = 0; lod <= 6; lod++)
            {
                MeshData meshData = MeshGenerator.GenerateTerrainMesh(data.heightMap, heightMult, meshHeightCurve, lod);
                int vertCount = meshData.vertices.Length;
                int triCount = meshData.tringles.Length / 3;
                writer.WriteLine($"{lod},{vertCount},{triCount}");
            }
        }
        Debug.Log($"LOD triangle counts written to {path}");
    }
}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly float[,] rMap;
    public readonly float[,] eMap;
    public readonly float[,] pvMap;
    public readonly Color[] colorMap;
    public MapData(float[,] heightMap, float[,] rMap, float[,] eMap, float[,] pvMap, Color[] colorMap)
    {
        this.heightMap = heightMap;
        this.rMap = rMap;
        this.eMap = eMap;
        this.pvMap = pvMap;
        this.colorMap = colorMap;
    }
}