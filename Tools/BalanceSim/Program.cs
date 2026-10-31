using System;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Rng;

namespace ProjectArtOfAll.Core.BalanceSim
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            int battleCount = 10000;
            if (args.Length > 0 &&
                (!int.TryParse(args[0], out battleCount) || battleCount <= 0))
            {
                Console.Error.WriteLine("Usage: BalanceSim [positive battle count]");
                return 2;
            }

            SampleCardCatalog catalog = SampleCardCatalog.Instance;
            BattleSnapshot snapshot = CreateSnapshot(catalog);
            Pcg32 seedSource = new Pcg32(20261031UL, 0x42414C414E4345UL);

            long totalTicks = 0L;
            int leftWins = 0;
            int rightWins = 0;
            int draws = 0;

            for (int index = 0; index < battleCount; index++)
            {
                ulong high = seedSource.NextUInt32();
                ulong low = seedSource.NextUInt32();
                ulong seed = (high << 32) | low;

                CombatResult result =
                    new CombatSimulation(snapshot, seed).RunToCompletion();
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
            return 0;
        }

        private static BattleSnapshot CreateSnapshot(ICardCatalog catalog)
        {
            return new BattleSnapshot(
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
        }
    }
}
