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
            int previousNodeId,
            int remainingSteps,
            IReadOnlyDictionary<int, int> playerNodeIds,
            int forcedFirstNodeId = -1)
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
                previousNodeId,
                remainingSteps,
                occupants,
                forcedFirstNodeId,
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
            int previousNodeId,
            int steps,
            int forcedFirstNodeId = -1)
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
            WalkPaths(
                graph,
                startNodeId,
                previousNodeId,
                steps,
                forcedFirstNodeId,
                current,
                paths);

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
            int previousNodeId,
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
            int previous = previousNodeId;

            for (int step = 0; step < outgoingEdgeChoices.Count; step++)
            {
                IReadOnlyList<int> legal = graph.LegalNextNodes(current, previous);
                int choice = outgoingEdgeChoices[step];
                if (choice < 0 || choice >= legal.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(outgoingEdgeChoices),
                        $"Step {step} choice {choice} is invalid at node {current}.");
                }

                previous = current;
                current = legal[choice];
                path.Add(current);
            }

            return path.AsReadOnly();
        }

        private static void Walk(
            MapGraph graph,
            int playerId,
            int currentNodeId,
            int previousNodeId,
            int stepsLeft,
            IReadOnlyDictionary<int, List<int>> occupants,
            int forcedFirstNodeId,
            List<int> path,
            List<MovementCandidate> candidates)
        {
            if (stepsLeft == 0)
            {
                return;
            }

            foreach (int next in LegalNextNodes(
                         graph,
                         currentNodeId,
                         previousNodeId,
                         forcedFirstNodeId,
                         path.Count))
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
                    currentNodeId,
                    stepsLeft - 1,
                    occupants,
                    forcedFirstNodeId,
                    path,
                    candidates);

                path.RemoveAt(path.Count - 1);
            }
        }

        private static void WalkPaths(
            MapGraph graph,
            int currentNodeId,
            int previousNodeId,
            int stepsLeft,
            int forcedFirstNodeId,
            List<int> currentPath,
            List<IReadOnlyList<int>> paths)
        {
            if (stepsLeft == 0)
            {
                paths.Add(Array.AsReadOnly(currentPath.ToArray()));
                return;
            }

            foreach (int next in LegalNextNodes(
                         graph,
                         currentNodeId,
                         previousNodeId,
                         forcedFirstNodeId,
                         currentPath.Count))
            {
                currentPath.Add(next);
                WalkPaths(
                    graph,
                    next,
                    currentNodeId,
                    stepsLeft - 1,
                    forcedFirstNodeId,
                    currentPath,
                    paths);
                currentPath.RemoveAt(currentPath.Count - 1);
            }
        }

        /// <summary>
        /// Where a roll is expected to end when it starts with
        /// <paramref name="forcedFirstNodeId"/> and spends every remaining step walking.
        /// The fork buttons use it to light up the tile the player will actually land on
        /// instead of the first tile behind the junction. Later junctions are walked
        /// straight-first (then right, then the only edge) because the highlight cannot ask
        /// the player twice in advance; the roll itself still offers those choices.
        /// </summary>
        public static int PredictLanding(
            MapGraph graph,
            int startNodeId,
            int previousNodeId,
            int steps,
            int forcedFirstNodeId = -1)
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
            int current = startNodeId;
            int previous = previousNodeId;

            for (int step = 0; step < steps; step++)
            {
                IReadOnlyList<int> legal = LegalNextNodes(
                    graph,
                    current,
                    previous,
                    forcedFirstNodeId,
                    step);
                if (legal.Count == 0)
                {
                    break;
                }

                int next = PreferredNext(graph, current, previous, legal);
                previous = current;
                current = next;
            }

            return current;
        }

        /// <summary>Straight first, then a right turn, otherwise whatever is left.</summary>
        private static int PreferredNext(
            MapGraph graph,
            int currentNodeId,
            int previousNodeId,
            IReadOnlyList<int> legal)
        {
            if (legal.Count == 1 || previousNodeId < 0)
            {
                return legal[0];
            }

            MapNode node = graph.GetNode(currentNodeId);
            MapNode previous = graph.GetNode(previousNodeId);
            if (!node.HasGridPosition || !previous.HasGridPosition)
            {
                return legal[0];
            }

            int forwardRow = node.GridRow - previous.GridRow;
            int forwardColumn = node.GridColumn - previous.GridColumn;

            foreach (int edge in legal)
            {
                if (IsStep(graph, node, edge, forwardRow, forwardColumn))
                {
                    return edge;
                }
            }

            foreach (int edge in legal)
            {
                if (IsStep(graph, node, edge, forwardColumn, -forwardRow))
                {
                    return edge;
                }
            }

            return legal[0];
        }

        private static bool IsStep(
            MapGraph graph,
            MapNode node,
            int edge,
            int stepRow,
            int stepColumn)
        {
            MapNode target = graph.GetNode(edge);
            return target.HasGridPosition &&
                   target.GridRow - node.GridRow == stepRow &&
                   target.GridColumn - node.GridColumn == stepColumn;
        }

        /// <summary>
        /// The edges available for one step. On the first step of a move the forced Home
        /// departure (counter-clockwise) is the only choice, so the walk never branches there.
        /// </summary>
        private static IReadOnlyList<int> LegalNextNodes(
            MapGraph graph,
            int currentNodeId,
            int previousNodeId,
            int forcedFirstNodeId,
            int stepIndex)
        {
            if (stepIndex == 0 && forcedFirstNodeId >= 0)
            {
                MapNode origin = graph.GetNode(currentNodeId);
                if (origin.OutEdges.Contains(forcedFirstNodeId))
                {
                    return new[] { forcedFirstNodeId };
                }
            }

            return graph.LegalNextNodes(currentNodeId, previousNodeId);
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
