using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Util;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// 玩家持有的牌。背包与战斗各是一排栏位，每张牌占连续的 SlotSize 个格子，
    /// 牌与牌之间允许留空：玩家可以把牌放在任意合法空位，从而主动拆散相邻关系，
    /// 或者刻意把两张牌贴在一起吃相邻光环（R-AURA-001 / UI 规格 §3.2）。
    /// </summary>
    internal sealed class PlayerCardCollection
    {
        private readonly List<CardSlot> bagSlots = new List<CardSlot>();
        private readonly List<CardSlot> battleSlots = new List<CardSlot>();

        public PlayerCardCollection(
            IEnumerable<CardInstance> startingCards,
            int startingBattleCapacity)
        {
            if (startingCards == null)
            {
                throw new ArgumentNullException(nameof(startingCards));
            }

            int bagColumn = 0;
            foreach (CardInstance card in startingCards)
            {
                if (card == null)
                {
                    continue;
                }

                int column = CardSlotRules.FindFreeColumn(
                    bagSlots,
                    card.Definition.SlotSize,
                    LoadoutRules.BagCapacity,
                    preferredColumn: bagColumn);
                if (column < 0 || !TryPlace(bagSlots, card, column))
                {
                    throw new InvalidOperationException(
                        "The starting card collection exceeds the Bag capacity.");
                }

                bagColumn = column + card.Definition.SlotSize;
            }

            int battleColumn = 0;
            foreach (CardSlot slot in bagSlots.OrderBy(slot => slot.Column).ToList())
            {
                int column = CardSlotRules.FindFreeColumn(
                    battleSlots,
                    slot.Card.Definition.SlotSize,
                    startingBattleCapacity,
                    preferredColumn: battleColumn);
                if (column < 0)
                {
                    break;
                }

                if (TryMoveBetween(bagSlots, battleSlots, slot.Card, column))
                {
                    battleColumn = column + slot.Card.Definition.SlotSize;
                }
            }
        }

        /// <summary>全部已拥有的牌（背包在前、战斗在后，各自按列从左到右）。</summary>
        public IReadOnlyList<CardInstance> OwnedCards => BagCards.Concat(BattleCards).ToArray();

        /// <summary>背包栏位里的牌，按列从左到右。</summary>
        public IReadOnlyList<CardInstance> BagCards => bagSlots
            .OrderBy(slot => slot.Column)
            .Select(slot => slot.Card)
            .ToArray();

        /// <summary>战斗栏位里的牌，按列从左到右。</summary>
        public IReadOnlyList<CardInstance> BattleCards => battleSlots
            .OrderBy(slot => slot.Column)
            .Select(slot => slot.Card)
            .ToArray();

        /// <summary>背包栏位的占位（只含非空牌，按列排序）。</summary>
        public IReadOnlyList<CardSlot> BagSlots => bagSlots
            .OrderBy(slot => slot.Column)
            .ToArray();

        /// <summary>战斗栏位的占位（只含非空牌，按列排序）。</summary>
        public IReadOnlyList<CardSlot> BattleSlots => battleSlots
            .OrderBy(slot => slot.Column)
            .ToArray();

        public int OwnedWidth =>
            bagSlots.Sum(slot => slot.Card.Definition.SlotSize) +
            battleSlots.Sum(slot => slot.Card.Definition.SlotSize);

        public int BattleWidth => battleSlots.Sum(slot => slot.Card.Definition.SlotSize);

        public bool TryAddCard(CardInstance card)
        {
            return TryAddCardAt(card, -1);
        }

        /// <summary>把新牌放进背包：给了 <paramref name="column"/> 就放那里，否则最靠左的合法空位。</summary>
        public bool TryAddCardAt(CardInstance card, int column)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (OwnedCards.Any(existing => existing.Uid == card.Uid) ||
                OwnedWidth + card.Definition.SlotSize > LoadoutRules.BagCapacity)
            {
                return false;
            }

            int target = CardSlotRules.FindFreeColumn(
                bagSlots,
                card.Definition.SlotSize,
                LoadoutRules.BagCapacity,
                preferredColumn: column);
            return target >= 0 && TryPlace(bagSlots, card, target);
        }

        public bool TryEquip(int cardUid, int battleCapacity)
        {
            return TryEquipAt(cardUid, battleCapacity, -1);
        }

        public bool TryEquipAt(int cardUid, int battleCapacity, int column)
        {
            CardInstance card = FindByUid(cardUid);
            if (card == null || IsEquipped(cardUid))
            {
                return false;
            }

            if (BattleWidth + card.Definition.SlotSize > battleCapacity)
            {
                return false;
            }

            int target = CardSlotRules.FindFreeColumn(
                battleSlots,
                card.Definition.SlotSize,
                battleCapacity,
                preferredColumn: column);
            return target >= 0 && TryMoveBetween(bagSlots, battleSlots, card, target);
        }

        /// <summary>
        /// 把牌摆到战斗栏位的 <paramref name="column"/> 列（从背包取出或在本行内移动）。
        /// 该列放不下时返回 false，由 UI 吸附到最近的合法空位。
        /// </summary>
        public bool TryMoveToBattle(int cardUid, int battleCapacity, int column)
        {
            CardInstance card = FindByUid(cardUid);
            if (card == null)
            {
                return false;
            }

            int slotSize = card.Definition.SlotSize;
            bool alreadyEquipped = IsEquipped(cardUid);
            int widthWithout = BattleWidth - (alreadyEquipped ? slotSize : 0);
            if (widthWithout + slotSize > battleCapacity)
            {
                return false;
            }

            int target = CardSlotRules.FindFreeColumn(
                battleSlots,
                slotSize,
                battleCapacity,
                ignoreUid: cardUid,
                preferredColumn: column);
            if (target < 0)
            {
                return false;
            }

            return TryMoveBetween(bagSlots, battleSlots, card, target);
        }

        /// <summary>把牌摆回背包栏位的 <paramref name="column"/> 列。</summary>
        public bool TryMoveToBag(int cardUid, int column)
        {
            CardInstance card = FindByUid(cardUid);
            if (card == null)
            {
                return false;
            }

            int target = CardSlotRules.FindFreeColumn(
                bagSlots,
                card.Definition.SlotSize,
                LoadoutRules.BagCapacity,
                ignoreUid: cardUid,
                preferredColumn: column);
            if (target < 0)
            {
                return false;
            }

            return TryMoveBetween(battleSlots, bagSlots, card, target);
        }

        public bool TryUnequip(int cardUid)
        {
            CardInstance card = FindByUid(cardUid);
            if (card == null || !IsEquipped(cardUid))
            {
                return false;
            }

            int target = CardSlotRules.FindFreeColumn(
                bagSlots,
                card.Definition.SlotSize,
                LoadoutRules.BagCapacity,
                ignoreUid: cardUid);
            return target >= 0 && TryMoveBetween(battleSlots, bagSlots, card, target);
        }

        public bool TryRemove(int cardUid, out CardInstance removed)
        {
            removed = FindByUid(cardUid);
            if (removed == null)
            {
                return false;
            }

            Remove(bagSlots, cardUid);
            Remove(battleSlots, cardUid);
            return true;
        }

        public CardInstance FindByUid(int cardUid)
        {
            CardSlot? bag = Find(bagSlots, cardUid);
            if (bag.HasValue)
            {
                return bag.Value.Card;
            }

            CardSlot? battle = Find(battleSlots, cardUid);
            return battle?.Card;
        }

        /// <summary>牌当前所在的栏位列号，不在任何栏位时返回 -1。</summary>
        public int ColumnOf(int cardUid)
        {
            CardSlot? bag = Find(bagSlots, cardUid);
            if (bag.HasValue)
            {
                return bag.Value.Column;
            }

            CardSlot? battle = Find(battleSlots, cardUid);
            return battle?.Column ?? -1;
        }

        public bool IsEquipped(int cardUid)
        {
            return Find(battleSlots, cardUid).HasValue;
        }

        public ulong AddToHash(ulong hash)
        {
            foreach (CardSlot slot in bagSlots.OrderBy(slot => slot.Column))
            {
                hash = StableHash.Add(hash, slot.Card.Uid);
                hash = StableHash.Add(hash, slot.Card.Definition.Id);
                hash = StableHash.Add(hash, 0);
                hash = StableHash.Add(hash, slot.Column);
            }

            foreach (CardSlot slot in battleSlots.OrderBy(slot => slot.Column))
            {
                hash = StableHash.Add(hash, slot.Card.Uid);
                hash = StableHash.Add(hash, slot.Card.Definition.Id);
                hash = StableHash.Add(hash, 1);
                hash = StableHash.Add(hash, slot.Column);
            }

            return hash;
        }

        private static bool TryPlace(List<CardSlot> slots, CardInstance card, int column)
        {
            if (card == null || column < 0 ||
                CardSlotRules.Overlaps(slots, column, card.Definition.SlotSize, card.Uid))
            {
                return false;
            }

            Remove(slots, card.Uid);
            slots.Add(new CardSlot(column, card));
            return true;
        }

        private static bool TryMoveBetween(
            List<CardSlot> from,
            List<CardSlot> to,
            CardInstance card,
            int column)
        {
            if (card == null || column < 0 ||
                CardSlotRules.Overlaps(to, column, card.Definition.SlotSize, card.Uid))
            {
                return false;
            }

            Remove(from, card.Uid);
            Remove(to, card.Uid);
            to.Add(new CardSlot(column, card));
            return true;
        }

        private static void Remove(List<CardSlot> slots, int cardUid)
        {
            slots.RemoveAll(slot => slot.Card != null && slot.Card.Uid == cardUid);
        }

        private static CardSlot? Find(List<CardSlot> slots, int cardUid)
        {
            foreach (CardSlot slot in slots)
            {
                if (slot.Card != null && slot.Card.Uid == cardUid)
                {
                    return slot;
                }
            }

            return null;
        }
    }
}
