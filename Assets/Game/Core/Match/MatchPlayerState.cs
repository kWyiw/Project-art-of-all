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

        /// <summary>还在倒计数的战斗增益（事件给的，见 <see cref="BattleModifier"/>）。</summary>
        private readonly List<BattleModifier> battleModifiers = new List<BattleModifier>();

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

        /// <summary>
        /// Node the player stepped off to reach <see cref="CurrentNode"/>; -1 when unknown
        /// (spawn, teleport). Used so a fork at the start of a move never offers the way back.
        /// </summary>
        public int LastArrivalNode { get; internal set; } = -1;

        /// <summary>
        /// True once the player has taken a voluntary move, so the forced counter-clockwise
        /// first step away from Home only applies to the opening move (R-MAP-002).
        /// </summary>
        public bool HasMoved { get; internal set; }
        public int Level { get; internal set; }
        public int Shards { get; internal set; }
        public int Wins { get; internal set; }
        public NormaType Norma { get; internal set; }
        public IReadOnlyList<CardInstance> OwnedCards => cards.OwnedCards;

        /// <summary>True when the card currently sits in the battle row (CJ-5).</summary>
        public bool IsEquipped(CardInstance card)
        {
            return card != null && cards.IsEquipped(card.Uid);
        }

        public IReadOnlyList<CardInstance> BattleCards => cards.BattleCards;

        /// <summary>背包栏位的占位（牌可以放在任意合法空位，中间允许留空）。</summary>
        public IReadOnlyList<CardSlot> BagSlots => cards.BagSlots;

        /// <summary>战斗栏位的占位（相邻光环按这里的列号判断相邻）。</summary>
        public IReadOnlyList<CardSlot> BattleSlots => cards.BattleSlots;

        /// <summary>这张牌当前占用的列号；不在任何栏位时返回 -1。</summary>
        public int ColumnOf(int cardUid)
        {
            return cards.ColumnOf(cardUid);
        }

        public int OwnedCardWidth => cards.OwnedWidth;
        public int BattleCardWidth => cards.BattleWidth;

        public bool MeetsCurrentNorma()
        {
            return Level >= 6 ||
                   NormaRules.MeetsThreshold(Level, Norma, Shards, Wins);
        }

        /// <summary>事件给的临时战斗增益（R-EVT-003），按剩余场次从多到少排列。</summary>
        public IReadOnlyList<BattleModifier> BattleModifiers => battleModifiers;

        /// <summary>
        /// 把还生效的增益合成一份：护盾百分比、伤害、格挡逐项相加。
        /// 没有增益时返回全零的一份（场次留 1，方便直接塞进战斗快照）。
        /// </summary>
        internal BattleModifier CombinedBattleModifier()
        {
            int shieldPercent = 0;
            int damageBonus = 0;
            int blockBonus = 0;
            foreach (BattleModifier modifier in battleModifiers)
            {
                shieldPercent += modifier.ShieldPercent;
                damageBonus += modifier.DamageBonus;
                blockBonus += modifier.BlockBonus;
            }

            return new BattleModifier(shieldPercent, damageBonus, blockBonus, 1);
        }

        internal void AddBattleModifier(BattleModifier modifier)
        {
            if (modifier.IsExpired)
            {
                return;
            }

            battleModifiers.Add(modifier);
        }

        /// <summary>每打完一场战斗，所有增益的剩余场次各减一，归零的丢掉。</summary>
        internal void ConsumeBattleModifiers()
        {
            for (int i = battleModifiers.Count - 1; i >= 0; i--)
            {
                BattleModifier next = battleModifiers[i].Consume();
                if (next.IsExpired)
                {
                    battleModifiers.RemoveAt(i);
                }
                else
                {
                    battleModifiers[i] = next;
                }
            }
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

            foreach (BattleModifier modifier in battleModifiers)
            {
                hash = StableHash.Add(hash, modifier.ShieldPercent);
                hash = StableHash.Add(hash, modifier.DamageBonus);
                hash = StableHash.Add(hash, modifier.BlockBonus);
                hash = StableHash.Add(hash, modifier.BattlesRemaining);
            }

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

        /// <summary>把牌摆到某个栏位的 <paramref name="column"/> 列（背包 / 战斗都一样）。</summary>
        internal bool TryMoveCard(int cardUid, bool toBattle, int column)
        {
            return toBattle
                ? cards.TryMoveToBattle(cardUid, LoadoutRules.GetBattleCapacity(Level), column)
                : cards.TryMoveToBag(cardUid, column);
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
