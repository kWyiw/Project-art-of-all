using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Match;

namespace MatchSim
{
    /// <summary>
    /// M3 acceptance harness: plays whole four-seat matches headlessly and reports the
    /// numbers the milestone asks for (duration, Shard curve, per-route win rate).
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            int matches = 100;
            ulong seed = 20261031UL;
            int maxTurns = 4000;
            string csvPath = null;
            bool useBrains = false;
            CpuDifficulty difficulty = CpuDifficulty.Normal;

            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--matches":
                        matches = int.Parse(args[++index], CultureInfo.InvariantCulture);
                        break;
                    case "--seed":
                        seed = ulong.Parse(args[++index], CultureInfo.InvariantCulture);
                        break;
                    case "--turns":
                        maxTurns = int.Parse(args[++index], CultureInfo.InvariantCulture);
                        break;
                    case "--csv":
                        csvPath = args[++index];
                        break;
                    case "--brains":
                        useBrains = true;
                        break;
                    case "--difficulty":
                        difficulty = (CpuDifficulty)int.Parse(
                            args[++index], CultureInfo.InvariantCulture);
                        break;
                    default:
                        Console.Error.WriteLine($"Unknown argument: {args[index]}");
                        return 2;
                }
            }

            if (matches <= 0 || maxTurns <= 0)
            {
                Console.Error.WriteLine("--matches and --turns must be positive.");
                return 2;
            }

            if (useBrains)
            {
                return RunBrains(matches, seed, difficulty, maxTurns);
            }

            List<MatchRow> rows = new List<MatchRow>(matches);
            string firstReport = null;

            for (int index = 0; index < matches; index++)
            {
                ulong matchSeed = seed + (ulong)index;
                MapGraph graph = BuildRingMap();
                MatchController controller = new MatchController(graph, matchSeed);

                using (MatchStatistics statistics = MatchStatistics.Attach(controller))
                {
                    SimpleMatchAutoplayer.Run(controller, maxTurns);

                    if (index == 0)
                    {
                        firstReport = statistics.ToReport();
                    }

                    rows.Add(MatchRow.From(statistics, matchSeed));
                }
            }

            Console.WriteLine(firstReport);
            Console.WriteLine(RenderAggregate(rows, seed, matches));

            if (!string.IsNullOrEmpty(csvPath))
            {
                File.WriteAllText(csvPath, RenderCsv(rows), new UTF8Encoding(false));
                Console.WriteLine($"csv written: {csvPath}");
            }

            return rows.All(row => row.Finished) ? 0 : 1;
        }


        /// <summary>
        /// M5 acceptance: four seats driven by <see cref="CpuMatchRunner"/> (the real AI)
        /// instead of the simple autoplayer. Reports the numbers M5 asks for.
        /// </summary>
        private static int RunBrains(
            int matches,
            ulong seed,
            CpuDifficulty difficulty,
            int maxTurns)
        {
            List<CpuMatchResult> results = new List<CpuMatchResult>(matches);
            string firstReport = null;

            for (int index = 0; index < matches; index++)
            {
                ulong matchSeed = seed + (ulong)index;
                MatchController controller = new MatchController(BuildRingMap(), matchSeed);
                CpuMatchRunner runner = new CpuMatchRunner(controller, difficulty);
                CpuMatchResult result = runner.Run(Math.Max(4000, maxTurns * 6));
                if (index == 0)
                {
                    firstReport = result.ToReport();
                }

                results.Add(result);
            }

            Console.WriteLine(firstReport);
            Console.WriteLine(RenderBrainAggregate(results, seed, matches, difficulty));
            return results.All(result => result.Finished) ? 0 : 1;
        }

        private static string RenderBrainAggregate(
            List<CpuMatchResult> results,
            ulong seed,
            int matches,
            CpuDifficulty difficulty)
        {
            int[] rounds = results.Select(result => result.Rounds).OrderBy(value => value).ToArray();
            int medianRounds = rounds.Length == 0 ? 0 : rounds[rounds.Length / 2];
            int minRounds = rounds.Length == 0 ? 0 : rounds[0];
            int maxRounds = rounds.Length == 0 ? 0 : rounds[rounds.Length - 1];

            int[] seatWins = new int[4];
            int[] shardRoute = new int[4];
            int[] winRoute = new int[4];
            int equips = 0;
            int buys = 0;
            foreach (CpuMatchResult result in results)
            {
                if (result.WinnerPlayerId >= 0 && result.WinnerPlayerId < seatWins.Length)
                {
                    seatWins[result.WinnerPlayerId]++;
                }

                foreach (CpuSeatStatistics seat in result.Seats)
                {
                    if (seat.PlayerId < 0 || seat.PlayerId >= 4)
                    {
                        continue;
                    }

                    shardRoute[seat.PlayerId] += seat.NormaShard;
                    winRoute[seat.PlayerId] += seat.NormaWins;
                    equips += seat.Equips;
                    buys += seat.ShopBuys;
                }
            }

            int totalShard = shardRoute.Sum();
            int totalWin = winRoute.Sum();
            StringBuilder text = new StringBuilder();
            text.AppendLine("=== CPU brain aggregate ===");
            text.Append("seed=").Append(seed)
                .Append(" matches=").Append(matches)
                .Append(" difficulty=").Append(difficulty.ToString())
                .AppendLine();
            text.Append("finished ").Append(results.Count(result => result.Finished))
                .Append('/').Append(results.Count)
                .Append("  stalls ").Append(results.Count(result => !result.Finished))
                .AppendLine();
            text.Append("rounds avg ").Append(Average(results.Select(r => (double)r.Rounds)))
                .Append(" median ").Append(medianRounds)
                .Append(" min ").Append(minRounds)
                .Append(" max ").Append(maxRounds)
                .AppendLine();
            text.Append("turns avg ").Append(Average(results.Select(r => (double)r.Turns)))
                .Append("  decisions avg ").Append(Average(results.Select(r => (double)r.Decisions)))
                .AppendLine();
            text.Append("seat wins P1 ").Append(PercentOf(seatWins[0], matches))
                .Append(" P2 ").Append(PercentOf(seatWins[1], matches))
                .Append(" P3 ").Append(PercentOf(seatWins[2], matches))
                .Append(" P4 ").Append(PercentOf(seatWins[3], matches))
                .AppendLine();
            text.Append("routes shard ").Append(PercentOf(totalShard, totalShard + totalWin))
                .Append(" wins ").Append(PercentOf(totalWin, totalShard + totalWin))
                .AppendLine();
            text.Append("per match equips ").Append(Average(results.Select(r => (double)equips / Math.Max(1, matches))))
                .Append(" shop buys ").Append(Average(results.Select(r => (double)buys / Math.Max(1, matches))))
                .AppendLine();

            foreach (string stall in results
                .Where(result => !result.Finished)
                .Select(result => result.StallReason)
                .Distinct()
                .Take(5))
            {
                text.Append("stall: ").AppendLine(stall);
            }

            return text.ToString();
        }

        private static string Average(IEnumerable<double> values)
        {
            double[] list = values.ToArray();
            return list.Length == 0
                ? "0.0"
                : list.Average().ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string PercentOf(int part, int total)
        {
            return total <= 0
                ? "0.0%"
                : (100.0 * part / total).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        /// Twenty-four tile ring: four homes, monsters, bosses, shops, rewards and warps.
        /// Roughly mirrors the production board's tile mix so the report is meaningful
        /// without loading a Unity scene.
        /// </summary>
        private static MapGraph BuildRingMap()
        {
            TileType[] layout =
            {
                TileType.Home, TileType.Monster, TileType.Reward, TileType.Shop,
                TileType.Monster, TileType.Warp, TileType.Home, TileType.Monster,
                TileType.Reward, TileType.Shop, TileType.Monster, TileType.Warp,
                TileType.Home, TileType.Monster, TileType.Reward, TileType.Shop,
                TileType.Monster, TileType.Boss, TileType.Home, TileType.Monster,
                TileType.Reward, TileType.Shop, TileType.Monster, TileType.Warp
            };

            List<MapNode> nodes = new List<MapNode>(layout.Length);
            for (int nodeId = 0; nodeId < layout.Length; nodeId++)
            {
                TileType tileType = layout[nodeId];
                int owner = tileType == TileType.Home ? HomeIndex(nodeId) : -1;
                nodes.Add(
                    new MapNode(
                        nodeId,
                        tileType,
                        owner,
                        (nodeId + 1) % layout.Length));
            }

            int[] warps = Enumerable.Range(0, layout.Length)
                .Where(nodeId => layout[nodeId] == TileType.Warp)
                .ToArray();

            return MapGraph.Create(
                nodes,
                playerCount: 4,
                teleportGroups: new[] { warps });
        }

        private static int HomeIndex(int nodeId)
        {
            switch (nodeId)
            {
                case 0: return 0;
                case 6: return 1;
                case 12: return 2;
                default: return 3;
            }
        }

        private static string RenderAggregate(
            IReadOnlyList<MatchRow> rows,
            ulong seed,
            int requested)
        {
            StringBuilder builder = new StringBuilder();
            int finished = rows.Count(row => row.Finished);
            builder.AppendLine("=== M3 aggregate ===");
            builder.AppendLine($"matches={rows.Count} requested={requested} seed={seed} finished={finished}");

            builder.AppendLine(
                $"rounds: avg={Average(rows, row => row.Rounds):F2} " +
                $"median={Median(rows.Select(row => (double)row.Rounds)):F1} " +
                $"min={rows.Min(row => row.Rounds)} max={rows.Max(row => row.Rounds)}");
            builder.AppendLine(
                $"turns: avg={Average(rows, row => row.Turns):F2} " +
                $"median={Median(rows.Select(row => (double)row.Turns)):F1}");
            builder.AppendLine(
                $"est match minutes (3.3 min/round): avg=" +
                $"{Average(rows, row => row.Rounds) * 3.3:F1}");

            builder.Append("winner seats: ");
            for (int seat = 0; seat < 4; seat++)
            {
                int count = rows.Count(row => row.Winner == seat);
                builder.Append($"P{seat + 1}={count}（{Percent(count, rows.Count):F1}%） ");
            }

            builder.AppendLine();

            int shardRouteWins = rows.Count(row => row.WinnerNorma == "Shard");
            int winRouteWins = rows.Count(row => row.WinnerNorma == "Wins");
            builder.AppendLine(
                $"winning route: shard={shardRouteWins}（{Percent(shardRouteWins, rows.Count):F1}%） " +
                $"wins={winRouteWins}（{Percent(winRouteWins, rows.Count):F1}%）");

            int shardChoices = rows.Sum(row => row.ShardChoices);
            int winChoices = rows.Sum(row => row.WinChoices);
            builder.AppendLine(
                $"norma choices: shard={shardChoices} wins={winChoices} " +
                $"wins share={Percent(winChoices, shardChoices + winChoices):F1}%");

            builder.AppendLine(
                $"pvp battles: avg={Average(rows, row => row.PlayerBattles):F2}/match " +
                $"pvp-free matches={rows.Count(row => row.PlayerBattles == 0)}");
            builder.AppendLine(
                $"steals: avg={Average(rows, row => row.Steals):F2}/match");
            builder.AppendLine(
                $"boss evolution reached (someone hit level 4+): " +
                $"{Percent(rows.Count(row => row.MaxLevel >= 4), rows.Count):F1}%");
            builder.AppendLine(
                $"winner levels: avg={Average(rows, row => row.MaxLevel):F2} " +
                $"finish rounds: avg={Average(rows, row => row.Rounds):F1}");

            IReadOnlyList<double> curve = AverageShardCurve(rows);
            builder.Append("shard curve by round (avg): ");
            for (int round = 0; round < curve.Count; round++)
            {
                if (round > 0)
                {
                    builder.Append(" -> ");
                }

                builder.Append($"{curve[round]:F0}");
            }

            builder.AppendLine();
            return builder.ToString();
        }

        private static IReadOnlyList<double> AverageShardCurve(IReadOnlyList<MatchRow> rows)
        {
            int longest = rows.Count == 0 ? 0 : rows.Max(row => row.ShardCurve.Length);
            double[] totals = new double[longest];
            int[] counts = new int[longest];

            foreach (MatchRow row in rows)
            {
                for (int index = 0; index < row.ShardCurve.Length; index++)
                {
                    totals[index] += row.ShardCurve[index];
                    counts[index]++;
                }
            }

            for (int index = 0; index < totals.Length; index++)
            {
                if (counts[index] > 0)
                {
                    totals[index] /= counts[index];
                }
            }

            return totals;
        }

        private static string RenderCsv(IReadOnlyList<MatchRow> rows)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(
                "seed,finished,winner,winner_norma,rounds,turns,max_level," +
                "pvp_battles,steals,shard_choices,win_choices,shard_curve");
            foreach (MatchRow row in rows)
            {
                builder.AppendLine(string.Join(
                    ",",
                    row.Seed.ToString(CultureInfo.InvariantCulture),
                    row.Finished ? "1" : "0",
                    row.Winner.ToString(CultureInfo.InvariantCulture),
                    row.WinnerNorma,
                    row.Rounds.ToString(CultureInfo.InvariantCulture),
                    row.Turns.ToString(CultureInfo.InvariantCulture),
                    row.MaxLevel.ToString(CultureInfo.InvariantCulture),
                    row.PlayerBattles.ToString(CultureInfo.InvariantCulture),
                    row.Steals.ToString(CultureInfo.InvariantCulture),
                    row.ShardChoices.ToString(CultureInfo.InvariantCulture),
                    row.WinChoices.ToString(CultureInfo.InvariantCulture),
                    string.Join("|", row.ShardCurve)));
            }

            return builder.ToString();
        }

        private static double Average(
            IReadOnlyList<MatchRow> rows,
            Func<MatchRow, int> selector)
        {
            return rows.Count == 0 ? 0 : rows.Sum(selector) / (double)rows.Count;
        }

        private static double Median(IEnumerable<double> values)
        {
            double[] sorted = values.OrderBy(value => value).ToArray();
            if (sorted.Length == 0)
            {
                return 0;
            }

            int middle = sorted.Length / 2;
            return sorted.Length % 2 == 0
                ? (sorted[middle - 1] + sorted[middle]) / 2.0
                : sorted[middle];
        }

        private static double Percent(int part, int total)
        {
            return total == 0 ? 0 : part * 100.0 / total;
        }

        private sealed class MatchRow
        {
            public ulong Seed { get; private set; }
            public bool Finished { get; private set; }
            public int Winner { get; private set; }
            public string WinnerNorma { get; private set; }
            public int Rounds { get; private set; }
            public int Turns { get; private set; }
            public int MaxLevel { get; private set; }
            public int PlayerBattles { get; private set; }
            public int Steals { get; private set; }
            public int ShardChoices { get; private set; }
            public int WinChoices { get; private set; }
            public int[] ShardCurve { get; private set; }

            public static MatchRow From(MatchStatistics statistics, ulong seed)
            {
                int winner = statistics.WinnerPlayerId;
                PlayerMatchStatistics winnerStats =
                    winner >= 0 && winner < statistics.Players.Count
                        ? statistics.Players[winner]
                        : null;

                return new MatchRow
                {
                    Seed = seed,
                    Finished = statistics.Finished,
                    Winner = winner,
                    WinnerNorma = winnerStats == null
                        ? "none"
                        : winnerStats.LastNorma.ToString(),
                    Rounds = statistics.Rounds,
                    Turns = statistics.Turns,
                    MaxLevel = statistics.Players.Max(player => player.Level),
                    PlayerBattles = statistics.Players.Sum(
                        player => player.PlayerBattlesFought) / 2,
                    Steals = statistics.Players.Sum(player => player.StealsWon),
                    ShardChoices = statistics.NormaChosenCount(NormaType.Shard),
                    WinChoices = statistics.NormaChosenCount(NormaType.Wins),
                    ShardCurve = statistics.ShardCurveByRound().ToArray()
                };
            }
        }
    }
}
