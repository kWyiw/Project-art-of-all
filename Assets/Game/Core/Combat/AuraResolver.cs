using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Cards;

namespace ProjectArtOfAll.Core.Combat
{
    /// <summary>
    /// Recomputes auras on every query (R-AURA-002) so reordering the battle bar instantly
    /// changes every number. Auras never chain: each one only reads the current card list.
    /// </summary>
    internal sealed class AuraResolver
    {
        public int GetDiceModifier(CombatantRuntime side, CombatCardRuntime target)
        {
            return GetAuraValue(side, target, AuraStat.DiceFlatModifier);
        }

        public int GetBonusMultiCast(CombatantRuntime side, CombatCardRuntime target)
        {
            return GetAuraValue(side, target, AuraStat.MultiCast);
        }

        public int GetBonusDiceCount(CombatantRuntime side, CombatCardRuntime target)
        {
            return GetAuraValue(side, target, AuraStat.DiceCount);
        }

        /// <summary>自噬之蛇: the target card's dice always roll their maximum face.</summary>
        public bool HasMaxRoll(CombatantRuntime side, CombatCardRuntime target)
        {
            return GetAuraValue(side, target, AuraStat.MaxRoll) > 0;
        }

        private static int GetAuraValue(
            CombatantRuntime side,
            CombatCardRuntime target,
            AuraStat stat)
        {
            if (target == null || target.IsDestroyed)
            {
                return 0;
            }

            List<CombatCardRuntime> activeCards = side.Cards
                .Where(card => !card.IsDestroyed)
                .OrderBy(card => card.SlotIndex)
                .ToList();

            int targetIndex = activeCards.IndexOf(target);
            if (targetIndex < 0)
            {
                return 0;
            }

            int total = 0;
            foreach (CombatCardRuntime source in activeCards)
            {
                foreach (AuraDefinition aura in source.Definition.Auras)
                {
                    if (aura.Stat != stat ||
                        !Targets(aura, source, target, targetIndex, activeCards))
                    {
                        continue;
                    }

                    if (aura.Scaling == AuraScaling.PerAdjacentMatchingTag)
                    {
                        int matchingNeighbours = CountAdjacentMatching(
                            source,
                            aura.RequiredTag,
                            activeCards);
                        total += aura.Amount * matchingNeighbours;
                    }
                    else
                    {
                        total += aura.Amount;
                    }
                }
            }

            return total;
        }

        private static bool Targets(
            AuraDefinition aura,
            CombatCardRuntime source,
            CombatCardRuntime target,
            int targetIndex,
            IReadOnlyList<CombatCardRuntime> activeCards)
        {
            if (aura.RequiredTag != CardTag.None &&
                !target.Definition.HasTag(aura.RequiredTag))
            {
                return false;
            }

            if (aura.MinimumSlotSize > 0 &&
                target.Definition.SlotSize < aura.MinimumSlotSize)
            {
                return false;
            }

            switch (aura.Target)
            {
                case AuraTarget.Self:
                    return source == target;
                case AuraTarget.AdjacentLeft:
                    // 目标是源卡的左邻：源卡恰好在 target 的右边一格。
                    return targetIndex + 1 < activeCards.Count &&
                           activeCards[targetIndex + 1] == source;
                case AuraTarget.AdjacentRight:
                    // 目标是源卡的右邻：源卡恰好在 target 的左边一格。
                    return targetIndex > 0 && activeCards[targetIndex - 1] == source;
                case AuraTarget.AllAdjacent:
                    return (targetIndex > 0 && activeCards[targetIndex - 1] == source) ||
                           (targetIndex + 1 < activeCards.Count &&
                            activeCards[targetIndex + 1] == source);
                case AuraTarget.AllByTag:
                    return true;
                default:
                    return false;
            }
        }

        private static int CountAdjacentMatching(
            CombatCardRuntime source,
            CardTag requiredTag,
            IReadOnlyList<CombatCardRuntime> activeCards)
        {
            int sourceIndex = -1;
            for (int index = 0; index < activeCards.Count; index++)
            {
                if (activeCards[index] == source)
                {
                    sourceIndex = index;
                    break;
                }
            }

            if (sourceIndex < 0)
            {
                return 0;
            }

            int count = 0;
            if (sourceIndex > 0 &&
                (requiredTag == CardTag.None ||
                 activeCards[sourceIndex - 1].Definition.HasTag(requiredTag)))
            {
                count++;
            }

            if (sourceIndex + 1 < activeCards.Count &&
                (requiredTag == CardTag.None ||
                 activeCards[sourceIndex + 1].Definition.HasTag(requiredTag)))
            {
                count++;
            }

            return count;
        }
    }
}
