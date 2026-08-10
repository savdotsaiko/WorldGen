using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
public class GrassChunk : MonoBehaviour
{
    [Header("Settings")]
    public int densityPerChunkSide = 40;
    public float heightMin = 0.3f;
    public float heightMax = 0.55f;
    public float maxSlope = 30f;
    public float scaleMin = 0.8f;
    public float scaleMax = 1.4f;

    private ComputeBuffer _instanceBuffer;
    private ComputeBuffer _argsBuffer;
    private Material _material;
    private Mesh _mesh;
    private int _instanceCount;
    private bool _ready = false;
    private Bounds _drawBounds;
    public LayerMask groundMask;

    // Called by your TerrainChunk after the mesh is ready
    public void Initialise(float chunkWorldSize, Vector3 chunkOrigin,
                       float[,] heightMap, float heightMultiplier,
                       AnimationCurve heightCurve,
                       int seed, Material grassMaterial, Mesh grassMesh)
    {
        _material = new Material(grassMaterial);
        _mesh = grassMesh;

        var matrices = GeneratePlacements(
            chunkWorldSize, chunkOrigin, heightMap, heightMultiplier, heightCurve, seed);
        if (matrices.Count == 0) return;

        _instanceCount = matrices.Count;

        // Upload to GPU
        _instanceBuffer = new ComputeBuffer(_instanceCount, sizeof(float) * 16);
        _instanceBuffer.SetData(matrices);
        _material.SetBuffer("_InstanceBuffer", _instanceBuffer);
        Debug.Log($"Buffer set with {_instanceCount} instances. Material: {_material != null} Mesh: {_mesh != null} Args: {_argsBuffer != null}");

        // Indirect args buffer: indexCount, instanceCount, startIndex, baseVertex, startInstance
        uint[] args = new uint[5];
        args[0] = (uint)_mesh.GetIndexCount(0);
        args[1] = (uint)_instanceCount;
        args[2] = (uint)_mesh.GetIndexStart(0);
        args[3] = (uint)_mesh.GetBaseVertex(0);
        args[4] = 0;
        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint),
                                        ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        _drawBounds = new Bounds(chunkOrigin + Vector3.up * heightMultiplier * 0.5f,
                                 Vector3.one * chunkWorldSize * 2f);
        _ready = true;
    }

    void Update()
    {
        if (!_ready) return;
        Graphics.DrawMeshInstancedIndirect(_mesh, 0, _material, _drawBounds, _argsBuffer);
    }

    void OnDestroy() => Release();

    public void Release()
    {
        _instanceBuffer?.Release();
        _argsBuffer?.Release();
        _ready = false;
    }

    private List<Matrix4x4> GeneratePlacements(
    float chunkSize, Vector3 origin,
    float[,] heightMap, float heightMultiplier,
    AnimationCurve heightCurve,
    int seed)
    {
        var result = new List<Matrix4x4>();
        var rng = new System.Random(seed);
        int mapW = heightMap.GetLength(0);
        int mapH = heightMap.GetLength(1);
        float step = chunkSize / densityPerChunkSide;

        for (int row = 0; row < densityPerChunkSide; row++)
        {
            for (int col = 0; col < densityPerChunkSide; col++)
            {
                float jx = (float)(rng.NextDouble() - 0.5) * step;
                float jz = (float)(rng.NextDouble() - 0.5) * step;

                float localX = col * step + step * 0.5f + jx;
                float localZ = row * step + step * 0.5f + jz;

                int hx = Mathf.Clamp(Mathf.RoundToInt(localX / chunkSize * (mapW - 1)), 0, mapW - 1);
                int hz = Mathf.Clamp(Mathf.RoundToInt((1f - localZ / chunkSize) * (mapH - 1)), 0, mapH - 1);

                float heightNormalised = heightMap[hx, hz];
                if (heightNormalised < heightMin || heightNormalised > heightMax) continue;

                int hx1 = Mathf.Clamp(hx + 1, 0, mapW - 1);
                int hz1 = Mathf.Clamp(hz + 1, 0, mapH - 1);
                float dhdx = (heightMap[hx1, hz] - heightMap[hx, hz]) * heightMultiplier;
                float dhdz = (heightMap[hx, hz1] - heightMap[hx, hz]) * heightMultiplier;
                float slope = Mathf.Atan2(Mathf.Sqrt(dhdx * dhdx + dhdz * dhdz), 1f) * Mathf.Rad2Deg;
                if (slope > maxSlope) continue;

                Vector3 rayOrigin = new Vector3(origin.x + localX, heightMultiplier + 500f, origin.z + localZ);

                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, heightMultiplier + 600f, groundMask))
                    continue;

                float yRot = (float)(rng.NextDouble() * 360.0);
                float scale = Mathf.Lerp(scaleMin, scaleMax, (float)rng.NextDouble());
                Matrix4x4 mat = Matrix4x4.TRS(
                    hit.point,
                    Quaternion.Euler(0, yRot, 0),
                    Vector3.one * scale);
                result.Add(mat);
            }
        }
        return result;
    }
}