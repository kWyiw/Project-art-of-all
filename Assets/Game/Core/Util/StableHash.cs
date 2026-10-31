using System;

namespace ProjectArtOfAll.Core.Util
{
    public static class StableHash
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Begin()
        {
            return Offset;
        }

        public static ulong Add(ulong hash, long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                return hash * Prime;
            }
        }

        public static ulong Add(ulong hash, ulong value)
        {
            unchecked
            {
                hash ^= value;
                return hash * Prime;
            }
        }

        public static ulong Add(ulong hash, string value)
        {
            unchecked
            {
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= Prime;
                }

                hash ^= 0xFFUL;
                return hash * Prime;
            }
        }
    }
}
