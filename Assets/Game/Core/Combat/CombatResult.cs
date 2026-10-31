using System;
using ProjectArtOfAll.Core.Events;

namespace ProjectArtOfAll.Core.Combat
{
    public sealed class CombatResult
    {
        public CombatResult(
            int winnerPlayerId,
            CombatEndReason reason,
            int endTick,
            int leftHealth,
            int rightHealth,
            int leftShield,
            int rightShield)
        {
            WinnerPlayerId = winnerPlayerId;
            Reason = reason;
            EndTick = endTick;
            LeftHealth = leftHealth;
            RightHealth = rightHealth;
            LeftShield = leftShield;
            RightShield = rightShield;
        }

        public int WinnerPlayerId { get; }
        public CombatEndReason Reason { get; }
        public int EndTick { get; }
        public int LeftHealth { get; }
        public int RightHealth { get; }
        public int LeftShield { get; }
        public int RightShield { get; }
        public int EndSecond => EndTick / 60;
        public bool IsDraw => WinnerPlayerId < 0;
    }
}
