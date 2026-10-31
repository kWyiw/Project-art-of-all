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
