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
        Resource = 1 << 8
    }

    public enum CardScope
    {
        BattleOnly = 0,
        BagAndBattle = 1,
        BagOnly = 2
    }

    public enum EffectTarget
    {
        Enemy = 0,
        Self = 1
    }

    public enum EffectOp
    {
        DealDamage = 0,
        GainShield = 1,
        ApplyPoison = 2,
        ApplyBurn = 3,
        ApplyRegeneration = 4,
        Heal = 5
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
        MultiCast = 1
    }

    public enum AuraScaling
    {
        Flat = 0,
        PerAdjacentMatchingTag = 1
    }

    public readonly struct DiceExpression
    {
        public DiceExpression(int count, int faces)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (faces < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(faces));
            }

            Count = count;
            Faces = faces;
        }

        public int Count { get; }
        public int Faces { get; }

        public int Roll(Rng.Pcg32 rng, int perDieModifier = 0)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            int total = 0;
            for (int die = 0; die < Count; die++)
            {
                int value = rng.NextInt(1, Faces + 1) + perDieModifier;
                total += Math.Max(1, Math.Min(Faces, value));
            }

            return total;
        }
    }

    public sealed class EffectDefinition
    {
        public EffectDefinition(
            EffectOp operation,
            DiceExpression amount,
            EffectTarget target)
        {
            Operation = operation;
            Amount = amount;
            Target = target;
        }

        public EffectOp Operation { get; }
        public DiceExpression Amount { get; }
        public EffectTarget Target { get; }
    }

    public sealed class AuraDefinition
    {
        public AuraDefinition(
            AuraTarget target,
            AuraStat stat,
            int amount,
            CardTag requiredTag = CardTag.None,
            AuraScaling scaling = AuraScaling.Flat)
        {
            Target = target;
            Stat = stat;
            Amount = amount;
            RequiredTag = requiredTag;
            Scaling = scaling;
        }

        public AuraTarget Target { get; }
        public AuraStat Stat { get; }
        public int Amount { get; }
        public CardTag RequiredTag { get; }
        public AuraScaling Scaling { get; }
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
            IEnumerable<EffectDefinition> activeEffects,
            IEnumerable<AuraDefinition> auras = null)
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

            if (tags == CardTag.None)
            {
                throw new ArgumentException(
                    "Every card requires at least one tag.",
                    nameof(tags));
            }

            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Id = id;
            DisplayName = displayName;
            SlotSize = slotSize;
            CooldownTicks = cooldownTicks;
            Tags = tags;
            Value = value;
            Scope = scope;
            ActiveEffects = new ReadOnlyCollection<EffectDefinition>(
                new List<EffectDefinition>(
                    activeEffects ?? Array.Empty<EffectDefinition>()).ToArray());
            Auras = new ReadOnlyCollection<AuraDefinition>(
                new List<AuraDefinition>(
                    auras ?? Array.Empty<AuraDefinition>()).ToArray());
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int SlotSize { get; }
        public int CooldownTicks { get; }
        public CardTag Tags { get; }
        public int Value { get; }
        public CardScope Scope { get; }
        public IReadOnlyList<EffectDefinition> ActiveEffects { get; }
        public IReadOnlyList<AuraDefinition> Auras { get; }

        public bool HasTag(CardTag tag)
        {
            return (Tags & tag) == tag;
        }
    }
}
