namespace ProjectArtOfAll.Core.Events
{
    public enum StatusKind
    {
        Poison = 0,
        Burn = 1,
        Regeneration = 2,
        Shield = 3
    }

    public enum CombatEndReason
    {
        Knockout = 0,
        TimeoutKnockout = 1,
        HardTimeoutDraw = 2
    }

    public sealed class CombatStartedEvent : GameEvent
    {
        public CombatStartedEvent(ulong seed)
        {
            Seed = seed;
        }

        public ulong Seed { get; }
    }

    public sealed class CardCastEvent : GameEvent
    {
        public CardCastEvent(int playerId, int slotIndex, int cardUid)
        {
            PlayerId = playerId;
            SlotIndex = slotIndex;
            CardUid = cardUid;
        }

        public int PlayerId { get; }
        public int SlotIndex { get; }
        public int CardUid { get; }
    }

    public sealed class DamageDealtEvent : GameEvent
    {
        public DamageDealtEvent(
            int sourcePlayerId,
            int targetPlayerId,
            int amount,
            int shieldAbsorbed,
            bool ignoresShield)
        {
            SourcePlayerId = sourcePlayerId;
            TargetPlayerId = targetPlayerId;
            Amount = amount;
            ShieldAbsorbed = shieldAbsorbed;
            IgnoresShield = ignoresShield;
        }

        public int SourcePlayerId { get; }
        public int TargetPlayerId { get; }
        public int Amount { get; }
        public int ShieldAbsorbed { get; }
        public bool IgnoresShield { get; }
    }

    public sealed class ShieldGainedEvent : GameEvent
    {
        public ShieldGainedEvent(int playerId, int amount)
        {
            PlayerId = playerId;
            Amount = amount;
        }

        public int PlayerId { get; }
        public int Amount { get; }
    }

    public sealed class StatusAppliedEvent : GameEvent
    {
        public StatusAppliedEvent(int playerId, StatusKind status, int layers)
        {
            PlayerId = playerId;
            Status = status;
            Layers = layers;
        }

        public int PlayerId { get; }
        public StatusKind Status { get; }
        public int Layers { get; }
    }

    public sealed class CombatEndedEvent : GameEvent
    {
        public CombatEndedEvent(int winnerPlayerId, CombatEndReason reason)
        {
            WinnerPlayerId = winnerPlayerId;
            Reason = reason;
        }

        /// <summary>-1 means a draw.</summary>
        public int WinnerPlayerId { get; }
        public CombatEndReason Reason { get; }
    }
}
