using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Rng;

namespace ProjectArtOfAll.Core.Match
{
    public enum CpuAction
    {
        Roll = 0,
        Move = 1,
        SetNorma = 2,
        ChooseReward = 3,
        ShopBuy = 4,
        ShopReroll = 5,
        ShopLeave = 6,
        EquipCard = 7,
        ChooseEvent = 8
    }

    /// <summary>One decision. The CPU never mutates state; it only produces this.</summary>
    public sealed class CpuDecision
    {
        private CpuDecision(
            CpuAction action,
            int nodeId,
            int[] forkDirections,
            NormaType norma,
            int index,
            int cardUid,
            string reason)
        {
            Action = action;
            NodeId = nodeId;
            ForkDirections = forkDirections ?? Array.Empty<int>();
            Norma = norma;
            Index = index;
            CardUid = cardUid;
            Reason = reason ?? string.Empty;
        }

        public CpuAction Action { get; }

        /// <summary>Move target, or -1 when the route walks the whole roll.</summary>
        public int NodeId { get; }

        public int[] ForkDirections { get; }

        public NormaType Norma { get; }

        public int Index { get; }

        public int CardUid { get; }

        public string Reason { get; }

        public static CpuDecision Roll(string reason)
        {
            return new CpuDecision(CpuAction.Roll, -1, null, NormaType.Shard, -1, -1, reason);
        }

        public static CpuDecision Move(int nodeId, IEnumerable<int> forkDirections, string reason)
        {
            return new CpuDecision(
                CpuAction.Move,
                nodeId,
                forkDirections?.ToArray(),
                NormaType.Shard,
                -1,
                -1,
                reason);
        }

        public static CpuDecision SetNorma(NormaType norma, string reason)
        {
            return new CpuDecision(CpuAction.SetNorma, -1, null, norma, -1, -1, reason);
        }

        public static CpuDecision Reward(int index, string reason)
        {
            return new CpuDecision(CpuAction.ChooseReward, -1, null, NormaType.Shard, index, -1, reason);
        }

        public static CpuDecision Buy(int offerIndex, string reason)
        {
            return new CpuDecision(CpuAction.ShopBuy, -1, null, NormaType.Shard, offerIndex, -1, reason);
        }

        public static CpuDecision Reroll(string reason)
        {
            return new CpuDecision(CpuAction.ShopReroll, -1, null, NormaType.Shard, -1, -1, reason);
        }

        public static CpuDecision Leave(string reason)
        {
            return new CpuDecision(CpuAction.ShopLeave, -1, null, NormaType.Shard, -1, -1, reason);
        }

        public static CpuDecision Equip(int cardUid, string reason)
        {
            return new CpuDecision(CpuAction.EquipCard, -1, null, NormaType.Shard, -1, cardUid, reason);
        }

        /// <summary>Picks an event option; -1 walks away from the event.</summary>
        public static CpuDecision ChooseEvent(int optionIndex, string reason)
        {
            return new CpuDecision(
                CpuAction.ChooseEvent,
                -1,
                null,
                NormaType.Shard,
                optionIndex,
                -1,
                reason);
        }

        public override string ToString()
        {
            return Action + " " + Reason;
        }
    }

    /// <summary>
    /// A utility CPU. It reads a <see cref="CpuObservation"/> only — no controller, no state
    /// mutation — and returns a decision the caller submits as a normal command (R-CPU-001).
    /// The four archetypes differ purely in scoring weights (R-CPU-005).
    /// </summary>
    public sealed class CpuBrain
    {
        /// <summary>Combat samples used to price an ambush.</summary>
        public const int BattleSamples = 6;

        private readonly Pcg32 random;

        public CpuBrain(
            int playerId,
            CpuPersonality personality,
            ulong seed,
            bool enableNoise = true)
        {
            if (personality == null)
            {
                throw new ArgumentNullException(nameof(personality));
            }

            PlayerId = playerId;
            Personality = personality;
            EnableNoise = enableNoise;
            random = new Pcg32(seed ^ (ulong)(playerId + 1) * 0x9E3779B97F4A7C15UL);
        }

        public int PlayerId { get; }

        public CpuPersonality Personality { get; }

        public bool EnableNoise { get; }

        public CpuDecision Decide(CpuObservation observation)
        {
            if (observation == null)
            {
                throw new ArgumentNullException(nameof(observation));
            }

            if (observation.PlayerId != PlayerId)
            {
                throw new InvalidOperationException(
                    $"Brain for player {PlayerId} received an observation for player {observation.PlayerId}.");
            }

            switch (observation.Phase)
            {
                case MatchPhase.AwaitRoll:
                {
                    CpuDecision equip = PlanEquip(observation);
                    return equip ?? CpuDecision.Roll("掷骰");
                }

                case MatchPhase.AwaitMove:
                    return ChooseRoute(observation);

                case MatchPhase.AwaitNorma:
                    return ChooseNorma(observation);

                case MatchPhase.AwaitReward:
                    return ChooseRewardCard(observation);

                case MatchPhase.AwaitShop:
                    return ChooseShopAction(observation);

                case MatchPhase.AwaitEvent:
                    return ChooseEventOption(observation);

                default:
                    return CpuDecision.Roll("对局已结束");
            }
        }

        /// <summary>Equip the best bag card while the battle row still has room.</summary>
        private CpuDecision PlanEquip(CpuObservation observation)
        {
            int remaining = observation.BattleCapacity - observation.BattleWidth;
            if (remaining <= 0 || observation.Bag.Count == 0)
            {
                return null;
            }

            CpuOwnCard best = null;
            double bestValue = 0.0;
            foreach (CpuOwnCard card in observation.Bag)
            {
                if (card.Definition.Scope == CardScope.BagOnly)
                {
                    continue;
                }

                if (card.Definition.SlotSize > remaining)
                {
                    continue;
                }

                double value = EstimateCardValue(card.Definition);
                if (value > bestValue)
                {
                    bestValue = value;
                    best = card;
                }
            }

            if (best == null || bestValue < 0.5)
            {
                return null;
            }

            return CpuDecision.Equip(best.Uid, "装配 " + best.Definition.DisplayName);
        }

        private CpuDecision ChooseRoute(CpuObservation observation)
        {
            CpuRoute best = null;
            double bestScore = double.NegativeInfinity;

            foreach (CpuRoute route in observation.Routes)
            {
                double score = ScoreRoute(observation, route);
                score = ApplyNoise(score);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = route;
                }
            }

            if (best == null)
            {
                return CpuDecision.Move(-1, null, "没有可用路线，直走");
            }

            CpuTile tile = best.Landing;
            string reason = (best.Stops ? "停在 " : "走到 ") + tile.Type
                            + "#" + tile.NodeId
                            + " (" + bestScore.ToString("0.0") + ")";
            return CpuDecision.Move(best.TargetNodeId, best.ForkDirections, reason);
        }

        private CpuDecision ChooseNorma(CpuObservation observation)
        {
            double shardProgress = observation.ShardThreshold <= 0
                ? 1.0
                : observation.Shards / (double)observation.ShardThreshold;
            double winProgress = observation.WinThreshold <= 0
                ? 1.0
                : observation.Wins / (double)observation.WinThreshold;

            double shardScore = shardProgress + (Personality.ShardRouteBias * 0.35) + ApplyNoise(0.0) * 0.25;
            double winScore = winProgress + (Personality.Aggression * 0.10) + ApplyNoise(0.0) * 0.25;

            if (shardScore >= winScore)
            {
                return CpuDecision.SetNorma(
                    NormaType.Shard,
                    "碎片路线 " + observation.Shards + "/" + observation.ShardThreshold);
            }

            return CpuDecision.SetNorma(
                NormaType.Wins,
                "胜场路线 " + observation.Wins + "/" + observation.WinThreshold);
        }

        private CpuDecision ChooseRewardCard(CpuObservation observation)
        {
            if (observation.RewardChoices == null || observation.RewardChoices.Count == 0)
            {
                return CpuDecision.Reward(0, "没有奖励可选");
            }

            int bestIndex = -1;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < observation.RewardChoices.Count; i++)
            {
                CardDefinition definition = observation.RewardChoices[i];
                bool fits = observation.OwnedWidth + definition.SlotSize <= observation.BagCapacity;
                double score = EstimateCardValue(definition) + (fits ? 3.0 : -25.0);
                score = ApplyNoise(score);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                bestIndex = 0;
            }

            return CpuDecision.Reward(
                bestIndex,
                "拿 " + observation.RewardChoices[bestIndex].DisplayName);
        }

        private CpuDecision ChooseShopAction(CpuObservation observation)
        {
            int reserve = observation.Norma == NormaType.Shard
                ? Math.Max(0, observation.ShardThreshold - observation.Shards)
                : 6;

            int bestIndex = -1;
            double bestScore = 0.0;
            for (int i = 0; i < observation.ShopOffers.Count; i++)
            {
                ShopOffer offer = observation.ShopOffers[i];
                if (!offer.IsAvailable)
                {
                    continue;
                }

                if (observation.OwnedWidth + offer.Definition.SlotSize > observation.BagCapacity)
                {
                    continue;
                }

                if (offer.Price > observation.Shards - reserve)
                {
                    continue;
                }

                double score = (EstimateCardValue(offer.Definition) * 4.0)
                               - (offer.Price * Personality.Economy * 1.2);
                score = ApplyNoise(score);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                return CpuDecision.Buy(
                    bestIndex,
                    "买 " + observation.ShopOffers[bestIndex].Definition.DisplayName
                    + " -" + observation.ShopOffers[bestIndex].Price + " 碎片");
            }

            if (!observation.ShopRerollUsed
                && observation.Shards - reserve >= MatchController.ShopRerollCost + 4
                && Personality.Economy >= 0.9)
            {
                return CpuDecision.Reroll("重掷商店");
            }

            return CpuDecision.Leave("离开商店");
        }

        /// <summary>
        /// Scoring exposed for tests and balance tools: it shows how differently two archetypes
        /// price the same route without having to read the private weights.
        /// </summary>
        public double ScoreRouteForDiagnostics(CpuObservation observation, CpuRoute route)
        {
            return ScoreRoute(observation, route);
        }

        private double ScoreRoute(CpuObservation observation, CpuRoute route)
        {
            CpuTile tile = route.Landing;
            if (tile == null)
            {
                return 0.0;
            }

            double score = 0.0;
            switch (tile.Type)
            {
                case TileType.Home:
                    score += (tile.OwnerPlayerId == observation.PlayerId ? 9.0 : 7.0)
                             * Personality.Growth
                             * UpgradeWorth(observation);
                    break;

                case TileType.Reward:
                    score += (6.5 * Personality.Economy) + (3.0 * Personality.Growth);
                    if (observation.OwnedWidth >= observation.BagCapacity)
                    {
                        score -= 9.0;
                    }

                    break;

                case TileType.Shop:
                    score += (observation.Shards >= 8 ? 6.0 : observation.Shards >= 4 ? 3.0 : 0.5)
                             * Personality.Economy;
                    break;

                case TileType.Event:
                    score += (observation.Shards >= 6 ? 5.5 : observation.Shards >= 3 ? 3.0 : 0.5)
                             * (Personality.Economy + Personality.Growth) * 0.5;
                    break;

                case TileType.Monster:
                    score += ScoreMonster(observation, tile, false);
                    break;

                case TileType.Boss:
                    score += ScoreMonster(observation, tile, true);
                    break;

                case TileType.Warp:
                    score -= (4.0 * Personality.Caution) + 1.5;
                    break;
            }

            if (route.OccupantPlayerId >= 0
                && tile.Type != TileType.Monster
                && tile.Type != TileType.Boss)
            {
                score += ScoreAmbush(observation, route.OccupantPlayerId);
            }

            return score;
        }

        /// <summary>
        /// Events sell cards or one-shot battle buffs for Shards. Skip anything unaffordable or
        /// that cannot fit the bag; walk away (option -1) when nothing beats the Shard cost.
        /// </summary>
        private CpuDecision ChooseEventOption(CpuObservation observation)
        {
            int bagRoom = observation.BagCapacity - observation.OwnedWidth;
            int bestIndex = -1;
            double bestScore = 0.0;

            for (int index = 0; index < observation.EventOptions.Count; index++)
            {
                MatchEventOption option = observation.EventOptions[index];
                if (option.ShardCost > observation.Shards || option.CardWidth > bagRoom)
                {
                    continue;
                }

                double value;
                switch (option.Effect)
                {
                    case EventEffectType.RandomCards:
                        value = (6.0 + (option.SlotSize * 3.5)) * option.Count
                                * Personality.Growth;
                        break;
                    case EventEffectType.GainCard:
                        value = 10.0 * Personality.Growth;
                        break;
                    case EventEffectType.BattleShield:
                        value = option.Amount * 0.16 * option.Battles * Personality.Caution;
                        break;
                    case EventEffectType.BattlePower:
                        value = option.Amount * 4.0 * option.Battles * Personality.Aggression;
                        break;
                    default:
                        value = 0.0;
                        break;
                }

                double score = value - (option.ShardCost * 1.2);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = index;
                }
            }

            return bestIndex >= 0
                ? CpuDecision.ChooseEvent(bestIndex, "接过委托")
                : CpuDecision.ChooseEvent(-1, "走开");
        }

        private double UpgradeWorth(CpuObservation observation)
        {
            if (observation.Level >= 5)
            {
                return 1.9;
            }

            if (observation.BossEvolutionTriggered)
            {
                return 1.3;
            }

            return 1.0 + (observation.Level * 0.08);
        }

        private double ScoreMonster(CpuObservation observation, CpuTile tile, bool boss)
        {
            double winChance = EstimateMonsterWinChance(observation, tile, boss);
            double shardRisk = observation.Shards * 0.5;
            double gain = (tile.MonsterPool + (boss ? 4 : 1)) * Personality.Economy;
            double risk = (shardRisk * 0.55) + 1.0;
            return (winChance * gain) - ((1.0 - winChance) * risk * Personality.Caution);
        }

        private double ScoreAmbush(CpuObservation observation, int opponentPlayerId)
        {
            CpuOpponent opponent = null;
            foreach (CpuOpponent candidate in observation.Opponents)
            {
                if (candidate.PlayerId == opponentPlayerId)
                {
                    opponent = candidate;
                    break;
                }
            }

            if (opponent == null)
            {
                return 0.0;
            }

            double winChance = SampleWinChance(observation, opponent);
            int steal = Math.Min(15, opponent.Shards / 2);
            double gain = (steal * Personality.Economy) + (2.0 * Personality.Growth) + 1.0;
            double risk = (observation.Shards / 2.0) * Personality.Caution;
            double score = (winChance * gain) - ((1.0 - winChance) * risk);
            return score * Personality.Aggression;
        }

        /// <summary>
        /// Prices an ambush by replaying the battle a few times. Only public information is used:
        /// the opponent's equipped cards and level.
        /// </summary>
        private double SampleWinChance(CpuObservation observation, CpuOpponent opponent)
        {
            if (observation.BattleCards.Count == 0)
            {
                return 0.05;
            }

            if (opponent.BattleCards.Count == 0)
            {
                return 0.92;
            }

            try
            {
                CombatantSnapshot mine = new CombatantSnapshot(
                    observation.PlayerId,
                    observation.Level,
                    observation.BattleCards.Select(card => card.Card).ToArray());

                List<CardInstance> theirs = new List<CardInstance>();
                int uid = 7000;
                foreach (CardDefinition definition in opponent.BattleCards)
                {
                    theirs.Add(new CardInstance(uid++, definition));
                }

                CombatantSnapshot enemy = new CombatantSnapshot(
                    opponent.PlayerId,
                    opponent.Level,
                    theirs);

                int wins = 0;
                for (int sample = 0; sample < BattleSamples; sample++)
                {
                    CombatSimulation simulation = new CombatSimulation(
                        new BattleSnapshot(mine, enemy),
                        NextSeed());
                    CombatResult result = simulation.RunToCompletion();
                    if (!result.IsDraw && result.WinnerPlayerId == observation.PlayerId)
                    {
                        wins++;
                    }
                }

                return wins / (double)BattleSamples;
            }
            catch (ArgumentException)
            {
                // A loadout the snapshot rejects (for example a stale width) cancels the estimate
                // rather than crashing the CPU turn.
                return 0.5;
            }
        }

        private double EstimateMonsterWinChance(CpuObservation observation, CpuTile tile, bool boss)
        {
            double mine = EstimateDeckStrength(observation.BattleCards);
            double baseline = BaselineStrength(observation.Level) * (boss ? 1.45 : 1.0);
            double ratio = mine / Math.Max(0.25, baseline);
            double chance = 0.5 + ((ratio - 1.0) * 0.45);
            return Math.Max(0.05, Math.Min(0.95, chance));
        }

        /// <summary>Rough per-second output of a full battle row, used for monster estimates.</summary>
        public static double EstimateDeckStrength(IEnumerable<CpuOwnCard> cards)
        {
            double total = 0.0;
            foreach (CpuOwnCard card in cards)
            {
                total += EstimateCardDps(card.Definition);
            }

            return total;
        }

        /// <summary>Expected strength of a monster of this level. 撬棍 alone is ~1.2.</summary>
        public static double BaselineStrength(int level)
        {
            return 1.9 + ((level - 1) * 0.75);
        }

        /// <summary>
        /// Card pricing in damage-per-second equivalent. This is a heuristic, it never rolls dice:
        /// the real numbers stay in the combat kernel.
        /// </summary>
        public static double EstimateCardValue(CardDefinition definition)
        {
            return EstimateCardDps(definition) * 4.0 + (definition.SlotSize * 0.4) + 1.0;
        }

        public static double EstimateCardDps(CardDefinition definition)
        {
            if (definition == null)
            {
                return 0.0;
            }

            if (!definition.IsActive)
            {
                double passive = (definition.Auras.Count * 0.55) + (definition.Passives.Count * 0.45);
                return passive;
            }

            double seconds = Math.Max(0.5, definition.CooldownTicks / 60.0);
            double total = 0.0;
            foreach (CardEffect effect in definition.ActiveEffects)
            {
                total += EffectValue(effect);
            }

            return total / seconds;
        }

        private static double EffectValue(CardEffect effect)
        {
            double average = AverageAmount(effect.Amount);
            switch (effect.Operation)
            {
                case EffectOp.DealDamage:
                    return average;
                case EffectOp.ApplyPoison:
                    return average * 1.9;
                case EffectOp.ApplyBurn:
                    return average * 1.1;
                case EffectOp.ApplyRegeneration:
                    return average * 0.8;
                case EffectOp.Heal:
                    return average * 0.85;
                case EffectOp.GainShield:
                    return average * 0.9;
                case EffectOp.Slow:
                    return 0.25 * Math.Max(1, effect.CardCount);
                case EffectOp.Haste:
                    return 0.3 * Math.Max(1, effect.CardCount);
                case EffectOp.Charge:
                    return 0.35 * Math.Max(1, effect.CardCount);
                case EffectOp.MultiCast:
                    return 0.5 * Math.Max(1, effect.CardCount);
                case EffectOp.DestroySelf:
                    return -3.0;
                case EffectOp.ModifyValue:
                    return 0.4;
                case EffectOp.ModifyDiceCount:
                    return 2.0;
                case EffectOp.ModifyShieldBonus:
                    return 1.0;
                default:
                    return 0.0;
            }
        }

        private static double AverageAmount(EffectAmount amount)
        {
            switch (amount.Kind)
            {
                case AmountKind.Constant:
                    return amount.Constant;
                case AmountKind.Dice:
                    return amount.Count * ((amount.Faces + 1) * 0.5);
                case AmountKind.ValueScaledDice:
                    return 2.0 * ((amount.Faces + 1) * 0.5);
                default:
                    return 0.0;
            }
        }

        private double ApplyNoise(double score)
        {
            if (!EnableNoise || Personality.Noise <= 0.0)
            {
                return score;
            }

            double jitter = (random.NextUInt32() / (double)uint.MaxValue) - 0.5;
            return score + (Math.Abs(score) + 2.0) * jitter * Personality.Noise;
        }

        private ulong NextSeed()
        {
            return ((ulong)random.NextUInt32() << 32) ^ random.NextUInt32();
        }
    }
}
