using System;
using System.Collections.Generic;
using System.IO;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Flow;
using ProjectArtOfAll.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectArtOfAll.Editor
{
    /// <summary>
    /// Rebuilds the dimension split from the current gameplay scene:
    /// Map1 keeps the baked classic board, Map2 receives a baked sweet-ring board,
    /// and MainGame becomes the transit scene that loads the selected map.
    /// </summary>
    public static class MapDimensionSceneBuilder
    {
        private const string MainGamePath = "Assets/Scenes/MainGame.unity";
        private const string Map1Path = "Assets/Scenes/Map1.unity";
        private const string Map2Path = "Assets/Scenes/Map2.unity";
        private const string GroundCleanupResultPath =
            "_tmp/map-ground-cleanup.result.txt";
        private const string TileScaleResultPath =
            "_tmp/map-tile-scale.result.txt";
        private const string CameraFrameResultPath =
            "_tmp/map-camera-frame.result.txt";
        private const string Map1PreviewPath = "_tmp/map1-no-white-ground.png";
        private const string Map2PreviewPath = "_tmp/map2-no-white-ground.png";

        [MenuItem("Tools/Project Art of All/Rebuild Map Dimension Scenes")]
        public static void RebuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            PrepareMap1();
            Scene map2 = PrepareMap2();
            if (!map2.IsValid())
            {
                throw new System.InvalidOperationException("Map dimension scenes could not be created.");
            }

            BuildTransitScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MapDimension] Map1, Map2 and MainGame rebuilt.");
        }

        [MenuItem("Tools/Project Art of All/Validate Map Dimension Scenes")]
        public static void ValidateAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            ValidateMapScene(Map1Path, MapCatalog.ClassicId, 68);
            ValidateMapScene(Map2Path, MapCatalog.SweetRingId, 36);

            Scene transit = EditorSceneManager.OpenScene(MainGamePath, OpenSceneMode.Single);
            if (!transit.IsValid() ||
                Object.FindObjectOfType<MapTransitController>() == null ||
                Object.FindObjectOfType<BoardGameplayController>() != null ||
                Object.FindObjectOfType<Canvas>() == null ||
                Object.FindObjectsOfType<Text>(true).Length < 4 ||
                Object.FindObjectsOfType<Image>(true).Length < 4)
            {
                throw new System.InvalidOperationException(
                    "MainGame must contain the serialized transit UI and no board.");
            }

            ValidateBuildSettings();
            Debug.Log("[MapDimension] Validation passed.");
        }

        [MenuItem("Tools/Project Art of All/Remove White Map Ground")]
        public static void RemoveWhiteMapGround()
        {
            RemoveWhiteMapGroundInternal(throwOnFailure: true);
        }

        [MenuItem("Tools/Project Art of All/Apply Map Tile Scale")]
        public static void ApplyMapTileScale()
        {
            ApplyMapTileScaleInternal();
        }

        [MenuItem("Tools/Project Art of All/Reframe Map Cameras")]
        public static void ReframeMapCameras()
        {
            ReframeMapCamerasInternal();
        }

        private static void ReframeMapCamerasInternal()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before reframing the map cameras.");
            }

            Scene previousActive = SceneManager.GetActiveScene();
            List<SceneCameraFrame> scenes = new List<SceneCameraFrame>();
            try
            {
                scenes.Add(FrameMapCamera(Map1Path));
                scenes.Add(FrameMapCamera(Map2Path));

                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                string result = string.Format(
                    "PASS\r\n{0}: orthographic {1:F2}, height {2:F2}\r\n{3}: orthographic {4:F2}, height {5:F2}",
                    Map1Path,
                    scenes[0].OrthographicSize,
                    scenes[0].CameraHeight,
                    Map2Path,
                    scenes[1].OrthographicSize,
                    scenes[1].CameraHeight);
                File.WriteAllText(CameraFrameResultPath, result);
                Debug.Log("[MapArt] " + result.Replace(System.Environment.NewLine, " | "));
            }
            catch
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                throw;
            }
            finally
            {
                for (int index = 0; index < scenes.Count; index++)
                {
                    if (scenes[index].OpenedForEdit &&
                        scenes[index].Scene.IsValid() &&
                        scenes[index].Scene.isLoaded)
                    {
                        EditorSceneManager.CloseScene(scenes[index].Scene, true);
                    }
                }
            }
        }

        private static SceneCameraFrame FrameMapCamera(string scenePath)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForEdit = !scene.IsValid() || !scene.isLoaded;
            if (openedForEdit)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            else if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Save or close the modified scene before reframing it: ",
                        scenePath));
            }

            Camera camera = null;
            BoardGameplayController controller = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (camera == null)
                {
                    Camera candidate = roots[rootIndex].GetComponentInChildren<Camera>(true);
                    if (candidate != null && candidate.CompareTag("MainCamera"))
                    {
                        camera = candidate;
                    }
                }

                if (controller == null)
                {
                    controller = roots[rootIndex]
                        .GetComponentInChildren<BoardGameplayController>(true);
                }
            }

            if (camera == null || controller == null)
            {
                throw new InvalidOperationException(
                    string.Concat("Map camera reframing is missing data: ", scenePath));
            }

            BoardTileView[] tiles = controller.GetComponentsInChildren<BoardTileView>(true);
            if (tiles.Length == 0)
            {
                throw new InvalidOperationException(
                    string.Concat("Map camera reframing found no tiles: ", scenePath));
            }

            bool hasBounds = false;
            Bounds bounds = default;
            for (int index = 0; index < tiles.Length; index++)
            {
                Renderer renderer = tiles[index].GetComponent<Renderer>();
                Bounds tileBounds = renderer != null
                    ? renderer.bounds
                    : new Bounds(tiles[index].transform.position, Vector3.one);
                if (!hasBounds)
                {
                    bounds = tileBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(tileBounds);
                }
            }

            const float aspect = 16f / 9f;
            float padding =
                BoardCameraController.BaseFullMapPadding *
                BoardCameraController.MaximumViewHeightMultiplier;
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;
            Vector3 center = bounds.center;
            float maximumRight = 0f;
            float maximumUp = 0f;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 offset = corner - center;
                        maximumRight = Mathf.Max(
                            maximumRight,
                            Mathf.Abs(Vector3.Dot(offset, right)));
                        maximumUp = Mathf.Max(
                            maximumUp,
                            Mathf.Abs(Vector3.Dot(offset, up)));
                    }
                }
            }

            float fittedHalfHeight = Mathf.Max(maximumUp, maximumRight / aspect);
            float targetSize = Mathf.Max(
                BoardCameraController.MinimumZoom,
                fittedHalfHeight * padding);
            float previousSize = Mathf.Max(0.001f, camera.orthographicSize);

            float forwardY = camera.transform.forward.y;
            if (forwardY < -0.0001f)
            {
                float distance = (center.y - camera.transform.position.y) / forwardY;
                Vector3 viewCenter =
                    camera.transform.position + camera.transform.forward * distance;
                camera.transform.position += center - viewCenter;
            }

            float physicalScale = targetSize / previousSize;
            Vector3 offsetFromCenter = camera.transform.position - center;
            camera.transform.position = center + offsetFromCenter * physicalScale;
            camera.orthographic = true;
            camera.orthographicSize = targetSize;

            BoardCameraController cameraController =
                camera.GetComponent<BoardCameraController>();
            if (cameraController != null)
            {
                SerializedObject serialized = new SerializedObject(cameraController);
                serialized.FindProperty("minimumZoom").floatValue =
                    BoardCameraController.MinimumZoom;
                serialized.FindProperty("maximumZoom").floatValue =
                    30f *
                    BoardMapBuilder.TileScaleMultiplier *
                    BoardCameraController.MaximumViewHeightMultiplier;
                serialized.FindProperty("fullMapPadding").floatValue = padding;
                serialized.FindProperty("panLimit").floatValue =
                    70f *
                    BoardMapBuilder.TileScaleMultiplier *
                    BoardCameraController.MaximumViewHeightMultiplier;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    string.Concat("Could not save reframed scene ", scenePath, "."));
            }

            Debug.Log(string.Format(
                "[MapArt] Reframed {0}: orthographic {1:F2}, camera height {2:F2}.",
                scenePath,
                targetSize,
                camera.transform.position.y));
            return new SceneCameraFrame(
                scene,
                openedForEdit,
                targetSize,
                camera.transform.position.y);
        }

        private static void ApplyMapTileScaleInternal()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before scaling the map tiles.");
            }

            Scene previousActive = SceneManager.GetActiveScene();
            List<SceneTileScaleResult> scenes = new List<SceneTileScaleResult>();
            try
            {
                scenes.Add(ScaleMapTiles(Map1Path));
                scenes.Add(ScaleMapTiles(Map2Path));

                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                string result = string.Format(
                    "PASS\r\n{0}: tiles {1}, scale {2}x\r\n{3}: tiles {4}, scale {5}x",
                    Map1Path,
                    scenes[0].TileCount,
                    scenes[0].AppliedScale,
                    Map2Path,
                    scenes[1].TileCount,
                    scenes[1].AppliedScale);
                File.WriteAllText(TileScaleResultPath, result);
                Debug.Log("[MapArt] " + result.Replace(System.Environment.NewLine, " | "));
            }
            catch
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                throw;
            }
            finally
            {
                for (int index = 0; index < scenes.Count; index++)
                {
                    if (scenes[index].OpenedForEdit &&
                        scenes[index].Scene.IsValid() &&
                        scenes[index].Scene.isLoaded)
                    {
                        EditorSceneManager.CloseScene(scenes[index].Scene, true);
                    }
                }
            }
        }

        private static SceneTileScaleResult ScaleMapTiles(string scenePath)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForEdit = !scene.IsValid() || !scene.isLoaded;
            if (openedForEdit)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            else if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Save or close the modified scene before scaling it: ",
                        scenePath));
            }

            BoardGameplayController controller = null;
            HashSet<BoardTileView> tiles = new HashSet<BoardTileView>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (controller == null)
                {
                    controller = roots[rootIndex]
                        .GetComponentInChildren<BoardGameplayController>(true);
                }

                BoardTileView[] rootTiles =
                    roots[rootIndex].GetComponentsInChildren<BoardTileView>(true);
                for (int index = 0; index < rootTiles.Length; index++)
                {
                    tiles.Add(rootTiles[index]);
                }
            }

            if (controller == null || tiles.Count == 0)
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Map tile scaling is missing a board controller or tiles: ",
                        scenePath));
            }

            Vector3 targetScale = new Vector3(
                BoardMapBuilder.TileFootprint,
                BoardMapBuilder.TileHeight,
                BoardMapBuilder.TileFootprint);
            foreach (BoardTileView tile in tiles)
            {
                Transform tileTransform = tile.transform;
                Vector3 currentScale = tileTransform.localScale;
                float baseFootprint =
                    BoardMapBuilder.TileFootprint /
                    BoardMapBuilder.TileScaleMultiplier;
                float currentFactor = currentScale.x / baseFootprint;
                if (currentFactor <= 0.0001f)
                {
                    throw new InvalidOperationException(
                        string.Concat("Tile has an invalid scale: ", tile.name));
                }

                float spacingScale =
                    BoardMapBuilder.TileScaleMultiplier / currentFactor;
                Vector3 position = tileTransform.localPosition;
                float groundY = position.y - currentScale.y * 0.5f;
                position.x *= spacingScale;
                position.z *= spacingScale;
                position.y = groundY + BoardMapBuilder.TileHeight * 0.5f;
                tileTransform.localPosition = position;
                tileTransform.localScale = targetScale;
            }

            SnapTokensToHomes(controller);
            ValidateSpawnPoints(controller);
            ValidateTileScale(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    string.Concat("Could not save scaled scene ", scenePath, "."));
            }

            Debug.Log(string.Format(
                "[MapArt] Scaled {0} tiles to {1:F2}x in {2}.",
                tiles.Count,
                BoardMapBuilder.TileScaleMultiplier,
                scenePath));
            return new SceneTileScaleResult(
                scene,
                openedForEdit,
                tiles.Count,
                BoardMapBuilder.TileScaleMultiplier);
        }

        [MenuItem("Tools/Project Art of All/Preview White Map Ground Removal")]
        public static void PreviewWhiteMapGroundRemoval()
        {
            RenderWhiteMapGroundPreview();
        }

        private static void RenderWhiteMapGroundPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Exit Play Mode before rendering the map preview.");
            }

            Scene previousActive = SceneManager.GetActiveScene();
            if (!previousActive.IsValid() ||
                !previousActive.isLoaded ||
                previousActive.isDirty ||
                string.IsNullOrEmpty(previousActive.path))
            {
                throw new InvalidOperationException(
                    "Save the current scene before rendering map previews.");
            }

            string previousScenePath = previousActive.path;
            try
            {
                RenderMapPreview(Map1Path, Map1PreviewPath);
                RenderMapPreview(Map2Path, Map2PreviewPath);
            }
            finally
            {
                Scene restored = EditorSceneManager.OpenScene(
                    previousScenePath,
                    OpenSceneMode.Single);
                SceneManager.SetActiveScene(restored);
            }

            Debug.Log(string.Format(
                "[MapArt] Wrote white-ground removal previews: {0}, {1}.",
                Map1PreviewPath,
                Map2PreviewPath));
        }

        private static void RenderMapPreview(string scenePath, string outputPath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Camera camera = null;
            BoardGameplayController controller = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (camera == null)
                {
                    Camera candidate = roots[rootIndex].GetComponentInChildren<Camera>(true);
                    if (candidate != null && candidate.CompareTag("MainCamera"))
                    {
                        camera = candidate;
                    }
                }

                if (controller == null)
                {
                    controller = roots[rootIndex]
                        .GetComponentInChildren<BoardGameplayController>(true);
                }
            }

            if (camera == null || controller == null)
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Map preview is missing a camera or gameplay controller: ",
                        scenePath));
            }

            Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
            for (int index = 0; index < canvases.Length; index++)
            {
                if (canvases[index].gameObject.scene == scene)
                {
                    canvases[index].gameObject.SetActive(false);
                }
            }

            const int width = 1600;
            const int height = 900;
            RenderTexture target = new RenderTexture(width, height, 24);
            Texture2D texture = new Texture2D(
                width,
                height,
                TextureFormat.RGB24,
                false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false, false);
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(texture);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static void RemoveWhiteMapGroundInternal(bool throwOnFailure)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (throwOnFailure)
                {
                    throw new InvalidOperationException(
                        "Exit Play Mode before removing the map ground.");
                }

                return;
            }

            Scene previousActive = SceneManager.GetActiveScene();
            List<SceneCleanup> scenes = new List<SceneCleanup>();
            try
            {
                scenes.Add(CleanupMapGround(Map1Path));
                scenes.Add(CleanupMapGround(Map2Path));

                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                string result = string.Format(
                    "PASS\r\n{0}: removed {1}, tiles {2}\r\n{3}: removed {4}, tiles {5}",
                    Map1Path,
                    scenes[0].RemovedObjects,
                    scenes[0].TileCount,
                    Map2Path,
                    scenes[1].RemovedObjects,
                    scenes[1].TileCount);
                File.WriteAllText(GroundCleanupResultPath, result);
                Debug.Log("[MapArt] " + result.Replace(System.Environment.NewLine, " | "));
            }
            catch
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActive);
                }

                throw;
            }
            finally
            {
                for (int index = 0; index < scenes.Count; index++)
                {
                    if (scenes[index].OpenedForCleanup &&
                        scenes[index].Scene.IsValid() &&
                        scenes[index].Scene.isLoaded)
                    {
                        EditorSceneManager.CloseScene(scenes[index].Scene, true);
                    }
                }
            }
        }

        private static SceneCleanup CleanupMapGround(string scenePath)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForCleanup = !scene.IsValid() || !scene.isLoaded;
            if (openedForCleanup)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            else if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Save or close the modified scene before cleaning it: ",
                        scenePath));
            }

            List<GameObject> groundObjects = new List<GameObject>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms =
                    roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    string objectName = transforms[index].gameObject.name;
                    if (objectName == "BoardBase" ||
                        objectName.StartsWith("GridV_", StringComparison.Ordinal) ||
                        objectName.StartsWith("GridH_", StringComparison.Ordinal))
                    {
                        groundObjects.Add(transforms[index].gameObject);
                    }
                }
            }

            int tileCount = 0;
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                tileCount += roots[rootIndex]
                    .GetComponentsInChildren<BoardTileView>(true)
                    .Length;
            }

            if (groundObjects.Count == 0)
            {
                if (openedForCleanup)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                return new SceneCleanup(scene, false, tileCount, 0);
            }

            for (int index = 0; index < groundObjects.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(groundObjects[index]);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    string.Concat("Could not save cleaned scene ", scenePath, "."));
            }

            Debug.Log(string.Format(
                "[MapArt] Removed {0} white-ground objects from {1}.",
                groundObjects.Count,
                scenePath));
            return new SceneCleanup(
                scene,
                openedForCleanup,
                tileCount,
                groundObjects.Count);
        }

        private static Scene PrepareMap1()
        {
            Scene mainGame = EditorSceneManager.OpenScene(MainGamePath, OpenSceneMode.Single);
            BoardGameplayController mainController =
                Object.FindObjectOfType<BoardGameplayController>();

            if (mainController != null)
            {
                if (!EditorSceneManager.SaveScene(mainGame, Map1Path, true))
                {
                    throw new System.InvalidOperationException(
                        string.Concat("Could not save ", Map1Path, "."));
                }
            }
            else if (!System.IO.File.Exists(Map1Path))
            {
                throw new System.InvalidOperationException(
                    "MainGame no longer contains gameplay and Map1 does not exist.");
            }

            Scene map1 = EditorSceneManager.OpenScene(Map1Path, OpenSceneMode.Single);
            BoardGameplayController controller =
                Object.FindObjectOfType<BoardGameplayController>();
            if (controller == null)
            {
                throw new System.InvalidOperationException("Map1 has no BoardGameplayController.");
            }

            EnsureBootstrap(MapCatalog.ClassicId);
            EditorSceneManager.MarkSceneDirty(map1);
            EditorSceneManager.SaveScene(map1);
            return map1;
        }

        private static Scene PrepareMap2()
        {
            Scene map1 = EditorSceneManager.OpenScene(Map1Path, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(map1, Map2Path, true))
            {
                throw new System.InvalidOperationException(
                    string.Concat("Could not save ", Map2Path, "."));
            }

            Scene map2 = EditorSceneManager.OpenScene(Map2Path, OpenSceneMode.Single);
            BoardGameplayController controller =
                Object.FindObjectOfType<BoardGameplayController>();
            if (controller == null)
            {
                throw new System.InvalidOperationException("Map2 has no BoardGameplayController.");
            }

            BakeSweetRing(controller);
            EnsureBootstrap(MapCatalog.SweetRingId);
            EditorSceneManager.MarkSceneDirty(map2);
            EditorSceneManager.SaveScene(map2);
            return map2;
        }

        private static void BakeSweetRing(BoardGameplayController controller)
        {
            Transform boardRoot = controller.transform;

            if (!BoardMapBuilder.Build(controller, BoardMapLibrary.SweetRing))
            {
                throw new System.InvalidOperationException(
                    "Sweet-ring board could not be baked into Map2.");
            }

            Transform generated = boardRoot.Find("GeneratedBoard");
            if (generated != null)
            {
                generated.name = "Board_Map2_SweetRing";
            }

            SnapTokensToHomes(controller);
        }

        private static void SnapTokensToHomes(BoardGameplayController controller)
        {
            BoardTileView[] tiles = controller.GetComponentsInChildren<BoardTileView>(true);
            BoardPlayerToken[] tokens = controller.GetComponentsInChildren<BoardPlayerToken>(true);
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                BoardPlayerToken token = tokens[tokenIndex];
                BoardTileView home = null;
                for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
                {
                    BoardTileView tile = tiles[tileIndex];
                    if (tile.TileType == TileType.Home &&
                        tile.OwnerPlayerId == token.PlayerId)
                    {
                        home = tile;
                        break;
                    }
                }

                if (home == null)
                {
                    throw new System.InvalidOperationException(string.Format(
                        "Map2 has no home tile for player {0}.",
                        token.PlayerId));
                }

                Vector3 position = home.transform.position;
                position.y += home.transform.lossyScale.y * 0.5f;
                position.y += token.transform.lossyScale.y * 0.5f + 0.01f;
                token.transform.position = position;
                token.SetBoardCell(home.Row, home.Column);
            }
        }

        private static void EnsureBootstrap(string mapId)
        {
            MapSceneBootstrap bootstrap = Object.FindObjectOfType<MapSceneBootstrap>();
            if (bootstrap == null)
            {
                GameObject bootstrapObject = new GameObject("Map Scene Bootstrap");
                bootstrap = bootstrapObject.AddComponent<MapSceneBootstrap>();
            }

            SerializedObject serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("mapId").stringValue = mapId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildTransitScene()
        {
            Scene transit = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
            camera.orthographic = true;
            cameraObject.AddComponent<AudioListener>();

            GameObject transitObject = new GameObject("Dimension Transit");
            MapTransitController transitController =
                transitObject.AddComponent<MapTransitController>();
            CanvasGroup overlay;
            Image glow;
            Image upperRule;
            Text mapTitle;
            Text mapSubtitle;
            Image progressFill;
            BuildTransitUi(
                out overlay,
                out glow,
                out upperRule,
                out mapTitle,
                out mapSubtitle,
                out progressFill);

            SerializedObject serialized = new SerializedObject(transitController);
            serialized.FindProperty("overlay").objectReferenceValue = overlay;
            serialized.FindProperty("glow").objectReferenceValue = glow;
            serialized.FindProperty("upperRule").objectReferenceValue = upperRule;
            serialized.FindProperty("mapTitle").objectReferenceValue = mapTitle;
            serialized.FindProperty("mapSubtitle").objectReferenceValue = mapSubtitle;
            serialized.FindProperty("progressFill").objectReferenceValue = progressFill;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(transit, MainGamePath))
            {
                throw new System.InvalidOperationException(
                    string.Concat("Could not save ", MainGamePath, "."));
            }
        }

        private static void BuildTransitUi(
            out CanvasGroup overlay,
            out Image glow,
            out Image upperRule,
            out Text mapTitle,
            out Text mapSubtitle,
            out Image progressFill)
        {
            GameObject canvasObject = new GameObject(
                "DimensionTransitCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            overlay = canvasObject.GetComponent<CanvasGroup>();
            overlay.alpha = 1f;
            overlay.blocksRaycasts = true;

            RectTransform root = (RectTransform)canvasObject.transform;
            Image background = CreateImage(
                root,
                "Background",
                new Color(0.025f, 0.035f, 0.055f, 1f));
            Stretch(background.rectTransform);

            Color classicAccent = new Color(0.96f, 0.73f, 0.34f);
            glow = CreateImage(
                root,
                "Glow",
                new Color(classicAccent.r, classicAccent.g, classicAccent.b, 0.06f));
            Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            glow.rectTransform.sizeDelta = new Vector2(760f, 760f);
            glow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            upperRule = CreateImage(
                root,
                "UpperRule",
                new Color(classicAccent.r, classicAccent.g, classicAccent.b, 0.72f));
            Anchor(
                upperRule.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            upperRule.rectTransform.anchoredPosition = new Vector2(0f, 118f);
            upperRule.rectTransform.sizeDelta = new Vector2(220f, 2f);

            Font font = CreateFont();
            Text kicker = CreateText(
                root,
                "Kicker",
                font,
                19,
                new Color(0.70f, 0.73f, 0.76f, 1f));
            Anchor(kicker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            kicker.rectTransform.anchoredPosition = new Vector2(0f, 84f);
            kicker.rectTransform.sizeDelta = new Vector2(920f, 40f);
            kicker.text = "PROJECT ART OF ALL  /  DIMENSION TRANSFER";

            mapTitle = CreateText(
                root,
                "MapTitle",
                font,
                62,
                new Color(0.96f, 0.94f, 0.87f, 1f));
            Anchor(
                mapTitle.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            mapTitle.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            mapTitle.rectTransform.sizeDelta = new Vector2(1200f, 92f);
            mapTitle.fontStyle = FontStyle.Bold;
            mapTitle.text = MapCatalog.Selected.DisplayName;

            mapSubtitle = CreateText(root, "Subtitle", font, 24, classicAccent);
            Anchor(
                mapSubtitle.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            mapSubtitle.rectTransform.anchoredPosition = new Vector2(0f, -66f);
            mapSubtitle.rectTransform.sizeDelta = new Vector2(1100f, 44f);
            mapSubtitle.text = MapCatalog.Selected.Subtitle;

            Image track = CreateImage(
                root,
                "ProgressTrack",
                new Color(0.16f, 0.18f, 0.21f, 0.9f));
            Anchor(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            track.rectTransform.anchoredPosition = new Vector2(0f, -142f);
            track.rectTransform.sizeDelta = new Vector2(420f, 5f);

            progressFill = CreateImage(track.rectTransform, "ProgressFill", classicAccent);
            Stretch(progressFill.rectTransform);
            progressFill.rectTransform.anchorMax = new Vector2(0.12f, 1f);

            Text footer = CreateText(
                root,
                "Footer",
                font,
                18,
                new Color(0.52f, 0.55f, 0.59f, 1f));
            Anchor(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            footer.rectTransform.anchoredPosition = new Vector2(0f, 52f);
            footer.rectTransform.sizeDelta = new Vector2(900f, 36f);
            footer.text = "正在校准棋盘坐标与玩家维度…";
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            int fontSize,
            Color color)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Font CreateFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Microsoft YaHei UI",
                    "Microsoft YaHei",
                    "PingFang SC",
                    "Noto Sans CJK SC",
                    "Arial"
                },
                22);
            return font != null
                ? font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void UpdateBuildSettings()
        {
            string[] orderedPaths =
            {
                "Assets/Scenes/StartMenu.unity",
                MainGamePath,
                "Assets/Scenes/ready room.unity",
                Map1Path,
                Map2Path
            };

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            for (int index = 0; index < orderedPaths.Length; index++)
            {
                scenes.Add(new EditorBuildSettingsScene(orderedPaths[index], true));
            }

            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            for (int index = 0; index < existing.Length; index++)
            {
                bool duplicate = false;
                for (int orderedIndex = 0; orderedIndex < scenes.Count; orderedIndex++)
                {
                    if (scenes[orderedIndex].path == existing[index].path)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    scenes.Add(existing[index]);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ValidateMapScene(
            string scenePath,
            string expectedMapId,
            int expectedTiles)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new System.InvalidOperationException(
                    string.Concat("Could not open ", scenePath, "."));
            }

            BoardGameplayController controller =
                Object.FindObjectOfType<BoardGameplayController>();
            MapSceneBootstrap bootstrap = Object.FindObjectOfType<MapSceneBootstrap>();
            int tiles = controller == null
                ? 0
                : controller.GetComponentsInChildren<BoardTileView>(true).Length;
            int whiteGroundObjects = 0;
            Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == "BoardBase" ||
                    transforms[index].name.StartsWith("GridH_") ||
                    transforms[index].name.StartsWith("GridV_"))
                {
                    whiteGroundObjects++;
                }
            }

            if (controller == null ||
                bootstrap == null ||
                bootstrap.MapId != expectedMapId ||
                tiles != expectedTiles ||
                whiteGroundObjects != 0)
            {
                throw new System.InvalidOperationException(string.Format(
                    "{0}: controller={1}, map='{2}', tiles={3}/{4}, whiteGround={5}.",
                    scenePath,
                    controller != null,
                    bootstrap == null ? "<missing>" : bootstrap.MapId,
                    tiles,
                    expectedTiles,
                    whiteGroundObjects));
            }

            ValidateSpawnPoints(controller);
            ValidateTileScale(controller);
        }

        private static void ValidateTileScale(BoardGameplayController controller)
        {
            BoardTileView[] tiles = controller.GetComponentsInChildren<BoardTileView>(true);
            Vector3 expectedScale = new Vector3(
                BoardMapBuilder.TileFootprint,
                BoardMapBuilder.TileHeight,
                BoardMapBuilder.TileFootprint);
            for (int index = 0; index < tiles.Length; index++)
            {
                Vector3 scale = tiles[index].transform.localScale;
                if (Mathf.Abs(scale.x - expectedScale.x) > 0.001f ||
                    Mathf.Abs(scale.y - expectedScale.y) > 0.001f ||
                    Mathf.Abs(scale.z - expectedScale.z) > 0.001f)
                {
                    throw new InvalidOperationException(string.Format(
                        "Tile {0} does not use the {1:F3}x map scale.",
                        tiles[index].name,
                        BoardMapBuilder.TileScaleMultiplier));
                }
            }
        }

        private static void ValidateSpawnPoints(BoardGameplayController controller)
        {
            BoardTileView[] tiles = controller.GetComponentsInChildren<BoardTileView>(true);
            BoardPlayerToken[] tokens = controller.GetComponentsInChildren<BoardPlayerToken>(true);
            if (tokens.Length != 4)
            {
                throw new System.InvalidOperationException(
                    string.Concat("Expected 4 player tokens, found ", tokens.Length, "."));
            }

            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                BoardPlayerToken token = tokens[tokenIndex];
                BoardTileView home = null;
                for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
                {
                    if (tiles[tileIndex].TileType == TileType.Home &&
                        tiles[tileIndex].OwnerPlayerId == token.PlayerId)
                    {
                        home = tiles[tileIndex];
                        break;
                    }
                }

                if (home == null ||
                    Mathf.Abs(token.transform.position.x - home.transform.position.x) > 0.05f ||
                    Mathf.Abs(token.transform.position.z - home.transform.position.z) > 0.05f)
                {
                    throw new System.InvalidOperationException(string.Format(
                        "Player {0} is not standing on its home spawn.",
                        token.PlayerId));
                }
            }
        }

        private sealed class SceneCleanup
        {
            public SceneCleanup(
                Scene scene,
                bool openedForCleanup,
                int tileCount,
                int removedObjects)
            {
                Scene = scene;
                OpenedForCleanup = openedForCleanup;
                TileCount = tileCount;
                RemovedObjects = removedObjects;
            }

            public Scene Scene { get; }
            public bool OpenedForCleanup { get; }
            public int TileCount { get; }
            public int RemovedObjects { get; }
        }

        private sealed class SceneTileScaleResult
        {
            public SceneTileScaleResult(
                Scene scene,
                bool openedForEdit,
                int tileCount,
                float appliedScale)
            {
                Scene = scene;
                OpenedForEdit = openedForEdit;
                TileCount = tileCount;
                AppliedScale = appliedScale;
            }

            public Scene Scene { get; }
            public bool OpenedForEdit { get; }
            public int TileCount { get; }
            public float AppliedScale { get; }
        }

        private sealed class SceneCameraFrame
        {
            public SceneCameraFrame(
                Scene scene,
                bool openedForEdit,
                float orthographicSize,
                float cameraHeight)
            {
                Scene = scene;
                OpenedForEdit = openedForEdit;
                OrthographicSize = orthographicSize;
                CameraHeight = cameraHeight;
            }

            public Scene Scene { get; }
            public bool OpenedForEdit { get; }
            public float OrthographicSize { get; }
            public float CameraHeight { get; }
        }

        private static void ValidateBuildSettings()
        {
            HashSet<string> enabled = new HashSet<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int index = 0; index < scenes.Length; index++)
            {
                if (scenes[index].enabled)
                {
                    enabled.Add(scenes[index].path);
                }
            }

            if (!enabled.Contains(MainGamePath) ||
                !enabled.Contains(Map1Path) ||
                !enabled.Contains(Map2Path))
            {
                throw new System.InvalidOperationException(
                    "MainGame, Map1 and Map2 must all be enabled in Build Settings.");
            }
        }
    }
}
