using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectArtOfAll.Editor
{
    /// <summary>
    /// Applies the close-up zoom floor shown by the standee detail preview.
    /// </summary>
    public static class SetMapCameraMinimum
    {
        private const string Map1Path = "Assets/Scenes/Map1.unity";
        private const string Map2Path = "Assets/Scenes/Map2.unity";
        private const string ResultPath = "_tmp/map-minimum-camera.result.txt";

        [MenuItem("Tools/Project Art of All/Set Map Minimum Camera Zoom")]
        public static void ApplyFromMenu()
        {
            RunAndWriteResult();
        }

        public static void ApplyFromCommandLine()
        {
            RunAndWriteResult();
        }

        private static void RunAndWriteResult()
        {
            List<SceneResult> results = ApplyToScenes();
            StringBuilder output = new StringBuilder();
            output.AppendLine("PASS");
            for (int index = 0; index < results.Count; index++)
            {
                output.AppendLine(
                    results[index].ScenePath +
                    ": orthographic=True minimumZoom=" +
                    results[index].MinimumZoom.ToString("F2") +
                    " maximumZoom=" +
                    results[index].MaximumZoom.ToString("F2"));
            }

            WriteResult(output.ToString());
            Debug.Log("[MapCamera] Switched map cameras to orthographic.");
        }

        private static List<SceneResult> ApplyToScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before changing the map camera minimum.");
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new InvalidOperationException(
                    "Save or close all modified scenes before changing the camera.");
            }

            Scene previousScene = SceneManager.GetActiveScene();
            List<SceneResult> results = new List<SceneResult>();
            try
            {
                results.Add(ApplyToScene(Map1Path));
                results.Add(ApplyToScene(Map2Path));
            }
            finally
            {
                if (previousScene.IsValid() &&
                    previousScene.isLoaded &&
                    SceneManager.GetActiveScene() != previousScene)
                {
                    SceneManager.SetActiveScene(previousScene);
                }
            }

            return results;
        }

        private static SceneResult ApplyToScene(string scenePath)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForEdit = !scene.IsValid() || !scene.isLoaded;
            if (openedForEdit)
            {
                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
            }
            else if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    "Save or close the modified scene before changing its camera: " +
                    scenePath);
            }

            try
            {
                BoardCameraController controller = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    BoardCameraController[] candidates =
                        roots[rootIndex].GetComponentsInChildren<BoardCameraController>(
                            true);
                    if (candidates.Length > 0)
                    {
                        controller = candidates[0];
                        break;
                    }
                }

                if (controller == null)
                {
                    throw new InvalidOperationException(
                        "Map camera controller is missing: " + scenePath);
                }

                Camera boardCamera = controller.GetComponent<Camera>();
                if (boardCamera == null)
                {
                    throw new InvalidOperationException(
                        "Map camera is missing: " + scenePath);
                }

                SerializedObject serialized = new SerializedObject(controller);
                SerializedProperty minimumZoom =
                    serialized.FindProperty("minimumZoom");
                SerializedProperty maximumZoom =
                    serialized.FindProperty("maximumZoom");
                SerializedProperty minimumCameraHeight =
                    serialized.FindProperty("minimumCameraHeight");
                SerializedProperty minimumCameraPitch =
                    serialized.FindProperty("minimumCameraPitch");
                SerializedProperty minimumFocusHeight =
                    serialized.FindProperty("minimumFocusHeight");
                if (minimumZoom == null ||
                    maximumZoom == null ||
                    minimumCameraHeight == null ||
                    minimumCameraPitch == null ||
                    minimumFocusHeight == null)
                {
                    throw new InvalidOperationException(
                        "BoardCameraController orthographic settings are not serializable.");
                }

                boardCamera.orthographic = true;
                minimumZoom.floatValue = BoardCameraController.MinimumZoom;
                maximumZoom.floatValue =
                    30f *
                    BoardMapBuilder.TileScaleMultiplier *
                    BoardCameraController.MaximumViewHeightMultiplier;
                minimumCameraHeight.floatValue =
                    BoardCameraController.CloseUpCameraHeight;
                minimumCameraPitch.floatValue =
                    BoardCameraController.CloseUpCameraPitch;
                minimumFocusHeight.floatValue =
                    BoardCameraController.CloseUpFocusHeight;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        "Could not save map camera settings: " + scenePath);
                }

                return new SceneResult(
                    scenePath,
                    minimumZoom.floatValue,
                    maximumZoom.floatValue);
            }
            finally
            {
                if (openedForEdit && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void WriteResult(string content)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(ResultPath) ?? "_tmp");
            File.WriteAllText(ResultPath, content);
        }

        private sealed class SceneResult
        {
            public SceneResult(
                string scenePath,
                float minimumZoom,
                float maximumZoom)
            {
                ScenePath = scenePath;
                MinimumZoom = minimumZoom;
                MaximumZoom = maximumZoom;
            }

            public string ScenePath { get; }
            public float MinimumZoom { get; }
            public float MaximumZoom { get; }
        }
    }
}
