using System;

namespace ProjectArtOfAll.Core.Combat
{
    /// <summary>
    /// Integer-only cooldown with fractional speed handled by an accumulator.
    /// Haste is 2/1, slow is 1/2, and haste plus slow is 1/1.
    /// Repeated applications extend duration but never stack intensity.
    /// </summary>
    public sealed class CooldownTimer
    {
        private int hasteRemainingTicks;
        private int slowRemainingTicks;
        private long speedAccumulator;

        public CooldownTimer(int cooldownTicks = 0)
        {
            RemainingTicks = Math.Max(0, cooldownTicks);
        }

        public int RemainingTicks { get; private set; }
        public int HasteRemainingTicks => hasteRemainingTicks;
        public int SlowRemainingTicks => slowRemainingTicks;
        public bool IsReady => RemainingTicks <= 0;

        public void SetRemainingTicks(int ticks)
        {
            RemainingTicks = Math.Max(0, ticks);
            speedAccumulator = 0L;
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
