using UnityEngine;

namespace GrassFlow
{
    public static class GrassBladeMesh
    {
        public static Mesh Create(int segments)
        {
            var vertices = new Vector3[segments * 2 + 1];
            var uv = new Vector2[vertices.Length];
            var indices = new int[(segments * 2 - 1) * 3];
            int cursor = 0;
            for (int level = 0; level < segments; level++)
            {
                float t = level / (float)segments;
                int a = level * 2;
                vertices[a] = new Vector3(-0.025f * (1 - t), t, 0);
                vertices[a + 1] = new Vector3(0.025f * (1 - t), t, 0);
                uv[a] = new Vector2(0, t); uv[a + 1] = new Vector2(1, t);
                if (level < segments - 1)
                {
                    indices[cursor++] = a; indices[cursor++] = a + 2; indices[cursor++] = a + 1;
                    indices[cursor++] = a + 1; indices[cursor++] = a + 2; indices[cursor++] = a + 3;
                }
            }
            vertices[vertices.Length - 1] = Vector3.up; uv[uv.Length - 1] = new Vector2(0.5f, 1);
            indices[cursor++] = vertices.Length - 3;
            indices[cursor++] = vertices.Length - 1;
            indices[cursor] = vertices.Length - 2;
            var mesh = new Mesh { name = $"Grass {segments} segments", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.up * 0.5f, new Vector3(2, 2, 2));
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
