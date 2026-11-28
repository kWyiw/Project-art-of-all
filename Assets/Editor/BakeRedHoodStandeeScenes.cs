using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProjectArtOfAll.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectArtOfAll.Editor
{
    /// <summary>
    /// Bakes the standee prefab into Map1 and Map2 and aligns its lowest point
    /// with the top surface of the player's current tile.
    /// </summary>
    public static class BakeRedHoodStandeeScenes
    {
        private const int MainCharacterId = 0;
        private const string PrefabPath =
            "Assets/Resources/Standees/RedHood/PF_RedHoodStandee.prefab";
        private const string VisualObjectName = "RedHoodStandee";
        private const string Map1Path = "Assets/Scenes/Map1.unity";
        private const string Map2Path = "Assets/Scenes/Map2.unity";
        private const string ResultPath =
            "_tmp/redhood-standee-bake.result.txt";

        [MenuItem("Tools/Project Art of All/Bake Red Hood Standee Into Maps")]
        public static void BakeFromMenu()
        {
            RunBakeAndWriteResult();
        }

        public static void BakeFromCommandLine()
        {
            RunBakeAndWriteResult();
        }

        private static void RunBakeAndWriteResult()
        {
            List<SceneBakeResult> results = BakeScenes();
            StringBuilder output = new StringBuilder();
            output.AppendLine("PASS");
            for (int index = 0; index < results.Count; index++)
            {
                SceneBakeResult result = results[index];
                output.AppendLine(
                    result.ScenePath +
                    ": token=" + result.TokenName +
                    " tile=R" + (result.Row + 1).ToString("D2") +
                    " C" + (result.Column + 1).ToString("D2") +
                    " tile_top=" + result.TileTopY.ToString("F4") +
                    " standee_bottom=" + result.StandeeBottomY.ToString("F4") +
                    " block_renderer=off");
            }

            WriteResult(output.ToString());
            Debug.Log("[Standee] Baked red-hood standee into Map1 and Map2.");
        }

        private static List<SceneBakeResult> BakeScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before baking the standee scene instances.");
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                throw new InvalidOperationException(
                    "Close Prefab Mode before baking the standee scene instances.");
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new InvalidOperationException(
                    "Save or close all modified scenes before baking the standee.");
            }

            Scene previousScene = SceneManager.GetActiveScene();
            List<SceneBakeResult> results = new List<SceneBakeResult>();
            try
            {
                results.Add(BakeScene(Map1Path));
                results.Add(BakeScene(Map2Path));
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

        private static SceneBakeResult BakeScene(string scenePath)
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
                    "Save or close the modified scene before baking it: " + scenePath);
            }

            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Standee prefab is missing: " + PrefabPath);
                }

                BoardPlayerToken token = FindMainCharacterToken(scene);
                BoardTileView tile = FindTile(
                    scene,
                    token.CurrentRow,
                    token.CurrentColumn);
                Renderer tileRenderer = tile.GetComponent<Renderer>();
                Renderer blockRenderer = token.GetComponent<Renderer>();
                if (tileRenderer == null || blockRenderer == null)
                {
                    throw new InvalidOperationException(
                        "Map player token or tile renderer is missing.");
                }

                RemoveExistingStandee(token.transform);
                blockRenderer.enabled = false;

                GameObject anchor = new GameObject(VisualObjectName);
                anchor.transform.SetParent(token.transform, false);
                Vector3 parentScale = token.transform.lossyScale;
                anchor.transform.localScale = new Vector3(
                    SafeInverse(parentScale.x),
                    SafeInverse(parentScale.y),
                    SafeInverse(parentScale.z));

                GameObject standee =
                    PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (standee == null)
                {
                    throw new InvalidOperationException(
                        "Unity could not instantiate the standee prefab.");
                }

                standee.transform.SetParent(anchor.transform, false);
                standee.name = "RedHoodStandee_Model";
                ConfigureStandee(
                    standee,
                    anchor.transform,
                    FindSceneCamera(scene),
                    tileRenderer.bounds.max.y);

                Bounds finalBounds = CalculateBounds(standee);
                float bottomDelta = Mathf.Abs(
                    finalBounds.min.y - tileRenderer.bounds.max.y);
                if (bottomDelta > 0.002f)
                {
                    throw new InvalidOperationException(
                        "Standee bottom is " + bottomDelta.ToString("F4") +
                        " m away from the tile top.");
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(standee);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        "Could not save baked standee scene: " + scenePath);
                }

                return new SceneBakeResult(
                    scenePath,
                    token.name,
                    token.CurrentRow,
                    token.CurrentColumn,
                    tileRenderer.bounds.max.y,
                    finalBounds.min.y);
            }
            finally
            {
                if (openedForEdit && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ConfigureStandee(
            GameObject standee,
            Transform anchor,
            Camera camera,
            float tileTopY)
        {
            Bounds authoredBounds = CalculateBounds(standee);
            if (authoredBounds.size.y <= 0.0001f)
            {
                throw new InvalidOperationException(
                    "Standee prefab has invalid render bounds.");
            }

            Vector3 authoredScale = standee.transform.localScale;
            float uniformScale =
                BoardStandeeVisual.GameplayHeight / authoredBounds.size.y;
            standee.transform.localScale = authoredScale * uniformScale;

            if (camera != null)
            {
                Vector3 direction = camera.transform.position - anchor.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    anchor.rotation = Quaternion.LookRotation(direction.normalized);
                }
            }

            Bounds bounds = CalculateBounds(standee);
            standee.transform.position +=
                Vector3.up * (tileTopY - bounds.min.y);
        }

        private static BoardPlayerToken FindMainCharacterToken(Scene scene)
        {
            BoardPlayerToken[] tokens =
                Object.FindObjectsOfType<BoardPlayerToken>(true);
            BoardPlayerToken match = null;
            for (int index = 0; index < tokens.Length; index++)
            {
                BoardPlayerToken token = tokens[index];
                if (token.gameObject.scene != scene ||
                    token.PlayerId != MainCharacterId)
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        "Found more than one player 1 token in " + scene.path);
                }

                match = token;
            }

            if (match == null)
            {
                throw new InvalidOperationException(
                    "Map has no player 1 token: " + scene.path);
            }

            return match;
        }

        private static BoardTileView FindTile(
            Scene scene,
            int row,
            int column)
        {
            BoardTileView[] tiles =
                Object.FindObjectsOfType<BoardTileView>(true);
            for (int index = 0; index < tiles.Length; index++)
            {
                BoardTileView tile = tiles[index];
                if (tile.gameObject.scene == scene &&
                    tile.Row == row &&
                    tile.Column == column)
                {
                    return tile;
                }
            }

            throw new InvalidOperationException(
                "Map has no tile at R" + (row + 1) + " C" + (column + 1) +
                ": " + scene.path);
        }

        private static Camera FindSceneCamera(Scene scene)
        {
            Camera[] cameras = Object.FindObjectsOfType<Camera>(true);
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera camera = cameras[index];
                if (camera.gameObject.scene == scene &&
                    camera.CompareTag("MainCamera"))
                {
                    return camera;
                }
            }

            return null;
        }

        private static void RemoveExistingStandee(Transform token)
        {
            for (int index = token.childCount - 1; index >= 0; index--)
            {
                Transform child = token.GetChild(index);
                if (child.name == VisualObjectName)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return default;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static float SafeInverse(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
        }

        private static void WriteResult(string content)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(ResultPath) ?? "_tmp");
            File.WriteAllText(ResultPath, content);
        }

        private sealed class SceneBakeResult
        {
            public SceneBakeResult(
                string scenePath,
                string tokenName,
                int row,
                int column,
                float tileTopY,
                float standeeBottomY)
            {
                ScenePath = scenePath;
                TokenName = tokenName;
                Row = row;
                Column = column;
                TileTopY = tileTopY;
                StandeeBottomY = standeeBottomY;
            }

            public string ScenePath { get; }
            public string TokenName { get; }
            public int Row { get; }
            public int Column { get; }
            public float TileTopY { get; }
            public float StandeeBottomY { get; }
        }
    }
}
