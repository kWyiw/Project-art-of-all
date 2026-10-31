using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Editor
{
    internal static class CreateReadyRoomScene
    {
        private const string ScenePath = "Assets/Scenes/ready room.unity";

        [MenuItem("Tools/Project Art of All/Create or Open Ready Room Scene")]
        private static void CreateOrOpenFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (existing != null)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                AddToBuildSettings();
                return;
            }

            CreateIfMissing();
        }

        private static void CreateIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            SceneAsset existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (existing != null)
            {
                AddToBuildSettings();
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[Scene] Failed to save {ScenePath}.");
                return;
            }

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.in2DMode = false;
                SceneView.RepaintAll();
            }

            AddToBuildSettings();
            Debug.Log($"[Scene] Created 3D scene {ScenePath}.");
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes =
                EditorBuildSettings.scenes.ToList();

            if (scenes.Any(scene => scene.path == ScenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
