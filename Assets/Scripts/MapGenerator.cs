using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;
using System;
using System.Threading;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode { NoiseMap, ColorMap, ContinentalnessMap, ErosionMap, PeaksValleysMap, Mesh }
    public DrawMode drawMode;

    public const int mapChunkSize = 241;
    [Range(0, 6)]
    public int previewLevelOfDetail;

    public float cScale;
    public int cOctaves;
    [Range(0, 1)] public float cPersistence;
    public float cLacunarity;
    public AnimationCurve continentalnessSpline;

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
                display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, heightMult, meshHeightCurve, previewLevelOfDetail), TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
                break;
            case DrawMode.NoiseMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
                break;
            case DrawMode.ContinentalnessMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.cMap));
                break;
            case DrawMode.ErosionMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.eMap));
                break;
            case DrawMode.PeaksValleysMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.pvMap));
                break;
            case DrawMode.ColorMap:
                display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
                break;
        }
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
        AnimationCurve continentalnessCopy = new AnimationCurve(continentalnessSpline.keys);
        AnimationCurve erosionCopy = new AnimationCurve(erosionSpline.keys);
        AnimationCurve peaksValleysCopy = new AnimationCurve(peaksValleysSpline.keys);

        Vector2 sampleOffset = centre + offset;

        float[,] cMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, cScale, cOctaves, cPersistence, cLacunarity, sampleOffset);
        float[,] eMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed + 1337, eScale, eOctaves, ePersistence, eLacunarity, sampleOffset);
        float[,] pvMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed + 2674, pvScale, pvOctaves, pvPersistence, pvLacunarity, sampleOffset);

        float[,] combinedMap = CombineNoiseMaps(cMap, eMap, pvMap, continentalnessCopy, erosionCopy, peaksValleysCopy);

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
        return new MapData(combinedMap, cMap, eMap, pvMap, colorMap);
    }

    float[,] CombineNoiseMaps(float[,] cMap, float[,] eMap, float[,] pvMap,
    AnimationCurve continentalnessCurve, AnimationCurve erosionCurve, AnimationCurve peaksValleysCurve)
    {
        float[,] result = new float[mapChunkSize, mapChunkSize];

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                float h = continentalnessCurve.Evaluate(cMap[x, y])
                        + erosionCurve.Evaluate(eMap[x, y])
                        * peaksValleysCurve.Evaluate(pvMap[x, y]);

                result[x, y] = h;
            }
        }

        //seam fix- use the min max value of the curves
        var cKeys = continentalnessCurve.keys;
        var eKeys = erosionCurve.keys;
        var pvKeys = peaksValleysCurve.keys;

        float cMax = 0f, eMax = 0f, pvMax = 0f, cMin = 0f, eMin = 0f, pvMin = 0f;
        foreach (var k in cKeys) { cMax = Mathf.Max(cMax, k.value); cMin = Mathf.Min(cMin, k.value); }
        foreach (var k in eKeys) { eMax = Mathf.Max(eMax, k.value); eMin = Mathf.Min(eMin, k.value); }
        foreach (var k in pvKeys) { pvMax = Mathf.Max(pvMax, k.value); pvMin = Mathf.Min(pvMin, k.value); }

        float globalMax = cMax + eMax * pvMax;
        float globalMin = cMin + eMin * pvMin;

        for (int y = 0; y < mapChunkSize; y++)
            for (int x = 0; x < mapChunkSize; x++)
                result[x, y] = Mathf.InverseLerp(globalMin, globalMax, result[x, y]);

        return result;
    }

    private void OnValidate()
    {
        if (cLacunarity < 1) cLacunarity = 1;
        if (eLacunarity < 1) eLacunarity = 1;
        if (pvLacunarity < 1) pvLacunarity = 1;
        if (cOctaves < 0) cOctaves = 0;
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
    public readonly float[,] cMap;
    public readonly float[,] eMap;
    public readonly float[,] pvMap;
    public readonly Color[] colorMap;
    public MapData(float[,] heightMap, float[,] cMap, float[,] eMap, float[,] pvMap, Color[] colorMap)
    {
        this.heightMap = heightMap;
        this.cMap = cMap;
        this.eMap = eMap;
        this.pvMap = pvMap;
        this.colorMap = colorMap;
    }
}