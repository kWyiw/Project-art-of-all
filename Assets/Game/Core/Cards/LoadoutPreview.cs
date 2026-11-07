using System;
using System.Collections.Generic;

namespace ProjectArtOfAll.Core.Cards
{
    /// <summary>
    /// 棋盘层构筑预览：只依赖卡表与排列，不依赖战斗运行时。
    /// 用途是给常驻卡槽带画"相邻加成是否生效"，让玩家在拖拽时立刻看到结果。
    /// 与战斗内光环求值同规则（R-AURA-001~005）：光环每次求值时现算、不写回卡牌，光环不能互相串联。
    ///
    /// 排列有两种口径：背包/战斗栏现在是一排格子，牌可以放在任意空位、中间可以留空，
    /// 相邻只看列号是否相连；只给了牌列表的旧重载按"从第 0 列连续摆放"处理。
    /// </summary>
    public static class LoadoutPreview
    {
        /// <summary>第 index 张牌当前能吃到的骰值修正合计（已按来源顺序相加，未夹取）。</summary>
        public static int DiceModifier(IReadOnlyList<CardInstance> row, int index)
        {
            return DiceModifier(Pack(row), index);
        }

        /// <summary>第 index 张牌当前能吃到的额外骰数（多重释放 / 骰数修正）。</summary>
        public static int BonusDice(IReadOnlyList<CardInstance> row, int index)
        {
            return BonusDice(Pack(row), index);
        }

        /// <summary>第 index 张牌的骰值是否被强制取最大值（自噬之蛇）。</summary>
        public static bool HasMaxRoll(IReadOnlyList<CardInstance> row, int index)
        {
            return HasMaxRoll(Pack(row), index);
        }

        /// <summary>一句话说明第 index 张牌吃到的加成，没有加成时返回 null。</summary>
        public static string Describe(IReadOnlyList<CardInstance> row, int index)
        {
            return Describe(Pack(row), index);
        }

        /// <summary>按栏位排列（<paramref name="slots"/>）看某张牌的骰值修正。</summary>
        public static int DiceModifier(IReadOnlyList<CardSlot> slots, int cardUid)
        {
            List<Placed> row = Place(slots);
            int index = IndexOf(row, cardUid);
            return index < 0 ? 0 : SumAura(row, index, AuraStat.DiceFlatModifier);
        }

        public static int BonusDice(IReadOnlyList<CardSlot> slots, int cardUid)
        {
            List<Placed> row = Place(slots);
            int index = IndexOf(row, cardUid);
            return index < 0
                ? 0
                : SumAura(row, index, AuraStat.MultiCast) +
                  SumAura(row, index, AuraStat.DiceCount);
        }

        public static bool HasMaxRoll(IReadOnlyList<CardSlot> slots, int cardUid)
        {
            List<Placed> row = Place(slots);
            int index = IndexOf(row, cardUid);
            if (index < 0)
            {
                return false;
            }

            for (int i = 0; i < row.Count; i++)
            {
                if (i == index)
                {
                    continue;
                }

                foreach (AuraDefinition aura in row[i].Card.Definition.Auras)
                {
                    if (aura.Stat == AuraStat.MaxRoll && Matches(row[i], row[index], aura))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static string Describe(IReadOnlyList<CardSlot> slots, int cardUid)
        {
            List<Placed> row = Place(slots);
            int index = IndexOf(row, cardUid);
            return index < 0 ? null : DescribeSlot(row, index);
        }

        private static int DiceModifier(IReadOnlyList<Placed> row, int index)
        {
            return SumAura(row, index, AuraStat.DiceFlatModifier);
        }

        private static int BonusDice(IReadOnlyList<Placed> row, int index)
        {
            return SumAura(row, index, AuraStat.MultiCast) + SumAura(row, index, AuraStat.DiceCount);
        }

        private static bool HasMaxRoll(IReadOnlyList<Placed> row, int index)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (i == index)
                {
                    continue;
                }

                foreach (AuraDefinition aura in row[i].Card.Definition.Auras)
                {
                    if (aura.Stat == AuraStat.MaxRoll && Matches(row[i], row[index], aura))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string Describe(IReadOnlyList<Placed> row, int index)
        {
            return index < 0 || index >= row.Count ? null : DescribeSlot(row, index);
        }

        private static string DescribeSlot(IReadOnlyList<Placed> row, int index)
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

        private static int SumAura(
            IReadOnlyList<Placed> row,
            int index,
            AuraStat stat)
        {
            if (row == null || index < 0 || index >= row.Count)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < row.Count; i++)
            {
                foreach (AuraDefinition aura in row[i].Card.Definition.Auras)
                {
                    // 自指光环（火堆给自己的多重释放）也要算进来，所以这里不跳过 i == index。
                    if (aura.Stat != stat || !Matches(row[i], row[index], aura))
                    {
                        continue;
                    }

                    total += aura.Amount * ScalingFactor(row, i, aura);
                }
            }

            return total;
        }

        /// <summary>光环来源是否覆盖目标（R-AURA-004 的五个选择器 + 词条过滤 + 尺寸过滤）。</summary>
        private static bool Matches(Placed source, Placed target, AuraDefinition aura)
        {
            if (aura.RequiredTag != CardTag.None &&
                (target.Card.Definition.Tags & aura.RequiredTag) == 0)
            {
                return false;
            }

            if (aura.MinimumSlotSize > 0 &&
                target.Card.Definition.SlotSize < aura.MinimumSlotSize)
            {
                return false;
            }

            switch (aura.Target)
            {
                case AuraTarget.Self:
                    return ReferenceEquals(source.Card, target.Card);
                case AuraTarget.AdjacentLeft:
                    // 目标是源卡的左邻：两张牌紧贴，中间不能留空。
                    return target.IsImmediatelyLeftOf(source);
                case AuraTarget.AdjacentRight:
                    return target.IsImmediatelyRightOf(source);
                case AuraTarget.AllAdjacent:
                    return target.Card != source.Card && target.IsAdjacentTo(source);
                case AuraTarget.AllByTag:
                    // 全场按词条：不限相邻，由上面的 RequiredTag 过滤决定谁受益。
                    return true;
                default:
                    return false;
            }
        }

        private static int ScalingFactor(
            IReadOnlyList<Placed> row,
            int sourceIndex,
            AuraDefinition aura)
        {
            if (aura.Scaling != AuraScaling.PerAdjacentMatchingTag)
            {
                return 1;
            }

            int count = 0;
            for (int i = 0; i < row.Count; i++)
            {
                if (i == sourceIndex || !row[i].IsAdjacentTo(row[sourceIndex]))
                {
                    continue;
                }

                if (aura.RequiredTag == CardTag.None ||
                    (row[i].Card.Definition.Tags & aura.RequiredTag) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<Placed> Pack(IReadOnlyList<CardInstance> row)
        {
            List<Placed> placed = new List<Placed>();
            if (row == null)
            {
                return placed;
            }

            int column = 0;
            foreach (CardInstance card in row)
            {
                if (card == null)
                {
                    continue;
                }

                placed.Add(new Placed(column, card));
                column += Math.Max(1, card.Definition.SlotSize);
            }

            return placed;
        }

        private static List<Placed> Place(IReadOnlyList<CardSlot> slots)
        {
            List<Placed> placed = new List<Placed>();
            if (slots == null)
            {
                return placed;
            }

            List<CardSlot> sorted = new List<CardSlot>(slots);
            sorted.Sort((left, right) => left.Column.CompareTo(right.Column));
            foreach (CardSlot slot in sorted)
            {
                if (slot.Card != null)
                {
                    placed.Add(new Placed(slot.Column, slot.Card));
                }
            }

            return placed;
        }

        private static int IndexOf(IReadOnlyList<Placed> row, int cardUid)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (row[i].Card.Uid == cardUid)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>一张牌在栏位里的位置：列号 + 牌。</summary>
        private readonly struct Placed
        {
            public Placed(int column, CardInstance card)
            {
                Column = column;
                Card = card;
            }

            public int Column { get; }
            public CardInstance Card { get; }

            public int Size => Math.Max(1, Card.Definition.SlotSize);

            public int RightColumn => Column + Size - 1;

            /// <summary>本卡紧贴 <paramref name="other"/> 的左侧（中间没有空格）。</summary>
            public bool IsImmediatelyLeftOf(Placed other)
            {
                return other.Card != null && RightColumn + 1 == other.Column;
            }

            public bool IsImmediatelyRightOf(Placed other)
            {
                return other.Card != null && other.RightColumn + 1 == Column;
            }

            public bool IsAdjacentTo(Placed other)
            {
                return IsImmediatelyLeftOf(other) || IsImmediatelyRightOf(other);
            }
        }
    }
}
