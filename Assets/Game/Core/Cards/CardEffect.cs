using System;
using ProjectArtOfAll.Core.Rng;

namespace ProjectArtOfAll.Core.Cards
{
    /// <summary>
    /// Every atomic effect the card list needs. The combat resolver dispatches on these;
    /// adding a card must never require a new C# class.
    /// </summary>
    public enum EffectOp
    {
        DealDamage = 0,
        GainShield = 1,
        ApplyPoison = 2,
        ApplyBurn = 3,
        ApplyRegeneration = 4,
        Heal = 5,
        Charge = 6,
        Haste = 7,
        Slow = 8,
        MultiCast = 9,
        DestroySelf = 10,
        ModifyValue = 11,

        /// <summary>粉碎机 / 热熔切割器: add whole dice to this card's rolls.</summary>
        ModifyDiceCount = 12,

        /// <summary>特种橡胶: every shield this side gains is +N.</summary>
        ModifyShieldBonus = 13
    }

    /// <summary>Who or what an effect lands on.</summary>
    public enum EffectTarget
    {
        Enemy = 0,
        Self = 1,
        SelfCard = 2,
        RandomOwnCard = 3,
        RandomEnemyCard = 4,

        /// <summary>Both neighbours of the source card (发电机).</summary>
        AdjacentOwnCards = 5
    }

    public enum AmountKind
    {
        Constant = 0,
        Dice = 1,
        ValueScaledDice = 2
    }

    /// <summary>
    /// An immutable amount description. Value-scaled dice read the card's current value
    /// (铜锣烧 Xd4, 黄金战车 Xd6), so the same card grows as its value grows.
    /// </summary>
    public readonly struct EffectAmount
    {
        private EffectAmount(AmountKind kind, int count, int faces, int constant)
        {
            Kind = kind;
            Count = count;
            Faces = faces;
            Constant = constant;
        }

        public AmountKind Kind { get; }
        public int Count { get; }
        public int Faces { get; }
        public int Constant { get; }

        public static EffectAmount Flat(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return new EffectAmount(AmountKind.Constant, 0, 2, value);
        }

        public static EffectAmount Dice(int count, int faces)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (faces < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(faces));
            }

            return new EffectAmount(AmountKind.Dice, count, faces, 0);
        }

        /// <summary>XdF where X is the card's current value (minimum one die).</summary>
        public static EffectAmount ValueDice(int faces)
        {
            if (faces < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(faces));
            }

            return new EffectAmount(AmountKind.ValueScaledDice, 0, faces, 0);
        }

        public int DiceCountFor(int value)
        {
            switch (Kind)
            {
                case AmountKind.ValueScaledDice:
                    return Math.Max(1, value);
                case AmountKind.Dice:
                    return Count;
                default:
                    return 0;
            }
        }

        public bool IsDice => Kind != AmountKind.Constant;

        /// <summary>
        /// Rolls the amount. Per-die modifiers clamp inside [1, faces] as the rules require
        /// (d6 roll 6 with +1 is still 6; roll 1 with -1 is still 1).
        /// </summary>
        public int Roll(
            Pcg32 rng,
            int value,
            int perDieModifier = 0,
            bool forceMaximum = false,
            int extraDice = 0)
        {
            if (!IsDice)
            {
                return Constant;
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            int dice = DiceCountFor(value) + Math.Max(0, extraDice);
            int total = 0;
            for (int die = 0; die < dice; die++)
            {
                int rolled = forceMaximum
                    ? Faces
                    : rng.NextInt(1, Faces + 1) + perDieModifier;
                total += Math.Max(1, Math.Min(Faces, rolled));
            }

            return total;
        }
    }

    /// <summary>One atomic effect entry in a card's active or passive list.</summary>
    public sealed class CardEffect
    {
        public CardEffect(
            EffectOp operation,
            EffectAmount amount,
            EffectTarget target,
            CardTag requiredTag = CardTag.None,
            int cardCount = 1,
            int durationTicks = 0)
        {
            if (cardCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cardCount));
            }

            if (durationTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            }

            Operation = operation;
            Amount = amount;
            Target = target;
            RequiredTag = requiredTag;
            CardCount = cardCount;
            DurationTicks = durationTicks;
        }

        public EffectOp Operation { get; }
        public EffectAmount Amount { get; }
        public EffectTarget Target { get; }
        public CardTag RequiredTag { get; }
        public int CardCount { get; }
        public int DurationTicks { get; }

        public override string ToString()
        {
            return RequiredTag == CardTag.None
                ? $"{Operation}({Amount.Kind})"
                : $"{Operation}({Amount.Kind},{RequiredTag})";
        }
    }

    /// <summary>Events a passive can subscribe to. Mirrors the R-EVT-002 event list.</summary>
    public enum PassiveTrigger
    {
        BattleStart = 0,
        OwnCardCast = 1,
        AnyCardCast = 2,
        SlowApplied = 3,
        HasteApplied = 4,
        ChargeApplied = 5,
        BurnApplied = 6,
        DamageDealt = 7,
        DamageTaken = 8,
        ShieldGained = 9,

        /// <summary>棋盘层事件；战斗中不会触发（幸运饼干）。</summary>
        CardSold = 10
    }

    /// <summary>
    /// A passive watcher on the owner's card. Passives never occupy the cooldown loop and
    /// never fire from their own card being cast unless <see cref="OwnSourceOnly"/> is false.
    /// </summary>
    public sealed class PassiveDefinition
    {
        public PassiveDefinition(
            PassiveTrigger trigger,
            CardEffect effect,
            CardTag castTag = CardTag.None,
            bool ownSideOnly = true,
            bool requiresAdjacentSource = false)
        {
            Trigger = trigger;
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
            CastTag = castTag;
            OwnSideOnly = ownSideOnly;
            RequiresAdjacentSource = requiresAdjacentSource;
        }

        public PassiveTrigger Trigger { get; }
        public CardEffect Effect { get; }
        public CardTag CastTag { get; }
        public bool OwnSideOnly { get; }
        public bool RequiresAdjacentSource { get; }
    }
}
