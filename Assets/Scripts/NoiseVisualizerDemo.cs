using UnityEngine;

// Standalone demo scene controller - NOT part of your endless-world pipeline.
// Reuses Noise.GenerateNoiseMap (Noise.cs) and the TerrainType struct (MapGenerator.cs)
// but generates its own small flat->warped plane so it can run synchronously on the main thread.
public class NoiseVisualizerDemo : MonoBehaviour
{
    public enum ViewMode { Idle, Continentalness, Erosion, PeaksValleys, Combined }
    public enum DisplayMode { Mesh, Texture }

    [System.Serializable]
    public class NoiseLayerSettings
    {
        public string label;
        public bool enabled = true;
        public float scale = 50f;
        public int octaves = 4;
        [Range(0, 1)] public float persistence = 0.5f;
        public float lacunarity = 2f;
        public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
        public int seedOffset;
    }

    [Header("References")]
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    public Material material;

    [Header("Mesh")]
    [Tooltip("Grid resolution. Keep well below mapChunkSize (241) - this recomputes on the main thread every time a param changes.")]
    public int mapSize = 121;
    public float meshScale = 1f;

    [Header("Noise Layers (0 = Continentalness, 1 = Erosion, 2 = PeaksValleys)")]
    public NoiseLayerSettings[] layers = new NoiseLayerSettings[3]
    {
        new NoiseLayerSettings { label = "Continentalness", seedOffset = 0 },
        new NoiseLayerSettings { label = "Erosion", seedOffset = 1337 },
        new NoiseLayerSettings { label = "PeaksValleys", seedOffset = 2674 },
    };

    [Header("Global")]
    public int seed = 0;
    public Vector2 offset;
    public float heightMultiplier = 20f;
    public TerrainType[] regions; // reused for Combined+Texture, mirrors MapGenerator's region colouring

    [Header("View State")]
    public ViewMode viewMode = ViewMode.Idle;
    public DisplayMode displayMode = DisplayMode.Mesh;

    [Header("Transition")]
    [Tooltip("Higher = snappier lerp. This is an exponential smoothing rate, not a duration.")]
    public float lerpSpeed = 4f;

    [Header("Idle / Y2K spin")]
    public float idleRotationSpeed = 30f;
    public bool idleBob = true;
    public float idleBobHeight = 0.5f;
    public float idleBobSpeed = 1f;
    public float idleTiltAmount = 5f;

    Mesh mesh;
    Vector3[] baseVertices;
    Vector3[] workingVertices;
    float[] currentHeights;
    float[] targetHeights;
    Color[] currentColors;
    Color[] targetColors;

    // Cached post-curve, pre-heightMultiplier value per vertex (0..1-ish).
    // Lets height-multiplier / display-mode changes skip a full noise regen.
    float[] lastNormalizedHeight;

    Texture2D displayTexture;
    bool textureDirty;
    float idleTimer;
    float idleBaseY;

    void Awake()
    {
        BuildBaseMesh();
        idleBaseY = transform.position.y;
        RecomputeTargets();
        System.Array.Copy(targetHeights, currentHeights, currentHeights.Length);
        System.Array.Copy(targetColors, currentColors, currentColors.Length);
        ApplyHeightsToMesh();
        ApplyTexture();
    }

    void Update()
    {
        bool stillLerping = LerpTowardsTarget();
        if (stillLerping) ApplyHeightsToMesh();

        if (viewMode == ViewMode.Idle)
        {
            transform.Rotate(Vector3.up, idleRotationSpeed * Time.deltaTime, Space.World);
            if (idleBob)
            {
                idleTimer += Time.deltaTime;
                Vector3 p = transform.position;
                p.y = idleBaseY + Mathf.Sin(idleTimer * idleBobSpeed) * idleBobHeight;
                transform.position = p;

                Vector3 e = transform.eulerAngles;
                transform.rotation = Quaternion.Euler(
                    Mathf.Sin(idleTimer * idleBobSpeed * 0.7f) * idleTiltAmount,
                    e.y,
                    Mathf.Cos(idleTimer * idleBobSpeed * 0.5f) * idleTiltAmount);
            }
        }
    }

    // ---------- mesh setup ----------

    void BuildBaseMesh()
    {
        mesh = new Mesh();
        mesh.indexFormat = mapSize * mapSize > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        meshFilter.sharedMesh = mesh;

        int vertCount = mapSize * mapSize;
        baseVertices = new Vector3[vertCount];
        workingVertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[(mapSize - 1) * (mapSize - 1) * 6];

        float topLeftX = (mapSize - 1) / -2f * meshScale;
        float topLeftZ = (mapSize - 1) / 2f * meshScale;

        int t = 0;
        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int i = y * mapSize + x;
                baseVertices[i] = new Vector3(topLeftX + x * meshScale, 0f, topLeftZ - y * meshScale);
                uvs[i] = new Vector2(x / (float)(mapSize - 1), y / (float)(mapSize - 1));

                if (x < mapSize - 1 && y < mapSize - 1)
                {
                    // same winding as MeshGenerator.GenerateTerrainMesh
                    triangles[t++] = i; triangles[t++] = i + mapSize + 1; triangles[t++] = i + mapSize;
                    triangles[t++] = i + mapSize + 1; triangles[t++] = i; triangles[t++] = i + 1;
                }
            }
        }

        mesh.vertices = baseVertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        currentHeights = new float[vertCount];
        targetHeights = new float[vertCount];
        currentColors = new Color[vertCount];
        targetColors = new Color[vertCount];
        lastNormalizedHeight = new float[vertCount];

        displayTexture = new Texture2D(mapSize, mapSize, TextureFormat.RGBA32, false);
        displayTexture.wrapMode = TextureWrapMode.Clamp;
        displayTexture.filterMode = FilterMode.Bilinear;
    }

    // ---------- target computation ----------

    public void RecomputeTargets()
    {
        switch (viewMode)
        {
            case ViewMode.Idle:
                FillFlat();
                break;
            case ViewMode.Combined:
                ApplyCombined();
                break;
            default:
                int idx = (int)viewMode - 1;
                if (idx >= 0 && idx < layers.Length) ApplySingleLayer(layers[idx]);
                break;
        }
        textureDirty = true;
    }

    void FillFlat()
    {
        for (int i = 0; i < targetHeights.Length; i++)
        {
            targetHeights[i] = 0f;
            targetColors[i] = new Color(0.15f, 0.15f, 0.18f, 1f);
        }
    }

    void ApplySingleLayer(NoiseLayerSettings s)
    {
        float[,] map = Noise.GenerateNoiseMap(mapSize, mapSize, seed + s.seedOffset, s.scale, s.octaves, s.persistence, s.lacunarity, offset);
        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int i = y * mapSize + x;
                float n = Mathf.Clamp01(map[x, y]);
                float evaluated = s.curve.Evaluate(n);
                lastNormalizedHeight[i] = evaluated;
                targetColors[i] = new Color(evaluated, evaluated, evaluated, 1f);
            }
        }
        ApplyHeightMultiplierOnly();
    }

    void ApplyCombined()
    {
        NoiseLayerSettings c = layers[0], e = layers[1], pv = layers[2];
        float[,] cMap = Noise.GenerateNoiseMap(mapSize, mapSize, seed + c.seedOffset, c.scale, c.octaves, c.persistence, c.lacunarity, offset);
        float[,] eMap = Noise.GenerateNoiseMap(mapSize, mapSize, seed + e.seedOffset, e.scale, e.octaves, e.persistence, e.lacunarity, offset);
        float[,] pvMap = Noise.GenerateNoiseMap(mapSize, mapSize, seed + pv.seedOffset, pv.scale, pv.octaves, pv.persistence, pv.lacunarity, offset);

        float[,] combined = new float[mapSize, mapSize];
        float minV = float.MaxValue, maxV = float.MinValue;

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                // mirrors MapGenerator.CombineNoiseMaps, but each layer is individually toggleable
                // for the demo. PeaksValleys is a multiplier, so it defaults to 1 (not 0) when off.
                float cV = c.enabled ? c.curve.Evaluate(cMap[x, y]) : 0f;
                float eV = e.enabled ? e.curve.Evaluate(eMap[x, y]) : 0f;
                float pvV = pv.enabled ? pv.curve.Evaluate(pvMap[x, y]) : 1f;

                float h = cV + eV * pvV;
                combined[x, y] = h;
                if (h < minV) minV = h;
                if (h > maxV) maxV = h;
            }
        }

        if (Mathf.Approximately(minV, maxV)) maxV = minV + 0.0001f;

        for (int y = 0; y < mapSize; y++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                int i = y * mapSize + x;
                float norm = Mathf.InverseLerp(minV, maxV, combined[x, y]);
                lastNormalizedHeight[i] = norm;
                targetColors[i] = ColorForHeight(norm);
            }
        }
        ApplyHeightMultiplierOnly();
    }

    Color ColorForHeight(float h)
    {
        if (regions == null || regions.Length == 0) return new Color(h, h, h, 1f);
        for (int i = 0; i < regions.Length; i++)
            if (h <= regions[i].height) return regions[i].color;
        return regions[regions.Length - 1].color;
    }

    // Reapplies heightMultiplier / displayMode to the cached normalized noise without regenerating it.
    void ApplyHeightMultiplierOnly()
    {
        bool flatten = viewMode == ViewMode.Idle || displayMode == DisplayMode.Texture;
        for (int i = 0; i < targetHeights.Length; i++)
            targetHeights[i] = flatten ? 0f : lastNormalizedHeight[i] * heightMultiplier;
        textureDirty = true;
    }

    // ---------- transition + apply ----------

    bool LerpTowardsTarget()
    {
        bool stillMoving = false;
        float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime); // framerate-independent exponential smoothing

        for (int i = 0; i < currentHeights.Length; i++)
        {
            currentHeights[i] = Mathf.Lerp(currentHeights[i], targetHeights[i], t);
            if (!stillMoving && Mathf.Abs(currentHeights[i] - targetHeights[i]) > 0.001f) stillMoving = true;
        }

        if (textureDirty || stillMoving)
        {
            for (int i = 0; i < currentColors.Length; i++)
                currentColors[i] = Color.Lerp(currentColors[i], targetColors[i], t);
            ApplyTexture();
            if (!stillMoving) textureDirty = false;
        }
        return stillMoving;
    }

    void ApplyHeightsToMesh()
    {
        for (int i = 0; i < workingVertices.Length; i++)
        {
            workingVertices[i].x = baseVertices[i].x;
            workingVertices[i].z = baseVertices[i].z;
            workingVertices[i].y = currentHeights[i];
        }
        mesh.SetVertices(workingVertices);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void ApplyTexture()
    {
        displayTexture.SetPixels(currentColors); // currentColors is row-major y*mapSize+x, matches SetPixels layout
        displayTexture.Apply(false);
        if (material != null) material.mainTexture = displayTexture;
    }

    // ---------- public API for UI ----------

    public void SetViewMode(int i)
    {
        viewMode = (ViewMode)i;
        RecomputeTargets();
    }

    public void SetDisplayMode(int i)
    {
        displayMode = (DisplayMode)i;
        if (viewMode == ViewMode.Idle) return;
        ApplyHeightMultiplierOnly();
    }

    public void SetHeightMultiplier(float v)
    {
        heightMultiplier = v;
        if (viewMode == ViewMode.Idle) return;
        ApplyHeightMultiplierOnly();
    }

    public void SetSeed(float v)
    {
        seed = Mathf.RoundToInt(v);
        RecomputeTargets();
    }

    public void SetOffsetX(float v) { offset.x = v; RecomputeTargets(); }
    public void SetOffsetY(float v) { offset.y = v; RecomputeTargets(); }

    public void SetLayerScale(int layerIndex, float v) { layers[layerIndex].scale = v; RefreshIfRelevant(layerIndex); }
    public void SetLayerOctaves(int layerIndex, float v) { layers[layerIndex].octaves = Mathf.RoundToInt(v); RefreshIfRelevant(layerIndex); }
    public void SetLayerPersistence(int layerIndex, float v) { layers[layerIndex].persistence = v; RefreshIfRelevant(layerIndex); }
    public void SetLayerLacunarity(int layerIndex, float v) { layers[layerIndex].lacunarity = Mathf.Max(1f, v); RefreshIfRelevant(layerIndex); }

    public void SetLayerEnabled(int layerIndex, bool v)
    {
        layers[layerIndex].enabled = v;
        if (viewMode == ViewMode.Combined) RecomputeTargets();
    }

    void RefreshIfRelevant(int layerIndex)
    {
        if (viewMode == ViewMode.Combined) { RecomputeTargets(); return; }
        if ((int)viewMode - 1 == layerIndex) RecomputeTargets();
    }
}