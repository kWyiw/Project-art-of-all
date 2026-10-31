using System;

namespace ProjectArtOfAll.Core.Rng
{
    /// <summary>
    /// PCG32 deterministic pseudo-random generator.
    /// Core gameplay must use this instead of UnityEngine.Random or System.Random.
    /// </summary>
    public sealed class Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong state;
        private readonly ulong increment;

        public Pcg32(ulong seed, ulong sequence = 54UL)
        {
            state = 0UL;
            increment = (sequence << 1) | 1UL;
            NextUInt32();
            unchecked
            {
                state += seed;
            }

            NextUInt32();
        }

        public uint NextUInt32()
        {
            ulong oldState = state;
            unchecked
            {
                state = oldState * Multiplier + increment;
            }

            uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);
            return (xorShifted >> rotation) |
                   (xorShifted << ((-rotation) & 31));
        }

        public ulong State => state;

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveMaximum),
                    "The exclusive maximum must be positive.");
            }

            uint bound = (uint)exclusiveMaximum;
            uint rejectionLimit = uint.MaxValue - (uint.MaxValue % bound);
            uint value;

            do
            {
                value = NextUInt32();
            }
            while (value >= rejectionLimit);

            return (int)(value % bound);
        }

        public int NextInt(int inclusiveMinimum, int exclusiveMaximum)
        {
            if (exclusiveMaximum <= inclusiveMinimum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveMaximum),
                    "The exclusive maximum must be greater than the minimum.");
            }

            long range = (long)exclusiveMaximum - inclusiveMinimum;
            if (range > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveMaximum),
                    "The requested range is too large.");
            }

            return inclusiveMinimum + NextInt((int)range);
        }

        public int NextD6()
        {
            return NextInt(1, 7);
        }

        /// <summary>
        /// Creates an independent deterministic stream for another subsystem.
        /// </summary>
        public Pcg32 Fork(ulong streamId)
        {
            ulong derivedSeed = state ^ unchecked(streamId * 0x9E3779B97F4A7C15UL);
            return new Pcg32(derivedSeed, streamId);
        }
    }
}
