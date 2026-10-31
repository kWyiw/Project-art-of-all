using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Util;

namespace ProjectArtOfAll.Core.Match
{
    public sealed class MatchPlayerState
    {
        private readonly PlayerCardCollection cards;

        internal MatchPlayerState(
            int id,
            string name,
            int homeNode,
            int currentNode,
            IEnumerable<CardInstance> cards)
        {
            Id = id;
            Name = name;
            HomeNode = homeNode;
            CurrentNode = currentNode;
            Level = 1;
            Shards = 0;
            Wins = 0;
            Norma = NormaType.Shard;
            this.cards = new PlayerCardCollection(cards, 4);
        }

        public int Id { get; }
        public string Name { get; }
        public int HomeNode { get; }
        public int CurrentNode { get; internal set; }
        public int Level { get; internal set; }
        public int Shards { get; internal set; }
        public int Wins { get; internal set; }
        public NormaType Norma { get; internal set; }
        public IReadOnlyList<CardInstance> OwnedCards => cards.OwnedCards;
        public IReadOnlyList<CardInstance> BattleCards => cards.BattleCards;
        public int OwnedCardWidth => cards.OwnedWidth;
        public int BattleCardWidth => cards.BattleWidth;

        public bool MeetsCurrentNorma()
        {
            return Level >= 6 ||
                   NormaRules.MeetsThreshold(Level, Norma, Shards, Wins);
        }

        internal ulong AddToHash(ulong hash)
        {
            hash = StableHash.Add(hash, Id);
            hash = StableHash.Add(hash, Name);
            hash = StableHash.Add(hash, CurrentNode);
            hash = StableHash.Add(hash, HomeNode);
            hash = StableHash.Add(hash, Level);
            hash = StableHash.Add(hash, Shards);
            hash = StableHash.Add(hash, Wins);
            hash = StableHash.Add(hash, (long)Norma);

            return cards.AddToHash(hash);
        }

        internal bool TryAddCard(CardInstance card, bool equipIfPossible)
        {
            if (!cards.TryAddCard(card))
            {
                return false;
            }

            if (equipIfPossible && Level <= 5)
            {
                cards.TryEquip(card.Uid, LoadoutRules.GetBattleCapacity(Level));
            }

            return true;
        }

        internal bool TrySellCard(int cardUid, out CardInstance removed)
        {
            return cards.TryRemove(cardUid, out removed);
        }

        internal bool TryEquipCard(int cardUid)
        {
            return Level <= 5 &&
                   cards.TryEquip(cardUid, LoadoutRules.GetBattleCapacity(Level));
        }

        internal bool TryUnequipCard(int cardUid)
        {
            return cards.TryUnequip(cardUid);
        }

        internal CardInstance FindCard(int cardUid)
        {
            return cards.FindByUid(cardUid);
        }

        internal bool IsCardEquipped(int cardUid)
        {
            return cards.IsEquipped(cardUid);
        }
    }
}
