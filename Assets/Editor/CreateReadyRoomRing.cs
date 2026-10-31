using System.IO;
using ProjectArtOfAll.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Editor
{
    internal static class CreateReadyRoomRing
    {
        private const string ReadyRoomPath = "Assets/Scenes/ready room.unity";
        private const string RingName = "WhiteRing";
        private const string MaterialPath = "Assets/Materials/WhiteRing.mat";

        [MenuItem("Tools/Project Art of All/Create or Rebuild White Ring")]
        private static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ReadyRoomPath)
            {
                EditorSceneManager.OpenScene(ReadyRoomPath, OpenSceneMode.Single);
            }

            GameObject existing = GameObject.Find(RingName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            CreateIfNeeded();
        }

        private static void CreateIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ReadyRoomPath || GameObject.Find(RingName) != null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[WhiteRing] No Main Camera found in ready room.");
                return;
            }

            Material material = LoadOrCreateMaterial();
            if (material == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create centered white ring");

            GameObject ring = new GameObject(
                RingName,
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(ScreenCenteredRing));

            Undo.RegisterCreatedObjectUndo(ring, "Create centered white ring");

            ring.transform.SetPositionAndRotation(
                new Vector3(0f, 0.01f, 0f),
                Quaternion.Euler(90f, 0f, 0f));

            MeshRenderer renderer = ring.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ring.GetComponent<ScreenCenteredRing>().RebuildMesh();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = ring;
            Debug.Log("[WhiteRing] Created centered white ring in ready room.");
        }

        private static Material LoadOrCreateMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            Directory.CreateDirectory("Assets/Materials");
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError("[WhiteRing] Unlit/Color shader was not found.");
                return null;
            }

            material = new Material(shader)
            {
                name = "WhiteRing",
                color = Color.white
            };
            material.SetColor("_Color", Color.white);

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
