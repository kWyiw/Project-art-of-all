using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectArtOfAll.Core.Board
{
    public static class MovementSolver
    {
        /// <summary>
        /// Finds every path on which the current player may voluntarily stop.
        /// A stop is legal at the player's own Home or at a node containing another player.
        /// The start node and the final non-stopping node are intentionally not candidates.
        /// </summary>
        public static IReadOnlyList<MovementCandidate> FindStopCandidates(
            MapGraph graph,
            int playerId,
            int startNodeId,
            int remainingSteps,
            IReadOnlyDictionary<int, int> playerNodeIds)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (remainingSteps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(remainingSteps));
            }

            graph.GetNode(startNodeId);
            Dictionary<int, List<int>> occupants = BuildOccupants(playerNodeIds);
            List<MovementCandidate> candidates = new List<MovementCandidate>();
            List<int> path = new List<int>();

            Walk(
                graph,
                playerId,
                startNodeId,
                remainingSteps,
                occupants,
                path,
                candidates);

            return candidates
                .OrderBy(candidate => candidate.Steps)
                .ThenBy(candidate => candidate.TargetNodeId)
                .ThenBy(candidate => string.Join(",", candidate.Path))
                .ToArray();
        }

        public static IReadOnlyList<IReadOnlyList<int>> FindPaths(
            MapGraph graph,
            int startNodeId,
            int steps)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (steps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(steps));
            }

            graph.GetNode(startNodeId);
            List<IReadOnlyList<int>> paths = new List<IReadOnlyList<int>>();
            List<int> current = new List<int>();
            WalkPaths(graph, startNodeId, steps, current, paths);

            return paths
                .OrderBy(path => string.Join(",", path))
                .ToArray();
        }

        /// <summary>
        /// Resolves an exact route using one outgoing-edge index per movement step.
        /// Returned nodes exclude the start node.
        /// </summary>
        public static IReadOnlyList<int> ResolvePathByChoices(
            MapGraph graph,
            int startNodeId,
            IReadOnlyList<int> outgoingEdgeChoices)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (outgoingEdgeChoices == null)
            {
                throw new ArgumentNullException(nameof(outgoingEdgeChoices));
            }

            List<int> path = new List<int>(outgoingEdgeChoices.Count);
            int current = startNodeId;

            for (int step = 0; step < outgoingEdgeChoices.Count; step++)
            {
                MapNode node = graph.GetNode(current);
                int choice = outgoingEdgeChoices[step];
                if (choice < 0 || choice >= node.OutEdges.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(outgoingEdgeChoices),
                        $"Step {step} choice {choice} is invalid at node {current}.");
                }

                current = node.OutEdges[choice];
                path.Add(current);
            }

            return path.AsReadOnly();
        }

        private static void Walk(
            MapGraph graph,
            int playerId,
            int currentNodeId,
            int stepsLeft,
            IReadOnlyDictionary<int, List<int>> occupants,
            List<int> path,
            List<MovementCandidate> candidates)
        {
            if (stepsLeft == 0)
            {
                return;
            }

            MapNode current = graph.GetNode(currentNodeId);
            foreach (int next in current.OutEdges.OrderBy(edge => edge))
            {
                path.Add(next);

                if (IsStoppable(graph.GetNode(next), playerId, occupants))
                {
                    candidates.Add(new MovementCandidate(next, path));
                }

                Walk(
                    graph,
                    playerId,
                    next,
                    stepsLeft - 1,
                    occupants,
                    path,
                    candidates);

                path.RemoveAt(path.Count - 1);
            }
        }

        private static void WalkPaths(
            MapGraph graph,
            int currentNodeId,
            int stepsLeft,
            List<int> currentPath,
            List<IReadOnlyList<int>> paths)
        {
            if (stepsLeft == 0)
            {
                paths.Add(Array.AsReadOnly(currentPath.ToArray()));
                return;
            }

            MapNode current = graph.GetNode(currentNodeId);
            foreach (int next in current.OutEdges.OrderBy(edge => edge))
            {
                currentPath.Add(next);
                WalkPaths(graph, next, stepsLeft - 1, currentPath, paths);
                currentPath.RemoveAt(currentPath.Count - 1);
            }
        }

        private static bool IsStoppable(
            MapNode node,
            int playerId,
            IReadOnlyDictionary<int, List<int>> occupants)
        {
            if (node.TileType == TileType.Home && node.OwnerPlayerId == playerId)
            {
                return true;
            }

            if (!occupants.TryGetValue(node.Id, out List<int> playersOnNode))
            {
                return false;
            }

            return playersOnNode.Any(occupantId => occupantId != playerId);
        }

        private static Dictionary<int, List<int>> BuildOccupants(
            IReadOnlyDictionary<int, int> playerNodeIds)
        {
            Dictionary<int, List<int>> occupants =
                new Dictionary<int, List<int>>();

            if (playerNodeIds == null)
            {
                return occupants;
            }

            foreach (KeyValuePair<int, int> pair in playerNodeIds)
            {
                if (!occupants.TryGetValue(pair.Value, out List<int> players))
                {
                    players = new List<int>();
                    occupants[pair.Value] = players;
                }

                players.Add(pair.Key);
            }

            foreach (List<int> players in occupants.Values)
            {
                players.Sort();
            }

            return occupants;
        }
    }
}
