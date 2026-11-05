using System;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// The four CPU personalities. The kernel is identical for every seat and the information
    /// set is identical too (R-CPU-002); only the scoring weights change, so the seats look
    /// different to the player without any of them cheating.
    /// </summary>
    public enum CpuArchetype
    {
        /// <summary>Chases fights and ambushes, accepts risk.</summary>
        Aggressive = 0,

        /// <summary>Holds shards, buys and rerolls in shops, walks the shard Norma.</summary>
        Economist = 1,

        /// <summary>Plays for levels: homes, reward tiles, minimal fights.</summary>
        Guardian = 2,

        /// <summary>Hunts other players for steals and wins.</summary>
        Hunter = 3
    }

    public enum CpuDifficulty
    {
        /// <summary>Weights flattened toward neutral and much noisier decisions.</summary>
        Easy = 0,

        Normal = 1,

        /// <summary>Weights sharpened and much less noise.</summary>
        Hard = 2
    }

    /// <summary>
    /// Score weights and a deterministic seat mapping. Creation is pure, so a match replay
    /// with the same seed always produces the same personalities.
    /// </summary>
    public sealed class CpuPersonality
    {
        private static readonly CpuArchetype[] SeatOrder =
        {
            CpuArchetype.Aggressive,
            CpuArchetype.Economist,
            CpuArchetype.Guardian,
            CpuArchetype.Hunter
        };

        public CpuPersonality(
            CpuArchetype archetype,
            CpuDifficulty difficulty,
            string label,
            double aggression,
            double economy,
            double growth,
            double caution,
            double shardRouteBias,
            double noise)
        {
            Archetype = archetype;
            Difficulty = difficulty;
            Label = label ?? string.Empty;
            Aggression = aggression;
            Economy = economy;
            Growth = growth;
            Caution = caution;
            ShardRouteBias = shardRouteBias;
            Noise = noise;
        }

        public CpuArchetype Archetype { get; }

        public CpuDifficulty Difficulty { get; }

        public string Label { get; }

        /// <summary>Weight on picking fights, including ambushing other players.</summary>
        public double Aggression { get; }

        /// <summary>Weight on shard income and shop value.</summary>
        public double Economy { get; }

        /// <summary>Weight on level progress (homes, rewards).</summary>
        public double Growth { get; }

        /// <summary>Weight on loss risk. Losing a fight halves the shard stack.</summary>
        public double Caution { get; }

        /// <summary>Positive values keep the shard Norma even when wins look cheaper.</summary>
        public double ShardRouteBias { get; }

        /// <summary>Relative noise added to every score. Easy seats misjudge more.</summary>
        public double Noise { get; }

        public static CpuArchetype ArchetypeForSeat(int playerId, int playerCount = 4)
        {
            if (playerCount <= 0)
            {
                playerCount = 4;
            }

            int index = ((playerId % playerCount) + playerCount) % playerCount;
            return SeatOrder[index % SeatOrder.Length];
        }

        public static CpuPersonality ForSeat(
            int playerId,
            CpuDifficulty difficulty = CpuDifficulty.Normal,
            int playerCount = 4)
        {
            return Create(ArchetypeForSeat(playerId, playerCount), difficulty);
        }

        public static CpuPersonality Create(
            CpuArchetype archetype,
            CpuDifficulty difficulty = CpuDifficulty.Normal)
        {
            double aggression;
            double economy;
            double growth;
            double caution;
            double shardRouteBias;
            double noise;
            string label;

            switch (archetype)
            {
                case CpuArchetype.Aggressive:
                    label = "激进";
                    aggression = 1.45;
                    economy = 0.75;
                    growth = 0.95;
                    caution = 0.55;
                    shardRouteBias = -0.25;
                    noise = 0.45;
                    break;

                case CpuArchetype.Economist:
                    label = "理财";
                    aggression = 0.70;
                    economy = 1.50;
                    growth = 1.00;
                    caution = 1.00;
                    shardRouteBias = 0.70;
                    noise = 0.35;
                    break;

                case CpuArchetype.Guardian:
                    label = "保守";
                    aggression = 0.85;
                    economy = 1.00;
                    growth = 1.35;
                    caution = 1.35;
                    shardRouteBias = -0.10;
                    noise = 0.30;
                    break;

                case CpuArchetype.Hunter:
                    label = "狩猎";
                    aggression = 1.20;
                    economy = 1.00;
                    growth = 1.15;
                    caution = 0.90;
                    shardRouteBias = 0.15;
                    noise = 0.50;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(archetype));
            }

            // Difficulty does not add knowledge, it only sharpens or blurs the same weights.
            double blend = difficulty == CpuDifficulty.Easy
                ? 0.45
                : difficulty == CpuDifficulty.Hard
                    ? -0.22
                    : 0.0;

            aggression = Blend(aggression, blend);
            economy = Blend(economy, blend);
            growth = Blend(growth, blend);
            caution = Blend(caution, blend);

            double noiseScale = difficulty == CpuDifficulty.Easy
                ? 2.0
                : difficulty == CpuDifficulty.Hard
                    ? 0.45
                    : 1.0;

            return new CpuPersonality(
                archetype,
                difficulty,
                label,
                aggression,
                economy,
                growth,
                caution,
                shardRouteBias,
                noise * noiseScale);
        }

        private static double Blend(double value, double towardOne)
        {
            return value + ((1.0 - value) * towardOne);
        }

        public override string ToString()
        {
            return Label + "/" + Difficulty;
        }
    }
}
