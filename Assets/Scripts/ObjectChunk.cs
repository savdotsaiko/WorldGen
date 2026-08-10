using UnityEngine;
using System.Collections.Generic;

public class ObjectChunk : MonoBehaviour
{
    private List<GameObject> _spawnedObjects = new();

    public void Initialise(float chunkSize, Vector3 origin, float[,] heightMap,
        float heightMultiplier, AnimationCurve heightCurve,
        int seed, ObjectPlacementSettings settings, LayerMask groundMask)
    {
        var rng = new System.Random(seed + 9999 + settings.seedOffset);
        int mapW = heightMap.GetLength(0);
        int mapH = heightMap.GetLength(1);

        for (int i = 0; i < settings.countPerChunk; i++)
        {
            float localX = (float)(rng.NextDouble() * chunkSize);
            float localZ = (float)(rng.NextDouble() * chunkSize);

            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / chunkSize * (mapW - 1)), 0, mapW - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt((1f - localZ / chunkSize) * (mapH - 1)), 0, mapH - 1);

            float heightNormalised = heightMap[hx, hz];
            if (heightNormalised < settings.heightMin || heightNormalised > settings.heightMax) continue;

            int hx1 = Mathf.Clamp(hx + 1, 0, mapW - 1);
            int hz1 = Mathf.Clamp(hz + 1, 0, mapH - 1);
            float dhdx = (heightMap[hx1, hz] - heightMap[hx, hz]) * heightMultiplier;
            float dhdz = (heightMap[hx, hz1] - heightMap[hx, hz]) * heightMultiplier;
            float slope = Mathf.Atan2(Mathf.Sqrt(dhdx * dhdx + dhdz * dhdz), 1f) * Mathf.Rad2Deg;
            if (slope > settings.maxSlope) continue;

            Vector3 rayOrigin = new Vector3(origin.x + localX, heightMultiplier + 500f, origin.z + localZ);
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, heightMultiplier + 600f, groundMask)) continue;

            GameObject prefab = settings.prefabs[rng.Next(settings.prefabs.Length)];
            float yRot = (float)(rng.NextDouble() * 360.0);

            float scaleMult = Mathf.Lerp(settings.scaleMin, settings.scaleMax, (float)rng.NextDouble());

            Quaternion rot = Quaternion.Euler(0, yRot, 0) * Quaternion.Euler(settings.rotationOffset);

            GameObject obj = Instantiate(prefab, hit.point, rot, transform);

            obj.transform.localScale = prefab.transform.localScale * scaleMult;
            _spawnedObjects.Add(obj);
        }
        Debug.Log($"ObjectChunk: spawned {_spawnedObjects.Count} objects");
    }

    void OnDestroy()
    {
        foreach (var obj in _spawnedObjects)
            if (obj != null) Destroy(obj);
        _spawnedObjects.Clear();
    }
}