using System.Collections.Generic;

namespace ProjectArtOfAll.Core.Cards
{
    /// <summary>
    /// 棋盘层构筑预览：只依赖卡表与排列，不依赖战斗运行时。
    /// 用途是给常驻卡槽带画"相邻加成是否生效"，让玩家在拖拽时立刻看到结果。
    /// 与战斗内光环求值同规则（R-AURA-001~005）：光环每次求值时现算、不写回卡牌，光环不能互相串联。
    /// </summary>
    public static class LoadoutPreview
    {
        /// <summary>第 index 张牌当前能吃到的骰值修正合计（已按来源顺序相加，未夹取）。</summary>
        public static int DiceModifier(IReadOnlyList<CardInstance> row, int index)
        {
            return SumAura(row, index, AuraStat.DiceFlatModifier);
        }

        /// <summary>第 index 张牌当前能吃到的额外骰数（多重释放 / 骰数修正）。</summary>
        public static int BonusDice(IReadOnlyList<CardInstance> row, int index)
        {
            return SumAura(row, index, AuraStat.MultiCast) + SumAura(row, index, AuraStat.DiceCount);
        }

        /// <summary>第 index 张牌的骰值是否被强制取最大值（自噬之蛇）。</summary>
        public static bool HasMaxRoll(IReadOnlyList<CardInstance> row, int index)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (i == index)
                {
                    continue;
                }

                CardInstance source = row[i];
                if (source == null)
                {
                    continue;
                }

                foreach (AuraDefinition aura in source.Definition.Auras)
                {
                    if (aura.Stat == AuraStat.MaxRoll && Matches(row, i, index, aura))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>一句话说明第 index 张牌吃到的加成，没有加成时返回 null。</summary>
        public static string Describe(IReadOnlyList<CardInstance> row, int index)
        {
            int dice = DiceModifier(row, index);
            int extra = BonusDice(row, index);
            bool maxRoll = HasMaxRoll(row, index);
            if (dice == 0 && extra == 0 && !maxRoll)
            {
                return null;
            }

            List<string> parts = new List<string>(3);
            if (dice != 0)
            {
                parts.Add("骰值 " + (dice > 0 ? "+" + dice : dice.ToString()));
            }

            if (extra != 0)
            {
                parts.Add("额外 " + (extra > 0 ? "+" + extra : extra.ToString()) + " 颗骰");
            }

            if (maxRoll)
            {
                parts.Add("骰值必定取最大值");
            }

            return string.Join(" · ", parts);
        }

        private static int SumAura(IReadOnlyList<CardInstance> row, int index, AuraStat stat)
        {
            if (row == null || index < 0 || index >= row.Count || row[index] == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < row.Count; i++)
            {
                CardInstance source = row[i];
                if (source == null)
                {
                    continue;
                }

                foreach (AuraDefinition aura in source.Definition.Auras)
                {
                    // 自指光环（火堆给自己的多重释放）也要算进来，所以这里不跳过 i == index。
                    if (aura.Stat != stat || !Matches(row, i, index, aura))
                    {
                        continue;
                    }

                    total += aura.Amount * ScalingFactor(row, i, aura);
                }
            }

            return total;
        }

        /// <summary>光环来源 i 是否覆盖目标 index（R-AURA-004 的五个选择器 + 词条过滤 + 尺寸过滤）。</summary>
        private static bool Matches(
            IReadOnlyList<CardInstance> row,
            int sourceIndex,
            int targetIndex,
            AuraDefinition aura)
        {
            CardInstance target = row[targetIndex];
            if (target == null)
            {
                return false;
            }

            if (aura.RequiredTag != CardTag.None && (target.Definition.Tags & aura.RequiredTag) == 0)
            {
                return false;
            }

            if (aura.MinimumSlotSize > 0 && target.Definition.SlotSize < aura.MinimumSlotSize)
            {
                return false;
            }

            bool adjacent = sourceIndex + 1 == targetIndex || sourceIndex - 1 == targetIndex;
            switch (aura.Target)
            {
                case AuraTarget.Self:
                    return sourceIndex == targetIndex;
                case AuraTarget.AdjacentLeft:
                    return sourceIndex - 1 == targetIndex;
                case AuraTarget.AdjacentRight:
                    return sourceIndex + 1 == targetIndex;
                case AuraTarget.AllAdjacent:
                    return adjacent;
                case AuraTarget.AllByTag:
                    // 全场按词条：不限相邻，由上面的 RequiredTag 过滤决定谁受益。
                    return true;
                default:
                    return false;
            }
        }

        private static int ScalingFactor(IReadOnlyList<CardInstance> row, int sourceIndex, AuraDefinition aura)
        {
            if (aura.Scaling != AuraScaling.PerAdjacentMatchingTag)
            {
                return 1;
            }

            int count = 0;
            for (int i = 0; i < row.Count; i++)
            {
                // 只数源卡的左右邻居（与战斗内 AuraResolver 的 Self 缩放口径一致）。
                if (row[i] == null || i == sourceIndex)
                {
                    continue;
                }

                if (i != sourceIndex - 1 && i != sourceIndex + 1)
                {
                    continue;
                }

                if (aura.RequiredTag == CardTag.None ||
                    (row[i].Definition.Tags & aura.RequiredTag) != 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
