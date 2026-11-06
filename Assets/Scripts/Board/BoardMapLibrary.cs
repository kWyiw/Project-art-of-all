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
    /// factory already uses: 12 × 12 cells, <see cref="BoardMapCell.Row"/> down the −Z axis and
    /// <see cref="BoardMapCell.Column"/> along +X.
    /// </summary>
    public sealed class BoardMapDefinition
    {
        public BoardMapDefinition(
            string id,
            string displayName,
            IReadOnlyList<BoardMapCell> cells,
            float rotationYDegrees,
            IReadOnlyList<BoardMapTeleport> teleports = null)
        {
            Id = id;
            DisplayName = displayName;
            Cells = cells;
            RotationYDegrees = rotationYDegrees;
            Teleports = teleports ?? new BoardMapTeleport[0];
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IReadOnlyList<BoardMapCell> Cells { get; }

        /// <summary>The whole board is spun around Y; 45° turns the square loop into a diamond.</summary>
        public float RotationYDegrees { get; }

        /// <summary>Warp gates that link into each other instead of all leading to one shared gate.</summary>
        public IReadOnlyList<BoardMapTeleport> Teleports { get; }
    }

    /// <summary>Board layouts that are generated at runtime (everything except the baked board).</summary>
    public static class BoardMapLibrary
    {
        private const int GridSize = 12;

        /// <summary>
        /// Outer ring, clockwise from the top-left home. One character per tile:
        /// h = home, Y = reward, R = monster, G = shop, P = warp, B = event.
        /// </summary>
        private const string RingPattern = "hYGYBGYGPYGhYRYRBYGPYRhYGPYRYRBYGhYRBYGPYRYR";

        /// <summary>Column of the first tile of the top pocket; the other three are rotated copies.</summary>
        private const int PocketColumn = 5;

        private static BoardMapDefinition sweetRing;

        public static BoardMapDefinition SweetRing => sweetRing ?? (sweetRing = CreateSweetRing());

        /// <summary>Definition for a generated board, or null when the scene board should be used.</summary>
        public static BoardMapDefinition For(string mapId)
        {
            return mapId == MapCatalog.SweetRingId ? SweetRing : null;
        }

        /// <summary>
        /// A diamond ring with a small loop hanging off each side, built the way the reference
        /// board looks: four homes on the corners of the diamond, four pockets that bulge inwards,
        /// four warp gates in two opposing pairs, four event tiles, and a mix of rewards, shops
        /// and monsters everywhere else (68 tiles in total).
        /// </summary>
        private static BoardMapDefinition CreateSweetRing()
        {
            List<BoardMapCell> cells = new List<BoardMapCell>(68);

            List<KeyValuePair<int, int>> ring = BuildRing();
            for (int index = 0; index < ring.Count; index++)
            {
                char code = index < RingPattern.Length ? RingPattern[index] : 'Y';
                int owner = HomeOwnerOfRingIndex(index);
                cells.Add(new BoardMapCell(
                    ring[index].Key,
                    ring[index].Value,
                    Parse(code.ToString()),
                    owner));
            }

            // Pocket: two link tiles hanging off the ring, then a 2 × 2 block that closes the loop.
            List<KeyValuePair<int, int>> pocket = new List<KeyValuePair<int, int>>
            {
                Pos(1, PocketColumn),
                Pos(1, PocketColumn + 1),
                Pos(2, PocketColumn),
                Pos(2, PocketColumn + 1),
                Pos(3, PocketColumn),
                Pos(3, PocketColumn + 1)
            };
            string pocketCodes = "GRYGRY";

            for (int side = 0; side < 4; side++)
            {
                for (int index = 0; index < pocket.Count; index++)
                {
                    KeyValuePair<int, int> cell = pocket[index];
                    cells.Add(new BoardMapCell(cell.Key, cell.Value, Parse(pocketCodes[index].ToString())));
                }

                pocket = pocket.ConvertAll(cell => Rotate90(cell.Key, cell.Value));
            }

            List<BoardMapTeleport> teleports = new List<BoardMapTeleport>
            {
                new BoardMapTeleport(0, 8, GridSize - 1, 8),
                new BoardMapTeleport(8, GridSize - 1, 5, 0)
            };

            return new BoardMapDefinition(
                MapCatalog.SweetRingId,
                "甜点回廊",
                cells,
                45f,
                teleports);
        }

        /// <summary>The 44 ring positions, walked clockwise from the top-left corner.</summary>
        private static List<KeyValuePair<int, int>> BuildRing()
        {
            List<KeyValuePair<int, int>> ring = new List<KeyValuePair<int, int>>(44);
            for (int column = 0; column < GridSize; column++)
            {
                ring.Add(Pos(0, column));
            }

            for (int row = 1; row < GridSize; row++)
            {
                ring.Add(Pos(row, GridSize - 1));
            }

            for (int column = GridSize - 2; column >= 0; column--)
            {
                ring.Add(Pos(GridSize - 1, column));
            }

            for (int row = GridSize - 2; row >= 1; row--)
            {
                ring.Add(Pos(row, 0));
            }

            return ring;
        }

        private static int HomeOwnerOfRingIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return 0;
                case 11:
                    return 1;
                case 22:
                    return 2;
                case 33:
                    return 3;
                default:
                    return -1;
            }
        }

        private static KeyValuePair<int, int> Pos(int row, int column)
        {
            return new KeyValuePair<int, int>(row, column);
        }

        /// <summary>Quarter turn of a cell around the centre of the 12 × 12 grid.</summary>
        private static KeyValuePair<int, int> Rotate90(int row, int column)
        {
            return Pos(column, GridSize - 1 - row);
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
                case "h":
                    return TileType.Home;
                default:
                    return TileType.Neutral;
            }
        }
    }
}
