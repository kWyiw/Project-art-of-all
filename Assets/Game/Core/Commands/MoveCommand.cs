using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectArtOfAll.Core.Commands
{
    /// <summary>
    /// Requests movement toward a specific stopping node.
    /// TargetNodeId is null when the player chooses to continue rather than stop.
    /// FirstForkDirection is the zero-based outgoing-edge index at the first fork.
    /// R-MOVE-003: intermediate tiles are Pass; the endpoint or the chosen
    /// stopping tile is Arrival.
    /// </summary>
    public sealed class MoveCommand : IMatchCommand
    {
        public MoveCommand(
            int playerId,
            int? targetNodeId = null,
            int? firstForkDirection = null,
            IEnumerable<int> forkDirections = null)
        {
            PlayerId = playerId;
            TargetNodeId = targetNodeId;
            FirstForkDirection = firstForkDirection;
            ForkDirections = new ReadOnlyCollection<int>(
                new List<int>(forkDirections ?? Array.Empty<int>()).ToArray());
        }

        public int PlayerId { get; }
        public int? TargetNodeId { get; }
        public int? FirstForkDirection { get; }
        public IReadOnlyList<int> ForkDirections { get; }
    }
}
