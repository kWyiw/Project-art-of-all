using System;
using System.Collections.Generic;
using ProjectArtOfAll.Core.Rng;

namespace ProjectArtOfAll.Core.Match
{
    public enum MonsterPhase
    {
        Early = 0,
        Mid = 1,
        Late = 2
    }

    /// <summary>One deterministic monster template used by board encounters.</summary>
    public sealed class MonsterDefinition
    {
        public MonsterDefinition(
            string id,
            string displayName,
            int maxHealth,
            int loadoutLevel,
            params string[] cardIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Monster id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Monster name is required.", nameof(displayName));
            }

            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            }

            if (loadoutLevel < 1 || loadoutLevel > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(loadoutLevel));
            }

            if (cardIds == null || cardIds.Length == 0)
            {
                throw new ArgumentException("A monster needs at least one card.", nameof(cardIds));
            }

            Id = id;
            DisplayName = displayName;
            MaxHealth = maxHealth;
            LoadoutLevel = loadoutLevel;
            CardIds = Array.AsReadOnly((string[])cardIds.Clone());
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxHealth { get; }

        /// <summary>
        /// Level used only for battle-row capacity and combat defaults. The template
        /// overrides MaxHealth, so this is not the player's level.
        /// </summary>
        public int LoadoutLevel { get; }

        public IReadOnlyList<string> CardIds { get; }
    }

    /// <summary>
    /// Monster templates by match phase. Adding monsters only requires data rows here;
    /// phase evaluation and encounter resolution remain unchanged.
    /// </summary>
    public static class MonsterRoster
    {
        public const int TargetRosterSizePerPhase = 3;

        private static readonly IReadOnlyList<MonsterDefinition> Early =
            Array.AsReadOnly(new[]
            {
                new MonsterDefinition(
                    "early_grunt_a",
                    "杂鱼A",
                    50,
                    1,
                    "crowbar",
                    "concrete_block",
                    "concrete_block"),
                new MonsterDefinition(
                    "early_grunt_b",
                    "杂鱼B",
                    200,
                    1,
                    "whetstone",
                    "duct_tape",
                    "special_rubber")
            });

        private static readonly IReadOnlyList<MonsterDefinition> Mid =
            Array.AsReadOnly(new[]
            {
                new MonsterDefinition(
                    "mid_grunt_a",
                    "大杂鱼A",
                    100,
                    2,
                    "car_door",
                    "fireman_axe",
                    "nail_gun")
            });

        private static readonly IReadOnlyList<MonsterDefinition> Late =
            Array.AsReadOnly(new[]
            {
                new MonsterDefinition(
                    "late_grunt_a",
                    "超级杂鱼A",
                    300,
                    5,
                    "titan",
                    "placeholder_4",
                    "torch",
                    "compound")
            });

        public static IReadOnlyList<MonsterDefinition> For(MonsterPhase phase)
        {
            switch (phase)
            {
                case MonsterPhase.Early:
                    return Early;
                case MonsterPhase.Mid:
                    return Mid;
                case MonsterPhase.Late:
                    return Late;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase));
            }
        }

        public static MonsterDefinition Pick(MonsterPhase phase, Pcg32 random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            IReadOnlyList<MonsterDefinition> roster = For(phase);
            return roster[random.NextInt(roster.Count)];
        }
    }
}
