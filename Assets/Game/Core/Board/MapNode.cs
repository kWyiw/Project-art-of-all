using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectArtOfAll.Core.Board
{
    public sealed class MapNode
    {
        private readonly ReadOnlyCollection<int> outEdges;

        public MapNode(
            int id,
            TileType tileType,
            int ownerPlayerId = -1,
            params int[] outEdges)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (outEdges == null)
            {
                throw new ArgumentNullException(nameof(outEdges));
            }

            Id = id;
            TileType = tileType;
            OwnerPlayerId = ownerPlayerId;
            this.outEdges = Array.AsReadOnly((int[])outEdges.Clone());
        }

        public int Id { get; }
        public TileType TileType { get; }
        public int OwnerPlayerId { get; }
        public IReadOnlyList<int> OutEdges => outEdges;
        public bool IsFork => outEdges.Count >= 2;
    }
}
