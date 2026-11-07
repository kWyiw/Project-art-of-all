using System.Collections.Generic;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

            ValidateMapScene(Map1Path, MapCatalog.ClassicId, 68, 194);
            ValidateMapScene(Map2Path, MapCatalog.SweetRingId, 36, 194);

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
            int expectedTiles,
            int expectedGridLines)
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
            int gridLines = 0;
            Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name.StartsWith("GridH_") ||
                    transforms[index].name.StartsWith("GridV_"))
                {
                    gridLines++;
                }
            }

            if (controller == null ||
                bootstrap == null ||
                bootstrap.MapId != expectedMapId ||
                tiles != expectedTiles ||
                gridLines != expectedGridLines)
            {
                throw new System.InvalidOperationException(string.Format(
                    "{0}: controller={1}, map='{2}', tiles={3}/{4}, gridLines={5}/{6}.",
                    scenePath,
                    controller != null,
                    bootstrap == null ? "<missing>" : bootstrap.MapId,
                    tiles,
                    expectedTiles,
                    gridLines,
                    expectedGridLines));
            }

            ValidateSpawnPoints(controller);
            ValidateTileGridAlignment(controller);
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

        private static void ValidateTileGridAlignment(BoardGameplayController controller)
        {
            float minX = float.MaxValue;
            float minZ = float.MaxValue;
            Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform current = transforms[index];
                if (current.name.StartsWith("GridV_"))
                {
                    minX = Mathf.Min(minX, current.position.x);
                }
                else if (current.name.StartsWith("GridH_"))
                {
                    minZ = Mathf.Min(minZ, current.position.z);
                }
            }

            if (minX == float.MaxValue || minZ == float.MaxValue)
            {
                throw new System.InvalidOperationException(
                    "Map base grid lines are missing.");
            }

            const float centreOffset = BoardMapBuilder.GridCellSize * 0.5f;
            BoardTileView[] tiles = controller.GetComponentsInChildren<BoardTileView>(true);
            for (int index = 0; index < tiles.Length; index++)
            {
                float x = (tiles[index].transform.position.x - (minX + centreOffset)) /
                          BoardMapBuilder.GridCellSize;
                float z = (tiles[index].transform.position.z - (minZ + centreOffset)) /
                          BoardMapBuilder.GridCellSize;
                if (Mathf.Abs(x - Mathf.Round(x)) > 0.01f ||
                    Mathf.Abs(z - Mathf.Round(z)) > 0.01f)
                {
                    throw new System.InvalidOperationException(string.Format(
                        "Tile {0} is not centred in a base-grid cell.",
                        tiles[index].name));
                }
            }
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
