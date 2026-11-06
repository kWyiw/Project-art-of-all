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

    /// <summary>One walkable link between two tiles of a generated board.</summary>
    public readonly struct BoardMapEdge
    {
        public BoardMapEdge(int rowA, int columnA, int rowB, int columnB)
        {
            RowA = rowA;
            ColumnA = columnA;
            RowB = rowB;
            ColumnB = columnB;
        }

        public int RowA { get; }

        public int ColumnA { get; }

        public int RowB { get; }

        public int ColumnB { get; }
    }

    /// <summary>Two warp gates of a generated board that teleport into each other.</summary>
    public readonly struct BoardMapTeleport
    {
        public BoardMapTeleport(int rowA, int columnA, int rowB, int columnB)
        {
            RowA = rowA;
            ColumnA = columnA;
            RowB = rowB;
            ColumnB = columnB;
        }

        public int RowA { get; }

        public int ColumnA { get; }

        public int RowB { get; }

        public int ColumnB { get; }
    }

    /// <summary>
    /// A board that is not baked into the scene. The grid convention is the one the map
    /// factory already uses: <see cref="BoardMapCell.Row"/> runs down the −Z axis and
    /// <see cref="BoardMapCell.Column"/> along +X.
    /// </summary>
    public sealed class BoardMapDefinition
    {
        public BoardMapDefinition(
            string id,
            string displayName,
            IReadOnlyList<BoardMapCell> cells,
            float rotationYDegrees,
            IReadOnlyList<BoardMapTeleport> teleports = null,
            IReadOnlyList<BoardMapEdge> edges = null,
            float tileScale = 1f)
        {
            Id = id;
            DisplayName = displayName;
            Cells = cells;
            RotationYDegrees = rotationYDegrees;
            Teleports = teleports ?? new BoardMapTeleport[0];
            Edges = edges ?? new BoardMapEdge[0];
            TileScale = tileScale <= 0f ? 1f : tileScale;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IReadOnlyList<BoardMapCell> Cells { get; }

        /// <summary>The whole board is spun around Y; 45° turns the square loop into a diamond.</summary>
        public float RotationYDegrees { get; }

        /// <summary>Warp gates that link into each other instead of all leading to one shared gate.</summary>
        public IReadOnlyList<BoardMapTeleport> Teleports { get; }

        /// <summary>
        /// Walkable links between tiles. An empty list means "link grid neighbours", which is
        /// what a plain block of tiles wants; shaped boards spell their links out.
        /// </summary>
        public IReadOnlyList<BoardMapEdge> Edges { get; }

        /// <summary>
        /// Multiplier on the scene tile size. A diagonal band of tiles needs slightly larger
        /// plates than a square grid so the band reads as one continuous ribbon.
        /// </summary>
        public float TileScale { get; }
    }

    /// <summary>Board layouts that are generated at runtime (everything except the baked board).</summary>
    public static class BoardMapLibrary
    {
        private static BoardMapDefinition sweetRing;

        public static BoardMapDefinition SweetRing => sweetRing ?? (sweetRing = CreateSweetRing());

        /// <summary>Definition for a generated board, or null when the scene board should be used.</summary>
        public static BoardMapDefinition For(string mapId)
        {
            return mapId == MapCatalog.SweetRingId ? SweetRing : null;
        }

        /// <summary>
        /// The second board, transcribed cell by cell from the drawn sketch: 11 × 9 squares.
        /// H = home, Y = reward, G = shop, R = monster, B = event, P = warp, '.' = empty.
        /// </summary>
        private static readonly string[] SketchRows =
        {
            "....GBY....",
            "....Y.P....",
            "...HRYGH...",
            ".YPG...RYG.",
            ".B.Y...Y.B.",
            ".GYR...GPY.",
            "...HGYRH...",
            "....P.Y....",
            "....YBG....",
        };

        private static BoardMapDefinition CreateSweetRing()
        {
            List<BoardMapCell> cells = new List<BoardMapCell>(36);
            for (int row = 0; row < SketchRows.Length; row++)
            {
                string line = SketchRows[row];
                for (int column = 0; column < line.Length; column++)
                {
                    char code = line[column];
                    if (code == '.')
                    {
                        continue;
                    }

                    cells.Add(new BoardMapCell(
                        row,
                        column,
                        Parse(code),
                        code == 'H' ? HomeOwnerFor(row, column) : -1));
                }
            }

            // Every pair of neighbouring squares is linked; the loops they form keep the board
            // walkable under the corridor rule.
            List<BoardMapEdge> edges = new List<BoardMapEdge>(40);
            for (int row = 0; row < SketchRows.Length; row++)
            {
                string line = SketchRows[row];
                for (int column = 0; column < line.Length; column++)
                {
                    if (line[column] == '.')
                    {
                        continue;
                    }

                    if (column + 1 < line.Length && line[column + 1] != '.')
                    {
                        edges.Add(new BoardMapEdge(row, column, row, column + 1));
                    }

                    if (row + 1 < SketchRows.Length &&
                        column < SketchRows[row + 1].Length &&
                        SketchRows[row + 1][column] != '.')
                    {
                        edges.Add(new BoardMapEdge(row, column, row + 1, column));
                    }
                }
            }

            List<BoardMapTeleport> teleports = new List<BoardMapTeleport>
            {
                new BoardMapTeleport(1, 6, 7, 4),
                new BoardMapTeleport(3, 2, 5, 8)
            };

            return new BoardMapDefinition(
                MapCatalog.SweetRingId,
                "甜点回廊",
                cells,
                0f,
                teleports,
                edges,
                1f);
        }

        /// <summary>The four home squares own one seat each, in the order the sketch numbers them.</summary>
        private static int HomeOwnerFor(int row, int column)
        {
            if (row == 2 && column == 3)
            {
                return 0;
            }

            if (row == 6 && column == 3)
            {
                return 1;
            }

            if (row == 6 && column == 7)
            {
                return 2;
            }

            return 3;
        }

        private static TileType Parse(char code)
        {
            switch (code)
            {
                case 'H':
                    return TileType.Home;
                case 'R':
                    return TileType.Monster;
                case 'G':
                    return TileType.Shop;
                case 'P':
                    return TileType.Warp;
                case 'B':
                    return TileType.Event;
                default:
                    return TileType.Reward;
            }
        }

    }
}
