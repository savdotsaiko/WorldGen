using UnityEngine;
using System.Collections.Generic;

public class ObjectChunkManager : MonoBehaviour
{
    public Transform viewer;
    public float spawnDistance = 120f;
    public ObjectPlacementSettings[] settingsLayers;

    private EndlessWorld _endlessWorld;
    private MapGenerator _mapGenerator;
    private Dictionary<Vector2, List<ObjectChunk>> _active = new();
    private float _timer;
    public LayerMask groundMask;
    void Awake()
    {
        _endlessWorld = Object.FindFirstObjectByType<EndlessWorld>();
        _mapGenerator = Object.FindFirstObjectByType<MapGenerator>();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < 0.5f) return;
        _timer = 0f;

        var chunks = _endlessWorld.GetAllChunks();

        foreach (var kvp in chunks)
        {
            Vector2 coord = kvp.Key;
            EndlessWorld.TerrainChunk chunk = kvp.Value;
            if (!chunk.MapDataReady) continue;

            float dist = Vector3.Distance(viewer.position,
                new Vector3(chunk.Position.x, 0, chunk.Position.y));

            if (dist < spawnDistance)
            {
                if (!_active.ContainsKey(coord))
                    Spawn(coord, chunk);
            }
            else
            {
                if (_active.ContainsKey(coord))
                    Despawn(coord);
            }
        }
    }

    void Spawn(Vector2 coord, EndlessWorld.TerrainChunk chunk)
    {
        Debug.Log($"Spawning objects at {coord}");
        int chunkSize = MapGenerator.mapChunkSize - 1;
        Vector3 origin = new Vector3(
            chunk.Position.x - chunkSize * 0.5f, 0,
            chunk.Position.y - chunkSize * 0.5f);

        var list = new List<ObjectChunk>();

        foreach (var settings in settingsLayers)
        {
            ObjectChunk oc = chunk.MeshObject.AddComponent<ObjectChunk>();
            oc.Initialise(chunkSize, origin, chunk.MapData.heightMap,
    _mapGenerator.heightMult, _mapGenerator.meshHeightCurve,
    _mapGenerator.seed, settings, groundMask);
            list.Add(oc);
        }

        _active[coord] = list;
    }

    void Despawn(Vector2 coord)
    {
        if (_active.TryGetValue(coord, out var list))
        {
            foreach (var oc in list)
                if (oc != null) Destroy(oc);
            _active.Remove(coord);
        }
    }
}