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
        private readonly HashSet<int> bossNodes = new HashSet<int>();

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
                if (node.TileType == TileType.Monster)
                {
                    monsterPools[node.Id] = 3;
                }
                else if (node.TileType == TileType.Boss)
                {
                    monsterPools[node.Id] = 10;
                    bossNodes.Add(node.Id);
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
        public bool BossEvolutionTriggered { get; internal set; }

        public MatchPlayerState CurrentPlayer => Players[CurrentPlayerIndex];

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
            return monsterPools.TryGetValue(nodeId, out int value) ? value : 0;
        }

        public bool IsBossNode(int nodeId)
        {
            return bossNodes.Contains(nodeId);
        }

        internal int TakeMonsterPool(int nodeId)
        {
            int value = GetMonsterPool(nodeId);
            monsterPools[nodeId] = 0;
            return value;
        }

        internal void EvolveAllMonstersToBosses()
        {
            if (BossEvolutionTriggered)
            {
                return;
            }

            BossEvolutionTriggered = true;
            foreach (MapNode node in Map.Nodes.Where(node => node.TileType == TileType.Monster))
            {
                bossNodes.Add(node.Id);
                monsterPools[node.Id] = 10;
            }
        }

        internal void RestoreMonster(int nodeId)
        {
            bossNodes.Remove(nodeId);
            monsterPools[nodeId] = 3;
        }

        internal void RefillMonsterPools()
        {
            foreach (MapNode node in Map.Nodes)
            {
                if (node.TileType == TileType.Monster || node.TileType == TileType.Boss)
                {
                    if (GetMonsterPool(node.Id) <= 0)
                    {
                        monsterPools[node.Id] = IsBossNode(node.Id) ? 10 : 3;
                    }
                }
            }
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
            hash = StableHash.Add(hash, BossEvolutionTriggered ? 1L : 0L);

            foreach (int nodeId in monsterPools.Keys.OrderBy(id => id))
            {
                hash = StableHash.Add(hash, nodeId);
                hash = StableHash.Add(hash, monsterPools[nodeId]);
            }

            foreach (int nodeId in bossNodes.OrderBy(id => id))
            {
                hash = StableHash.Add(hash, nodeId);
            }

            foreach (MatchPlayerState player in Players)
            {
                hash = player.AddToHash(hash);
            }

            return hash;
        }
    }
}
