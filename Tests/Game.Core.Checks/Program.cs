using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Commands;
using ProjectArtOfAll.Core.Events;
using ProjectArtOfAll.Core.Match;
using ProjectArtOfAll.Core.Rng;
using ProjectArtOfAll.Core.Util;

namespace ProjectArtOfAll.Core.Checks
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                CheckPcg32();
                CheckMapAndMovement();
                CheckWarpArrivalAndPassThrough();
                CheckSlotRules();
                CheckCombatClocks();
                CheckEventBus();
                CheckCardDatabase();
                CheckCardKeywords();
                CheckCardText();
                CheckLoadoutPreview();
                CheckCombatSimulation();
                CheckCombatTimeline();
                CheckSlotAdjacencyInCombat();
                CheckCustomMaxHealth();
                CheckCardMechanics();
                CheckM1MatchDurability();
                CheckRandomness();
                CheckHomeProgression();
                CheckRewardChoice();
                CheckWarpTeleport();
                CheckCardBandCommands();
                CheckFreeActionsDuringAnotherTurn();
                CheckSceneMapTeleport();
            CheckOpeningGrant();
                CheckFirstMoveFromHome();
                CheckEarlyHomeStop();
                CheckPassThroughOpponent();
                CheckMatchEvent();
                CheckCombatBonus();
                CheckStraightOnlyAndPrediction();
                CheckShopTile();
                CheckCpuAi();
                CheckM3FullMatch();
                CheckM3MatchReport();
                Console.WriteLine("Game.Core checks passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void CheckPcg32()
        {
            Pcg32 first = new Pcg32(123456UL, 7UL);
            Pcg32 second = new Pcg32(123456UL, 7UL);

            for (int index = 0; index < 1000; index++)
            {
                Assert(
                    first.NextUInt32() == second.NextUInt32(),
                    "PCG32 must reproduce the same sequence for the same seed.");
            }

            int[] counts = new int[6];
            Pcg32 distribution = new Pcg32(42UL, 1UL);
            const int sampleCount = 60000;
            for (int index = 0; index < sampleCount; index++)
            {
                counts[distribution.NextD6() - 1]++;
            }

            double expected = sampleCount / 6.0;
            foreach (int count in counts)
            {
                double deviation = Math.Abs(count - expected) / expected;
                Assert(deviation < 0.03, "D6 distribution deviated by more than 3%.");
            }
        }

        private static void CheckMapAndMovement()
        {
            MapGraph graph = CreateTestGraph();
            Dictionary<int, int> playerNodes = new Dictionary<int, int>
            {
                [0] = 0,
                [1] = 5,
                [2] = 2,
                [3] = 6
            };

            IReadOnlyList<MovementCandidate> candidates =
                MovementSolver.FindStopCandidates(
                    graph,
                    playerId: 0,
                    startNodeId: 1,
                    previousNodeId: -1,
                    remainingSteps: 3,
                    playerNodeIds: playerNodes);

            Assert(candidates.Any(candidate => candidate.TargetNodeId == 2),
                "A node occupied by another player must be a stop candidate.");
            Assert(candidates.Any(candidate => candidate.TargetNodeId == 5),
                "The fork branch to an occupied node must be a stop candidate.");
            Assert(candidates.All(candidate => candidate.Steps <= 3),
                "Movement candidates cannot exceed the dice result.");

            IReadOnlyList<MovementCandidate> homeCandidates =
                MovementSolver.FindStopCandidates(
                    graph,
                    playerId: 0,
                    startNodeId: 7,
                    previousNodeId: -1,
                    remainingSteps: 1,
                    playerNodeIds: playerNodes);
            Assert(homeCandidates.Count == 1 &&
                   homeCandidates[0].TargetNodeId == 0,
                "The player's own Home must be stoppable.");

            IReadOnlyList<int> path = MovementSolver.ResolvePathByChoices(
                graph,
                startNodeId: 1,
                previousNodeId: -1,
                outgoingEdgeChoices: new[] { 1, 0, 0 });
            Assert(path.SequenceEqual(new[] { 5, 6, 7 }),
                "Explicit edge choices must resolve deterministically.");

            IReadOnlyList<IReadOnlyList<int>> passThroughPaths =
                MovementSolver.FindPaths(graph, startNodeId: 0, previousNodeId: -1, steps: 2);
            Assert(
                passThroughPaths.Count == 2 &&
                passThroughPaths.Any(path => path.SequenceEqual(new[] { 1, 2 })) &&
                passThroughPaths.Any(path => path.SequenceEqual(new[] { 1, 5 })) &&
                !passThroughPaths.Any(path => path.SequenceEqual(new[] { 1, 3 })),
                "Movement pathfinding must traverse Warp nodes as normal graph nodes.");
        }

        private static void CheckWarpArrivalAndPassThrough()
        {
            MapGraph graph = CreateTestGraph();

            MatchController passController =
                CreateControllerWithRoll(graph, desiredRoll: 2);
            int passEventStart = passController.Events.History.Count;
            passController.Submit(
                new MoveCommand(passController.State.CurrentPlayer.Id));

            PlayerMovedEvent[] passMoves = passController.Events.History
                .Skip(passEventStart)
                .OfType<PlayerMovedEvent>()
                .ToArray();
            Assert(
                passMoves.Length == 2 &&
                passMoves[0].ToNodeId == 1 &&
                passMoves[0].VisitKind == MovementVisitKind.Pass,
                "A Warp node crossed before the movement endpoint must be a Pass.");
            Assert(
                passMoves[1].VisitKind == MovementVisitKind.Arrival,
                "The movement endpoint must be marked as an Arrival.");
            Assert(
                !passController.Events.History
                    .Skip(passEventStart)
                    .OfType<PlayerTeleportedEvent>()
                    .Any(),
                "Passing through a Warp tile must not trigger displacement.");

            MatchController arrivalController =
                CreateControllerWithRoll(graph, desiredRoll: 1);
            int arrivalEventStart = arrivalController.Events.History.Count;
            arrivalController.Submit(
                new MoveCommand(arrivalController.State.CurrentPlayer.Id));

            PlayerMovedEvent[] arrivalMoves = arrivalController.Events.History
                .Skip(arrivalEventStart)
                .OfType<PlayerMovedEvent>()
                .ToArray();
            PlayerTeleportedEvent teleport = arrivalController.Events.History
                .Skip(arrivalEventStart)
                .OfType<PlayerTeleportedEvent>()
                .SingleOrDefault();
            Assert(
                arrivalMoves.Length == 1 &&
                arrivalMoves[0].ToNodeId == 1 &&
                arrivalMoves[0].VisitKind == MovementVisitKind.Arrival,
                "Ending movement on a Warp tile must be an Arrival.");
            Assert(
                teleport != null &&
                teleport.FromNodeId == 1 &&
                teleport.ToNodeId != 1,
                "Arriving on a Warp tile must immediately trigger displacement.");
        }

        private static MatchController CreateControllerWithRoll(
            MapGraph graph,
            int desiredRoll)
        {
            for (ulong seed = 0UL; seed < 512UL; seed++)
            {
                MatchController controller =
                    new MatchController(graph, seed);
                MatchPlayerState player = controller.State.CurrentPlayer;
                controller.Submit(new RollCommand(player.Id));
                if (controller.State.LastRoll == desiredRoll)
                {
                    return controller;
                }
            }

            throw new InvalidOperationException(
                $"Could not find a deterministic seed for roll {desiredRoll}.");
        }

        private static void CheckSlotRules()
        {
            Assert(LoadoutRules.GetBattleCapacity(1) == 4, "Level 1 must be 4c.");
            Assert(LoadoutRules.GetBattleCapacity(2) == 6, "Level 2 must be 6c.");
            Assert(LoadoutRules.GetBattleCapacity(3) == 8, "Level 3 must be 8c.");
            Assert(LoadoutRules.GetBattleCapacity(4) == 10, "Level 4 must be 10c.");
            Assert(LoadoutRules.GetBattleCapacity(5) == 10, "Level 5 must be 10c.");
            Assert(!LoadoutRules.CheckPlacement(
                    LoadoutZone.Bag,
                    9,
                    2,
                    1).Allowed,
                "A 2c card cannot enter a Bag with only 1c available.");
            Assert(!LoadoutRules.CheckPlacement(
                    LoadoutZone.Battle,
                    3,
                    2,
                    1).Allowed,
                "A 2c card cannot enter a 4c battle row with only 1c available.");
        }

        private static void CheckCombatClocks()
        {
            CombatTickAccumulator tickAccumulator = new CombatTickAccumulator();
            Assert(
                tickAccumulator.Advance(1000000L) == 60,
                "One second must produce exactly 60 combat ticks.");

            CooldownTimer normal = new CooldownTimer(180);
            normal.AdvanceTicks(179);
            Assert(!normal.IsReady, "A 3s cooldown must not finish in 179 ticks.");
            normal.AdvanceTick();
            Assert(normal.IsReady, "A 3s cooldown must finish in 180 ticks.");

            CooldownTimer slowed = new CooldownTimer(180);
            slowed.ApplySlow(400);
            slowed.AdvanceTicks(359);
            Assert(!slowed.IsReady, "Slow must halve cooldown progress.");
            slowed.AdvanceTick();
            Assert(slowed.IsReady, "A slowed 3s card must finish in 360 ticks.");

            CooldownTimer canceled = new CooldownTimer(180);
            canceled.ApplyHaste(400);
            canceled.ApplySlow(400);
            canceled.AdvanceTicks(180);
            Assert(canceled.IsReady, "Haste plus slow must return to 1x speed.");
        }

        private static void CheckCardText()
        {
            CardDefinition crowbar = CardDatabase.Instance.Get("crowbar");
            Assert(CardText.Cooldown(crowbar) == "3 秒",
                "Crowbar must read as a 3-second active card.");
            Assert(CardText.Meta(crowbar) == "1c · 3 秒 · 武器 · 工具",
                "Crowbar meta line must carry size, cooldown and both tags.");
            Assert(CardText.Description(crowbar).Contains("伤害"),
                "Crowbar must describe its damage.");

            CardDefinition snake = CardDatabase.Instance.Get("ouroboros");
            Assert(CardText.Description(snake).Contains("必定取最大值"),
                "自噬之蛇 must read as a最大骰 aura, not a flat modifier.");

            CardDefinition fire = CardDatabase.Instance.Get("campfire");
            Assert(CardText.Description(fire).Contains("光环"),
                "Campfire must describe its aura.");

            CardDefinition tape = CardDatabase.Instance.Get("duct_tape");
            Assert(CardText.Description(tape).Contains("被动"),
                "Duct tape must describe its passive.");

            CardDefinition passiveCard = CardDatabase.Instance.Get("whetstone");
            Assert(CardText.Cooldown(passiveCard) == "被动",
                "A card without a cooldown must read as 被动.");

            foreach (CardDefinition card in CardDatabase.Instance.All)
            {
                Assert(
                    !string.IsNullOrWhiteSpace(CardText.Meta(card)),
                    $"Card {card.Id} must have a meta line.");
                Assert(
                    !string.IsNullOrWhiteSpace(CardText.Description(card)),
                    $"Card {card.Id} must have a description.");
                Assert(
                    !CardText.Description(card).Contains("None") &&
                    !CardText.Description(card).Contains("Unknown"),
                    $"Card {card.Id} leaked an enum name into its text.");
            }
        }

        private static void CheckLoadoutPreview()
        {
            // 磨刀石：相邻武器骰值 +2（AdjacentLeft/Right 都有）。
            CardInstance whetstone = new CardInstance(1, CardDatabase.Instance.Get("whetstone"));
            CardInstance crowbar = new CardInstance(2, CardDatabase.Instance.Get("crowbar"));
            CardInstance axe = new CardInstance(3, CardDatabase.Instance.Get("fireman_axe"));

            CardInstance[] pair = { whetstone, crowbar };
            Assert(LoadoutPreview.DiceModifier(pair, 1) == 2,
                "磨刀石在左侧时，右侧撬棍必须吃到 +2 骰值。");
            Assert(LoadoutPreview.DiceModifier(new[] { crowbar, whetstone }, 0) == 2,
                "磨刀石在右侧时，左侧撬棍也必须吃到 +2 骰值。");

            CardInstance[] apart = { whetstone, axe, crowbar };
            Assert(LoadoutPreview.DiceModifier(apart, 2) == 0,
                "隔着一张牌时不再是相邻，磨刀石不能生效。");
            Assert(LoadoutPreview.DiceModifier(apart, 1) == 2,
                "相邻的消防斧必须吃到 +2。");

            // 自噬之蛇：右侧相邻牌骰值必定取最大值。
            CardInstance snake = new CardInstance(4, CardDatabase.Instance.Get("ouroboros"));
            CardInstance[] snakePair = { snake, crowbar };
            Assert(LoadoutPreview.HasMaxRoll(snakePair, 1),
                "自噬之蛇右侧的牌必须被标记为必定最大骰。");
            Assert(!LoadoutPreview.HasMaxRoll(new[] { crowbar, snake }, 0),
                "自噬之蛇只影响它右侧的那张牌。");
            Assert(LoadoutPreview.Describe(snakePair, 1) != null,
                "有加成时必须能描述出来。");
            Assert(LoadoutPreview.Describe(new[] { crowbar, axe }, 0) == null,
                "没有加成时描述必须为空。");

            // 火堆：每与一张废料相邻 +1 多重释放（只数邻居）。
            CardInstance fire = new CardInstance(5, CardDatabase.Instance.Get("campfire"));
            CardInstance waste = new CardInstance(6, CardDatabase.Instance.Get("concrete_block"));
            CardInstance waste2 = new CardInstance(7, CardDatabase.Instance.Get("nuclear_water"));
            Assert(LoadoutPreview.BonusDice(new[] { waste, fire, waste2 }, 1) == 2,
                "火堆左右各一张废料时，自己获得 +2 多重释放。");
            Assert(LoadoutPreview.BonusDice(new[] { fire, waste, waste2 }, 0) == 1,
                "火堆只有一张相邻废料时只能 +1。");

            // 栏位可以留空：磨刀石在 0 列、撬棍在 2 列（中间空一格）就不再相邻。
            List<CardSlot> gapped = new List<CardSlot>
            {
                new CardSlot(0, whetstone),
                new CardSlot(2, crowbar)
            };
            Assert(LoadoutPreview.DiceModifier(gapped, crowbar.Uid) == 0,
                "A gap between two slots must break the adjacency bonus.");
            List<CardSlot> packed = new List<CardSlot>
            {
                new CardSlot(0, whetstone),
                new CardSlot(1, crowbar)
            };
            Assert(LoadoutPreview.DiceModifier(packed, crowbar.Uid) == 2,
                "Two touching slots must still count as adjacent.");

            // 回收站：全场废料多重释放 +1（AllByTag，不限相邻）。
            CardInstance recycler = new CardInstance(8, CardDatabase.Instance.Get("recycler"));
            Assert(LoadoutPreview.BonusDice(new[] { recycler, crowbar, waste }, 2) == 1,
                "回收站必须给场上的废料 +1 多重释放。");
            Assert(LoadoutPreview.BonusDice(new[] { recycler, crowbar }, 1) == 0,
                "回收站只影响废料，武器不额外获得骰数。");
        }

        private static void CheckCardDatabase()
        {
            CardDatabase catalog = CardDatabase.Instance;
            Assert(catalog.All.Count == 31,
                "The catalog must contain all thirty-one cards of the first set.");
            Assert(catalog.Get("crowbar").CooldownTicks == 180,
                "Crowbar must use a 180-tick cooldown.");
            Assert(catalog.Get("crowbar").SlotSize == 1,
                "Crowbar occupies one bag slot.");
            Assert(catalog.Get("whetstone").Auras.Count == 1,
                "Whetstone must publish its adjacent Weapon aura.");
            Assert(catalog.Get("plastic_shed").Passives.Count == 1,
                "Plastic shed must slow on cast and shield when a slow lands.");
            Assert(!catalog.Get("whetstone").IsActive,
                "A pure aura card must never cast on its own.");
            Assert(catalog.Get("crowbar").IsActive,
                "A card with a cooldown and effects must cast on its own.");
            Assert(catalog.All.Select(card => card.Id).Distinct().Count() == 31,
                "Card ids must be unique.");

            // 这些是摆在战斗栏里的装备（食物相当于带回复的防具），不是一次性消耗品：
            // 牌是永久资产（CJ-1.2），效果只在战斗中结算，背包里不该有回血、回再生这类效果。
            foreach (string id in new[]
                     {
                         "fresh_water", "mineral_water", "chocolate", "hardtack", "baguette", "dorayaki"
                     })
            {
                Assert(catalog.Get(id).Scope == CardScope.BattleOnly,
                    string.Concat(id, " is battle gear and must only work in battle."));
            }

            Assert(catalog.Get("recycler").Scope == CardScope.BagAndBattle,
                "The recycler keeps working while it sits in the bag.");
        }

        private static void CheckCardKeywords()
        {
            // 每个词都必须能被匹配到，并且配色是合法的 #RRGGBB。
            foreach (string keyword in CardKeywords.All)
            {
                string color = CardKeywords.ColorOf(keyword);
                Assert(color != null && color.Length == 7 && color[0] == '#',
                    string.Concat("Keyword ", keyword, " must have a #RRGGBB colour."));
                Assert(CardKeywords.Colorize(keyword).Contains("<b>" + keyword + "</b>"),
                    string.Concat("Keyword ", keyword, " must colour itself."));
            }

            // 长词优先：生命再生不能被拆成「生命」+「再生」。
            Assert(CardKeywords.Colorize("生命再生量提高 15").Contains("<b>生命再生</b>"),
                "Life regeneration must match as one keyword.");

            // 伤害必须上色——这条是回归断言（之前它就漏在匹配表外）。
            string damage = CardKeywords.Colorize("1d6 伤害");
            Assert(damage.Contains("<b>伤害</b>") && damage.Contains(CardKeywords.DamageColor),
                "Damage text must be coloured.");

            // 每张牌的每段效果 / 光环 / 被动文案，只要提到状态词就必须至少着色一次。
            string[] mustColour =
            {
                "伤害", "护盾", "中毒", "剧毒", "燃烧", "灼烧", "再生", "治疗",
                "充能", "加速", "缓速", "额外施放", "摧毁", "骰数", "骰值"
            };

            foreach (CardDefinition card in CardDatabase.Instance.All)
            {
                List<string> lines = new List<string>();
                if (card.ActiveEffects != null)
                {
                    foreach (CardEffect effect in card.ActiveEffects)
                    {
                        lines.Add(CardText.Effect(effect));
                    }
                }

                if (card.Auras != null)
                {
                    foreach (AuraDefinition aura in card.Auras)
                    {
                        lines.Add(CardText.Aura(aura));
                    }
                }

                if (card.Passives != null)
                {
                    foreach (PassiveDefinition passive in card.Passives)
                    {
                        lines.Add(CardText.Passive(passive));
                    }
                }

                foreach (string line in lines)
                {
                    bool mentionsStatus = false;
                    for (int index = 0; index < mustColour.Length; index++)
                    {
                        if (line.Contains(mustColour[index]))
                        {
                            mentionsStatus = true;
                            break;
                        }
                    }

                    if (!mentionsStatus)
                    {
                        continue;
                    }

                    Assert(CardKeywords.Colorize(line).Contains("<color="),
                        string.Concat("Card ", card.Id, " line \"", line, "\" must be coloured."));
                }
            }

            Console.WriteLine(string.Concat(
                "[keywords] count=",
                CardKeywords.All.Count.ToString(),
                " damage=",
                damage));
        }

        private static void CheckEventBus()
        {
            GameEventBus bus = new GameEventBus();
            List<string> order = new List<string>();

            bus.Subscribe<CombatStartedEvent>(
                order: 10,
                handler: _ =>
                {
                    order.Add("second");
                });

            bus.Subscribe<CombatStartedEvent>(
                order: 0,
                handler: _ =>
                {
                    order.Add("first");
                    bus.Publish(
                        tick: 0,
                        gameEvent: new DamageDealtEvent(
                            sourcePlayerId: 0,
                            targetPlayerId: 1,
                            amount: 3,
                            shieldAbsorbed: 0,
                            ignoresShield: false));
                });

            CombatStartedEvent started = bus.Publish(
                tick: 0,
                gameEvent: new CombatStartedEvent(123UL));

            Assert(order.SequenceEqual(new[] { "first", "second" }),
                "Event listeners must run by deterministic order.");
            Assert(started.Sequence == 0, "The first event must have sequence 0.");
            Assert(bus.History.Count == 2, "Nested events must remain in history.");
            Assert(bus.History[1].Sequence == 1,
                "Nested events must receive the next sequence.");
        }

        private static void CheckCombatSimulation()
        {
            CardDatabase catalog = CardDatabase.Instance;
            BattleSnapshot snapshot = new BattleSnapshot(
                new CombatantSnapshot(
                    playerId: 0,
                    level: 1,
                    battleCards: new[]
                    {
                        new CardInstance(1, catalog.Get("whetstone")),
                        new CardInstance(2, catalog.Get("crowbar"))
                    }),
                new CombatantSnapshot(
                    playerId: 1,
                    level: 1,
                    battleCards: new[]
                    {
                        new CardInstance(3, catalog.Get("car_door"))
                    }));

            CombatSimulation first = new CombatSimulation(snapshot, 987654UL);
            for (int index = 0; index < 1200 && !first.IsFinished; index++)
            {
                first.TickOnce();
            }

            int crowbarCasts = first.Events.History
                .OfType<CardCastEvent>()
                .Count(gameEvent =>
                    gameEvent.PlayerId == 0 &&
                    gameEvent.CardUid == 2);
            // 冷却从满值开始：第一次施放在 3s，之后每 3s 一次，头 20 秒一共 6 次（3/6/9/12/15/18s）。
            Assert(crowbarCasts == 6,
                "A 3s card must cast exactly six times in the first 20 seconds.");

            DamageDealtEvent firstCrowbarHit = first.Events.History
                .OfType<DamageDealtEvent>()
                .First(gameEvent =>
                    gameEvent.SourcePlayerId == 0 &&
                    gameEvent.TargetPlayerId == 1 &&
                    !gameEvent.IgnoresShield);
            Assert(firstCrowbarHit.Amount >= 3 && firstCrowbarHit.Amount <= 8,
                "Whetstone must add +2 per die after the base die is clamped.");

            CombatSimulation second = new CombatSimulation(snapshot, 987654UL);
            CombatResult firstResult = first.RunToCompletion();
            CombatResult secondResult = second.RunToCompletion();
            Assert(firstResult.WinnerPlayerId == secondResult.WinnerPlayerId &&
                   firstResult.Reason == secondResult.Reason &&
                   firstResult.EndTick == secondResult.EndTick,
                "The same combat snapshot and seed must produce the same result.");
            Assert(
                FingerprintEvents(first.Events.History) ==
                FingerprintEvents(second.Events.History),
                "The same combat seed must produce the same event stream.");

            BattleSnapshot emptyBattle = new BattleSnapshot(
                new CombatantSnapshot(10, 1, Array.Empty<CardInstance>()),
                new CombatantSnapshot(11, 1, Array.Empty<CardInstance>()));
            CombatResult timeoutResult =
                new CombatSimulation(emptyBattle, 3UL).RunToCompletion();
            Assert(timeoutResult.Reason == CombatEndReason.TimeoutKnockout,
                "Equal cardless combatants should fall to timeout penalties together.");
            Assert(timeoutResult.IsDraw,
                "Equal effective health and no actions must end in a draw.");
        }

        /// <summary>
        /// 随机数机制的回归检查：
        /// ① 每局的根种子必须每次都不同、且不为 0；
        /// ② 子流派生是确定性的，不同编号互不相同；
        /// ③ 同一颗种子必然复现同一串骰子，换种子必换骰子序列；
        /// ④ 怪物牌组按（种子, 地块）生成，同种子同格一致、换格即换牌，且不超容量。
        /// </summary>
        private static void CheckRandomness()
        {
            HashSet<ulong> seeds = new HashSet<ulong>();
            for (int index = 0; index < 512; index++)
            {
                ulong created = MatchSeed.Create();
                Assert(created != 0UL, "A root seed must never be zero.");
                Assert(seeds.Add(created),
                    "MatchSeed.Create must not hand out the same seed twice.");
            }

            ulong root = seeds.First();
            Assert(MatchSeed.Derive(root, 1UL) == MatchSeed.Derive(root, 1UL),
                "Deriving the same stream twice must be deterministic.");
            Assert(MatchSeed.Derive(root, 1UL) != MatchSeed.Derive(root, 2UL),
                "Different stream ids must produce different streams.");

            List<int> rollsA = RollSequence(11UL, 40);
            List<int> rollsC = RollSequence(11UL, 40);
            List<int> rollsB = RollSequence(12UL, 40);
            Assert(rollsA.Count >= 20,
                "The probe match must roll at least 20 times.");
            Assert(rollsA.SequenceEqual(rollsC),
                "The same seed must reproduce the same dice sequence.");
            Assert(!rollsA.SequenceEqual(rollsB),
                "Two different seeds must not roll the same dice sequence.");

            MapGraph graph = CreateM3Graph();
            MatchController deckA = new MatchController(graph, 11UL);
            MatchController deckB = new MatchController(graph, 12UL);

            // 怪物牌组是固定的：随机化实测把 1 级怪物战胜率从 92% 打到 26%、
            // 中位圈数从 10 拉到 12，属于数值改动，按约定不擅自调整。
            // 因此这里断言它「跨种子稳定、按等级逐级加强、永远装得下」。
            Assert(
                DeckFingerprint(deckA.CreateEnemyDeck(3, 5000)) ==
                DeckFingerprint(deckB.CreateEnemyDeck(3, 5000)),
                "Monster decks must stay stable across seeds.");
            Assert(
                DeckFingerprint(deckA.CreateEnemyDeck(3, 5000)) !=
                DeckFingerprint(deckA.CreateEnemyDeck(1, 5000)),
                "Higher level monsters must field a bigger deck.");

            for (int level = 1; level <= 5; level++)
            {
                int width = deckA
                    .CreateEnemyDeck(level, 9000)
                    .Sum(card => card.Definition.SlotSize);
                Assert(width <= LoadoutRules.GetBattleCapacity(level),
                    "A generated monster deck must fit the battle row capacity.");
            }

            Console.WriteLine(
                $"[rng] seeds={seeds.Count} rolls={rollsA.Count} " +
                $"sample={DeckFingerprint(deckA.CreateEnemyDeck(3, 5000))}");
        }

        /// <summary>用一颗种子跑一串掷骰，返回骰值顺序（其余阶段用最省事的命令推进）。</summary>
        private static List<int> RollSequence(ulong seed, int count)
        {
            MatchController controller = new MatchController(CreateM3Graph(), seed);
            List<int> rolls = new List<int>();
            int guard = 0;
            while (rolls.Count < count && guard++ < 4000 && !controller.IsFinished)
            {
                MatchPlayerState player = controller.State.CurrentPlayer;
                switch (controller.State.Phase)
                {
                    case MatchPhase.AwaitRoll:
                        controller.Submit(new RollCommand(player.Id));
                        rolls.Add(controller.State.LastRoll);
                        break;
                    case MatchPhase.AwaitMove:
                        controller.Submit(new MoveCommand(player.Id));
                        break;
                    case MatchPhase.AwaitNorma:
                        controller.Submit(new SetNormaCommand(player.Id, NormaType.Shard));
                        break;
                    case MatchPhase.AwaitReward:
                        controller.Submit(new ChooseRewardCommand(player.Id, 0));
                        break;
                    case MatchPhase.AwaitShop:
                        controller.Submit(
                            new ShopActionCommand(player.Id, ShopActionType.Leave));
                        break;
                    default:
                        return rolls;
                }
            }

            return rolls;
        }

        private static string DeckFingerprint(IEnumerable<CardInstance> cards)
        {
            return string.Join(",", cards.Select(card => card.Definition.Id));
        }

        private static void CheckM1MatchDurability()
        {
            MapGraph graph = CreateTestGraph();
            MatchController first = new MatchController(graph, 20261031UL);
            MatchController second = new MatchController(graph, 20261031UL);

            MatchAutomationReport firstReport =
                SimpleMatchAutoplayer.Run(first, maximumTurns: 200);
            MatchAutomationReport secondReport =
                SimpleMatchAutoplayer.Run(second, maximumTurns: 200);

            Assert(firstReport.CompletedTurns == 200 || firstReport.Finished,
                "Four headless seats must complete 200 turns or reach a winner.");
            Assert(firstReport.StateHash == secondReport.StateHash,
                "The same seed and AI policy must produce the same match hash.");
            Assert(first.Events.History.Count > 0,
                "The match must emit an event stream.");
            Assert(first.Events.History.OfType<PlayerTeleportedEvent>().Any(),
                "The 200-turn regression map must exercise Warp movement.");
        }

        private static void CheckHomeProgression()
        {
            MapGraph graph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Reward, -1, 2),
                    new MapNode(2, TileType.Home, 1, 3),
                    new MapNode(3, TileType.Reward, -1, 4),
                    new MapNode(4, TileType.Home, 2, 5),
                    new MapNode(5, TileType.Reward, -1, 6),
                    new MapNode(6, TileType.Home, 3, 7),
                    new MapNode(7, TileType.Reward, -1, 0)
                },
                playerCount: 4);
            MatchController controller = new MatchController(graph, 44UL);
            SimpleMatchAutoplayer.Run(controller, maximumTurns: 200);
            Assert(
                controller.State.Players.Any(player => player.Level > 1),
                "Reward income and Home Norma resolution must allow progression.");
        }

        /// <summary>
        /// R-TURN-002：移动/重排是免费动作，但必须按格位宽度校验战斗栏容量（CJ-5）。
        /// </summary>
        private static void CheckCardBandCommands()
        {
            MapGraph graph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1, 11),
                    new MapNode(1, TileType.Neutral, -1, 2),
                    new MapNode(2, TileType.Neutral, -1, 3),
                    new MapNode(3, TileType.Home, 1, 4),
                    new MapNode(4, TileType.Neutral, -1, 5),
                    new MapNode(5, TileType.Neutral, -1, 6),
                    new MapNode(6, TileType.Home, 2, 7),
                    new MapNode(7, TileType.Neutral, -1, 8),
                    new MapNode(8, TileType.Neutral, -1, 9),
                    new MapNode(9, TileType.Home, 3, 10),
                    new MapNode(10, TileType.Neutral, -1, 11),
                    new MapNode(11, TileType.Neutral, -1, 0)
                },
                playerCount: 4);
            MatchController controller = new MatchController(graph, 90125UL);
            MatchPlayerState player = controller.State.CurrentPlayer;
            Assert(
                player.OwnedCards.Count == 0,
                "The opening hand must be empty; the seat is funded with shards instead.");
            Assert(
                player.Shards == MatchController.StartingShards,
                "The opening grant must be " + MatchController.StartingShards.ToString() + " shards.");

            // 开局不发牌了（R-SHOP-001）：这里手动塞几张，等价于在商店买到的牌。
            for (int seeded = 0; seeded < 3; seeded++)
            {
                string cardId = seeded == 1 ? "nuclear_water" : "crowbar";
                Assert(
                    player.TryAddCard(
                        new CardInstance(7000 + seeded, CardDatabase.Instance.Get(cardId)),
                        equipIfPossible: true),
                    "Seeding the opening loadout must succeed.");
            }

            Assert(player.OwnedCards.Count >= 2, "The seeded loadout must contain cards.");

            CardInstance battleCard = player.BattleCards[0];
            controller.Submit(new MoveCardCommand(player.Id, battleCard.Uid, toBattle: false, column: 0));
            Assert(!player.IsEquipped(battleCard), "Dropping a battle card on the bag must unequip it.");
            Assert(
                player.ColumnOf(battleCard.Uid) >= 0,
                "The unequipped card must land on some free column of the bag row.");

            // 关键：栏位可以不连续。先把战斗栏全部退回背包，再单独把这张牌停到第 5 列，
            // 中间 0~4 列留空（R-CARD-011）。
            int battleCapacity = LoadoutRules.GetBattleCapacity(player.Level);
            foreach (CardInstance equipped in player.BattleCards.ToArray())
            {
                controller.Submit(new MoveCardCommand(player.Id, equipped.Uid, toBattle: false, column: -1));
            }

            Assert(player.BattleCards.Count == 0, "The battle row must be empty before the gap test.");
            if (battleCapacity >= 6)
            {
                controller.Submit(new MoveCardCommand(
                    player.Id,
                    battleCard.Uid,
                    toBattle: true,
                    column: 5));
                Assert(
                    player.ColumnOf(battleCard.Uid) == 5,
                    "A card dropped on column 5 must stay on column 5 with a gap in front.");
            }
            else
            {
                controller.Submit(new MoveCardCommand(
                    player.Id,
                    battleCard.Uid,
                    toBattle: true,
                    column: 0));
            }

            Assert(
                player.BattleCards[0].Uid == battleCard.Uid,
                "BattleCards must stay ordered by column.");

            // 塞满战斗栏：首张放不下的牌必须被拒绝，且容量永不被突破。
            int capacity = LoadoutRules.GetBattleCapacity(player.Level);
            CardInstance overflow = null;
            foreach (CardInstance card in player.OwnedCards.ToArray())
            {
                if (player.IsEquipped(card))
                {
                    continue;
                }

                if (player.BattleCardWidth + card.Definition.SlotSize <= capacity)
                {
                    controller.Submit(new MoveCardCommand(player.Id, card.Uid, true, 0));
                    continue;
                }

                overflow = card;
            }

            bool rejected = false;
            if (overflow != null)
            {
                try
                {
                    controller.Submit(new MoveCardCommand(player.Id, overflow.Uid, true, 0));
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
            }

            if (overflow != null)
            {
                Assert(rejected, "A card that does not fit the battle capacity must be rejected.");
            }

            Assert(
                player.BattleCardWidth <= capacity,
                "The battle row must never exceed the capacity of the current level.");

            CardInstance doomed = player.OwnedCards[0];
            int valueBefore = doomed.Value;
            int shardsBefore = player.Shards;
            controller.Submit(new SellCardCommand(player.Id, doomed.Uid));
            Assert(
                player.Shards == shardsBefore + valueBefore,
                "R-ECO-005: selling must pay the card's current value.");
            Assert(
                player.OwnedCards.All(card => card.Uid != doomed.Uid),
                "CJ-6.5: a sold card is gone for good.");
            bool soldTwice = false;
            try
            {
                controller.Submit(new SellCardCommand(player.Id, doomed.Uid));
                soldTwice = true;
            }
            catch (InvalidOperationException)
            {
            }

            Assert(!soldTwice, "A sold card cannot be sold again.");
            Console.WriteLine(
                $"[sell] value={valueBefore} shards={player.Shards}");
        }

        private static void CheckRewardChoice()
        {
            MapGraph graph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Reward, -1, 2),
                    new MapNode(2, TileType.Home, 1, 3),
                    new MapNode(3, TileType.Reward, -1, 4),
                    new MapNode(4, TileType.Home, 2, 5),
                    new MapNode(5, TileType.Reward, -1, 6),
                    new MapNode(6, TileType.Home, 3, 7),
                    new MapNode(7, TileType.Reward, -1, 0)
                },
                playerCount: 4);

            for (ulong seed = 1; seed < 4000; seed++)
            {
                MatchController controller = new MatchController(graph, seed);
                controller.Submit(new RollCommand(0));
                if (controller.State.LastRoll != 1)
                {
                    continue;
                }

                controller.Submit(new MoveCommand(0));
                Assert(
                    controller.State.Phase == MatchPhase.AwaitReward,
                    "Landing on a Reward tile must hold the turn until a card is chosen.");
                Assert(
                    controller.CurrentRewardChoices.Count == 2,
                    "A Reward tile must offer exactly two cards.");

                CardDefinition chosen = controller.CurrentRewardChoices[0];
                controller.Submit(new ChooseRewardCommand(0, 0));
                Assert(
                    controller.State.Players[0].OwnedCards.Any(
                        card => card.Definition.Id == chosen.Id),
                    "The chosen reward must enter the player's bag.");
                Assert(
                    controller.State.Phase != MatchPhase.AwaitReward,
                    "Choosing a reward must end the Reward phase.");
                return;
            }

            throw new InvalidOperationException(
                "Could not find a seed whose first roll is 1.");
        }

        private static void CheckWarpTeleport()
        {
            MapGraph graph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Neutral, -1, 2),
                    new MapNode(2, TileType.Home, 1, 3),
                    new MapNode(3, TileType.Warp, -1, 4),
                    new MapNode(4, TileType.Home, 2, 5),
                    new MapNode(5, TileType.Neutral, -1, 6),
                    new MapNode(6, TileType.Home, 3, 7),
                    new MapNode(7, TileType.Warp, -1, 0)
                },
                playerCount: 4,
                teleportGroups: new[] { new[] { 3, 7 } });

            // 经过传送格不传送（R-MOVE-006）：从 0 走 4 步，途经 3 号传送格。
            bool passChecked = false;
            for (ulong seed = 1; seed < 4000 && !passChecked; seed++)
            {
                MatchController passController = new MatchController(graph, seed);
                passController.Submit(new RollCommand(0));
                if (passController.State.LastRoll != 4)
                {
                    continue;
                }

                int passStart = passController.Events.History.Count;
                passController.Submit(new MoveCommand(0));
                Assert(
                    !passController.Events.History
                        .Skip(passStart)
                        .OfType<PlayerTeleportedEvent>()
                        .Any(),
                    "Passing over a Warp tile must not teleport the player.");
                Assert(
                    passController.State.Players[0].CurrentNode == 4,
                    "A pass over a Warp tile must leave the player on the rolled destination.");
                passChecked = true;
            }

            Assert(passChecked, "Could not find a seed whose first roll is 4.");

            for (ulong seed = 1; seed < 4000; seed++)
            {
                MatchController controller = new MatchController(graph, seed);
                controller.Submit(new RollCommand(0));
                if (controller.State.LastRoll != 3)
                {
                    continue;
                }

                int eventStart = controller.Events.History.Count;
                controller.Submit(new MoveCommand(0));

                List<PlayerTeleportedEvent> teleports = controller.Events.History
                    .Skip(eventStart)
                    .OfType<PlayerTeleportedEvent>()
                    .ToList();
                Assert(
                    teleports.Count == 1,
                    "Stopping on a Warp tile must teleport exactly once.");
                Assert(
                    teleports[0].FromNodeId == 3 && teleports[0].ToNodeId == 7,
                    "Warp must move the player to the other tile of the same group.");
                Assert(
                    controller.State.Players[0].CurrentNode == 7,
                    "The player must end the turn on the warp destination.");
                return;
            }

            throw new InvalidOperationException(
                "Could not find a seed whose first roll is 3.");
        }

        private static void CheckShopTile()
        {
            MapGraph graph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Shop, -1, 2),
                    new MapNode(2, TileType.Home, 1, 3),
                    new MapNode(3, TileType.Neutral, -1, 4),
                    new MapNode(4, TileType.Home, 2, 5),
                    new MapNode(5, TileType.Shop, -1, 6),
                    new MapNode(6, TileType.Home, 3, 7),
                    new MapNode(7, TileType.Neutral, -1, 0)
                },
                playerCount: 4);

            for (ulong seed = 1UL; seed <= 4096UL; seed++)
            {
                MatchController controller = new MatchController(graph, seed);
                MatchPlayerState player = controller.State.CurrentPlayer;
                int shardsBefore = player.Shards;
                controller.Submit(new RollCommand(player.Id));
                if (controller.State.LastRoll != 1)
                {
                    continue;
                }

                controller.Submit(new MoveCommand(player.Id));

                Assert(
                    controller.State.Phase == MatchPhase.AwaitShop,
                    "Landing on a Shop tile must open the shop phase.");
                Assert(
                    controller.CurrentShop != null,
                    "The shop session must exist while the phase is AwaitShop.");
                Assert(
                    controller.CurrentShop.Offers.Count == 3,
                    "Rule 2.5: the shop shows three offers.");
                Assert(
                    !controller.CurrentShop.RerollUsed,
                    "A fresh shop visit must not be flagged as rerolled.");

                foreach (ShopOffer offer in controller.CurrentShop.Offers)
                {
                    int expected = offer.Definition.SlotSize == 1
                        ? 2
                        : offer.Definition.SlotSize == 2 ? 4 : 8;
                    Assert(
                        offer.Price == expected,
                        "Rule 2.5: 1c/2c/3c cost 2/4/8 Shards.");
                    Assert(
                        offer.IsAvailable,
                        "Fresh offers must be available.");
                }

                int price = controller.CurrentShop.Offers[0].Price;
                bool bought = false;
                try
                {
                    controller.Submit(new ShopActionCommand(
                        player.Id,
                        ShopActionType.Buy,
                        0));
                    bought = true;
                }
                catch (InvalidOperationException)
                {
                }

                if (shardsBefore < price)
                {
                    Assert(
                        !bought,
                        "Buying without enough Shards must be rejected.");
                    Assert(
                        controller.CurrentShop.Offers[0].IsAvailable,
                        "A rejected purchase must not consume the offer.");
                }
                else
                {
                    Assert(
                        bought && player.Shards == shardsBefore - price,
                        "A purchase must deduct the offer price.");
                    Assert(
                        !controller.CurrentShop.Offers[0].IsAvailable,
                        "A bought offer must leave the shelf.");
                    bool boughtTwice = false;
                    try
                    {
                        controller.Submit(new ShopActionCommand(
                            player.Id,
                            ShopActionType.Buy,
                            0));
                        boughtTwice = true;
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    Assert(
                        !boughtTwice,
                        "An offer can only be bought once per visit.");
                }

                int moverId = player.Id;
                controller.Submit(new ShopActionCommand(
                    moverId,
                    ShopActionType.Leave));
                Assert(
                    controller.State.Phase == MatchPhase.AwaitRoll,
                    "Leaving the shop must end the turn.");
                Assert(
                    controller.State.CurrentPlayer.Id != moverId,
                    "Leaving the shop must pass the turn on.");
                Console.WriteLine(
                    $"[shop] offers=3 seed={seed} bought={bought} " +
                    $"shards={shardsBefore}");
                return;
            }

            throw new InvalidOperationException(
                "Could not find a seed whose first roll lands on the shop.");
        }

        private static void CheckM3FullMatch()
        {
            MapGraph graph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Reward, -1, 2),
                    new MapNode(2, TileType.Shop, -1, 3),
                    new MapNode(3, TileType.Home, 1, 4),
                    new MapNode(4, TileType.Reward, -1, 5),
                    new MapNode(5, TileType.Event, -1, 6),
                    new MapNode(6, TileType.Home, 2, 7),
                    new MapNode(7, TileType.Reward, -1, 8),
                    new MapNode(8, TileType.Shop, -1, 9),
                    new MapNode(9, TileType.Home, 3, 10),
                    new MapNode(10, TileType.Reward, -1, 11),
                    new MapNode(11, TileType.Event, -1, 0)
                },
                playerCount: 4);
            MatchController controller = new MatchController(graph, 20261031UL);
            MatchAutomationReport report =
                SimpleMatchAutoplayer.Run(controller, maximumTurns: 1000);

            Assert(report.Finished,
                "M3 AI regression must reach a level-6 winner.");
            Assert(report.WinnerPlayerId >= 0,
                "A finished M3 match must have a winner.");
            Assert(controller.State.Players.Any(player => player.Level >= 6),
                "The winner must reach level 6.");
            Assert(
                controller.Events.History.OfType<GlobalShardsGrantedEvent>().Any(),
                "The match must exercise global Shard distribution.");
            Assert(
                controller.Events.History.OfType<CardAddedEvent>().Any(),
                "The match must exercise reward or shop card acquisition.");
            Assert(
                controller.Events.History
                    .OfType<CardAddedEvent>()
                    .Any(gameEvent => gameEvent.Source == "Shop"),
                "The M3 match must exercise at least one shop purchase.");
            Console.WriteLine(
                $"[M3] winner={report.WinnerPlayerId + 1} " +
                $"turns={report.CompletedTurns} " +
                $"hash={report.StateHash:X16}");
        }

        private static BattleSnapshot CombatOf(
            IEnumerable<string> leftCards,
            IEnumerable<string> rightCards)
        {
            CardDatabase catalog = CardDatabase.Instance;
            int uid = 1;
            List<CardInstance> left = new List<CardInstance>();
            foreach (string id in leftCards)
            {
                left.Add(new CardInstance(uid++, catalog.Get(id)));
            }

            List<CardInstance> right = new List<CardInstance>();
            foreach (string id in rightCards)
            {
                right.Add(new CardInstance(uid++, catalog.Get(id)));
            }

            return new BattleSnapshot(
                new CombatantSnapshot(0, 1, left),
                new CombatantSnapshot(1, 1, right));
        }

        /// <summary>回放用的时间轴：结算结果必须把完整事件流和战前快照带出来。</summary>
        /// <summary>
        /// R-CARD-011：栏位可以留空，留空就不是相邻——战斗里的光环也必须按列号判邻，
        /// 于是"磨刀石 + 撬棍"中间空一格时撬棍吃不到 +2 骰值。
        /// </summary>
        /// <summary>
        /// R-TURN-002：整理卡牌、出售、装备是免费动作，不占用回合行动——所以轮不到自己
        /// 的时候也能提交（本地玩家要能在别人回合里调整自己的装配）。掷骰这类回合行动
        /// 仍然只属于当前座位。
        /// </summary>
        /// <summary>
        /// 开局不再发牌（R-SHOP-001）：回归测试要验栏位/相邻这些东西，就自己往背包里塞几张，
        /// 效果等同于在商店买到。返回塞进去的牌，方便调用方断言。
        /// </summary>
        private static List<CardInstance> SeedLoadout(
            MatchPlayerState player,
            int uidStart,
            params string[] cardIds)
        {
            List<CardInstance> granted = new List<CardInstance>();
            for (int index = 0; index < cardIds.Length; index++)
            {
                CardInstance card = new CardInstance(
                    uidStart + index,
                    CardDatabase.Instance.Get(cardIds[index]));
                Assert(
                    player.TryAddCard(card, equipIfPossible: true),
                    "Seeding a card into the loadout must succeed.");
                granted.Add(card);
            }

            return granted;
        }

        /// <summary>
        /// R-ECO-008：开局不发牌，每个席位只拿 10 碎片。原来那几套起始卡牌
        /// 已经搬去当怪物配置（`MonsterDecks`），不再是玩家开局手牌。
        /// </summary>
        private static void CheckOpeningGrant()
        {
            MatchController controller = new MatchController(CreateSceneMapGraph(), 20261102UL);
            foreach (MatchPlayerState player in controller.State.Players)
            {
                Assert(
                    player.OwnedCards.Count == 0,
                    "Seat " + player.Id.ToString() + " must open with an empty loadout.");
                Assert(
                    player.BattleCards.Count == 0,
                    "Seat " + player.Id.ToString() + " must open with an empty battle row.");
                Assert(
                    player.Shards == MatchController.StartingShards,
                    "Seat " + player.Id.ToString() + " must open with "
                        + MatchController.StartingShards.ToString() + " shards, had "
                        + player.Shards.ToString() + ".");
            }

            Console.WriteLine(
                "[opening] cards=0 shards=" + MatchController.StartingShards.ToString());
        }

        private static void CheckFreeActionsDuringAnotherTurn()
        {
            MatchController controller = new MatchController(CreateM3Graph(), 5150UL);
            MatchPlayerState active = controller.State.CurrentPlayer;
            MatchPlayerState other = controller.State.Players.First(player => player.Id != active.Id);
            Assert(
                controller.State.Phase == MatchPhase.AwaitRoll,
                "The match has to open on the roll phase for the free action test.");

            // 开局不发牌：测试自己往背包里塞牌（等价于在商店买的）。
            SeedLoadout(other, 8100, "crowbar", "nuclear_water");

            CardInstance equipped = other.BattleCards[0];
            controller.Submit(new MoveCardCommand(other.Id, equipped.Uid, toBattle: false, column: 3));
            Assert(
                !other.IsEquipped(equipped),
                "A seat that is not active must still be able to pull a card out of battle.");
            Assert(
                other.ColumnOf(equipped.Uid) == 3,
                "A seat that is not active keeps the exact column it asked for.");

            CardInstance spare = other.OwnedCards.First(card => card.Uid != equipped.Uid);
            int shardsBefore = other.Shards;
            int ownedBefore = other.OwnedCards.Count;
            controller.Submit(new SellCardCommand(other.Id, spare.Uid));
            Assert(
                other.OwnedCards.Count == ownedBefore - 1,
                "A seat that is not active must still be able to sell a card.");
            Assert(other.Shards > shardsBefore, "Selling refunds shards no matter whose turn it is.");

            controller.Submit(new EquipCardCommand(other.Id, equipped.Uid));
            Assert(
                other.IsEquipped(equipped),
                "A seat that is not active must still be able to equip a card.");

            bool rollRejected = false;
            try
            {
                controller.Submit(new RollCommand(other.Id));
            }
            catch (InvalidOperationException)
            {
                rollRejected = true;
            }

            Assert(rollRejected, "Rolling stays exclusive to the active seat.");
            Console.WriteLine("[freeactions] move/sell/equip outside turn ok");
        }

        private static void CheckSlotAdjacencyInCombat()
        {
            CardInstance Make(int uid, string id)
            {
                return new CardInstance(uid, CardDatabase.Instance.Get(id));
            }

            CardInstance stone = Make(1, "whetstone");
            CardInstance bar = Make(2, "crowbar");
            CardInstance door = Make(3, "car_door");

            List<CardSlot> touching = new List<CardSlot>
            {
                new CardSlot(0, stone),
                new CardSlot(1, bar)
            };
            List<CardSlot> gapped = new List<CardSlot>
            {
                new CardSlot(0, stone),
                new CardSlot(2, bar)
            };
            List<CardSlot> lone = new List<CardSlot> { new CardSlot(0, door) };

            int touchingDamage = DamageInto(touching, lone);
            int gappedDamage = DamageInto(gapped, lone);
            Assert(
                touchingDamage > gappedDamage,
                "A gap between the whetstone and the crowbar must drop the +2 damage bonus.");

            Console.WriteLine($"[slots] touching={touchingDamage} gapped={gappedDamage}");
        }

        private static int DamageInto(List<CardSlot> left, List<CardSlot> right)
        {
            BattleSnapshot battle = new BattleSnapshot(
                new CombatantSnapshot(0, 1, left),
                new CombatantSnapshot(1, 1, right));
            CombatResult result = new CombatSimulation(battle, 424242UL).RunToCompletion();
            int total = 0;
            foreach (GameEvent gameEvent in result.Timeline)
            {
                if (gameEvent is DamageDealtEvent damage && damage.TargetPlayerId == 1)
                {
                    total += damage.Amount;
                }
            }

            return total;
        }

        private static void CheckCombatTimeline()
        {
            CardDatabase catalog = CardDatabase.Instance;
            BattleSnapshot snapshot = new BattleSnapshot(
                new CombatantSnapshot(
                    playerId: 0,
                    level: 2,
                    battleCards: new[]
                    {
                        new CardInstance(1, catalog.Get("crowbar")),
                        new CardInstance(2, catalog.Get("nuclear_water"))
                    }),
                new CombatantSnapshot(
                    playerId: 1,
                    level: 2,
                    battleCards: new[]
                    {
                        new CardInstance(3, catalog.Get("car_door"))
                    }));

            CombatResult result = new CombatSimulation(snapshot, 424242UL).RunToCompletion();

            Assert(result.Battle != null,
                "A combat result must carry the battle snapshot for playback.");
            Assert(result.Timeline.Count > 0,
                "A combat result must carry its event timeline.");
            Assert(result.Timeline[0] is CombatStartedEvent,
                "The timeline must start with CombatStartedEvent.");
            Assert(result.Timeline[result.Timeline.Count - 1] is CombatEndedEvent,
                "The timeline must end with CombatEndedEvent.");
            for (int index = 1; index < result.Timeline.Count; index++)
            {
                Assert(
                    result.Timeline[index].Tick >= result.Timeline[index - 1].Tick,
                    "The timeline must be ordered by tick.");
            }

            Assert(result.Timeline.Any(gameEvent => gameEvent is DamageDealtEvent),
                "A playback timeline without damage would render nothing.");
            foreach (CardCastEvent cast in result.Timeline.OfType<CardCastEvent>())
            {
                var cards = cast.PlayerId == result.Battle.Left.PlayerId
                    ? result.Battle.Left.BattleCards
                    : result.Battle.Right.BattleCards;
                Assert(
                    cards.Any(card => card.Uid == cast.CardUid),
                    "CardCastEvent must reference a card that is in that side's loadout.");
            }

            AssertReplayMatchesResult(result, "standalone");

            // 怪物战同样要能对上：玩家在左、怪物在右（怪物 id = 1000 + 地块 id）。
            CombatResult monster = FindMonsterCombat();
            if (monster != null)
            {
                AssertReplayMatchesResult(monster, "monster");
            }

            // 带治疗的对局必须能对上：回放层曾经漏掉 HealthRestoredEvent，
            // 血条只减不加，于是出现"血条空了却判你赢"的自相矛盾画面。
            CombatResult healing = new CombatSimulation(
                CombatOf(new[] { "snow_purifier", "car_door" }, new[] { "crowbar", "nuclear_water" }),
                909UL).RunToCompletion();
            Assert(
                healing.Timeline.Any(entry => entry is HealthRestoredEvent),
                "A healing deck must publish HealthRestoredEvent so the replay can show it.");
            AssertReplayMatchesResult(healing, "healing");

            Console.WriteLine(
                $"[timeline] events={result.Timeline.Count} end={result.EndTick}");
        }

        /// <summary>
        /// 用回放层那套算术（伤害扣血、治疗加血、护盾吸收）重演一遍时间轴，
        /// 结果必须与内核给出的最终血量一致——否则战斗画面会与结算面板自相矛盾
        /// （典型表现：玩家血条先归零，结算却判胜利）。
        /// </summary>
        private static void AssertReplayMatchesResult(CombatResult result, string label)
        {
            BattleSnapshot battle = result.Battle;
            Assert(battle != null, "Playback reconciliation needs the battle snapshot.");

            int leftHealth = battle.Left.MaxHealth;
            int rightHealth = battle.Right.MaxHealth;
            int leftShield = 0;
            int rightShield = 0;

            foreach (GameEvent gameEvent in result.Timeline
                         .OrderBy(entry => entry.Tick)
                         .ThenBy(entry => entry.Sequence))
            {
                if (gameEvent is DamageDealtEvent damage)
                {
                    bool onLeft = damage.TargetPlayerId == battle.Left.PlayerId;
                    int healthDamage = Math.Max(0, damage.Amount - damage.ShieldAbsorbed);
                    if (onLeft)
                    {
                        leftShield = Math.Max(0, leftShield - damage.ShieldAbsorbed);
                        leftHealth = Math.Max(0, leftHealth - healthDamage);
                    }
                    else
                    {
                        rightShield = Math.Max(0, rightShield - damage.ShieldAbsorbed);
                        rightHealth = Math.Max(0, rightHealth - healthDamage);
                    }

                    continue;
                }

                if (gameEvent is HealthRestoredEvent restored)
                {
                    bool onLeft = restored.PlayerId == battle.Left.PlayerId;
                    int healed = Math.Max(0, restored.ActualAmount);
                    if (onLeft)
                    {
                        leftHealth = Math.Min(battle.Left.MaxHealth, leftHealth + healed);
                    }
                    else
                    {
                        rightHealth = Math.Min(battle.Right.MaxHealth, rightHealth + healed);
                    }

                    continue;
                }

                if (gameEvent is ShieldGainedEvent gained)
                {
                    if (gained.PlayerId == battle.Left.PlayerId)
                    {
                        leftShield += gained.Amount;
                    }
                    else
                    {
                        rightShield += gained.Amount;
                    }
                }
            }

            Assert(
                leftHealth == result.LeftHealth && rightHealth == result.RightHealth,
                "Replayed health (" + leftHealth + "/" + rightHealth + ") must match the "
                + "kernel result (" + result.LeftHealth + "/" + result.RightHealth + ") in "
                + label + " combat.");
        }

        private static CombatResult FindMonsterCombat()
        {
            MatchController controller = new MatchController(CreateM3Graph(), 24680UL);
            new CpuMatchRunner(controller).Run(20000);
            foreach (CombatResolvedEvent combat in controller.Events.History
                         .OfType<CombatResolvedEvent>())
            {
                if (combat.DefenderId >= 1000 && combat.Result != null)
                {
                    return combat.Result;
                }
            }

            return null;
        }

        /// <summary>模拟模式：双方的初始生命值由玩家自己填，内核必须照这个数开局。</summary>
        private static void CheckCustomMaxHealth()
        {
            CardDatabase catalog = CardDatabase.Instance;
            BattleSnapshot snapshot = new BattleSnapshot(
                new CombatantSnapshot(
                    playerId: 0,
                    level: 5,
                    battleCards: new[] { new CardInstance(1, catalog.Get("crowbar")) },
                    maxHealth: 640),
                new CombatantSnapshot(
                    playerId: 1,
                    level: 5,
                    battleCards: new[] { new CardInstance(2, catalog.Get("car_door")) },
                    maxHealth: 480));

            Assert(snapshot.Left.MaxHealth == 640, "The left custom max health must be kept.");
            Assert(snapshot.Right.MaxHealth == 480, "Each side has its own max health.");

            CombatResult result = new CombatSimulation(snapshot, 7UL).RunToCompletion();
            Assert(result.Battle.Left.MaxHealth == 640,
                "The playback snapshot must keep the custom max health.");
            Assert(result.Battle.Right.MaxHealth == 480,
                "The playback snapshot must keep both custom health values.");
            Assert(result.EndTick > 0, "A custom battle must still run to an end.");
            Assert(result.LeftHealth <= 640 && result.RightHealth <= 480,
                "Neither side can end above its custom max health.");

            // 不带自定义生命值时仍然按等级取默认值（1 级 = 100）。
            Assert(new CombatantSnapshot(2, 1, Array.Empty<CardInstance>()).MaxHealth == 100,
                "Without an override the level table still decides the max health.");

            Console.WriteLine(
                "[lab] left=" + snapshot.Left.MaxHealth + " right=" + snapshot.Right.MaxHealth);
        }

        private static void CheckCardMechanics()
        {
            CooldownTimer timer = new CooldownTimer(180);
            timer.ApplyCharge(60);
            Assert(timer.RemainingTicks == 120,
                "Charge must advance the remaining cooldown directly.");
            timer.ApplyCharge(200);
            Assert(timer.RemainingTicks == 0 && timer.ChargeCarryTicks == 80,
                "Charge beyond the remaining cooldown must be stored as carry.");
            timer.SetRemainingTicks(180);
            Assert(timer.RemainingTicks == 100 && timer.ChargeCarryTicks == 0,
                "The stored carry must be spent on the next cooldown and then cleared.");

            CombatSimulation maxRoll = new CombatSimulation(
                CombatOf(new[] { "ouroboros", "crowbar" }, new[] { "car_door" }),
                11UL);
            for (int tick = 0; tick < 240 && !maxRoll.IsFinished; tick++)
            {
                maxRoll.TickOnce();
            }

            List<DamageDealtEvent> forced =
                maxRoll.Events.History
                    .OfType<DamageDealtEvent>()
                    .Where(gameEvent =>
                        gameEvent.SourcePlayerId == 0 &&
                        gameEvent.TargetPlayerId == 1 &&
                        !gameEvent.IgnoresShield)
                    .ToList();
            Assert(forced.Count > 0 && forced.All(hit => hit.Amount == 6),
                "Ouroboros must force the right neighbour to roll the maximum value.");

            CombatSimulation taped = new CombatSimulation(
                CombatOf(new[] { "duct_tape", "crowbar" }, new[] { "car_door" }),
                12UL);
            // 冷却从满值开始，撬棍 3s 后才会施放第一次（BT-2.5 ③）。
            for (int tick = 0; tick < 240 && !taped.IsFinished; tick++)
            {
                taped.TickOnce();
            }

            Assert(
                taped.Events.History.OfType<ShieldGainedEvent>()
                    .Any(gameEvent => gameEvent.PlayerId == 0),
                "Duct tape must shield its owner when an adjacent card casts.");

            CombatSimulation recycled = new CombatSimulation(
                CombatOf(new[] { "recycler", "concrete_block" }, new[] { "car_door" }),
                13UL);
            for (int tick = 0; tick < 240 && !recycled.IsFinished; tick++)
            {
                recycled.TickOnce();
            }

            Assert(
                recycled.Events.History.OfType<CardCastEvent>()
                    .Count(gameEvent =>
                        gameEvent.PlayerId == 0 && gameEvent.CardUid == 2) == 2,
                "Recycler must grant one extra cast to every waste card.");

            CombatSimulation molotov = new CombatSimulation(
                CombatOf(new[] { "molotov" }, new[] { "car_door" }),
                14UL);
            for (int tick = 0; tick < 600 && !molotov.IsFinished; tick++)
            {
                molotov.TickOnce();
            }

            Assert(
                molotov.Events.History.OfType<CardDestroyedEvent>()
                    .Any(gameEvent => gameEvent.PlayerId == 0),
                "Molotov must destroy itself after its only cast.");
            Assert(
                molotov.Events.History.OfType<CardCastEvent>()
                    .Count(gameEvent => gameEvent.PlayerId == 0) == 1,
                "A destroyed card must not cast again.");

            CombatSimulation slowedEnemy = new CombatSimulation(
                CombatOf(new[] { "concrete_block" }, new[] { "crowbar" }),
                16UL);
            for (int tick = 0; tick < 240 && !slowedEnemy.IsFinished; tick++)
            {
                slowedEnemy.TickOnce();
            }

            Assert(
                slowedEnemy.Events.History.OfType<SlowAppliedEvent>()
                    .Any(gameEvent => gameEvent.PlayerId == 1),
                "Concrete block must slow a random enemy card.");

            CombatSimulation shredder = new CombatSimulation(
                CombatOf(new[] { "shredder" }, new[] { "car_door" }),
                17UL);
            for (int tick = 0; tick < 600 && !shredder.IsFinished; tick++)
            {
                shredder.TickOnce();
            }

            DamageDealtEvent shedHit =
                shredder.Events.History
                    .OfType<DamageDealtEvent>()
                    .First(gameEvent =>
                        gameEvent.SourcePlayerId == 0 &&
                        gameEvent.TargetPlayerId == 1 &&
                        !gameEvent.IgnoresShield);
            Assert(shedHit.Amount >= 2 && shedHit.Amount <= 16,
                "Shredder must gain a permanent extra die at battle start.");

            CombatSimulation chariot = new CombatSimulation(
                CombatOf(new[] { "golden_chariot" }, new[] { "car_door" }),
                18UL);
            for (int tick = 0; tick < 800 && !chariot.IsFinished; tick++)
            {
                chariot.TickOnce();
            }

            DamageDealtEvent chariotHit =
                chariot.Events.History
                    .OfType<DamageDealtEvent>()
                    .First(gameEvent =>
                        gameEvent.SourcePlayerId == 0 &&
                        gameEvent.TargetPlayerId == 1 &&
                        !gameEvent.IgnoresShield);
            Assert(chariotHit.Amount >= 4 && chariotHit.Amount <= 24,
                "Golden chariot must roll one die per point of value (4d6).");
        }

        private readonly struct SceneCell
        {
            public SceneCell(int row, int column, TileType tileType, int ownerPlayerId)
            {
                Row = row;
                Column = column;
                TileType = tileType;
                OwnerPlayerId = ownerPlayerId;
            }

            public int Row { get; }
            public int Column { get; }
            public TileType TileType { get; }
            public int OwnerPlayerId { get; }
        }

        /// <summary>
        /// R-MAP-002：开局离开 Home 的第一步不给选择，强制走逆时针方向。
        /// </summary>
        private static void CheckFirstMoveFromHome()
        {
            MapGraph graph = CreateSceneMapGraph();
            MatchController controller = new MatchController(graph, 86420UL);
            MatchPlayerState player = controller.State.CurrentPlayer;
            int home = player.HomeNode;
            int expected = graph.CounterClockwiseNeighbour(home);

            Assert(player.CurrentNode == home, "A match starts with the player on Home.");
            Assert(expected >= 0, "Home must have a counter-clockwise neighbour.");
            Assert(!player.HasMoved, "A fresh player has not moved yet.");

            IReadOnlyList<int> legal = controller.LegalNextNodes(home, -1);
            Assert(
                legal.Count == 1 && legal[0] == expected,
                "Leaving Home on the opening move must force the counter-clockwise edge.");

            controller.Submit(new RollCommand(player.Id));
            foreach (MovementCandidate candidate in controller.CurrentCandidates)
            {
                Assert(
                    candidate.Path[0] == expected,
                    "Every opening candidate must start counter-clockwise from Home.");
            }

            controller.Submit(new MoveCommand(player.Id, null, 0));
            PlayerMovedEvent firstStep = controller.Events.History
                .OfType<PlayerMovedEvent>()
                .FirstOrDefault(moved => moved.PlayerId == player.Id && moved.StepIndex == 0);

            Assert(firstStep != null, "The opening move must publish its first step.");
            Assert(
                firstStep.FromNodeId == home && firstStep.ToNodeId == expected,
                "The opening move must leave Home towards the counter-clockwise edge.");
            Assert(player.HasMoved, "Moving must remember that the player left Home.");

            Console.WriteLine(
                "[firstmove] home=" + home.ToString()
                + " forced=" + expected.ToString()
                + " stops=" + controller.CurrentCandidates.Count.ToString());
        }

        private static void CheckEarlyHomeStop()
        {
            // 四个家首尾相接：任何一次 >= 5 点的掷骰都会路过自家 Home，
            // 用来验证 R-HOME-002 的"经过自家 Home 可以提前停步"。
            MapGraph graph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Home, 1, 2),
                    new MapNode(2, TileType.Home, 2, 3),
                    new MapNode(3, TileType.Home, 3, 0)
                },
                playerCount: 4);

            MatchController match = null;
            MovementCandidate stop = null;
            for (ulong seed = 1; seed <= 200 && stop == null; seed++)
            {
                MatchController probe = new MatchController(graph, seed);
                MatchPlayerState probePlayer = probe.State.CurrentPlayer;
                Assert(
                    probePlayer.CurrentNode == probePlayer.HomeNode,
                    "The probe player must start on their own Home.");
                probe.Submit(new RollCommand(probePlayer.Id));

                int roll = probe.State.LastRoll;
                foreach (MovementCandidate candidate in probe.CurrentCandidates)
                {
                    if (candidate.TargetNodeId == probePlayer.HomeNode &&
                        candidate.Steps < roll)
                    {
                        match = probe;
                        stop = candidate;
                        break;
                    }
                }
            }

            Assert(
                match != null && stop != null,
                "Some roll must offer an early stop on the player's own Home.");

            MatchPlayerState player = match.State.CurrentPlayer;
            int rollValue = match.State.LastRoll;
            int steps = stop.Steps;
            Assert(
                steps < rollValue,
                "The early stop must cost fewer steps than the rolled value.");

            match.Submit(new MoveCommand(player.Id, stop.TargetNodeId));

            List<PlayerMovedEvent> walked = match.Events.History
                .OfType<PlayerMovedEvent>()
                .Where(moved => moved.PlayerId == player.Id)
                .ToList();

            Assert(
                walked.Count == steps,
                "Stopping early on Home must walk exactly "
                    + steps.ToString() + " steps, walked "
                    + walked.Count.ToString() + ".");
            Assert(
                walked.Count < rollValue,
                "Stopping early on Home must give up the remaining steps.");
            Assert(
                player.CurrentNode == player.HomeNode,
                "The early stop must leave the player on their own Home.");
            // 开局有 10 碎片，停在自己的家会立刻弹出升级选择，所以这里可能是
            // AwaitNorma（升级）而不是 AwaitRoll；两种都算"这一轮结束了"。
            Assert(
                match.State.Phase == MatchPhase.AwaitRoll ||
                match.State.Phase == MatchPhase.AwaitNorma,
                "An early stop must end the movement, phase was "
                    + match.State.Phase.ToString() + ".");

            Console.WriteLine(
                "[homestop] roll=" + rollValue.ToString()
                + " steps=" + steps.ToString()
                + " home=" + player.HomeNode.ToString());
        }

        private static void CheckPassThroughOpponent()
        {
            // 8 格环：节点 0..3 是四个家（玩家 0..3 各占一个），4..7 是空地。
            // 玩家 0 从家出发时，节点 1/2/3 上分别站着玩家 1/2/3。
            MapNode[] nodes = new MapNode[8];
            for (int index = 0; index < nodes.Length; index++)
            {
                bool isHome = index < 4;
                nodes[index] = new MapNode(
                    index,
                    isHome ? TileType.Home : TileType.Neutral,
                    isHome ? index : -1,
                    (index + 1) % nodes.Length);
            }

            MapGraph graph = MapGraph.Create(nodes, playerCount: 4);

            // 不带 TargetNodeId：走满 5 步，路过玩家 1/2/3 的地块也不开打。
            MatchController walker = FindRollingController(graph, 5);
            Assert(walker != null, "Some seed must roll a 5 for the pass-through probe.");

            MatchPlayerState mover = walker.State.CurrentPlayer;
            Assert(mover.Id == 0, "The free-walk probe expects player 1 to take the first turn.");
            Assert(mover.CurrentNode == 0, "The free-walk probe expects player 1 to start on Home.");

            walker.Submit(new MoveCommand(mover.Id, null));
            Assert(
                mover.CurrentNode == 5,
                "Ignoring an opponent must keep walking to the rolled destination, landed at "
                    + mover.CurrentNode.ToString() + ".");
            Assert(
                !walker.Events.History.OfType<CombatResolvedEvent>().Any(),
                "Walking through an occupied tile must not resolve a fight.");

            // 带 TargetNodeId：停在被别人占住的格子上才结算战斗。
            MatchController brawl = FindRollingController(graph, 3);
            Assert(brawl != null, "Some seed must roll a 3 for the landing probe.");

            MatchPlayerState attacker = brawl.State.CurrentPlayer;
            brawl.Submit(new MoveCommand(attacker.Id, 3));
            CombatResolvedEvent fight = brawl.Events.History
                .OfType<CombatResolvedEvent>()
                .FirstOrDefault();
            Assert(fight != null, "Stopping on an occupied tile must resolve a fight.");
            Assert(
                fight.AttackerId == attacker.Id && fight.DefenderId == 3,
                "The landing fight must be against the player standing on that tile.");

            // 正好落在别人格子上时也能「避开」：不结算战斗，两人同格（R-MOVE-009）。
            MatchController dodge = FindRollingController(graph, 3);
            Assert(dodge != null, "Some seed must roll a 3 for the avoid probe.");

            MatchPlayerState dodger = dodge.State.CurrentPlayer;
            dodge.Submit(new MoveCommand(dodger.Id, 3, avoidEncounter: true));
            Assert(
                dodger.CurrentNode == 3,
                "Avoiding must still finish the walk on the occupied tile, landed at "
                    + dodger.CurrentNode.ToString() + ".");
            Assert(
                !dodge.Events.History.OfType<CombatResolvedEvent>().Any(),
                "Avoiding an opponent must not resolve a fight.");
            Assert(
                dodge.State.Players.Count(other => other.CurrentNode == 3) == 2,
                "Avoiding must leave both players on the same tile.");
            // 落地格是玩家 3 的家：避开后格子效果照常结算，可能停在 AwaitNorma / AwaitReward，
            // 只要不再是「还在走」就说明这一轮结束了。
            Assert(
                dodge.State.Phase != MatchPhase.AwaitMove,
                "Avoiding must end the turn instead of reopening the movement, phase was "
                    + dodge.State.Phase.ToString() + ".");

            Console.WriteLine(
                "[passthrough] walk-through stays open, landing on 3 fights player 4, avoid sticks");
        }

        /// <summary>
        /// 事件格（R-EVT-001..003）：停上去开一次三选一，付不起的选项被拒绝，「走开」什么都不拿就结束回合。
        /// </summary>
        private static void CheckMatchEvent()
        {
            // 8 格环：0..3 四个家、4 空地、5 事件格（奇数号 → 神秘饮料）、6..7 空地。
            // 玩家 0 掷 5 从家出发，正好停在事件格上。
            MapNode[] nodes = new MapNode[8];
            for (int index = 0; index < nodes.Length; index++)
            {
                bool isHome = index < 4;
                bool isEvent = index == 5;
                nodes[index] = new MapNode(
                    index,
                    isHome ? TileType.Home : isEvent ? TileType.Event : TileType.Neutral,
                    isHome ? index : -1,
                    (index + 1) % nodes.Length);
            }

            MapGraph graph = MapGraph.Create(nodes, playerCount: 4);

            // 1) 选「打包带走」：免费获得神秘饮料。
            MatchController take = FindRollingController(graph, 5);
            Assert(take != null, "Some seed must roll a 5 for the event probe.");

            MatchPlayerState taker = take.State.CurrentPlayer;
            int shardsBefore = taker.Shards;
            take.Submit(new MoveCommand(taker.Id, null));
            Assert(
                take.State.Phase == MatchPhase.AwaitEvent,
                "Landing on an event tile must open the event, phase was "
                    + take.State.Phase.ToString() + ".");
            Assert(take.CurrentEvent != null, "An open event must expose its session.");
            Assert(
                take.CurrentEvent.Definition.Kind == MatchEventKind.MysteryDrink,
                "An odd event node id must resolve to the mystery drink.");
            Assert(
                take.CurrentEvent.Options.Count == 3,
                "The mystery drink must offer three choices.");
            Assert(
                take.Events.History.OfType<EventOfferedEvent>().Any(
                    offered => offered.PlayerId == taker.Id && offered.NodeId == 5),
                "Opening an event must publish EventOfferedEvent.");

            take.Submit(new EventChoiceCommand(taker.Id, 1));
            Assert(
                taker.OwnedCards.Any(
                    card => card.Definition.Id == MatchEventCatalog.MysteryDrinkCardId),
                "Taking the mystery drink must put it in the bag.");
            Assert(
                take.Events.History.OfType<CardAddedEvent>().Any(
                    added => added.PlayerId == taker.Id
                        && added.CardId == MatchEventCatalog.MysteryDrinkCardId
                        && added.Source == "Event"),
                "A granted card must be reported as an event reward.");
            Assert(taker.Shards == shardsBefore, "The free option must not cost shards.");
            Assert(
                take.State.Phase == MatchPhase.AwaitRoll,
                "Resolving an event must hand the turn to the next seat.");
            Assert(take.CurrentEvent == null, "A resolved event must be cleared.");

            // 2) 走开：什么都不拿，回合照常结束。
            MatchController leave = FindRollingController(graph, 5);
            Assert(leave != null, "Some seed must roll a 5 for the walk-away probe.");

            MatchPlayerState leaver = leave.State.CurrentPlayer;
            int leaveShards = leaver.Shards;
            leave.Submit(new MoveCommand(leaver.Id, null));
            leave.Submit(new EventChoiceCommand(leaver.Id, -1));
            Assert(
                !leave.Events.History.OfType<CardAddedEvent>().Any(
                    added => added.PlayerId == leaver.Id),
                "Walking away must not grant anything.");
            Assert(leaver.Shards == leaveShards, "Walking away must not cost shards.");
            Assert(leave.State.Phase == MatchPhase.AwaitRoll, "Walking away must end the turn.");
            Assert(leave.CurrentEvent == null, "Walking away must clear the event.");

            // 3) 付不起的选项被拒绝，事件仍然开着。
            MatchController broke = FindRollingController(graph, 5);
            Assert(broke != null, "Some seed must roll a 5 for the refusal probe.");

            MatchPlayerState poor = broke.State.CurrentPlayer;
            // 开局现在有 10 碎片，最便宜的选项也付得起；这里把钱包清零，
            // 专门去撞「付不起」这条分支（内核里是 internal set，测试程序集可见）。
            poor.Shards = 0;
            broke.Submit(new MoveCommand(poor.Id, null));
            MatchEventOption paid = broke.CurrentEvent.Options[0];
            Assert(
                paid.ShardCost > poor.Shards,
                "The probe expects the first mystery-drink option to be unaffordable.");
            bool refused = false;
            try
            {
                broke.Submit(new EventChoiceCommand(poor.Id, 0));
            }
            catch (InvalidOperationException)
            {
                refused = true;
            }

            Assert(refused, "An unaffordable event option must be refused.");
            Assert(
                broke.State.Phase == MatchPhase.AwaitEvent,
                "A refused option must leave the event open.");
            broke.Submit(new EventChoiceCommand(poor.Id, -1));

            Console.WriteLine(
                "[event] mystery drink granted, walk-away and unaffordable options verified");
        }

        /// <summary>
        /// 事件给的战斗增益（神秘饮料，R-EVT-002）：护盾百分比在开局真的折成护盾，
        /// 且只作用于拿到增益的一方。这里直接走战斗层的公开 API。
        /// </summary>
        private static void CheckCombatBonus()
        {
            CardDefinition crowbar = CardDatabase.Instance.Get("crowbar");
            CombatBonus bonus = new CombatBonus(50, 2, 3);
            int maxHealth = BattleConfig.GetMaxHealth(1);

            BattleSnapshot battle = new BattleSnapshot(
                new CombatantSnapshot(
                    0,
                    1,
                    new[] { new CardInstance(1, crowbar) },
                    null,
                    bonus),
                new CombatantSnapshot(
                    1,
                    1,
                    new[] { new CardInstance(2, crowbar) }));

            CombatSimulation simulation = new CombatSimulation(battle, 777UL);

            Assert(
                bonus.ShieldFor(maxHealth) == maxHealth / 2,
                "The shield bonus must scale with maximum health.");
            Assert(
                simulation.Events.History.OfType<ShieldGainedEvent>().Any(
                    gained => gained.PlayerId == 0 && gained.Amount == maxHealth / 2),
                "A starting shield bonus must be granted when the battle begins.");
            Assert(
                !simulation.Events.History.OfType<ShieldGainedEvent>().Any(
                    gained => gained.PlayerId == 1),
                "The opponent must not inherit the player's shield bonus.");

            CombatResult result = simulation.RunToCompletion();
            Assert(result != null, "A battle with a starting shield must still finish.");

            Console.WriteLine(
                "[combatbonus] shield=" + (maxHealth / 2).ToString()
                + " of " + maxHealth.ToString());
        }

        private static MatchController FindRollingController(MapGraph graph, int roll)
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                MatchController controller = new MatchController(graph, seed);
                controller.Submit(new RollCommand(controller.State.CurrentPlayer.Id));
                if (controller.State.LastRoll == roll)
                {
                    return controller;
                }
            }

            return null;
        }

        /// <summary>
        /// 力直格（ForceStraight）不再给岔路选择，预测落点按「直行优先」走完剩余步数。
        /// </summary>
        private static void CheckStraightOnlyAndPrediction()
        {
            MapGraph Build(bool straightOnly)
            {
                MapNode[] nodes =
                {
                    new MapNode(0, TileType.Home, 0, new MapCell(0, 0), 1, 3),
                    new MapNode(1, TileType.Home, 1, new MapCell(0, 1), straightOnly, 0, 2, 4),
                    new MapNode(2, TileType.Home, 2, new MapCell(0, 2), 1, 3),
                    new MapNode(3, TileType.Home, 3, new MapCell(0, 3), 2, 0),
                    new MapNode(4, TileType.Neutral, -1, new MapCell(1, 1), 1),
                };

                return MapGraph.Create(nodes, playerCount: 4);
            }

            MapGraph open = Build(false);
            Assert(
                open.LegalNextNodes(1, 0).Count == 2,
                "A normal junction must offer the straight edge and the right turn.");

            MapGraph forced = Build(true);
            IReadOnlyList<int> legal = forced.LegalNextNodes(1, 0);
            Assert(
                legal.Count == 1 && legal[0] == 2,
                "A straight-only tile must keep only the straight edge, not the right turn.");

            int landing = MovementSolver.PredictLanding(forced, 0, 3, 3);
            Assert(
                landing == 3,
                "Predicting three steps across a straight-only tile must land on node 3, got "
                    + landing.ToString() + ".");

            Console.WriteLine("[straightonly] junction keeps straight only, predicted landing=3");
        }

        private static void CheckSceneMapTeleport()
        {
            MapGraph graph = CreateSceneMapGraph();
            int warps = graph.Nodes.Count(node => node.TileType == TileType.Warp);
            Assert(warps >= 2, "The scene map must contain at least two Warp tiles.");
            Assert(
                graph.Nodes.All(node => node.OutEdges.Count >= 1),
                "Every scene tile needs at least one outgoing edge (R-MAP-003).");

            int junctions = graph.Nodes.Count(node => node.OutEdges.Count >= 3);
            Console.WriteLine("[scene] junctions=" + junctions.ToString());
            Assert(
                junctions >= 4,
                "The scene map needs three-way junctions for the fork UI, found "
                    + junctions.ToString() + ".");

            // R-MAP-002：走廊（只有前后两格）只有一个前进方向，且永远不是原路。
            foreach (MapNode node in graph.Nodes.Where(n => n.OutEdges.Count == 2))
            {
                foreach (int entry in node.OutEdges)
                {
                    IReadOnlyList<int> forward = graph.LegalNextNodes(node.Id, entry);
                    Assert(
                        forward.Count == 1,
                        "A corridor tile must have exactly one legal way forward.");
                    Assert(
                        forward[0] != entry,
                        "A corridor tile must not offer the way back.");
                }
            }

            foreach (MapNode junction in graph.Nodes.Where(n => n.OutEdges.Count >= 3))
            {
                foreach (int entry in junction.OutEdges)
                {
                    MapNode from = graph.GetNode(entry);
                    int dr = junction.GridRow - from.GridRow;
                    int dc = junction.GridColumn - from.GridColumn;
                    IReadOnlyList<int> legal = graph.LegalNextNodes(junction.Id, entry);
                    Assert(
                        legal.Count >= 1,
                        "A junction must always leave at least one legal way forward.");
                    Assert(
                        legal.All(edge => edge != entry),
                        "A junction must not offer the way back.");

                    Assert(
                        legal.Count >= 1,
                        "A junction must always leave at least one legal way forward.");
                    foreach (int next in legal)
                    {
                        MapNode target = graph.GetNode(next);
                        int stepRow = target.GridRow - junction.GridRow;
                        int stepColumn = target.GridColumn - junction.GridColumn;
                        bool straight = stepRow == dr && stepColumn == dc;
                        bool right = stepRow == dc && stepColumn == -dr;
                        Assert(
                            straight || right,
                            "A junction only allows going straight or turning right.");
                    }
                }
            }


            Dictionary<int, TileType> types = graph.Nodes
                .ToDictionary(node => node.Id, node => node.TileType);
            int teleports = 0;

            for (ulong seed = 1; seed <= 40; seed++)
            {
                MatchController controller = new MatchController(graph, seed);
                int start = controller.Events.History.Count;
                SimpleMatchAutoplayer.Run(controller, maximumTurns: 400);

                Dictionary<int, int> lastCell = new Dictionary<int, int>();
                foreach (GameEvent gameEvent in controller.Events.History.Skip(start))
                {
                    if (gameEvent is PlayerMovedEvent moved)
                    {
                        lastCell[moved.PlayerId] = moved.ToNodeId;
                        continue;
                    }

                    if (gameEvent is TurnStartedEvent started)
                    {
                        lastCell.Remove(started.PlayerId);
                        continue;
                    }

                    if (!(gameEvent is PlayerTeleportedEvent teleport))
                    {
                        continue;
                    }

                    Assert(
                        lastCell.TryGetValue(teleport.PlayerId, out int landed) &&
                            landed == teleport.FromNodeId,
                        "A teleport must fire from the tile the move just landed on.");
                    teleports++;

                    Assert(
                        types[teleport.FromNodeId] == TileType.Warp,
                        "A teleport may only start on a Warp tile, was on "
                            + types[teleport.FromNodeId].ToString() + ".");
                    Assert(
                        types[teleport.ToNodeId] == TileType.Warp,
                        "A teleport target must be a Warp tile.");
                    Assert(
                        teleport.FromNodeId != teleport.ToNodeId,
                        "A teleport must move to another Warp tile.");
                }
            }

            Assert(teleports > 0, "Expected the scene map run to produce teleports.");
            Console.WriteLine("[scene] teleports=" + teleports.ToString());
        }

        private static MapGraph CreateSceneMapGraph()
        {
            SceneCell[] cells =
            {
            new SceneCell(0, 0, TileType.Home, 0),
            new SceneCell(0, 1, TileType.Reward, 0),
            new SceneCell(0, 2, TileType.Monster, 0),
            new SceneCell(0, 3, TileType.Reward, 0),
            new SceneCell(0, 4, TileType.Monster, 0),
            new SceneCell(0, 5, TileType.Event, 0),
            new SceneCell(0, 6, TileType.Reward, 0),
            new SceneCell(0, 7, TileType.Monster, 0),
            new SceneCell(0, 8, TileType.Event, 0),
            new SceneCell(0, 9, TileType.Warp, 0),
            new SceneCell(0, 10, TileType.Event, 0),
            new SceneCell(0, 11, TileType.Home, 1),
            new SceneCell(1, 0, TileType.Monster, 0),
            new SceneCell(1, 4, TileType.Neutral, 0),
            new SceneCell(1, 11, TileType.Reward, 0),
            new SceneCell(2, 0, TileType.Warp, 0),
            new SceneCell(2, 4, TileType.Neutral, 0),
            new SceneCell(2, 11, TileType.Event, 0),
            new SceneCell(3, 0, TileType.Event, 0),
            new SceneCell(3, 4, TileType.Neutral, 0),
            new SceneCell(3, 11, TileType.Reward, 0),
            new SceneCell(4, 0, TileType.Reward, 0),
            new SceneCell(4, 4, TileType.Shop, 0),
            new SceneCell(4, 5, TileType.Neutral, 0),
            new SceneCell(4, 6, TileType.Neutral, 0),
            new SceneCell(4, 7, TileType.Shop, 0),
            new SceneCell(4, 8, TileType.Neutral, 0),
            new SceneCell(4, 9, TileType.Neutral, 0),
            new SceneCell(4, 10, TileType.Neutral, 0),
            new SceneCell(4, 11, TileType.Monster, 0),
            new SceneCell(5, 0, TileType.Event, 0),
            new SceneCell(5, 4, TileType.Neutral, 0),
            new SceneCell(5, 7, TileType.Neutral, 0),
            new SceneCell(5, 11, TileType.Monster, 0),
            new SceneCell(6, 0, TileType.Warp, 0),
            new SceneCell(6, 4, TileType.Neutral, 0),
            new SceneCell(6, 7, TileType.Neutral, 0),
            new SceneCell(6, 11, TileType.Reward, 0),
            new SceneCell(7, 0, TileType.Event, 0),
            new SceneCell(7, 1, TileType.Neutral, 0),
            new SceneCell(7, 2, TileType.Neutral, 0),
            new SceneCell(7, 3, TileType.Neutral, 0),
            new SceneCell(7, 4, TileType.Shop, 0),
            new SceneCell(7, 5, TileType.Neutral, 0),
            new SceneCell(7, 6, TileType.Neutral, 0),
            new SceneCell(7, 7, TileType.Shop, 0),
            new SceneCell(7, 11, TileType.Event, 0),
            new SceneCell(8, 0, TileType.Monster, 0),
            new SceneCell(8, 7, TileType.Neutral, 0),
            new SceneCell(8, 11, TileType.Monster, 0),
            new SceneCell(9, 0, TileType.Monster, 0),
            new SceneCell(9, 7, TileType.Neutral, 0),
            new SceneCell(9, 11, TileType.Event, 0),
            new SceneCell(10, 0, TileType.Event, 0),
            new SceneCell(10, 7, TileType.Neutral, 0),
            new SceneCell(10, 11, TileType.Event, 0),
            new SceneCell(11, 0, TileType.Home, 3),
            new SceneCell(11, 1, TileType.Warp, 0),
            new SceneCell(11, 2, TileType.Reward, 0),
            new SceneCell(11, 3, TileType.Monster, 0),
            new SceneCell(11, 4, TileType.Event, 0),
            new SceneCell(11, 5, TileType.Monster, 0),
            new SceneCell(11, 6, TileType.Event, 0),
            new SceneCell(11, 7, TileType.Reward, 0),
            new SceneCell(11, 8, TileType.Reward, 0),
            new SceneCell(11, 9, TileType.Monster, 0),
            new SceneCell(11, 10, TileType.Event, 0),
            new SceneCell(11, 11, TileType.Home, 2),
            };

            const int gridSize = 12;
            Dictionary<int, SceneCell> byId = new Dictionary<int, SceneCell>();
            foreach (SceneCell cell in cells)
            {
                byId[cell.Row * gridSize + cell.Column] = cell;
            }

            int[] rowOffsets = { -1, 0, 1, 0 };
            int[] columnOffsets = { 0, 1, 0, -1 };
            List<MapNode> nodes = new List<MapNode>();
            List<int> warpIds = new List<int>();

            foreach (SceneCell cell in cells)
            {
                int id = cell.Row * gridSize + cell.Column;
                List<int> outgoing = new List<int>();
                for (int index = 0; index < 4; index++)
                {
                    int row = cell.Row + rowOffsets[index];
                    int column = cell.Column + columnOffsets[index];
                    if (row < 0 || row >= gridSize || column < 0 || column >= gridSize)
                    {
                        continue;
                    }

                    if (!byId.TryGetValue(row * gridSize + column, out SceneCell neighbour))
                    {
                        continue;
                    }

                    outgoing.Add(neighbour.Row * gridSize + neighbour.Column);
                }

                outgoing.Sort();
                int owner = cell.TileType == TileType.Home ? cell.OwnerPlayerId : -1;
                nodes.Add(
                    new MapNode(
                        id,
                        cell.TileType,
                        owner,
                        new MapCell(cell.Row, cell.Column),
                        outgoing.ToArray()));
                if (cell.TileType == TileType.Warp)
                {
                    warpIds.Add(id);
                }
            }

            return MapGraph.Create(
                nodes,
                playerCount: 4,
                teleportGroups: new[] { warpIds.ToArray() });
        }

        private static void CheckM3MatchReport()
        {
            ulong[] seeds = { 777UL, 9091UL, 20261031UL, 424242UL };
            int shardChoices = 0;
            int winChoices = 0;
            int playerBattles = 0;
            int closedMatches = 0;

            foreach (ulong seed in seeds)
            {
                MatchController controller = new MatchController(CreateM3Graph(), seed);

                using (MatchStatistics statistics = MatchStatistics.Attach(controller))
                {
                    MatchAutomationReport report =
                        SimpleMatchAutoplayer.Run(controller, maximumTurns: 4000);

                    Assert(report.Finished, $"Seed {seed} must reach a winner.");
                    Assert(
                        report.WinnerPlayerId >= 0 && report.WinnerPlayerId < 4,
                        $"Seed {seed} must produce a valid winner seat.");
                    Assert(
                        controller.State.Players.Any(player => player.Level >= 6),
                        $"Seed {seed} winner must reach level 6.");
                    Assert(
                        statistics.Turns == statistics.Players.Sum(player => player.Turns),
                        $"Seed {seed} per-seat turn counters must add up.");
                    Assert(
                        statistics.ShardCurveByRound().Count >= 3,
                        $"Seed {seed} must sample the Shard curve once per round.");
                    Assert(
                        statistics.Players.Any(player => player.BattlesFought > 0),
                        $"Seed {seed} must include combat.");
                    Assert(
                        statistics.ToReport().Contains("M3 match report"),
                        "The statistics report must render.");

                    shardChoices += statistics.NormaChosenCount(NormaType.Shard);
                    winChoices += statistics.NormaChosenCount(NormaType.Wins);
                    playerBattles +=
                        statistics.Players.Sum(player => player.PlayerBattlesFought) / 2;

                    if (statistics.Finished)
                    {
                        closedMatches++;
                    }
                }
            }

            Assert(closedMatches == seeds.Length, "Every seed must close out.");
            Assert(shardChoices > 0, "The Shard Norma route must be exercised.");
            Assert(winChoices > 0, "The Win Norma route must be exercised.");
            Assert(
                playerBattles > 0,
                "The ambush policy must trigger player-versus-player fights.");
        }

        private static void CheckCpuAi()
        {
            // --- personalities are deterministic and distinct per seat ---
            HashSet<CpuArchetype> archetypes = new HashSet<CpuArchetype>();
            for (int seat = 0; seat < 4; seat++)
            {
                CpuPersonality personality = CpuPersonality.ForSeat(seat, CpuDifficulty.Normal, 4);
                archetypes.Add(personality.Archetype);
                Assert(
                    Math.Abs(personality.Aggression - 1.0) + Math.Abs(personality.Economy - 1.0) > 0.05,
                    "Every archetype must depart from neutral weights.");
            }

            Assert(archetypes.Count == 4, "Four seats must receive four distinct archetypes.");

            CpuPersonality normal = CpuPersonality.Create(CpuArchetype.Hunter, CpuDifficulty.Normal);
            CpuPersonality easy = CpuPersonality.Create(CpuArchetype.Hunter, CpuDifficulty.Easy);
            CpuPersonality hard = CpuPersonality.Create(CpuArchetype.Hunter, CpuDifficulty.Hard);
            Assert(
                easy.Noise > normal.Noise && hard.Noise < normal.Noise,
                "Difficulty must scale decision noise in the expected direction.");
            Assert(
                hard.Aggression >= normal.Aggression && easy.Aggression <= normal.Aggression,
                "Difficulty must sharpen or blur the same weight instead of changing it arbitrarily.");

            // --- information set: the CPU cannot reach hidden state at all ---
            AssertCpuInformationSetIsClosed();

            // --- three full CPU matches on the stand-in board ---
            ulong[] seeds = { 424242UL, 9091UL, 20261031UL };
            int closed = 0;
            foreach (ulong seed in seeds)
            {
                MatchController controller = new MatchController(CreateM3Graph(), seed);
                CpuMatchRunner runner = new CpuMatchRunner(controller);
                CpuMatchResult result = runner.Run();

                Assert(result.Finished, $"{seed}: a CPU match must reach a winner.");
                Assert(
                    result.WinnerPlayerId >= 0 && result.WinnerPlayerId < 4,
                    $"{seed}: the winner seat must be valid.");
                Assert(result.StallReason.Length == 0, $"{seed}: a CPU match must not stall. " + result.StallReason + " / " + result.ToReport());
                Assert(result.Decisions > 50, $"{seed}: a CPU match must take real decisions.");
                Assert(
                    result.Seats.All(seat => seat.Decisions > 0),
                    $"{seed}: every seat must act.");
                Assert(
                    result.Seats.Sum(seat => seat.NormaWins + seat.NormaShard) >= 4,
                    $"{seed}: every seat must pick a Norma route.");
                Assert(
                    result.Seats.Sum(seat => seat.Moves) > 20,
                    $"{seed}: the CPU must spend turns on movement.");
                Assert(result.ToReport().Contains("CPU match"), "The CPU report must render.");

                closed++;
            }

            Assert(closed == seeds.Length, "Every CPU match must close out.");

            // --- the four archetypes price the same move differently ---
            MatchController probe = new MatchController(CreateM3Graph(), 123456UL);
            CpuMatchRunner probeRunner = new CpuMatchRunner(probe);
            CpuObservation observation = null;
            int guard = 0;
            while (!probe.IsFinished && guard++ < 4000)
            {
                if (probe.State.Phase == MatchPhase.AwaitMove && probe.CurrentCandidates.Count > 0)
                {
                    observation = CpuObservation.Capture(probe);
                    break;
                }

                string failure;
                if (!probeRunner.Step(out failure))
                {
                    break;
                }
            }

            Assert(observation != null, "A CPU match must reach a movement decision.");
            Assert(observation.Routes.Count > 0, "A movement decision must expose at least one route.");
            Assert(
                observation.DescribeInformationSet().Contains("背包"),
                "The observation dump must describe the observer's own bag.");

            CpuBrain aggressive = new CpuBrain(
                observation.PlayerId,
                CpuPersonality.Create(CpuArchetype.Aggressive),
                7UL,
                enableNoise: false);
            CpuBrain guardian = new CpuBrain(
                observation.PlayerId,
                CpuPersonality.Create(CpuArchetype.Guardian),
                7UL,
                enableNoise: false);

            double largestGap = 0.0;
            foreach (CpuRoute route in observation.Routes)
            {
                double first = aggressive.ScoreRouteForDiagnostics(observation, route);
                double second = guardian.ScoreRouteForDiagnostics(observation, route);
                largestGap = Math.Max(largestGap, Math.Abs(first - second));
            }

            Assert(
                largestGap > 0.05,
                "An aggressive and a guardian CPU must price the same route differently.");

            CpuDecision decision = aggressive.Decide(observation);
            Assert(decision.Action == CpuAction.Move, "A movement phase must yield a move decision.");
            Assert(
                decision.ForkDirections.Length <= observation.Routes.Max(route => route.ForkDirections.Length),
                "The CPU must never invent fork choices.");

            Console.WriteLine(
                $"[cpu] matches={seeds.Length} probeRoutes={observation.Routes.Count} gap={largestGap:0.00}");
        }

        /// <summary>
        /// Reflection guard for R-CPU-002: no public CPU type may expose the controller, the live
        /// state, or another player's bag cards.
        /// </summary>
        private static void AssertCpuInformationSetIsClosed()
        {
            System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

            string[] opponentProperties =
            {
                "PlayerId", "Level", "Shards", "Wins", "CurrentNode", "Norma",
                "BattleWidth", "BagWidth", "BagCount", "BattleCards"
            };

            foreach (System.Reflection.PropertyInfo property in typeof(CpuOpponent).GetProperties(flags))
            {
                Assert(
                    opponentProperties.Contains(property.Name),
                    "CpuOpponent must not expose " + property.Name + ".");
            }

            Type[] forbidden =
            {
                typeof(MatchController),
                typeof(MatchState),
                typeof(MatchPlayerState)
            };

            foreach (System.Reflection.PropertyInfo property in typeof(CpuObservation).GetProperties(flags))
            {
                Assert(
                    !forbidden.Contains(property.PropertyType),
                    "CpuObservation must not expose " + property.PropertyType.Name + ".");
            }

            foreach (System.Reflection.MethodInfo method in typeof(CpuBrain).GetMethods(flags))
            {
                if (method.DeclaringType != typeof(CpuBrain))
                {
                    continue;
                }

                foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
                {
                    Assert(
                        !forbidden.Contains(parameter.ParameterType),
                        "CpuBrain." + method.Name + " must not accept " + parameter.ParameterType.Name + ".");
                }
            }

            Assert(
                typeof(CpuBrain).GetConstructors().All(constructor =>
                    constructor.GetParameters().All(parameter => !forbidden.Contains(parameter.ParameterType))),
                "CpuBrain must be buildable without a live controller.");
        }

        private static MapGraph CreateM3Graph()
        {
            // Stand-in for the 12x12 scene board: 24 nodes, one ring, the tile mix of
            // 规则书 §1.5 (no Trap: its effect is still undefined in the rule book).
            TileType[] layout =
            {
                TileType.Home, TileType.Neutral, TileType.Monster, TileType.Reward,
                TileType.Shop, TileType.Neutral, TileType.Home, TileType.Neutral,
                TileType.Monster, TileType.Reward, TileType.Shop, TileType.Neutral,
                TileType.Home, TileType.Neutral, TileType.Monster, TileType.Reward,
                TileType.Shop, TileType.Neutral, TileType.Home, TileType.Neutral,
                TileType.Reward, TileType.Reward, TileType.Neutral, TileType.Neutral
            };
            int[] homeOwners = { 0, 1, 2, 3 };
            int homeIndex = 0;
            MapNode[] nodes = new MapNode[layout.Length];

            for (int index = 0; index < layout.Length; index++)
            {
                bool isHome = layout[index] == TileType.Home;
                nodes[index] = new MapNode(
                    index,
                    layout[index],
                    isHome ? homeOwners[homeIndex++] : -1,
                    (index + 1) % layout.Length);
            }

            return MapGraph.Create(nodes, playerCount: 4);
        }

        private static MapGraph CreateTestGraph()
        {
            return MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Warp, -1, 2, 5),
                    new MapNode(2, TileType.Home, 1, 3),
                    new MapNode(3, TileType.Warp, -1, 4),
                    new MapNode(4, TileType.Home, 2, 5),
                    new MapNode(5, TileType.Warp, -1, 6),
                    new MapNode(6, TileType.Home, 3, 7),
                    new MapNode(7, TileType.Warp, -1, 0)
                },
                playerCount: 4,
                teleportGroups: new[] { new[] { 1, 3 }, new[] { 5, 7 } });
        }

        private static ulong FingerprintEvents(IEnumerable<GameEvent> events)
        {
            ulong hash = StableHash.Begin();
            foreach (GameEvent gameEvent in events)
            {
                hash = StableHash.Add(hash, gameEvent.GetType().Name);
                hash = StableHash.Add(hash, gameEvent.Tick);
                hash = StableHash.Add(hash, gameEvent.Sequence);

                if (gameEvent is CardCastEvent cast)
                {
                    hash = StableHash.Add(hash, cast.PlayerId);
                    hash = StableHash.Add(hash, cast.Column);
                    hash = StableHash.Add(hash, cast.CardUid);
                }
                else if (gameEvent is DamageDealtEvent damage)
                {
                    hash = StableHash.Add(hash, damage.SourcePlayerId);
                    hash = StableHash.Add(hash, damage.TargetPlayerId);
                    hash = StableHash.Add(hash, damage.Amount);
                    hash = StableHash.Add(hash, damage.ShieldAbsorbed);
                    hash = StableHash.Add(hash, damage.IgnoresShield ? 1L : 0L);
                }
                else if (gameEvent is StatusAppliedEvent status)
                {
                    hash = StableHash.Add(hash, status.PlayerId);
                    hash = StableHash.Add(hash, (long)status.Status);
                    hash = StableHash.Add(hash, status.Layers);
                }
                else if (gameEvent is CombatEndedEvent end)
                {
                    hash = StableHash.Add(hash, end.WinnerPlayerId);
                    hash = StableHash.Add(hash, (long)end.Reason);
                }
            }

            return hash;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
