using System;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// 棋盘上的陷阱（设计稿里「铁丝网 / 路障」出售时留在当前地块的东西）。
    ///
    /// 陷阱挂在 <see cref="MatchState"/> 上而不是卡牌上：卡已经卖掉了，
    /// 但地块上留下的效果要按次数结算，并且必须参与状态哈希。
    /// </summary>
    public enum BoardTrapKind
    {
        /// <summary>铁丝网：停在这里的单位被禁止移动若干回合（一次性）。</summary>
        Immobilize = 0,

        /// <summary>路障：任何单位经过都会被强制停下，触发若干次后失效。</summary>
        ForcedStop = 1
    }

    public sealed class BoardTrap
    {
        internal BoardTrap(int nodeId, BoardTrapKind kind, int ownerPlayerId, int charges)
        {
            if (nodeId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeId));
            }

            if (charges <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(charges));
            }

            NodeId = nodeId;
            Kind = kind;
            OwnerPlayerId = ownerPlayerId;
            Charges = charges;
        }

        public int NodeId { get; }
        public BoardTrapKind Kind { get; }

        /// <summary>布下陷阱的人，只用于事件回放，不影响结算。</summary>
        public int OwnerPlayerId { get; }

        /// <summary>剩余触发次数（铁丝网是禁止移动的回合数，路障是还能拦几次）。</summary>
        public int Charges { get; internal set; }
    }

    /// <summary>哪张牌出售时留下什么陷阱、剩几次（设计稿数值，升级版加一档）。</summary>
    public static class BoardTrapCatalog
    {
        public const string BarbedWireId = "barbed_wire";
        public const string RoadblockId = "roadblock";

        public static bool TryDescribe(
            string baseCardId,
            bool upgraded,
            out BoardTrapKind kind,
            out int charges)
        {
            switch (baseCardId)
            {
                case BarbedWireId:
                    kind = BoardTrapKind.Immobilize;
                    charges = upgraded ? 3 : 2;
                    return true;
                case RoadblockId:
                    kind = BoardTrapKind.ForcedStop;
                    charges = upgraded ? 4 : 3;
                    return true;
                default:
                    kind = BoardTrapKind.Immobilize;
                    charges = 0;
                    return false;
            }
        }
    }
}
