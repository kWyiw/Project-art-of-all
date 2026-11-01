using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Util;

namespace ProjectArtOfAll.Core.Match
{
    internal sealed class PlayerCardCollection
    {
        private readonly List<CardInstance> ownedCards = new List<CardInstance>();
        private readonly List<int> battleOrder = new List<int>();

        public PlayerCardCollection(
            IEnumerable<CardInstance> startingCards,
            int startingBattleCapacity)
        {
            if (startingCards == null)
            {
                throw new ArgumentNullException(nameof(startingCards));
            }

            foreach (CardInstance card in startingCards)
            {
                if (!TryAddCard(card))
                {
                    throw new InvalidOperationException(
                        "The starting card collection exceeds the Bag capacity.");
                }
            }

            foreach (CardInstance card in ownedCards.ToArray())
            {
                TryEquip(card.Uid, startingBattleCapacity);
            }
        }

        public IReadOnlyList<CardInstance> OwnedCards => ownedCards;
        public IReadOnlyList<CardInstance> BattleCards => battleOrder
            .Select(FindByUid)
            .Where(card => card != null)
            .ToArray();
        public int OwnedWidth => ownedCards.Sum(card => card.Definition.SlotSize);
        public int BattleWidth => BattleCards.Sum(card => card.Definition.SlotSize);

        public bool TryAddCard(CardInstance card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (ownedCards.Any(existing => existing.Uid == card.Uid) ||
                OwnedWidth + card.Definition.SlotSize > LoadoutRules.BagCapacity)
            {
                return false;
            }

            ownedCards.Add(card);
            return true;
        }

        public bool TryEquip(int cardUid, int battleCapacity)
        {
            CardInstance card = FindByUid(cardUid);
            if (card == null || battleOrder.Contains(cardUid))
            {
                return false;
            }

            if (BattleWidth + card.Definition.SlotSize > battleCapacity)
            {
                return false;
            }

            battleOrder.Add(cardUid);
            return true;
        }

        /// <summary>
        /// Moves an owned card to <paramref name="index"/> inside the battle row,
        /// equipping it when it was in the bag. Order is the visual order (UI 规格 §3.2).
        /// </summary>
        public bool TryMoveToBattle(int cardUid, int battleCapacity, int index)
        {
            CardInstance card = FindByUid(cardUid);
            if (card == null)
            {
                return false;
            }

            int slotSize = card.Definition.SlotSize;
            bool already = battleOrder.Contains(cardUid);
            int widthWithout = BattleWidth - (already ? slotSize : 0);
            if (widthWithout + slotSize > battleCapacity)
            {
                return false;
            }

            battleOrder.Remove(cardUid);
            int clamped = Math.Max(0, Math.Min(index, battleOrder.Count));
            battleOrder.Insert(clamped, cardUid);
            return true;
        }

        /// <summary>Moves a card back to the bag row at <paramref name="index"/>.</summary>
        public bool TryMoveToBag(int cardUid, int index)
        {
            CardInstance card = FindByUid(cardUid);
            if (card == null)
            {
                return false;
            }

            battleOrder.Remove(cardUid);
            ownedCards.Remove(card);
            int clamped = Math.Max(0, Math.Min(index, ownedCards.Count));
            ownedCards.Insert(clamped, card);
            return true;
        }

        public bool TryUnequip(int cardUid)
        {
            return battleOrder.Remove(cardUid);
        }

        public bool TryRemove(int cardUid, out CardInstance removed)
        {
            CardInstance card = FindByUid(cardUid);
            if (card == null)
            {
                removed = null;
                return false;
            }

            battleOrder.Remove(cardUid);
            ownedCards.Remove(card);
            removed = card;
            return true;
        }

        public CardInstance FindByUid(int cardUid)
        {
            return ownedCards.FirstOrDefault(card => card.Uid == cardUid);
        }

        public bool IsEquipped(int cardUid)
        {
            return battleOrder.Contains(cardUid);
        }

        public ulong AddToHash(ulong hash)
        {
            foreach (CardInstance card in ownedCards)
            {
                hash = StableHash.Add(hash, card.Uid);
                hash = StableHash.Add(hash, card.Definition.Id);
            }

            foreach (int cardUid in battleOrder)
            {
                hash = StableHash.Add(hash, cardUid);
            }

            return hash;
        }
    }
}
