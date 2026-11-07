using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using UnityEngine;

namespace ProjectArtOfAll.Board
{
    public static class UnityMapGraphFactory
    {
        /// <summary>Coordinates are packed as row * Stride + column; 16 covers every board we ship.</summary>
        private const int Stride = 16;
        private const int MaxCoordinate = Stride - 1;

        public static MapGraph Build(
            IEnumerable<BoardTileView> views,
            IEnumerable<IEnumerable<int>> teleportGroups = null)
        {
            Dictionary<int, BoardTileView> byId =
                new Dictionary<int, BoardTileView>();

            foreach (BoardTileView view in views)
            {
                byId.Add(NodeId(view.Row, view.Column), view);
            }

            List<MapNode> nodes = new List<MapNode>();
            List<int> warpNodes = new List<int>();

            foreach (BoardTileView view in byId.Values.OrderBy(view => view.Row)
                         .ThenBy(view => view.Column))
            {
                int id = NodeId(view.Row, view.Column);
                List<int> outgoing = BuildAdjacencyEdges(view, byId);
                nodes.Add(
                    new MapNode(
                        id,
                        view.TileType,
                        view.TileType == TileType.Home
                            ? view.OwnerPlayerId
                            : -1,
                        new MapCell(view.Row, view.Column),
                        outgoing.ToArray()));

                if (view.TileType == TileType.Warp)
                {
                    warpNodes.Add(id);
                }
            }

            if (teleportGroups == null)
            {
                // 默认：所有传送门通到同一处（场景棋盘就是这个口径）。
                teleportGroups = warpNodes.Count >= 2
                    ? new[] { warpNodes.OrderBy(id => id).ToArray() }
                    : Enumerable.Empty<IEnumerable<int>>();
            }

            return MapGraph.Create(
                nodes,
                playerCount: 4,
                teleportGroups: teleportGroups);
        }

        /// <summary>
        /// Builds the graph of a generated board. Shaped boards spell out their own links, so the
        /// grid adjacency of the baked board is not applied to them; a board without links is
        /// linked by grid neighbours.
        /// </summary>
        public static MapGraph BuildFromDefinition(BoardMapDefinition definition)
        {
            if (definition == null || definition.Cells.Count == 0)
            {
                throw new System.ArgumentException("The board definition has no cells.", nameof(definition));
            }

            Dictionary<long, BoardMapCell> byCell = new Dictionary<long, BoardMapCell>();
            foreach (BoardMapCell cell in definition.Cells)
            {
                byCell[Pack(cell.Row, cell.Column)] = cell;
            }

            Dictionary<long, List<int>> links = new Dictionary<long, List<int>>();
            foreach (BoardMapCell cell in definition.Cells)
            {
                links[Pack(cell.Row, cell.Column)] = new List<int>();
            }

            if (definition.Edges.Count > 0)
            {
                foreach (BoardMapEdge edge in definition.Edges)
                {
                    bool hasA = byCell.ContainsKey(Pack(edge.RowA, edge.ColumnA));
                    bool hasB = byCell.ContainsKey(Pack(edge.RowB, edge.ColumnB));
                    if (!hasA || !hasB)
                    {
                        Debug.LogWarning(string.Format(
                            "[Board] edge ({0},{1})-({2},{3}) touches a tile that is not on the board; skipped.",
                            edge.RowA,
                            edge.ColumnA,
                            edge.RowB,
                            edge.ColumnB));
                        continue;
                    }

                    int idA = NodeId(edge.RowA, edge.ColumnA);
                    int idB = NodeId(edge.RowB, edge.ColumnB);
                    links[Pack(edge.RowA, edge.ColumnA)].Add(idB);
                    links[Pack(edge.RowB, edge.ColumnB)].Add(idA);
                }
            }
            else
            {
                Vector2Int[] offsets =
                {
                    new Vector2Int(-1, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(1, 0),
                    new Vector2Int(0, -1)
                };

                foreach (BoardMapCell cell in definition.Cells)
                {
                    foreach (Vector2Int offset in offsets)
                    {
                        long key = Pack(cell.Row + offset.x, cell.Column + offset.y);
                        if (byCell.ContainsKey(key))
                        {
                            links[Pack(cell.Row, cell.Column)].Add(
                                NodeId(cell.Row + offset.x, cell.Column + offset.y));
                        }
                    }
                }
            }

            List<MapNode> nodes = new List<MapNode>(definition.Cells.Count);
            foreach (BoardMapCell cell in definition.Cells
                         .OrderBy(cell => cell.Row)
                         .ThenBy(cell => cell.Column))
            {
                List<int> outgoing = links[Pack(cell.Row, cell.Column)];
                outgoing.Sort();
                nodes.Add(new MapNode(
                    NodeId(cell.Row, cell.Column),
                    cell.Type,
                    cell.Type == TileType.Home ? cell.OwnerPlayerId : -1,
                    new MapCell(cell.Row, cell.Column),
                    cell.ForceStraight,
                    outgoing.ToArray()));
            }

            List<IEnumerable<int>> teleportGroups = new List<IEnumerable<int>>();
            foreach (BoardMapTeleport teleport in definition.Teleports)
            {
                teleportGroups.Add(new[]
                {
                    NodeId(teleport.RowA, teleport.ColumnA),
                    NodeId(teleport.RowB, teleport.ColumnB)
                });
            }

            return MapGraph.Create(
                nodes,
                playerCount: 4,
                teleportGroups: teleportGroups);
        }

        public static int NodeId(int row, int column)
        {
            return row * Stride + column;
        }

        public static Vector2Int CellFromNodeId(int nodeId)
        {
            return new Vector2Int(nodeId / Stride, nodeId % Stride);
        }

        private static long Pack(int row, int column)
        {
            return ((long)row << 32) | (uint)column;
        }

        private static List<int> BuildAdjacencyEdges(
            BoardTileView source,
            IReadOnlyDictionary<int, BoardTileView> byId)
        {
            Vector2Int[] offsets =
            {
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 0),
                new Vector2Int(0, -1)
            };

            List<int> outgoing = new List<int>();

            foreach (Vector2Int offset in offsets)
            {
                int row = source.Row + offset.x;
                int column = source.Column + offset.y;
                if (row < 0 || row > MaxCoordinate || column < 0 || column > MaxCoordinate)
                {
                    continue;
                }

                int id = NodeId(row, column);
                if (!byId.TryGetValue(id, out BoardTileView target))
                {
                    continue;
                }

                outgoing.Add(id);
            }

            return outgoing.OrderBy(id => id).ToList();
        }
    }
}
