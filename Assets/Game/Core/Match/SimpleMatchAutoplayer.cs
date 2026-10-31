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
                        controller.Submit(
                            new SetNormaCommand(
                                player.Id,
                                NormaType.Shard));
                        break;
                    case MatchPhase.AwaitReward:
                        ChooseReward(controller, player);
                        break;
                    case MatchPhase.AwaitShop:
                        HandleShop(controller, player);
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

            return new MoveCommand(
                player.Id,
                targetNodeId: null,
                firstForkDirection: 0);
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
