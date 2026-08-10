using UnityEngine;

public static class GrassMeshBuilder
{
    public static Mesh Build(float width = 0.05f, float height = 0.2f)
    {
        Mesh mesh = new Mesh();

        float halfW = width * 0.5f;

        Vector3[] verts = new Vector3[]
        {
            new Vector3(-halfW, 0,      0),
            new Vector3( halfW, 0,      0),
            new Vector3(-halfW * 0.5f, height * 0.5f, 0),
            new Vector3( halfW * 0.5f, height * 0.5f, 0),
            new Vector3(0,      height, 0),
        };

        int[] tris = new int[]
        {
            0, 2, 1,
            1, 2, 3,
            2, 4, 3,
        };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0,   0),
            new Vector2(1,   0),
            new Vector2(0,   0.5f),
            new Vector2(1,   0.5f),
            new Vector2(0.5f,1),
        };

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}