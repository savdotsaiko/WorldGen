using UnityEngine;
using System.Collections.Generic;

public class GrassManager : MonoBehaviour
{
    public static GrassManager Instance { get; private set; }

    public Transform viewer;
    public Material grassMaterial;
    public float grassDrawDistance = 100f;
    public float grassUpdateDistance = 80f;
    public int densityPerChunkSide = 40;

    private Mesh _grassMesh;
    private EndlessWorld _endlessWorld;
    private MapGenerator _mapGenerator;
    private Dictionary<Vector2, GrassChunk> _activeGrass = new();
    private float _checkTimer;

    void Awake()
    {
        Instance = this;
        _grassMesh = GrassMeshBuilder.Build();
        _endlessWorld = Object.FindFirstObjectByType<EndlessWorld>();
        _mapGenerator = Object.FindFirstObjectByType<MapGenerator>();
    }

    void Update()
    {
        _checkTimer += Time.deltaTime;
        if (_checkTimer < 0.5f) return;
        _checkTimer = 0f;
        UpdateGrass();
    }

    void UpdateGrass()
    {
        var chunks = _endlessWorld.GetAllChunks();
        if (chunks != null)
        {
            foreach (var kvp in chunks)
            {
                Vector2 coord = kvp.Key;
                EndlessWorld.TerrainChunk chunk = kvp.Value;

                if (!chunk.MapDataReady) continue;

                float dist = Vector3.Distance(
                    viewer.position,
                    new Vector3(chunk.Position.x, 0, chunk.Position.y));

                if (dist < grassUpdateDistance)
                {
                    if (!_activeGrass.ContainsKey(coord))
                        AddGrass(coord, chunk);
                }
                else
                {
                    if (_activeGrass.ContainsKey(coord))
                        RemoveGrass(coord);
                }
            }
        }
    }

    void AddGrass(Vector2 coord, EndlessWorld.TerrainChunk chunk)
    {
        Debug.Log($"AddGrass called for {coord}");
        int chunkSize = MapGenerator.mapChunkSize - 1;
        Vector3 origin = new Vector3(
            chunk.Position.x - chunkSize * 0.5f,
            0,
            chunk.Position.y - chunkSize * 0.5f);

        GrassChunk gc = chunk.MeshObject.AddComponent<GrassChunk>();
        gc.densityPerChunkSide = densityPerChunkSide;
        gc.groundMask = LayerMask.GetMask("Ground");
        gc.Initialise(
            chunkSize,
            origin,
            chunk.MapData.heightMap,
            _mapGenerator.heightMult,
            _mapGenerator.meshHeightCurve,
            _mapGenerator.seed,
            grassMaterial,
            _grassMesh);

        _activeGrass[coord] = gc;
    }

    void RemoveGrass(Vector2 coord)
    {
        if (_activeGrass.TryGetValue(coord, out var gc))
        {
            gc.Release();
            Destroy(gc);
            _activeGrass.Remove(coord);
        }
    }
}