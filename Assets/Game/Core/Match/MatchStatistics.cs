using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// Per-seat economy and progression counters, filled purely from the event stream.
    /// </summary>
    public sealed class PlayerMatchStatistics
    {
        private readonly List<int> levelUpTurns = new List<int>();

        internal PlayerMatchStatistics(int playerId)
        {
            PlayerId = playerId;
            Level = 1;
        }

        public int PlayerId { get; }
        public int Turns { get; internal set; }
        public int ShardsGained { get; internal set; }
        public int ShardsLost { get; internal set; }
        public int WinsGained { get; internal set; }
        public int PeakShards { get; internal set; }
        public int CurrentShards { get; internal set; }
        public int FinalWins { get; internal set; }
        public int Level { get; internal set; }
        public int BattlesFought { get; internal set; }
        public int BattlesWon { get; internal set; }
        public int PlayerBattlesFought { get; internal set; }
        public int PlayerBattlesWon { get; internal set; }
        public int StealsWon { get; internal set; }
        public int ShardsStolenFrom { get; internal set; }
        public int CardsBought { get; internal set; }
        public int CardsSold { get; internal set; }
        public int LevelSixTurn { get; internal set; } = -1;
        public NormaType LastNorma { get; internal set; }
        public IReadOnlyList<int> LevelUpTurns => new ReadOnlyCollection<int>(levelUpTurns);

        internal void RecordLevelUp(int turn)
        {
            levelUpTurns.Add(turn);
        }
    }

    /// <summary>
    /// M3 acceptance instrument: observes a running match and produces the report the
    /// milestone asks for (duration, Shard curve, per-route progression).
    /// </summary>
    public sealed class MatchStatistics : IDisposable
    {
        private readonly MatchController controller;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly PlayerMatchStatistics[] players;
        private readonly List<KeyValuePair<int, int>> shardSamples =
            new List<KeyValuePair<int, int>>();
        private readonly List<KeyValuePair<int, NormaType>> normaChoices =
            new List<KeyValuePair<int, NormaType>>();
        private int roundNumber = 1;
        private int turnNumber = 1;
        private int turns = 0;
        private bool disposed;

        private MatchStatistics(MatchController controller)
        {
            this.controller = controller;
            players = new PlayerMatchStatistics[controller.State.Players.Count];
            for (int index = 0; index < players.Length; index++)
            {
                players[index] = new PlayerMatchStatistics(index);
            }
        }

        public static MatchStatistics Attach(MatchController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            MatchStatistics statistics = new MatchStatistics(controller);
            statistics.Subscribe();
            return statistics;
        }

        public IReadOnlyList<PlayerMatchStatistics> Players =>
            new ReadOnlyCollection<PlayerMatchStatistics>(players);

        public IReadOnlyList<KeyValuePair<int, NormaType>> NormaChoices =>
            new ReadOnlyCollection<KeyValuePair<int, NormaType>>(normaChoices);

        public IReadOnlyList<KeyValuePair<int, int>> ShardSamples =>
            new ReadOnlyCollection<KeyValuePair<int, int>>(shardSamples);

        public int Turns => turns;
        public int Rounds => roundNumber;
        public int TotalTurnsInMatch => controller.State.TurnNumber;
        public bool Finished => controller.IsFinished;
        public int WinnerPlayerId => controller.State.WinnerPlayerId;

        /// <summary>Shard curve sampled at the end of every round.</summary>
        public IReadOnlyList<int> ShardCurveByRound()
        {
            Dictionary<int, int> lastPerRound = new Dictionary<int, int>();
            foreach (KeyValuePair<int, int> sample in shardSamples)
            {
                lastPerRound[sample.Key] = sample.Value;
            }

            return lastPerRound.OrderBy(pair => pair.Key)
                .Select(pair => pair.Value)
                .ToArray();
        }

        public int NormaChosenCount(NormaType normaType)
        {
            return normaChoices.Count(pair => pair.Value == normaType);
        }

        public PlayerMatchStatistics this[int playerId]
        {
            get
            {
                foreach (PlayerMatchStatistics player in players)
                {
                    if (player.PlayerId == playerId)
                    {
                        return player;
                    }
                }

                throw new ArgumentOutOfRangeException(nameof(playerId));
            }
        }

        public string ToReport()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("=== M3 match report ===");
            builder.AppendLine(
                $"seed={controller.State.Seed} finished={Finished} " +
                $"winner={WinnerPlayerId} rounds={Rounds} turns={Turns}");
            builder.AppendLine(
                "id | lvl | norma | shards | wins | turns | battles won/fought | pvp won/fought | steals");
            foreach (PlayerMatchStatistics player in players)
            {
                builder.AppendLine(
                    $"{player.PlayerId} | {player.Level} | {player.LastNorma} | " +
                    $"{player.CurrentShards} | {player.FinalWins} | {player.Turns} | " +
                    $"{player.BattlesWon}/{player.BattlesFought} | " +
                    $"{player.PlayerBattlesWon}/{player.PlayerBattlesFought} | " +
                    $"{player.StealsWon}");
            }

            builder.Append("shard curve by round: ");
            IReadOnlyList<int> curve = ShardCurveByRound();
            for (int index = 0; index < curve.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(" -> ");
                }

                builder.Append(curve[index]);
            }

            builder.AppendLine();
            builder.AppendLine(
                $"norma routes: shard={NormaChosenCount(NormaType.Shard)} " +
                $"win={NormaChosenCount(NormaType.Wins)}");
            return builder.ToString();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (IDisposable subscription in subscriptions)
            {
                subscription.Dispose();
            }

            subscriptions.Clear();
        }

        private void Subscribe()
        {
            int order = 1000;
            subscriptions.Add(controller.Events.Subscribe<TurnStartedEvent>(order, OnTurnStarted));
            subscriptions.Add(controller.Events.Subscribe<TurnEndedEvent>(order, OnTurnEnded));
            subscriptions.Add(controller.Events.Subscribe<ShardsChangedEvent>(order, OnShardsChanged));
            subscriptions.Add(controller.Events.Subscribe<WinsChangedEvent>(order, OnWinsChanged));
            subscriptions.Add(controller.Events.Subscribe<PlayerLeveledUpEvent>(order, OnLeveledUp));
            subscriptions.Add(controller.Events.Subscribe<CombatResolvedEvent>(order, OnCombatResolved));
            subscriptions.Add(controller.Events.Subscribe<CardAddedEvent>(order, OnCardAdded));
            subscriptions.Add(controller.Events.Subscribe<CardSoldEvent>(order, OnCardSold));
        }

        private void OnTurnStarted(TurnStartedEvent gameEvent)
        {
            roundNumber = gameEvent.RoundNumber;
            turnNumber = gameEvent.TurnNumber;
        }

        private void OnTurnEnded(TurnEndedEvent gameEvent)
        {
            turns++;
            PlayerMatchStatistics player = Find(gameEvent.PlayerId);
            if (player != null)
            {
                player.Turns++;
            }

            shardSamples.Add(
                new KeyValuePair<int, int>(
                    roundNumber == 0 ? 1 : roundNumber,
                    TotalShards()));
        }

        private void OnShardsChanged(ShardsChangedEvent gameEvent)
        {
            PlayerMatchStatistics player = Find(gameEvent.PlayerId);
            if (player == null)
            {
                return;
            }

            if (gameEvent.Delta > 0)
            {
                player.ShardsGained += gameEvent.Delta;
            }
            else
            {
                player.ShardsLost += -gameEvent.Delta;
            }

            player.CurrentShards = gameEvent.NewValue;
            if (gameEvent.NewValue > player.PeakShards)
            {
                player.PeakShards = gameEvent.NewValue;
            }
        }

        private void OnWinsChanged(WinsChangedEvent gameEvent)
        {
            PlayerMatchStatistics player = Find(gameEvent.PlayerId);
            if (player == null)
            {
                return;
            }

            if (gameEvent.Delta > 0)
            {
                player.WinsGained += gameEvent.Delta;
            }

            player.FinalWins = gameEvent.NewValue;
        }

        private void OnLeveledUp(PlayerLeveledUpEvent gameEvent)
        {
            PlayerMatchStatistics player = Find(gameEvent.PlayerId);
            if (player == null)
            {
                return;
            }

            player.Level = gameEvent.NewLevel;
            player.RecordLevelUp(turnNumber);

            NormaType norma = controller.State.Players[gameEvent.PlayerId].Norma;
            player.LastNorma = norma;
            normaChoices.Add(
                new KeyValuePair<int, NormaType>(gameEvent.PlayerId, norma));

            if (gameEvent.NewLevel >= 6)
            {
                player.LevelSixTurn = turnNumber;
            }
        }

        private void OnCombatResolved(CombatResolvedEvent gameEvent)
        {
            PlayerMatchStatistics attacker = Find(gameEvent.AttackerId);
            if (attacker == null)
            {
                return;
            }

            attacker.BattlesFought++;
            if (gameEvent.Result.WinnerPlayerId == gameEvent.AttackerId)
            {
                attacker.BattlesWon++;
            }

            if (gameEvent.DefenderId < players.Length)
            {
                PlayerMatchStatistics defender = players[gameEvent.DefenderId];
                attacker.PlayerBattlesFought++;
                defender.PlayerBattlesFought++;

                if (gameEvent.Result.WinnerPlayerId == gameEvent.AttackerId)
                {
                    attacker.PlayerBattlesWon++;
                    attacker.StealsWon++;
                }
                else if (gameEvent.Result.WinnerPlayerId == gameEvent.DefenderId)
                {
                    defender.PlayerBattlesWon++;
                }
            }
        }

        private void OnCardAdded(CardAddedEvent gameEvent)
        {
            PlayerMatchStatistics player = Find(gameEvent.PlayerId);
            if (player != null && gameEvent.Source == "Shop")
            {
                player.CardsBought++;
            }
        }

        private void OnCardSold(CardSoldEvent gameEvent)
        {
            PlayerMatchStatistics player = Find(gameEvent.PlayerId);
            if (player != null)
            {
                player.CardsSold++;
            }
        }

        private int TotalShards()
        {
            int total = 0;
            foreach (PlayerMatchStatistics player in players)
            {
                total += player.CurrentShards;
            }

            return total;
        }

        private PlayerMatchStatistics Find(int playerId)
        {
            return playerId >= 0 && playerId < players.Length
                ? players[playerId]
                : null;
        }
    }
}
