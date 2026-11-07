using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Commands;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>Per-seat counters for one CPU match.</summary>
    public sealed class CpuSeatStatistics
    {
        public CpuSeatStatistics(int playerId, CpuArchetype archetype)
        {
            PlayerId = playerId;
            Archetype = archetype;
        }

        public int PlayerId { get; }

        public CpuArchetype Archetype { get; }

        public int Decisions { get; internal set; }

        public int Rolls { get; internal set; }

        public int Moves { get; internal set; }

        public int Equips { get; internal set; }

        public int Rewards { get; internal set; }

        public int ShopBuys { get; internal set; }

        public int ShopRerolls { get; internal set; }

        public int ShopLeaves { get; internal set; }

        public int Events { get; internal set; }

        public int NormaShard { get; internal set; }

        public int NormaWins { get; internal set; }

        public override string ToString()
        {
            return $"P{PlayerId + 1} {Archetype} 决策{Decisions} 移动{Moves} 装配{Equips} "
                   + $"奖励{Rewards} 购买{ShopBuys} 重掷{ShopRerolls} "
                   + $"Norma碎片{NormaShard}/胜场{NormaWins}";
        }
    }

    /// <summary>Outcome of one CPU-played match.</summary>
    public sealed class CpuMatchResult
    {
        public CpuMatchResult(
            bool finished,
            int winnerPlayerId,
            int rounds,
            int turns,
            int decisions,
            IReadOnlyList<CpuSeatStatistics> seats,
            string stallReason)
        {
            Finished = finished;
            WinnerPlayerId = winnerPlayerId;
            Rounds = rounds;
            Turns = turns;
            Decisions = decisions;
            Seats = seats ?? Array.Empty<CpuSeatStatistics>();
            StallReason = stallReason ?? string.Empty;
        }

        public bool Finished { get; }

        public int WinnerPlayerId { get; }

        public int Rounds { get; }

        public int Turns { get; }

        public int Decisions { get; }

        public IReadOnlyList<CpuSeatStatistics> Seats { get; }

        public string StallReason { get; }

        public string ToReport()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append("=== CPU match ===").AppendLine();
            builder.Append("finished=").Append(Finished);
            builder.Append(" winner=").Append(WinnerPlayerId + 1);
            builder.Append(" rounds=").Append(Rounds);
            builder.Append(" turns=").Append(Turns);
            builder.Append(" decisions=").Append(Decisions).AppendLine();
            foreach (CpuSeatStatistics seat in Seats)
            {
                builder.Append(seat.ToString()).AppendLine();
            }

            if (!string.IsNullOrEmpty(StallReason))
            {
                builder.Append("stall: ").Append(StallReason).AppendLine();
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Drives a entire match with one <see cref="CpuBrain"/> per seat, submitting ordinary
    /// commands only. Used by the M5 acceptance runs and by headless tools; the Unity layer
    /// reuses the same brains for the CPU seats of a human match.
    /// </summary>
    public sealed class CpuMatchRunner
    {
        private const int DefaultMaximumDecisions = 20000;
        private const int MaximumConsecutiveFailures = 6;

        private readonly MatchController controller;
        private readonly CpuBrain[] brains;
        private readonly CpuSeatStatistics[] seats;

        public CpuMatchRunner(
            MatchController controller,
            CpuDifficulty difficulty = CpuDifficulty.Normal)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));

            int playerCount = controller.State.Players.Count;
            brains = new CpuBrain[playerCount];
            seats = new CpuSeatStatistics[playerCount];

            for (int seat = 0; seat < playerCount; seat++)
            {
                CpuPersonality personality = CpuPersonality.ForSeat(seat, difficulty, playerCount);
                brains[seat] = new CpuBrain(
                    seat,
                    personality,
                    controller.State.Seed + ((ulong)seat * 7919UL));
                seats[seat] = new CpuSeatStatistics(seat, personality.Archetype);
            }
        }

        public CpuBrain BrainFor(int playerId)
        {
            return playerId >= 0 && playerId < brains.Length ? brains[playerId] : null;
        }

        public IReadOnlyList<CpuSeatStatistics> Seats => seats;

        public int TotalDecisions { get; private set; }

        /// <summary>Runs one decision. Returns false once the match is finished or stalled.</summary>
        public bool Step(out string failure)
        {
            failure = string.Empty;
            if (controller.IsFinished)
            {
                return false;
            }

            MatchPlayerState player = controller.State.CurrentPlayer;
            CpuObservation observation = CpuObservation.Capture(controller);
            CpuDecision decision = brains[player.Id].Decide(observation);

            try
            {
                Apply(player.Id, decision);
            }
            catch (InvalidOperationException error)
            {
                failure = decision.Action + " -> " + error.Message;
                return false;
            }

            Count(player.Id, decision);
            TotalDecisions++;
            return true;
        }

        private void Count(int playerId, CpuDecision decision)
        {
            CpuSeatStatistics seat = seats[playerId];
            seat.Decisions++;
            switch (decision.Action)
            {
                case CpuAction.Roll:
                    seat.Rolls++;
                    break;
                case CpuAction.Move:
                    seat.Moves++;
                    break;
                case CpuAction.EquipCard:
                    seat.Equips++;
                    break;
                case CpuAction.ChooseReward:
                    seat.Rewards++;
                    break;
                case CpuAction.ShopBuy:
                    seat.ShopBuys++;
                    break;
                case CpuAction.ShopReroll:
                    seat.ShopRerolls++;
                    break;
                case CpuAction.ShopLeave:
                    seat.ShopLeaves++;
                    break;
                case CpuAction.ChooseEvent:
                    seat.Events++;
                    break;
                case CpuAction.SetNorma:
                    if (decision.Norma == NormaType.Shard)
                    {
                        seat.NormaShard++;
                    }
                    else
                    {
                        seat.NormaWins++;
                    }

                    break;
            }
        }

        private void Apply(int playerId, CpuDecision decision)
        {
            switch (decision.Action)
            {
                case CpuAction.Roll:
                    controller.Submit(new RollCommand(playerId));
                    break;

                case CpuAction.Move:
                    controller.Submit(new MoveCommand(
                        playerId,
                        decision.NodeId >= 0 ? decision.NodeId : (int?)null,
                        firstForkDirection: null,
                        forkDirections: decision.ForkDirections));
                    break;

                case CpuAction.SetNorma:
                    controller.Submit(new SetNormaCommand(playerId, decision.Norma));
                    break;

                case CpuAction.ChooseReward:
                    controller.Submit(new ChooseRewardCommand(playerId, decision.Index));
                    break;

                case CpuAction.ShopBuy:
                    controller.Submit(new ShopActionCommand(playerId, ShopActionType.Buy, decision.Index));
                    break;

                case CpuAction.ShopReroll:
                    controller.Submit(new ShopActionCommand(playerId, ShopActionType.Reroll));
                    break;

                case CpuAction.ShopLeave:
                    controller.Submit(new ShopActionCommand(playerId, ShopActionType.Leave));
                    break;

                case CpuAction.EquipCard:
                    controller.Submit(new EquipCardCommand(playerId, decision.CardUid));
                    break;

                case CpuAction.ChooseEvent:
                    controller.Submit(new EventChoiceCommand(playerId, decision.Index));
                    break;

                default:
                    throw new InvalidOperationException("Unsupported CPU action " + decision.Action + ".");
            }
        }

        public CpuMatchResult Run(int maximumDecisions = DefaultMaximumDecisions)
        {
            string stall = string.Empty;
            int failures = 0;

            while (!controller.IsFinished && TotalDecisions < maximumDecisions)
            {
                string failure;
                if (Step(out failure))
                {
                    continue;
                }

                failures++;
                stall = failure;
                if (failures >= MaximumConsecutiveFailures)
                {
                    break;
                }

                RecoverFromFailure();
            }

            if (controller.IsFinished)
            {
                stall = string.Empty;
            }
            else if (string.IsNullOrEmpty(stall))
            {
                stall = "decision limit " + maximumDecisions;
            }

            return new CpuMatchResult(
                controller.IsFinished,
                controller.IsFinished ? controller.State.WinnerPlayerId : -1,
                controller.State.RoundNumber,
                controller.State.TurnNumber,
                TotalDecisions,
                seats,
                stall);
        }

        /// <summary>Gets the match moving again after a rejected command.</summary>
        private void RecoverFromFailure()
        {
            if (controller.IsFinished)
            {
                return;
            }

            int playerId = controller.State.CurrentPlayer.Id;
            switch (controller.State.Phase)
            {
                case MatchPhase.AwaitRoll:
                    controller.Submit(new RollCommand(playerId));
                    break;
                case MatchPhase.AwaitMove:
                    controller.Submit(new MoveCommand(playerId, null, 0));
                    break;
                case MatchPhase.AwaitNorma:
                    controller.Submit(new SetNormaCommand(playerId, NormaType.Wins));
                    break;
                case MatchPhase.AwaitReward:
                    controller.Submit(new ChooseRewardCommand(playerId, 0));
                    break;
                case MatchPhase.AwaitShop:
                    controller.Submit(new ShopActionCommand(playerId, ShopActionType.Leave));
                    break;
                case MatchPhase.AwaitEvent:
                    controller.Submit(new EventChoiceCommand(playerId, -1));
                    break;
            }
        }
    }
}
