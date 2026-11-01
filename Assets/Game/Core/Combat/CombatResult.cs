using System;
using System.Collections.Generic;
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
        /// <summary>战前快照：表现层据此画出双方的战斗栏。</summary>
        public BattleSnapshot Battle { get; internal set; }

        /// <summary>按 tick 排序的完整事件时间轴（60 tick = 1 秒），供回放使用。</summary>
        public IReadOnlyList<GameEvent> Timeline { get; internal set; } =
            Array.Empty<GameEvent>();

        public int EndSecond => EndTick / 60;
        public bool IsDraw => WinnerPlayerId < 0;
    }
}
