// ================================================================
// CHANGES TO: MapGenerator.cs
// Goal: extend DrawMode enum to preview C, E, PV maps individually,
//       the combined map, and keep ColorMap — all before touching
//       any mesh generation code.
// ================================================================

// ----------------------------------------------------------------
// STEP 1 — Extend the enum (find your existing enum and replace it)
// ----------------------------------------------------------------
using UnityEngine;

public class MapGeneratorMC : MonoBehaviour
{
    public enum DrawMode
    {
        // Keep Sebastian's originals
        NoiseMap,           // now means "CombinedNoiseMap" (or remove if you want)
        ColorMap,           // unchanged
        FalloffMap,         // unchanged (if you have it)

        // ↓ NEW
        ContinentalnessMap, // raw continentalness noise layer
        ErosionMap,         // raw erosion noise layer
        PeaksValleysMap,    // raw peaks & valleys noise layer
        CombinedNoiseMap    // the 3 layers combined via splines
    }

    // ----------------------------------------------------------------
    // STEP 2 — Add these fields to MapGenerator (inspector fields)
    //           Place them BELOW your existing noiseSettings field.
    //           If you're in early episodes and don't have NoiseSettings
    //           objects yet, see the note at the bottom of this file.
    // ----------------------------------------------------------------

    [Header("Continentalness (large-scale land/sea shape)")]
    public NoiseSettings continentalnessSettings;   // low freq  (~scale 800)
    public AnimationCurve continentalnessSpline;     // maps C (0-1) → base height

    [Header("Erosion (controls how flat or dramatic terrain is)")]
    public NoiseSettings erosionSettings;            // mid freq  (~scale 300)
    public AnimationCurve erosionSpline;             // maps E (0-1) → PV amplitude

    [Header("Peaks & Valleys (actual surface detail)")]
    public NoiseSettings peaksValleysSettings;       // high freq (~scale 100)
    public AnimationCurve peaksValleysSpline;        // maps PV (0-1) → height offset

    // ----------------------------------------------------------------
    // STEP 3 — Replace / extend your GenerateMap() method
    // ----------------------------------------------------------------

    public void GenerateMap()
    {
        // -- Generate the 3 raw noise maps independently --
        // Each uses its own NoiseSettings (different scale, seed offset etc)
        float[,] cMap = Noise.GenerateNoiseMap(mapWidth, mapHeight,
                             continentalnessSettings, Vector2.zero);

        float[,] eMap = Noise.GenerateNoiseMap(mapWidth, mapHeight,
                             erosionSettings, Vector2.zero);

        float[,] pvMap = Noise.GenerateNoiseMap(mapWidth, mapHeight,
                             peaksValleysSettings, Vector2.zero);

        // -- Combine into one final heightmap using the splines --
        float[,] combinedMap = CombineNoiseMaps(cMap, eMap, pvMap);

        // -- Build colour map from the COMBINED heightmap (unchanged from Seb) --
        Color[] colorMap = new Color[mapWidth * mapHeight];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float currentHeight = combinedMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                    {
                        colorMap[y * mapWidth + x] = regions[i].colour;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        // -- Draw whichever map the enum selects --
        MapDisplay display = FindObjectOfType<MapDisplay>();

        switch (drawMode)
        {
            case DrawMode.ContinentalnessMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(cMap));
                break;

            case DrawMode.ErosionMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(eMap));
                break;

            case DrawMode.PeaksValleysMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(pvMap));
                break;

            case DrawMode.CombinedNoiseMap:
            case DrawMode.NoiseMap:          // keep NoiseMap pointing here too
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(combinedMap));
                break;

            case DrawMode.ColorMap:
                display.DrawTexture(TextureGenerator.TextureFromColorMap(
                    colorMap, mapWidth, mapHeight));
                break;

            case DrawMode.FalloffMap:
                // Unchanged from Seb's version — only relevant on flat terrain
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(
                    FalloffGenerator.GenerateFalloffMap(mapWidth)));
                break;
        }
    }

    // ----------------------------------------------------------------
    // STEP 4 — Add this new method to MapGenerator
    //           (the actual 3-map combination logic)
    // ----------------------------------------------------------------

    float[,] CombineNoiseMaps(float[,] cMap, float[,] eMap, float[,] pvMap)
    {
        float[,] result = new float[mapWidth, mapHeight];
        float min = float.MaxValue, max = float.MinValue;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {

                // Sample each spline (AnimationCurve — the graph in the inspector)
                float baseHeight = continentalnessSpline.Evaluate(cMap[x, y]);
                float pvAmplitude = erosionSpline.Evaluate(eMap[x, y]);
                float pvDetail = peaksValleysSpline.Evaluate(pvMap[x, y]);

                // THE formula — erosion multiplies how loud PV is
                float h = baseHeight + pvAmplitude * pvDetail;

                result[x, y] = h;
                if (h > max) max = h;
                if (h < min) min = h;
            }
        }

        // Normalise to [0,1] so MeshGenerator receives values it expects
        for (int y = 0; y < mapHeight; y++)
            for (int x = 0; x < mapWidth; x++)
                result[x, y] = Mathf.InverseLerp(min, max, result[x, y]);

        return result;
    }
}
// ================================================================
// SEED OFFSETS — add these to your noise settings so each layer
// generates independently even with the same master seed.
// In your inspector or in Awake():
//
//   continentalnessSettings.seed = masterSeed;
//   erosionSettings.seed         = masterSeed + 1337;
//   peaksValleysSettings.seed    = masterSeed + 2674;
//
// ================================================================

// ================================================================
// NOTE — IF YOU ARE IN EARLY EPISODES (no NoiseSettings object yet)
// and your Noise.GenerateNoiseMap takes loose params like:
//   Noise.GenerateNoiseMap(width, height, seed, scale, octaves, ...)
//
// Just declare three sets of the same raw fields:
//
//   [Header("Continentalness")]
//   public float cScale = 800f;
//   public int   cOctaves = 4;
//   public float cPersistance = 0.5f;
//   public float cLacunarity = 2f;
//   public int   cSeed = 0;
//
//   [Header("Erosion")]
//   public float eScale = 300f;
//   ... etc
//
// Then call:
//   float[,] cMap = Noise.GenerateNoiseMap(w, h, cSeed, cScale, cOctaves, ...);
//
// The combination logic in CombineNoiseMaps stays identical either way.
// ================================================================

// ================================================================
// STARTING CURVE VALUES — set these in the inspector to get
// reasonable-looking terrain right away without trial and error.
//
// continentalnessSpline (C → base height):
//   (0.0, -0.5)  (0.35, -0.05)  (0.4, 0.0)  (0.65, 0.25)  (1.0, 0.65)
//   S-shaped. Flat at extremes, steep through the shoreline.
//
// erosionSpline (E → PV amplitude, i.e. how loud detail is):
//   (0.0, 1.0)  (0.4, 0.5)  (0.7, 0.05)  (1.0, 0.0)
//   Falls steeply. High erosion = detail silenced = flat land.
//
// peaksValleysSpline (PV → height offset):
//   (0.0, -0.3)  (0.45, 0.0)  (0.7, 0.15)  (1.0, 0.5)
//   Roughly linear but push peaks sharper / valleys wider to taste.
// ================================================================