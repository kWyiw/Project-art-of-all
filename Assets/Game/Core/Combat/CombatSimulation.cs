using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Events;
using ProjectArtOfAll.Core.Rng;

namespace ProjectArtOfAll.Core.Combat
{
    /// <summary>
    /// Deterministic 60 Hz automatic combat. It returns state changes and events only;
    /// it never references Unity time, scenes or rendering objects.
    /// </summary>
    public sealed class CombatSimulation
    {
        public const int TicksPerSecond = 60;
        public const int TimeoutStartTick = 1200;
        public const int HardDrawTick = 3600;
        public const int TimeoutIntervalTicks = 6;

        private readonly CombatantRuntime left;
        private readonly CombatantRuntime right;
        private readonly GameEventBus eventBus = new GameEventBus();
        private readonly AuraResolver auraResolver = new AuraResolver();
        private readonly EffectResolver effectResolver;

        public CombatSimulation(BattleSnapshot snapshot, ulong seed)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            left = new CombatantRuntime(snapshot.Left);
            right = new CombatantRuntime(snapshot.Right);
            Rng = new Pcg32(seed, 0x434F4D424154UL);
            effectResolver = new EffectResolver(auraResolver);

            eventBus.Publish(0L, new CombatStartedEvent(seed));
        }

        public int Tick { get; private set; }
        public bool IsFinished { get; private set; }
        public CombatResult Result { get; private set; }
        public GameEventBus Events => eventBus;
        internal Pcg32 Rng { get; }
        internal CombatantRuntime Left => left;
        internal CombatantRuntime Right => right;

        public void TickOnce()
        {
            if (IsFinished)
            {
                return;
            }

            if (Tick >= HardDrawTick)
            {
                FinishBattle(-1, CombatEndReason.HardTimeoutDraw);
                return;
            }

            AdvanceCooldowns(left);
            AdvanceCooldowns(right);

            ResolvePeriodicStatuses(left);
            ResolvePeriodicStatuses(right);

            ResolveReadyCards(left, right);
            ResolveReadyCards(right, left);

            if (TryFinishFromKnockout(CombatEndReason.Knockout))
            {
                return;
            }

            ResolveTimeoutPenalty();
            if (TryFinishFromKnockout(CombatEndReason.TimeoutKnockout))
            {
                return;
            }

            Tick++;
        }

        public CombatResult RunToCompletion()
        {
            while (!IsFinished)
            {
                TickOnce();
            }

            return Result;
        }

        internal void ApplyDamage(
            int sourcePlayerId,
            CombatantRuntime target,
            int amount,
            bool ignoresShield)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            int shieldAbsorbed = 0;
            int healthDamage = amount;

            if (!ignoresShield && target.Shield > 0)
            {
                shieldAbsorbed = Math.Min(target.Shield, amount);
                target.Shield -= shieldAbsorbed;
                healthDamage -= shieldAbsorbed;
            }

            target.Health = Math.Max(0, target.Health - healthDamage);
            Publish(
                new DamageDealtEvent(
                    sourcePlayerId,
                    target.PlayerId,
                    amount,
                    shieldAbsorbed,
                    ignoresShield));
        }

        internal void ApplyHealing(CombatantRuntime target, int amount)
        {
            int before = target.Health;
            target.Health = Math.Min(
                target.MaximumHealth,
                target.Health + Math.Max(0, amount));
            Publish(
                new HealthRestoredEvent(
                    target.PlayerId,
                    amount,
                    target.Health - before));
        }

        internal void Publish(GameEvent gameEvent)
        {
            eventBus.Publish(Tick, gameEvent);
        }

        private void ResolveReadyCards(
            CombatantRuntime source,
            CombatantRuntime enemy)
        {
            foreach (CombatCardRuntime card in source.Cards
                         .Where(card => !card.IsDestroyed)
                         .OrderBy(card => card.SlotIndex)
                         .ToArray())
            {
                if (!card.Cooldown.IsReady ||
                    card.Definition.CooldownTicks <= 0 ||
                    card.Definition.ActiveEffects.Count == 0)
                {
                    continue;
                }

                card.Cooldown.SetRemainingTicks(card.Definition.CooldownTicks);
                int casts = 1 + Math.Max(
                    0,
                    auraResolver.GetBonusMultiCast(source, card));

                for (int cast = 0; cast < casts && !card.IsDestroyed; cast++)
                {
                    Publish(
                        new CardCastEvent(
                            source.PlayerId,
                            card.SlotIndex,
                            card.Instance.Uid));
                    effectResolver.Resolve(this, source, enemy, card);
                }
            }
        }

        private static void AdvanceCooldowns(CombatantRuntime combatant)
        {
            foreach (CombatCardRuntime card in combatant.Cards)
            {
                if (!card.IsDestroyed)
                {
                    card.Cooldown.AdvanceTick();
                }
            }
        }

        private void ResolvePeriodicStatuses(CombatantRuntime target)
        {
            if (Tick > 0 && Tick % 60 == 0 && target.Poison > 0)
            {
                ApplyDamage(-1, target, target.Poison, true);
            }

            if (Tick > 0 && Tick % 30 == 0 && target.Burn > 0)
            {
                ApplyDamage(-1, target, target.Burn, true);
                target.Burn = Math.Max(0, target.Burn - 1);
            }

            if (Tick > 0 && Tick % 60 == 0 && target.Regeneration > 0)
            {
                ApplyHealing(target, target.Regeneration);
            }
        }

        private void ResolveTimeoutPenalty()
        {
            if (Tick < TimeoutStartTick ||
                (Tick - TimeoutStartTick) % TimeoutIntervalTicks != 0)
            {
                return;
            }

            int penaltyIndex = (Tick - TimeoutStartTick) /
                               TimeoutIntervalTicks + 1;
            int damage = (penaltyIndex + 9) / 10;
            ApplyDamage(-1, left, damage, false);
            ApplyDamage(-1, right, damage, false);
        }

        private bool TryFinishFromKnockout(CombatEndReason reason)
        {
            if (!left.IsKnockedOut && !right.IsKnockedOut)
            {
                return false;
            }

            if (left.IsKnockedOut && right.IsKnockedOut)
            {
                FinishBattle(-1, reason);
            }
            else if (left.IsKnockedOut)
            {
                FinishBattle(right.PlayerId, reason);
            }
            else
            {
                FinishBattle(left.PlayerId, reason);
            }

            return true;
        }

        private void FinishBattle(int winnerPlayerId, CombatEndReason reason)
        {
            if (IsFinished)
            {
                return;
            }

            IsFinished = true;
            Result = new CombatResult(
                winnerPlayerId,
                reason,
                Tick,
                left.Health,
                right.Health,
                left.Shield,
                right.Shield);
            Publish(new CombatEndedEvent(winnerPlayerId, reason));
        }
    }
}
