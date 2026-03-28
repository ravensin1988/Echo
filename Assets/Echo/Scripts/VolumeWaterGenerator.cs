// C# скрипт для генерации объемной воды
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class VolumeWaterGenerator : MonoBehaviour
{
    public int widthSegments = 50;
    public int lengthSegments = 50;
    public float height = 5f;
    
    void Start()
    {
        GenerateVolumeMesh();
        GetComponent<MeshCollider>().sharedMesh = GetComponent<MeshFilter>().mesh;
    }
    
    void GenerateVolumeMesh()
    {
        Mesh mesh = new Mesh();
        
        int verticesCount = (widthSegments + 1) * (lengthSegments + 1) * 2;
        Vector3[] vertices = new Vector3[verticesCount];
        Vector2[] uv = new Vector2[verticesCount];
        int[] triangles = new int[widthSegments * lengthSegments * 6 * 2];
        
        // Генерация вершин для поверхности и дна
        int vertexIndex = 0;
        for (int z = 0; z <= lengthSegments; z++)
        {
            for (int x = 0; x <= widthSegments; x++)
            {
                // Верхняя поверхность (вода)
                float xPos = (float)x / widthSegments;
                float zPos = (float)z / lengthSegments;
                
                vertices[vertexIndex] = new Vector3(
                    xPos - 0.5f, 
                    0, // Вершины на поверхности
                    zPos - 0.5f
                );
                uv[vertexIndex] = new Vector2(xPos, zPos);
                vertexIndex++;
                
                // Нижняя поверхность (дно воды)
                vertices[vertexIndex] = new Vector3(
                    xPos - 0.5f, 
                    -height, // Нижние вершины
                    zPos - 0.5f
                );
                uv[vertexIndex] = new Vector2(xPos, zPos);
                vertexIndex++;
            }
        }
        
        // Генерация треугольников
        int triIndex = 0;
        for (int z = 0; z < lengthSegments; z++)
        {
            for (int x = 0; x < widthSegments; x++)
            {
                int topLeft = (z * (widthSegments + 1) + x) * 2;
                int topRight = topLeft + 2;
                int bottomLeft = topLeft + 1;
                int bottomRight = topLeft + 3;
                
                // Боковые грани (4 стороны)
                // Передняя грань
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topRight;
                
                triangles[triIndex++] = topRight;
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = bottomRight;
                
                // Задняя грань
                triangles[triIndex++] = topRight + 2;
                triangles[triIndex++] = bottomRight + 2;
                triangles[triIndex++] = topLeft + 2;
                
                triangles[triIndex++] = topLeft + 2;
                triangles[triIndex++] = bottomRight + 2;
                triangles[triIndex++] = bottomLeft + 2;
                
                // Боковые грани для объема
                int nextRow = ((z + 1) * (widthSegments + 1) + x) * 2;
                // ... добавляйте остальные треугольники для боковых сторон
            }
        }
        
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        GetComponent<MeshFilter>().mesh = mesh;
    }
}