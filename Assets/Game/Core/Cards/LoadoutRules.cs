using System;
using System.Collections.Generic;

namespace ProjectArtOfAll.Core.Cards
{
    public enum LoadoutZone
    {
        Bag = 0,
        Battle = 1
    }

    public enum PlacementRejection
    {
        None = 0,
        InvalidCardSize = 1,
        BagCapacity = 2,
        BattleCapacity = 3,
        BattleLocked = 4
    }

    public readonly struct PlacementCheck
    {
        public PlacementCheck(bool allowed, PlacementRejection rejection)
        {
            Allowed = allowed;
            Rejection = rejection;
        }

        public bool Allowed { get; }
        public PlacementRejection Rejection { get; }
    }

    public static class LoadoutRules
    {
        public const int BagCapacity = 10;

        /// <summary>
        /// CJ-5.1 / CJ-6.2：背包这一排固定 10c，战斗栏的宽度另算（CJ-5.2）。所以判断
        /// “背包还放不放得下”只看 <paramref name="bagWidth"/>——装到战斗栏上的牌不占背包容量，
        /// 否则 4 级以后满装备的玩家会连一张奖励牌都拿不了。
        /// </summary>
        public static bool FitsInBag(int bagWidth, int cardSize)
        {
            if (cardSize <= 0)
            {
                return true;
            }

            if (cardSize > 3)
            {
                return false;
            }

            return bagWidth + cardSize <= BagCapacity;
        }

        /// <summary>
        /// CJ-5.2: battle capacity is 4/6/8/10/10c for levels 1 through 5.
        /// Level 6 never enters combat.
        /// </summary>
        public static int GetBattleCapacity(int level)
        {
            switch (level)
            {
                case 1:
                    return 4;
                case 2:
                    return 6;
                case 3:
                    return 8;
                case 4:
                case 5:
                    return 10;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(level),
                        "Combat loadouts only exist for levels 1 through 5.");
            }
        }

        public static PlacementCheck CheckPlacement(
            LoadoutZone zone,
            int currentUsedSlots,
            int cardSize,
            int playerLevel)
        {
            if (currentUsedSlots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentUsedSlots));
            }

            if (cardSize < 1 || cardSize > 3)
            {
                return new PlacementCheck(false, PlacementRejection.InvalidCardSize);
            }

            int capacity = zone == LoadoutZone.Bag
                ? BagCapacity
                : GetBattleCapacity(playerLevel);

            if (currentUsedSlots + cardSize > capacity)
            {
                PlacementRejection reason = zone == LoadoutZone.Bag
                    ? PlacementRejection.BagCapacity
                    : PlacementRejection.BattleCapacity;
                return new PlacementCheck(false, reason);
            }

            return new PlacementCheck(true, PlacementRejection.None);
        }

        public static int CalculateUsedSlots(IEnumerable<int> cardSizes)
        {
            if (cardSizes == null)
            {
                throw new ArgumentNullException(nameof(cardSizes));
            }

            int total = 0;
            foreach (int size in cardSizes)
            {
                if (size < 1 || size > 3)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(cardSizes),
                        "Every card size must be 1c, 2c, or 3c.");
                }

                total += size;
            }

            return total;
        }
    }
}
