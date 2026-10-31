using UnityEngine;

namespace ProjectArtOfAll.Flow
{
    public static class GameEntryContext
    {
        public static int EntryAngleDegrees { get; private set; } = 90;

        public static void SetEntryAngle(float angleDegrees)
        {
            int normalized = Mathf.RoundToInt(angleDegrees / 90f) * 90;
            normalized %= 360;
            if (normalized < 0)
            {
                normalized += 360;
            }

            EntryAngleDegrees = normalized;
        }

        public static void Reset()
        {
            EntryAngleDegrees = 90;
        }
    }
}
