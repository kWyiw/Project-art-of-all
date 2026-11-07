using ProjectArtOfAll.Core.Commands;

namespace ProjectArtOfAll.Core.Match
{
    public sealed class RollCommand : IMatchCommand
    {
        public RollCommand(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    public sealed class SetNormaCommand : IMatchCommand
    {
        public SetNormaCommand(int playerId, NormaType norma)
        {
            PlayerId = playerId;
            Norma = norma;
        }

        public int PlayerId { get; }
        public NormaType Norma { get; }
    }

    public sealed class ChooseRewardCommand : IMatchCommand
    {
        public ChooseRewardCommand(int playerId, int choiceIndex)
        {
            PlayerId = playerId;
            ChoiceIndex = choiceIndex;
        }

        public int PlayerId { get; }
        public int ChoiceIndex { get; }
    }

    public enum ShopActionType
    {
        Buy = 0,
        Reroll = 1,
        Leave = 2
    }

    public sealed class ShopActionCommand : IMatchCommand
    {
        public ShopActionCommand(
            int playerId,
            ShopActionType action,
            int offerIndex = -1)
        {
            PlayerId = playerId;
            Action = action;
            OfferIndex = offerIndex;
        }

        public int PlayerId { get; }
        public ShopActionType Action { get; }
        public int OfferIndex { get; }
    }

    public sealed class SellCardCommand : IMatchCommand
    {
        public SellCardCommand(int playerId, int cardUid)
        {
            PlayerId = playerId;
            CardUid = cardUid;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
    }

    /// <summary>
    /// Free card action (`大富翁层规则书.md` R-TURN-002): put a card into any legal column of
    /// the bag or the battle row. Cards may leave gaps between them on purpose
    /// (UI 规格 §3.2), so <see cref="Column"/> is a slot column, not a compacted index.
    /// </summary>
    public sealed class MoveCardCommand : IMatchCommand
    {
        public MoveCardCommand(int playerId, int cardUid, bool toBattle, int column)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            ToBattle = toBattle;
            Column = column;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public bool ToBattle { get; }

        /// <summary>目标栏位里最左一列的列号（0 起）。</summary>
        public int Column { get; }
    }

    public sealed class EquipCardCommand : IMatchCommand
    {
        public EquipCardCommand(int playerId, int cardUid)
        {
            PlayerId = playerId;
            CardUid = cardUid;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
    }

    public sealed class UnequipCardCommand : IMatchCommand
    {
        public UnequipCardCommand(int playerId, int cardUid)
        {
            PlayerId = playerId;
            CardUid = cardUid;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
    }

    /// <summary>
    /// 事件格上的选择（R-EVT-001）。<see cref="OptionIndex"/> 取 -1 表示不参与、
    /// 直接走完这一轮，所以碎片不够时永远有出路。
    /// </summary>
    public sealed class EventChoiceCommand : IMatchCommand
    {
        public EventChoiceCommand(int playerId, int optionIndex)
        {
            PlayerId = playerId;
            OptionIndex = optionIndex;
        }

        public int PlayerId { get; }

        public int OptionIndex { get; }
    }
}
