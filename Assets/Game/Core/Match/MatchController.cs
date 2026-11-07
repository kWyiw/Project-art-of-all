using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Commands;
using ProjectArtOfAll.Core.Events;
using ProjectArtOfAll.Core.Rng;
using ProjectArtOfAll.Core.Util;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// Authoritative board, economy and progression controller.
    /// </summary>
    public sealed class MatchController
    {
        private const int ShopOfferCount = 3;
        private const int RewardChoiceCount = 2;
        /// <summary>每次停留只能重掷一次，重掷花费 2 碎片（规则书 §2.5）。</summary>
        public const int ShopRerollCost = 2;
        private const int GlobalShardGrant = 5;
        private const int GlobalGrantRoundInterval = 3;

        private readonly MapGraph graph;
        private readonly ulong seed;

        /// <summary>本局的根种子（同一种子 + 同一串命令 = 完全一样的一局）。</summary>
        public ulong Seed => seed;

        /// <summary>掷骰与传送落点。</summary>
        private readonly Pcg32 random;

        /// <summary>商店货与重掷。</summary>
        private readonly Pcg32 shopRandom;

        /// <summary>奖励格的两个候选。</summary>
        private readonly Pcg32 rewardRandom;

        /// <summary>每场战斗的种子。</summary>
        private readonly Pcg32 combatRandom;

        // 同一个 seed 配不同的 sequence 就是 PCG 的独立子流（sequence 决定 increment）。
        private const ulong DiceStream = 0x4D41544348UL;
        private const ulong ShopStream = DiceStream + 1UL;
        private const ulong RewardStream = DiceStream + 2UL;
        private const ulong CombatStream = DiceStream + 3UL;
        private readonly GameEventBus eventBus = new GameEventBus();
        private readonly ICardCatalog cardCatalog = CardDatabase.Instance;
        private readonly List<MovementCandidate> currentCandidates =
            new List<MovementCandidate>();
        private readonly List<CardDefinition> currentRewardChoices =
            new List<CardDefinition>();
        private int nextCardUid = 1;

        public MatchController(MapGraph graph, ulong seed, int startingPlayerId = 0)
        {
            this.graph = graph ?? throw new ArgumentNullException(nameof(graph));
            if (graph.PlayerCount != 4)
            {
                throw new ArgumentException(
                    "M1 matches require exactly four seats.",
                    nameof(graph));
            }

            this.seed = seed;
            random = new Pcg32(seed, DiceStream);
            shopRandom = new Pcg32(seed, ShopStream);
            rewardRandom = new Pcg32(seed, RewardStream);
            combatRandom = new Pcg32(seed, CombatStream);
            State = new MatchState(graph, seed);
            State.CurrentPlayerIndex = ((startingPlayerId % 4) + 4) % 4;
            CreatePlayers();

            eventBus.Publish(
                State.TurnNumber,
                new MatchStartedEvent(seed));
            PublishTurnStarted();
        }

        public MatchState State { get; }
        public GameEventBus Events => eventBus;
        public IReadOnlyList<MovementCandidate> CurrentCandidates => currentCandidates;
        public IReadOnlyList<CardDefinition> CurrentRewardChoices =>
            currentRewardChoices;
        public ShopSession CurrentShop { get; private set; }
        public CombatResult LastCombatResult { get; private set; }
        public bool IsFinished => State.Phase == MatchPhase.Finished;

        public void Submit(IMatchCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (IsFinished)
            {
                throw new InvalidOperationException("The match is already finished.");
            }

            switch (command)
            {
                case RollCommand roll:
                    HandleRoll(roll);
                    break;
                case MoveCommand move:
                    HandleMove(move);
                    break;
                case SetNormaCommand norma:
                    HandleNorma(norma);
                    break;
                case ChooseRewardCommand reward:
                    HandleReward(reward);
                    break;
                case ShopActionCommand shop:
                    HandleShopAction(shop);
                    break;
                case SellCardCommand sell:
                    HandleSell(sell);
                    break;
                case MoveCardCommand move:
                    HandleMoveCard(move);
                    break;
                case EquipCardCommand equip:
                    HandleEquip(equip);
                    break;
                case UnequipCardCommand unequip:
                    HandleUnequip(unequip);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Command type {command.GetType().Name} is not supported.");
            }
        }

        public ulong ComputeStateHash()
        {
            // 四种子流的状态都参与哈希：只哈希骰子流时，
            // 商店/奖励/战斗流被意外改成共享会查不出来。
            ulong randomState = StableHash.Begin();
            randomState = StableHash.Add(randomState, random.State);
            randomState = StableHash.Add(randomState, shopRandom.State);
            randomState = StableHash.Add(randomState, rewardRandom.State);
            randomState = StableHash.Add(randomState, combatRandom.State);
            ulong hash = State.ComputeHash(randomState);

            foreach (CardDefinition definition in currentRewardChoices)
            {
                hash = Util.StableHash.Add(hash, definition.Id);
            }

            if (CurrentShop != null)
            {
                hash = Util.StableHash.Add(hash, CurrentShop.RerollUsed ? 1L : 0L);
                foreach (ShopOffer offer in CurrentShop.Offers)
                {
                    hash = Util.StableHash.Add(hash, offer.Definition.Id);
                    hash = Util.StableHash.Add(hash, offer.Price);
                    hash = Util.StableHash.Add(hash, offer.IsAvailable ? 1L : 0L);
                }
            }

            return hash;
        }

        private void HandleRoll(RollCommand command)
        {
            ValidateCurrentPlayer(command.PlayerId);
            if (State.Phase != MatchPhase.AwaitRoll)
            {
                throw new InvalidOperationException(
                    $"RollCommand is invalid during phase {State.Phase}.");
            }

            State.LastRoll = random.NextD6();
            Publish(new DiceRolledEvent(command.PlayerId, State.LastRoll));

            currentCandidates.Clear();
            currentCandidates.AddRange(
                MovementSolver.FindStopCandidates(
                    graph,
                    command.PlayerId,
                    State.CurrentPlayer.CurrentNode,
                    State.CurrentPlayer.LastArrivalNode,
                    State.LastRoll,
                    State.GetPlayerNodeIds(),
                    ForcedFirstStepNodeId()));

            State.Phase = MatchPhase.AwaitMove;
        }

        private void HandleMove(MoveCommand command)
        {
            ValidateCurrentPlayer(command.PlayerId);
            if (State.Phase != MatchPhase.AwaitMove)
            {
                throw new InvalidOperationException(
                    $"MoveCommand is invalid during phase {State.Phase}.");
            }

            MatchPlayerState player = State.CurrentPlayer;
            IReadOnlyList<int> path = SelectPath(command);
            player.HasMoved = true;
            int fromNode = player.CurrentNode;

            for (int stepIndex = 0; stepIndex < path.Count; stepIndex++)
            {
                int nextNode = path[stepIndex];
                MovementVisitKind visitKind =
                    stepIndex == path.Count - 1
                        ? MovementVisitKind.Arrival
                        : MovementVisitKind.Pass;
                Publish(
                    new PlayerMovedEvent(
                        player.Id,
                        fromNode,
                        nextNode,
                        stepIndex,
                        visitKind));
                player.CurrentNode = nextNode;
                player.LastArrivalNode = fromNode;
                fromNode = nextNode;
            }

            if (command.TargetNodeId.HasValue)
            {
                ResolvePlayerEncounter(player, player.CurrentNode);
            }

            bool isWarpDestination = false;
            int arrivalNode = player.CurrentNode;
            MapNode arrival = graph.GetNode(arrivalNode);
            if (arrival.TileType == TileType.Warp)
            {
                int warpTarget = SelectWarpTarget(arrivalNode);
                if (warpTarget != arrivalNode)
                {
                    Publish(
                        new PlayerTeleportedEvent(
                            player.Id,
                            arrivalNode,
                            warpTarget));
                    player.CurrentNode = warpTarget;
                    player.LastArrivalNode = -1;
                    arrivalNode = warpTarget;
                    isWarpDestination = true;
                    arrival = graph.GetNode(arrivalNode);
                }
            }

            ResolveArrivalEffect(player, arrival, isWarpDestination);

            if (State.Phase != MatchPhase.AwaitNorma &&
                State.Phase != MatchPhase.AwaitReward &&
                State.Phase != MatchPhase.AwaitShop &&
                State.Phase != MatchPhase.Finished)
            {
                EndCurrentTurn();
            }
        }

        private void HandleNorma(SetNormaCommand command)
        {
            ValidateCurrentPlayer(command.PlayerId);
            if (State.Phase != MatchPhase.AwaitNorma)
            {
                throw new InvalidOperationException(
                    $"SetNormaCommand is invalid during phase {State.Phase}.");
            }

            State.CurrentPlayer.Norma = command.Norma;
            EndCurrentTurn();
        }

        private void HandleReward(ChooseRewardCommand command)
        {
            ValidateCurrentPlayer(command.PlayerId);
            if (State.Phase != MatchPhase.AwaitReward)
            {
                throw new InvalidOperationException(
                    $"ChooseRewardCommand is invalid during phase {State.Phase}.");
            }

            if (command.ChoiceIndex < 0 ||
                command.ChoiceIndex >= currentRewardChoices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(command.ChoiceIndex));
            }

            CardDefinition definition = currentRewardChoices[command.ChoiceIndex];
            CardInstance card = new CardInstance(nextCardUid++, definition);
            if (!State.CurrentPlayer.TryAddCard(card, equipIfPossible: true))
            {
                throw new InvalidOperationException(
                    "The selected reward does not fit in the player's Bag.");
            }

            Publish(
                new CardAddedEvent(
                    State.CurrentPlayer.Id,
                    card.Uid,
                    definition.Id,
                    "Reward"));
            currentRewardChoices.Clear();
            EndCurrentTurn();
        }

        private void HandleShopAction(ShopActionCommand command)
        {
            ValidateCurrentPlayer(command.PlayerId);
            if (State.Phase != MatchPhase.AwaitShop || CurrentShop == null)
            {
                throw new InvalidOperationException(
                    $"ShopActionCommand is invalid during phase {State.Phase}.");
            }

            MatchPlayerState player = State.CurrentPlayer;
            switch (command.Action)
            {
                case ShopActionType.Buy:
                    BuyShopOffer(player, command.OfferIndex);
                    break;
                case ShopActionType.Reroll:
                    RerollShop(player);
                    break;
                case ShopActionType.Leave:
                    CurrentShop = null;
                    EndCurrentTurn();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command.Action));
            }
        }

        private void HandleSell(SellCardCommand command)
        {
            MatchPlayerState actor = ResolveFreeActionPlayer(command.PlayerId);
            if (!actor.TrySellCard(command.CardUid, out CardInstance card))
            {
                throw new InvalidOperationException(
                    $"Player {command.PlayerId} does not own card {command.CardUid}.");
            }

            // R-ECO-005：出售按这张牌的当前价值结算（幸运饼干这类会成长）。
            int refund = card.Value;
            AddShards(actor, refund);
            Publish(
                new CardSoldEvent(
                    actor.Id,
                    card.Uid,
                    card.Definition.Id,
                    refund));
        }

        private void HandleMoveCard(MoveCardCommand command)
        {
            MatchPlayerState actor = ResolveFreeActionPlayer(command.PlayerId);
            CardInstance card = actor.FindCard(command.CardUid);
            if (card == null)
            {
                throw new InvalidOperationException(
                    $"Player {command.PlayerId} does not own card {command.CardUid}.");
            }

            if (!actor.TryMoveCard(command.CardUid, command.ToBattle, command.Column))
            {
                throw new InvalidOperationException(
                    "The card does not fit in that slot of the current row.");
            }

            Publish(
                new CardMovedEvent(
                    actor.Id,
                    card.Uid,
                    card.Definition.Id,
                    command.ToBattle,
                    actor.ColumnOf(card.Uid)));
        }

        private void HandleEquip(EquipCardCommand command)
        {
            MatchPlayerState actor = ResolveFreeActionPlayer(command.PlayerId);
            CardInstance card = actor.FindCard(command.CardUid);
            if (card == null)
            {
                throw new InvalidOperationException(
                    $"Player {command.PlayerId} does not own card {command.CardUid}.");
            }

            if (!actor.TryEquipCard(command.CardUid))
            {
                throw new InvalidOperationException(
                    "The card cannot be equipped at the current battle capacity.");
            }

            Publish(new CardEquippedEvent(actor.Id, card.Uid, true));
        }

        private void HandleUnequip(UnequipCardCommand command)
        {
            MatchPlayerState actor = ResolveFreeActionPlayer(command.PlayerId);
            if (!actor.TryUnequipCard(command.CardUid))
            {
                throw new InvalidOperationException(
                    $"Card {command.CardUid} is not currently equipped.");
            }

            Publish(new CardEquippedEvent(actor.Id, command.CardUid, false));
        }

        /// <summary>
        /// The node the current player must step to when leaving Home on the opening move,
        /// or -1 when the forced counter-clockwise rule does not apply (R-MAP-002).
        /// </summary>
        public int ForcedFirstStepNodeId()
        {
            MatchPlayerState player = State.CurrentPlayer;
            if (player == null || player.HasMoved || player.CurrentNode != player.HomeNode)
            {
                return -1;
            }

            return graph.CounterClockwiseNeighbour(player.CurrentNode);
        }

        /// <summary>
        /// Exits the current player may take from <paramref name="nodeId"/>, honouring the
        /// forced counter-clockwise first step away from Home.
        /// </summary>
        public IReadOnlyList<int> LegalNextNodes(int nodeId, int previousNodeId)
        {
            int forced = ForcedFirstStepNodeId();
            if (forced >= 0 && State.CurrentPlayer != null &&
                nodeId == State.CurrentPlayer.CurrentNode)
            {
                return new[] { forced };
            }

            return graph.LegalNextNodes(nodeId, previousNodeId);
        }

        private IReadOnlyList<int> SelectPath(MoveCommand command)
        {
            IReadOnlyList<IReadOnlyList<int>> paths;

            if (command.TargetNodeId.HasValue)
            {
                paths = currentCandidates
                    .Where(candidate => candidate.TargetNodeId == command.TargetNodeId.Value)
                    .Select(candidate => candidate.Path)
                    .ToArray();
            }
            else
            {
                paths = MovementSolver.FindPaths(
                    graph,
                    State.CurrentPlayer.CurrentNode,
                    State.CurrentPlayer.LastArrivalNode,
                    State.LastRoll,
                    ForcedFirstStepNodeId());
            }

            if (command.ForkDirections.Count > 0)
            {
                paths = paths
                    .Where(path => MatchesForkChoices(path, command.ForkDirections))
                    .ToArray();
            }
            else if (command.FirstForkDirection.HasValue)
            {
                paths = paths
                    .Where(path => MatchesFirstForkChoice(
                        path,
                        command.FirstForkDirection.Value))
                    .ToArray();
            }

            IReadOnlyList<int> selected = paths.FirstOrDefault();
            if (selected == null)
            {
                throw new InvalidOperationException(
                    "The requested movement target or fork direction is not reachable.");
            }

            return selected;
        }

        private bool MatchesForkChoices(
            IReadOnlyList<int> path,
            IReadOnlyList<int> forkDirections)
        {
            int current = State.CurrentPlayer.CurrentNode;
            int previous = State.CurrentPlayer.LastArrivalNode;
            int choiceIndex = 0;

            foreach (int next in path)
            {
                IReadOnlyList<int> legal = LegalNextNodes(current, previous);
                if (legal.Count >= 2)
                {
                    if (choiceIndex >= forkDirections.Count)
                    {
                        return false;
                    }

                    int selectedEdge = -1;
                    for (int edgeIndex = 0; edgeIndex < legal.Count; edgeIndex++)
                    {
                        if (legal[edgeIndex] == next)
                        {
                            selectedEdge = edgeIndex;
                            break;
                        }
                    }

                    if (selectedEdge != forkDirections[choiceIndex])
                    {
                        return false;
                    }

                    choiceIndex++;
                }

                previous = current;
                current = next;
            }

            return choiceIndex == forkDirections.Count;
        }

        private bool MatchesFirstForkChoice(
            IReadOnlyList<int> path,
            int outgoingEdgeIndex)
        {
            int current = State.CurrentPlayer.CurrentNode;
            int previous = State.CurrentPlayer.LastArrivalNode;
            foreach (int next in path)
            {
                IReadOnlyList<int> legal = LegalNextNodes(current, previous);
                if (legal.Count >= 2)
                {
                    for (int edgeIndex = 0; edgeIndex < legal.Count; edgeIndex++)
                    {
                        if (legal[edgeIndex] == next)
                        {
                            return edgeIndex == outgoingEdgeIndex;
                        }
                    }
                }

                previous = current;
                current = next;
            }

            return true;
        }

        private void ResolveArrivalEffect(
            MatchPlayerState player,
            MapNode arrival,
            bool isWarpDestination)
        {
            switch (arrival.TileType)
            {
                case TileType.Home:
                    // R-HOME-005: only a normal movement Arrival upgrades Home.
                    if (!isWarpDestination)
                    {
                        TryUpgrade(player);
                    }

                    break;
                case TileType.Reward:
                    BeginReward(player);
                    break;
                case TileType.Monster:
                    ResolveMonster(
                        player,
                        arrival,
                        boss: State.IsBossNode(arrival.Id));
                    TryUpgrade(player);
                    break;
                case TileType.Boss:
                    ResolveMonster(player, arrival, boss: true);
                    TryUpgrade(player);
                    break;
                case TileType.Shop:
                    BeginShop(player);
                    break;
                case TileType.Neutral:
                case TileType.Event:
                case TileType.Warp:
                    break;
            }
        }

        private void BeginReward(MatchPlayerState player)
        {
            currentRewardChoices.Clear();
            currentRewardChoices.AddRange(
                CreateRandomDefinitions(RewardChoiceCount, rewardRandom));

            bool canTakeAtLeastOne = currentRewardChoices.Any(
                definition =>
                    player.OwnedCardWidth + definition.SlotSize <=
                    LoadoutRules.BagCapacity);
            if (!canTakeAtLeastOne)
            {
                currentRewardChoices.Clear();
                Publish(new RewardFailedEvent(player.Id, "Bag is full."));
                return;
            }

            State.Phase = MatchPhase.AwaitReward;
        }

        private void BeginShop(MatchPlayerState player)
        {
            currentRewardChoices.Clear();
            CurrentShop = new ShopSession(CreateShopOffers());
            State.Phase = MatchPhase.AwaitShop;
        }

        private void BuyShopOffer(MatchPlayerState player, int offerIndex)
        {
            if (offerIndex < 0 || offerIndex >= CurrentShop.Offers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(offerIndex));
            }

            ShopOffer offer = CurrentShop.Offers[offerIndex];
            if (!offer.IsAvailable)
            {
                throw new InvalidOperationException("That shop offer was already purchased.");
            }

            if (player.Shards < offer.Price)
            {
                throw new InvalidOperationException("Not enough Shards.");
            }

            CardInstance card = new CardInstance(
                nextCardUid++,
                offer.Definition);
            if (!player.TryAddCard(card, equipIfPossible: true))
            {
                throw new InvalidOperationException(
                    "The card does not fit in the player's Bag.");
            }

            AddShards(player, -offer.Price);
            offer.IsAvailable = false;
            Publish(
                new CardAddedEvent(
                    player.Id,
                    card.Uid,
                    offer.Definition.Id,
                    "Shop"));
        }

        private void RerollShop(MatchPlayerState player)
        {
            if (CurrentShop.RerollUsed)
            {
                throw new InvalidOperationException(
                    "This shop visit has already been rerolled.");
            }

            if (player.Shards < ShopRerollCost)
            {
                throw new InvalidOperationException("Not enough Shards to reroll.");
            }

            AddShards(player, -ShopRerollCost);
            CurrentShop.ReplaceOffers(CreateShopOffers());
            CurrentShop.RerollUsed = true;
        }

        private IReadOnlyList<ShopOffer> CreateShopOffers()
        {
            return CreateRandomDefinitions(ShopOfferCount, shopRandom)
                .Select(definition => new ShopOffer(
                    definition,
                    GetShopPrice(definition.SlotSize)))
                .ToArray();
        }

        private IReadOnlyList<CardDefinition> CreateRandomDefinitions(
            int count,
            Pcg32 source)
        {
            List<CardDefinition> pool = cardCatalog.All.ToList();
            Shuffle(pool, source);
            return pool.Take(Math.Min(count, pool.Count)).ToArray();
        }

        /// <summary>Fisher-Yates，随机源由调用方给，互不干扰。</summary>
        private static void Shuffle<T>(IList<T> items, Pcg32 source)
        {
            for (int index = items.Count - 1; index > 0; index--)
            {
                int swapIndex = source.NextInt(index + 1);
                T temporary = items[index];
                items[index] = items[swapIndex];
                items[swapIndex] = temporary;
            }
        }

        private static int GetShopPrice(int slotSize)
        {
            switch (slotSize)
            {
                case 1:
                    return 2;
                case 2:
                    return 4;
                case 3:
                    return 8;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slotSize));
            }
        }

        private void ResolvePlayerEncounter(
            MatchPlayerState attacker,
            int arrivalNode)
        {
            MatchPlayerState defender = State.Players
                .Where(player => player.Id != attacker.Id &&
                                 player.CurrentNode == arrivalNode)
                .OrderBy(player => player.Id)
                .FirstOrDefault();
            if (defender == null)
            {
                return;
            }

            CombatResult result = RunCombat(
                attacker,
                defender,
                attacker.Level,
                defender.Level);
            Publish(new CombatResolvedEvent(attacker.Id, defender.Id, result));

            if (result.IsDraw)
            {
                return;
            }

            MatchPlayerState winner = result.WinnerPlayerId == attacker.Id
                ? attacker
                : defender;
            MatchPlayerState loser = winner == attacker ? defender : attacker;
            int stolen = Math.Min(15, loser.Shards / 2);

            SetShards(loser, loser.Shards - stolen);
            AddShards(winner, stolen);
            AddWins(winner, 2);
        }

        private void ResolveMonster(
            MatchPlayerState player,
            MapNode arrival,
            bool boss)
        {
            List<CardInstance> enemyCards = CreateEnemyDeck(
                player.Level,
                5000 + arrival.Id * 10);
            int enemyId = 1000 + arrival.Id;
            int enemyLevel = Math.Min(5, boss ? player.Level + 1 : player.Level);

            CombatResult result = RunCombat(
                player,
                enemyId,
                player.Level,
                enemyLevel,
                enemyCards);
            Publish(new CombatResolvedEvent(player.Id, enemyId, result));

            if (result.WinnerPlayerId == player.Id)
            {
                int poolReward = State.TakeMonsterPool(arrival.Id);
                AddShards(player, poolReward);
                AddWins(player, boss ? 3 : 1);

                if (boss)
                {
                    // R-TILE-004：Boss 被击败后**所有** BOSS 格恢复为怪物格。
                    State.RestoreAllBosses();
                }
            }
            else if (!result.IsDraw)
            {
                int loss = player.Shards / 2;
                SetShards(player, player.Shards - loss);
            }
        }

        private CombatResult RunCombat(
            MatchPlayerState left,
            MatchPlayerState right,
            int leftLevel,
            int rightLevel)
        {
            return RunCombat(
                left,
                right.Id,
                leftLevel,
                rightLevel,
                right.BattleCards);
        }

        private CombatResult RunCombat(
            MatchPlayerState left,
            int rightPlayerId,
            int leftLevel,
            int rightLevel,
            IEnumerable<CardInstance> rightCards)
        {
            CombatantSnapshot leftSnapshot = new CombatantSnapshot(
                left.Id,
                leftLevel,
                left.BattleSlots);
            CombatantSnapshot rightSnapshot = new CombatantSnapshot(
                rightPlayerId,
                rightLevel,
                rightCards);
            BattleSnapshot battle = new BattleSnapshot(
                leftSnapshot,
                rightSnapshot);
            CombatSimulation simulation = new CombatSimulation(
                battle,
                NextCombatSeed());
            CombatResult result = simulation.RunToCompletion();
            LastCombatResult = result;
            return result;
        }

        private ulong NextCombatSeed()
        {
            ulong high = combatRandom.NextUInt32();
            ulong low = combatRandom.NextUInt32();
            return (high << 32) | low;
        }

        /// <summary>
        /// R-TILE-008：目标从全图其他所有传送格中随机取，排除当前格。
        /// 只有 1 个传送格时不传送（地图校验要求至少 2 个）。
        /// </summary>
        private int SelectWarpTarget(int sourceNodeId)
        {
            int[] candidates = graph.Nodes
                .Where(node => node.TileType == TileType.Warp)
                .Select(node => node.Id)
                .Where(nodeId => nodeId != sourceNodeId)
                .OrderBy(nodeId => nodeId)
                .ToArray();

            return candidates.Length == 0
                ? sourceNodeId
                : candidates[random.NextInt(candidates.Length)];
        }

        private void TryUpgrade(MatchPlayerState player)
        {
            if (player.Level >= 6 || !player.MeetsCurrentNorma())
            {
                return;
            }

            player.Level++;
            Publish(new PlayerLeveledUpEvent(player.Id, player.Level));

            if (player.Level == 4 && !State.BossEvolutionTriggered)
            {
                State.EvolveAllMonstersToBosses();
                Publish(new BossEvolutionEvent(player.Id));
            }

            if (player.Level >= 6)
            {
                State.WinnerPlayerId = player.Id;
                State.Phase = MatchPhase.Finished;
                Publish(new MatchFinishedEvent(player.Id));
                return;
            }

            State.Phase = MatchPhase.AwaitNorma;
        }

        private void EndCurrentTurn()
        {
            MatchPlayerState endingPlayer = State.CurrentPlayer;
            Publish(new TurnEndedEvent(endingPlayer.Id, State.TurnNumber));

            State.CurrentPlayerIndex =
                (State.CurrentPlayerIndex + 1) % State.Players.Count;
            if (State.CurrentPlayerIndex == 0)
            {
                State.RoundNumber++;
                if ((State.RoundNumber - 1) % GlobalGrantRoundInterval == 0)
                {
                    Publish(new GlobalShardsGrantedEvent(GlobalShardGrant));
                    foreach (MatchPlayerState player in State.Players)
                    {
                        AddShards(player, GlobalShardGrant);
                    }

                    State.RefillMonsterPools();
                }
            }

            State.TurnNumber++;
            State.Phase = MatchPhase.AwaitRoll;
            currentCandidates.Clear();
            currentRewardChoices.Clear();
            CurrentShop = null;
            PublishTurnStarted();
        }

        private void PublishTurnStarted()
        {
            Publish(
                new TurnStartedEvent(
                    State.CurrentPlayer.Id,
                    State.RoundNumber,
                    State.TurnNumber));
        }

        private void Publish(GameEvent gameEvent)
        {
            eventBus.Publish(State.TurnNumber, gameEvent);
        }

        private void AddShards(MatchPlayerState player, int amount)
        {
            if (amount == 0)
            {
                return;
            }

            int oldValue = player.Shards;
            player.Shards = Math.Max(0, player.Shards + amount);
            int delta = player.Shards - oldValue;
            if (delta != 0)
            {
                Publish(new ShardsChangedEvent(player.Id, delta, player.Shards));
            }
        }

        private void SetShards(MatchPlayerState player, int amount)
        {
            AddShards(player, Math.Max(0, amount) - player.Shards);
        }

        private void AddWins(MatchPlayerState player, int amount)
        {
            if (amount == 0)
            {
                return;
            }

            player.Wins = Math.Max(0, player.Wins + amount);
            Publish(new WinsChangedEvent(player.Id, amount, player.Wins));
        }

        /// <summary>
        /// R-TURN-002：整理卡牌、出售、装备这些免费动作不属于"回合行动"，任何座位在
        /// 对局进行中都可以随时提交——包括正在等别人掷骰的时候（本地玩家要能在对手
        /// 回合里调整自己的装配）。
        /// </summary>
        private MatchPlayerState ResolveFreeActionPlayer(int playerId)
        {
            if (playerId < 0 || playerId >= State.Players.Count)
            {
                throw new InvalidOperationException(
                    $"Player {playerId} is not seated.");
            }

            return State.Players[playerId];
        }

        private void ValidateCurrentPlayer(int playerId)
        {
            if (State.CurrentPlayer.Id != playerId)
            {
                throw new InvalidOperationException(
                    $"Player {playerId} is not the active seat.");
            }
        }

        private void CreatePlayers()
        {
            List<MapNode> homes = graph.Nodes
                .Where(node => node.TileType == TileType.Home)
                .OrderBy(node => node.OwnerPlayerId)
                .ToList();

            foreach (MapNode home in homes)
            {
                State.AddPlayer(
                    $"Player {home.OwnerPlayerId + 1}",
                    home.Id,
                    CreateStarterDeck());
            }
        }

        private List<CardInstance> CreateStarterDeck()
        {
            return new List<CardInstance>
            {
                new CardInstance(nextCardUid++, cardCatalog.Get("crowbar")),
                new CardInstance(nextCardUid++, cardCatalog.Get("nuclear_water")),
                new CardInstance(nextCardUid++, cardCatalog.Get("hardtack"))
            };
        }

        internal List<CardInstance> CreateEnemyDeck(int level, int uidStart)
        {
            List<CardInstance> cards = new List<CardInstance>
            {
                new CardInstance(uidStart, cardCatalog.Get("crowbar")),
                new CardInstance(uidStart + 1, cardCatalog.Get("lighter")),
                new CardInstance(uidStart + 2, cardCatalog.Get("hardtack"))
            };

            if (level >= 2)
            {
                cards.Add(new CardInstance(uidStart + 3, cardCatalog.Get("car_door")));
            }

            return cards;
        }
    }

    public sealed class MatchFinishedEvent : GameEvent
    {
        public MatchFinishedEvent(int winnerPlayerId)
        {
            WinnerPlayerId = winnerPlayerId;
        }

        public int WinnerPlayerId { get; }
    }
}
