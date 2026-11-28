using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Environment
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class ReadyRoomRingWalls : MonoBehaviour
    {
        private const string WallObjectName = "RingWalls";
        private const string ReadyRoomSceneName = "ready room";
        // The Ready Room eye height is 1.7m, so 5.1m is three camera heights.
        private const float WallHeight = 5.1f;
        private const float WallThickness = 0.34f;
        private const float DoorOpeningWidth = 3.12f;
        private const float DoorOpeningHeight = 2.2f;
        private const float BaseOffset = -0.04f;
        private const float MaxDegreesPerSegment = 0.35f;

        private static readonly float[] DoorAngles = { 0f, 90f, 180f, 270f };

        [SerializeField, HideInInspector] private float innerRadius = 40f;
        [SerializeField, HideInInspector] private float outerRadius = 50f;
        [SerializeField, HideInInspector] private float baseHeight;
        [SerializeField, HideInInspector] private Mesh generatedMesh;

        private Material generatedMaterial;
        private bool isBuilding;

        public static ReadyRoomRingWalls EnsureExists(
            Scene scene,
            float ringInnerRadius,
            float ringOuterRadius,
            float ringHeight)
        {
            if (!scene.IsValid() ||
                !scene.isLoaded ||
                !string.Equals(scene.name, ReadyRoomSceneName))
            {
                return null;
            }

            ReadyRoomRingWalls[] walls =
                Object.FindObjectsOfType<ReadyRoomRingWalls>(true);
            for (int index = 0; index < walls.Length; index++)
            {
                ReadyRoomRingWalls wall = walls[index];
                if (wall != null && wall.gameObject.scene == scene)
                {
                    wall.Configure(ringInnerRadius, ringOuterRadius, ringHeight);
                    wall.Rebuild();
                    return wall;
                }
            }

            GameObject wallObject = new GameObject(WallObjectName);
            wallObject.SetActive(false);
            wallObject.hideFlags =
                HideFlags.DontSaveInEditor |
                HideFlags.DontSaveInBuild;
            wallObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(wallObject, scene);

            ReadyRoomRingWalls createdWalls =
                wallObject.AddComponent<ReadyRoomRingWalls>();
            createdWalls.Configure(ringInnerRadius, ringOuterRadius, ringHeight);
            wallObject.SetActive(true);
            return createdWalls;
        }

        private void Configure(
            float newInnerRadius,
            float newOuterRadius,
            float newBaseHeight)
        {
            innerRadius = Mathf.Max(0.5f, newInnerRadius);
            outerRadius = Mathf.Max(innerRadius + WallThickness, newOuterRadius);
            baseHeight = newBaseHeight;
        }

        private void OnEnable()
        {
            if (!string.Equals(
                    gameObject.scene.name,
                    ReadyRoomSceneName))
            {
                ScheduleDestroy();
                return;
            }

            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            Rebuild();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            if (!string.Equals(nextScene.name, ReadyRoomSceneName))
            {
                ScheduleDestroy();
            }
        }

        private void ScheduleDestroy()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= DestroyGeneratedObject;
                UnityEditor.EditorApplication.delayCall += DestroyGeneratedObject;
                return;
            }
#endif
            Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void DestroyGeneratedObject()
        {
            UnityEditor.EditorApplication.delayCall -= DestroyGeneratedObject;
            if (this != null)
            {
                DestroyImmediate(gameObject);
            }
        }
#endif

        private void Rebuild()
        {
            if (isBuilding)
            {
                return;
            }

            isBuilding = true;
            try
            {
                if (generatedMesh != null)
                {
                    DestroyTransient(generatedMesh);
                    generatedMesh = null;
                }

                generatedMesh = CreateWallsMesh();
                generatedMaterial ??= CreateWallMaterial();

                MeshFilter meshFilter = GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
                MeshCollider meshCollider = GetComponent<MeshCollider>();

                meshFilter.sharedMesh = generatedMesh;
                meshRenderer.sharedMaterial = generatedMaterial;
                meshRenderer.shadowCastingMode = ShadowCastingMode.On;
                meshRenderer.receiveShadows = true;
                meshRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = generatedMesh;
            }
            finally
            {
                isBuilding = false;
            }
        }

        private Mesh CreateWallsMesh()
        {
            List<Vector3> vertices = new List<Vector3>(48000);
            List<int> triangles = new List<int>(96000);

            float wallBase = baseHeight + BaseOffset;
            float wallTop = baseHeight + WallHeight;
            float doorwayTop = baseHeight + DoorOpeningHeight;

            AppendWall(
                vertices,
                triangles,
                innerRadius,
                wallBase,
                wallTop,
                doorwayTop,
                true);
            AppendWall(
                vertices,
                triangles,
                outerRadius,
                wallBase,
                wallTop,
                doorwayTop,
                false);

            Mesh mesh = new Mesh
            {
                name = "ReadyRoomRingWalls_Generated",
                hideFlags = HideFlags.DontSave
            };

            if (vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendWall(
            List<Vector3> vertices,
            List<int> triangles,
            float centerRadius,
            float wallBase,
            float wallTop,
            float doorwayTop,
            bool hasDoorOpenings)
        {
            List<float> angles = BuildAngles(hasDoorOpenings, centerRadius);
            float innerFaceRadius = centerRadius - WallThickness * 0.5f;
            float outerFaceRadius = centerRadius + WallThickness * 0.5f;
            float doorHalfAngle = Mathf.Asin(
                Mathf.Min(0.95f, DoorOpeningWidth * 0.5f / centerRadius)) *
                Mathf.Rad2Deg;
            doorwayTop = Mathf.Min(wallTop, doorwayTop);

            for (int index = 0; index < angles.Count - 1; index++)
            {
                float angle0 = angles[index];
                float angle1 = angles[index + 1];
                if (angle1 - angle0 <= 0.0001f)
                {
                    continue;
                }

                bool isDoorway =
                    hasDoorOpenings &&
                    IsInsideDoorway((angle0 + angle1) * 0.5f, doorHalfAngle);
                float lowerY = isDoorway ? doorwayTop : wallBase;

                float radians0 = angle0 * Mathf.Deg2Rad;
                float radians1 = angle1 * Mathf.Deg2Rad;
                float cosine0 = Mathf.Cos(radians0);
                float sine0 = Mathf.Sin(radians0);
                float cosine1 = Mathf.Cos(radians1);
                float sine1 = Mathf.Sin(radians1);

                Vector3 innerBottom0 = new Vector3(
                    cosine0 * innerFaceRadius,
                    lowerY,
                    sine0 * innerFaceRadius);
                Vector3 outerBottom0 = new Vector3(
                    cosine0 * outerFaceRadius,
                    lowerY,
                    sine0 * outerFaceRadius);
                Vector3 innerTop0 = new Vector3(
                    cosine0 * innerFaceRadius,
                    wallTop,
                    sine0 * innerFaceRadius);
                Vector3 outerTop0 = new Vector3(
                    cosine0 * outerFaceRadius,
                    wallTop,
                    sine0 * outerFaceRadius);

                Vector3 innerBottom1 = new Vector3(
                    cosine1 * innerFaceRadius,
                    lowerY,
                    sine1 * innerFaceRadius);
                Vector3 outerBottom1 = new Vector3(
                    cosine1 * outerFaceRadius,
                    lowerY,
                    sine1 * outerFaceRadius);
                Vector3 innerTop1 = new Vector3(
                    cosine1 * innerFaceRadius,
                    wallTop,
                    sine1 * innerFaceRadius);
                Vector3 outerTop1 = new Vector3(
                    cosine1 * outerFaceRadius,
                    wallTop,
                    sine1 * outerFaceRadius);

                AddQuad(
                    vertices,
                    triangles,
                    outerBottom0,
                    outerTop0,
                    outerTop1,
                    outerBottom1);
                AddQuad(
                    vertices,
                    triangles,
                    innerBottom0,
                    innerBottom1,
                    innerTop1,
                    innerTop0);
                AddQuad(
                    vertices,
                    triangles,
                    innerTop0,
                    innerTop1,
                    outerTop1,
                    outerTop0);

                // The two coincident caps make the wall solid from either side.
                AddQuad(
                    vertices,
                    triangles,
                    innerBottom0,
                    outerBottom0,
                    outerTop0,
                    innerTop0);
                AddQuad(
                    vertices,
                    triangles,
                    innerBottom0,
                    innerTop0,
                    outerTop0,
                    outerBottom0);
                AddQuad(
                    vertices,
                    triangles,
                    innerBottom1,
                    outerBottom1,
                    outerTop1,
                    innerTop1);
                AddQuad(
                    vertices,
                    triangles,
                    innerBottom1,
                    innerTop1,
                    outerTop1,
                    outerBottom1);

                if (isDoorway)
                {
                    AddQuad(
                        vertices,
                        triangles,
                        innerBottom0,
                        outerBottom0,
                        outerBottom1,
                        innerBottom1);
                }
            }
        }

        private static List<float> BuildAngles(
            bool includeDoorBoundaries,
            float centerRadius)
        {
            List<float> boundaries = new List<float> { 0f, 360f };
            if (includeDoorBoundaries)
            {
                foreach (float doorAngle in DoorAngles)
                {
                    float halfAngle = Mathf.Asin(
                        Mathf.Min(
                            0.95f,
                            DoorOpeningWidth * 0.5f / centerRadius)) *
                        Mathf.Rad2Deg;
                    AddNormalizedBoundary(boundaries, doorAngle - halfAngle);
                    AddNormalizedBoundary(boundaries, doorAngle + halfAngle);
                }
            }

            boundaries.Sort();
            List<float> samples = new List<float>(1600) { 0f };
            for (int index = 0; index < boundaries.Count - 1; index++)
            {
                float start = boundaries[index];
                float end = boundaries[index + 1];
                float span = end - start;
                int steps = Mathf.Max(1, Mathf.CeilToInt(span / MaxDegreesPerSegment));
                for (int step = 1; step <= steps; step++)
                {
                    float angle = Mathf.Lerp(start, end, step / (float)steps);
                    if (samples[samples.Count - 1] < 360f - 0.0001f)
                    {
                        samples.Add(angle);
                    }
                }
            }

            return samples;
        }

        private static void AddNormalizedBoundary(List<float> boundaries, float angle)
        {
            angle = Mathf.Repeat(angle, 360f);
            if (angle > 0.0001f && angle < 359.9999f)
            {
                boundaries.Add(angle);
            }
        }

        private static bool IsInsideDoorway(float angle, float halfAngle)
        {
            foreach (float doorAngle in DoorAngles)
            {
                float distance = Mathf.Abs(
                    Mathf.DeltaAngle(angle, doorAngle));
                if (distance < halfAngle)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Material CreateWallMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Diffuse");
            }

            Material material = new Material(shader)
            {
                name = "ReadyRoomRingWalls_Generated",
                hideFlags = HideFlags.DontSave,
                globalIlluminationFlags = MaterialGlobalIlluminationFlags.None
            };

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(0.74f, 0.77f, 0.81f, 1f));
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.18f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor(
                    "_EmissionColor",
                    new Color(0.018f, 0.022f, 0.028f, 1f));
                material.EnableKeyword("_EMISSION");
            }

            return material;
        }

        private void OnDestroy()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            MeshCollider meshCollider = GetComponent<MeshCollider>();

            if (meshCollider != null && meshCollider.sharedMesh == generatedMesh)
            {
                meshCollider.sharedMesh = null;
            }

            if (meshFilter != null && meshFilter.sharedMesh == generatedMesh)
            {
                meshFilter.sharedMesh = null;
            }

            if (meshRenderer != null && meshRenderer.sharedMaterial == generatedMaterial)
            {
                meshRenderer.sharedMaterial = null;
            }

            DestroyTransient(generatedMesh);
            DestroyTransient(generatedMaterial);
            generatedMesh = null;
            generatedMaterial = null;
        }

        private static void DestroyTransient(Object asset)
        {
            if (asset == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(asset);
            }
            else
            {
                DestroyImmediate(asset);
            }
        }
    }
}
