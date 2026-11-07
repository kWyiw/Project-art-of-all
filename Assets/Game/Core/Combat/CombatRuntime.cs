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
            MaximumHealth = snapshot.MaxHealth;
            Health = MaximumHealth;
            Cards = snapshot.BattleSlots
                .Select(slot => new CombatCardRuntime(slot.Card, slot.Column))
                .ToList();
        }

        public CombatantSnapshot Snapshot { get; }
        public int PlayerId => Snapshot.PlayerId;
        public int MaximumHealth { get; }
        public int Health { get; set; }
        public int Shield { get; set; }
        public int Poison { get; set; }
        public int Burn { get; set; }
        public int Regeneration { get; set; }

        /// <summary>特种橡胶: every shield this side gains is +N.</summary>
        public int ShieldBonus { get; set; }

        public List<CombatCardRuntime> Cards { get; }

        public bool IsKnockedOut => Health <= 0;

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
            Cooldown = new CooldownTimer(
                instance.Definition.IsActive ? instance.Definition.CooldownTicks : 0);
        }

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
        public bool IsDestroyed { get; private set; }

        /// <summary>热熔切割器: dice gained inside this battle.</summary>
        public int BattleDiceBonus { get; set; }

        public int TotalDiceCountBonus =>
            BattleDiceBonus + Instance.PermanentDiceCountBonus;

        public void Destroy()
        {
            IsDestroyed = true;
        }
    }
}
