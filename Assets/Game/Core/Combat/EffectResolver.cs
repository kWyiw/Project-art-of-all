using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Events;

namespace ProjectArtOfAll.Core.Combat
{
    internal sealed class EffectResolver
    {
        private readonly AuraResolver auraResolver;

        public EffectResolver(AuraResolver auraResolver)
        {
            this.auraResolver = auraResolver;
        }

        public void Resolve(
            CombatSimulation simulation,
            CombatantRuntime source,
            CombatantRuntime enemy,
            CombatCardRuntime sourceCard)
        {
            int diceModifier = auraResolver.GetDiceModifier(source, sourceCard);

            foreach (EffectDefinition effect in sourceCard.Definition.ActiveEffects)
            {
                int amount = effect.Amount.Roll(simulation.Rng, diceModifier);
                CombatantRuntime target = effect.Target == EffectTarget.Enemy
                    ? enemy
                    : source;

                switch (effect.Operation)
                {
                    case EffectOp.DealDamage:
                        simulation.ApplyDamage(source.PlayerId, target, amount, false);
                        break;
                    case EffectOp.GainShield:
                        target.Shield += amount;
                        simulation.Publish(new ShieldGainedEvent(target.PlayerId, amount));
                        break;
                    case EffectOp.ApplyPoison:
                        target.Poison += amount;
                        simulation.Publish(
                            new StatusAppliedEvent(
                                target.PlayerId,
                                StatusKind.Poison,
                                amount));
                        break;
                    case EffectOp.ApplyBurn:
                        target.Burn += amount;
                        simulation.Publish(
                            new StatusAppliedEvent(
                                target.PlayerId,
                                StatusKind.Burn,
                                amount));
                        break;
                    case EffectOp.ApplyRegeneration:
                        target.Regeneration += amount;
                        simulation.Publish(
                            new StatusAppliedEvent(
                                target.PlayerId,
                                StatusKind.Regeneration,
                                amount));
                        break;
                    case EffectOp.Heal:
                        simulation.ApplyHealing(target, amount);
                        break;
                }
            }
        }
    }
}
