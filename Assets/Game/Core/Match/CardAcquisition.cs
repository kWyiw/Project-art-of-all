using System;
using ProjectArtOfAll.Core.Cards;

namespace ProjectArtOfAll.Core.Match
{
    public enum CardAcquisitionKind
    {
        /// <summary>没拿到：背包放不下，也没有同名牌可以融合。</summary>
        Failed = 0,

        /// <summary>新牌进了背包（或直接装上了战斗栏）。</summary>
        Added = 1,

        /// <summary>同名牌在自己手里，这次拿到的被融合掉了，原牌升了一级（CJ-5.11）。</summary>
        Upgraded = 2
    }

    /// <summary>
    /// 一次「获得卡牌」的结果。升级时 <see cref="Card"/> 是那张被升级的原牌，
    /// 新牌并不存在——玩家手里仍然是同样数量的牌。
    /// </summary>
    public readonly struct CardAcquisition
    {
        private CardAcquisition(CardAcquisitionKind kind, CardInstance card)
        {
            Kind = kind;
            Card = card;
        }

        public CardAcquisitionKind Kind { get; }

        public CardInstance Card { get; }

        public bool Succeeded => Kind != CardAcquisitionKind.Failed;

        public static CardAcquisition Failed()
        {
            return new CardAcquisition(CardAcquisitionKind.Failed, null);
        }

        public static CardAcquisition Added(CardInstance card)
        {
            return new CardAcquisition(
                CardAcquisitionKind.Added,
                card ?? throw new ArgumentNullException(nameof(card)));
        }

        public static CardAcquisition Upgraded(CardInstance card)
        {
            return new CardAcquisition(
                CardAcquisitionKind.Upgraded,
                card ?? throw new ArgumentNullException(nameof(card)));
        }
    }
}
