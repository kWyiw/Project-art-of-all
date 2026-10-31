using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ProjectArtOfAll.Core.Cards;

namespace ProjectArtOfAll.Core.Combat
{
    public sealed class CombatantSnapshot
    {
        public CombatantSnapshot(
            int playerId,
            int level,
            IEnumerable<CardInstance> battleCards)
        {
            if (playerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            BattleConfig.GetMaxHealth(level);

            if (battleCards == null)
            {
                throw new ArgumentNullException(nameof(battleCards));
            }

            List<CardInstance> cards = battleCards.ToList();
            if (cards.Any(card => card == null))
            {
                throw new ArgumentException(
                    "Battle cards cannot contain null entries.",
                    nameof(battleCards));
            }

            int usedWidth = cards.Sum(card => card.Definition.SlotSize);
            int capacity = LoadoutRules.GetBattleCapacity(level);
            if (usedWidth > capacity)
            {
                throw new ArgumentException(
                    $"Battle loadout uses {usedWidth}c but level {level} only allows {capacity}c.",
                    nameof(battleCards));
            }

            PlayerId = playerId;
            Level = level;
            BattleCards = Array.AsReadOnly(cards.ToArray());
        }

        public int PlayerId { get; }
        public int Level { get; }
        public IReadOnlyList<CardInstance> BattleCards { get; }
    }

    public sealed class BattleSnapshot
    {
        public BattleSnapshot(CombatantSnapshot left, CombatantSnapshot right)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));

            if (left.PlayerId == right.PlayerId)
            {
                throw new ArgumentException(
                    "A combatant cannot fight itself in a two-player battle.");
            }
        }

        public CombatantSnapshot Left { get; }
        public CombatantSnapshot Right { get; }
    }

    public static class BattleConfig
    {
        public static int GetMaxHealth(int level)
        {
            switch (level)
            {
                case 1:
                    return 100;
                case 2:
                    return 150;
                case 3:
                    return 200;
                case 4:
                    return 250;
                case 5:
                    return 300;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(level),
                        "Combat levels must be between 1 and 5.");
            }
        }
    }
}
