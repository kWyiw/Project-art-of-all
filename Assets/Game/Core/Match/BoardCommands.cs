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
}
