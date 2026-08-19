using System.Collections.Generic;
using UnityEngine;
public class EndlessWorld : MonoBehaviour
{
    public static int currentGenerationVersion = 0;
    const float scale = 1;
    const float chunkUpdateThreshold = 25f;
    const float sqrchunkUpdateThreshold = chunkUpdateThreshold * chunkUpdateThreshold;
    public LevelOfDetailInfo[] detailLevels;
    public static float maxViewDistance;
    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    public static Vector2 viewerPositionold;
    static MapGenerator mapGenerator;
    int chunkSize;
    int chunksVisibleInViewDistance;
    public Dictionary<Vector2, TerrainChunk> GetAllChunks() => allChunksLoaded ? terrainChunkDictionary : null;
    private bool allChunksLoaded = false;
    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new();
    static List<TerrainChunk> terrainChunksVisibleLastUpdate = new();
    void Start()
    {
        chunkSize = MapGenerator.mapChunkSize - 1;
        maxViewDistance = detailLevels[detailLevels.Length - 1].threshold;
        chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / chunkSize);
        mapGenerator = FindFirstObjectByType<MapGenerator>();
        UpdateChunks();
    }


    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;
        if ((viewerPositionold - viewerPosition).sqrMagnitude > sqrchunkUpdateThreshold)
        {
            viewerPositionold = viewerPosition;
            UpdateChunks();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            RegenerateWorld();
        }
        if (!allChunksLoaded)
        {
            foreach (var chunk in terrainChunkDictionary.Values)
            {
                if (chunk.MeshReady)
                {
                    allChunksLoaded = true;
                    break;
                }
            }
        }
    }
    private int generationVersion = 0;
    public void RegenerateWorld()
    {
        currentGenerationVersion++; 
        allChunksLoaded = false;
        FindFirstObjectByType<SpiderFormation>().SnapAllSpiders();
        mapGenerator.seed = Random.Range(0, int.MaxValue);

        foreach (var chunk in terrainChunkDictionary.Values)
            Destroy(chunk.MeshObject);

        terrainChunkDictionary.Clear();
        terrainChunksVisibleLastUpdate.Clear();
        UpdateChunks();
    }
    void UpdateChunks()
    {
        for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
        {
            terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDistance; yOffset <= chunksVisibleInViewDistance; yOffset++)
        {
            for (int xOffset = -chunksVisibleInViewDistance; xOffset <= chunksVisibleInViewDistance; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    terrainChunkDictionary[viewedChunkCoord].Update();
                }
                else
                {
                    terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial, currentGenerationVersion));
                }
            }
        }
    }
    public TerrainChunk GetChunkAt(Vector2 worldPosXZ)
    {
        int cx = Mathf.RoundToInt(worldPosXZ.x / chunkSize);
        int cy = Mathf.RoundToInt(worldPosXZ.y / chunkSize);
        return terrainChunkDictionary.TryGetValue(new Vector2(cx, cy), out var chunk) ? chunk : null;
    }

    public bool IsChunkReadyAt(Vector2 worldPosXZ)
    {
        var chunk = GetChunkAt(worldPosXZ);
        return chunk != null && chunk.MeshReady;
    }
    public class TerrainChunk
    {
        public GameObject MeshObject => meshObject;
        public Vector2 Position => pos;
        public MapData MapData => mapData;
        public bool MapDataReady => mapDataReceived;
        public bool MeshReady;

        GameObject meshObject;
        Vector2 pos;
        Bounds bounds;

        MapData mapData;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;
        LevelOfDetailInfo[] detailLevel;
        LODMesh[] lodMeshes;
        LODMesh collisionLODMesh;

        bool mapDataReceived;
        int previousLODIndex = -1;
        int myVersion;
        public TerrainChunk(Vector2 coord, int size, LevelOfDetailInfo[] detailLevel, Transform parent, Material mat, int version)
        {
            myVersion = version;
            this.detailLevel = detailLevel;
            this.MeshReady = false;
            pos = coord * size;
            bounds = new Bounds(pos, Vector2.one * size);
            Vector3 posv3 = new Vector3(pos.x, 0, pos.y);
            meshObject = new GameObject("Terrain Chunk");
            meshObject.layer = LayerMask.NameToLayer("Ground");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material = mat;
            meshObject.transform.position = posv3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;
            SetVisible(false);

            lodMeshes = new LODMesh[detailLevel.Length];
            for (int i = 0; i < detailLevel.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevel[i].lod, Update);
                if (detailLevel[i].useForCollider)
                {
                    collisionLODMesh = lodMeshes[i];
                }
            }
            mapGenerator.RequestMapData(pos, OnMapDataReceived);
        }

        void OnMapDataReceived(MapData mapData)
        {
            if (myVersion != currentGenerationVersion) return; 

            this.mapData = mapData;
            mapDataReceived = true;
            Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            meshRenderer.material.mainTexture = texture;
            Update();
        }
        public void Update()
        {
            if (!mapDataReceived)
            {
                return;
            }
            float viewerDist = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            bool visible = viewerDist <= maxViewDistance;
            if (visible)
            {
                int lodIndex = 0;
                for (int i = 0; i < detailLevel.Length - 1; i++)
                {
                    if (viewerDist > detailLevel[i].threshold)
                    {
                        lodIndex = i + 1;
                    }
                    else
                    {
                        break;
                    }
                }
                if (lodIndex != previousLODIndex)
                {
                    LODMesh lodMesh = lodMeshes[lodIndex];
                    if (lodMesh.hasMesh)
                    {
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    }
                    else if (!lodMesh.hasRequestedMesh)
                    {
                        lodMesh.RequestMesh(mapData);
                    }
                }
                if (lodIndex == 0)
                {
                    if (collisionLODMesh.hasMesh)
                    {
                        meshCollider.sharedMesh = collisionLODMesh.mesh;
                        MeshReady = true;
                        FindFirstObjectByType<SpiderFormation>().SnapAllSpiders();
                    }
                    else if (!collisionLODMesh.hasRequestedMesh)
                    {
                        collisionLODMesh.RequestMesh(mapData);
                    }
                }
                terrainChunksVisibleLastUpdate.Add(this);
            }
            SetVisible(visible);
        }
        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }
        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        System.Action updateCallback;
        public LODMesh(int lod, System.Action updateCallback)
        {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        void onMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;
            updateCallback();
        }
        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, onMeshDataReceived, lod);
        }
    }
    [System.Serializable]
    public struct LevelOfDetailInfo
    {
        public int lod;
        public float threshold;
        public bool useForCollider;
        public LevelOfDetailInfo(int lod, float threshold, bool useForCollider)
        {
            this.lod = lod;
            this.threshold = threshold;
            this.useForCollider = useForCollider;
        }
    }
}
