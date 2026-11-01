using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectArtOfAll.Core.Cards
{
    public interface ICardCatalog
    {
        CardDefinition Get(string id);
        bool TryGet(string id, out CardDefinition definition);
        IReadOnlyList<CardDefinition> All { get; }
    }

    /// <summary>
    /// The whole first card set, imported from 卡牌设计稿\第一套卡牌.txt (30 cards).
    /// Every card is pure data: adding a 31st card must not require new C# code.
    /// </summary>
    public sealed class CardDatabase : ICardCatalog
    {
        public const string SourceFile = "卡牌设计稿/第一套卡牌.txt";

        private const int TicksPerSecond = 60;

        public static CardDatabase Instance { get; } =
            new CardDatabase(CreateDefinitions());

        private readonly IReadOnlyDictionary<string, CardDefinition> byId;

        public CardDatabase(IEnumerable<CardDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            List<CardDefinition> all = definitions.ToList();
            if (all.Count == 0)
            {
                throw new ArgumentException("The catalog cannot be empty.", nameof(definitions));
            }

            IGrouping<string, CardDefinition> duplicate = all
                .GroupBy(card => card.Id, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new ArgumentException(
                    $"Duplicate card id '{duplicate.Key}'.",
                    nameof(definitions));
            }

            byId = all.ToDictionary(card => card.Id, StringComparer.Ordinal);
            All = all.AsReadOnly();
        }

        public IReadOnlyList<CardDefinition> All { get; }

        public CardDefinition Get(string id)
        {
            if (!byId.TryGetValue(id ?? string.Empty, out CardDefinition definition))
            {
                throw new KeyNotFoundException($"Unknown card id '{id}'.");
            }

            return definition;
        }

        public bool TryGet(string id, out CardDefinition definition)
        {
            return byId.TryGetValue(id ?? string.Empty, out definition);
        }

        /// <summary>Default sale value: 1c = 1g, 2c = 2g, 3c = 4g (卡牌示例.txt).</summary>
        public static int DefaultValueForSize(int slotSize)
        {
            switch (slotSize)
            {
                case 1:
                    return 1;
                case 2:
                    return 2;
                default:
                    return 4;
            }
        }

        private static IReadOnlyList<CardDefinition> CreateDefinitions()
        {
            const CardScope battle = CardScope.BattleOnly;
            const CardScope both = CardScope.BagAndBattle;

            return new[]
            {
                // ── 一、工具 / 废料（21 张） ───────────────────────────────────────
                Card(
                    "shredder", "粉碎机", 3, 9, CardTag.Estate | CardTag.Weapon, 4, battle,
                    effects: new[] { Damage(1, 8) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.BattleStart,
                            ModifyDiceCount(1))
                    }),

                Card(
                    "generator", "发电机", 2, 3, CardTag.Estate | CardTag.Tool, 2, battle,
                    effects: new[] { Charge(1, EffectTarget.AdjacentOwnCards) }),

                Card(
                    "concrete_block", "混凝土块", 1, 3, CardTag.Waste | CardTag.Weapon, 1, battle,
                    effects: new[] { Damage(1, 4), Slow(1, 1) }),

                Card(
                    "crowbar", "撬棍", 1, 3, CardTag.Weapon | CardTag.Tool, 1, battle,
                    effects: new[] { Damage(1, 6) }),

                Card(
                    "fireman_axe", "消防斧", 2, 6, CardTag.Weapon | CardTag.Tool, 2, battle,
                    effects: new[] { Damage(1, 20) }),

                Card(
                    "car_door", "汽车门板", 2, 9, CardTag.Waste, 2, battle,
                    effects: new[] { Shield(2, 20) }),

                Card(
                    "nuclear_water", "核废水", 1, 4, CardTag.Waste, 1, battle,
                    effects: new[] { Status(EffectOp.ApplyPoison, 1, 4) }),

                Card(
                    "oil_barrel", "油桶", 2, 6, CardTag.Waste, 2, battle,
                    effects: new[] { Status(EffectOp.ApplyBurn, 1, 6) }),

                Card(
                    "plastic_shed", "塑料棚", 3, 6, CardTag.Waste, 4, battle,
                    effects: new[] { Slow(2, 3) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.SlowApplied,
                            Shield(1, 6))
                    }),

                Card(
                    "special_rubber", "特种橡胶", 1, 0, CardTag.Waste, 1, both,
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.BurnApplied,
                            ShieldBonus(1))
                    }),

                Card(
                    "whetstone", "磨刀石", 1, 0, CardTag.Waste, 1, both,
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.AllAdjacent,
                            AuraStat.DiceFlatModifier,
                            2,
                            CardTag.Weapon)
                    }),

                Card(
                    "duct_tape", "胶带", 1, 0, CardTag.Waste, 1, both,
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.AnyCardCast,
                            Shield(1, 4),
                            requiresAdjacentSource: true)
                    }),

                Card(
                    "molotov", "燃烧瓶", 1, 6, CardTag.Waste, 1, battle,
                    effects: new[]
                    {
                        Status(EffectOp.ApplyBurn, 1, 6),
                        Slow(1, 2),
                        DestroySelf()
                    }),

                Card(
                    "lighter", "打火机", 1, 5, CardTag.Tool, 1, battle,
                    effects: new[] { Status(EffectOp.ApplyBurn, 1, 4) }),

                Card(
                    "campfire", "火堆", 2, 8, CardTag.Waste | CardTag.Tool, 2, battle,
                    effects: new[] { Status(EffectOp.ApplyBurn, 1, 4) },
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.Self,
                            AuraStat.MultiCast,
                            1,
                            CardTag.Waste,
                            AuraScaling.PerAdjacentMatchingTag)
                    }),

                Card(
                    "lubricant", "润滑油", 1, 5, CardTag.Waste, 1, battle,
                    effects: new[] { Haste(3, 1, CardTag.Tool) }),

                Card(
                    "barbed_wire", "铁丝网", 2, 6, CardTag.Trap, 2, battle,
                    effects: new[] { Slow(1, 2) }),

                Card(
                    "heat_cutter", "热熔切割器", 2, 12, CardTag.Weapon | CardTag.Tool, 2, battle,
                    effects: new[]
                    {
                        Damage(1, 6),
                        Status(EffectOp.ApplyBurn, 1, 4)
                    },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.SlowApplied,
                            ModifyDiceCount(1))
                    }),

                Card(
                    "recycler", "回收站", 3, 0, CardTag.Estate, 4, both,
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.AllByTag,
                            AuraStat.MultiCast,
                            1,
                            CardTag.Waste)
                    }),

                Card(
                    "snow_purifier", "雪水净化器", 2, 8, CardTag.Estate | CardTag.Waste, 2, battle,
                    effects: new[] { Heal(1, 20) }),

                Card(
                    "fresh_water", "淡水", 1, 5, CardTag.Waste, 0, both,
                    effects: new[] { Heal(1, 4) }),

                // ── 二、食物 / 伙伴（7 张） ────────────────────────────────────────
                Card(
                    "mineral_water", "矿泉水", 1, 3, CardTag.Food, 1, both,
                    effects: new[] { Heal(1, 6) }),

                Card(
                    "chocolate", "巧克力", 1, 5, CardTag.Food, 1, both,
                    effects: new[]
                    {
                        Haste(2, 1),
                        Slow(1, 1)
                    }),

                Card(
                    "hardtack", "压缩饼干", 1, 3, CardTag.Food, 1, both,
                    effects: new[] { Status(EffectOp.ApplyRegeneration, 1, 4) }),

                Card(
                    "baguette", "法棍", 1, 6, CardTag.Food | CardTag.Weapon, 1, both,
                    effects: new[]
                    {
                        Damage(1, 6),
                        Heal(1, 6)
                    }),

                Card(
                    "dorayaki", "铜锣烧", 1, 6, CardTag.Food, 1, both,
                    effects: new[]
                    {
                        new CardEffect(
                            EffectOp.Heal,
                            EffectAmount.ValueDice(4),
                            EffectTarget.Self)
                    }),

                Card(
                    "fortune_cookie", "幸运饼干", 1, 0, CardTag.None, 1, both,
                    passives: new[]
                    {
                        // 棋盘层被动：出售时最左侧食物永久 +1 修正。战斗中不触发。
                        new PassiveDefinition(
                            PassiveTrigger.CardSold,
                            new CardEffect(
                                EffectOp.ModifyValue,
                                EffectAmount.Flat(1),
                                EffectTarget.Self))
                    }),

                Card(
                    "doggy", "小汪", 2, 8, CardTag.Companion, 2, battle,
                    effects: new[] { Damage(1, 8) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.OwnCardCast,
                            Charge(2, EffectTarget.SelfCard),
                            CardTag.Food)
                    }),

                // ── 三、特殊（2 张） ──────────────────────────────────────────────
                Card(
                    "ouroboros", "自噬之蛇", 1, 0, CardTag.Companion, 1, both,
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.AdjacentRight,
                            AuraStat.MaxRoll,
                            1)
                    }),

                Card(
                    "golden_chariot", "黄金战车", 3, 12, CardTag.Weapon, 4, battle,
                    effects: new[]
                    {
                        new CardEffect(
                            EffectOp.DealDamage,
                            EffectAmount.ValueDice(6),
                            EffectTarget.Enemy)
                    })
            };
        }

        // ── 构造辅助：让卡表读起来像卡面 ─────────────────────────────────────

        private static CardDefinition Card(
            string id,
            string name,
            int slotSize,
            int cooldownSeconds,
            CardTag tags,
            int value,
            CardScope scope,
            CardEffect[] effects = null,
            AuraDefinition[] auras = null,
            PassiveDefinition[] passives = null)
        {
            return new CardDefinition(
                id,
                name,
                slotSize,
                cooldownSeconds * TicksPerSecond,
                tags,
                value,
                scope,
                effects ?? Array.Empty<CardEffect>(),
                auras,
                passives);
        }

        private static CardEffect Damage(int count, int faces)
        {
            return new CardEffect(
                EffectOp.DealDamage,
                EffectAmount.Dice(count, faces),
                EffectTarget.Enemy);
        }

        private static CardEffect Shield(int count, int faces)
        {
            return new CardEffect(
                EffectOp.GainShield,
                EffectAmount.Dice(count, faces),
                EffectTarget.Self);
        }

        private static CardEffect Heal(int count, int faces)
        {
            return new CardEffect(
                EffectOp.Heal,
                EffectAmount.Dice(count, faces),
                EffectTarget.Self);
        }

        private static CardEffect Status(EffectOp operation, int count, int faces)
        {
            return new CardEffect(
                operation,
                EffectAmount.Dice(count, faces),
                EffectTarget.Enemy);
        }

        /// <summary>缓速对方 <paramref name="count"/> 张牌 <paramref name="seconds"/> 秒。</summary>
        private static CardEffect Slow(int seconds, int count)
        {
            return new CardEffect(
                EffectOp.Slow,
                EffectAmount.Flat(count),
                EffectTarget.RandomEnemyCard,
                cardCount: count,
                durationTicks: seconds * TicksPerSecond);
        }

        private static CardEffect Haste(
            int seconds,
            int count,
            CardTag requiredTag = CardTag.None)
        {
            return new CardEffect(
                EffectOp.Haste,
                EffectAmount.Flat(count),
                EffectTarget.RandomOwnCard,
                requiredTag,
                count,
                seconds * TicksPerSecond);
        }

        private static CardEffect Charge(int seconds, EffectTarget target)
        {
            return new CardEffect(
                EffectOp.Charge,
                EffectAmount.Flat(seconds * TicksPerSecond),
                target);
        }

        private static CardEffect DestroySelf()
        {
            return new CardEffect(
                EffectOp.DestroySelf,
                EffectAmount.Flat(1),
                EffectTarget.SelfCard);
        }

        private static CardEffect ModifyDiceCount(int amount)
        {
            return new CardEffect(
                EffectOp.ModifyDiceCount,
                EffectAmount.Flat(amount),
                EffectTarget.SelfCard);
        }

        private static CardEffect ShieldBonus(int amount)
        {
            return new CardEffect(
                EffectOp.ModifyShieldBonus,
                EffectAmount.Flat(amount),
                EffectTarget.Self);
        }
    }
}
