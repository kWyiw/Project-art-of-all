using System;

namespace ProjectArtOfAll.Core.Combat
{
    /// <summary>
    /// Converts elapsed integer time into fixed 60 Hz combat ticks.
    /// Floating point time may be converted at the Unity boundary, but never enters this accumulator.
    /// </summary>
    public sealed class CombatTickAccumulator
    {
        public const int TicksPerSecond = 60;
        private const long MicrosecondsPerSecond = 1000000L;

        private long remainderMicroseconds;

        public int Advance(long elapsedMicroseconds)
        {
            if (elapsedMicroseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMicroseconds));
            }

            long scaled = remainderMicroseconds +
                          elapsedMicroseconds * TicksPerSecond;
            int ticks = (int)(scaled / MicrosecondsPerSecond);
            remainderMicroseconds = scaled % MicrosecondsPerSecond;
            return ticks;
        }

        public void Reset()
        {
            remainderMicroseconds = 0L;
        }
    }
}
