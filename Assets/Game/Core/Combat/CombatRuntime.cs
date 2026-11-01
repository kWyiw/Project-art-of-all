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
            MaximumHealth = BattleConfig.GetMaxHealth(snapshot.Level);
            Health = MaximumHealth;
            Cards = snapshot.BattleCards
                .Select((card, slotIndex) =>
                    new CombatCardRuntime(card, slotIndex))
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
                .OrderBy(card => card.SlotIndex)
                .ToList();
        }
    }

    internal sealed class CombatCardRuntime
    {
        public CombatCardRuntime(CardInstance instance, int slotIndex)
        {
            Instance = instance;
            SlotIndex = slotIndex;
            Cooldown = new CooldownTimer(0);
        }

        public CardInstance Instance { get; }
        public CardDefinition Definition => Instance.Definition;
        public int SlotIndex { get; }

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
