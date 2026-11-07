using System.Linq;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Commands;

namespace ProjectArtOfAll.Core.Match
{
    public sealed class MatchAutomationReport
    {
        internal MatchAutomationReport(
            int completedTurns,
            bool finished,
            int winnerPlayerId,
            ulong stateHash)
        {
            CompletedTurns = completedTurns;
            Finished = finished;
            WinnerPlayerId = winnerPlayerId;
            StateHash = stateHash;
        }

        public int CompletedTurns { get; }
        public bool Finished { get; }
        public int WinnerPlayerId { get; }
        public ulong StateHash { get; }
    }

    /// <summary>
    /// M1 headless driver: four seats use the same command API as a human player.
    /// Its policy is intentionally simple and deterministic.
    /// </summary>
    public static class SimpleMatchAutoplayer
    {
        public static MatchAutomationReport Run(
            MatchController controller,
            int maximumTurns)
        {
            if (maximumTurns < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(maximumTurns));
            }

            int completedTurns = 0;
            int actionBudget = maximumTurns * 4 + 16;

            while (!controller.IsFinished &&
                   completedTurns < maximumTurns &&
                   actionBudget-- > 0)
            {
                int turnBeforeAction = controller.State.TurnNumber;
                MatchPlayerState player = controller.State.CurrentPlayer;

                switch (controller.State.Phase)
                {
                    case MatchPhase.AwaitRoll:
                        controller.Submit(new RollCommand(player.Id));
                        break;
                    case MatchPhase.AwaitMove:
                        controller.Submit(ChooseMove(controller));
                        break;
                    case MatchPhase.AwaitNorma:
                        controller.Submit(ChooseNorma(controller, player));
                        break;
                    case MatchPhase.AwaitReward:
                        ChooseReward(controller, player);
                        break;
                    case MatchPhase.AwaitShop:
                        HandleShop(controller, player);
                        break;
                    case MatchPhase.AwaitEvent:
                        ChooseEvent(controller, player);
                        break;
                }

                if (controller.State.TurnNumber != turnBeforeAction ||
                    controller.IsFinished)
                {
                    completedTurns++;
                }
            }

            return new MatchAutomationReport(
                completedTurns,
                controller.IsFinished,
                controller.State.WinnerPlayerId,
                controller.ComputeStateHash());
        }

        private static MoveCommand ChooseMove(MatchController controller)
        {
            MatchPlayerState player = controller.State.CurrentPlayer;
            MovementCandidate homeCandidate = controller.CurrentCandidates
                .FirstOrDefaultForPlayerHome(
                    controller.State.Map,
                    player);

            if (player.MeetsCurrentNorma() && homeCandidate != null)
            {
                return new MoveCommand(
                    player.Id,
                    homeCandidate.TargetNodeId,
                    firstForkDirection: null);
            }

            MovementCandidate ambush = SelectAmbushCandidate(controller, player);
            if (ambush != null)
            {
                return new MoveCommand(
                    player.Id,
                    ambush.TargetNodeId,
                    firstForkDirection: null);
            }

            return new MoveCommand(
                player.Id,
                targetNodeId: null,
                firstForkDirection: 0);
        }

        /// <summary>
        /// Picks the reachable landing node that holds the richest opponent we can beat.
        /// Player-versus-player combat only happens on an explicit landing, so the seat has
        /// to aim for it; without this the whole steal pillar never runs in headless play.
        /// </summary>
        private static MovementCandidate SelectAmbushCandidate(
            MatchController controller,
            MatchPlayerState player)
        {
            MovementCandidate bestCandidate = null;
            int bestShards = -1;

            foreach (MovementCandidate candidate in controller.CurrentCandidates)
            {
                MapNode node = controller.State.Map.GetNode(candidate.TargetNodeId);
                if (node.TileType == TileType.Home)
                {
                    continue;
                }

                MatchPlayerState defender = controller.State.Players
                    .Where(other => other.Id != player.Id &&
                                    other.CurrentNode == candidate.TargetNodeId)
                    .OrderByDescending(other => other.Shards)
                    .FirstOrDefault();

                if (defender == null ||
                    defender.Shards < 2 ||
                    !LooksFavourable(player, defender) ||
                    defender.Shards <= bestShards)
                {
                    continue;
                }

                bestCandidate = candidate;
                bestShards = defender.Shards;
            }

            return bestCandidate;
        }

        private static bool LooksFavourable(
            MatchPlayerState attacker,
            MatchPlayerState defender)
        {
            return BattleStrength(attacker) >= BattleStrength(defender);
        }

        private static int BattleStrength(MatchPlayerState player)
        {
            return player.BattleCardWidth * 2 + player.BattleCards.Count;
        }

        /// <summary>
        /// Two-track Norma: stay on the route that is furthest along relative to its own
        /// threshold. The per-seat margin stands in for the M5 personality weights so that
        /// both the Shard and the Win route are exercised in headless runs.
        /// </summary>
        private static SetNormaCommand ChooseNorma(
            MatchController controller,
            MatchPlayerState player)
        {
            int shardThreshold = NormaRules.GetThreshold(player.Level, NormaType.Shard);
            int winThreshold = NormaRules.GetThreshold(player.Level, NormaType.Wins);
            double shardProgress = shardThreshold <= 0
                ? 0.0
                : player.Shards / (double)shardThreshold;
            double winProgress = winThreshold <= 0
                ? 0.0
                : player.Wins / (double)winThreshold;

            double margin = player.Id % 2 == 0 ? 0.5 : -0.5;
            NormaType chosen = winProgress > shardProgress + margin
                ? NormaType.Wins
                : NormaType.Shard;
            return new SetNormaCommand(player.Id, chosen);
        }

        private static MovementCandidate FirstOrDefaultForPlayerHome(
            this System.Collections.Generic.IReadOnlyList<MovementCandidate> candidates,
            MapGraph graph,
            MatchPlayerState player)
        {
            foreach (MovementCandidate candidate in candidates)
            {
                MapNode node = graph.GetNode(candidate.TargetNodeId);
                if (node.TileType == TileType.Home &&
                    node.OwnerPlayerId == player.Id)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void ChooseReward(
            MatchController controller,
            MatchPlayerState player)
        {
            int selectedIndex = -1;
            for (int index = 0; index < controller.CurrentRewardChoices.Count; index++)
            {
                int size = controller.CurrentRewardChoices[index].SlotSize;
                if (player.OwnedCardWidth + size <= LoadoutRules.BagCapacity)
                {
                    selectedIndex = index;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                throw new System.InvalidOperationException(
                    "Reward phase has no selectable card.");
            }

            controller.Submit(
                new ChooseRewardCommand(player.Id, selectedIndex));
        }

        /// <summary>Buys the cheapest affordable option that still fits the bag.</summary>
        private static void ChooseEvent(
            MatchController controller,
            MatchPlayerState player)
        {
            MatchEventSession session = controller.CurrentEvent;
            int bestIndex = -1;
            int bestPrice = int.MaxValue;

            for (int index = 0; index < session.Options.Count; index++)
            {
                MatchEventOption option = session.Options[index];
                if (option.ShardCost > player.Shards ||
                    player.OwnedCardWidth + option.CardWidth > LoadoutRules.BagCapacity)
                {
                    continue;
                }

                if (option.ShardCost < bestPrice)
                {
                    bestPrice = option.ShardCost;
                    bestIndex = index;
                }
            }

            controller.Submit(new EventChoiceCommand(player.Id, bestIndex));
        }

        private static void HandleShop(
            MatchController controller,
            MatchPlayerState player)
        {
            ShopSession shop = controller.CurrentShop;
            bool boughtOffer = false;
            ShopOffer bestOffer = null;
            int shardReserve = player.Norma == NormaType.Shard
                ? NormaRules.GetThreshold(player.Level, NormaType.Shard)
                : 0;
            int spendableShards = System.Math.Max(0, player.Shards - shardReserve);

            foreach (ShopOffer offer in shop.Offers)
            {
                if (!offer.IsAvailable)
                {
                    boughtOffer = true;
                    continue;
                }

                if (spendableShards < offer.Price ||
                    player.OwnedCardWidth + offer.Definition.SlotSize >
                    LoadoutRules.BagCapacity)
                {
                    continue;
                }

                if (bestOffer == null || offer.Price < bestOffer.Price)
                {
                    bestOffer = offer;
                }
            }

            if (!boughtOffer && bestOffer != null)
            {
                int offerIndex = -1;
                for (int index = 0; index < shop.Offers.Count; index++)
                {
                    if (shop.Offers[index] == bestOffer)
                    {
                        offerIndex = index;
                        break;
                    }
                }

                controller.Submit(
                    new ShopActionCommand(
                        player.Id,
                        ShopActionType.Buy,
                        offerIndex));
                return;
            }

            if (!boughtOffer &&
                !shop.RerollUsed &&
                spendableShards >= 2)
            {
                controller.Submit(
                    new ShopActionCommand(player.Id, ShopActionType.Reroll));
                return;
            }

            controller.Submit(
                new ShopActionCommand(player.Id, ShopActionType.Leave));
        }
    }
}
