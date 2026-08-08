using UnityEngine;

public static class MeshGenerator
{
    public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMult, AnimationCurve _meshHeightCurve, int levelOfDetail)
    {
        AnimationCurve meshHeightCurve = new AnimationCurve(_meshHeightCurve.keys);
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);
        float topLeftX = (width - 1) / -2f;
        float topLeftZ = (height - 1) / 2f;

        int lodIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;
        int verticesPerLine = (width - 1) / lodIncrement + 1;

        MeshData meshData = new MeshData(verticesPerLine, verticesPerLine);
        int vertexIndex = 0;

        for (int y = 0; y < height; y += lodIncrement)
        {
            for (int x = 0; x < width; x += lodIncrement)
            {
                meshData.vertices[vertexIndex] = new Vector3(topLeftX + x,meshHeightCurve.Evaluate(heightMap[x, y]) * heightMult, topLeftZ - y);
                meshData.uvs[vertexIndex] = new Vector2(x / (float)width, y / (float)height);

                if (x < width - 1 && y < height - 1)
                {
                    meshData.AddTriangle(vertexIndex, vertexIndex + verticesPerLine + 1, vertexIndex + verticesPerLine);
                    meshData.AddTriangle(vertexIndex + verticesPerLine + 1, vertexIndex, vertexIndex + 1);
                }

                vertexIndex++;
            }
        }
        return meshData;
    }
}


public class MeshData
{
    public Vector3[] vertices;
    public int[] tringles;
    public Vector2[] uvs;

    int triangleIndex;
    public MeshData(int width, int height)
    {
        vertices = new Vector3[width * height];
        uvs = new Vector2[width * height];
        tringles = new int[(width - 1) * (height - 1) * 6];
    }

    public void AddTriangle(int a, int b, int c)
    {
        tringles[triangleIndex] = a;
        tringles[triangleIndex + 1] = b;
        tringles[triangleIndex + 2] = c;
        triangleIndex += 3;
    }

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = tringles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        return mesh;
    }
}