using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Cards;

namespace ProjectArtOfAll.Core.Combat
{
    internal sealed class CombatantRuntime
    {
        public CombatantRuntime(CombatantSnapshot snapshot)
        {
            Snapshot = snapshot;
            BaseMaximumHealth = snapshot.MaxHealth;
            MaximumHealth = BaseMaximumHealth;
            Health = MaximumHealth;
            // 事件给的增益（R-EVT-003）：开战先按最大生命值百分比给盾，之后每张牌伤害/格挡各加点。
            Shield = snapshot.Bonus.ShieldFor(MaximumHealth);
            DamageBonus = snapshot.Bonus.DamageBonus;
            BlockBonus = snapshot.Bonus.BlockBonus;
            HealBonus = snapshot.Bonus.HealBonus;
            PoisonBonus = snapshot.Bonus.PoisonBonus;
            BurnBonus = snapshot.Bonus.BurnBonus;
            RegenerationBonus = snapshot.Bonus.RegenerationBonus;
            Cards = snapshot.BattleSlots
                .Select(slot => new CombatCardRuntime(slot.Card, slot.Column))
                .ToList();
        }

        public CombatantSnapshot Snapshot { get; }
        public int PlayerId => Snapshot.PlayerId;
        public int Wins => Snapshot.Wins;
        public int BaseMaximumHealth { get; }
        public int MaximumHealth { get; private set; }
        public int Health { get; set; }
        public int Shield { get; set; }
        public int Poison { get; set; }
        public int Burn { get; set; }
        public int Regeneration { get; set; }

        /// <summary>事件增益：本场战斗所有伤害 +N。</summary>
        public int DamageBonus { get; }

        /// <summary>事件增益：本场战斗所有格挡 +N。</summary>
        public int BlockBonus { get; }

        /// <summary>事件「神秘饮料」：本场治疗数值 +N。</summary>
        public int HealBonus { get; }

        /// <summary>事件「神秘饮料」：本场中毒数值 +N。</summary>
        public int PoisonBonus { get; }

        /// <summary>事件「神秘饮料」：本场灼烧数值 +N。</summary>
        public int BurnBonus { get; }

        /// <summary>事件「神秘饮料」：本场再生数值 +N。</summary>
        public int RegenerationBonus { get; }

        /// <summary>特种橡胶: every shield this side gains is +N.</summary>
        public int ShieldBonus { get; set; }

        public List<CombatCardRuntime> Cards { get; }

        public bool IsKnockedOut => Health <= 0;

        internal void RefreshMaximumHealth(int bonus)
        {
            MaximumHealth = Math.Max(1, BaseMaximumHealth + Math.Max(0, bonus));
            Health = Math.Min(Health, MaximumHealth);
        }

        public IReadOnlyList<CombatCardRuntime> ActiveCards()
        {
            return Cards
                .Where(card => !card.IsDestroyed)
                .OrderBy(card => card.Column)
                .ToList();
        }
    }

    internal sealed class CombatCardRuntime
    {
        public CombatCardRuntime(CardInstance instance, int column)
        {
            Instance = instance;
            Column = column;
            // 战斗开始时冷却从零开始走（BT-2.5 ③）：主动牌要等满一整条冷却才会施放第一次。
            BattleCooldownTicks = instance.Definition.IsActive
                ? instance.Definition.CooldownTicks
                : 0;
            Cooldown = new CooldownTimer(BattleCooldownTicks);
        }

        private readonly HashSet<int> triggeredOncePassives = new HashSet<int>();

        public CardInstance Instance { get; }
        public CardDefinition Definition => Instance.Definition;

        /// <summary>战斗栏里的最左列号；相邻光环按列号判断相邻，中间留空就不相邻。</summary>
        public int Column { get; }

        /// <summary>这张牌占的格子数。</summary>
        public int Size => System.Math.Max(1, Definition.SlotSize);

        /// <summary>紧贴在本卡左侧（中间没有空格）。</summary>
        public bool IsImmediatelyLeftOf(CombatCardRuntime other)
        {
            return other != null && Column + Size == other.Column;
        }

        /// <summary>紧贴在本卡右侧（中间没有空格）。</summary>
        public bool IsImmediatelyRightOf(CombatCardRuntime other)
        {
            return other != null && other.Column + other.Size == Column;
        }

        public bool IsAdjacentTo(CombatCardRuntime other)
        {
            return !ReferenceEquals(this, other) &&
                   (IsImmediatelyLeftOf(other) || IsImmediatelyRightOf(other));
        }

        /// <summary>Position inside the battle bar; the bar keeps its slots when a card dies.</summary>
        public CooldownTimer Cooldown { get; }

        /// <summary>
        /// The cooldown length used for this battle. Normally this is the definition value;
        /// Chainsaw changes it after its first cast so later cycles stay shortened.
        /// </summary>
        public int BattleCooldownTicks { get; private set; }

        public bool IsDestroyed { get; private set; }

        /// <summary>热熔切割器: dice gained inside this battle.</summary>
        public int BattleDiceBonus { get; set; }

        /// <summary>Compound / Torch / Titan: per-die modifier gained inside this battle.</summary>
        public int BattleDiceModifier { get; set; }

        /// <summary>Placeholder 5 / 六六大顺: shield modifier gained or evaluated per card.</summary>
        public int BattleShieldBonus { get; set; }

        public int TotalDiceCountBonus =>
            BattleDiceBonus + Instance.PermanentDiceCountBonus;

        public bool TryMarkPassiveOnce(int passiveIndex)
        {
            return triggeredOncePassives.Add(passiveIndex);
        }

        public void Destroy()
        {
            IsDestroyed = true;
        }

        public void HalveBattleCooldown()
        {
            BattleCooldownTicks = System.Math.Max(0, BattleCooldownTicks / 2);
            Cooldown.HalveRemaining();
        }
    }
}
