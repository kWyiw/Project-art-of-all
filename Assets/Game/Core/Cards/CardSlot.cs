using System.Collections.Generic;
using System.Linq;

namespace ProjectArtOfAll.Core.Cards
{
    /// <summary>
    /// 一张牌摆在栏位里的位置：从该栏位最左一列算起的列号 + 牌本身。
    /// 栏位是一排独立格子，牌占连续的 <see cref="CardInstance.Definition"/> 个格子，
    /// 格子之间允许留空（玩家可以主动把两张牌分开，避开相邻光环）。
    /// </summary>
    public readonly struct CardSlot
    {
        public CardSlot(int column, CardInstance card)
        {
            Column = column;
            Card = card;
        }

        /// <summary>牌占据的最左一列（0 起）。</summary>
        public int Column { get; }

        public CardInstance Card { get; }

        public bool IsEmpty => Card == null;

        /// <summary>牌占据的最右一列（含）。</summary>
        public int RightColumn => Card == null
            ? Column
            : Column + Card.Definition.SlotSize - 1;

        /// <summary>该牌是否覆盖 <paramref name="column"/> 这一列。</summary>
        public bool Covers(int column)
        {
            return Card != null && column >= Column && column <= RightColumn;
        }

        public override string ToString()
        {
            return Card == null
                ? "empty@" + Column
                : Card.Definition.Id + "@" + Column;
        }
    }

    public static class CardSlotRules
    {
        /// <summary>把一排占位写回“压紧”的牌列表（旧口径：按列顺序、忽略空格）。</summary>
        public static IReadOnlyList<CardInstance> Pack(IEnumerable<CardSlot> slots)
        {
            if (slots == null)
            {
                return new CardInstance[0];
            }

            return slots
                .Where(slot => slot.Card != null)
                .OrderBy(slot => slot.Column)
                .Select(slot => slot.Card)
                .ToArray();
        }

        /// <summary>把一排压紧的牌按 <paramref name="startColumn"/> 起连续摆放。</summary>
        public static IReadOnlyList<CardSlot> Unpack(
            IEnumerable<CardInstance> cards,
            int startColumn = 0)
        {
            if (cards == null)
            {
                return new CardSlot[0];
            }

            List<CardSlot> slots = new List<CardSlot>();
            int column = startColumn;
            foreach (CardInstance card in cards)
            {
                if (card == null)
                {
                    continue;
                }

                slots.Add(new CardSlot(column, card));
                column += card.Definition.SlotSize;
            }

            return slots;
        }

        /// <summary>两段栏位是否重叠（忽略 <paramref name="ignoreUid"/> 指向的牌，便于原地移动）。</summary>
        public static bool Overlaps(
            IEnumerable<CardSlot> slots,
            int column,
            int size,
            int ignoreUid = -1)
        {
            if (slots == null || size <= 0)
            {
                return false;
            }

            int right = column + size - 1;
            foreach (CardSlot slot in slots)
            {
                if (slot.Card == null || slot.Card.Uid == ignoreUid)
                {
                    continue;
                }

                if (slot.Column <= right && slot.RightColumn >= column)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>在 <paramref name="capacity"/> 里找最靠左能塞下 <paramref name="size"/> 个格子的位置。</summary>
        public static int FindFreeColumn(
            IEnumerable<CardSlot> slots,
            int size,
            int capacity,
            int ignoreUid = -1,
            int preferredColumn = -1)
        {
            if (size <= 0 || capacity < size)
            {
                return -1;
            }

            int last = capacity - size;
            if (preferredColumn >= 0 && preferredColumn <= last &&
                !Overlaps(slots, preferredColumn, size, ignoreUid))
            {
                return preferredColumn;
            }

            for (int column = 0; column <= last; column++)
            {
                if (!Overlaps(slots, column, size, ignoreUid))
                {
                    return column;
                }
            }

            return -1;
        }

        /// <summary>离 <paramref name="wantedColumn"/> 最近的合法空位（拖拽落点用）。</summary>
        public static int NearestFreeColumn(
            IEnumerable<CardSlot> slots,
            int size,
            int capacity,
            int wantedColumn,
            int ignoreUid = -1)
        {
            List<CardSlot> list = slots == null ? new List<CardSlot>() : slots.ToList();
            int last = capacity - size;
            if (last < 0)
            {
                return -1;
            }

            int clamped = System.Math.Max(0, System.Math.Min(wantedColumn, last));
            if (!Overlaps(list, clamped, size, ignoreUid))
            {
                return clamped;
            }

            for (int step = 1; step <= capacity; step++)
            {
                int left = clamped - step;
                if (left >= 0 && !Overlaps(list, left, size, ignoreUid))
                {
                    return left;
                }

                int right = clamped + step;
                if (right <= last && !Overlaps(list, right, size, ignoreUid))
                {
                    return right;
                }
            }

            return -1;
        }
    }
}
