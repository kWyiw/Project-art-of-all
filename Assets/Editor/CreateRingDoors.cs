using ProjectArtOfAll.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Editor
{
    internal static class CreateRingDoors
    {
        private const string ReadyRoomPath = "Assets/Scenes/ready room.unity";
        private const string DoorRootName = "RingDoors";
        private const string WhiteMaterialPath = "Assets/Materials/WhiteRing.mat";
        private const float FrameWidth = 3f;
        private const float FrameHeight = 2f;
        private const float BarThickness = 0.1f;
        private const float BarDepth = 0.14f;

        [MenuItem("Tools/Project Art of All/Create or Rebuild Ring Doors")]
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

            GameObject existing = GameObject.Find(DoorRootName);
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
            if (scene.path != ReadyRoomPath || GameObject.Find(DoorRootName) != null)
            {
                return;
            }

            ScreenCenteredRing ring = Object.FindObjectOfType<ScreenCenteredRing>();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(WhiteMaterialPath);
            if (ring == null || material == null)
            {
                Debug.LogError("[RingDoors] WhiteRing or its material is missing.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create ring doors");

            GameObject root = new GameObject(DoorRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create ring doors");

            for (int index = 0; index < 4; index++)
            {
                float angle = index * 90f;
                CreateDoor(root.transform, ring, material, angle);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = root;
            Debug.Log("[RingDoors] Created four ring-bound doors.");
        }

        private static void CreateDoor(
            Transform parent,
            ScreenCenteredRing ring,
            Material material,
            float angle)
        {
            GameObject door = new GameObject(
                $"Door_{angle:000}",
                typeof(RingDoorPortal));
            Undo.RegisterCreatedObjectUndo(door, "Create ring door");
            door.transform.SetParent(parent, false);

            RingDoorPortal portal = door.GetComponent<RingDoorPortal>();
            SerializedObject serializedPortal = new SerializedObject(portal);
            serializedPortal.FindProperty("ring").objectReferenceValue = ring;
            serializedPortal.FindProperty("angleDegrees").floatValue = angle;
            serializedPortal.FindProperty("frameWidth").floatValue = FrameWidth;
            serializedPortal.FindProperty("frameHeight").floatValue = FrameHeight;
            serializedPortal.FindProperty("targetScene").stringValue = "MainGame";
            serializedPortal.ApplyModifiedPropertiesWithoutUndo();

            float postOffset = (FrameWidth - BarThickness) * 0.5f;
            CreateBar(
                door.transform,
                "Post_Left",
                new Vector3(-postOffset, FrameHeight * 0.5f, 0f),
                new Vector3(BarThickness, FrameHeight, BarDepth),
                material);
            CreateBar(
                door.transform,
                "Post_Right",
                new Vector3(postOffset, FrameHeight * 0.5f, 0f),
                new Vector3(BarThickness, FrameHeight, BarDepth),
                material);
            CreateBar(
                door.transform,
                "Lintel",
                new Vector3(0f, FrameHeight - BarThickness * 0.5f, 0f),
                new Vector3(FrameWidth, BarThickness, BarDepth),
                material);

            portal.ApplyRingBinding();
        }

        private static void CreateBar(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localRotation = Quaternion.identity;
            bar.transform.localScale = localScale;
            bar.GetComponent<MeshRenderer>().sharedMaterial = material;

            Undo.RegisterCreatedObjectUndo(bar, "Create door frame");
        }
    }
}
