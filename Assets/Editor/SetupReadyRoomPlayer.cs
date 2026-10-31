using ProjectArtOfAll.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Editor
{
    internal static class SetupReadyRoomPlayer
    {
        private const string ReadyRoomPath = "Assets/Scenes/ready room.unity";
        private const float EyeHeight = 1.7f;

        [MenuItem("Tools/Project Art of All/Setup Ready Room Player")]
        private static void SetupFromMenu()
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

            SetupIfNeeded();
        }

        private static void SetupIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ReadyRoomPath ||
                Object.FindObjectOfType<ReadyRoomPlayerController>() != null)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[ReadyRoom] Main Camera is missing.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Ready Room player");

            GameObject player = new GameObject(
                "Player",
                typeof(CharacterController),
                typeof(ReadyRoomPlayerController));

            Undo.RegisterCreatedObjectUndo(player, "Create player");
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            CharacterController controller = player.GetComponent<CharacterController>();
            controller.height = EyeHeight;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, EyeHeight * 0.5f, 0f);
            controller.skinWidth = 0.04f;
            controller.stepOffset = 0.3f;

            GameObject head = new GameObject("Head");
            Undo.RegisterCreatedObjectUndo(head, "Create player head");
            head.transform.SetParent(player.transform, false);
            head.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            head.transform.localRotation = Quaternion.identity;

            Undo.SetTransformParent(mainCamera.transform, head.transform, "Parent camera to player head");
            mainCamera.transform.localPosition = Vector3.zero;
            mainCamera.transform.localRotation = Quaternion.identity;
            mainCamera.nearClipPlane = 0.05f;

            ReadyRoomPlayerController playerController =
                player.GetComponent<ReadyRoomPlayerController>();
            SerializedObject serializedController = new SerializedObject(playerController);
            serializedController.FindProperty("head").objectReferenceValue = head.transform;
            serializedController.FindProperty("playerCamera").objectReferenceValue = mainCamera;
            serializedController.FindProperty("moveSpeed").floatValue = 3f;
            serializedController.FindProperty("eyeHeight").floatValue = EyeHeight;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = player;
            Debug.Log("[ReadyRoom] Player, head camera and ring movement are configured.");
        }
    }
}
