using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProjectArtOfAll.Core.Rng;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// 事件格上出现的阶段事件。阶段池由 <see cref="MatchEventCatalog.PickForPhase"/> 决定。
    /// </summary>
    public enum MatchEventKind
    {
        Expedition = 0,
        MysteryDrink = 1,
        UnknownTrail = 2,
        Raid = 3,
        Biovirus = 4
    }

    /// <summary>选项结算后要做的事。数值由 <see cref="MatchEventOption"/> 上的字段解释。</summary>
    public enum EventEffectType
    {
        /// <summary>随机获得 <see cref="MatchEventOption.Count"/> 张 <see cref="MatchEventOption.SlotSize"/> 格的牌。</summary>
        RandomCards = 0,

        /// <summary>接下来 <see cref="MatchEventOption.Battles"/> 场战斗开始时，获得相当于最大生命值 <see cref="MatchEventOption.Amount"/>% 的护盾。</summary>
        BattleShield = 1,

        /// <summary>获得一张 <see cref="MatchEventOption.CardId"/> 指定的牌。</summary>
        GainCard = 2,

        /// <summary>接下来 <see cref="MatchEventOption.Battles"/> 场战斗里，伤害/格挡/恢复各 +Amount，状态数值 +StatusAmount。</summary>
        BattlePower = 3,

        /// <summary>传送到随机地块，不触发额外移动。</summary>
        RandomTeleport = 4,

        /// <summary>支付后进入指定地块选择。</summary>
        TeleportTarget = 5,

        /// <summary>获得一张事件牌，并开启若干完整回合的杀戮时间。</summary>
        KillTime = 6,

        /// <summary>失去胜场并获得碎片。</summary>
        LoseWinGainShards = 7,

        /// <summary>一次获得多张指定牌。</summary>
        GainSpecificCards = 8,

        /// <summary>失去当前等级倍率的碎片。</summary>
        LoseShardsByLevel = 9
    }

    /// <summary>事件面板上的一个选项。费用与效果都写在数据里，UI 只负责展示。</summary>
    public sealed class MatchEventOption
    {
        public MatchEventOption(
            string label,
            string description,
            int shardCost,
            EventEffectType effect,
            int slotSize = 1,
            int count = 1,
            int amount = 0,
            int battles = 1,
            string cardId = null,
            int shardCostPerLevel = 0,
            int winsCost = 0,
            int rounds = 0,
            string[] cardIds = null,
            int cardWidth = -1,
            int statusAmount = 0)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("An event option needs a label.", nameof(label));
            }

            if (shardCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shardCost));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (battles <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(battles));
            }

            if (shardCostPerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shardCostPerLevel));
            }

            if (winsCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(winsCost));
            }

            if (rounds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rounds));
            }

            if (statusAmount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(statusAmount));
            }

            if (cardWidth < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(cardWidth));
            }

            Label = label;
            Description = description ?? string.Empty;
            ShardCost = shardCost;
            Effect = effect;
            SlotSize = slotSize;
            Count = count;
            Amount = amount;
            Battles = battles;
            CardId = cardId;
            ShardCostPerLevel = shardCostPerLevel;
            WinsCost = winsCost;
            Rounds = rounds;
            CardIds = (cardIds ?? Array.Empty<string>()).Clone() as string[]
                ?? Array.Empty<string>();
            StatusAmount = statusAmount;
            CardWidth = cardWidth >= 0
                ? cardWidth
                : ComputeCardWidth(effect, slotSize, count, cardIds);
        }

        public string Label { get; }

        public string Description { get; }

        public int ShardCost { get; }

        public EventEffectType Effect { get; }

        public int SlotSize { get; }

        public int Count { get; }

        public int Amount { get; }

        public int Battles { get; }

        public string CardId { get; }

        public int ShardCostPerLevel { get; }

        public int WinsCost { get; }

        public int Rounds { get; }

        public IReadOnlyList<string> CardIds { get; }

        public int StatusAmount { get; }

        /// <summary>这次选项要塞进背包的格数（0 表示不拿牌）。</summary>
        public int CardWidth { get; }

        public int GetShardCost(int level)
        {
            int currentLevel = Math.Max(1, level);
            return ShardCost + ShardCostPerLevel * currentLevel;
        }

        private static int ComputeCardWidth(
            EventEffectType effect,
            int slotSize,
            int count,
            string[] cardIds)
        {
            switch (effect)
            {
                case EventEffectType.RandomCards:
                case EventEffectType.GainCard:
                case EventEffectType.KillTime:
                    return slotSize * count;
                case EventEffectType.GainSpecificCards:
                    return slotSize * (cardIds == null ? 0 : cardIds.Length);
                default:
                    return 0;
            }
        }
    }

    /// <summary>一整个事件：标题、说明与三个选项。</summary>
    public sealed class MatchEventDefinition
    {
        public MatchEventDefinition(
            MatchEventKind kind,
            string id,
            string title,
            string description,
            IEnumerable<MatchEventOption> options)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An event needs an id.", nameof(id));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            List<MatchEventOption> list = new List<MatchEventOption>(options);
            if (list.Count == 0)
            {
                throw new ArgumentException("An event needs at least one option.", nameof(options));
            }

            Kind = kind;
            Id = id;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Options = new ReadOnlyCollection<MatchEventOption>(list);
        }

        public MatchEventKind Kind { get; }

        public string Id { get; }

        public string Title { get; }

        public string Description { get; }

        public IReadOnlyList<MatchEventOption> Options { get; }
    }

    /// <summary>玩家停在事件格时正在进行的选择。</summary>
    public sealed class MatchEventSession
    {
        public MatchEventSession(int nodeId, MatchEventDefinition definition)
        {
            NodeId = nodeId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public int NodeId { get; }

        public MatchEventDefinition Definition { get; }

        public IReadOnlyList<MatchEventOption> Options => Definition.Options;
    }

    /// <summary>
    /// 两张地图共用的事件表。事件按地块节点号固定分配，所以同一种子下同一个格子
    /// 永远出同一个事件（R-EVT-002）。
    /// </summary>
    public static class MatchEventCatalog
    {
        public const string MysteryDrinkCardId = "mystery_drink";
        public const string MurderWeaponCardId = "murder_weapon";
        public const string BiovirusCardId = "biovirus";
        public const string MotherCardId = "mother";

        /// <summary>只能由事件发放的牌：不进奖励 / 商店的随机牌池。</summary>
        public static bool IsEventOnlyCard(string cardId)
        {
            return string.Equals(cardId, MysteryDrinkCardId, StringComparison.Ordinal) ||
                   string.Equals(cardId, MurderWeaponCardId, StringComparison.Ordinal) ||
                   string.Equals(cardId, BiovirusCardId, StringComparison.Ordinal) ||
                   string.Equals(cardId, MotherCardId, StringComparison.Ordinal);
        }

        private static readonly MatchEventDefinition Expedition = CreateExpedition();
        private static readonly MatchEventDefinition MysteryDrink = CreateMysteryDrink();
        private static readonly MatchEventDefinition UnknownTrail = CreateUnknownTrail();
        private static readonly MatchEventDefinition Raid = CreateRaid();
        private static readonly MatchEventDefinition Biovirus = CreateBiovirus();

        private static readonly MatchEventDefinition[] StageOne =
        {
            Expedition,
            MysteryDrink,
            UnknownTrail
        };

        private static readonly MatchEventDefinition[] StageTwo =
        {
            Raid,
            Expedition,
            MysteryDrink,
            UnknownTrail
        };

        private static readonly MatchEventDefinition[] StageThree =
        {
            Biovirus,
            UnknownTrail
        };

        private static readonly MatchEventDefinition[] Definitions =
        {
            Expedition,
            MysteryDrink,
            UnknownTrail,
            Raid,
            Biovirus
        };

        public static IReadOnlyList<MatchEventDefinition> All => Definitions;

        public static MatchEventDefinition Get(MatchEventKind kind)
        {
            switch (kind)
            {
                case MatchEventKind.Expedition:
                    return Expedition;
                case MatchEventKind.MysteryDrink:
                    return MysteryDrink;
                case MatchEventKind.UnknownTrail:
                    return UnknownTrail;
                case MatchEventKind.Raid:
                    return Raid;
                case MatchEventKind.Biovirus:
                    return Biovirus;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>旧调用方的兼容入口；正式对局使用 <see cref="PickForPhase"/>。</summary>
        public static MatchEventKind KindForNode(int nodeId)
        {
            return (nodeId % 2) == 0
                ? MatchEventKind.Expedition
                : MatchEventKind.MysteryDrink;
        }

        public static MatchEventDefinition PickForPhase(MonsterPhase phase, Pcg32 random)
        {
            MatchEventDefinition[] pool;
            switch (phase)
            {
                case MonsterPhase.Mid:
                    pool = StageTwo;
                    break;
                case MonsterPhase.Late:
                    pool = StageThree;
                    break;
                default:
                    pool = StageOne;
                    break;
            }

            return pool[random.NextInt(pool.Length)];
        }

        private static MatchEventDefinition CreateExpedition()
        {
            return new MatchEventDefinition(
                MatchEventKind.Expedition,
                "expedition",
                "探险委托",
                "一位满身尘土的老手在树下招人。挑一档报酬，跟着他走一趟。",
                new[]
                {
                    new MatchEventOption(
                        "讨伐小队",
                        "清理林道上的杂鱼，战利品归你。",
                        4,
                        EventEffectType.RandomCards,
                        slotSize: 1,
                        count: 3),
                    new MatchEventOption(
                        "护卫商队",
                        "护送商队穿过废墟，报酬按人头算。",
                        5,
                        EventEffectType.RandomCards,
                        slotSize: 2,
                        count: 2),
                    new MatchEventOption(
                        "深入遗迹",
                        "独自下到遗迹最深处，值钱的东西只带得动一件。",
                        6,
                        EventEffectType.RandomCards,
                        slotSize: 3,
                        count: 1)
                });
        }

        private static MatchEventDefinition CreateMysteryDrink()
        {
            return new MatchEventDefinition(
                MatchEventKind.MysteryDrink,
                "mystery_drink",
                "神秘饮料",
                "路边摊上摆着一只不断冒泡的玻璃瓶。选择一种喝法，或者把它打包带走。",
                new[]
                {
                    new MatchEventOption(
                        "当场喝掉",
                        "把冒泡的液体一饮而尽。",
                        0,
                        EventEffectType.BattleShield,
                        amount: 50,
                        battles: 1),
                    new MatchEventOption(
                        "兑进酒里",
                        "把瓶里的东西兑进随身酒壶。",
                        0,
                        EventEffectType.BattlePower,
                        amount: 2,
                        battles: 2,
                        statusAmount: 1),
                    new MatchEventOption(
                        "打包带走",
                        "塞进背包，留着慢慢喝，也可以倒手卖掉。",
                        0,
                        EventEffectType.GainCard,
                        slotSize: 1,
                        count: 1,
                        cardId: MysteryDrinkCardId)
                });
        }

        private static MatchEventDefinition CreateUnknownTrail()
        {
            return new MatchEventDefinition(
                MatchEventKind.UnknownTrail,
                "unknown_trail",
                "未知小径",
                "一条没有路标的小径伸进雾里。你可以赌一把，也可以花钱让向导带路。",
                new[]
                {
                    new MatchEventOption(
                        "随便走走",
                        "沿着小径走到一个随机地块。",
                        0,
                        EventEffectType.RandomTeleport),
                    new MatchEventOption(
                        "雇个向导",
                        "支付 10 碎片，传送到指定地块。",
                        10,
                        EventEffectType.TeleportTarget)
                });
        }

        private static MatchEventDefinition CreateRaid()
        {
            return new MatchEventDefinition(
                MatchEventKind.Raid,
                "raid",
                "烧杀劫掠",
                "战火与贪婪一起点燃。接下来三回合，PvP 会变得更加凶狠。",
                new[]
                {
                    new MatchEventOption(
                        "杀进去",
                        "支付 10 碎片，获得「凶器」，开启 3 回合杀戮时间。",
                        10,
                        EventEffectType.KillTime,
                        slotSize: 1,
                        count: 1,
                        rounds: 3,
                        cardId: MurderWeaponCardId),
                    new MatchEventOption(
                        "交出胜场",
                        "失去 1 胜场，换得 5 碎片。",
                        0,
                        EventEffectType.LoseWinGainShards,
                        amount: 5,
                        winsCost: 1)
                });
        }

        private static MatchEventDefinition CreateBiovirus()
        {
            return new MatchEventDefinition(
                MatchEventKind.Biovirus,
                "biovirus",
                "生化病毒",
                "实验室的封条已经破裂。拿走样本，或者用碎片喂饱看守。",
                new[]
                {
                    new MatchEventOption(
                        "带走样本",
                        "获得「生化病毒」与「母体」。",
                        0,
                        EventEffectType.GainSpecificCards,
                        slotSize: 3,
                        count: 2,
                        cardIds: new[] { BiovirusCardId, MotherCardId },
                        cardWidth: 3),
                    new MatchEventOption(
                        "花钱消灾",
                        "失去当前等级 ×10 碎片。",
                        0,
                        EventEffectType.LoseShardsByLevel,
                        amount: 10,
                        shardCostPerLevel: 10)
                });
        }
    }
}
