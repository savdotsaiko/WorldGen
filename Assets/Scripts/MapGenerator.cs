using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode { NoiseMap, ColorMap, ContinentalnessMap, ErosionMap, PeaksValleysMap, CombinedNoiseMap }
    public DrawMode drawMode;

    public int mapWidth, mapHeight;

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
    public bool autoUpdate;
    public TerrainType[] regions;

    public void GenerateMap()
    {
        float[,] cMap = Noise.GenerateNoiseMap(mapWidth, mapHeight, seed, cScale, cOctaves, cPersistence, cLacunarity, offset);
        float[,] eMap = Noise.GenerateNoiseMap(mapWidth, mapHeight, seed + 1337, eScale, eOctaves, ePersistence, eLacunarity, offset);
        float[,] pvMap = Noise.GenerateNoiseMap(mapWidth, mapHeight, seed + 2674, pvScale, pvOctaves, pvPersistence, pvLacunarity, offset);

        float[,] combinedMap = CombineNoiseMaps(cMap, eMap, pvMap);

        Color[] colorMap = new Color[mapWidth * mapHeight];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float currentHeight = combinedMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colorMap[y * mapWidth + x] = regions[i].color;
                        break;
                    }
                }
            }
        }

        MapDisplay display = Object.FindFirstObjectByType<MapDisplay>();

        switch (drawMode)
        {
            case DrawMode.NoiseMap:
            case DrawMode.CombinedNoiseMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(combinedMap));
                break;
            case DrawMode.ContinentalnessMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(cMap));
                break;
            case DrawMode.ErosionMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(eMap));
                break;
            case DrawMode.PeaksValleysMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(pvMap));
                break;
            case DrawMode.ColorMap:
                display.DrawTexture(TextureGenerator.TextureFromColorMap(colorMap, mapWidth, mapHeight));
                break;
        }
    }

    float[,] CombineNoiseMaps(float[,] cMap, float[,] eMap, float[,] pvMap)
    {
        float[,] result = new float[mapWidth, mapHeight];
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float h = continentalnessSpline.Evaluate(cMap[x, y])
                        + erosionSpline.Evaluate(eMap[x, y])
                        * peaksValleysSpline.Evaluate(pvMap[x, y]);

                result[x, y] = h;
                if (h > max) max = h;
                if (h < min) min = h;
            }
        }

        for (int y = 0; y < mapHeight; y++)
            for (int x = 0; x < mapWidth; x++)
                result[x, y] = Mathf.InverseLerp(min, max, result[x, y]);

        return result;
    }

    private void OnValidate()
    {
        if (mapWidth < 1) mapWidth = 1;
        if (mapHeight < 1) mapHeight = 1;
        if (cLacunarity < 1) cLacunarity = 1;
        if (eLacunarity < 1) eLacunarity = 1;
        if (pvLacunarity < 1) pvLacunarity = 1;
        if (cOctaves < 0) cOctaves = 0;
        if (eOctaves < 0) eOctaves = 0;
        if (pvOctaves < 0) pvOctaves = 0;
    }
}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}