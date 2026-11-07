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
            IEnumerable<CardInstance> battleCards,
            int? maxHealth = null)
            : this(playerId, level, CardSlotRules.Unpack(battleCards), maxHealth)
        {
        }

        /// <summary>
        /// 带占位的构造：战斗栏里的牌可以留空，相邻光环按列号判断相邻。
        /// </summary>
        public CombatantSnapshot(
            int playerId,
            int level,
            IEnumerable<CardSlot> battleSlots,
            int? maxHealth = null)
        {
            if (playerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            int defaultHealth = BattleConfig.GetMaxHealth(level);
            if (maxHealth.HasValue && maxHealth.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxHealth),
                    "Max health must be positive.");
            }

            if (battleSlots == null)
            {
                throw new ArgumentNullException(nameof(battleSlots));
            }

            List<CardSlot> slots = battleSlots.ToList();
            if (slots.Any(slot => slot.Card == null))
            {
                throw new ArgumentException(
                    "Battle slots cannot contain empty entries.",
                    nameof(battleSlots));
            }

            int capacity = LoadoutRules.GetBattleCapacity(level);
            if (slots.Any(slot => slot.Column < 0 || slot.RightColumn >= capacity))
            {
                throw new ArgumentException(
                    "A battle slot extends past the battle capacity.",
                    nameof(battleSlots));
            }

            for (int i = 0; i < slots.Count; i++)
            {
                for (int j = i + 1; j < slots.Count; j++)
                {
                    if (slots[i].Column <= slots[j].RightColumn &&
                        slots[j].Column <= slots[i].RightColumn)
                    {
                        throw new ArgumentException(
                            "Two battle slots overlap.",
                            nameof(battleSlots));
                    }
                }
            }

            int usedWidth = slots.Sum(slot => slot.Card.Definition.SlotSize);
            if (usedWidth > capacity)
            {
                throw new ArgumentException(
                    $"Battle loadout uses {usedWidth}c but level {level} only allows {capacity}c.",
                    nameof(battleSlots));
            }

            PlayerId = playerId;
            Level = level;
            MaxHealth = maxHealth ?? defaultHealth;
            BattleSlots = Array.AsReadOnly(
                slots.OrderBy(slot => slot.Column).ToArray());
            BattleCards = Array.AsReadOnly(
                slots.OrderBy(slot => slot.Column)
                    .Select(slot => slot.Card)
                    .ToArray());
        }

        public int PlayerId { get; }
        public int Level { get; }

        /// <summary>本场最大生命值。默认按等级取（100/150/200/250/300），模拟模式可以自己填。</summary>
        public int MaxHealth { get; }

        /// <summary>战斗栏的占位（按列排序，中间可能有空列）。</summary>
        public IReadOnlyList<CardSlot> BattleSlots { get; }

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
