using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectArtOfAll.Core.Cards
{
    public interface ICardCatalog
    {
        CardDefinition Get(string id);
        bool TryGet(string id, out CardDefinition definition);
        IReadOnlyList<CardDefinition> All { get; }
    }

    /// <summary>
    /// Six M2 cards with rules grounded in 第一套卡牌.txt. Full 30-card import is
    /// intentionally deferred until the external card-count/size audit is complete.
    /// </summary>
    public sealed class SampleCardCatalog : ICardCatalog
    {
        public static SampleCardCatalog Instance { get; } =
            new SampleCardCatalog(CreateDefinitions());

        private readonly IReadOnlyDictionary<string, CardDefinition> byId;

        public SampleCardCatalog(IEnumerable<CardDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            List<CardDefinition> all = definitions.ToList();
            byId = all.ToDictionary(card => card.Id, StringComparer.Ordinal);
            All = all.AsReadOnly();
        }

        public IReadOnlyList<CardDefinition> All { get; }

        public CardDefinition Get(string id)
        {
            if (!byId.TryGetValue(id, out CardDefinition definition))
            {
                throw new KeyNotFoundException($"Unknown card id '{id}'.");
            }

            return definition;
        }

        public bool TryGet(string id, out CardDefinition definition)
        {
            return byId.TryGetValue(id, out definition);
        }

        private static IReadOnlyList<CardDefinition> CreateDefinitions()
        {
            return new[]
            {
                new CardDefinition(
                    "crowbar",
                    "撬棍",
                    1,
                    180,
                    CardTag.Weapon | CardTag.Tool,
                    1,
                    CardScope.BattleOnly,
                    new[]
                    {
                        new EffectDefinition(
                            EffectOp.DealDamage,
                            new DiceExpression(1, 6),
                            EffectTarget.Enemy)
                    }),
                new CardDefinition(
                    "car_door",
                    "汽车门板",
                    2,
                    540,
                    CardTag.Waste,
                    2,
                    CardScope.BattleOnly,
                    new[]
                    {
                        new EffectDefinition(
                            EffectOp.GainShield,
                            new DiceExpression(2, 20),
                            EffectTarget.Self)
                    }),
                new CardDefinition(
                    "nuclear_water",
                    "核废水",
                    1,
                    240,
                    CardTag.Waste,
                    1,
                    CardScope.BattleOnly,
                    new[]
                    {
                        new EffectDefinition(
                            EffectOp.ApplyPoison,
                            new DiceExpression(1, 4),
                            EffectTarget.Enemy)
                    }),
                new CardDefinition(
                    "lighter",
                    "打火机",
                    1,
                    300,
                    CardTag.Tool,
                    1,
                    CardScope.BattleOnly,
                    new[]
                    {
                        new EffectDefinition(
                            EffectOp.ApplyBurn,
                            new DiceExpression(1, 4),
                            EffectTarget.Enemy)
                    }),
                new CardDefinition(
                    "whetstone",
                    "磨刀石",
                    1,
                    0,
                    CardTag.Waste,
                    1,
                    CardScope.BattleOnly,
                    Array.Empty<EffectDefinition>(),
                    new[]
                    {
                        new AuraDefinition(
                            AuraTarget.AllAdjacent,
                            AuraStat.DiceFlatModifier,
                            2,
                            CardTag.Weapon)
                    }),
                new CardDefinition(
                    "hardtack",
                    "压缩饼干",
                    1,
                    180,
                    CardTag.Food,
                    1,
                    CardScope.BattleOnly,
                    new[]
                    {
                        new EffectDefinition(
                            EffectOp.ApplyRegeneration,
                            new DiceExpression(1, 4),
                            EffectTarget.Self)
                    })
            };
        }
    }
}
