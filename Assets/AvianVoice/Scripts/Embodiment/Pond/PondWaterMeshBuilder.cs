using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Builds an organic pond-shaped horizontal water mesh (fan from center + irregular shoreline).
    /// </summary>
    public static class PondWaterMeshBuilder
    {
        public static Mesh CreatePondMesh(
            int segments = 72,
            float radiusX = 11f,
            float radiusZ = 9f,
            float edgeIrregularity = 0.24f,
            int shapeSeed = 7)
        {
            segments = Mathf.Max(12, segments);
            var mesh = new Mesh { name = "PondWaterMesh" };

            var ringCount = segments + 1;
            var vertexCount = 1 + ringCount;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var tangents = new Vector4[vertexCount];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);
            tangents[0] = new Vector4(1f, 0f, 0f, 1f);

            var rng = new System.Random(shapeSeed);
            var harmonicA = 0.55f + (float)rng.NextDouble() * 0.35f;
            var harmonicB = 1.15f + (float)rng.NextDouble() * 0.55f;
            var harmonicC = 2.05f + (float)rng.NextDouble() * 0.75f;
            var phaseA = (float)rng.NextDouble() * Mathf.PI * 2f;
            var phaseB = (float)rng.NextDouble() * Mathf.PI * 2f;
            var phaseC = (float)rng.NextDouble() * Mathf.PI * 2f;

            for (var i = 0; i < ringCount; i++)
            {
                var t = i / (float)segments;
                var angle = t * Mathf.PI * 2f;
                var wobble = Mathf.Sin(angle * harmonicA + phaseA) * 0.38f
                             + Mathf.Sin(angle * harmonicB + phaseB) * 0.24f
                             + Mathf.Sin(angle * harmonicC + phaseC) * 0.14f;
                var scale = 1f + wobble * edgeIrregularity;
                var x = Mathf.Cos(angle) * radiusX * scale;
                var z = Mathf.Sin(angle) * radiusZ * scale;

                var index = 1 + i;
                vertices[index] = new Vector3(x, 0f, z);
                normals[index] = Vector3.up;
                uvs[index] = new Vector2(
                    x / (radiusX * 1.15f) * 0.5f + 0.5f,
                    z / (radiusZ * 1.15f) * 0.5f + 0.5f);
                tangents[index] = new Vector4(1f, 0f, 0f, 1f);
            }

            for (var i = 0; i < segments; i++)
            {
                var tri = i * 3;
                triangles[tri] = 0;
                triangles[tri + 1] = 1 + i;
                triangles[tri + 2] = 1 + i + 1;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
