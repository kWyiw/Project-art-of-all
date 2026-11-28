using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Events;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// R-MOVE-003: Pass is an intermediate node; Arrival is the movement
    /// endpoint or the voluntary stopping point.
    /// </summary>
    public enum MovementVisitKind
    {
        Pass = 0,
        Arrival = 1
    }

    public sealed class MatchStartedEvent : GameEvent
    {
        public MatchStartedEvent(ulong seed)
        {
            Seed = seed;
        }

        public ulong Seed { get; }
    }

    public sealed class TurnStartedEvent : GameEvent
    {
        public TurnStartedEvent(int playerId, int roundNumber, int turnNumber)
        {
            PlayerId = playerId;
            RoundNumber = roundNumber;
            TurnNumber = turnNumber;
        }

        public int PlayerId { get; }
        public int RoundNumber { get; }
        public int TurnNumber { get; }
    }

    public sealed class TurnEndedEvent : GameEvent
    {
        public TurnEndedEvent(int playerId, int turnNumber)
        {
            PlayerId = playerId;
            TurnNumber = turnNumber;
        }

        public int PlayerId { get; }
        public int TurnNumber { get; }
    }

    public sealed class DiceRolledEvent : GameEvent
    {
        public DiceRolledEvent(int playerId, int value)
        {
            PlayerId = playerId;
            Value = value;
        }

        public int PlayerId { get; }
        public int Value { get; }
    }

    public sealed class PlayerMovedEvent : GameEvent
    {
        public PlayerMovedEvent(
            int playerId,
            int fromNodeId,
            int toNodeId,
            int stepIndex,
            MovementVisitKind visitKind)
        {
            PlayerId = playerId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            StepIndex = stepIndex;
            VisitKind = visitKind;
        }

        public int PlayerId { get; }
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public int StepIndex { get; }
        public MovementVisitKind VisitKind { get; }
    }

    /// <summary>
    /// R-MOVE-006: Warp is displacement, not a movement step.
    /// </summary>
    public sealed class PlayerTeleportedEvent : GameEvent
    {
        public PlayerTeleportedEvent(
            int playerId,
            int fromNodeId,
            int toNodeId,
            bool isEventTeleport = false)
        {
            PlayerId = playerId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            IsEventTeleport = isEventTeleport;
        }

        public int PlayerId { get; }
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public bool IsEventTeleport { get; }
    }

    public sealed class ShardsChangedEvent : GameEvent
    {
        public ShardsChangedEvent(int playerId, int delta, int newValue)
        {
            PlayerId = playerId;
            Delta = delta;
            NewValue = newValue;
        }

        public int PlayerId { get; }
        public int Delta { get; }
        public int NewValue { get; }
    }

    public sealed class WinsChangedEvent : GameEvent
    {
        public WinsChangedEvent(int playerId, int delta, int newValue)
        {
            PlayerId = playerId;
            Delta = delta;
            NewValue = newValue;
        }

        public int PlayerId { get; }
        public int Delta { get; }
        public int NewValue { get; }
    }

    public sealed class PlayerLeveledUpEvent : GameEvent
    {
        public PlayerLeveledUpEvent(int playerId, int newLevel)
        {
            PlayerId = playerId;
            NewLevel = newLevel;
        }

        public int PlayerId { get; }
        public int NewLevel { get; }
    }

    public sealed class CombatResolvedEvent : GameEvent
    {
        public CombatResolvedEvent(int attackerId, int defenderId, CombatResult result)
        {
            AttackerId = attackerId;
            DefenderId = defenderId;
            Result = result;
        }

        public int AttackerId { get; }
        public int DefenderId { get; }
        public CombatResult Result { get; }
    }

    public sealed class CardAddedEvent : GameEvent
    {
        public CardAddedEvent(int playerId, int cardUid, string cardId, string source)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            CardId = cardId;
            Source = source;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public string CardId { get; }
        public string Source { get; }
    }

    /// <summary>融合升级：手里已有的同名牌吃掉新牌，Definition 换成升级版（CJ-5.11）。</summary>
    public sealed class CardUpgradedEvent : GameEvent
    {
        public CardUpgradedEvent(
            int playerId,
            int cardUid,
            string baseId,
            string upgradedId,
            string source)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            BaseId = baseId;
            UpgradedId = upgradedId;
            Source = source;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public string BaseId { get; }
        public string UpgradedId { get; }
        public string Source { get; }
    }

    /// <summary>事件牌将一张已有的 1c 牌永久转化为另一张牌。</summary>
    public sealed class CardTransformedEvent : GameEvent
    {
        public CardTransformedEvent(
            int playerId,
            int cardUid,
            string fromCardId,
            string toCardId,
            string source)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            FromCardId = fromCardId;
            ToCardId = toCardId;
            Source = source;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public string FromCardId { get; }
        public string ToCardId { get; }
        public string Source { get; }
    }

    /// <summary>牌被非出售手段消耗掉了（粉碎机每场战斗吃掉背包最左侧的废料）。</summary>
    public sealed class CardConsumedEvent : GameEvent
    {
        public CardConsumedEvent(int playerId, int cardUid, string cardId, string reason)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            CardId = cardId;
            Reason = reason;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public string CardId { get; }
        public string Reason { get; }
    }

    public sealed class CardSoldEvent : GameEvent
    {
        public CardSoldEvent(int playerId, int cardUid, string cardId, int shards)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            CardId = cardId;
            Shards = shards;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public string CardId { get; }
        public int Shards { get; }
    }

    /// <summary>A card changed rows (or order) without changing ownership.</summary>
    public sealed class CardMovedEvent : GameEvent
    {
        public CardMovedEvent(int playerId, int cardUid, string cardId, bool toBattle, int index)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            CardId = cardId;
            ToBattle = toBattle;
            Index = index;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public string CardId { get; }
        public bool ToBattle { get; }
        public int Index { get; }
    }

    public sealed class CardEquippedEvent : GameEvent
    {
        public CardEquippedEvent(int playerId, int cardUid, bool equipped)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            Equipped = equipped;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public bool Equipped { get; }
    }

    public sealed class RewardFailedEvent : GameEvent
    {
        public RewardFailedEvent(int playerId, string reason)
        {
            PlayerId = playerId;
            Reason = reason;
        }

        public int PlayerId { get; }
        public string Reason { get; }
    }

    public sealed class GlobalShardsGrantedEvent : GameEvent
    {
        public GlobalShardsGrantedEvent(int amount)
        {
            Amount = amount;
        }

        public int Amount { get; }
    }

    public sealed class BossEvolutionEvent : GameEvent
    {
        public BossEvolutionEvent(int triggeredByPlayerId)
        {
            TriggeredByPlayerId = triggeredByPlayerId;
        }

        public int TriggeredByPlayerId { get; }
    }

    /// <summary>玩家停在事件格，事件面板打开（R-EVT-001）。</summary>
    public sealed class EventOfferedEvent : GameEvent
    {
        public EventOfferedEvent(int playerId, string eventId, int nodeId)
        {
            PlayerId = playerId;
            EventId = eventId;
            NodeId = nodeId;
        }

        public int PlayerId { get; }
        public string EventId { get; }
        public int NodeId { get; }
    }

    /// <summary>未知小径：事件选项已付款，等待选择指定传送地块。</summary>
    public sealed class EventTargetRequestedEvent : GameEvent
    {
        public EventTargetRequestedEvent(int playerId, int nodeId, string eventId)
        {
            PlayerId = playerId;
            NodeId = nodeId;
            EventId = eventId;
        }

        public int PlayerId { get; }
        public int NodeId { get; }
        public string EventId { get; }
    }

    /// <summary>事件选项结算完成；<see cref="OptionIndex"/> 为 -1 表示直接走开。</summary>
    public sealed class EventResolvedEvent : GameEvent
    {
        public EventResolvedEvent(
            int playerId,
            string eventId,
            int optionIndex,
            int shardCost)
        {
            PlayerId = playerId;
            EventId = eventId;
            OptionIndex = optionIndex;
            ShardCost = shardCost;
        }

        public int PlayerId { get; }
        public string EventId { get; }
        public int OptionIndex { get; }
        public int ShardCost { get; }
    }

    /// <summary>铁丝网 / 路障出售后在地块上留下了陷阱（R-TRAP-001）。</summary>
    public sealed class BoardTrapPlacedEvent : GameEvent
    {
        public BoardTrapPlacedEvent(
            int playerId,
            int nodeId,
            string cardId,
            BoardTrapKind kind,
            int charges)
        {
            PlayerId = playerId;
            NodeId = nodeId;
            CardId = cardId;
            Kind = kind;
            Charges = charges;
        }

        public int PlayerId { get; }
        public int NodeId { get; }
        public string CardId { get; }
        public BoardTrapKind Kind { get; }
        public int Charges { get; }
    }

    /// <summary>陷阱被踩中：路障强制停车或铁丝网禁行。</summary>
    public sealed class BoardTrapTriggeredEvent : GameEvent
    {
        public BoardTrapTriggeredEvent(
            int ownerPlayerId,
            int playerId,
            int nodeId,
            BoardTrapKind kind,
            int chargesLeft)
        {
            OwnerPlayerId = ownerPlayerId;
            PlayerId = playerId;
            NodeId = nodeId;
            Kind = kind;
            ChargesLeft = chargesLeft;
        }

        public int OwnerPlayerId { get; }
        public int PlayerId { get; }
        public int NodeId { get; }
        public BoardTrapKind Kind { get; }
        public int ChargesLeft { get; }
    }

    /// <summary>被铁丝网困住，这次回合不能移动。</summary>
    public sealed class PlayerImmobilizedEvent : GameEvent
    {
        public PlayerImmobilizedEvent(int playerId, int roundsLeft)
        {
            PlayerId = playerId;
            RoundsLeft = roundsLeft;
        }

        public int PlayerId { get; }
        public int RoundsLeft { get; }
    }
}
