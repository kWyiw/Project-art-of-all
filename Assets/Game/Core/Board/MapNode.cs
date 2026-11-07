using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectArtOfAll.Core.Board
{
    /// <summary>Row / column of a tile on the board grid (row grows downwards).</summary>
    public readonly struct MapCell
    {
        public MapCell(int row, int column)
        {
            Row = row;
            Column = column;
        }

        public int Row { get; }
        public int Column { get; }
    }

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

        /// <summary>
        /// Node on a rectangular grid. The grid position is only used to decide which
        /// way is "straight" and which way is "counter-clockwise" (R-MAP-002); -1 means
        /// the node has no grid position and direction checks fall back to "any edge
        /// except the way you came".
        /// </summary>
        public MapNode(
            int id,
            TileType tileType,
            int ownerPlayerId,
            MapCell cell,
            params int[] outEdges)
            : this(id, tileType, ownerPlayerId, outEdges)
        {
            Cell = cell;
            GridRow = cell.Row;
            GridColumn = cell.Column;
        }

        /// <summary>Grid node that never offers a choice: it may only be walked straight through.</summary>
        public MapNode(
            int id,
            TileType tileType,
            int ownerPlayerId,
            MapCell cell,
            bool forceStraight,
            params int[] outEdges)
            : this(id, tileType, ownerPlayerId, cell, outEdges)
        {
            ForceStraight = forceStraight;
        }

        public int Id { get; }
        public TileType TileType { get; }
        public int OwnerPlayerId { get; }
        public IReadOnlyList<int> OutEdges => outEdges;
        public int GridRow { get; } = -1;
        public int GridColumn { get; } = -1;
        public bool HasGridPosition => GridRow >= 0 && GridColumn >= 0;
        public MapCell Cell { get; }

        /// <summary>
        /// 这一格不算岔路口：即使它有三条连线，走到这里也只能继续直行。
        /// 用来掐掉那些"右拐会把人带回顺时针"的格子（R-MAP-002）。
        /// </summary>
        public bool ForceStraight { get; }

        public bool IsFork => !ForceStraight && outEdges.Count >= 2;
    }
}
