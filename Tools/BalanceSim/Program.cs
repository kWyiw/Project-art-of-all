using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Events;
using ProjectArtOfAll.Core.Rng;

namespace ProjectArtOfAll.Core.BalanceSim
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            int battleCount = 2000;
            if (args.Length > 0 &&
                (!int.TryParse(args[0], out battleCount) || battleCount <= 0))
            {
                Console.Error.WriteLine("Usage: BalanceSim [positive battle count]");
                return 2;
            }

            CardDatabase catalog = CardDatabase.Instance;
            RunRegressionScenario(catalog, battleCount);
            RunPerCardProbe(catalog, battleCount);
            return 0;
        }

        private static void RunRegressionScenario(CardDatabase catalog, int battleCount)
        {
            BattleSnapshot snapshot = new BattleSnapshot(
                new CombatantSnapshot(
                    playerId: 0,
                    level: 3,
                    battleCards: new[]
                    {
                        new CardInstance(100, catalog.Get("whetstone")),
                        new CardInstance(101, catalog.Get("crowbar")),
                        new CardInstance(102, catalog.Get("nuclear_water"))
                    }),
                new CombatantSnapshot(
                    playerId: 1,
                    level: 3,
                    battleCards: new[]
                    {
                        new CardInstance(200, catalog.Get("car_door")),
                        new CardInstance(201, catalog.Get("hardtack")),
                        new CardInstance(202, catalog.Get("lighter"))
                    }));

            Pcg32 seedSource = new Pcg32(20261031UL, 0x42414C414E4345UL);
            long totalTicks = 0L;
            int leftWins = 0;
            int rightWins = 0;
            int draws = 0;

            for (int index = 0; index < battleCount; index++)
            {
                CombatResult result = new CombatSimulation(
                    snapshot,
                    NextSeed(seedSource)).RunToCompletion();
                totalTicks += result.EndTick;

                if (result.IsDraw)
                {
                    draws++;
                }
                else if (result.WinnerPlayerId == 0)
                {
                    leftWins++;
                }
                else
                {
                    rightWins++;
                }
            }

            double averageSeconds =
                totalTicks / (double)(battleCount * CombatSimulation.TicksPerSecond);
            Console.WriteLine($"battles={battleCount}");
            Console.WriteLine($"averageSeconds={averageSeconds:F3}");
            Console.WriteLine($"leftWins={leftWins}");
            Console.WriteLine($"rightWins={rightWins}");
            Console.WriteLine($"draws={draws}");
            Console.WriteLine($"leftWinRate={leftWins * 100.0 / battleCount:F2}%");
            Console.WriteLine($"rightWinRate={rightWins * 100.0 / battleCount:F2}%");
            Console.WriteLine($"drawRate={draws * 100.0 / battleCount:F2}%");
        }

        /// <summary>
        /// 每张牌单独对一块固定靶（汽车门板）跑 N 局，报告伤害/治疗/护盾的每秒产出。
        /// 这是"这张牌在当前数据下值不值它的价格"的唯一量化口径。
        /// </summary>
        private static void RunPerCardProbe(CardDatabase catalog, int battleCount)
        {
            Console.WriteLine();
            Console.WriteLine("card\tslot\tcd\tdps\thps\tsps\twin%\tdraw%\tavgS");

            List<string> report = new List<string>();
            foreach (CardDefinition card in catalog.All)
            {
                Pcg32 seedSource = new Pcg32(777UL, 0x50524F4245UL);
                long totalTicks = 0L;
                long totalDamage = 0L;
                long totalHeal = 0L;
                long totalShield = 0L;
                int wins = 0;
                int draws = 0;

                for (int index = 0; index < battleCount; index++)
                {
                    CombatSimulation simulation = new CombatSimulation(
                        DuelOf(card),
                        NextSeed(seedSource));
                    CombatResult result = simulation.RunToCompletion();

                    totalTicks += result.EndTick;
                    if (result.IsDraw)
                    {
                        draws++;
                    }
                    else if (result.WinnerPlayerId == 0)
                    {
                        wins++;
                    }

                    foreach (GameEvent gameEvent in simulation.Events.History)
                    {
                        if (gameEvent is DamageDealtEvent damage &&
                            damage.TargetPlayerId == 1 &&
                            gameEvent.Tick < CombatSimulation.TimeoutStartTick)
                        {
                            // 中毒 / 灼烧的周期伤害由 -1 发出，在 1v1 里归给对手侧。
                            totalDamage += damage.Amount;
                        }
                        else if (gameEvent is HealthRestoredEvent heal &&
                                 heal.PlayerId == 0)
                        {
                            totalHeal += heal.Amount;
                        }
                        else if (gameEvent is ShieldGainedEvent shield &&
                                 shield.PlayerId == 0)
                        {
                            totalShield += shield.Amount;
                        }
                    }
                }

                double totalSeconds = totalTicks / (double)CombatSimulation.TicksPerSecond;
                double averageSeconds = totalSeconds / battleCount;
                double denominator = Math.Max(totalSeconds, 0.001);
                report.Add(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2}\t{3:F2}\t{4:F2}\t{5:F2}\t{6:F1}\t{7:F1}\t{8:F1}",
                    card.DisplayName,
                    card.SlotSize,
                    card.CooldownTicks / CombatSimulation.TicksPerSecond,
                    totalDamage / denominator,
                    totalHeal / denominator,
                    totalShield / denominator,
                    wins * 100.0 / battleCount,
                    draws * 100.0 / battleCount,
                    averageSeconds));
            }

            foreach (string line in report)
            {
                Console.WriteLine(line);
            }
        }

        private static BattleSnapshot DuelOf(CardDefinition card)
        {
            CardDatabase catalog = CardDatabase.Instance;
            return new BattleSnapshot(
                new CombatantSnapshot(
                    playerId: 0,
                    level: 1,
                    battleCards: new[] { new CardInstance(1, card) }),
                new CombatantSnapshot(
                    playerId: 1,
                    level: 1,
                    battleCards: new[]
                    {
                        new CardInstance(2, catalog.Get("crowbar"))
                    }));
        }

        private static ulong NextSeed(Pcg32 source)
        {
            ulong high = source.NextUInt32();
            ulong low = source.NextUInt32();
            return (high << 32) | low;
        }
    }
}
