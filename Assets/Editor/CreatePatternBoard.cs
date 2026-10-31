using System.Collections.Generic;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Editor
{
    internal static class CreatePatternBoard
    {
        private const string MainGamePath = "Assets/Scenes/MainGame.unity";
        private const string BoardRootName = "Board_GridLayout";
        private const string LegacyBoardRootName = "Board_28Tiles";
        private const string MaterialsFolder = "Assets/Materials";

        private const int GridSize = 12;
        private const int GroundGridCells = 96;
        private const float CellPitch = 1.62f;
        private const float TileSize = 1.5f;
        private const float TileHeight = 0.18f;
        private const float BaseHeight = 0.08f;
        private const float GridLineWidth = 0.025f;
        private const float PlayerWidth = 0.58f;
        private const float PlayerHeight = 1.35f;
        private const int MapGenerationSeed = 20261031;

        private static readonly string[] Pattern =
        {
            "############",
            "#...#......#",
            "#...#......#",
            "#...#......#",
            "#...########",
            "#...#..#...#",
            "#...#..#...#",
            "########...#",
            "#......#...#",
            "#......#...#",
            "#......#...#",
            "############"
        };

        [MenuItem("Tools/Project Art of All/Rebuild MainGame Pattern Board")]
        private static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = OpenMainGameScene();
            BuildBoard(scene);
        }

        private static Scene OpenMainGameScene()
        {
            Scene active = SceneManager.GetActiveScene();
            return active.path == MainGamePath
                ? active
                : EditorSceneManager.OpenScene(MainGamePath, OpenSceneMode.Single);
        }

        private static void BuildBoard(Scene scene)
        {
            GameObject existing = GameObject.Find(BoardRootName);
            GameObject legacy = GameObject.Find(LegacyBoardRootName);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build MainGame pattern board");

            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            if (legacy != null)
            {
                Undo.DestroyObjectImmediate(legacy);
            }

            EnsureFolder(MaterialsFolder);
            Material neutralMaterial = CreateOrUpdateMaterial(
                $"{MaterialsFolder}/BoardTileTeal.mat",
                new Color(15f / 255f, 118f / 255f, 110f / 255f, 1f),
                metallic: 0f,
                smoothness: 0.22f);
            Dictionary<TileType, Material> tileMaterials =
                CreateTileMaterials(neutralMaterial);
            Material[] playerMaterials = new Material[4];
            Color[] playerColors =
            {
                new Color(0.82f, 0.16f, 0.13f, 1f),
                new Color(0.96f, 0.76f, 0.14f, 1f),
                new Color(0.12f, 0.40f, 0.88f, 1f),
                new Color(0.16f, 0.68f, 0.28f, 1f)
            };
            string[] colorNames = { "Red", "Yellow", "Blue", "Green" };

            for (int playerId = 0; playerId < playerColors.Length; playerId++)
            {
                Color playerColor = playerColors[playerId];
                playerMaterials[playerId] = CreateOrUpdateMaterial(
                    $"{MaterialsFolder}/Player_{colorNames[playerId]}.mat",
                    playerColor,
                    metallic: 0f,
                    smoothness: 0.36f);
            }

            Material homeTileMaterial = CreateOrUpdateMaterial(
                $"{MaterialsFolder}/Home_Red.mat",
                Color.Lerp(Color.white, playerColors[0], 0.36f),
                metallic: 0f,
                smoothness: 0.12f);

            Material baseMaterial = CreateOrUpdateMaterial(
                $"{MaterialsFolder}/BoardBaseWhite.mat",
                new Color(0.985f, 0.99f, 0.995f, 1f),
                metallic: 0f,
                smoothness: 0.04f);
            Material gridMaterial = CreateOrUpdateMaterial(
                $"{MaterialsFolder}/BoardGridLine.mat",
                new Color(0.72f, 0.76f, 0.80f, 1f),
                metallic: 0f,
                smoothness: 0.05f);

            GameObject boardRoot = new GameObject(BoardRootName);
            Undo.RegisterCreatedObjectUndo(boardRoot, "Create pattern board");
            boardRoot.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            CreateBase(boardRoot.transform, baseMaterial);
            CreateGridLines(boardRoot.transform, gridMaterial);
            int tileCount = CreatePatternTiles(
                boardRoot.transform,
                tileMaterials,
                homeTileMaterial,
                playerMaterials);
            if (boardRoot.GetComponent<BoardGameplayController>() == null)
            {
                Undo.AddComponent<BoardGameplayController>(boardRoot);
            }
            ConfigureCamera(boardRoot.transform);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = boardRoot;
            Debug.Log(
                $"[Board] Built a {tileCount}-tile pattern on a " +
                $"{GridSize}x{GridSize} grid in {MainGamePath}.");
        }

        private static void ConfigureCamera(Transform boardRoot)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            BoardCameraController controller =
                camera.GetComponent<BoardCameraController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<BoardCameraController>(camera.gameObject);
            }

            Transform target = boardRoot.Find("PlayerTokens/Player_1_Red");
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("boardCamera").objectReferenceValue = camera;
            serializedController.FindProperty("followTarget").objectReferenceValue = target;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateBase(Transform parent, Material material)
        {
            float boardSize = GroundGridCells * CellPitch;
            GameObject boardBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boardBase.name = "BoardBase";
            boardBase.transform.SetParent(parent, false);
            boardBase.transform.localPosition = new Vector3(0f, -BaseHeight * 0.5f + 0.01f, 0f);
            boardBase.transform.localScale = new Vector3(
                boardSize,
                BaseHeight,
                boardSize);
            boardBase.GetComponent<MeshRenderer>().sharedMaterial = material;
            Undo.RegisterCreatedObjectUndo(boardBase, "Create board base");
        }

        private static void CreateGridLines(Transform parent, Material material)
        {
            float boardSize = GroundGridCells * CellPitch;
            float lineHeight = 0.01f;
            float lineY = TileHeight + 0.008f;
            int halfCells = GroundGridCells / 2;

            for (int index = -halfCells; index <= halfCells; index++)
            {
                float offset = index * CellPitch;
                int lineNumber = index + halfCells;

                CreateLine(
                    parent,
                    $"GridV_{lineNumber:00}",
                    new Vector3(offset, lineY, 0f),
                    new Vector3(GridLineWidth, lineHeight, boardSize),
                    material);
                CreateLine(
                    parent,
                    $"GridH_{lineNumber:00}",
                    new Vector3(0f, lineY, -offset),
                    new Vector3(boardSize, lineHeight, GridLineWidth),
                    material);
            }
        }

        private static void CreateLine(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.SetParent(parent, false);
            line.transform.localPosition = localPosition;
            line.transform.localScale = localScale;
            line.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(line.GetComponent<Collider>());
            Undo.RegisterCreatedObjectUndo(line, "Create grid line");
        }

        private static int CreatePatternTiles(
            Transform parent,
            IReadOnlyDictionary<TileType, Material> tileMaterials,
            Material homeTileMaterial,
            IReadOnlyList<Material> playerMaterials)
        {
            int tileNumber = 0;
            System.Random random = new System.Random(MapGenerationSeed);
            GameObject playerTokens = new GameObject("PlayerTokens");
            playerTokens.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(playerTokens, "Create player tokens");
            HashSet<int> shopCells = new HashSet<int>
            {
                RowColumnKey(4, 4),
                RowColumnKey(4, 7),
                RowColumnKey(7, 4),
                RowColumnKey(7, 7)
            };
            Dictionary<int, int> homeOwners = new Dictionary<int, int>
            {
                [RowColumnKey(0, 0)] = 0,
                [RowColumnKey(0, GridSize - 1)] = 1,
                [RowColumnKey(GridSize - 1, GridSize - 1)] = 2,
                [RowColumnKey(GridSize - 1, 0)] = 3
            };

            for (int row = 0; row < GridSize; row++)
            {
                if (Pattern[row].Length != GridSize)
                {
                    throw new System.InvalidOperationException(
                        $"Pattern row {row} must contain {GridSize} cells.");
                }

                for (int column = 0; column < GridSize; column++)
                {
                    if (Pattern[row][column] != '#')
                    {
                        continue;
                    }

                    tileNumber++;
                    int cellKey = RowColumnKey(row, column);
                    TileType tileType;
                    int ownerPlayerId = -1;
                    Material material;

                    if (homeOwners.TryGetValue(cellKey, out ownerPlayerId))
                    {
                        tileType = TileType.Home;
                        material = homeTileMaterial;
                    }
                    else if (shopCells.Contains(cellKey))
                    {
                        tileType = TileType.Shop;
                        material = tileMaterials[tileType];
                    }
                    else if (IsOuterRing(row, column))
                    {
                        tileType = RollOuterRingTileType(random);
                        material = tileMaterials[tileType];
                    }
                    else
                    {
                        tileType = TileType.Neutral;
                        material = tileMaterials[tileType];
                    }

                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Tile_{tileNumber:00}_R{row:00}_C{column:00}";
                    tile.transform.SetParent(parent, false);
                    tile.transform.localPosition = new Vector3(
                        (column - (GridSize - 1) * 0.5f) * CellPitch,
                        TileHeight * 0.5f + 0.01f,
                        ((GridSize - 1) * 0.5f - row) * CellPitch);
                    tile.transform.localScale = new Vector3(
                        TileSize,
                        TileHeight,
                        TileSize);
                    tile.GetComponent<MeshRenderer>().sharedMaterial = material;
                    tile.AddComponent<BoardTileView>().Configure(
                        row,
                        column,
                        tileType,
                        ownerPlayerId);
                    Undo.RegisterCreatedObjectUndo(tile, "Create pattern tile");

                    if (tileType == TileType.Home)
                    {
                        CreatePlayerToken(
                            playerTokens.transform,
                            tile.transform.localPosition,
                            playerMaterials[ownerPlayerId],
                            ownerPlayerId,
                            row,
                            column);
                    }
                }
            }

            return tileNumber;
        }

        private static Dictionary<TileType, Material> CreateTileMaterials(
            Material neutralMaterial)
        {
            return new Dictionary<TileType, Material>
            {
                [TileType.Neutral] = neutralMaterial,
                [TileType.Shop] = CreateOrUpdateMaterial(
                    $"{MaterialsFolder}/Tile_Shop.mat",
                    new Color(0.48f, 0.28f, 0.68f, 1f),
                    metallic: 0f,
                    smoothness: 0.3f),
                [TileType.Reward] = CreateOrUpdateMaterial(
                    $"{MaterialsFolder}/Tile_Reward.mat",
                    new Color(0.96f, 0.70f, 0.16f, 1f),
                    metallic: 0f,
                    smoothness: 0.34f),
                [TileType.Monster] = CreateOrUpdateMaterial(
                    $"{MaterialsFolder}/Tile_Monster.mat",
                    new Color(0.56f, 0.15f, 0.13f, 1f),
                    metallic: 0f,
                    smoothness: 0.24f),
                [TileType.Event] = CreateOrUpdateMaterial(
                    $"{MaterialsFolder}/Tile_Event.mat",
                    new Color(0.16f, 0.58f, 0.65f, 1f),
                    metallic: 0f,
                    smoothness: 0.26f),
                [TileType.Warp] = CreateOrUpdateMaterial(
                    $"{MaterialsFolder}/Tile_Warp.mat",
                    new Color(0.18f, 0.28f, 0.70f, 1f),
                    metallic: 0f,
                    smoothness: 0.28f)
            };
        }

        private static TileType RollOuterRingTileType(System.Random random)
        {
            int roll = random.Next(100);
            if (roll < 34)
            {
                return TileType.Reward;
            }

            if (roll < 60)
            {
                return TileType.Monster;
            }

            if (roll < 82)
            {
                return TileType.Event;
            }

            return TileType.Warp;
        }

        private static bool IsOuterRing(int row, int column)
        {
            return row == 0 ||
                   row == GridSize - 1 ||
                   column == 0 ||
                   column == GridSize - 1;
        }

        private static int RowColumnKey(int row, int column)
        {
            return row * GridSize + column;
        }

        private static void CreatePlayerToken(
            Transform parent,
            Vector3 tilePosition,
            Material material,
            int playerId,
            int row,
            int column)
        {
            GameObject token = GameObject.CreatePrimitive(PrimitiveType.Cube);
            token.name = $"Player_{playerId + 1}_{GetPlayerColorName(playerId)}";
            token.transform.SetParent(parent, false);
            token.transform.localPosition = new Vector3(
                tilePosition.x,
                TileHeight + 0.01f + PlayerHeight * 0.5f,
                tilePosition.z);
            token.transform.localRotation = Quaternion.identity;
            token.transform.localScale = new Vector3(
                PlayerWidth,
                PlayerHeight,
                PlayerWidth);
            token.GetComponent<MeshRenderer>().sharedMaterial = material;
            token.AddComponent<BoardPlayerToken>().Configure(playerId, row, column);
            Undo.RegisterCreatedObjectUndo(token, "Create player token");
        }

        private static string GetPlayerColorName(int playerId)
        {
            switch (playerId)
            {
                case 0:
                    return "Red";
                case 1:
                    return "Yellow";
                case 2:
                    return "Blue";
                default:
                    return "Green";
            }
        }

        private static Material CreateOrUpdateMaterial(
            string assetPath,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }
        }
    }
}
