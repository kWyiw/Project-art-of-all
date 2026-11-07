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
        public CardCastEvent(int playerId, int column, int cardUid)
        {
            PlayerId = playerId;
            Column = column;
            CardUid = cardUid;
        }

        public int PlayerId { get; }

        /// <summary>施放时这张牌在战斗栏里的最左列号（中间可以留空）。</summary>
        public int Column { get; }

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

    /// <summary>充能：一张牌被瞬间推进了 <see cref="Ticks"/> 的读秒。</summary>
    public sealed class ChargeAppliedEvent : GameEvent
    {
        public ChargeAppliedEvent(int playerId, int cardUid, int ticks, int sourceCardUid)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            Ticks = ticks;
            SourceCardUid = sourceCardUid;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public int Ticks { get; }

        /// <summary>-1 when the charge came from a passive instead of a cast card.</summary>
        public int SourceCardUid { get; }
    }

    public sealed class HasteAppliedEvent : GameEvent
    {
        public HasteAppliedEvent(int playerId, int cardUid, int durationTicks)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            DurationTicks = durationTicks;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public int DurationTicks { get; }
    }

    public sealed class SlowAppliedEvent : GameEvent
    {
        public SlowAppliedEvent(int playerId, int cardUid, int durationTicks, int sourceCardUid)
        {
            PlayerId = playerId;
            CardUid = cardUid;
            DurationTicks = durationTicks;
            SourceCardUid = sourceCardUid;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
        public int DurationTicks { get; }

        /// <summary>-1 when the slow came from a passive instead of a cast card.</summary>
        public int SourceCardUid { get; }
    }

    public sealed class CardDestroyedEvent : GameEvent
    {
        public CardDestroyedEvent(int playerId, int cardUid)
        {
            PlayerId = playerId;
            CardUid = cardUid;
        }

        public int PlayerId { get; }
        public int CardUid { get; }
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
