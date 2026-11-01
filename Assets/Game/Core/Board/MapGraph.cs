using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProjectArtOfAll.Core.Board
{
    /// <summary>
    /// Immutable, validated directed graph used by board movement.
    /// Rendering coordinates intentionally do not exist in Core.
    /// </summary>
    public sealed class MapGraph
    {
        private readonly ReadOnlyCollection<MapNode> nodes;
        private readonly IReadOnlyDictionary<int, MapNode> nodesById;
        private readonly ReadOnlyCollection<ReadOnlyCollection<int>> teleportGroups;
        private readonly double centreRow;
        private readonly double centreColumn;

        private MapGraph(
            IEnumerable<MapNode> sourceNodes,
            int playerCount,
            IEnumerable<IEnumerable<int>> sourceTeleportGroups)
        {
            List<MapNode> orderedNodes = sourceNodes.OrderBy(node => node.Id).ToList();
            List<ReadOnlyCollection<int>> groups = (sourceTeleportGroups ??
                Enumerable.Empty<IEnumerable<int>>())
                .Select(group => Array.AsReadOnly((group ?? Array.Empty<int>()).ToArray()))
                .ToList();

            List<string> errors = Validate(
                orderedNodes,
                playerCount,
                groups);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invalid map graph:\n- " + string.Join("\n- ", errors));
            }

            // 逆时针方向以整张图的中心为基准，中心取带网格坐标节点的包围盒中点。
            List<MapNode> placed = orderedNodes.Where(node => node.HasGridPosition).ToList();
            if (placed.Count > 0)
            {
                centreRow = (placed.Min(node => node.GridRow) + placed.Max(node => node.GridRow)) * 0.5;
                centreColumn = (placed.Min(node => node.GridColumn) + placed.Max(node => node.GridColumn)) * 0.5;
            }

            nodes = Array.AsReadOnly(orderedNodes.ToArray());
            nodesById = orderedNodes.ToDictionary(node => node.Id);
            teleportGroups = Array.AsReadOnly(groups.ToArray());
            PlayerCount = playerCount;
        }

        public int PlayerCount { get; }
        public IReadOnlyList<MapNode> Nodes => nodes;
        public IReadOnlyList<ReadOnlyCollection<int>> TeleportGroups => teleportGroups;

        public static MapGraph Create(
            IEnumerable<MapNode> nodes,
            int playerCount = 4,
            IEnumerable<IEnumerable<int>> teleportGroups = null)
        {
            if (nodes == null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            return new MapGraph(nodes, playerCount, teleportGroups);
        }

        public MapNode GetNode(int nodeId)
        {
            if (!nodesById.TryGetValue(nodeId, out MapNode node))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nodeId),
                    $"Node {nodeId} does not exist.");
            }

            return node;
        }

        /// <summary>
        /// Edges a player may take when arriving at <paramref name="nodeId"/> from
        /// <paramref name="previousNodeId"/>. Walking straight back the way you came is
        /// not a choice (R-MOVE-001); at a dead end the only edge is still returned.
        /// </summary>
        /// <summary>
        /// Where a player may go after arriving at <paramref name="nodeId"/> from
        /// <paramref name="previousNodeId"/>.
        /// R-MAP-002：走廊（只有前后两格）只能**逆时针**走；岔路口（三岔及以上）
        /// 由玩家选，除原路返回外皆可。
        /// </summary>
        public IReadOnlyList<int> LegalNextNodes(int nodeId, int previousNodeId)
        {
            MapNode node = GetNode(nodeId);
            if (node.OutEdges.Count <= 1)
            {
                return node.OutEdges;
            }

            if (node.OutEdges.Count >= 3)
            {
                if (previousNodeId < 0)
                {
                    return node.OutEdges;
                }

                List<int> choices = node.OutEdges
                    .Where(edge => edge != previousNodeId)
                    .ToList();

                return choices.Count == 0 ? node.OutEdges : choices.AsReadOnly();
            }

            int counterClockwise = FindCounterClockwiseNeighbour(node);
            if (counterClockwise >= 0)
            {
                return new[] { counterClockwise };
            }

            List<int> legal = node.OutEdges
                .Where(edge => edge != previousNodeId)
                .ToList();

            return legal.Count == 0 ? node.OutEdges : legal.AsReadOnly();
        }

        /// <summary>
        /// The neighbour that lies counter-clockwise from <paramref name="node"/> around
        /// the board centre (smallest positive angle step). -1 when nodes carry no grid
        /// position or there is no such neighbour.
        /// </summary>
        private int FindCounterClockwiseNeighbour(MapNode node)
        {
            if (!node.HasGridPosition)
            {
                return -1;
            }

            double sourceAngle = PolarAngle(node.GridRow, node.GridColumn);
            int best = -1;
            double bestDelta = double.MaxValue;

            foreach (int edge in node.OutEdges)
            {
                MapNode neighbour = GetNode(edge);
                if (!neighbour.HasGridPosition)
                {
                    continue;
                }

                double delta = NormalizeDegrees(
                    PolarAngle(neighbour.GridRow, neighbour.GridColumn) - sourceAngle);
                if (delta > 0.0001 && delta < bestDelta)
                {
                    bestDelta = delta;
                    best = edge;
                }
            }

            return best;
        }

        private double PolarAngle(int row, int column)
        {
            return Math.Atan2(centreRow - row, column - centreColumn) * (180.0 / Math.PI);
        }

        private static double NormalizeDegrees(double degrees)
        {
            double value = degrees % 360.0;
            if (value < 0)
            {
                value += 360.0;
            }

            return value;
        }

        public int StepsBetween(int startNodeId, int targetNodeId)
        {
            MapNode start = GetNode(startNodeId);
            GetNode(targetNodeId);

            if (startNodeId == targetNodeId)
            {
                return 0;
            }

            Queue<(int NodeId, int Distance)> frontier =
                new Queue<(int NodeId, int Distance)>();
            HashSet<int> visited = new HashSet<int> { startNodeId };
            frontier.Enqueue((startNodeId, 0));

            while (frontier.Count > 0)
            {
                (int nodeId, int distance) = frontier.Dequeue();
                MapNode node = GetNode(nodeId);

                foreach (int nextNodeId in node.OutEdges.OrderBy(edge => edge))
                {
                    if (!visited.Add(nextNodeId))
                    {
                        continue;
                    }

                    if (nextNodeId == targetNodeId)
                    {
                        return distance + 1;
                    }

                    frontier.Enqueue((nextNodeId, distance + 1));
                }
            }

            return int.MaxValue;
        }

        private static List<string> Validate(
            IReadOnlyList<MapNode> mapNodes,
            int playerCount,
            IReadOnlyList<ReadOnlyCollection<int>> groups)
        {
            List<string> errors = new List<string>();

            if (mapNodes.Count == 0)
            {
                errors.Add("R-MAP-003: The graph has no nodes.");
                return errors;
            }

            if (playerCount <= 0)
            {
                errors.Add("R-BASE-001: Player count must be positive.");
            }

            Dictionary<int, MapNode> byId = new Dictionary<int, MapNode>();
            foreach (MapNode node in mapNodes)
            {
                if (!byId.TryAdd(node.Id, node))
                {
                    errors.Add($"R-MAP-003: Duplicate node id {node.Id}.");
                }
            }

            HashSet<int> referencedNodes = new HashSet<int>();
            foreach (MapNode node in mapNodes)
            {
                if (node.OutEdges.Count == 0)
                {
                    errors.Add($"R-MAP-003: Node {node.Id} has no outgoing edge.");
                }

                HashSet<int> uniqueEdges = new HashSet<int>();
                foreach (int edge in node.OutEdges)
                {
                    referencedNodes.Add(edge);
                    if (!byId.ContainsKey(edge))
                    {
                        errors.Add($"R-MAP-003: Node {node.Id} points to missing node {edge}.");
                    }

                    if (!uniqueEdges.Add(edge))
                    {
                        errors.Add($"R-MAP-003: Node {node.Id} repeats edge to {edge}.");
                    }
                }

                if (node.TileType == TileType.Home)
                {
                    if (node.OwnerPlayerId < 0 || node.OwnerPlayerId >= playerCount)
                    {
                        errors.Add(
                            $"R-MAP-003: Home node {node.Id} has invalid owner " +
                            $"{node.OwnerPlayerId}.");
                    }
                }
                else if (node.OwnerPlayerId != -1)
                {
                    errors.Add(
                        $"R-MAP-003: Non-home node {node.Id} has owner " +
                        $"{node.OwnerPlayerId}.");
                }
            }

            List<MapNode> homes = mapNodes
                .Where(node => node.TileType == TileType.Home)
                .ToList();

            if (homes.Count != playerCount ||
                homes.Select(home => home.OwnerPlayerId).Distinct().Count() != playerCount)
            {
                errors.Add(
                    $"R-MAP-003: Expected {playerCount} uniquely owned Home nodes, " +
                    $"found {homes.Count}.");
            }

            foreach (MapNode home in homes)
            {
                if (home.OwnerPlayerId >= 0 && home.OwnerPlayerId < playerCount)
                {
                    HashSet<int> reachable = FloodFill(home.Id, byId);
                    if (reachable.Count != mapNodes.Count)
                    {
                        errors.Add(
                            $"R-MAP-003: Home {home.Id} cannot reach all nodes.");
                    }
                }
            }

            HashSet<int> teleportMembers = new HashSet<int>();
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                ReadOnlyCollection<int> group = groups[groupIndex];
                if (group.Count < 2)
                {
                    errors.Add(
                        $"R-MAP-003: Teleport group {groupIndex} needs at least 2 nodes.");
                }

                foreach (int nodeId in group)
                {
                    if (!byId.ContainsKey(nodeId))
                    {
                        errors.Add(
                            $"R-MAP-003: Teleport group {groupIndex} references missing node " +
                            $"{nodeId}.");
                    }

                    if (!teleportMembers.Add(nodeId))
                    {
                        errors.Add(
                            $"R-MAP-003: Teleport node {nodeId} appears in multiple groups.");
                    }
                }
            }

            return errors;
        }

        private static HashSet<int> FloodFill(
            int startNodeId,
            IReadOnlyDictionary<int, MapNode> byId)
        {
            HashSet<int> visited = new HashSet<int> { startNodeId };
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(startNodeId);

            while (queue.Count > 0)
            {
                int nodeId = queue.Dequeue();
                if (!byId.TryGetValue(nodeId, out MapNode node))
                {
                    continue;
                }

                foreach (int next in node.OutEdges)
                {
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return visited;
        }
    }
}
