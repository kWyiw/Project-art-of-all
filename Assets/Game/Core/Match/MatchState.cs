using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Util;

namespace ProjectArtOfAll.Core.Match
{
    public sealed class MatchState
    {
        private readonly List<MatchPlayerState> players;
        private readonly Dictionary<int, int> monsterPools =
            new Dictionary<int, int>();
        private readonly List<BoardTrap> traps = new List<BoardTrap>();

        internal MatchState(MapGraph graph, ulong seed)
        {
            Seed = seed;
            Players = new ReadOnlyCollection<MatchPlayerState>(players = new List<MatchPlayerState>());
            Map = graph;
            CurrentPlayerIndex = 0;
            RoundNumber = 1;
            TurnNumber = 1;
            Phase = MatchPhase.AwaitRoll;
            WinnerPlayerId = -1;

            foreach (MapNode node in graph.Nodes)
            {
                if (IsMonsterTile(node.TileType))
                {
                    monsterPools[node.Id] = 3;
                }
            }
        }

        public ulong Seed { get; }
        public MapGraph Map { get; }
        public IReadOnlyList<MatchPlayerState> Players { get; }
        public int CurrentPlayerIndex { get; internal set; }
        public int RoundNumber { get; internal set; }
        public int TurnNumber { get; internal set; }
        public MatchPhase Phase { get; internal set; }
        public int WinnerPlayerId { get; internal set; }
        public int LastRoll { get; internal set; }
        public MatchPlayerState CurrentPlayer => Players[CurrentPlayerIndex];

        /// <summary>
        /// R-MON-001. Late takes precedence: if one player reaches Lv.4 before every
        /// player reaches Lv.2, later monster encounters still use the late roster.
        /// </summary>
        public MonsterPhase MonsterPhase
        {
            get
            {
                if (Players.Any(player => player.Level >= 4))
                {
                    return MonsterPhase.Late;
                }

                return Players.All(player => player.Level >= 2)
                    ? MonsterPhase.Mid
                    : MonsterPhase.Early;
            }
        }

        /// <summary>地块上还生效的陷阱（铁丝网 / 路障），按放置顺序。</summary>
        public IReadOnlyList<BoardTrap> Traps => traps;

        public IReadOnlyList<BoardTrap> TrapsAt(int nodeId)
        {
            List<BoardTrap> found = new List<BoardTrap>();
            foreach (BoardTrap trap in traps)
            {
                if (trap.NodeId == nodeId)
                {
                    found.Add(trap);
                }
            }

            return found;
        }

        internal BoardTrap AddTrap(int nodeId, BoardTrapKind kind, int ownerPlayerId, int charges)
        {
            BoardTrap trap = new BoardTrap(nodeId, kind, ownerPlayerId, charges);
            traps.Add(trap);
            return trap;
        }

        internal void RemoveTrap(BoardTrap trap)
        {
            traps.Remove(trap);
        }

        internal MatchPlayerState AddPlayer(
            string name,
            int homeNode,
            IEnumerable<Cards.CardInstance> cards)
        {
            MatchPlayerState player = new MatchPlayerState(
                players.Count,
                name,
                homeNode,
                homeNode,
                cards);
            players.Add(player);
            return player;
        }

        public IReadOnlyDictionary<int, int> GetPlayerNodeIds()
        {
            return Players.ToDictionary(player => player.Id, player => player.CurrentNode);
        }

        public int GetMonsterPool(int nodeId)
        {
            MapNode node = Map.GetNode(nodeId);
            if (IsMonsterTile(node.TileType) && MonsterPhase == MonsterPhase.Late)
            {
                return 10;
            }

            return monsterPools.TryGetValue(nodeId, out int value) ? value : 0;
        }

        public bool IsBossNode(int nodeId)
        {
            MapNode node = Map.GetNode(nodeId);
            return IsMonsterTile(node.TileType) && MonsterPhase == MonsterPhase.Late;
        }

        internal int TakeMonsterPool(int nodeId)
        {
            if (IsBossNode(nodeId))
            {
                return 10;
            }

            int value = GetMonsterPool(nodeId);
            monsterPools[nodeId] = 0;
            return value;
        }

        internal void RefillMonsterPools()
        {
            foreach (MapNode node in Map.Nodes)
            {
                if (IsMonsterTile(node.TileType))
                {
                    if (!monsterPools.ContainsKey(node.Id))
                    {
                        monsterPools[node.Id] = 3;
                    }
                    else if (monsterPools[node.Id] <= 0)
                    {
                        monsterPools[node.Id] = 3;
                    }
                }
            }
        }

        public static bool IsMonsterTile(TileType type)
        {
            return type == TileType.Monster || type == TileType.Boss;
        }

        public ulong ComputeHash(ulong randomState)
        {
            ulong hash = StableHash.Begin();
            hash = StableHash.Add(hash, Seed);
            hash = StableHash.Add(hash, randomState);
            hash = StableHash.Add(hash, CurrentPlayerIndex);
            hash = StableHash.Add(hash, RoundNumber);
            hash = StableHash.Add(hash, TurnNumber);
            hash = StableHash.Add(hash, (long)Phase);
            hash = StableHash.Add(hash, WinnerPlayerId);
            hash = StableHash.Add(hash, LastRoll);
            hash = StableHash.Add(hash, (long)MonsterPhase);

            foreach (int nodeId in monsterPools.Keys.OrderBy(id => id))
            {
                hash = StableHash.Add(hash, nodeId);
                hash = StableHash.Add(hash, monsterPools[nodeId]);
            }

            foreach (BoardTrap trap in traps
                         .OrderBy(trap => trap.NodeId)
                         .ThenBy(trap => (long)trap.Kind))
            {
                hash = StableHash.Add(hash, trap.NodeId);
                hash = StableHash.Add(hash, (long)trap.Kind);
                hash = StableHash.Add(hash, trap.OwnerPlayerId);
                hash = StableHash.Add(hash, trap.Charges);
            }

            foreach (MatchPlayerState player in Players)
            {
                hash = player.AddToHash(hash);
            }

            return hash;
        }
    }
}
