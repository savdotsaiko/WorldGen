using UnityEngine;

[CreateAssetMenu(fileName = "ObjectPlacementSettings", menuName = "World/Object Placement Settings")]
public class ObjectPlacementSettings : ScriptableObject
{
    public GameObject[] prefabs;
    public int countPerChunk = 20;
    public float heightMin = 0.4f;
    public float heightMax = 0.85f;
    public float maxSlope = 25f;
    public float scaleMin = 0.8f;
    public float scaleMax = 1.4f;
    public Vector3 rotationOffset = Vector3.zero;
    public int seedOffset = 0;
}