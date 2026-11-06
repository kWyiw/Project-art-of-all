using System.Collections.Generic;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Flow;

namespace ProjectArtOfAll.Board
{
    /// <summary>One placed tile of a generated board.</summary>
    public readonly struct BoardMapCell
    {
        public BoardMapCell(int row, int column, TileType type, int ownerPlayerId = -1)
        {
            Row = row;
            Column = column;
            Type = type;
            OwnerPlayerId = ownerPlayerId;
        }

        public int Row { get; }

        public int Column { get; }

        public TileType Type { get; }

        public int OwnerPlayerId { get; }
    }

    /// <summary>
    /// A board that is not baked into the scene. The grid convention is the one the map
    /// factory already uses: 12 × 12 cells, <see cref="Row"/> down the −Z axis and
    /// <see cref="Column"/> along +X.
    /// </summary>
    public sealed class BoardMapDefinition
    {
        public BoardMapDefinition(
            string id,
            string displayName,
            IReadOnlyList<BoardMapCell> cells,
            float rotationYDegrees)
        {
            Id = id;
            DisplayName = displayName;
            Cells = cells;
            RotationYDegrees = rotationYDegrees;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IReadOnlyList<BoardMapCell> Cells { get; }

        /// <summary>The whole board is spun around Y; 45° turns the square loop into a diamond.</summary>
        public float RotationYDegrees { get; }
    }

    /// <summary>Board layouts that are generated at runtime (everything except the baked board).</summary>
    public static class BoardMapLibrary
    {
        private const int RingSize = 10;

        private static BoardMapDefinition sweetRing;

        public static BoardMapDefinition SweetRing => sweetRing ?? (sweetRing = CreateSweetRing());

        /// <summary>Definition for a generated board, or null when the scene board should be used.</summary>
        public static BoardMapDefinition For(string mapId)
        {
            return mapId == MapCatalog.SweetRingId ? SweetRing : null;
        }

        /// <summary>
        /// A single 10 × 10 loop: four homes on the corners, eight tiles per side, four warp
        /// gates, four event tiles, seven shops, eight monsters and nine rewards.
        /// Every tile has exactly two neighbours, so the counter-clockwise corridor rule
        /// walks the loop without ever asking the player to choose.
        /// </summary>
        private static BoardMapDefinition CreateSweetRing()
        {
            List<BoardMapCell> cells = new List<BoardMapCell>
            {
                new BoardMapCell(1, 1, TileType.Home, 0),
                new BoardMapCell(1, RingSize, TileType.Home, 1),
                new BoardMapCell(RingSize, RingSize, TileType.Home, 2),
                new BoardMapCell(RingSize, 1, TileType.Home, 3)
            };

            // Clockwise from the top-left home: top edge, right edge, bottom edge, left edge.
            string[] top = { "Y", "P", "R", "G", "Y", "R", "B", "G" };
            string[] right = { "G", "Y", "R", "P", "Y", "G", "R", "B" };
            string[] bottom = { "Y", "B", "R", "Y", "G", "P", "R", "Y" };
            string[] left = { "P", "R", "Y", "G", "B", "Y", "R", "G" };

            for (int index = 0; index < top.Length; index++)
            {
                cells.Add(new BoardMapCell(1, 2 + index, Parse(top[index])));
                cells.Add(new BoardMapCell(2 + index, RingSize, Parse(right[index])));
                cells.Add(new BoardMapCell(RingSize, RingSize - 1 - index, Parse(bottom[index])));
                cells.Add(new BoardMapCell(RingSize - 1 - index, 1, Parse(left[index])));
            }

            return new BoardMapDefinition(
                MapCatalog.SweetRingId,
                "甜点回廊",
                cells,
                45f);
        }

        private static TileType Parse(string code)
        {
            switch (code)
            {
                case "Y":
                    return TileType.Reward;
                case "R":
                    return TileType.Monster;
                case "G":
                    return TileType.Shop;
                case "P":
                    return TileType.Warp;
                case "B":
                    return TileType.Event;
                default:
                    return TileType.Neutral;
            }
        }
    }
}
