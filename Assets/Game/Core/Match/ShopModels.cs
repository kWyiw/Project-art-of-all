using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ProjectArtOfAll.Core.Cards;

namespace ProjectArtOfAll.Core.Match
{
    public sealed class ShopOffer
    {
        internal ShopOffer(CardDefinition definition, int price)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Price = price;
        }

        public CardDefinition Definition { get; }
        public int Price { get; }
        public bool IsAvailable { get; internal set; } = true;
    }

    public sealed class ShopSession
    {
        private readonly List<ShopOffer> offers;

        internal ShopSession(IEnumerable<ShopOffer> offers)
        {
            this.offers = offers.ToList();
            Offers = new ReadOnlyCollection<ShopOffer>(this.offers);
        }

        public IReadOnlyList<ShopOffer> Offers { get; }
        public bool RerollUsed { get; internal set; }

        internal void ReplaceOffers(IEnumerable<ShopOffer> newOffers)
        {
            offers.Clear();
            offers.AddRange(newOffers);
        }
    }
}
