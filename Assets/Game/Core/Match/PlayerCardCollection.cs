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

        /// <summary>背包这一排已占的宽度（不含战斗栏）。</summary>
        public int BagWidth => bagSlots.Sum(slot => slot.Card.Definition.SlotSize);

        public bool TryAddCard(CardInstance card)
        {
            return TryAddCardAt(card, -1);
        }

        /// <summary>
        /// 找到一张可以融合升级的同名牌：还没有升级过、而且自己手里确实有它
        /// （CJ-5.11）。战斗栏优先于背包，各自按列从左到右，结果稳定可复现。
        /// </summary>
        public CardInstance FindUpgradableCopy(string baseId)
        {
            if (string.IsNullOrEmpty(baseId))
            {
                return null;
            }

            foreach (CardSlot slot in battleSlots
                         .OrderBy(slot => slot.Column)
                         .Concat(bagSlots.OrderBy(slot => slot.Column)))
            {
                CardInstance card = slot.Card;
                if (card == null || !card.CanUpgrade)
                {
                    continue;
                }

                if (string.Equals(card.Definition.BaseId, baseId, StringComparison.Ordinal))
                {
                    return card;
                }
            }

            return null;
        }

        /// <summary>把新牌放进背包：给了 <paramref name="column"/> 就放那里，否则最靠左的合法空位。</summary>
        public bool TryAddCardAt(CardInstance card, int column)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (OwnedCards.Any(existing => existing.Uid == card.Uid) ||
                !LoadoutRules.FitsInBag(BagWidth, card.Definition.SlotSize))
            {
                return false;
            }

            int target = CardSlotRules.FindFreeColumn(
                bagSlots,
                card.Definition.SlotSize,
                LoadoutRules.BagCapacity,
                preferredColumn: column);
            if (target >= 0)
            {
                return TryPlace(bagSlots, card, target);
            }

            // R-ECO-006 的容量口径是背包总宽度。零散空格可能拼不出连续位置，
            // 此时按原顺序做最小位移重排，避免“还剩 5c 却收不下 2c 牌”。
            return TryRepackWithCard(card);
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
                hash = AddCardGrowth(hash, slot.Card);
            }

            foreach (CardSlot slot in battleSlots.OrderBy(slot => slot.Column))
            {
                hash = StableHash.Add(hash, slot.Card.Uid);
                hash = StableHash.Add(hash, slot.Card.Definition.Id);
                hash = StableHash.Add(hash, 1);
                hash = StableHash.Add(hash, slot.Column);
                hash = AddCardGrowth(hash, slot.Card);
            }

            return hash;
        }

        private static ulong AddCardGrowth(ulong hash, CardInstance card)
        {
            hash = StableHash.Add(hash, card.ValueBonus);
            hash = StableHash.Add(hash, card.PermanentDiceModifier);
            hash = StableHash.Add(hash, card.PermanentDiceCountBonus);
            hash = StableHash.Add(hash, card.PermanentHealingModifier);
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

        private bool TryRepackWithCard(CardInstance card)
        {
            List<CardSlot> existing = bagSlots
                .OrderBy(slot => slot.Column)
                .ToList();
            int usedWidth = existing.Sum(slot => slot.Card.Definition.SlotSize);
            if (usedWidth + card.Definition.SlotSize > LoadoutRules.BagCapacity)
            {
                return false;
            }

            int bestInsertion = -1;
            int bestMovement = int.MaxValue;
            int bestNewColumn = int.MaxValue;
            for (int insertion = 0; insertion <= existing.Count; insertion++)
            {
                int column = 0;
                int existingIndex = 0;
                int movement = 0;
                int newColumn = -1;
                bool valid = true;

                for (int order = 0; order <= existing.Count; order++)
                {
                    if (order == insertion)
                    {
                        newColumn = column;
                        column += card.Definition.SlotSize;
                    }
                    else
                    {
                        CardSlot slot = existing[existingIndex++];
                        movement += Math.Abs(slot.Column - column);
                        column += slot.Card.Definition.SlotSize;
                    }

                    if (column > LoadoutRules.BagCapacity)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid &&
                    (movement < bestMovement ||
                     (movement == bestMovement && newColumn < bestNewColumn)))
                {
                    bestInsertion = insertion;
                    bestMovement = movement;
                    bestNewColumn = newColumn;
                }
            }

            if (bestInsertion < 0)
            {
                return false;
            }

            List<CardInstance> ordered = existing
                .Select(slot => slot.Card)
                .ToList();
            ordered.Insert(bestInsertion, card);

            bagSlots.Clear();
            int packedColumn = 0;
            foreach (CardInstance packed in ordered)
            {
                bagSlots.Add(new CardSlot(packedColumn, packed));
                packedColumn += packed.Definition.SlotSize;
            }

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
