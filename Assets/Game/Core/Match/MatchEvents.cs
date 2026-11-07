using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// 事件格上出现的事件（规则书 R-EVT-001）。目前只有两个：
    /// 探险委托（用碎片换随机牌）与神秘饮料（换战斗增益或一张值钱的牌）。
    /// </summary>
    public enum MatchEventKind
    {
        Expedition = 0,
        MysteryDrink = 1
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

        /// <summary>接下来 <see cref="MatchEventOption.Battles"/> 场战斗里，伤害与格挡各 +<see cref="MatchEventOption.Amount"/>。</summary>
        BattlePower = 3
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
            string cardId = null)
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

            Label = label;
            Description = description ?? string.Empty;
            ShardCost = shardCost;
            Effect = effect;
            SlotSize = slotSize;
            Count = count;
            Amount = amount;
            Battles = battles;
            CardId = cardId;
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

        /// <summary>这次选项要塞进背包的格数（0 表示不拿牌）。</summary>
        public int CardWidth
        {
            get
            {
                switch (Effect)
                {
                    case EventEffectType.RandomCards:
                        return SlotSize * Count;
                    case EventEffectType.GainCard:
                        return SlotSize * Count;
                    default:
                        return 0;
                }
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

        /// <summary>只能由事件发放的牌：不进奖励 / 商店的随机牌池。</summary>
        public static bool IsEventOnlyCard(string cardId)
        {
            return string.Equals(cardId, MysteryDrinkCardId, StringComparison.Ordinal);
        }

        private static readonly MatchEventDefinition[] Definitions =
        {
            CreateExpedition(),
            CreateMysteryDrink()
        };

        public static IReadOnlyList<MatchEventDefinition> All => Definitions;

        public static MatchEventDefinition Get(MatchEventKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= Definitions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return Definitions[index];
        }

        /// <summary>事件格用节点号奇偶交替分配，保证两种事件都能遇到。</summary>
        public static MatchEventKind KindForNode(int nodeId)
        {
            return (nodeId % 2) == 0
                ? MatchEventKind.Expedition
                : MatchEventKind.MysteryDrink;
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
                "路边摊上摆着一只不断冒泡的玻璃瓶，摊主坚持说喝下去会有好事。",
                new[]
                {
                    new MatchEventOption(
                        "当场喝掉",
                        "瓶身还在冒泡，喝了再说。",
                        2,
                        EventEffectType.BattleShield,
                        amount: 50,
                        battles: 1),
                    new MatchEventOption(
                        "打包带走",
                        "塞进背包，留着慢慢喝，也可以倒手卖掉。",
                        0,
                        EventEffectType.GainCard,
                        slotSize: 2,
                        count: 1,
                        cardId: MysteryDrinkCardId),
                    new MatchEventOption(
                        "兑进酒里",
                        "把瓶里的东西兑进随身酒壶。",
                        2,
                        EventEffectType.BattlePower,
                        amount: 1,
                        battles: 2)
                });
        }
    }
}
