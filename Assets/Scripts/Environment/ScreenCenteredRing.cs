using UnityEngine;

namespace ProjectArtOfAll.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ScreenCenteredRing : MonoBehaviour
    {
        [Header("Radius")]
        [SerializeField, Min(0.01f)] private float outerRadius = 3f;
        [SerializeField, Min(0f)] private float innerRadius = 2.82f;

        [Header("Quality")]
        [SerializeField, Range(24, 512)] private int segments = 160;

        [SerializeField, HideInInspector] private Mesh generatedMesh;

        public float OuterRadius => outerRadius;
        public float InnerRadius => innerRadius;
        public float PathRadius => (outerRadius + innerRadius) * 0.5f;

        private void OnEnable()
        {
            RebuildMesh();
        }

        private void OnValidate()
        {
            RebuildMesh();
        }

        public void RebuildMesh()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                return;
            }

            outerRadius = Mathf.Max(0.01f, outerRadius);
            innerRadius = Mathf.Clamp(innerRadius, 0f, outerRadius - 0.005f);
            segments = Mathf.Clamp(segments, 24, 512);

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = "WhiteRing_Generated",
                    hideFlags = HideFlags.DontSave
                };
            }

            generatedMesh.Clear();

            int ringVertexCount = segments + 1;
            Vector3[] vertices = new Vector3[ringVertexCount * 2];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int index = 0; index <= segments; index++)
            {
                float angle = index / (float)segments * Mathf.PI * 2f;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);

                vertices[index * 2] = new Vector3(
                    cosine * innerRadius,
                    sine * innerRadius,
                    0f);
                vertices[index * 2 + 1] = new Vector3(
                    cosine * outerRadius,
                    sine * outerRadius,
                    0f);

                normals[index * 2] = Vector3.back;
                normals[index * 2 + 1] = Vector3.back;

                float u = index / (float)segments;
                uv[index * 2] = new Vector2(u, 0f);
                uv[index * 2 + 1] = new Vector2(u, 1f);

                if (index == segments)
                {
                    continue;
                }

                int vertex = index * 2;
                int next = vertex + 2;
                int triangle = index * 6;

                triangles[triangle] = vertex;
                triangles[triangle + 1] = next;
                triangles[triangle + 2] = vertex + 1;

                triangles[triangle + 3] = next;
                triangles[triangle + 4] = next + 1;
                triangles[triangle + 5] = vertex + 1;
            }

            generatedMesh.vertices = vertices;
            generatedMesh.normals = normals;
            generatedMesh.uv = uv;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();

            meshFilter.sharedMesh = generatedMesh;
        }
    }
}
