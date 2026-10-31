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
        public List<CombatCardRuntime> Cards { get; }

        public bool IsKnockedOut => Health <= 0;
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
        public CooldownTimer Cooldown { get; }
        public bool IsDestroyed { get; private set; }

        public void Destroy()
        {
            IsDestroyed = true;
        }
    }
}
