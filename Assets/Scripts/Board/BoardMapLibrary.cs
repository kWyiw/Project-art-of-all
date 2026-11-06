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
        /// The board of the reference picture: a diamond band two tiles deep. The outer ring has
        /// 24 tiles, the inner ring 20, and the two rings are joined by one link in each corner so
        /// every side carries a loop. The four home tiles sit at the cardinal tips of the inner
        /// ring, the four warp tiles sit in opposite pairs, and the rest is a mix of rewards,
        /// shops, monsters and events (44 tiles in total).
        /// </summary>
        private static BoardMapDefinition CreateSweetRing()
        {
            // Clockwise from the top tile, six tiles per quadrant.
            string outer = "GYBGPR" + "GYBGPR" + "GYBGPR" + "GYBGPR";
            // Clockwise from the top home, five tiles per quadrant.
            string inner = "HGYGY" + "HGYGY" + "HGYGY" + "HGYGY";

            List<BoardMapCell> cells = new List<BoardMapCell>(44);
            List<Pair> outerRing = BuildDiamondRing(6);
            List<Pair> innerRing = BuildDiamondRing(5);

            for (int index = 0; index < outerRing.Count; index++)
            {
                char code = outer[index % outer.Length];
                cells.Add(new BoardMapCell(
                    outerRing[index].Row,
                    outerRing[index].Column,
                    Parse(code),
                    -1));
            }

            // The four homes sit on the inner ring, one per quadrant, in seat order.
            int homeOwner = 0;
            for (int index = 0; index < innerRing.Count; index++)
            {
                char code = inner[index % inner.Length];
                int owner = -1;
                if (code == 'H')
                {
                    owner = homeOwner;
                    homeOwner = (homeOwner + 1) % LobbyState.SeatCount;
                }

                cells.Add(new BoardMapCell(
                    innerRing[index].Row,
                    innerRing[index].Column,
                    Parse(code),
                    owner));
            }

            List<BoardMapEdge> edges = new List<BoardMapEdge>(48);
            AddRingEdges(edges, outerRing);
            AddRingEdges(edges, innerRing);

            // One link per corner joins the outer ring to the inner ring.
            edges.Add(new BoardMapEdge(2, 8, 3, 8));
            edges.Add(new BoardMapEdge(8, 10, 8, 9));
            edges.Add(new BoardMapEdge(10, 4, 9, 4));
            edges.Add(new BoardMapEdge(4, 2, 4, 3));

            List<BoardMapTeleport> teleports = new List<BoardMapTeleport>
            {
                new BoardMapTeleport(2, 4, 10, 8),
                new BoardMapTeleport(4, 10, 8, 2)
            };

            return new BoardMapDefinition(
                MapCatalog.SweetRingId,
                "甜点回廊",
                cells,
                45f,
                teleports,
                edges,
                1.25f);
        }

        /// <summary>Cells that sit exactly <paramref name="radius"/> steps away from the centre.</summary>
        private static List<Pair> BuildDiamondRing(int radius)
        {
            List<Pair> ring = new List<Pair>(radius * 4);
            int centre = 6;
            for (int row = 0; row < centre * 2 + 1; row++)
            {
                for (int column = 0; column < centre * 2 + 1; column++)
                {
                    int distance = System.Math.Abs(row - centre) + System.Math.Abs(column - centre);
                    if (distance != radius)
                    {
                        continue;
                    }

                    ring.Add(new Pair(row, column));
                }
            }

            // Clockwise from the top: sort by the angle around the centre (screen space, +Y is up).
            ring.Sort((a, b) => AngleOf(a).CompareTo(AngleOf(b)));
            return ring;
        }

        private static double AngleOf(Pair cell)
        {
            const int centre = 6;
            double angle = System.Math.Atan2(cell.Column - centre, centre - cell.Row);
            return angle < 0 ? angle + System.Math.PI * 2.0 : angle;
        }

        private static void AddRingEdges(List<BoardMapEdge> edges, List<Pair> ring)
        {
            for (int index = 0; index < ring.Count; index++)
            {
                Pair a = ring[index];
                Pair b = ring[(index + 1) % ring.Count];
                edges.Add(new BoardMapEdge(a.Row, a.Column, b.Row, b.Column));
            }
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

        private readonly struct Pair
        {
            public Pair(int row, int column)
            {
                Row = row;
                Column = column;
            }

            public int Row { get; }

            public int Column { get; }
        }
    }
}
