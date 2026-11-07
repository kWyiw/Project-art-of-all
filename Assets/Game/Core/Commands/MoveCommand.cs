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
            IEnumerable<int> forkDirections = null,
            bool avoidEncounter = false)
        {
            PlayerId = playerId;
            TargetNodeId = targetNodeId;
            FirstForkDirection = firstForkDirection;
            ForkDirections = new ReadOnlyCollection<int>(
                new List<int>(forkDirections ?? Array.Empty<int>()).ToArray());
            AvoidEncounter = avoidEncounter;
        }

        public int PlayerId { get; }
        public int? TargetNodeId { get; }
        public int? FirstForkDirection { get; }
        public IReadOnlyList<int> ForkDirections { get; }

        /// <summary>
        /// R-MOVE-009：正好停在别人所在的格子上时，玩家可以「避开」——不结算战斗，
        /// 两人同格（R-TILE-009），这一轮照常结束。
        /// </summary>
        public bool AvoidEncounter { get; }
    }
}
