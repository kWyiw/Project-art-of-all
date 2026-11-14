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
        /// Where a player may go after arriving at <paramref name="nodeId"/> from
        /// <paramref name="previousNodeId"/>.
        /// R-MOVE-004：只要路径经过分岔路，就提供所有非回头方向供玩家选择。
        /// R-MAP-002：普通走廊没有选择，只沿唯一的前进方向继续。
        /// R-MOVE-001：原路折返永远不是选项。
        /// </summary>
        public IReadOnlyList<int> LegalNextNodes(int nodeId, int previousNodeId)
        {
            MapNode node = GetNode(nodeId);
            MapNode previous = previousNodeId >= 0
                ? GetNode(previousNodeId)
                : null;
            if (!node.HasGridPosition ||
                (previous != null && !previous.HasGridPosition))
            {
                return WithoutPrevious(node, previousNodeId);
            }

            if (node.IsFork)
            {
                int forkForwardRow = node.GridRow - previous.GridRow;
                int forkForwardColumn = node.GridColumn - previous.GridColumn;
                List<int> forkChoices = new List<int>(2);
                foreach (int edge in node.OutEdges)
                {
                    if (edge == previousNodeId)
                    {
                        continue;
                    }

                    MapNode target = GetNode(edge);
                    int stepRow = target.GridRow - node.GridRow;
                    int stepColumn = target.GridColumn - node.GridColumn;
                    bool isStraight =
                        stepRow == forkForwardRow &&
                        stepColumn == forkForwardColumn;
                    bool isLeftTurn =
                        stepRow == -forkForwardColumn &&
                        stepColumn == forkForwardRow;
                    if (isStraight || isLeftTurn)
                    {
                        forkChoices.Add(edge);
                    }
                }

                return forkChoices.Count > 0
                    ? forkChoices.AsReadOnly()
                    : WithoutPrevious(node, previousNodeId);
            }

            if (previous == null)
            {
                List<int> counterClockwise = new List<int>(node.OutEdges.Count);
                foreach (int edge in node.OutEdges)
                {
                    if (IsCounterClockwiseStep(node, GetNode(edge)))
                    {
                        counterClockwise.Add(edge);
                    }
                }

                return counterClockwise.Count > 0
                    ? counterClockwise.AsReadOnly()
                    : node.OutEdges;
            }

            int forwardRow = node.GridRow - previous.GridRow;
            int forwardColumn = node.GridColumn - previous.GridColumn;
            List<int> straight = new List<int>(2);
            List<int> leftTurn = new List<int>(2);
            List<int> fallback = new List<int>(2);

            foreach (int edge in node.OutEdges)
            {
                if (edge == previousNodeId)
                {
                    continue;
                }

                MapNode target = GetNode(edge);
                if (!target.HasGridPosition)
                {
                    fallback.Add(edge);
                    continue;
                }

                int stepRow = target.GridRow - node.GridRow;
                int stepColumn = target.GridColumn - node.GridColumn;
                if (stepRow == forwardRow && stepColumn == forwardColumn)
                {
                    straight.Add(edge);
                }
                else if (stepRow == -forwardColumn && stepColumn == forwardRow)
                {
                    leftTurn.Add(edge);
                }
                else
                {
                    fallback.Add(edge);
                }
            }

            if (node.ForceStraight && straight.Count > 0)
            {
                return straight.AsReadOnly();
            }

            if (straight.Count > 0)
            {
                return straight.AsReadOnly();
            }

            if (leftTurn.Count > 0)
            {
                return leftTurn.AsReadOnly();
            }

            if (fallback.Count > 0)
            {
                return fallback.AsReadOnly();
            }

            return node.OutEdges;
        }

        private IReadOnlyList<int> WithoutPrevious(MapNode node, int previousNodeId)
        {
            List<int> legal = node.OutEdges
                .Where(edge => edge != previousNodeId)
                .ToList();

            return legal.Count == 0 ? node.OutEdges : legal.AsReadOnly();
        }

        /// <summary>
        /// True when moving from <paramref name="fromNodeId"/> to
        /// <paramref name="toNodeId"/> advances the polar angle around the board centre.
        /// A half-turn through the centre still counts as forward progress.
        /// </summary>
        public bool IsCounterClockwiseStep(int fromNodeId, int toNodeId)
        {
            return IsCounterClockwiseStep(GetNode(fromNodeId), GetNode(toNodeId));
        }

        private bool IsCounterClockwiseStep(MapNode from, MapNode to)
        {
            if (!from.HasGridPosition || !to.HasGridPosition)
            {
                return false;
            }

            double delta = NormalizeDegrees(
                PolarAngle(to.GridRow, to.GridColumn) -
                PolarAngle(from.GridRow, from.GridColumn));
            return delta > 0.0001 && delta <= 180.0001;
        }

        /// <summary>
        /// The edge a player takes when leaving Home on their very first move: the
        /// neighbour that turns counter-clockwise around the board centre. The choice is
        /// forced there, so no fork prompt is shown (R-MAP-002). -1 when the node has no
        /// grid position or no neighbour.
        /// </summary>
        public int CounterClockwiseNeighbour(int nodeId)
        {
            return FindCounterClockwiseNeighbour(GetNode(nodeId));
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
                if (delta > 0.0001 && delta <= 180.0001 && delta < bestDelta)
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
