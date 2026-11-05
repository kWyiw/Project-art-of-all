using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Cards;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>One of the observing player's own cards. The CPU may see all of these.</summary>
    public sealed class CpuOwnCard
    {
        public CpuOwnCard(CardInstance card, bool inBattle)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            InBattle = inBattle;
        }

        public CardInstance Card { get; }

        public CardDefinition Definition => Card.Definition;

        public int Uid => Card.Uid;

        public bool InBattle { get; }
    }

    /// <summary>
    /// What the CPU may know about another player (R-CPU-002). Deliberately has no field that
    /// could carry a bag card identity, a bag card value or a hidden effect number: the only
    /// bag information is its occupied width and its card count.
    /// </summary>
    public sealed class CpuOpponent
    {
        public CpuOpponent(
            int playerId,
            int level,
            int shards,
            int wins,
            int currentNode,
            NormaType norma,
            int battleWidth,
            int bagWidth,
            int bagCount,
            IEnumerable<CardDefinition> battleCards)
        {
            PlayerId = playerId;
            Level = level;
            Shards = shards;
            Wins = wins;
            CurrentNode = currentNode;
            Norma = norma;
            BattleWidth = battleWidth;
            BagWidth = bagWidth;
            BagCount = bagCount;
            BattleCards = (battleCards ?? Enumerable.Empty<CardDefinition>()).ToArray();
        }

        public int PlayerId { get; }

        public int Level { get; }

        public int Shards { get; }

        public int Wins { get; }

        public int CurrentNode { get; }

        public NormaType Norma { get; }

        public int BattleWidth { get; }

        /// <summary>Occupied bag cells. Public: everyone can see how full a bag is.</summary>
        public int BagWidth { get; }

        /// <summary>Number of cards in the bag. Identities are never exposed.</summary>
        public int BagCount { get; }

        /// <summary>Equipped cards are open information once a battle starts (R-CBT-010).</summary>
        public IReadOnlyList<CardDefinition> BattleCards { get; }

        public string Describe()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("P").Append(PlayerId + 1);
            builder.Append(" Lv.").Append(Level);
            builder.Append(" 碎片 ").Append(Shards);
            builder.Append(" 胜场 ").Append(Wins);
            builder.Append(" 位置 ").Append(CurrentNode);
            builder.Append(" Norma ").Append(Norma);
            builder.Append(" 战斗栏 ").Append(BattleWidth).Append("c");
            builder.Append(" 背包 ").Append(BagWidth).Append("c/").Append(BagCount).Append("张");
            builder.Append(" 战斗栏牌：");
            builder.Append(BattleCards.Count == 0
                ? "空"
                : string.Join("、", BattleCards.Select(card => card.DisplayName).ToArray()));
            return builder.ToString();
        }
    }

    /// <summary>Public tile information, including the boss flag and the monster pool size.</summary>
    public sealed class CpuTile
    {
        public CpuTile(int nodeId, TileType type, int ownerPlayerId, bool isBoss, int monsterPool)
        {
            NodeId = nodeId;
            Type = type;
            OwnerPlayerId = ownerPlayerId;
            IsBoss = isBoss;
            MonsterPool = monsterPool;
        }

        public int NodeId { get; }

        public TileType Type { get; }

        public int OwnerPlayerId { get; }

        public bool IsBoss { get; }

        public int MonsterPool { get; }
    }

    /// <summary>
    /// One way this turn's roll can be spent: either a voluntary early stop (own home or a tile
    /// holding another player) or a full-length landing that walks all remaining steps.
    /// </summary>
    public sealed class CpuRoute
    {
        public CpuRoute(
            int targetNodeId,
            IReadOnlyList<int> path,
            int[] forkDirections,
            bool stops,
            CpuTile landing,
            int occupantPlayerId)
        {
            TargetNodeId = targetNodeId;
            Path = path ?? Array.Empty<int>();
            ForkDirections = forkDirections ?? Array.Empty<int>();
            Stops = stops;
            Landing = landing;
            OccupantPlayerId = occupantPlayerId;
        }

        public int TargetNodeId { get; }

        public IReadOnlyList<int> Path { get; }

        public int[] ForkDirections { get; }

        /// <summary>True when this route ends early on purpose; false walks the whole roll.</summary>
        public bool Stops { get; }

        public CpuTile Landing { get; }

        /// <summary>Player standing on the landing tile, or -1.</summary>
        public int OccupantPlayerId { get; }
    }

    /// <summary>
    /// The CPU's whole view of the match. Built from public state plus the observer's own
    /// cards, which is exactly what a human at this seat knows (R-CPU-002).
    /// </summary>
    public sealed class CpuObservation
    {
        private readonly Dictionary<int, CpuTile> tilesByNode;

        private CpuObservation()
        {
            tilesByNode = new Dictionary<int, CpuTile>();
        }

        public int PlayerId { get; private set; }

        public MatchPhase Phase { get; private set; }

        public int RoundNumber { get; private set; }

        public int TurnNumber { get; private set; }

        public int LastRoll { get; private set; }

        public int Level { get; private set; }

        public int Shards { get; private set; }

        public int Wins { get; private set; }

        public int CurrentNode { get; private set; }

        public int LastArrivalNode { get; private set; }

        public int HomeNode { get; private set; }

        public NormaType Norma { get; private set; }

        public int BattleWidth { get; private set; }

        public int BattleCapacity { get; private set; }

        /// <summary>Total owned cells, which is what the bag capacity limits.</summary>
        public int OwnedWidth { get; private set; }

        /// <summary>Occupied cells of the cards currently sitting in the bag.</summary>
        public int BagWidth { get; private set; }

        public int BagCapacity { get; private set; }

        public int ShardThreshold { get; private set; }

        public int WinThreshold { get; private set; }

        public bool BossEvolutionTriggered { get; private set; }

        public bool ShopRerollUsed { get; private set; }

        public IReadOnlyList<CpuOwnCard> Bag { get; private set; } = Array.Empty<CpuOwnCard>();

        public IReadOnlyList<CpuOwnCard> BattleCards { get; private set; } = Array.Empty<CpuOwnCard>();

        public IReadOnlyList<CpuOpponent> Opponents { get; private set; } = Array.Empty<CpuOpponent>();

        public IReadOnlyList<CpuTile> Tiles { get; private set; } = Array.Empty<CpuTile>();

        public IReadOnlyList<CpuRoute> Routes { get; private set; } = Array.Empty<CpuRoute>();

        public IReadOnlyList<CardDefinition> RewardChoices { get; private set; } = Array.Empty<CardDefinition>();

        public IReadOnlyList<ShopOffer> ShopOffers { get; private set; } = Array.Empty<ShopOffer>();

        public CpuTile TileOf(int nodeId)
        {
            CpuTile tile;
            return tilesByNode.TryGetValue(nodeId, out tile) ? tile : null;
        }

        public CpuOpponent OpponentAt(int nodeId)
        {
            foreach (CpuOpponent opponent in Opponents)
            {
                if (opponent.CurrentNode == nodeId)
                {
                    return opponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Snapshot of everything this observation exposes. The information-set test dumps it and
        /// asserts that no other player's bag card can appear in it.
        /// </summary>
        public string DescribeInformationSet()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("P").Append(PlayerId + 1);
            builder.Append(" phase=").Append(Phase);
            builder.Append(" Lv.").Append(Level);
            builder.Append(" 碎片 ").Append(Shards).Append("/").Append(ShardThreshold);
            builder.Append(" 胜场 ").Append(Wins).Append("/").Append(WinThreshold);
            builder.Append(" Norma ").Append(Norma);
            builder.Append(" 位置 ").Append(CurrentNode);
            builder.Append(" 来路 ").Append(LastArrivalNode);
            builder.Append(" 装配 ").Append(BattleWidth).Append("/").Append(BattleCapacity).Append("c");
            builder.Append(" 背包 ").Append(BagWidth).Append("/").Append(BagCapacity).Append("c");
            builder.AppendLine();

            builder.Append("战斗栏：");
            builder.Append(BattleCards.Count == 0
                ? "空"
                : string.Join("、", BattleCards.Select(card => card.Definition.DisplayName).ToArray()));
            builder.AppendLine();

            builder.Append("背包：");
            builder.Append(Bag.Count == 0
                ? "空"
                : string.Join("、", Bag.Select(card => card.Definition.DisplayName).ToArray()));
            builder.AppendLine();

            foreach (CpuOpponent opponent in Opponents)
            {
                builder.AppendLine(opponent.Describe());
            }

            builder.Append("地块：").Append(Tiles.Count).Append(" 格；路线：").Append(Routes.Count).Append(" 条");
            return builder.ToString();
        }

        public static CpuObservation Capture(MatchController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            MatchState state = controller.State;
            MatchPlayerState self = state.CurrentPlayer;
            MatchGraphReader graphReader = new MatchGraphReader(state.Map, state, controller);

            CpuObservation observation = new CpuObservation
            {
                PlayerId = self.Id,
                Phase = state.Phase,
                RoundNumber = state.RoundNumber,
                TurnNumber = state.TurnNumber,
                LastRoll = state.LastRoll,
                Level = self.Level,
                Shards = self.Shards,
                Wins = self.Wins,
                CurrentNode = self.CurrentNode,
                LastArrivalNode = self.LastArrivalNode,
                HomeNode = self.HomeNode,
                Norma = self.Norma,
                BattleWidth = self.BattleCardWidth,
                BattleCapacity = self.Level >= 6 ? 10 : LoadoutRules.GetBattleCapacity(self.Level),
                OwnedWidth = self.OwnedCardWidth,
                BagWidth = self.OwnedCardWidth - self.BattleCardWidth,
                BagCapacity = LoadoutRules.BagCapacity,
                ShardThreshold = NormaRules.GetThreshold(self.Level, NormaType.Shard),
                WinThreshold = NormaRules.GetThreshold(self.Level, NormaType.Wins),
                BossEvolutionTriggered = state.BossEvolutionTriggered
            };

            List<CpuOwnCard> battle = new List<CpuOwnCard>();
            List<CpuOwnCard> bag = new List<CpuOwnCard>();
            foreach (CardInstance card in self.OwnedCards)
            {
                bool equipped = self.IsEquipped(card);
                (equipped ? battle : bag).Add(new CpuOwnCard(card, equipped));
            }

            observation.BattleCards = battle;
            observation.Bag = bag;

            List<CpuOpponent> opponents = new List<CpuOpponent>();
            foreach (MatchPlayerState player in state.Players)
            {
                if (player.Id == self.Id)
                {
                    continue;
                }

                CpuOpponent opponent = new CpuOpponent(
                    player.Id,
                    player.Level,
                    player.Shards,
                    player.Wins,
                    player.CurrentNode,
                    player.Norma,
                    player.BattleCardWidth,
                    player.OwnedCardWidth - player.BattleCardWidth,
                    player.OwnedCards.Count - player.BattleCards.Count,
                    player.BattleCards.Select(card => card.Definition));
                opponents.Add(opponent);
            }

            observation.Opponents = opponents;

            List<CpuTile> tiles = new List<CpuTile>();
            foreach (MapNode node in state.Map.Nodes)
            {
                CpuTile tile = new CpuTile(
                    node.Id,
                    node.TileType,
                    node.OwnerPlayerId,
                    state.IsBossNode(node.Id),
                    node.TileType == TileType.Monster || node.TileType == TileType.Boss
                        ? state.GetMonsterPool(node.Id)
                        : 0);
                tiles.Add(tile);
                observation.tilesByNode[node.Id] = tile;
            }

            observation.Tiles = tiles;

            if (state.Phase == MatchPhase.AwaitReward)
            {
                observation.RewardChoices = controller.CurrentRewardChoices.ToArray();
            }

            if (state.Phase == MatchPhase.AwaitShop && controller.CurrentShop != null)
            {
                observation.ShopOffers = controller.CurrentShop.Offers.ToArray();
                observation.ShopRerollUsed = controller.CurrentShop.RerollUsed;
            }

            if (state.Phase == MatchPhase.AwaitMove && state.LastRoll > 0)
            {
                observation.Routes = graphReader.BuildRoutes(self);
            }

            return observation;
        }

        /// <summary>Builds the movement options from the public map and the roll.</summary>
        private sealed class MatchGraphReader
        {
            private readonly MapGraph graph;
            private readonly MatchState state;
            private readonly MatchController controller;

            public MatchGraphReader(MapGraph graph, MatchState state, MatchController controller)
            {
                this.graph = graph;
                this.state = state;
                this.controller = controller;
            }

            public IReadOnlyList<CpuRoute> BuildRoutes(MatchPlayerState self)
            {
                List<CpuRoute> routes = new List<CpuRoute>();

                foreach (MovementCandidate candidate in controller.CurrentCandidates)
                {
                    routes.Add(new CpuRoute(
                        candidate.TargetNodeId,
                        candidate.Path,
                        ForkChoicesFor(candidate.Path, self),
                        true,
                        Tile(candidate.TargetNodeId),
                        OccupantAt(candidate.TargetNodeId, self.Id)));
                }

                Dictionary<int, int> occupied = new Dictionary<int, int>();
                foreach (MatchPlayerState player in state.Players)
                {
                    if (player.Id != self.Id)
                    {
                        occupied[player.CurrentNode] = player.Id;
                    }
                }

                foreach (IReadOnlyList<int> path in MovementSolver.FindPaths(
                             graph,
                             self.CurrentNode,
                             self.LastArrivalNode,
                             state.LastRoll))
                {
                    if (path.Count == 0)
                    {
                        continue;
                    }

                    int landing = path[path.Count - 1];
                    int occupant = -1;
                    occupied.TryGetValue(landing, out occupant);
                    routes.Add(new CpuRoute(
                        -1,
                        path,
                        ForkChoicesFor(path, self),
                        false,
                        Tile(landing),
                        occupant));
                }

                return routes;
            }

            /// <summary>
            /// Records one outgoing-edge index for every fork the route passes through, which is
            /// the exact shape MoveCommand.ForkDirections expects.
            /// </summary>
            private int[] ForkChoicesFor(IReadOnlyList<int> path, MatchPlayerState self)
            {
                List<int> choices = new List<int>();
                int current = self.CurrentNode;
                int previous = self.LastArrivalNode;

                foreach (int next in path)
                {
                    IReadOnlyList<int> legal = graph.LegalNextNodes(current, previous);
                    if (legal.Count >= 2)
                    {
                        int index = 0;
                        for (int i = 0; i < legal.Count; i++)
                        {
                            if (legal[i] == next)
                            {
                                index = i;
                                break;
                            }
                        }

                        choices.Add(index);
                    }

                    previous = current;
                    current = next;
                }

                return choices.ToArray();
            }

            private CpuTile Tile(int nodeId)
            {
                MapNode node = graph.GetNode(nodeId);
                return new CpuTile(
                    node.Id,
                    node.TileType,
                    node.OwnerPlayerId,
                    state.IsBossNode(node.Id),
                    node.TileType == TileType.Monster || node.TileType == TileType.Boss
                        ? state.GetMonsterPool(node.Id)
                        : 0);
            }

            private int OccupantAt(int nodeId, int selfId)
            {
                foreach (MatchPlayerState player in state.Players)
                {
                    if (player.Id != selfId && player.CurrentNode == nodeId)
                    {
                        return player.Id;
                    }
                }

                return -1;
            }
        }
    }
}
