using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Match;

namespace ProjectArtOfAll.Core.Cards
{
    public enum CardUnlockKind
    {
        Normal = 0,
        EventLimited = 1,
        FusionUpgrade = 2
    }

    /// <summary>Stable catalog metadata shared by the codex UI, combat lab and tests.</summary>
    public static class CardCatalogMetadata
    {
        private static readonly CardTag[] TypeFilterOrder =
        {
            CardTag.Weapon,
            CardTag.Tool,
            CardTag.Waste,
            CardTag.Food,
            CardTag.Companion,
            CardTag.Trap,
            CardTag.Estate,
            CardTag.Craft,
            CardTag.Resource,
            CardTag.Mascot,
            CardTag.Disease
        };

        public static IReadOnlyList<CardDefinition> All =>
            CardDatabase.Instance.AllDefinitions;

        public static IReadOnlyList<CardTag> TypeFilters => TypeFilterOrder;

        public static CardUnlockKind AcquisitionOf(CardDefinition card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (card.IsUpgraded)
            {
                return CardUnlockKind.FusionUpgrade;
            }

            return MatchEventCatalog.IsEventOnlyCard(card.Id)
                ? CardUnlockKind.EventLimited
                : CardUnlockKind.Normal;
        }

        public static string AcquisitionLabel(CardUnlockKind kind)
        {
            switch (kind)
            {
                case CardUnlockKind.EventLimited:
                    return "事件限定";
                case CardUnlockKind.FusionUpgrade:
                    return "融合升级";
                default:
                    return "正常获得";
            }
        }

        public static string PrimaryTypeLabel(CardDefinition card)
        {
            CardTag tag = PrimaryTag(card);
            return tag == CardTag.None ? "无类型" : CardText.TagName(tag);
        }

        public static CardTag PrimaryTag(CardDefinition card)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            foreach (CardTag tag in TypeFilterOrder)
            {
                if (card.HasTag(tag))
                {
                    return tag;
                }
            }

            return CardTag.None;
        }

        public static IReadOnlyList<CardDefinition> Filter(
            int slotSize = 0,
            CardTag type = CardTag.None,
            CardUnlockKind? acquisition = null)
        {
            return All
                .Where(card => slotSize <= 0 || card.SlotSize == slotSize)
                .Where(card => type == CardTag.None || card.HasTag(type))
                .Where(card =>
                    !acquisition.HasValue ||
                    AcquisitionOf(card) == acquisition.Value)
                .OrderBy(card => card.SlotSize)
                .ThenBy(card => TypeIndex(card))
                .ThenBy(card => card.BaseId, StringComparer.Ordinal)
                .ThenBy(card => card.IsUpgraded ? 1 : 0)
                .ToArray();
        }

        private static int TypeIndex(CardDefinition card)
        {
            CardTag primary = PrimaryTag(card);
            for (int index = 0; index < TypeFilterOrder.Length; index++)
            {
                if (TypeFilterOrder[index] == primary)
                {
                    return index;
                }
            }

            return TypeFilterOrder.Length;
        }
    }
}
