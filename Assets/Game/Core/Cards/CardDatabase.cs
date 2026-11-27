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
    /// The full card catalog, imported from 卡牌设计稿\第一套卡牌.txt and 第二套卡牌.txt
    /// plus the event-only 神秘饮料. Every card is pure data.
    ///
    /// 卡面写「1d4/1d6」的地方是同一张牌的两级：基础版用斜杠前的数值，升级版用斜杠后的
    /// 数值，升级版 id 是基础版 id 加一个 "+"、名字加一个 "+"（CJ-5.11 融合升级）。
    /// 一张牌只能升级一次，所以升级版自己不再带升级版。
    /// </summary>
    public sealed class CardDatabase : ICardCatalog
    {
        public const string SourceFile = "卡牌设计稿/第一套卡牌.txt";
        public const string SecondSourceFile = "卡牌设计稿/第二套卡牌.txt";

        private const int TicksPerSecond = 60;

        public static CardDatabase Instance { get; } =
            new CardDatabase(CreateDefinitions());

        private readonly IReadOnlyDictionary<string, CardDefinition> byId;
        private readonly List<CardDefinition> allDefinitions;

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

            allDefinitions = all;
            byId = all.ToDictionary(card => card.Id, StringComparer.Ordinal);
            All = all.Where(card => !card.IsUpgraded).ToList().AsReadOnly();
        }

        /// <summary>可以出现在商店 / 奖励里的基础牌（升级版不在池子里）。</summary>
        public IReadOnlyList<CardDefinition> All { get; }

        /// <summary>基础牌 + 升级版，含 id 查找。</summary>
        public IReadOnlyList<CardDefinition> AllDefinitions => allDefinitions.AsReadOnly();

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

        private static List<CardDefinition> CreateDefinitions()
        {
            const CardScope battle = CardScope.BattleOnly;
            const CardScope both = CardScope.BagAndBattle;

            List<CardDefinition> all = new List<CardDefinition>();

            // ── 一、工具 / 废料（22 张） ───────────────────────────────────────
            // 粉碎机：升级只加伤害；「每场战斗开始摧毁背包最左侧的废料，永久 +1 骰数」
            // 是棋盘层被动，由 BoardCardEconomy 在开战前结算。
            Add(
                all,
                Card(
                    "shredder", "粉碎机", 3, 9, CardTag.Estate | CardTag.Weapon, 4, battle,
                    effects: new[] { Damage(1, 8) }),
                new Upgrade(effects: new[] { Damage(1, 12) }));

            Add(
                all,
                Card(
                    "generator", "发电机", 2, 3, CardTag.Estate | CardTag.Tool, 2, battle,
                    effects: new[] { Charge(1, EffectTarget.AdjacentOwnCards) }),
                new Upgrade(effects: new[] { Charge(2, EffectTarget.AdjacentOwnCards) }));

            Add(
                all,
                Card(
                    "concrete_block", "混凝土块", 1, 3, CardTag.Waste | CardTag.Weapon, 1, battle,
                    effects: new[] { Damage(1, 4), Slow(1, 1) }),
                new Upgrade(effects: new[] { Damage(1, 4), Slow(1, 2) }));

            Add(
                all,
                Card(
                    "crowbar", "撬棍", 1, 3, CardTag.Weapon | CardTag.Tool, 1, battle,
                    effects: new[] { Damage(1, 6) }),
                new Upgrade(cooldownSeconds: 2));

            Add(
                all,
                Card(
                    "fireman_axe", "消防斧", 2, 6, CardTag.Weapon | CardTag.Tool, 2, battle,
                    effects: new[] { Damage(1, 20) }),
                new Upgrade(cooldownSeconds: 4));

            Add(
                all,
                Card(
                    "car_door", "汽车门板", 2, 9, CardTag.Waste, 2, battle,
                    effects: new[] { Shield(2, 20) }),
                new Upgrade(effects: new[] { Shield(3, 20) }));

            Add(
                all,
                Card(
                    "nuclear_water", "核废水", 1, 4, CardTag.Waste, 1, battle,
                    effects: new[] { Status(EffectOp.ApplyPoison, 1, 4) }),
                new Upgrade(effects: new[] { Status(EffectOp.ApplyPoison, 1, 6) }));

            Add(
                all,
                Card(
                    "oil_barrel", "油桶", 2, 6, CardTag.Waste, 2, battle,
                    effects: new[] { Status(EffectOp.ApplyBurn, 1, 6) }),
                new Upgrade(effects: new[] { Status(EffectOp.ApplyBurn, 1, 10) }));

            // 塑料棚：主动缓速 3 张牌 2 秒，等于触发 3 次缓速，所以被动会给自己 3 次护盾。
            Add(
                all,
                Card(
                    "plastic_shed", "塑料棚", 3, 6, CardTag.Waste, 4, battle,
                    effects: new[] { Slow(2, 3) },
                    passives: new[]
                    {
                        new PassiveDefinition(PassiveTrigger.SlowApplied, Shield(1, 6))
                    }),
                new Upgrade(passives: new[]
                {
                    new PassiveDefinition(PassiveTrigger.SlowApplied, Shield(1, 10))
                }));

            Add(
                all,
                Card(
                    "special_rubber", "特种橡胶", 1, 0, CardTag.Waste, 1, both,
                    passives: new[]
                    {
                        new PassiveDefinition(PassiveTrigger.BurnApplied, ShieldBonus(1))
                    }),
                new Upgrade(passives: new[]
                {
                    new PassiveDefinition(PassiveTrigger.BurnApplied, ShieldBonus(2))
                }));

            Add(
                all,
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
                new Upgrade(auras: new[]
                {
                    new AuraDefinition(
                        AuraTarget.AllAdjacent,
                        AuraStat.DiceFlatModifier,
                        4,
                        CardTag.Weapon)
                }));

            Add(
                all,
                Card(
                    "duct_tape", "胶带", 1, 0, CardTag.Waste, 1, both,
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.AnyCardCast,
                            Shield(1, 4),
                            requiresAdjacentSource: true)
                    }),
                new Upgrade(passives: new[]
                {
                    new PassiveDefinition(
                        PassiveTrigger.AnyCardCast,
                        Shield(1, 6),
                        requiresAdjacentSource: true)
                }));

            Add(
                all,
                Card(
                    "molotov", "燃烧瓶", 1, 6, CardTag.Waste, 1, battle,
                    effects: new[]
                    {
                        Status(EffectOp.ApplyBurn, 1, 6),
                        Slow(1, 2),
                        DestroySelf()
                    }),
                new Upgrade(effects: new[]
                {
                    Status(EffectOp.ApplyBurn, 1, 8),
                    Slow(2, 2),
                    DestroySelf()
                }));

            Add(
                all,
                Card(
                    "lighter", "打火机", 1, 5, CardTag.Tool, 1, battle,
                    effects: new[] { Status(EffectOp.ApplyBurn, 1, 4) }),
                new Upgrade(effects: new[] { Status(EffectOp.ApplyBurn, 1, 6) }));

            Add(
                all,
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
                new Upgrade(effects: new[] { Status(EffectOp.ApplyBurn, 1, 6) }));

            Add(
                all,
                Card(
                    "lubricant", "润滑油", 1, 5, CardTag.Waste, 1, battle,
                    effects: new[] { Haste(3, 1, CardTag.Tool) }),
                new Upgrade(effects: new[] { Haste(3, 2, CardTag.Tool) }));

            // 铁丝网：出售时放陷阱是棋盘层被动，陷阱系统尚未实现（见核对表）。
            Add(
                all,
                Card(
                    "barbed_wire", "铁丝网", 2, 6, CardTag.Trap, 2, battle,
                    effects: new[] { Slow(1, 2) }),
                new Upgrade(effects: new[] { Slow(1, 3) }));

            Add(
                all,
                Card(
                    "heat_cutter", "热熔切割器", 2, 12, CardTag.Weapon | CardTag.Tool, 2, battle,
                    effects: new[]
                    {
                        Damage(1, 6),
                        Status(EffectOp.ApplyBurn, 1, 4)
                    },
                    passives: new[]
                    {
                        new PassiveDefinition(PassiveTrigger.SlowApplied, ModifyDiceCount(1))
                    }),
                new Upgrade(effects: new[]
                {
                    Damage(1, 8),
                    Status(EffectOp.ApplyBurn, 1, 6)
                }));

            // 回收站：废料多重释放 +1；废料价值 +1/2/3 由 BoardCardEconomy 结算（升级版翻倍）。
            // 设计稿只写了「升级版对废料牌的价值加成翻倍」，所以升级版的光环与基础版相同。
            Add(
                all,
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
                new Upgrade(auras: new[]
                {
                    new AuraDefinition(
                        AuraTarget.AllByTag,
                        AuraStat.MultiCast,
                        1,
                        CardTag.Waste)
                }));

            // 雪水净化器：设计稿没写升级版（只有被动产出的「淡水/淡水+」），所以不能融合升级。
            Add(
                all,
                Card(
                    "snow_purifier", "雪水净化器", 2, 8, CardTag.Estate | CardTag.Waste, 2, battle,
                    effects: new[] { Heal(1, 20) }));

            // 淡水：出售时给最左侧的恢复牌 +1/+2 永久修正（棋盘层，BoardCardEconomy）。
            Add(
                all,
                Card(
                    "fresh_water", "淡水", 1, 5, CardTag.Waste, 0, battle,
                    effects: new[] { Heal(1, 4) }),
                new Upgrade(effects: new[] { Heal(1, 4) }));

            // 路障：出售时放陷阱，经过时强制停留（3/4 次后失效）——陷阱系统未实现。
            Add(
                all,
                Card(
                    "roadblock", "路障", 2, 6, CardTag.Waste | CardTag.Estate, 2, battle,
                    effects: new[] { Shield(1, 12) }),
                new Upgrade(effects: new[] { Shield(1, 20) }));

            // ── 二、食物 / 伙伴（10 张） ──────────────────────────────────────
            Add(
                all,
                Card(
                    "mineral_water", "矿泉水", 1, 3, CardTag.Food, 1, battle,
                    effects: new[] { Heal(1, 6) }),
                new Upgrade(cooldownSeconds: 2));

            Add(
                all,
                Card(
                    "chocolate", "巧克力", 1, 5, CardTag.Food, 1, battle,
                    effects: new[]
                    {
                        Haste(2, 1),
                        Slow(1, 1)
                    }),
                new Upgrade(cooldownSeconds: 3));

            Add(
                all,
                Card(
                    "hardtack", "压缩饼干", 1, 3, CardTag.Food, 1, battle,
                    effects: new[] { Regeneration(1, 4) }),
                new Upgrade(effects: new[] { Regeneration(1, 8) }));

            Add(
                all,
                Card(
                    "baguette", "法棍", 1, 6, CardTag.Food | CardTag.Weapon, 1, battle,
                    effects: new[]
                    {
                        Damage(1, 6),
                        Heal(1, 6)
                    }),
                new Upgrade(effects: new[]
                {
                    Damage(1, 10),
                    Heal(1, 10)
                }));

            // 铜锣烧：恢复 Xd4 / Xd6，X 为当前价值；每轮结束价值 +1（棋盘层）。
            Add(
                all,
                Card(
                    "dorayaki", "铜锣烧", 1, 6, CardTag.Food, 1, battle,
                    effects: new[]
                    {
                        new CardEffect(
                            EffectOp.Heal,
                            EffectAmount.ValueDice(4),
                            EffectTarget.Self)
                    }),
                new Upgrade(effects: new[]
                {
                    new CardEffect(
                        EffectOp.Heal,
                        EffectAmount.ValueDice(6),
                        EffectTarget.Self)
                }));

            // 幸运饼干：出售时给最左侧的食物 +1/+2 永久修正（棋盘层，BoardCardEconomy）。
            Add(
                all,
                Card(
                    "fortune_cookie", "幸运饼干", 1, 0, CardTag.Food, 1, both,
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.CardSold,
                            new CardEffect(
                                EffectOp.ModifyValue,
                                EffectAmount.Flat(1),
                                EffectTarget.Self))
                    }),
                new Upgrade(passives: new[]
                {
                    new PassiveDefinition(
                        PassiveTrigger.CardSold,
                        new CardEffect(
                            EffectOp.ModifyValue,
                            EffectAmount.Flat(2),
                            EffectTarget.Self))
                }));

            Add(
                all,
                Card(
                    "doggy", "小汪", 2, 8, CardTag.Companion, 2, battle,
                    effects: new[] { Damage(1, 20) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.OwnCardCast,
                            Charge(2, EffectTarget.SelfCard),
                            CardTag.Food)
                    }),
                new Upgrade(cooldownSeconds: 6));

            // 鲨旋风：出手同时给己方所有武器 +2/+3 伤害修正。
            Add(
                all,
                Card(
                    "shark_cyclone", "鲨旋风", 2, 6, CardTag.Weapon | CardTag.Companion, 2, battle,
                    effects: new[] { Damage(1, 6) },
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.AllByTag,
                            AuraStat.DiceFlatModifier,
                            2,
                            CardTag.Weapon)
                    }),
                new Upgrade(auras: new[]
                {
                    new AuraDefinition(
                        AuraTarget.AllByTag,
                        AuraStat.DiceFlatModifier,
                        3,
                        CardTag.Weapon)
                }));

            Add(
                all,
                Card(
                    "blade", "刀片", 1, 6, CardTag.Weapon, 1, battle,
                    effects: new[] { Damage(2, 6) }),
                new Upgrade(effects: new[] { Damage(3, 6) }));

            // 企鹅：其他伙伴或食物施放时给自己充能 1 秒（自己施放不算）。
            Add(
                all,
                Card(
                    "penguin", "企鹅", 1, 7, CardTag.Weapon | CardTag.Companion, 1, battle,
                    effects: new[] { Damage(1, 12) },
                    passives: new[]
                    {
                        PenguinCharge(CardTag.Companion),
                        PenguinCharge(CardTag.Food)
                    }),
                new Upgrade(cooldownSeconds: 5));

            // ── 三、特殊（2 张） ──────────────────────────────────────────────
            // 自噬之蛇：右侧相邻牌所有数值 +100 修正（升级后左右都吃）。
            Add(
                all,
                Card(
                    "ouroboros", "自噬之蛇", 1, 0, CardTag.Companion, 1, both,
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.AdjacentRight,
                            AuraStat.ValueBonus,
                            100)
                    }),
                new Upgrade(auras: new[]
                {
                    new AuraDefinition(
                        AuraTarget.AllAdjacent,
                        AuraStat.ValueBonus,
                        100)
                }));

            // 黄金战车：Xd6 / Xd10 伤害，X 为当前价值；每次出售卡牌使它的价值 +1（棋盘层）。
            Add(
                all,
                Card(
                    "golden_chariot", "黄金战车", 3, 12, CardTag.Weapon, 4, battle,
                    effects: new[]
                    {
                        new CardEffect(
                            EffectOp.DealDamage,
                            EffectAmount.ValueDice(6),
                            EffectTarget.Enemy)
                    }),
                new Upgrade(effects: new[]
                {
                    new CardEffect(
                        EffectOp.DealDamage,
                        EffectAmount.ValueDice(10),
                        EffectTarget.Enemy)
                }));

            // ── 四、吉祥物 / 价值系列 ───────────────────────────────────────
            Add(
                all,
                Card(
                    "six_six_streak", "六六大顺", 2, 6, CardTag.Mascot, 2, battle,
                    effects: new[] { Shield(2, 20) },
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.Self,
                            AuraStat.ShieldBonus,
                            1,
                            scaling: AuraScaling.SelfAndAdjacentValue)
                    }));

            Add(
                all,
                Card(
                    "placeholder_7", "占位符7", 1, 6, CardTag.Mascot, 2, battle,
                    effects: new[]
                    {
                        new CardEffect(
                            EffectOp.ApplyRegeneration,
                            EffectAmount.FromCardValue(),
                            EffectTarget.Self)
                    }));

            // ── 五、第二套：武器 / 废料 / 伙伴 / 疾病 ──────────────────────
            Add(
                all,
                Card(
                    "rusted_blade", "锈刃", 3, 5, CardTag.Weapon, 4, battle,
                    effects: new[] { Damage(1, 10) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.DamageDealt,
                            Status(EffectOp.ApplyPoison, 1, 2))
                    }),
                new Upgrade(passives: new[]
                {
                    new PassiveDefinition(
                        PassiveTrigger.DamageDealt,
                        Status(EffectOp.ApplyPoison, 1, 4))
                }));

            Add(
                all,
                Card(
                    "compound", "化合物", 2, 4, CardTag.Waste, 2, battle,
                    effects: new[] { Status(EffectOp.ApplyPoison, 1, 6) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.HasteApplied,
                            ModifyDiceModifier(1),
                            targetCardOnly: true)
                    }));

            Add(
                all,
                Card(
                    "torch", "火炬", 2, 4, CardTag.Waste, 2, battle,
                    effects: new[] { Status(EffectOp.ApplyBurn, 1, 6) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.OwnCardCast,
                            ModifyDiceModifier(1),
                            sourceCardOnly: true)
                    }),
                new Upgrade(effects: new[] { Status(EffectOp.ApplyBurn, 1, 10) }));

            Add(
                all,
                Card(
                    "catalyst", "催化剂", 1, 5, CardTag.Waste, 1, battle,
                    effects: new[] { Haste(1, 2) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.PoisonApplied,
                            Charge(1, EffectTarget.SelfCard)),
                        new PassiveDefinition(
                            PassiveTrigger.BurnApplied,
                            Charge(1, EffectTarget.SelfCard))
                    }),
                new Upgrade(effects: new[] { Haste(1, 3) }));

            Add(
                all,
                Card(
                    "placeholder_1", "占位符1", 1, 5,
                    CardTag.Weapon | CardTag.Companion, 1, battle,
                    effects: new[] { Damage(1, 8) },
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.Self,
                            AuraStat.Lifesteal,
                            1)
                    }),
                new Upgrade(effects: new[] { Damage(1, 12) }));

            Add(
                all,
                Card(
                    "placeholder_2", "占位符2", 2, 0, CardTag.Tool, 2, battle,
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.AdjacentRight,
                            AuraStat.Lifesteal,
                            1,
                            CardTag.Weapon)
                    }),
                new Upgrade(auras: new[]
                {
                    new AuraDefinition(
                        AuraTarget.AllAdjacent,
                        AuraStat.Lifesteal,
                        1,
                        CardTag.Weapon)
                }));

            Add(
                all,
                Card(
                    "placeholder_3", "占位符3", 3, 10, CardTag.Weapon, 4, battle,
                    effects: new[] { Damage(1, 100) },
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.Self,
                            AuraStat.DiceFlatModifier,
                            10,
                            CardTag.Weapon,
                            AuraScaling.PerOtherMatchingTag)
                    }),
                new Upgrade(cooldownSeconds: 8));

            Add(
                all,
                Card(
                    "placeholder_4", "占位符4", 3, 7,
                    CardTag.Weapon | CardTag.Companion, 4, battle,
                    effects: new[]
                    {
                        Damage(1, 20),
                        Haste(1, 1)
                    },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.EnemyCardCast,
                            Charge(2, EffectTarget.SelfCard))
                    }),
                new Upgrade(cooldownSeconds: 5));

            Add(
                all,
                Card(
                    "chainsaw", "链锯", 3, 6, CardTag.Weapon | CardTag.Tool, 4, battle,
                    effects: new[] { Damage(2, 20) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.OwnCardCast,
                            HalveCurrentCooldown(),
                            sourceCardOnly: true,
                            oncePerBattle: true)
                    }));

            Add(
                all,
                Card(
                    "nail_gun", "射钉枪", 2, 3, CardTag.Weapon | CardTag.Tool, 2, battle,
                    effects: new[]
                    {
                        Damage(1, 6),
                        Slow(1, 1)
                    }),
                new Upgrade(effects: new[]
                {
                    Damage(1, 8),
                    Slow(1, 2)
                }));

            // 设计稿没有给占位符5词条；它是护盾支援牌，暂归“工具”。
            Add(
                all,
                Card(
                    "placeholder_5", "占位符5", 2, 5, CardTag.Tool, 2, battle,
                    effects: new[] { Shield(1, 20) },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.OwnCardCast,
                            ModifyCardShieldBonus(2),
                            requiresAdjacentSource: true)
                    }),
                new Upgrade(passives: new[]
                {
                    new PassiveDefinition(
                        PassiveTrigger.OwnCardCast,
                        ModifyCardShieldBonus(4),
                        requiresAdjacentSource: true)
                }));

            Add(
                all,
                Card(
                    "titan", "泰坦", 3, 25, CardTag.Weapon | CardTag.Companion, 4, battle,
                    effects: new[] { Damage(2, 100) },
                    passives: TitanPassives(4)),
                new Upgrade(passives: TitanPassives(6)));

            Add(
                all,
                Card(
                    "fruit_knife", "水果刀", 1, 6, CardTag.Tool | CardTag.Weapon, 1, battle,
                    effects: new[] { Damage(1, 12) },
                    auras: new[]
                    {
                        new AuraDefinition(
                            AuraTarget.Self,
                            AuraStat.DiceFlatModifier,
                            1,
                            scaling: AuraScaling.PerOwnerRegeneration)
                    }));

            Add(
                all,
                Card(
                    "food_poisoning", "食物中毒", 2, 6, CardTag.Disease, 2, battle,
                    effects: new[]
                    {
                        new CardEffect(
                            EffectOp.Charge,
                            EffectAmount.Flat(2 * TicksPerSecond),
                            EffectTarget.AdjacentOwnCards,
                            CardTag.Food)
                    },
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.OwnCardCast,
                            Status(EffectOp.ApplyPoison, 1, 2),
                            CardTag.Food)
                    }));

            // 事件「神秘饮料」打包带走的那瓶：战斗栏里开局回 8 点生命，也可以直接卖掉（R-EVT-002）。
            // 没有设计升级版，所以同名第二瓶不会融合。
            all.Add(
                Card(
                    "mystery_drink", "神秘饮料", 2, 0, CardTag.Food, 8, both,
                    passives: new[]
                    {
                        new PassiveDefinition(
                            PassiveTrigger.BattleStart,
                            new CardEffect(
                                EffectOp.Heal,
                                EffectAmount.Flat(8),
                                EffectTarget.Self))
                    }));

            return all;
        }

        // ── 构造辅助：让卡表读起来像卡面 ─────────────────────────────────────

        /// <summary>一张牌的两级：只写变化的部分，其余从基础版继承。</summary>
        private sealed class Upgrade
        {
            public Upgrade(
                int cooldownSeconds = -1,
                CardEffect[] effects = null,
                AuraDefinition[] auras = null,
                PassiveDefinition[] passives = null)
            {
                CooldownSeconds = cooldownSeconds;
                Effects = effects;
                Auras = auras;
                Passives = passives;
            }

            public int CooldownSeconds { get; }
            public CardEffect[] Effects { get; }
            public AuraDefinition[] Auras { get; }
            public PassiveDefinition[] Passives { get; }
        }

        private static void Add(
            List<CardDefinition> all,
            CardDefinition baseCard,
            Upgrade upgrade = null)
        {
            all.Add(baseCard);
            if (upgrade == null)
            {
                return;
            }

            CardDefinition upgraded = new CardDefinition(
                baseCard.Id + "+",
                baseCard.DisplayName + "+",
                baseCard.SlotSize,
                upgrade.CooldownSeconds >= 0
                    ? upgrade.CooldownSeconds * TicksPerSecond
                    : baseCard.CooldownTicks,
                baseCard.Tags,
                baseCard.Value,
                baseCard.Scope,
                upgrade.Effects ?? baseCard.ActiveEffects,
                upgrade.Auras ?? baseCard.Auras,
                upgrade.Passives ?? baseCard.Passives,
                baseId: baseCard.Id,
                isUpgraded: true);
            baseCard.LinkUpgrade(upgraded);
            all.Add(upgraded);
        }

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

        /// <summary>中毒 / 灼烧这类打到对手身上的状态。</summary>
        private static CardEffect Status(EffectOp operation, int count, int faces)
        {
            return new CardEffect(
                operation,
                EffectAmount.Dice(count, faces),
                EffectTarget.Enemy);
        }

        /// <summary>再生这类加到自己身上的状态。</summary>
        private static CardEffect Regeneration(int count, int faces)
        {
            return new CardEffect(
                EffectOp.ApplyRegeneration,
                EffectAmount.Dice(count, faces),
                EffectTarget.Self);
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

        private static CardEffect ModifyDiceModifier(int amount)
        {
            return new CardEffect(
                EffectOp.ModifyDiceModifier,
                EffectAmount.Flat(amount),
                EffectTarget.SelfCard);
        }

        private static CardEffect ModifyCardShieldBonus(int amount)
        {
            return new CardEffect(
                EffectOp.ModifyCardShieldBonus,
                EffectAmount.Flat(amount),
                EffectTarget.SelfCard);
        }

        private static CardEffect HalveCurrentCooldown()
        {
            return new CardEffect(
                EffectOp.HalveCurrentCooldownOnce,
                EffectAmount.Flat(1),
                EffectTarget.SelfCard);
        }

        private static PassiveDefinition[] TitanPassives(int diceModifier)
        {
            PassiveTrigger[] triggers =
            {
                PassiveTrigger.PoisonApplied,
                PassiveTrigger.BurnApplied,
                PassiveTrigger.HasteApplied,
                PassiveTrigger.SlowApplied
            };
            PassiveDefinition[] passives =
                new PassiveDefinition[triggers.Length * 2];
            for (int index = 0; index < triggers.Length; index++)
            {
                passives[index * 2] = new PassiveDefinition(
                    triggers[index],
                    Charge(2, EffectTarget.SelfCard));
                passives[index * 2 + 1] = new PassiveDefinition(
                    triggers[index],
                    ModifyDiceModifier(diceModifier));
            }

            return passives;
        }

        private static PassiveDefinition PenguinCharge(CardTag tag)
        {
            return new PassiveDefinition(
                PassiveTrigger.OwnCardCast,
                Charge(1, EffectTarget.SelfCard),
                tag,
                excludesSource: true);
        }
    }
}
