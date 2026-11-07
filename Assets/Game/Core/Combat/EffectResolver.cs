using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Events;
using ProjectArtOfAll.Core.Rng;

namespace ProjectArtOfAll.Core.Combat
{
    /// <summary>
    /// Executes one card's effect list. Every number it produces comes from the aura
    /// resolver, the card instance's permanent growth, or the roll itself — nothing is
    /// hard-coded per card id.
    /// </summary>
    internal sealed class EffectResolver
    {
        private readonly AuraResolver auraResolver;

        public EffectResolver(AuraResolver auraResolver)
        {
            this.auraResolver = auraResolver;
        }

        public void ResolveActive(
            CombatSimulation simulation,
            CombatantRuntime source,
            CombatantRuntime enemy,
            CombatCardRuntime card)
        {
            foreach (CardEffect effect in card.Definition.ActiveEffects)
            {
                if (card.IsDestroyed)
                {
                    return;
                }

                Resolve(simulation, source, enemy, card, effect, fromPassive: false);
            }
        }

        public void Resolve(
            CombatSimulation simulation,
            CombatantRuntime owner,
            CombatantRuntime enemy,
            CombatCardRuntime card,
            CardEffect effect,
            bool fromPassive)
        {
            switch (effect.Operation)
            {
                case EffectOp.Slow:
                    ApplySlow(simulation, owner, enemy, card, effect, fromPassive);
                    return;
                case EffectOp.Haste:
                    ApplyHaste(simulation, owner, card, effect);
                    return;
                case EffectOp.Charge:
                    ApplyCharge(simulation, owner, card, effect);
                    return;
                case EffectOp.DestroySelf:
                    DestroySelf(simulation, owner, card);
                    return;
                case EffectOp.ModifyValue:
                    card.Instance.ModifyValue(RollFlat(effect));
                    return;
                case EffectOp.ModifyDiceCount:
                    ApplyDiceCount(simulation, owner, card, effect, fromPassive);
                    return;
                case EffectOp.ModifyShieldBonus:
                    // 提升"每次获得护盾时的增量"（特种橡胶），本身不给盾。
                    owner.ShieldBonus += RollFlat(effect);
                    return;
                default:
                    break;
            }

            int amount = Roll(simulation, owner, card, effect);
            CombatantRuntime target = effect.Target == EffectTarget.Enemy ? enemy : owner;

            switch (effect.Operation)
            {
                case EffectOp.DealDamage:
                    // 事件增益（R-EVT-003）：本场战斗的伤害加成只加在牌造成的伤害上，
                    // 中毒/灼烧的周期结算不加。
                    simulation.ApplyDamage(
                        owner.PlayerId,
                        target,
                        amount + owner.DamageBonus,
                        ignoresShield: false);
                    break;

                case EffectOp.GainShield:
                    simulation.GainShield(owner, target, amount + owner.BlockBonus);
                    break;

                case EffectOp.ApplyPoison:
                    target.Poison += amount;
                    simulation.Publish(new StatusAppliedEvent(
                        target.PlayerId,
                        StatusKind.Poison,
                        amount));
                    break;

                case EffectOp.ApplyBurn:
                    target.Burn += amount;
                    simulation.Publish(new StatusAppliedEvent(
                        target.PlayerId,
                        StatusKind.Burn,
                        amount));
                    simulation.TriggerPassives(
                        owner,
                        card,
                        PassiveTrigger.BurnApplied,
                        CardTag.None);
                    break;

                case EffectOp.ApplyRegeneration:
                    target.Regeneration += amount;
                    simulation.Publish(new StatusAppliedEvent(
                        target.PlayerId,
                        StatusKind.Regeneration,
                        amount));
                    break;

                case EffectOp.Heal:
                    simulation.ApplyHealing(target, amount);
                    break;

                case EffectOp.MultiCast:
                    card.BattleDiceBonus += Math.Max(1, amount);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Effect {effect.Operation} is not handled.");
            }
        }

        private void ApplySlow(
            CombatSimulation simulation,
            CombatantRuntime owner,
            CombatantRuntime enemy,
            CombatCardRuntime card,
            CardEffect effect,
            bool fromPassive)
        {
            int count = Math.Max(1, (int)effect.Amount.Constant);
            int duration = Math.Max(0, effect.DurationTicks);

            for (int index = 0; index < count; index++)
            {
                CombatCardRuntime target = PickRandom(
                    enemy.ActiveCards(),
                    simulation.Rng);
                if (target == null)
                {
                    return;
                }

                target.Cooldown.ApplySlow(duration);
                simulation.Publish(new SlowAppliedEvent(
                    enemy.PlayerId,
                    target.Instance.Uid,
                    duration,
                    card?.Instance.Uid ?? -1));
                simulation.TriggerPassives(
                    owner,
                    card,
                    PassiveTrigger.SlowApplied,
                    CardTag.None);

                if (fromPassive)
                {
                    continue;
                }
            }
        }

        private void ApplyHaste(
            CombatSimulation simulation,
            CombatantRuntime owner,
            CombatCardRuntime card,
            CardEffect effect)
        {
            int count = Math.Max(1, (int)effect.Amount.Constant);
            int duration = Math.Max(0, effect.DurationTicks);

            List<CombatCardRuntime> candidates = owner.ActiveCards()
                .Where(target =>
                    effect.RequiredTag == CardTag.None ||
                    target.Definition.HasTag(effect.RequiredTag))
                .ToList();

            for (int index = 0; index < count; index++)
            {
                CombatCardRuntime target = PickRandom(candidates, simulation.Rng);
                if (target == null)
                {
                    return;
                }

                target.Cooldown.ApplyHaste(duration);
                simulation.Publish(new HasteAppliedEvent(
                    owner.PlayerId,
                    target.Instance.Uid,
                    duration));
                simulation.TriggerPassives(
                    owner,
                    card,
                    PassiveTrigger.HasteApplied,
                    CardTag.None);
            }
        }

        private void ApplyCharge(
            CombatSimulation simulation,
            CombatantRuntime owner,
            CombatCardRuntime card,
            CardEffect effect)
        {
            int ticks = Math.Max(0, (int)effect.Amount.Constant);
            if (ticks == 0 || card == null)
            {
                return;
            }

            switch (effect.Target)
            {
                case EffectTarget.SelfCard:
                    ChargeCard(simulation, owner, card, ticks, card);
                    break;

                case EffectTarget.Self:
                    foreach (CombatCardRuntime own in owner.ActiveCards())
                    {
                        ChargeCard(simulation, owner, own, ticks, card);
                    }

                    break;

                case EffectTarget.AdjacentOwnCards:
                    foreach (CombatCardRuntime neighbour in Neighbours(owner, card))
                    {
                        ChargeCard(simulation, owner, neighbour, ticks, card);
                    }

                    break;
            }
        }

        private void ChargeCard(
            CombatSimulation simulation,
            CombatantRuntime owner,
            CombatCardRuntime target,
            int ticks,
            CombatCardRuntime source)
        {
            target.Cooldown.ApplyCharge(ticks);
            simulation.Publish(new ChargeAppliedEvent(
                owner.PlayerId,
                target.Instance.Uid,
                ticks,
                source?.Instance.Uid ?? -1));
            simulation.TriggerPassives(
                owner,
                source,
                PassiveTrigger.ChargeApplied,
                CardTag.None);
        }

        private void DestroySelf(
            CombatSimulation simulation,
            CombatantRuntime owner,
            CombatCardRuntime card)
        {
            if (card == null || card.IsDestroyed)
            {
                return;
            }

            card.Destroy();
            simulation.Publish(new CardDestroyedEvent(
                owner.PlayerId,
                card.Instance.Uid));
        }

        private void ApplyDiceCount(
            CombatSimulation simulation,
            CombatantRuntime owner,
            CombatCardRuntime card,
            CardEffect effect,
            bool fromPassive)
        {
            int amount = RollFlat(effect);
            if (!fromPassive)
            {
                card.BattleDiceBonus += amount;
                return;
            }

            // 粉碎机: 每场战斗开始的 +1 是永久成长（写入卡牌实例，跨战斗保留）。
            card.Instance.PermanentDiceCountBonus += amount;
        }

        private static IReadOnlyList<CombatCardRuntime> Neighbours(
            CombatantRuntime owner,
            CombatCardRuntime card)
        {
            IReadOnlyList<CombatCardRuntime> active = owner.ActiveCards();
            List<CombatCardRuntime> result = new List<CombatCardRuntime>(2);
            foreach (CombatCardRuntime candidate in active)
            {
                if (candidate.IsAdjacentTo(card))
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        private int Roll(
            CombatSimulation simulation,
            CombatantRuntime owner,
            CombatCardRuntime card,
            CardEffect effect)
        {
            int perDieModifier =
                auraResolver.GetDiceModifier(owner, card) +
                card.Instance.PermanentDiceModifier;
            int extraDice =
                card.TotalDiceCountBonus +
                auraResolver.GetBonusDiceCount(owner, card);
            bool forceMaximum = auraResolver.HasMaxRoll(owner, card);

            return effect.Amount.Roll(
                simulation.Rng,
                card.Instance.Value,
                perDieModifier,
                forceMaximum,
                extraDice);
        }

        private static int RollFlat(CardEffect effect)
        {
            return Math.Max(0, (int)effect.Amount.Constant);
        }

        private static CombatCardRuntime PickRandom(
            IReadOnlyList<CombatCardRuntime> candidates,
            Pcg32 rng)
        {
            if (candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            return candidates[rng.NextInt(0, candidates.Count)];
        }
    }
}
