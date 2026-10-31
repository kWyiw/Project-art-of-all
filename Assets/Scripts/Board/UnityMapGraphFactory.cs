using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using UnityEngine;

namespace ProjectArtOfAll.Board
{
    public static class UnityMapGraphFactory
    {
        private const int GridSize = 12;
        private const float CenterCoordinate = 5.5f;
        private const float AngleEpsilon = 0.001f;

        public static MapGraph Build(IEnumerable<BoardTileView> views)
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
                List<int> outgoing = BuildCounterClockwiseEdges(view, byId);
                nodes.Add(
                    new MapNode(
                        id,
                        view.TileType,
                        view.TileType == TileType.Home
                            ? view.OwnerPlayerId
                            : -1,
                        outgoing.ToArray()));

                if (view.TileType == TileType.Warp)
                {
                    warpNodes.Add(id);
                }
            }

            IEnumerable<IEnumerable<int>> teleportGroups =
                warpNodes.Count >= 2
                    ? new[] { warpNodes.OrderBy(id => id).ToArray() }
                    : Enumerable.Empty<IEnumerable<int>>();

            return MapGraph.Create(
                nodes,
                playerCount: 4,
                teleportGroups: teleportGroups);
        }

        public static int NodeId(int row, int column)
        {
            return row * GridSize + column;
        }

        public static Vector2Int CellFromNodeId(int nodeId)
        {
            return new Vector2Int(nodeId / GridSize, nodeId % GridSize);
        }

        private static List<int> BuildCounterClockwiseEdges(
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

            float sourceAngle = PolarAngle(source.Row, source.Column);
            List<int> outgoing = new List<int>();

            foreach (Vector2Int offset in offsets)
            {
                int row = source.Row + offset.x;
                int column = source.Column + offset.y;
                int id = NodeId(row, column);
                if (!byId.TryGetValue(id, out BoardTileView target))
                {
                    continue;
                }

                float delta = Mathf.DeltaAngle(
                    sourceAngle,
                    PolarAngle(target.Row, target.Column));
                if (delta > AngleEpsilon)
                {
                    outgoing.Add(id);
                }
            }

            return outgoing.OrderBy(id => id).ToList();
        }

        private static float PolarAngle(int row, int column)
        {
            return Mathf.Atan2(
                CenterCoordinate - row,
                column - CenterCoordinate) * Mathf.Rad2Deg;
        }
    }
}
