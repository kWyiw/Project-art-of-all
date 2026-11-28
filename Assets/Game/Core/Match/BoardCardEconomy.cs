using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Cards;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// 棋盘层的卡牌经济被动（设计稿里写在「战斗外成长」那些行）。
    ///
    /// 战斗内的被动是 <see cref="Cards.PassiveDefinition"/> 数据，由 CombatSimulation 跑；
    /// 棋盘层没有事件总线可以挂，所以这一层按基础牌 id 结算，集中在这一个类里，
    /// 方便以后整体搬成数据（对照表见 规则书/卡牌实现核对表.md）。
    /// </summary>
    internal static class BoardCardEconomy
    {
        public const string ShredderId = "shredder";
        public const string SnowPurifierId = "snow_purifier";
        public const string RecyclerId = "recycler";
        public const string FreshWaterId = "fresh_water";
        public const string DorayakiId = "dorayaki";
        public const string FortuneCookieId = "fortune_cookie";
        public const string GoldenChariotId = "golden_chariot";
        public const string SixSixStreakId = "six_six_streak";
        public const string ValuePlaceholder7Id = "placeholder_7";
        public const string DamageCompanionId = "placeholder_1";

        /// <summary>出售这张牌时，给「最左侧的某类牌」加多少永久修正（升级版翻倍）。</summary>
        public static int SaleBuffAmount(CardDefinition sold)
        {
            if (sold == null)
            {
                return 0;
            }

            switch (sold.BaseId)
            {
                case FortuneCookieId:
                case FreshWaterId:
                    return sold.IsUpgraded ? 2 : 1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 回收站：战斗栏里放着回收站时，出售的废料按大小多卖 1/2/3 碎片；
        /// 升级版回收站翻倍（设计稿「升级版回收站对废料牌的价值加成翻倍」）。
        /// </summary>
        public static int RecyclerBonus(MatchPlayerState player, CardInstance card)
        {
            if (player == null || card == null || !card.Definition.HasTag(CardTag.Waste))
            {
                return 0;
            }

            int bonus = 0;
            foreach (CardInstance owned in player.OwnedCards)
            {
                if (!string.Equals(owned.Definition.BaseId, RecyclerId, StringComparison.Ordinal))
                {
                    continue;
                }

                int perSize = owned.IsUpgraded ? 2 : 1;
                bonus += Math.Max(1, card.Definition.SlotSize) * perSize;
            }

            return bonus;
        }

        /// <summary>出售时的实际成交价：牌面价值 + 回收站加成。</summary>
        public static int SaleValue(MatchPlayerState player, CardInstance card)
        {
            if (card == null)
            {
                return 0;
            }

            int eventSaleBonus = string.Equals(
                    card.Definition.BaseId,
                    "mystery_drink",
                    StringComparison.Ordinal)
                ? 6
                : 0;
            return card.Value + RecyclerBonus(player, card) + eventSaleBonus;
        }

        public static int EffectiveValue(MatchPlayerState player, CardInstance card)
        {
            return card == null ? 0 : card.Value + RecyclerBonus(player, card);
        }

        /// <summary>出售结算后的成长：幸运饼干 / 淡水 / 黄金战车。</summary>
        public static void OnCardSold(MatchPlayerState seller, CardInstance sold)
        {
            if (seller == null || sold == null)
            {
                return;
            }

            string baseId = sold.Definition.BaseId;
            if (string.Equals(baseId, FortuneCookieId, StringComparison.Ordinal))
            {
                BuffLeftmost(
                    seller,
                    definition => definition.HasTag(CardTag.Food),
                    SaleBuffAmount(sold.Definition),
                    healingOnly: false);
            }
            else if (string.Equals(baseId, FreshWaterId, StringComparison.Ordinal))
            {
                BuffLeftmost(
                    seller,
                    definition => definition.ActiveEffects.Any(
                        effect => effect.Operation == EffectOp.Heal),
                    SaleBuffAmount(sold.Definition),
                    healingOnly: true);
            }
            else if (string.Equals(baseId, GoldenChariotId, StringComparison.Ordinal))
            {
                // 黄金战车自己被卖掉就不再成长；这一条留给别的牌。
            }

            foreach (CardInstance owned in seller.OwnedCards)
            {
                if (string.Equals(
                        owned.Definition.BaseId,
                        GoldenChariotId,
                        StringComparison.Ordinal) &&
                    !ReferenceEquals(owned, sold))
                {
                    owned.ModifyValue(1);
                }
            }
        }

        /// <summary>
        /// 该玩家回合结束：铜锣烧 / 占位符1成长；每张雪水净化器各产出一张淡水。
        /// 这些是棋盘层被动，放在背包里也生效。
        /// </summary>
        public static List<CardDefinition> OnTurnEnded(MatchPlayerState player)
        {
            List<CardDefinition> produced = new List<CardDefinition>();
            if (player == null)
            {
                return produced;
            }

            foreach (CardInstance owned in player.OwnedCards)
            {
                if (string.Equals(
                        owned.Definition.BaseId,
                        DamageCompanionId,
                        StringComparison.Ordinal))
                {
                    owned.PermanentDiceModifier += 1;
                }

                if (string.Equals(owned.Definition.BaseId, DorayakiId, StringComparison.Ordinal))
                {
                    owned.ModifyValue(1);
                }
            }

            int purifierCount = player.OwnedCards.Count(
                card => string.Equals(
                    card.Definition.BaseId,
                    SnowPurifierId,
                    StringComparison.Ordinal));
            CardDefinition freshWater = CardDatabase.Instance.Get(FreshWaterId);
            for (int index = 0; index < purifierCount; index++)
            {
                produced.Add(freshWater);
            }

            return produced;
        }

        /// <summary>六六大顺：该玩家自己的移动骰子掷出 6 时，每张六六大顺价值 +2。</summary>
        public static void OnDiceRolled(
            MatchPlayerState player,
            int roll)
        {
            if (player == null || roll != 6)
            {
                return;
            }

            foreach (CardInstance owned in player.OwnedCards)
            {
                if (string.Equals(
                        owned.Definition.BaseId,
                        SixSixStreakId,
                        StringComparison.Ordinal))
                {
                    owned.ModifyValue(2);
                }
            }
        }

        /// <summary>占位符7：每次真正停在奖励地块，每张占位符7价值 +1。</summary>
        public static void OnRewardStopped(MatchPlayerState player)
        {
            if (player == null)
            {
                return;
            }

            foreach (CardInstance owned in player.OwnedCards)
            {
                if (string.Equals(
                        owned.Definition.BaseId,
                        ValuePlaceholder7Id,
                        StringComparison.Ordinal))
                {
                    owned.ModifyValue(1);
                }
            }
        }

        /// <summary>
        /// 战斗开始：每张在场的粉碎机吃掉背包最左侧的一张废料，并给自己永久 +1 颗骰子。
        /// 返回被吃掉的牌，交给 MatchController 发事件。
        /// </summary>
        public static List<CardInstance> PrepareBattleDice(MatchPlayerState player)
        {
            List<CardInstance> consumed = new List<CardInstance>();
            if (player == null)
            {
                return consumed;
            }

            foreach (CardInstance shredder in player.BattleCards)
            {
                if (!string.Equals(
                        shredder.Definition.BaseId,
                        ShredderId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                CardSlot? leftmost = Leftmost(player, CardTag.Waste, includeBattle: false);
                if (leftmost == null)
                {
                    continue;
                }

                if (player.TryConsumeCard(leftmost.Value.Card.Uid, out CardInstance removed))
                {
                    shredder.PermanentDiceCountBonus += 1;
                    consumed.Add(removed);
                }
            }

            return consumed;
        }

        private static void BuffLeftmost(
            MatchPlayerState player,
            Predicate<CardDefinition> match,
            int amount,
            bool healingOnly)
        {
            if (amount <= 0)
            {
                return;
            }

            CardSlot? target = LeftmostCardSlot(player, match);
            if (target == null)
            {
                return;
            }

            if (healingOnly)
            {
                target.Value.Card.PermanentHealingModifier += amount;
            }
            else
            {
                target.Value.Card.PermanentDiceModifier += amount;
            }
        }

        private static CardSlot? LeftmostCardSlot(
            MatchPlayerState player,
            Predicate<CardDefinition> match)
        {
            foreach (CardInstance card in player.BattleCards)
            {
                if (match(card.Definition))
                {
                    return new CardSlot(player.ColumnOf(card.Uid), card);
                }
            }

            foreach (CardSlot slot in player.BagSlots)
            {
                if (slot.Card != null && match(slot.Card.Definition))
                {
                    return slot;
                }
            }

            return null;
        }

        private static CardSlot? Leftmost(
            MatchPlayerState player,
            CardTag tag,
            bool includeBattle)
        {
            if (includeBattle)
            {
                foreach (CardInstance card in player.BattleCards)
                {
                    if (card.Definition.HasTag(tag))
                    {
                        return new CardSlot(player.ColumnOf(card.Uid), card);
                    }
                }
            }

            foreach (CardSlot slot in player.BagSlots)
            {
                if (slot.Card != null && slot.Card.Definition.HasTag(tag))
                {
                    return slot;
                }
            }

            return null;
        }
    }
}
