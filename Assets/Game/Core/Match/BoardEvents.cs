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
        public PlayerTeleportedEvent(int playerId, int fromNodeId, int toNodeId)
        {
            PlayerId = playerId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }

        public int PlayerId { get; }
        public int FromNodeId { get; }
        public int ToNodeId { get; }
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
}
