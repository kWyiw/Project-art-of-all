using System;

namespace ProjectArtOfAll.Core.Combat
{
    /// <summary>
    /// Integer-only cooldown with fractional speed handled by an accumulator.
    /// Haste is 2/1, slow is 1/2, and haste plus slow is 1/1.
    /// Repeated applications extend duration but never stack intensity.
    /// Charge that overflows a finished cooldown is carried into the next cycle (BT-3.6).
    /// </summary>
    public sealed class CooldownTimer
    {
        private int hasteRemainingTicks;
        private int slowRemainingTicks;
        private int chargeCarryTicks;
        private long speedAccumulator;

        public CooldownTimer(int cooldownTicks = 0)
        {
            RemainingTicks = Math.Max(0, cooldownTicks);
        }

        public int RemainingTicks { get; private set; }
        public int HasteRemainingTicks => hasteRemainingTicks;
        public int SlowRemainingTicks => slowRemainingTicks;
        public int ChargeCarryTicks => chargeCarryTicks;
        public bool IsReady => RemainingTicks <= 0;

        /// <summary>
        /// Starts a fresh cooldown. Any charge carried from the previous cycle is spent
        /// immediately; whatever it does not cover is discarded (BT-3.6).
        /// </summary>
        public void SetRemainingTicks(int ticks)
        {
            int clamped = Math.Max(0, ticks);
            if (chargeCarryTicks > 0)
            {
                clamped = Math.Max(0, clamped - chargeCarryTicks);
                chargeCarryTicks = 0;
            }

            RemainingTicks = clamped;
            speedAccumulator = 0L;
        }

        public void Reset(int cooldownTicks)
        {
            ClearSpeedModifiers();
            RemainingTicks = Math.Max(0, cooldownTicks);
        }

        /// <summary>
        /// 充能：冷却读秒瞬间前进 <paramref name="ticks"/>。溢出部分记为 chargeCarry，
        /// 在该牌下一次进入冷却时扣掉。
        /// </summary>
        public void ApplyCharge(int ticks)
        {
            if (ticks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks));
            }

            if (ticks == 0)
            {
                return;
            }

            if (RemainingTicks > ticks)
            {
                RemainingTicks -= ticks;
                return;
            }

            chargeCarryTicks = checked(chargeCarryTicks + (ticks - RemainingTicks));
            RemainingTicks = 0;
        }

        public void ApplyHaste(int durationTicks)
        {
            if (durationTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            }

            hasteRemainingTicks = checked(hasteRemainingTicks + durationTicks);
        }

        public void ApplySlow(int durationTicks)
        {
            if (durationTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            }

            slowRemainingTicks = checked(slowRemainingTicks + durationTicks);
        }

        public void ClearSpeedModifiers()
        {
            hasteRemainingTicks = 0;
            slowRemainingTicks = 0;
            speedAccumulator = 0L;
        }

        public void AdvanceTick()
        {
            bool hasHaste = hasteRemainingTicks > 0;
            bool hasSlow = slowRemainingTicks > 0;

            if (hasHaste)
            {
                hasteRemainingTicks--;
            }

            if (hasSlow)
            {
                slowRemainingTicks--;
            }

            if (RemainingTicks <= 0)
            {
                return;
            }

            int numerator;
            int denominator;

            if (hasHaste && hasSlow)
            {
                numerator = 1;
                denominator = 1;
            }
            else if (hasHaste)
            {
                numerator = 2;
                denominator = 1;
            }
            else if (hasSlow)
            {
                numerator = 1;
                denominator = 2;
            }
            else
            {
                numerator = 1;
                denominator = 1;
            }

            speedAccumulator += numerator;
            long elapsedCooldownTicks = speedAccumulator / denominator;
            speedAccumulator %= denominator;

            RemainingTicks = (int)Math.Max(
                0L,
                RemainingTicks - elapsedCooldownTicks);
        }

        public void AdvanceTicks(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            for (int index = 0; index < count; index++)
            {
                AdvanceTick();
            }
        }
    }
}
