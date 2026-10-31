using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectArtOfAll.Core.Board
{
    public sealed class MovementCandidate
    {
        public MovementCandidate(int targetNodeId, IEnumerable<int> path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            TargetNodeId = targetNodeId;
            Path = Array.AsReadOnly(new List<int>(path).ToArray());
        }

        public int TargetNodeId { get; }
        public IReadOnlyList<int> Path { get; }
        public int Steps => Path.Count;

        public override string ToString()
        {
            return $"Node {TargetNodeId}: {string.Join(" -> ", Path)}";
        }
    }
}
