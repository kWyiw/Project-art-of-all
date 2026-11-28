using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectArtOfAll.Core.Cards
{
    [Flags]
    public enum CardTag
    {
        None = 0,
        Weapon = 1 << 0,
        Tool = 1 << 1,
        Waste = 1 << 2,
        Food = 1 << 3,
        Companion = 1 << 4,
        Trap = 1 << 5,
        Estate = 1 << 6,
        Craft = 1 << 7,
        Resource = 1 << 8,
        Mascot = 1 << 9,
        Disease = 1 << 10,
        Event = 1 << 11
    }

    public enum CardScope
    {
        BattleOnly = 0,
        BagAndBattle = 1,
        BagOnly = 2
    }

    public enum AuraTarget
    {
        Self = 0,
        AdjacentLeft = 1,
        AdjacentRight = 2,
        AllAdjacent = 3,
        AllByTag = 4
    }

    public enum AuraStat
    {
        DiceFlatModifier = 0,
        MultiCast = 1,

        /// <summary>自噬之蛇: every die always rolls its maximum (BT-7.7).</summary>
        MaxRoll = 2,

        /// <summary>Adds whole dice to the roll (data-driven cards such as 粉碎机).</summary>
        DiceCount = 3,

        /// <summary>
        /// 结算出来的最终数值直接 +N（不夹到骰面）：自噬之蛇的「所有数值 +100」用它。
        /// </summary>
        ValueBonus = 4,

        /// <summary>This card's shield gains an extra modifier.</summary>
        ShieldBonus = 5,

        /// <summary>Attack damage dealt by this card restores the same amount of health.</summary>
        Lifesteal = 6,

        /// <summary>Only modifies dice that produce attack damage.</summary>
        DamageDiceModifier = 7,

        /// <summary>母体: maximum health increases per poison layer.</summary>
        MaxHealthPerPoison = 8
    }

    public enum AuraScaling
    {
        Flat = 0,
        PerAdjacentMatchingTag = 1,
        PerOtherMatchingTag = 2,
        SelfAndAdjacentValue = 3,
        PerOwnerRegeneration = 4,
        PerOwnerWins = 5
    }

    public sealed class AuraDefinition
    {
        public AuraDefinition(
            AuraTarget target,
            AuraStat stat,
            int amount,
            CardTag requiredTag = CardTag.None,
            AuraScaling scaling = AuraScaling.Flat,
            int minimumSlotSize = 0)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (minimumSlotSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumSlotSize));
            }

            Target = target;
            Stat = stat;
            Amount = amount;
            RequiredTag = requiredTag;
            Scaling = scaling;
            MinimumSlotSize = minimumSlotSize;
        }

        public AuraTarget Target { get; }
        public AuraStat Stat { get; }
        public int Amount { get; }
        public CardTag RequiredTag { get; }
        public AuraScaling Scaling { get; }

        /// <summary>0 = any size. 双持 only buffs 1c weapons.</summary>
        public int MinimumSlotSize { get; }
    }

    public sealed class CardDefinition
    {
        public CardDefinition(
            string id,
            string displayName,
            int slotSize,
            int cooldownTicks,
            CardTag tags,
            int value,
            CardScope scope,
            IEnumerable<CardEffect> activeEffects,
            IEnumerable<AuraDefinition> auras = null,
            IEnumerable<PassiveDefinition> passives = null,
            string baseId = null,
            bool isUpgraded = false,
            bool cannotSell = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Card id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "Card display name is required.",
                    nameof(displayName));
            }

            if (slotSize < 1 || slotSize > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(slotSize));
            }

            if (cooldownTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownTicks));
            }

            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            CardEffect[] effects = ToArray(activeEffects ?? Array.Empty<CardEffect>());
            AuraDefinition[] auraArray = ToArray(auras ?? Array.Empty<AuraDefinition>());
            PassiveDefinition[] passiveArray =
                ToArray(passives ?? Array.Empty<PassiveDefinition>());

            if (effects.Length == 0 && passiveArray.Length == 0 && auraArray.Length == 0)
            {
                throw new ArgumentException(
                    $"Card '{id}' has no effect, aura or passive and can never do anything.",
                    nameof(activeEffects));
            }

            if (effects.Length > 0 && cooldownTicks <= 0)
            {
                throw new ArgumentException(
                    $"Card '{id}' has active effects and therefore needs a cooldown.",
                    nameof(cooldownTicks));
            }

            Id = id;
            DisplayName = displayName;
            SlotSize = slotSize;
            CooldownTicks = cooldownTicks;
            Tags = tags;
            Value = value;
            Scope = scope;
            ActiveEffects = new ReadOnlyCollection<CardEffect>(effects);
            Auras = new ReadOnlyCollection<AuraDefinition>(auraArray);
            Passives = new ReadOnlyCollection<PassiveDefinition>(passiveArray);
            IsUpgraded = isUpgraded;
            BaseId = isUpgraded
                ? (string.IsNullOrEmpty(baseId) ? id : baseId)
                : id;
            CannotSell = cannotSell;
            if (isUpgraded && BaseId == id)
            {
                throw new ArgumentException(
                    $"Upgraded card '{id}' needs a different base id.",
                    nameof(baseId));
            }
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int SlotSize { get; }
        public int CooldownTicks { get; }
        public CardTag Tags { get; }
        public int Value { get; }
        public CardScope Scope { get; }
        public IReadOnlyList<CardEffect> ActiveEffects { get; }
        public IReadOnlyList<AuraDefinition> Auras { get; }
        public IReadOnlyList<PassiveDefinition> Passives { get; }

        /// <summary>升级版的 id 前缀：基础牌指向自己，升级牌指向原始牌（CJ-5.11）。</summary>
        public string BaseId { get; }

        /// <summary>这张定义是不是某个基础牌的升级版。</summary>
        public bool IsUpgraded { get; }

        /// <summary>事件牌可以由设计稿禁止出售；核心命令与 UI 都必须拒绝出售。</summary>
        public bool CannotSell { get; }

        /// <summary>
        /// 基础牌对应的升级版；已经升级的牌、以及没有设计升级版的牌都是 null。
        /// 每张牌只能升级一次，所以升级版永远不再带升级目标。
        /// </summary>
        public CardDefinition Upgraded { get; private set; }

        public bool CanUpgrade => Upgraded != null;

        internal void LinkUpgrade(CardDefinition upgraded)
        {
            if (upgraded == null)
            {
                throw new ArgumentNullException(nameof(upgraded));
            }

            if (!upgraded.IsUpgraded || upgraded.BaseId != Id)
            {
                throw new ArgumentException(
                    $"'{upgraded.Id}' is not the upgrade of '{Id}'.",
                    nameof(upgraded));
            }

            Upgraded = upgraded;
        }

        public bool HasTag(CardTag tag)
        {
            return (Tags & tag) == tag;
        }

        public bool IsActive => CooldownTicks > 0 && ActiveEffects.Count > 0;

        private static T[] ToArray<T>(IEnumerable<T> source)
        {
            if (source is T[] array)
            {
                return (T[])array.Clone();
            }

            return new List<T>(source).ToArray();
        }
    }
}
