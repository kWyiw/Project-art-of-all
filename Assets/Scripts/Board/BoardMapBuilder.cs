using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Flow;
using UnityEngine;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// Replaces the baked scene tiles with the tiles of a generated board. The scene board is
    /// used as a palette: every material, the tile scale and the tile parent are captured once,
    /// then the old tiles are removed and the new layout is built from cubes.
    /// </summary>
    public static class BoardMapBuilder
    {
        private const float TileSpacing = 1.62f;
        private const float TileHeight = 0.1f;
        private const string GeneratedRootName = "GeneratedBoard";

        private static readonly TileType[] MaterialFallbacks =
        {
            TileType.Neutral,
            TileType.Reward,
            TileType.Monster,
            TileType.Shop,
            TileType.Warp,
            TileType.Event,
            TileType.Home
        };

        /// <summary>
        /// Builds <paramref name="definition"/> in place of the scene tiles and returns true when
        /// the board was replaced. The caller must rebuild its tile lookup afterwards.
        /// </summary>
        public static bool Build(BoardGameplayController controller, BoardMapDefinition definition)
        {
            if (controller == null || definition == null || definition.Cells.Count == 0)
            {
                return false;
            }

            BoardTileView[] existing =
                controller.GetComponentsInChildren<BoardTileView>(true);
            if (existing.Length == 0)
            {
                return false;
            }

            Transform parent = existing[0].transform.parent != null
                ? existing[0].transform.parent
                : controller.transform;
            Vector3 tileScale = existing[0].transform.localScale;
            Dictionary<int, Material> materials = CaptureMaterials(existing);

            for (int index = 0; index < existing.Length; index++)
            {
                BoardTileView tile = existing[index];
                if (tile == null)
                {
                    continue;
                }

                Object.DestroyImmediate(tile.gameObject);
            }

            Transform root = new GameObject(GeneratedRootName).transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.Euler(0f, definition.RotationYDegrees, 0f);
            // 旋转 45° 的菱形环对角线比场景里那张方阵更长，整体缩一点才不会被
            // 固定机位的相机切掉顶点（两角正好是玩家的 home）。
            float fit = definition.RotationYDegrees > 0.01f ? 0.86f : 1f;
            root.localScale = new Vector3(fit, 1f, fit);

            // 生成的地图只占网格的一部分，按自身包围盒居中，才能落在镜头中央。
            float centreRow = 0f;
            float centreColumn = 0f;
            int minRow = int.MaxValue;
            int maxRow = int.MinValue;
            int minColumn = int.MaxValue;
            int maxColumn = int.MinValue;
            foreach (BoardMapCell cell in definition.Cells)
            {
                minRow = Mathf.Min(minRow, cell.Row);
                maxRow = Mathf.Max(maxRow, cell.Row);
                minColumn = Mathf.Min(minColumn, cell.Column);
                maxColumn = Mathf.Max(maxColumn, cell.Column);
            }

            centreRow = (minRow + maxRow) * 0.5f;
            centreColumn = (minColumn + maxColumn) * 0.5f;

            int order = 0;
            foreach (BoardMapCell cell in definition.Cells
                         .OrderBy(cell => cell.Row)
                         .ThenBy(cell => cell.Column))
            {
                order++;
                GameObject tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tileObject.name = string.Format(
                    "Tile_{0:D2}_R{1:D2}_C{2:D2}",
                    order,
                    cell.Row,
                    cell.Column);
                tileObject.transform.SetParent(root, false);
                tileObject.transform.localPosition =
                    CellPosition(cell.Row, cell.Column, centreRow, centreColumn);
                tileObject.transform.localScale = tileScale;

                MeshRenderer renderer = tileObject.GetComponent<MeshRenderer>();
                Material material = ResolveMaterial(materials, cell.Type, cell.OwnerPlayerId);
                if (renderer != null && material != null)
                {
                    renderer.sharedMaterial = material;
                }

                BoardTileView view = tileObject.AddComponent<BoardTileView>();
                view.Configure(
                    cell.Row,
                    cell.Column,
                    cell.Type,
                    cell.Type == TileType.Home ? cell.OwnerPlayerId : -1);
            }

            Debug.Log(string.Format(
                "[Board] map '{0}' built: {1} tiles, rotation {2}°",
                definition.Id,
                order,
                definition.RotationYDegrees));
            return true;
        }

        private static Vector3 CellPosition(
            int row,
            int column,
            float centreRow,
            float centreColumn)
        {
            return new Vector3(
                (column - centreColumn) * TileSpacing,
                TileHeight,
                (centreRow - row) * TileSpacing);
        }

        private static Dictionary<int, Material> CaptureMaterials(BoardTileView[] tiles)
        {
            Dictionary<int, Material> materials = new Dictionary<int, Material>();
            for (int index = 0; index < tiles.Length; index++)
            {
                BoardTileView tile = tiles[index];
                if (tile == null)
                {
                    continue;
                }

                int key = MaterialKey(tile.TileType, tile.OwnerPlayerId);
                if (materials.ContainsKey(key))
                {
                    continue;
                }

                MeshRenderer renderer = tile.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    materials[key] = renderer.sharedMaterial;
                }
            }

            return materials;
        }

        private static Material ResolveMaterial(
            IReadOnlyDictionary<int, Material> materials,
            TileType type,
            int ownerPlayerId)
        {
            Material material;
            if (materials.TryGetValue(MaterialKey(type, ownerPlayerId), out material))
            {
                return material;
            }

            if (type == TileType.Home)
            {
                for (int owner = 0; owner < LobbyState.SeatCount; owner++)
                {
                    if (materials.TryGetValue(MaterialKey(TileType.Home, owner), out material))
                    {
                        return material;
                    }
                }
            }

            for (int index = 0; index < MaterialFallbacks.Length; index++)
            {
                TileType fallback = MaterialFallbacks[index];
                if (materials.TryGetValue(MaterialKey(fallback, -1), out material))
                {
                    return material;
                }
            }

            return null;
        }

        private static int MaterialKey(TileType type, int ownerPlayerId)
        {
            int owner = type == TileType.Home ? ownerPlayerId : -1;
            return (int)type * 16 + (owner + 1);
        }
    }
}
