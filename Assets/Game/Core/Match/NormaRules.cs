using System;

namespace ProjectArtOfAll.Core.Match
{
    public static class NormaRules
    {
        private static readonly int[] ShardThresholds =
        {
            6,
            14,
            26,
            40,
            54
        };

        private static readonly int[] WinThresholds =
        {
            1,
            2,
            4,
            6,
            8
        };

        public static int GetThreshold(int level, NormaType normaType)
        {
            if (level < 1 || level > 5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(level),
                    "Norma thresholds exist for levels 1 through 5.");
            }

            return normaType == NormaType.Shard
                ? ShardThresholds[level - 1]
                : WinThresholds[level - 1];
        }

        public static bool MeetsThreshold(
            int level,
            NormaType normaType,
            int shards,
            int wins)
        {
            return normaType == NormaType.Shard
                ? shards >= GetThreshold(level, normaType)
                : wins >= GetThreshold(level, normaType);
        }
    }
}
