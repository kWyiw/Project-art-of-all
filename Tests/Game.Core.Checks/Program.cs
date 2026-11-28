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
                CheckCardCatalogMetadata();
                CheckCardKeywords();
                CheckCardText();
                CheckLoadoutPreview();
                CheckCombatSimulation();
                CheckCombatTimeline();
                CheckSlotAdjacencyInCombat();
                CheckCustomMaxHealth();
                CheckCardMechanics();
                CheckSecondSetMechanics();
                CheckCardStrictnessFixes();
                CheckM1MatchDurability();
                CheckRandomness();
                CheckMonsterPhases();
                CheckBossPhase();
                CheckDebugRollRange();
                CheckHomeProgression();
                CheckRewardChoice();
                CheckWarpTeleport();
                CheckCardBandCommands();
                CheckCardFusion();
                CheckDeveloperGrantCard();
                CheckBagCapacity();
                CheckFreeActionsDuringAnotherTurn();
                CheckSceneMapTeleport();
                CheckSweetRingMovementRules();
                CheckOpeningGrant();
                CheckFirstMoveFromHome();
                CheckEarlyHomeStop();
                CheckPassThroughOpponent();
                CheckMatchEvent();
                CheckEventCardMechanics();
                CheckBoardTraps();
                CheckCombatBonus();
                CheckStraightOnlyAndPrediction();
                CheckShopTile();
                CheckCpuAi();
                CheckCpuRewardFitRegression();
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

            Assert(
                MovementSolver.PredictLanding(
                    graph,
                    startNodeId: 0,
                    previousNodeId: -1,
                    steps: 2) == 2,
                "Normal route prediction may pass through a Warp tile.");
            Assert(
                MovementSolver.PredictLanding(
                    graph,
                    startNodeId: 0,
                    previousNodeId: -1,
                    steps: 2,
                    stopAtWarp: true) == 1,
                "Fork previews must stop on the Warp tile instead of showing a later landing.");
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
            Assert(CardText.Description(crowbar).Contains("主动：造成1D6伤害。"),
                "Crowbar must quote the design text verbatim.");

            CardDefinition snake = CardDatabase.Instance.Get("ouroboros");
            Assert(CardText.Description(snake).Contains("100") &&
                   CardText.Description(snake).Contains("数值"),
                "自噬之蛇 must read as a +100 value aura.");

            CardDefinition fire = CardDatabase.Instance.Get("campfire");
            Assert(CardText.Description(fire).Contains("多重释放"),
                "Campfire must quote its design text (multi-cast aura).");

            CardDefinition tape = CardDatabase.Instance.Get("duct_tape");
            Assert(CardText.Description(tape).Contains("被动"),
                "Duct tape must describe its passive.");

            // 卡面不写效果，正文只在悬停窗里显示：设计稿原文逐行照抄，升级版取斜杠后的数值。
            CardDefinition lighterBase = CardDatabase.Instance.Get("lighter");
            CardDefinition lighterPlus = CardDatabase.Instance.Get("lighter+");
            Assert(CardDesignText.Lines(lighterBase).Count == 1 &&
                   CardText.Description(lighterBase).Contains("1D4") &&
                   CardText.Description(lighterPlus).Contains("1d6"),
                "Design text must resolve the slash per card tier.");

            // 设计稿里整行以「（」开头的括注是写给实现者的解释（举例子、规则示例），
            // 不进玩家看的正文；词条含义交给悬停窗底部的词条区。
            CardDefinition cocoon = CardDatabase.Instance.Get("nuclear_water");
            Assert(CardDesignText.RawLines(cocoon).Count == 2 &&
                   CardDesignText.RawLines(cocoon)[1].StartsWith("（中毒：", StringComparison.Ordinal),
                "The design notes must still be kept verbatim in the raw table.");
            Assert(CardDesignText.Lines(cocoon).Count == 1 &&
                   !CardText.Description(cocoon).Contains("（", StringComparison.Ordinal),
                "Explanatory parenthetical lines must not reach the player-facing text.");

            CardDefinition campfireCard = CardDatabase.Instance.Get("campfire");
            Assert(!CardText.Description(campfireCard).Contains("例如你上场的牌是", StringComparison.Ordinal),
                "Worked examples from the design document must not be printed.");

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

            // 自噬之蛇：右侧相邻牌的结算数值 +100。
            CardInstance snake = new CardInstance(4, CardDatabase.Instance.Get("ouroboros"));
            CardInstance[] snakePair = { snake, crowbar };
            Assert(LoadoutPreview.ValueBonus(snakePair, 1) == 100,
                "自噬之蛇右侧的牌必须拿到 +100 数值。");
            Assert(LoadoutPreview.ValueBonus(new[] { crowbar, snake }, 0) == 0,
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

            CardInstance weaponCounter =
                new CardInstance(9, CardDatabase.Instance.Get("placeholder_3"));
            Assert(
                LoadoutPreview.DiceModifier(new[] { weaponCounter, crowbar, axe }, 0) == 20,
                "占位符3的构筑预览必须按其他武器数量显示 +10。");
        }

        private static void CheckCardDatabase()
        {
            CardDatabase catalog = CardDatabase.Instance;
            // 54 张基础牌 + 其中 44 张各自的「+」升级版。
            Assert(catalog.All.Count == 54,
                "The catalog must expose all fifty-four base cards.");
            Assert(catalog.All.All(card => !card.IsUpgraded),
                "The shop / reward pool must never list upgraded cards.");
            Assert(catalog.AllDefinitions.Count == 98,
                "Every upgradable base card must ship its upgraded twin.");

            // 商店 / 奖励的随机池直接取 catalog.All，只滤掉事件专属牌；任何新牌掉队都会在这里炸。
            foreach (CardDefinition card in catalog.All)
            {
                bool excluded = MatchEventCatalog.IsEventOnlyCard(card.Id);
                Assert(
                    !excluded ||
                    card.Id == MatchEventCatalog.MysteryDrinkCardId ||
                    card.Id == MatchEventCatalog.MurderWeaponCardId ||
                    card.Id == MatchEventCatalog.BiovirusCardId ||
                    card.Id == MatchEventCatalog.MotherCardId,
                    $"Only event-limited cards may stay out of the shop / reward pool (got {card.Id}).");
            }
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
            Assert(
                catalog.AllDefinitions.Select(card => card.Id).Distinct().Count() ==
                catalog.AllDefinitions.Count,
                "Card ids must be unique.");

            // 融合升级（CJ-5.11）：每张升级牌都指回自己的基础牌，基础牌反过来指向它。
            CardDefinition lighterPlus = catalog.Get("lighter+");
            Assert(lighterPlus != null && lighterPlus.IsUpgraded,
                "打火机 must ship a 打火机+ upgrade.");
            Assert(lighterPlus.BaseId == "lighter" && lighterPlus.DisplayName == "打火机+",
                "The upgrade must keep its base id and grow a plus sign.");
            Assert(ReferenceEquals(catalog.Get("lighter").Upgraded, lighterPlus),
                "The base card must point at its upgraded twin.");
            Assert(catalog.Get("snow_purifier").Upgraded == null,
                "雪水净化器 must have no upgrade (its value is the 淡水 it produces).");
            Assert(catalog.Get("mystery_drink").Upgraded == null,
                "神秘饮料 must have no upgrade (it is a sellable event card).");
            Assert(catalog.Get("recycler+") != null && catalog.Get("recycler+").IsUpgraded,
                "回收站 must ship 回收站+ (its sell bonus doubles).");
            Assert(catalog.Get("rusted_blade+") != null,
                "锈刃 must ship its stronger poison upgrade.");
            Assert(catalog.Get("six_six_streak").HasTag(CardTag.Mascot),
                "六六大顺 must carry the Mascot tag.");
            Assert(catalog.Get("food_poisoning").HasTag(CardTag.Disease),
                "食物中毒 must carry the Disease tag.");
            Assert(
                catalog.Get(MatchEventCatalog.MysteryDrinkCardId).HasTag(CardTag.Event) &&
                catalog.Get(MatchEventCatalog.MysteryDrinkCardId).Value == 1,
                "神秘饮料 must be a 1-value event card.");
            Assert(
                catalog.Get(MatchEventCatalog.MurderWeaponCardId).HasTag(CardTag.Event) &&
                catalog.Get(MatchEventCatalog.MurderWeaponCardId).Auras.Any(
                    aura => aura.Scaling == AuraScaling.PerOwnerWins),
                "凶器 must be an event weapon with win scaling.");
            Assert(
                catalog.Get(MatchEventCatalog.BiovirusCardId).CannotSell &&
                catalog.Get(MatchEventCatalog.MotherCardId).CannotSell,
                "Biovirus and 母体 must not be sellable.");
            Assert(catalog.Get("placeholder_2").Upgraded != null,
                "占位符2 must upgrade from right-side to adjacent lifesteal.");
            Assert(CardText.Description(catalog.Get("titan")).Contains("2d100"),
                "泰坦 design text must remain visible in the tooltip.");

            // 棋盘层被动必须写在卡面上（BoardNote），否则玩家会以为这些牌没有效果。
            Assert(CardText.Description(catalog.Get("fortune_cookie")).Contains("最左侧的食物"),
                "幸运饼干的卡面必须说明它给最左侧食物加永久修正。");
            Assert(CardText.Description(catalog.Get("barbed_wire")).Contains("陷阱"),
                "铁丝网的卡面必须说明出售时留陷阱。");
            Assert(!CardText.Description(catalog.Get("fortune_cookie")).Contains("被出售时"),
                "出售类被动不该再重复成一行「被出售时 → 数值自身」，棋盘层由 BoardNote 描述。");

            // 这些是摆在战斗栏里的装备（食物相当于带回复的防具），不是一次性消耗品：
            // 牌是永久资产（CJ-1.2），效果只在战斗中结算，背包里不该有回血、回再生这类效果。
            foreach (string id in new[]
                     {
                         "fresh_water", "mineral_water", "chocolate", "hardtack", "baguette"
                     })
            {
                Assert(catalog.Get(id).Scope == CardScope.BattleOnly,
                    string.Concat(id, " is battle gear and must only work in battle."));
            }

            foreach (string id in new[]
                     {
                         "whetstone", "duct_tape", "special_rubber", "ouroboros"
                     })
            {
                Assert(catalog.Get(id).Scope == CardScope.BattleOnly,
                    string.Concat(id, " has only combat effects and must be BattleOnly."));
            }

            Assert(
                catalog.Get("mystery_drink").Scope == CardScope.BagAndBattle,
                "Mystery drink is an event card that can be sold from either row.");
            foreach (string id in new[]
                     {
                         "recycler", "snow_purifier", "dorayaki", "golden_chariot",
                         "six_six_streak", "placeholder_7", "placeholder_1"
                     })
            {
                Assert(catalog.Get(id).Scope == CardScope.BagAndBattle,
                    string.Concat(id, " has a board-layer economy passive that works from the bag."));
            }
        }

        private static void CheckCardCatalogMetadata()
        {
            IReadOnlyList<CardDefinition> all = CardCatalogMetadata.All;
            Assert(
                all.Count == CardDatabase.Instance.AllDefinitions.Count &&
                all.Count == 98,
                "The codex catalog must include all base and upgraded definitions.");
            Assert(
                all.Count(card =>
                    CardCatalogMetadata.AcquisitionOf(card) ==
                    CardUnlockKind.FusionUpgrade) == 44,
                "The codex must classify all 44 upgraded cards as fusion upgrades.");
            Assert(
                all.Count(card =>
                    CardCatalogMetadata.AcquisitionOf(card) ==
                    CardUnlockKind.EventLimited) == 4,
                "The codex must classify all four event cards as event-limited.");
            Assert(
                all.Count(card =>
                    CardCatalogMetadata.AcquisitionOf(card) ==
                    CardUnlockKind.Normal) == 50,
                "The remaining 50 base cards are normal acquisitions.");

            IReadOnlyList<CardDefinition> eventCards = CardCatalogMetadata.Filter(
                acquisition: CardUnlockKind.EventLimited);
            Assert(
                eventCards.Count == 4 &&
                eventCards.Any(card => card.Id == MatchEventCatalog.MysteryDrinkCardId) &&
                eventCards.Any(card => card.Id == MatchEventCatalog.MurderWeaponCardId) &&
                eventCards.Any(card => card.Id == MatchEventCatalog.BiovirusCardId) &&
                eventCards.Any(card => card.Id == MatchEventCatalog.MotherCardId),
                "Event-limited filter must return all event cards.");
            Assert(
                CardCatalogMetadata.Filter(slotSize: 3).All(card => card.SlotSize == 3),
                "Size filter must only return matching slot costs.");
            Assert(
                CardCatalogMetadata.Filter(type: CardTag.Disease).Any(
                    card => card.Id == "food_poisoning"),
                "Type filter must find cards by any tag.");
            Assert(
                CardCatalogMetadata.Filter(
                    acquisition: CardUnlockKind.FusionUpgrade)
                    .Any(card => card.Id == "rusted_blade+"),
                "The catalog must expose upgraded cards individually.");

            Console.WriteLine(
                $"[codex] definitions={all.Count} normal=50 event=4 upgrades=44");
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
        /// ④ 怪物模板牌组可解析、能装下，同模板生成稳定；随机抽取由怪物子流负责。
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

            MonsterDefinition earlyMonster = MonsterRoster.For(MonsterPhase.Early)[0];
            Assert(
                DeckFingerprint(deckA.CreateEnemyDeck(earlyMonster, 5000)) ==
                DeckFingerprint(deckB.CreateEnemyDeck(earlyMonster, 5000)),
                "The same monster template must produce the same deck across matches.");

            MonsterPhase[] phases =
            {
                MonsterPhase.Early,
                MonsterPhase.Mid,
                MonsterPhase.Late
            };
            for (int phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
            {
                IReadOnlyList<MonsterDefinition> roster =
                    MonsterRoster.For(phases[phaseIndex]);
                Assert(roster.Count > 0, "Every monster phase needs a roster.");
                for (int monsterIndex = 0; monsterIndex < roster.Count; monsterIndex++)
                {
                    MonsterDefinition monster = roster[monsterIndex];
                    List<CardInstance> deck = deckA.CreateEnemyDeck(monster, 9000);
                    Assert(deck.All(card => card.Definition != null),
                        "Every monster card id must resolve in CardDatabase.");
                    Assert(deck.Select(card => card.Uid).Distinct().Count() == deck.Count,
                        "Duplicate monster cards still need unique runtime card ids.");
                    int width = deck.Sum(card => card.Definition.SlotSize);
                    Assert(width <= LoadoutRules.GetBattleCapacity(monster.LoadoutLevel),
                        $"{monster.DisplayName} must fit its battle row capacity.");
                }
            }

            Console.WriteLine(
                $"[rng] seeds={seeds.Count} rolls={rollsA.Count} " +
                $"sample={DeckFingerprint(deckA.CreateEnemyDeck(earlyMonster, 5000))}");
        }

        private static void CheckMonsterPhases()
        {
            MatchController controller =
                new MatchController(CreateM3Graph(), 20261128UL);
            Assert(controller.State.MonsterPhase == MonsterPhase.Early,
                "A fresh match must start in the early monster phase.");

            controller.State.Players[0].Level = 2;
            Assert(controller.State.MonsterPhase == MonsterPhase.Early,
                "The mid phase starts only after every player reaches Lv.2.");

            for (int index = 0; index < controller.State.Players.Count; index++)
            {
                controller.State.Players[index].Level = 2;
            }

            Assert(controller.State.MonsterPhase == MonsterPhase.Mid,
                "All players at Lv.2 must unlock the mid monster phase.");

            controller.State.Players[3].Level = 4;
            Assert(controller.State.MonsterPhase == MonsterPhase.Late,
                "Any player reaching Lv.4 must unlock the late monster phase.");

            MatchController precedence =
                new MatchController(CreateM3Graph(), 20261130UL);
            precedence.State.Players[0].Level = 4;
            Assert(precedence.State.MonsterPhase == MonsterPhase.Late,
                "Lv.4 must win even if the other players have not reached Lv.2.");

            IReadOnlyList<MonsterDefinition> early =
                MonsterRoster.For(MonsterPhase.Early);
            Assert(early.Count == 2,
                "The supplied early roster must contain 杂鱼A and 杂鱼B.");
            MonsterDefinition earlyA = early.First(monster =>
                monster.DisplayName == "杂鱼A");
            MonsterDefinition earlyB = early.First(monster =>
                monster.DisplayName == "杂鱼B");
            Assert(earlyA.MaxHealth == 50,
                "杂鱼A must have 50 HP.");
            Assert(earlyA.CardIds.SequenceEqual(
                    new[] { "crowbar", "concrete_block", "concrete_block" }),
                "杂鱼A must use 撬棍 / 混凝土块 / 混凝土块.");
            Assert(earlyB.MaxHealth == 200,
                "杂鱼B must have 200 HP.");
            Assert(earlyB.CardIds.SequenceEqual(
                    new[] { "whetstone", "duct_tape", "special_rubber" }),
                "杂鱼B must use 磨刀石 / 胶带 / 特种橡胶.");

            IReadOnlyList<MonsterDefinition> mid =
                MonsterRoster.For(MonsterPhase.Mid);
            Assert(mid.Count == 1 &&
                   mid[0].DisplayName == "大杂鱼A" &&
                   mid[0].MaxHealth == 100,
                "The supplied mid roster must contain 大杂鱼A at 100 HP.");
            Assert(mid[0].CardIds.SequenceEqual(
                    new[] { "car_door", "fireman_axe", "nail_gun" }),
                "大杂鱼A must use 汽车门板 / 消防斧 / 射钉枪.");

            IReadOnlyList<MonsterDefinition> late =
                MonsterRoster.For(MonsterPhase.Late);
            Assert(late.Count == 1 &&
                   late[0].DisplayName == "超级杂鱼A" &&
                   late[0].MaxHealth == 300,
                "The supplied late roster must contain 超级杂鱼A at 300 HP.");
            Assert(late[0].CardIds.SequenceEqual(
                    new[] { "titan", "placeholder_4", "torch", "compound" }),
                "超级杂鱼A must use 泰坦 / 占位符4 / 火炬 / 化合物.");

            Pcg32 first = new Pcg32(8801UL, 17UL);
            Pcg32 second = new Pcg32(8801UL, 17UL);
            HashSet<string> picked = new HashSet<string>();
            for (int index = 0; index < 32; index++)
            {
                MonsterDefinition a = MonsterRoster.Pick(MonsterPhase.Early, first);
                MonsterDefinition b = MonsterRoster.Pick(MonsterPhase.Early, second);
                Assert(a.Id == b.Id,
                    "Monster selection must be deterministic for the same seed.");
                picked.Add(a.Id);
            }

            Assert(picked.Count > 1,
                "The early phase must actually randomise between its monsters.");

            MapGraph encounterGraph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Monster, -1, 2),
                    new MapNode(2, TileType.Home, 1, 3),
                    new MapNode(3, TileType.Monster, -1, 4),
                    new MapNode(4, TileType.Home, 2, 5),
                    new MapNode(5, TileType.Monster, -1, 6),
                    new MapNode(6, TileType.Home, 3, 7),
                    new MapNode(7, TileType.Monster, -1, 0)
                },
                playerCount: 4);
            MatchController encounter =
                new MatchController(encounterGraph, 20261129UL);
            MatchPlayerState mover = encounter.State.CurrentPlayer;
            mover.Shards = NormaRules.GetThreshold(mover.Level, mover.Norma);
            Assert(mover.MeetsCurrentNorma(),
                "The monster gate probe must begin with an already-satisfied Norma.");
            encounter.Submit(new DebugRollCommand(mover.Id, 1));
            encounter.Submit(new MoveCommand(mover.Id));
            CombatResolvedEvent combat = encounter.Events.History
                .OfType<CombatResolvedEvent>()
                .LastOrDefault();
            Assert(combat != null,
                "Moving onto a monster tile must publish a combat result.");
            MonsterDefinition selected = early.First(monster =>
                monster.DisplayName == combat.Result.Battle.Right.DisplayName);
            Assert(combat.Result.Battle.Right.MaxHealth == selected.MaxHealth,
                "The encountered monster must use its template HP.");
            Assert(mover.Level == 1,
                "Satisfying Norma after a monster fight must not upgrade away from Home.");

            Console.WriteLine(
                $"[monster] early={early.Count} mid={mid.Count} late={late.Count} "
                + $"picked={picked.Count}");
        }

        private static void CheckBossPhase()
        {
            MapGraph graph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Home, 1, 2),
                    new MapNode(2, TileType.Monster, -1, 3),
                    new MapNode(3, TileType.Home, 2, 4),
                    new MapNode(4, TileType.Boss, -1, 5),
                    new MapNode(5, TileType.Home, 3, 6),
                    new MapNode(6, TileType.Neutral, -1, 7),
                    new MapNode(7, TileType.Monster, -1, 0)
                },
                playerCount: 4);
            MatchController controller =
                new MatchController(graph, 20261201UL);
            Assert(controller.State.MonsterPhase == MonsterPhase.Early,
                "The Boss probe must start before phase three.");
            Assert(!controller.State.IsBossNode(2) &&
                   !controller.State.IsBossNode(4),
                "Monster and legacy Boss tiles are not Boss before phase three.");

            MatchPlayerState trigger = controller.State.CurrentPlayer;
            trigger.Level = 3;
            trigger.Norma = NormaType.Shard;
            trigger.Shards = NormaRules.GetThreshold(trigger.Level, trigger.Norma);
            controller.Submit(new DebugRollCommand(trigger.Id, 1));
            controller.Submit(new MoveCommand(trigger.Id));
            Assert(trigger.Level == 4 &&
                   controller.State.MonsterPhase == MonsterPhase.Late,
                "Stopping on Home at the threshold must enter phase three.");
            Assert(controller.Events.History
                    .OfType<BossEvolutionEvent>()
                    .Count() == 1,
                "Entering phase three must publish exactly one boss phase event.");

            for (int nodeId = 0; nodeId < graph.Nodes.Count; nodeId++)
            {
                MapNode node = graph.GetNode(nodeId);
                if (!MatchState.IsMonsterTile(node.TileType))
                {
                    continue;
                }

                Assert(controller.State.IsBossNode(nodeId),
                    $"Monster tile {nodeId} must be a Boss in phase three.");
                Assert(controller.State.GetMonsterPool(nodeId) == 10,
                    $"Boss tile {nodeId} must expose a permanent 10-shard pool.");
            }

            Assert(controller.State.TakeMonsterPool(2) == 10,
                "Reading a phase-three Boss pool must award 10.");
            Assert(controller.State.GetMonsterPool(2) == 10,
                "A phase-three Boss pool must not be consumed.");

            CpuObservation observation = CpuObservation.Capture(controller);
            Assert(observation.MonsterPhase == MonsterPhase.Late,
                "CPU observations must receive the same global monster phase.");
            Assert(observation.Tiles
                    .Where(tile => MatchState.IsMonsterTile(tile.Type))
                    .All(tile => tile.IsBoss && tile.MonsterPool == 10),
                "CPU tiles must treat every phase-three monster as a Boss.");
            Console.WriteLine("[boss] phase=late pool=10 permanent=yes cpu=yes");
        }

        private static void CheckDebugRollRange()
        {
            bool rejected = false;
            try
            {
                new DebugRollCommand(0, 13);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejected = true;
            }

            Assert(rejected, "A debug movement range outside 1..12 must be rejected.");

            MatchController controller = new MatchController(CreateTestGraph(), 909UL);
            int playerId = controller.State.CurrentPlayer.Id;
            controller.Submit(new DebugRollCommand(playerId, 12));
            Assert(
                controller.State.LastRoll == 12 &&
                controller.State.Phase == MatchPhase.AwaitMove,
                "The debug range must enter the normal movement phase with value 12.");

            int eventStart = controller.Events.History.Count;
            controller.Submit(new MoveCommand(playerId));
            Assert(
                controller.Events.History
                    .Skip(eventStart)
                    .OfType<PlayerMovedEvent>()
                    .Count() == 12,
                "The debug range must execute twelve movement steps.");
            Console.WriteLine("[debugroll] range=12 steps=12");
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
            int[] shardTargets = { 15, 30, 60, 120, 240 };
            int[] winTargets = { 1, 3, 6, 12, 24 };
            for (int level = 1; level <= 5; level++)
            {
                Assert(
                    NormaRules.GetThreshold(level, NormaType.Shard) ==
                    shardTargets[level - 1],
                    "Norma shard threshold table must match the design for level "
                    + level + ".");
                Assert(
                    NormaRules.GetThreshold(level, NormaType.Wins) ==
                    winTargets[level - 1],
                    "Norma win threshold table must match the design for level "
                    + level + ".");
            }

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

            MapGraph homeGateGraph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Home, 1, 2),
                    new MapNode(2, TileType.Monster, -1, 3),
                    new MapNode(3, TileType.Home, 2, 4),
                    new MapNode(4, TileType.Neutral, -1, 5),
                    new MapNode(5, TileType.Home, 3, 6),
                    new MapNode(6, TileType.Neutral, -1, 7),
                    new MapNode(7, TileType.Neutral, -1, 0)
                },
                playerCount: 4);
            MatchController homeGate =
                new MatchController(homeGateGraph, 45UL);
            MatchPlayerState homeMover = homeGate.State.CurrentPlayer;
            homeMover.Shards = NormaRules.GetThreshold(homeMover.Level, homeMover.Norma);
            Assert(homeMover.MeetsCurrentNorma(),
                "The Home upgrade probe must begin with a satisfied Norma.");
            homeGate.Submit(new DebugRollCommand(homeMover.Id, 1));
            homeGate.Submit(new MoveCommand(homeMover.Id));
            Assert(homeMover.Level == 2 &&
                   homeGate.State.Phase == MatchPhase.AwaitNorma,
                "Stopping on HomeTile with a satisfied Norma must upgrade and request the next Norma.");
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

        /// <summary>
        /// 背包容量只算背包这一排（CJ-5.1 / CJ-6.2）：装到战斗栏上的牌不占背包空间。
        /// 旧版本用「全部持有宽度」去卡背包，4 级以后满装备的玩家背包明明是空的，
        /// 商店和奖励却一律提示「装不下」。
        /// </summary>
        /// <summary>
        /// 融合升级（CJ-5.11）：手里已有同名牌时，再获得一张不再占新格子，而是把那张牌升级。
        /// </summary>
        private static void CheckCardFusion()
        {
            MapGraph graph = CreateTestGraph();
            MatchController controller = new MatchController(graph, 21UL);
            MatchPlayerState player = controller.State.Players[0];
            CardDefinition lighter = CardDatabase.Instance.Get("lighter");

            controller.AcquireOrUpgrade(player, lighter, "Test", "no room");
            Assert(player.OwnedCards.Count == 1 &&
                   player.OwnedCards[0].Definition.Id == "lighter",
                "The first 打火机 must simply enter the bag.");

            CardInstance first = player.OwnedCards[0];
            CardInstance upgraded = controller.AcquireOrUpgrade(player, lighter, "Shop", "no room");
            Assert(ReferenceEquals(upgraded, first) && player.OwnedCards.Count == 1,
                "A second 打火机 must fuse into the one you already own.");
            Assert(first.Definition.Id == "lighter+" && first.Definition.DisplayName == "打火机+",
                "The fused card must become 打火机+.");
            Assert(
                CardText.Description(first.Definition).Contains("1d6", StringComparison.OrdinalIgnoreCase) &&
                CardText.Description(lighter).Contains("1d4", StringComparison.OrdinalIgnoreCase),
                "打火机+ must burn harder than 打火机 (1d4 -> 1d6).");

            CardUpgradedEvent upgradedEvent = controller.Events.History
                .OfType<CardUpgradedEvent>()
                .LastOrDefault();
            Assert(
                upgradedEvent != null &&
                upgradedEvent.PlayerId == player.Id &&
                upgradedEvent.BaseId == "lighter" &&
                upgradedEvent.UpgradedId == "lighter+" &&
                upgradedEvent.Source == "Shop",
                "Fusing must announce which card was upgraded and why.");

            // 已经升到顶的牌不会再融合：新来的那张只能自己占位。
            controller.AcquireOrUpgrade(player, lighter, "Shop", "no room");
            Assert(player.OwnedCards.Count == 2,
                "An already upgraded card must not fuse again.");

            // 升级后的牌在战斗里用的是升级版数值。
            Assert(
                CardDatabase.Instance.Get("crowbar+").CooldownTicks == 120 &&
                CardDatabase.Instance.Get("crowbar").CooldownTicks == 180,
                "撬棍+ must cast in 2 seconds (upgrade shortens the cooldown).");

            Console.WriteLine(
                "[fusion] lighter -> lighter+ fused in place, bag=" +
                player.OwnedCards.Count + " cost=" + first.Definition.CooldownTicks);
        }

        private static void CheckDeveloperGrantCard()
        {
            MatchController controller =
                new MatchController(CreateTestGraph(), 20261128UL);
            MatchPlayerState player = controller.State.CurrentPlayer;
            int countBefore = player.OwnedCards.Count;

            controller.Submit(new GrantCardCommand(player.Id, "crowbar"));
            Assert(player.OwnedCards.Any(card => card.Definition.Id == "crowbar"),
                "Developer grant must add the requested card.");
            Assert(controller.Events.History
                    .OfType<CardAddedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == player.Id &&
                        gameEvent.Source == "Developer"),
                "Developer grant must publish a Developer card event.");

            controller.Submit(new GrantCardCommand(player.Id, "crowbar"));
            Assert(player.OwnedCards.Any(card => card.Definition.Id == "crowbar+"),
                "Granting an owned base card must use the normal fusion upgrade path.");
            Assert(player.OwnedCards.Count == countBefore + 1,
                "Fusion from a developer grant must not create a duplicate base card.");

            bool rejected = false;
            try
            {
                controller.Submit(new GrantCardCommand(player.Id, "missing_card"));
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Assert(rejected, "Developer grant must reject unknown card ids.");
            Console.WriteLine(
                $"[developer] cards={player.OwnedCards.Count} upgraded=crowbar+");
        }

        private static void CheckBagCapacity()
        {
            Assert(LoadoutRules.FitsInBag(0, 3), "An empty bag must fit a 3c card.");
            Assert(LoadoutRules.FitsInBag(7, 3), "A bag holding 7c must still fit a 3c card.");
            Assert(!LoadoutRules.FitsInBag(8, 3), "A bag holding 8c must reject a 3c card.");
            Assert(!LoadoutRules.FitsInBag(10, 1), "A full bag must reject any card.");
            Assert(LoadoutRules.FitsInBag(10, 0), "A zero-width effect must never be blocked.");

            List<CardInstance> cards = new List<CardInstance>();
            for (int index = 0; index < 5; index++)
            {
                cards.Add(new CardInstance(
                    8500 + index,
                    CardDatabase.Instance.Get("car_door")));
            }

            PlayerCardCollection collection = new PlayerCardCollection(
                cards,
                LoadoutRules.GetBattleCapacity(4));
            Assert(collection.BattleWidth == 10, "Five 2c cards must fill a level-4 battle row.");
            Assert(collection.BagWidth == 0, "With every card equipped the bag must be empty.");
            Assert(
                collection.OwnedWidth == collection.BagWidth + collection.BattleWidth,
                "Owned width must be the sum of the two rows.");

            Assert(
                collection.TryAddCard(new CardInstance(
                    8600,
                    CardDatabase.Instance.Get("crowbar"))),
                "A full battle row must not block a card from an empty bag.");
            Assert(collection.BagWidth == 1, "The new card must occupy one column of the bag.");

            Assert(
                collection.TryAddCard(new CardInstance(
                    8601,
                    CardDatabase.Instance.Get("car_door"))),
                "The bag must keep accepting cards until its own 10c is used.");

            PlayerCardCollection fragmented = new PlayerCardCollection(
                new List<CardInstance>(),
                LoadoutRules.GetBattleCapacity(1));
            for (int column = 0; column < 10; column += 2)
            {
                Assert(
                    fragmented.TryAddCardAt(
                        new CardInstance(
                            8700 + column,
                            CardDatabase.Instance.Get("crowbar")),
                        column),
                    "The fragmented-bag fixture must place every 1c card.");
            }

            Assert(
                fragmented.TryAddCard(new CardInstance(
                    8799,
                    CardDatabase.Instance.Get("car_door"))),
                "A 2c card must fit when total bag width has room even if every gap is 1c.");
            Assert(
                fragmented.BagWidth == 7,
                "Repacking must preserve every card and the new card's width.");
            Console.WriteLine(
                $"[bagspace] bag={collection.BagWidth} battle={collection.BattleWidth}");
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
        /// 已经搬去当怪物配置（`MonsterRoster`），不再是玩家开局手牌。
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
            Assert(
                forced.Count > 0 && forced.All(hit => hit.Amount > 100 && hit.Amount <= 106),
                "Ouroboros must add +100 to every number the right neighbour resolves.");

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

        private static void CheckSecondSetMechanics()
        {
            CardDatabase catalog = CardDatabase.Instance;

            // 棋盘层成长：掷出6、停在奖励地块、每轮结束。
            MatchController board = new MatchController(CreateM3Graph(), 20261127UL);
            MatchPlayerState owner = board.State.Players[0];
            CardInstance six = SeedLoadout(owner, 9000, "six_six_streak")[0];
            CardInstance rewardValue = SeedLoadout(owner, 9010, "placeholder_7")[0];
            CardInstance roundDamage = SeedLoadout(owner, 9020, "placeholder_1")[0];

            board.Submit(new DebugRollCommand(owner.Id, 6));
            Assert(six.Value == 4, "六六大顺 must gain 2 value when the owner rolls a 6.");
            BoardCardEconomy.OnDiceRolled(owner, 5);
            Assert(six.Value == 4, "六六大顺 must ignore non-6 rolls.");
            BoardCardEconomy.OnRewardStopped(owner);
            Assert(rewardValue.Value == 3, "占位符7 must gain 1 value on a Reward stop.");
            BoardCardEconomy.OnTurnEnded(owner);
            Assert(
                roundDamage.PermanentDiceModifier == 1,
                "占位符1 must gain 1 permanent dice modifier each round.");

            CardInstance capped = new CardInstance(9030, catalog.Get("six_six_streak"));
            capped.ModifyValue(999);
            Assert(capped.Value == 20, "Permanent value growth must stop at 20.");

            BattleSnapshot Duel(params CardInstance[] cards)
            {
                return new BattleSnapshot(
                    new CombatantSnapshot(0, 5, cards, maxHealth: 5000),
                    new CombatantSnapshot(
                        1,
                        5,
                        new[] { new CardInstance(9999, catalog.Get("car_door")) },
                        maxHealth: 5000));
            }

            // 六六大顺：护盾修正 = 自身价值 + 左右邻价值。
            CardInstance sixBattle = new CardInstance(9100, catalog.Get("six_six_streak"));
            CardInstance leftNeighbour = new CardInstance(9101, catalog.Get("crowbar"));
            CardInstance rightNeighbour = new CardInstance(9102, catalog.Get("car_door"));
            sixBattle.ModifyValue(6);       // 8g
            leftNeighbour.ModifyValue(1);   // 2g
            rightNeighbour.ModifyValue(2);  // 4g
            CombatSimulation sixSimulation = new CombatSimulation(
                Duel(leftNeighbour, sixBattle, rightNeighbour),
                31UL);
            CombatCardRuntime sixRuntime = sixSimulation.Left.Cards
                .First(card => card.Instance.Uid == sixBattle.Uid);
            AuraResolver aura = new AuraResolver();
            int sixShieldModifier =
                aura.GetShieldModifier(sixSimulation.Left, sixRuntime);
            Assert(
                sixShieldModifier == 14,
                "六六大顺 shield modifier must equal self plus both adjacent values, got "
                + sixShieldModifier + ".");

            // 占位符3：其他武器每张 +10；水果刀：每层再生 +1。
            CardInstance placeholder3 = new CardInstance(9110, catalog.Get("placeholder_3"));
            CombatSimulation weaponCounter = new CombatSimulation(
                Duel(
                    placeholder3,
                    new CardInstance(9111, catalog.Get("crowbar")),
                    new CardInstance(9112, catalog.Get("fireman_axe"))),
                32UL);
            CombatCardRuntime placeholder3Runtime = weaponCounter.Left.Cards
                .First(card => card.Instance.Uid == placeholder3.Uid);
            Assert(
                aura.GetDamageDiceModifier(weaponCounter.Left, placeholder3Runtime) == 20,
                "占位符3 must gain +10 for each other weapon.");

            CardInstance fruitKnife = new CardInstance(9120, catalog.Get("fruit_knife"));
            CombatSimulation regenerationCounter = new CombatSimulation(
                Duel(fruitKnife),
                33UL);
            regenerationCounter.Left.Regeneration = 5;
            CombatCardRuntime fruitKnifeRuntime = regenerationCounter.Left.Cards[0];
            Assert(
                aura.GetDamageDiceModifier(regenerationCounter.Left, fruitKnifeRuntime) == 5,
                "水果刀 must gain +1 dice modifier per Regeneration layer.");

            // 占位符2：右邻武器获得吸血。
            CardInstance lifestealAura = new CardInstance(9130, catalog.Get("placeholder_2"));
            CardInstance lifestealWeapon = new CardInstance(9131, catalog.Get("crowbar"));
            CombatSimulation lifestealAuraSimulation = new CombatSimulation(
                Duel(lifestealAura, lifestealWeapon),
                34UL);
            CombatCardRuntime lifestealWeaponRuntime = lifestealAuraSimulation.Left.Cards
                .First(card => card.Instance.Uid == lifestealWeapon.Uid);
            Assert(
                aura.HasLifesteal(lifestealAuraSimulation.Left, lifestealWeaponRuntime),
                "占位符2 must grant lifesteal to its right-side weapon.");

            // 占位符1：主动攻击吸血。
            CardInstance lifestealSelf = new CardInstance(9140, catalog.Get("placeholder_1"));
            CombatSimulation lifestealSimulation = new CombatSimulation(
                Duel(lifestealSelf),
                35UL);
            for (int tick = 0; tick < 360; tick++)
            {
                lifestealSimulation.TickOnce();
            }

            Assert(
                lifestealSimulation.Events.History
                    .OfType<HealthRestoredEvent>()
                    .Any(gameEvent => gameEvent.PlayerId == 0 && gameEvent.Amount > 0),
                "占位符1 must heal from its attack damage through lifesteal.");

            // 占位符7：再生层数等于当前价值。
            CardInstance valueRegeneration = new CardInstance(9150, catalog.Get("placeholder_7"));
            valueRegeneration.ModifyValue(3);
            CombatSimulation regenerationValueSimulation = new CombatSimulation(
                Duel(valueRegeneration),
                36UL);
            for (int tick = 0; tick < 360; tick++)
            {
                regenerationValueSimulation.TickOnce();
            }

            Assert(
                regenerationValueSimulation.Events.History
                    .OfType<StatusAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 0 &&
                        gameEvent.Status == StatusKind.Regeneration &&
                        gameEvent.Layers == 5),
                "占位符7 must gain Regeneration equal to its current value.");

            // 锈刃：任意己方攻击伤害后追加中毒。
            CardInstance rustedBlade = new CardInstance(9160, catalog.Get("rusted_blade"));
            CombatSimulation rustedSimulation = new CombatSimulation(
                Duel(rustedBlade),
                37UL);
            for (int tick = 0; tick < 320; tick++)
            {
                rustedSimulation.TickOnce();
            }

            Assert(
                rustedSimulation.Events.History.OfType<StatusAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 1 &&
                        gameEvent.Status == StatusKind.Poison),
                "锈刃 must apply Poison after its own damage.");

            // 催化剂：己方造成中毒时给自己充能。
            CardInstance catalyst = new CardInstance(9170, catalog.Get("catalyst"));
            CombatSimulation catalystSimulation = new CombatSimulation(
                Duel(catalyst, new CardInstance(9171, catalog.Get("nuclear_water"))),
                38UL);
            for (int tick = 0; tick < 260; tick++)
            {
                catalystSimulation.TickOnce();
            }

            Assert(
                catalystSimulation.Events.History.OfType<ChargeAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 0 &&
                        gameEvent.CardUid == catalyst.Uid),
                "催化剂 must charge itself when poison is applied.");

            // 催化剂一次加速 2 张牌时不得重复命中同一张牌。
            CombatSimulation catalystTargetsSimulation = new CombatSimulation(
                Duel(
                    new CardInstance(9230, catalog.Get("catalyst")),
                    new CardInstance(9231, catalog.Get("crowbar")),
                    new CardInstance(9232, catalog.Get("fireman_axe"))),
                44UL);
            for (int tick = 0; tick < 360 && !catalystTargetsSimulation.IsFinished; tick++)
            {
                catalystTargetsSimulation.TickOnce();
            }

            List<HasteAppliedEvent> catalystTargets =
                catalystTargetsSimulation.Events.History
                    .OfType<HasteAppliedEvent>()
                    .Where(gameEvent => gameEvent.PlayerId == 0)
                    .Take(2)
                    .ToList();
            Assert(
                catalystTargets.Count == 2 &&
                catalystTargets[0].CardUid != catalystTargets[1].CardUid,
                "催化剂 must not select the same own card twice in one haste effect.");

            // 射钉枪+一次缓速 2 张牌时不得重复命中同一张牌。
            CardInstance nailGunPlus = new CardInstance(9233, catalog.Get("nail_gun+"));
            CombatSimulation nailGunTargetsSimulation = new CombatSimulation(
                new BattleSnapshot(
                    new CombatantSnapshot(
                        0, 5, new[] { nailGunPlus }, maxHealth: 5000),
                    new CombatantSnapshot(
                        1,
                        5,
                        new[]
                        {
                            new CardInstance(9234, catalog.Get("car_door")),
                            new CardInstance(9235, catalog.Get("crowbar"))
                        },
                        maxHealth: 5000)),
                45UL);
            for (int tick = 0; tick < 240 && !nailGunTargetsSimulation.IsFinished; tick++)
            {
                nailGunTargetsSimulation.TickOnce();
            }

            List<SlowAppliedEvent> nailGunTargets =
                nailGunTargetsSimulation.Events.History
                    .OfType<SlowAppliedEvent>()
                    .Where(gameEvent => gameEvent.PlayerId == 1)
                    .Take(2)
                    .ToList();
            Assert(
                nailGunTargets.Count == 2 &&
                nailGunTargets[0].CardUid != nailGunTargets[1].CardUid,
                "射钉枪+ must not slow the same enemy card twice in one effect.");

            // 火炬：先结算本次灼烧，再用本次施法获得下一发的 +1 灼烧修正。
            CardInstance torch = new CardInstance(9172, catalog.Get("torch"));
            CombatSimulation torchSimulation = new CombatSimulation(
                Duel(torch),
                46UL);
            for (int tick = 0; tick < 280 && !torchSimulation.IsFinished; tick++)
            {
                torchSimulation.TickOnce();
            }

            CombatCardRuntime torchRuntime = torchSimulation.Left.Cards
                .First(card => card.Instance.Uid == torch.Uid);
            Assert(
                torchRuntime.BattleDiceModifier >= 1,
                "火炬 must increase its burn modifier after its first cast.");

            // 催化剂：灼烧事件也要触发本卡充能，而不只监听中毒。
            CardInstance catalystBurn = new CardInstance(9173, catalog.Get("catalyst"));
            CardInstance burnSource = new CardInstance(9174, catalog.Get("oil_barrel"));
            CombatSimulation catalystBurnSimulation = new CombatSimulation(
                Duel(catalystBurn, burnSource),
                47UL);
            for (int tick = 0; tick < 380 && !catalystBurnSimulation.IsFinished; tick++)
            {
                catalystBurnSimulation.TickOnce();
            }

            Assert(
                catalystBurnSimulation.Events.History.OfType<ChargeAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 0 &&
                        gameEvent.CardUid == catalystBurn.Uid &&
                        gameEvent.Ticks == 60),
                "催化剂 must charge itself when Burn is applied by an ally.");

            // 锈刃：别的武器造成伤害也会触发它的中毒被动。
            CardInstance rustedBladeAura = new CardInstance(9175, catalog.Get("rusted_blade"));
            CardInstance rustedBladeWeapon = new CardInstance(9176, catalog.Get("crowbar"));
            CombatSimulation rustedBladeAuraSimulation = new CombatSimulation(
                Duel(rustedBladeAura, rustedBladeWeapon),
                48UL);
            for (int tick = 0; tick < 240 && !rustedBladeAuraSimulation.IsFinished; tick++)
            {
                rustedBladeAuraSimulation.TickOnce();
            }

            Assert(
                rustedBladeAuraSimulation.Events.History.OfType<StatusAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 1 &&
                        gameEvent.Status == StatusKind.Poison),
                "锈刃 must poison after another allied weapon deals damage.");

            // 占位符5：自己的主动施放不算“使用相邻卡牌”。
            CardInstance shieldSelfOnly =
                new CardInstance(9177, catalog.Get("placeholder_5"));
            CombatSimulation shieldSelfOnlySimulation = new CombatSimulation(
                Duel(shieldSelfOnly),
                49UL);
            for (int tick = 0; tick < 320 && !shieldSelfOnlySimulation.IsFinished; tick++)
            {
                shieldSelfOnlySimulation.TickOnce();
            }

            CombatCardRuntime shieldSelfOnlyRuntime = shieldSelfOnlySimulation.Left.Cards
                .First(card => card.Instance.Uid == shieldSelfOnly.Uid);
            Assert(
                shieldSelfOnlyRuntime.BattleShieldBonus == 0,
                "占位符5 must not gain shield modifier when it casts itself.");

            // 食物中毒：只监听己方使用食物牌，不监听普通武器。
            CombatSimulation nonFoodSimulation = new CombatSimulation(
                Duel(
                    new CardInstance(9178, catalog.Get("food_poisoning")),
                    new CardInstance(9179, catalog.Get("crowbar"))),
                50UL);
            for (int tick = 0; tick < 240 && !nonFoodSimulation.IsFinished; tick++)
            {
                nonFoodSimulation.TickOnce();
            }

            Assert(
                !nonFoodSimulation.Events.History.OfType<StatusAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 1 &&
                        gameEvent.Status == StatusKind.Poison),
                "食物中毒 must not poison when only a non-food card is cast.");

            // 占位符4：敌人施法时充能自己。
            CardInstance enemyCastCharge = new CardInstance(9180, catalog.Get("placeholder_4"));
            CombatSimulation enemyCastSimulation = new CombatSimulation(
                new BattleSnapshot(
                    new CombatantSnapshot(0, 5, new[] { enemyCastCharge }, maxHealth: 5000),
                    new CombatantSnapshot(
                        1,
                        5,
                        new[] { new CardInstance(9181, catalog.Get("crowbar")) },
                        maxHealth: 5000)),
                39UL);
            for (int tick = 0; tick < 200; tick++)
            {
                enemyCastSimulation.TickOnce();
            }

            Assert(
                enemyCastSimulation.Events.History.OfType<ChargeAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 0 &&
                        gameEvent.CardUid == enemyCastCharge.Uid),
                "占位符4 must charge when the enemy casts a card.");

            // 食物中毒：己方使用食物时给敌方中毒。
            CombatSimulation foodSimulation = new CombatSimulation(
                Duel(
                    new CardInstance(9190, catalog.Get("food_poisoning")),
                    new CardInstance(9191, catalog.Get("mineral_water"))),
                40UL);
            for (int tick = 0; tick < 220; tick++)
            {
                foodSimulation.TickOnce();
            }

            Assert(
                foodSimulation.Events.History.OfType<StatusAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 1 &&
                        gameEvent.Status == StatusKind.Poison),
                "食物中毒 must poison the enemy when food is cast.");

            // 占位符5：相邻牌施放后只提高自身护盾修正。
            CardInstance shieldPlaceholder = new CardInstance(9200, catalog.Get("placeholder_5"));
            CombatSimulation shieldSimulation = new CombatSimulation(
                Duel(shieldPlaceholder, new CardInstance(9201, catalog.Get("crowbar"))),
                41UL);
            for (int tick = 0; tick < 200; tick++)
            {
                shieldSimulation.TickOnce();
            }

            CombatCardRuntime shieldRuntime = shieldSimulation.Left.Cards
                .First(card => card.Instance.Uid == shieldPlaceholder.Uid);
            Assert(
                shieldRuntime.BattleShieldBonus == 2,
                "占位符5 must gain +2 shield modifier after an adjacent cast.");

            // 链锯：首次施法前等待完整 360 tick，之后每 180 tick 施放一次。
            CardInstance chainsaw = new CardInstance(9210, catalog.Get("chainsaw"));
            CombatSimulation chainsawSimulation = new CombatSimulation(
                Duel(chainsaw),
                42UL);
            for (int tick = 0; tick < 1000; tick++)
            {
                chainsawSimulation.TickOnce();
            }

            List<CardCastEvent> chainsawCasts = chainsawSimulation.Events.History
                .OfType<CardCastEvent>()
                .Where(gameEvent => gameEvent.CardUid == chainsaw.Uid)
                .ToList();
            Assert(chainsawCasts.Count >= 4, "链锯 must cast at least four times.");
            Assert(
                chainsawCasts[0].Tick >= 359,
                "链锯 must wait for its full 6s initial cooldown.");
            Assert(
                chainsawSimulation.Events.History.OfType<CooldownChangedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 0 &&
                        gameEvent.CardUid == chainsaw.Uid &&
                        gameEvent.CooldownTicks == 180),
                "链锯 must publish its persistent 3s cooldown change.");
            Assert(
                chainsawCasts[1].Tick - chainsawCasts[0].Tick == 180,
                "链锯 first cooldown after its initial cast must be halved.");
            Assert(
                chainsawCasts[2].Tick - chainsawCasts[1].Tick == 180 &&
                chainsawCasts[3].Tick - chainsawCasts[2].Tick == 180,
                "链锯 must keep the halved 3s cooldown after the first cast.");

            // 泰坦：己方中毒事件同时触发充能与伤害修正。
            CardInstance titan = new CardInstance(9220, catalog.Get("titan"));
            CombatSimulation titanSimulation = new CombatSimulation(
                Duel(titan, new CardInstance(9221, catalog.Get("nuclear_water"))),
                43UL);
            for (int tick = 0; tick < 260; tick++)
            {
                titanSimulation.TickOnce();
            }

            CombatCardRuntime titanRuntime = titanSimulation.Left.Cards
                .First(card => card.Instance.Uid == titan.Uid);
            Assert(
                titanRuntime.BattleDiceModifier == 4,
                "泰坦 must gain +4 damage modifier when poison is applied.");
            Assert(
                titanSimulation.Events.History.OfType<ChargeAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.PlayerId == 0 &&
                        gameEvent.CardUid == titan.Uid),
                "泰坦 must charge itself when poison is applied.");

            // 泰坦：毒、烧、加速、缓速四类事件都能独立触发两者。
            CardInstance titanAll = new CardInstance(9250, catalog.Get("titan"));
            CombatSimulation titanAllSimulation = new CombatSimulation(
                Duel(
                    titanAll,
                    new CardInstance(9251, catalog.Get("torch")),
                    new CardInstance(9252, catalog.Get("nail_gun")),
                    new CardInstance(9253, catalog.Get("nuclear_water")),
                    new CardInstance(9254, catalog.Get("catalyst"))),
                51UL);
            for (int tick = 0; tick < 320 && !titanAllSimulation.IsFinished; tick++)
            {
                titanAllSimulation.TickOnce();
            }

            CombatCardRuntime titanAllRuntime = titanAllSimulation.Left.Cards
                .First(card => card.Instance.Uid == titanAll.Uid);
            Assert(
                titanAllRuntime.BattleDiceModifier >= 16,
                "泰坦 must gain +4 for poison, burn, haste and slow events.");
            Assert(
                titanAllSimulation.Events.History.OfType<ChargeAppliedEvent>()
                    .Count(gameEvent =>
                        gameEvent.PlayerId == 0 &&
                        gameEvent.CardUid == titanAll.Uid) >= 4,
                "泰坦 must charge for each of the four status event types.");

            Assert(catalog.Get("rusted_blade+").Passives.Count == 1,
                "锈刃+ must retain the damage-triggered poison passive.");
            Assert(catalog.Get("placeholder_2+").Auras[0].Target == AuraTarget.AllAdjacent,
                "占位符2+ must affect all adjacent weapons.");

            Console.WriteLine(
                "[second-set] board-growth, lifesteal, triggers and dynamic modifiers verified");
        }

        private static void CheckCardStrictnessFixes()
        {
            CardDatabase catalog = CardDatabase.Instance;

            // 回合结束只结算当前玩家；棋盘经济被动从背包也生效。
            MatchController turn = new MatchController(CreateM3Graph(), 20261210UL);
            MatchPlayerState owner = turn.State.Players[0];
            CardInstance dorayaki = new CardInstance(9300, catalog.Get("dorayaki"));
            CardInstance purifierA = new CardInstance(9301, catalog.Get("snow_purifier"));
            CardInstance purifierB = new CardInstance(9302, catalog.Get("snow_purifier"));
            Assert(owner.TryAddCard(dorayaki, equipIfPossible: false), "Dorayaki fixture must fit.");
            Assert(owner.TryAddCard(purifierA, equipIfPossible: false), "Purifier A fixture must fit.");
            Assert(owner.TryAddCard(purifierB, equipIfPossible: false), "Purifier B fixture must fit.");
            Assert(
                !owner.IsEquipped(dorayaki) &&
                !owner.IsEquipped(purifierA) &&
                !owner.IsEquipped(purifierB),
                "Turn-end economy fixtures must start in the bag.");

            int eventStart = turn.Events.History.Count;
            turn.Submit(new DebugRollCommand(owner.Id, 1));
            turn.Submit(new MoveCommand(owner.Id));
            Assert(
                dorayaki.Value == 2,
                "Dorayaki must grow at its owner's first turn end, not four turns later.");
            Assert(
                turn.Events.History.Skip(eventStart)
                    .OfType<CardAddedEvent>()
                    .Count(cardEvent => cardEvent.Source == "Purifier") == 1 &&
                turn.Events.History.Skip(eventStart)
                    .OfType<CardUpgradedEvent>()
                    .Count(cardEvent => cardEvent.Source == "Purifier") == 1,
                "Two bagged Snow Purifiers must each produce a Fresh Water at turn end.");

            for (int index = 0; index < 3; index++)
            {
                MatchPlayerState other = turn.State.CurrentPlayer;
                turn.Submit(new DebugRollCommand(other.Id, 1));
                turn.Submit(new MoveCommand(other.Id));
            }

            Assert(
                dorayaki.Value == 2,
                "Other players' turn ends must not grow the owner's Dorayaki.");

            // Recycler value aura works from the bag and is reflected as effective value.
            MatchController recyclerMatch = new MatchController(CreateM3Graph(), 20261211UL);
            MatchPlayerState recyclerOwner = recyclerMatch.State.Players[0];
            CardInstance recycler =
                new CardInstance(9310, catalog.Get("recycler"));
            CardInstance waste =
                new CardInstance(9311, catalog.Get("roadblock"));
            Assert(recyclerOwner.TryAddCard(recycler, equipIfPossible: false), "Recycler must fit.");
            Assert(recyclerOwner.TryAddCard(waste, equipIfPossible: false), "Waste must fit.");
            Assert(
                !recyclerOwner.IsEquipped(recycler),
                "The recycler value probe must run from the bag.");
            Assert(
                recyclerOwner.EffectiveCardValue(waste) == 4 &&
                BoardCardEconomy.SaleValue(recyclerOwner, waste) == 4,
                "A bagged Recycler must add +2 value to a 2c waste card.");

            // "Weapon damage" modifiers must not leak into healing effects.
            CombatSimulation modified = new CombatSimulation(
                new BattleSnapshot(
                    new CombatantSnapshot(
                        0,
                        5,
                        new[]
                        {
                            new CardInstance(9320, catalog.Get("whetstone")),
                            new CardInstance(9321, catalog.Get("baguette"))
                        },
                        maxHealth: 5000),
                    new CombatantSnapshot(
                        1,
                        5,
                        new[] { new CardInstance(9322, catalog.Get("car_door")) },
                        maxHealth: 5000)),
                2UL);
            CombatSimulation baseline = new CombatSimulation(
                new BattleSnapshot(
                    new CombatantSnapshot(
                        0,
                        5,
                        new[] { new CardInstance(9321, catalog.Get("baguette")) },
                        maxHealth: 5000),
                    new CombatantSnapshot(
                        1,
                        5,
                        new[] { new CardInstance(9322, catalog.Get("car_door")) },
                        maxHealth: 5000)),
                2UL);
            for (int tick = 0; tick < 400; tick++)
            {
                modified.TickOnce();
                baseline.TickOnce();
            }

            HealthRestoredEvent modifiedHeal = modified.Events.History
                .OfType<HealthRestoredEvent>()
                .First(gameEvent => gameEvent.PlayerId == 0);
            HealthRestoredEvent baselineHeal = baseline.Events.History
                .OfType<HealthRestoredEvent>()
                .First(gameEvent => gameEvent.PlayerId == 0);
            Assert(
                modifiedHeal.Amount == baselineHeal.Amount,
                "Whetstone must not increase Baguette's healing dice.");
            CombatCardRuntime baguetteRuntime = modified.Left.Cards
                .First(card => card.Instance.Uid == 9321);
            AuraResolver aura = new AuraResolver();
            Assert(
                aura.GetDamageDiceModifier(modified.Left, baguetteRuntime) == 2 &&
                aura.GetDiceModifier(modified.Left, baguetteRuntime) == 0,
                "Whetstone's +2 must be classified as damage-only.");

            // Fresh Water's sale bonus is healing-only.
            CardInstance healingOnly = new CardInstance(9330, catalog.Get("baguette"));
            healingOnly.PermanentHealingModifier = 2;
            Assert(
                healingOnly.PermanentDiceModifier == 0 &&
                healingOnly.PermanentHealingModifier == 2,
                "Fresh Water's permanent buff must not become a generic dice modifier.");

            MatchController freshWaterMatch =
                new MatchController(CreateM3Graph(), 20261212UL);
            MatchPlayerState freshWaterOwner = freshWaterMatch.State.Players[0];
            CardInstance foodHeal = new CardInstance(9340, catalog.Get("baguette"));
            CardInstance freshWater = new CardInstance(9341, catalog.Get("fresh_water"));
            Assert(freshWaterOwner.TryAddCard(foodHeal, equipIfPossible: false), "Food must fit.");
            Assert(freshWaterOwner.TryAddCard(freshWater, equipIfPossible: false), "Water must fit.");
            freshWaterMatch.Submit(new SellCardCommand(
                freshWaterOwner.Id,
                freshWater.Uid));
            Assert(
                foodHeal.PermanentHealingModifier == 1 &&
                foodHeal.PermanentDiceModifier == 0,
                "Selling Fresh Water must buff the leftmost healing value only.");

            Console.WriteLine(
                "[strictness] turn ownership, bag economy, modifier scope verified");
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

            MatchController rewards = FindRollingController(graph, 3);
            Assert(rewards != null, "Some seed must roll a 3 for the PvP reward probe.");
            MatchPlayerState rewardAttacker = rewards.State.CurrentPlayer;
            MatchPlayerState rewardDefender = rewards.State.Players[3];
            SeedLoadout(
                rewardAttacker,
                12000,
                "crowbar",
                "crowbar",
                "crowbar",
                "crowbar");
            rewardAttacker.Wins = 3;
            rewardDefender.Wins = 3;
            int attackerShards = rewardAttacker.Shards;
            int defenderShards = rewardDefender.Shards;
            rewards.Submit(new MoveCommand(rewardAttacker.Id, 3));

            CombatResolvedEvent rewardFight = rewards.Events.History
                .OfType<CombatResolvedEvent>()
                .Last();
            Assert(
                rewardFight.Result.WinnerPlayerId == rewardAttacker.Id,
                "The seeded PvP reward probe must have a deterministic winner.");
            Assert(
                rewardAttacker.Shards == attackerShards &&
                rewardDefender.Shards == defenderShards,
                "PvP victory must not transfer Shards.");
            Assert(
                rewardAttacker.Wins == 4 && rewardDefender.Wins == 2,
                "PvP victory must add 1 to the winner and subtract 1 from the loser.");
            WinsChangedEvent[] winChanges = rewards.Events.History
                .OfType<WinsChangedEvent>()
                .ToArray();
            Assert(
                winChanges.Any(change =>
                    change.PlayerId == rewardAttacker.Id && change.Delta == 1) &&
                winChanges.Any(change =>
                    change.PlayerId == rewardDefender.Id && change.Delta == -1),
                "PvP must publish +1 and -1 WinsChangedEvents.");

            MatchController winFloor = FindRollingController(graph, 3);
            MatchPlayerState floorWinner = winFloor.State.CurrentPlayer;
            MatchPlayerState floorLoser = winFloor.State.Players[3];
            SeedLoadout(
                floorWinner,
                12100,
                "crowbar",
                "crowbar",
                "crowbar",
                "crowbar");
            floorWinner.Wins = 0;
            floorLoser.Wins = 0;
            winFloor.Submit(new MoveCommand(floorWinner.Id, 3));
            Assert(
                winFloor.Events.History
                    .OfType<CombatResolvedEvent>()
                    .Last()
                    .Result
                    .WinnerPlayerId == floorWinner.Id,
                "The PvP win-floor probe needs the seeded attacker to win.");
            Assert(
                floorWinner.Wins == 1 && floorLoser.Wins == 0,
                "PvP loss must not reduce Wins below zero.");
            Assert(
                !winFloor.Events.History
                    .OfType<WinsChangedEvent>()
                    .Any(change =>
                        change.PlayerId == floorLoser.Id && change.Delta < 0),
                "A zero-Win loser must not emit a fake -1 WinsChangedEvent.");

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
                "[pvp] shards=unchanged winner=+1 loser=-1 floor=0; "
                + "passthrough and avoid sticks");
        }

        /// <summary>
        /// 事件格（R-EVT-001..005）：按阶段抽取事件，校验动态费用、传送目标和「走开」。
        /// </summary>
        /// <summary>
        /// R-TRAP-001：铁丝网 / 路障出售后在地块上留陷阱。路障强制停车并消耗次数，
        /// 铁丝网让停在上面的单位禁行若干回合，禁行回合里掷骰不动。
        /// </summary>
        private static void CheckBoardTraps()
        {
            // 8 格环：0..3 是四个家（R-MAP-003 要求恰好 4 个唯一归属的家），4..7 空地。
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

            // 1) 路障强制停车：路径上第一个强制停车陷阱把移动截断在那里。
            MatchController stopper = FindRollingController(graph, 5);
            Assert(stopper != null, "Some seed must roll a 5 for the trap probe.");
            MatchPlayerState walker = stopper.State.CurrentPlayer;
            stopper.State.AddTrap(2, BoardTrapKind.ForcedStop, walker.Id, 2);
            stopper.State.AddTrap(2, BoardTrapKind.ForcedStop, walker.Id, 2);
            stopper.Submit(new MoveCommand(walker.Id, null));
            Assert(
                walker.CurrentNode == 2,
                "A roadblock must stop the walker on its tile, reached "
                    + walker.CurrentNode.ToString() + ".");
            Assert(
                stopper.State.Traps.Count == 2 &&
                stopper.State.Traps.All(trap => trap.Charges == 1),
                "Stacked roadblocks must each spend one charge when they trigger.");
            Assert(
                stopper.Events.History.OfType<BoardTrapTriggeredEvent>().Any(
                    triggered => triggered.PlayerId == walker.Id
                        && triggered.Kind == BoardTrapKind.ForcedStop),
                "Triggering a trap must be reported.");

            // 2) 出售路障 / 铁丝网会在地块上留下陷阱。
            MatchController seller = new MatchController(graph, 20261203UL);
            MatchPlayerState owner = seller.State.CurrentPlayer;
            List<CardInstance> cards = SeedLoadout(owner, 9100, "roadblock", "barbed_wire+");
            seller.Submit(new SellCardCommand(owner.Id, cards[0].Uid));
            Assert(
                seller.State.Traps.Count == 1 &&
                seller.State.Traps[0].Kind == BoardTrapKind.ForcedStop &&
                seller.State.Traps[0].NodeId == owner.CurrentNode &&
                seller.State.Traps[0].Charges == 3,
                "Selling a roadblock must leave a 3-charge forced stop on the tile.");
            Assert(
                seller.Events.History.OfType<BoardTrapPlacedEvent>().Any(
                    placed => placed.PlayerId == owner.Id
                        && placed.Kind == BoardTrapKind.ForcedStop),
                "Placing a trap must be reported.");

            seller.Submit(new SellCardCommand(owner.Id, cards[1].Uid));
            Assert(
                seller.State.Traps.Count == 2 &&
                seller.State.Traps.Any(
                    trap => trap.Kind == BoardTrapKind.Immobilize && trap.Charges == 3),
                "Selling 铁丝网+ must leave a 3-round immobilise on the tile.");

            MatchController stackSeller = new MatchController(graph, 20261205UL);
            MatchPlayerState stackOwner = stackSeller.State.CurrentPlayer;
            List<CardInstance> stackCards =
                SeedLoadout(stackOwner, 9200, "roadblock", "roadblock");
            stackSeller.Submit(new SellCardCommand(stackOwner.Id, stackCards[0].Uid));
            stackSeller.Submit(new SellCardCommand(stackOwner.Id, stackCards[1].Uid));
            Assert(
                stackSeller.State.TrapsAt(stackOwner.CurrentNode)
                    .Count(trap => trap.Kind == BoardTrapKind.ForcedStop) == 2,
                "Selling two roadblocks on one tile must leave two independent traps.");

            // 3) 铁丝网：停上去就被禁行，禁行回合里掷骰也不动。
            MatchController trapper = FindRollingController(graph, 1);
            Assert(trapper != null, "Some seed must roll a 1 for the immobilise probe.");
            MatchPlayerState victim = trapper.State.CurrentPlayer;
            trapper.State.AddTrap(1, BoardTrapKind.Immobilize, victim.Id, 2);
            trapper.State.AddTrap(1, BoardTrapKind.Immobilize, victim.Id, 3);
            trapper.Submit(new MoveCommand(victim.Id, null));
            Assert(
                victim.CurrentNode == 1 && victim.ImmobileRounds == 3,
                "Stacked 铁丝网 traps must each trigger; the strongest duration applies.");
            Assert(
                trapper.State.Traps.Count == 0,
                "Every stacked 铁丝网 is one-shot and must disappear after it triggers.");

            // 4) 禁行回合：掷骰后直接结束回合，人物不动。
            MatchController stuck = new MatchController(graph, 20261204UL);
            MatchPlayerState immobile = stuck.State.CurrentPlayer;
            immobile.ImmobileRounds = 2;
            int turnBefore = stuck.State.TurnNumber;
            int rollEventStart = stuck.Events.History.Count;
            stuck.Submit(new RollCommand(immobile.Id));
            Assert(
                immobile.CurrentNode == 0,
                "An immobilised player must not move.");
            Assert(
                stuck.State.TurnNumber == turnBefore + 1 &&
                stuck.State.CurrentPlayer.Id == 1,
                "An immobilised turn must end immediately.");
            Assert(
                immobile.ImmobileRounds == 1,
                "Immobilise on the player must count down every turn.");
            Assert(
                stuck.State.LastRoll >= 1 && stuck.State.LastRoll <= 6 &&
                stuck.Events.History.Skip(rollEventStart)
                    .OfType<DiceRolledEvent>()
                    .Any(dice => dice.PlayerId == immobile.Id && dice.Value == stuck.State.LastRoll),
                "An immobilised player must still roll the die before skipping movement.");
            Assert(
                stuck.Events.History.OfType<PlayerImmobilizedEvent>().Any(
                    immobilized => immobilized.PlayerId == immobile.Id),
                "Being immobilised must be reported.");

            Console.WriteLine(
                "[trap] roadblock stops and spends a charge, 铁丝网 immobilises for 2 rounds");
        }

        private static void CheckMatchEvent()
        {
            // 8 格环：0..3 四个家、4 空地、5 事件格、6..7 空地。
            MapNode[] nodes = new MapNode[8];
            for (int index = 0; index < nodes.Length; index++)
            {
                bool isHome = index < 4;
                nodes[index] = new MapNode(
                    index,
                    isHome ? TileType.Home : index == 5 ? TileType.Event : TileType.Neutral,
                    isHome ? index : -1,
                    (index + 1) % nodes.Length);
            }

            MapGraph graph = MapGraph.Create(nodes, playerCount: 4);

            for (ulong seed = 1; seed <= 64; seed++)
            {
                MatchEventDefinition early = MatchEventCatalog.PickForPhase(
                    MonsterPhase.Early,
                    new Pcg32(seed, 11UL));
                Assert(
                    early.Kind == MatchEventKind.Expedition ||
                    early.Kind == MatchEventKind.MysteryDrink ||
                    early.Kind == MatchEventKind.UnknownTrail,
                    "Early events must come from the stage-one pool.");
            }

            MatchController LandEvent(
                MatchEventKind kind,
                bool allPlayersLevelTwo = false,
                bool latePhase = false)
            {
                for (ulong seed = 1; seed <= 2000; seed++)
                {
                    MatchController controller = new MatchController(graph, seed);
                    if (allPlayersLevelTwo)
                    {
                        foreach (MatchPlayerState seat in controller.State.Players)
                        {
                            seat.Level = 2;
                        }
                    }

                    if (latePhase)
                    {
                        controller.State.Players[0].Level = 4;
                    }

                    MatchPlayerState player = controller.State.CurrentPlayer;
                    controller.Submit(new DebugRollCommand(player.Id, 5));
                    controller.Submit(new MoveCommand(player.Id, null));
                    if (controller.CurrentEvent?.Definition.Kind == kind)
                    {
                        return controller;
                    }
                }

                return null;
            }

            MatchController mystery = LandEvent(MatchEventKind.MysteryDrink);
            Assert(mystery != null, "The stage-one pool must contain mystery drink.");
            MatchPlayerState taker = mystery.State.CurrentPlayer;
            int shardsBefore = taker.Shards;
            Assert(mystery.CurrentEvent.Options.Count == 3,
                "The mystery drink must offer three choices.");
            mystery.Submit(new EventChoiceCommand(taker.Id, 2));
            CardInstance drink = taker.OwnedCards.FirstOrDefault(
                card => card.Definition.Id == MatchEventCatalog.MysteryDrinkCardId);
            Assert(drink != null, "Taking the mystery drink must put it in the bag.");
            Assert(taker.Shards == shardsBefore, "The free drink option must cost no shards.");
            Assert(
                BoardCardEconomy.SaleValue(taker, drink) == 7,
                "The mystery drink must sell for 1 + 6 shards.");
            Assert(
                mystery.Events.History.OfType<CardAddedEvent>().Any(
                    added => added.PlayerId == taker.Id &&
                             added.CardId == MatchEventCatalog.MysteryDrinkCardId &&
                             added.Source == "Event"),
                "A granted event card must be reported.");

            MatchController power = LandEvent(MatchEventKind.MysteryDrink);
            MatchPlayerState powered = power.State.CurrentPlayer;
            power.Submit(new EventChoiceCommand(powered.Id, 1));
            Assert(
                powered.BattleModifiers.Any(modifier =>
                    modifier.DamageBonus == 2 &&
                    modifier.BlockBonus == 2 &&
                    modifier.HealBonus == 2 &&
                    modifier.PoisonBonus == 1 &&
                    modifier.BurnBonus == 1 &&
                    modifier.RegenerationBonus == 1),
                "Mystery drink's mixed battle modifier must be recorded.");

            MatchController randomTrail = LandEvent(MatchEventKind.UnknownTrail);
            Assert(randomTrail != null, "The stage-one pool must contain unknown trail.");
            MatchPlayerState trailWalker = randomTrail.State.CurrentPlayer;
            int trailStart = trailWalker.CurrentNode;
            randomTrail.Submit(new EventChoiceCommand(trailWalker.Id, 0));
            Assert(
                randomTrail.Events.History.OfType<PlayerTeleportedEvent>()
                    .Any(teleport =>
                        teleport.PlayerId == trailWalker.Id &&
                        teleport.IsEventTeleport &&
                        teleport.FromNodeId == trailStart),
                "Unknown trail's random option must teleport through an event teleport.");

            MatchController targetTrail = LandEvent(MatchEventKind.UnknownTrail);
            MatchPlayerState targetWalker = targetTrail.State.CurrentPlayer;
            int beforeTarget = targetWalker.CurrentNode;
            targetTrail.Submit(new EventChoiceCommand(targetWalker.Id, 1));
            Assert(
                targetTrail.State.Phase == MatchPhase.AwaitEventTarget,
                "The guided option must wait for a target tile.");
            int selectedTarget = beforeTarget == 0 ? 1 : 0;
            targetTrail.Submit(new EventTeleportCommand(targetWalker.Id, selectedTarget));
            Assert(
                targetWalker.CurrentNode == selectedTarget &&
                targetTrail.State.Phase == MatchPhase.AwaitRoll,
                "The guided option must teleport to the selected tile.");

            MatchController raid = LandEvent(MatchEventKind.Raid, allPlayersLevelTwo: true);
            Assert(raid != null, "The stage-two pool must contain raid.");
            MatchPlayerState raider = raid.State.CurrentPlayer;
            int raidRound = raid.State.RoundNumber;
            raid.Submit(new EventChoiceCommand(raider.Id, 0));
            Assert(
                raider.OwnedCards.Any(
                    card => card.Definition.Id == MatchEventCatalog.MurderWeaponCardId),
                "Raid's paid option must grant 凶器.");
            Assert(
                raid.State.KillTimeStartRound == raidRound + 1 &&
                raid.State.KillTimeEndRound == raidRound + 3,
                "Raid must open exactly the next three rounds.");

            MatchController raidTrade = LandEvent(MatchEventKind.Raid, allPlayersLevelTwo: true);
            MatchPlayerState trader = raidTrade.State.CurrentPlayer;
            trader.Wins = 1;
            int traderShards = trader.Shards;
            raidTrade.Submit(new EventChoiceCommand(trader.Id, 1));
            Assert(
                trader.Wins == 0 && trader.Shards == traderShards + 5,
                "Raid's second option must trade one win for five shards.");

            MatchController biovirus = LandEvent(MatchEventKind.Biovirus, latePhase: true);
            Assert(biovirus != null, "The stage-three pool must contain biovirus.");
            MatchPlayerState infected = biovirus.State.CurrentPlayer;
            biovirus.Submit(new EventChoiceCommand(infected.Id, 0));
            Assert(
                infected.OwnedCards.Any(
                    card => card.Definition.Id == MatchEventCatalog.BiovirusCardId) &&
                infected.OwnedCards.Any(
                    card => card.Definition.Id == MatchEventCatalog.MotherCardId),
                "Biovirus must grant both 生化病毒 and 母体.");
            Assert(
                infected.OwnedCards
                    .Where(card => card.Definition.Id == MatchEventCatalog.BiovirusCardId ||
                                   card.Definition.Id == MatchEventCatalog.MotherCardId)
                    .All(card => card.Definition.CannotSell),
                "Biovirus and 母体 must be unsellable.");

            MatchController biovirusPay = LandEvent(MatchEventKind.Biovirus, latePhase: true);
            MatchPlayerState payer = biovirusPay.State.CurrentPlayer;
            payer.Shards = 50;
            biovirusPay.Submit(new EventChoiceCommand(payer.Id, 1));
            Assert(payer.Shards == 10,
                "Paying the biovirus event must cost level ×10 shards.");

            // 杀戮时间的 PvP 例外：胜场变化翻倍，并掠夺败者等级 ×5 碎片。
            MatchController killTimeDuel = new MatchController(graph, 72UL);
            MatchPlayerState killer = killTimeDuel.State.CurrentPlayer;
            MatchPlayerState victim = killTimeDuel.State.Players[1];
            killer.Level = 5;
            victim.Level = 1;
            SeedLoadout(
                killer,
                9950,
                "placeholder_3",
                "fireman_axe");
            victim.CurrentNode = 1;
            killer.Wins = 1;
            victim.Wins = 1;
            killer.Shards = 0;
            victim.Shards = 20;
            killTimeDuel.State.StartKillTime(3);
            killTimeDuel.State.RoundNumber = killTimeDuel.State.KillTimeStartRound;
            killTimeDuel.Submit(new DebugRollCommand(killer.Id, 1));
            killTimeDuel.Submit(new MoveCommand(killer.Id, 1));
            CombatResolvedEvent killTimeCombat = killTimeDuel.Events.History
                .OfType<CombatResolvedEvent>()
                .Last();
            Assert(
                killTimeCombat.Result.WinnerPlayerId == killer.Id,
                "The kill-time duel fixture must produce an attacker win.");
            Assert(
                killer.Wins == 3 && victim.Wins == 0,
                "Kill time must double both sides' win changes.");
            Assert(
                killer.Shards == 5 && victim.Shards == 15,
                "Kill time must steal the loser's level ×5 shards.");

            // 生化病毒的战斗后转化：只转背包里的 1c 牌。
            MapGraph monsterGraph = MapGraph.Create(
                new[]
                {
                    new MapNode(0, TileType.Home, 0, 1),
                    new MapNode(1, TileType.Monster, -1, 2),
                    new MapNode(2, TileType.Home, 1, 3),
                    new MapNode(3, TileType.Home, 2, 4),
                    new MapNode(4, TileType.Home, 3, 0)
                },
                playerCount: 4);
            MatchController infection = new MatchController(monsterGraph, 71UL);
            MatchPlayerState infectedOwner = infection.State.CurrentPlayer;
            CardInstance virus = new CardInstance(
                9900,
                CardDatabase.Instance.Get(MatchEventCatalog.BiovirusCardId));
            CardInstance oneCost = new CardInstance(
                9901,
                CardDatabase.Instance.Get("crowbar"));
            Assert(infectedOwner.TryAddCard(virus, equipIfPossible: false),
                "The biovirus transform fixture must fit.");
            Assert(infectedOwner.TryAddCard(oneCost, equipIfPossible: false),
                "The one-cost transform fixture must fit.");
            infection.Submit(new DebugRollCommand(infectedOwner.Id, 1));
            infection.Submit(new MoveCommand(infectedOwner.Id, null));
            Assert(
                infection.Events.History.OfType<CardTransformedEvent>().Any(
                    transformed =>
                        transformed.PlayerId == infectedOwner.Id &&
                        transformed.ToCardId == MatchEventCatalog.BiovirusCardId),
                "A battle with biovirus must transform one bag 1c card.");

            bool refused = false;
            try
            {
                infection.Submit(new SellCardCommand(
                    infectedOwner.Id,
                    virus.Uid));
            }
            catch (InvalidOperationException)
            {
                refused = true;
            }

            Assert(refused, "A biovirus card must be refused by the sell command.");

            Console.WriteLine(
                "[event] stage pools, mystery, trails, raid, biovirus and transforms verified");
        }

        private static void CheckEventCardMechanics()
        {
            CardDatabase catalog = CardDatabase.Instance;
            CardDefinition weapon = catalog.Get(MatchEventCatalog.MurderWeaponCardId);
            CardDefinition mother = catalog.Get(MatchEventCatalog.MotherCardId);
            CardDefinition biovirus = catalog.Get(MatchEventCatalog.BiovirusCardId);

            Assert(
                weapon.HasTag(CardTag.Event) &&
                weapon.HasTag(CardTag.Weapon) &&
                weapon.Auras.Any(aura => aura.Scaling == AuraScaling.PerOwnerWins),
                "凶器 must be an event weapon that scales with wins.");
            Assert(
                mother.HasTag(CardTag.Event) &&
                mother.Auras.Any(aura => aura.Stat == AuraStat.MaxHealthPerPoison) &&
                mother.CannotSell,
                "母体 must expose its poison-to-health aura and be unsellable.");
            Assert(
                biovirus.HasTag(CardTag.Event) &&
                biovirus.HasTag(CardTag.Disease) &&
                biovirus.ActiveEffects.Count == 2 &&
                biovirus.CannotSell,
                "生化病毒 must poison both sides and be unsellable.");

            CardInstance weaponInstance = new CardInstance(9400, weapon);
            CombatSimulation weaponSimulation = new CombatSimulation(
                new BattleSnapshot(
                    new CombatantSnapshot(
                        0, 5, new[] { weaponInstance }, maxHealth: 5000, wins: 5),
                    new CombatantSnapshot(
                        1, 5,
                        new[] { new CardInstance(9401, catalog.Get("car_door")) },
                        maxHealth: 5000)),
                80UL);
            CombatCardRuntime weaponRuntime = weaponSimulation.Left.Cards[0];
            AuraResolver aura = new AuraResolver();
            Assert(
                aura.GetDamageDiceModifier(weaponSimulation.Left, weaponRuntime) == 5,
                "凶器 must gain one damage modifier per accrued win.");
            for (int tick = 0; tick < 600 && !weaponSimulation.IsFinished; tick++)
            {
                weaponSimulation.TickOnce();
            }

            DamageDealtEvent weaponHit = weaponSimulation.Events.History
                .OfType<DamageDealtEvent>()
                .First(gameEvent => gameEvent.SourcePlayerId == 0);
            Assert(
                weaponHit.Amount >= 18,
                "凶器 must resolve three d20 dice plus the win modifier.");

            CardInstance motherInstance = new CardInstance(9410, mother);
            CardInstance poisonSource = new CardInstance(
                9411,
                catalog.Get(MatchEventCatalog.BiovirusCardId));
            CombatSimulation motherSimulation = new CombatSimulation(
                new BattleSnapshot(
                    new CombatantSnapshot(
                        0,
                        1,
                        new[] { motherInstance, poisonSource },
                        maxHealth: 100),
                    new CombatantSnapshot(
                        1,
                        1,
                        new[] { new CardInstance(9412, catalog.Get("car_door")) },
                        maxHealth: 100)),
                81UL);
            for (int tick = 0; tick < 300 && !motherSimulation.IsFinished; tick++)
            {
                motherSimulation.TickOnce();
            }

            Assert(
                motherSimulation.Left.Poison > 0 &&
                motherSimulation.Left.MaximumHealth ==
                    motherSimulation.Left.BaseMaximumHealth +
                    motherSimulation.Left.Poison * 20,
                "母体 must add 20 maximum health per poison layer.");
            Assert(
                motherSimulation.Left.Regeneration > 0,
                "母体 must gain regeneration equal to poison it applies.");

            CombatSimulation biovirusSimulation = new CombatSimulation(
                new BattleSnapshot(
                    new CombatantSnapshot(
                        0,
                        3,
                        new[] { new CardInstance(9420, biovirus) },
                        maxHealth: 5000),
                    new CombatantSnapshot(
                        1,
                        3,
                        new[] { new CardInstance(9421, catalog.Get("car_door")) },
                        maxHealth: 5000)),
                82UL);
            for (int tick = 0; tick < 240 && !biovirusSimulation.IsFinished; tick++)
            {
                biovirusSimulation.TickOnce();
            }

            Assert(
                biovirusSimulation.Events.History.OfType<StatusAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.Status == StatusKind.Poison &&
                        gameEvent.PlayerId == 0) &&
                biovirusSimulation.Events.History.OfType<StatusAppliedEvent>()
                    .Any(gameEvent =>
                        gameEvent.Status == StatusKind.Poison &&
                        gameEvent.PlayerId == 1),
                "生化病毒 must poison both the owner and the enemy.");

            Console.WriteLine(
                "[event-cards] weapon, mother and biovirus mechanics verified");
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
                    new MapNode(0, TileType.Home, 0, new MapCell(0, 2), 1),
                    new MapNode(1, TileType.Home, 1, new MapCell(0, 1), straightOnly, 0, 2, 4),
                    new MapNode(2, TileType.Home, 2, new MapCell(0, 0), 1, 3),
                    new MapNode(3, TileType.Home, 3, new MapCell(1, 3), 2),
                    new MapNode(4, TileType.Neutral, -1, new MapCell(1, 1), 1),
                };

                return MapGraph.Create(nodes, playerCount: 4);
            }

            MapGraph open = Build(false);
            Assert(
                open.LegalNextNodes(1, 0).Count == 2,
                "A normal junction must offer every non-back direction.");

            MapGraph forced = Build(true);
            IReadOnlyList<int> legal = forced.LegalNextNodes(1, 0);
            Assert(
                legal.Count == 1 && legal[0] == 2,
                "A straight-only tile must keep only its authored straight edge.");

            int landing = MovementSolver.PredictLanding(forced, 0, 3, 3);
            Assert(
                landing == 3,
                "Predicting three steps across a straight-only tile must land on node 3, got "
                    + landing.ToString() + ".");

            Console.WriteLine("[straightonly] junction keeps straight only, predicted landing=3");
        }

        private static bool IsStraightOrLeft(
            MapGraph graph,
            int previousNodeId,
            int nodeId,
            int nextNodeId)
        {
            MapNode previous = graph.GetNode(previousNodeId);
            MapNode node = graph.GetNode(nodeId);
            MapNode next = graph.GetNode(nextNodeId);
            int forwardRow = node.GridRow - previous.GridRow;
            int forwardColumn = node.GridColumn - previous.GridColumn;
            int stepRow = next.GridRow - node.GridRow;
            int stepColumn = next.GridColumn - node.GridColumn;
            return (stepRow == forwardRow && stepColumn == forwardColumn) ||
                   (stepRow == -forwardColumn && stepColumn == forwardRow);
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

            // R-MAP-002：走廊只有一个前进方向，且永远不是原路。
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

            int promptEntries = 0;
            foreach (MapNode junction in graph.Nodes.Where(n => n.OutEdges.Count >= 3))
            {
                foreach (int entry in junction.OutEdges)
                {
                    IReadOnlyList<int> legal = graph.LegalNextNodes(junction.Id, entry);
                    Assert(
                        legal.Count >= 1,
                        "A Map1 junction must always leave a legal forward exit.");
                    Assert(
                        legal.All(edge => edge != entry),
                        "A junction must not offer the way back.");

                    if (legal.Count >= 2)
                    {
                        promptEntries++;
                        Assert(
                            legal.All(next => IsStraightOrLeft(
                                graph,
                                entry,
                                junction.Id,
                                next)),
                            "A Map1 fork prompt may only offer straight or left turns.");
                    }
                }
            }

            Assert(
                promptEntries == junctions,
                "Every Map1 junction must expose one straight-or-left prompt.");

            int fork = 4 * 12 + 4;
            int forkApproach = 4 * 12 + 5;
            IReadOnlyList<int> forkExits = graph.LegalNextNodes(fork, forkApproach);
            Assert(
                forkExits.Count == 2 &&
                forkExits.Contains(5 * 12 + 4) &&
                forkExits.Contains(4 * 12 + 3),
                "A real Map1 fork must keep both valid counter-clockwise choices.");

            Dictionary<int, TileType> types = graph.Nodes
                .ToDictionary(node => node.Id, node => node.TileType);
            int teleports = 0;

            for (ulong seed = 1; seed <= 40; seed++)
            {
                MatchController controller = new MatchController(graph, seed);
                int start = controller.Events.History.Count;
                SimpleMatchAutoplayer.Run(controller, maximumTurns: 400);

                Dictionary<int, int> lastCell = new Dictionary<int, int>();
                Dictionary<int, int> previousCell = new Dictionary<int, int>();
                foreach (GameEvent gameEvent in controller.Events.History.Skip(start))
                {
                    if (gameEvent is PlayerMovedEvent moved)
                    {
                        if (lastCell.TryGetValue(moved.PlayerId, out int previousNode))
                        {
                            Assert(
                                moved.FromNodeId == previousNode,
                                "Movement events must continue from the last visited tile.");
                            if (previousCell.TryGetValue(
                                    moved.PlayerId,
                                    out int nodeBeforeMove))
                            {
                                IReadOnlyList<int> legal =
                                    graph.LegalNextNodes(moved.FromNodeId, nodeBeforeMove);
                                Assert(
                                    legal.Contains(moved.ToNodeId),
                                    "A movement step must use one of the legal forward exits.");
                                Assert(
                                    moved.ToNodeId != nodeBeforeMove,
                                    "A movement step must not reverse immediately.");
                            }
                        }

                        Assert(
                            moved.ToNodeId != moved.FromNodeId,
                            "A movement step must change tiles.");
                        previousCell[moved.PlayerId] = moved.FromNodeId;
                        lastCell[moved.PlayerId] = moved.ToNodeId;
                        continue;
                    }

                    if (gameEvent is TurnStartedEvent started)
                    {
                        lastCell.Remove(started.PlayerId);
                        previousCell.Remove(started.PlayerId);
                        continue;
                    }

                    if (!(gameEvent is PlayerTeleportedEvent teleport))
                    {
                        continue;
                    }

                    if (teleport.IsEventTeleport)
                    {
                        Assert(
                            types[teleport.FromNodeId] == TileType.Event,
                            "An event teleport must start on the event tile.");
                        teleports++;
                        lastCell.Remove(teleport.PlayerId);
                        previousCell.Remove(teleport.PlayerId);
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
                    lastCell.Remove(teleport.PlayerId);
                    previousCell.Remove(teleport.PlayerId);
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
            new SceneCell(1, 7, TileType.Neutral, 0),
            new SceneCell(1, 11, TileType.Reward, 0),
            new SceneCell(2, 0, TileType.Warp, 0),
            new SceneCell(2, 7, TileType.Neutral, 0),
            new SceneCell(2, 11, TileType.Event, 0),
            new SceneCell(3, 0, TileType.Event, 0),
            new SceneCell(3, 7, TileType.Neutral, 0),
            new SceneCell(3, 11, TileType.Reward, 0),
            new SceneCell(4, 0, TileType.Reward, 0),
            new SceneCell(4, 4, TileType.Shop, 0),
            new SceneCell(4, 5, TileType.Neutral, 0),
            new SceneCell(4, 6, TileType.Neutral, 0),
            new SceneCell(4, 7, TileType.Shop, 0),
            new SceneCell(4, 1, TileType.Neutral, 0),
            new SceneCell(4, 2, TileType.Neutral, 0),
            new SceneCell(4, 3, TileType.Neutral, 0),
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
            new SceneCell(7, 4, TileType.Shop, 0),
            new SceneCell(7, 5, TileType.Neutral, 0),
            new SceneCell(7, 6, TileType.Neutral, 0),
            new SceneCell(7, 7, TileType.Shop, 0),
            new SceneCell(7, 8, TileType.Neutral, 0),
            new SceneCell(7, 9, TileType.Neutral, 0),
            new SceneCell(7, 10, TileType.Neutral, 0),
            new SceneCell(7, 11, TileType.Event, 0),
            new SceneCell(8, 0, TileType.Monster, 0),
            new SceneCell(8, 4, TileType.Neutral, 0),
            new SceneCell(8, 11, TileType.Monster, 0),
            new SceneCell(9, 0, TileType.Monster, 0),
            new SceneCell(9, 4, TileType.Neutral, 0),
            new SceneCell(9, 11, TileType.Event, 0),
            new SceneCell(10, 0, TileType.Event, 0),
            new SceneCell(10, 4, TileType.Neutral, 0),
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

        private static void CheckSweetRingMovementRules()
        {
            MapGraph graph = CreateSweetRingGraph();
            Assert(graph.Nodes.Count == 36, "Map2 must keep all 36 sketch cells.");

            int junctions = graph.Nodes.Count(node => node.OutEdges.Count >= 3);
            Assert(junctions > 0, "Map2 must contain forks.");

            foreach (MapNode node in graph.Nodes)
            {
                foreach (int entry in node.OutEdges)
                {
                    IReadOnlyList<int> legal = graph.LegalNextNodes(node.Id, entry);
                    Assert(
                        legal.Count > 0,
                        "Every Map2 entry must leave a legal forward exit.");
                    Assert(
                        legal.All(next => next != entry),
                        "Map2 must not offer the reverse direction.");
                }
            }

            int promptEntries = 0;
            foreach (MapNode junction in graph.Nodes.Where(node => node.OutEdges.Count >= 3))
            {
                foreach (int entry in junction.OutEdges)
                {
                    IReadOnlyList<int> legal = graph.LegalNextNodes(junction.Id, entry);
                    if (legal.Count >= 2)
                    {
                        promptEntries++;
                        Assert(
                            legal.All(next => IsStraightOrLeft(
                                graph,
                                entry,
                                junction.Id,
                                next)),
                            "A Map2 fork prompt may only offer straight or left turns.");
                    }
                }
            }

            Assert(
                promptEntries == junctions,
                "Every Map2 junction must expose one straight-or-left prompt.");

            int fork = 2 * 16 + 6;
            int forkEntry = 2 * 16 + 5;
            IReadOnlyList<int> forkExits = graph.LegalNextNodes(fork, forkEntry);
            Assert(
                forkExits.Count == 2 &&
                forkExits.Contains(1 * 16 + 6) &&
                forkExits.Contains(2 * 16 + 7),
                "Map2 must keep both valid straight-or-left fork choices.");

            int warp = 1 * 16 + 6;
            Assert(
                MovementSolver.PredictLanding(
                    graph,
                    startNodeId: fork,
                    previousNodeId: forkEntry,
                    steps: 2,
                    forcedFirstNodeId: warp,
                    stopAtWarp: true) == warp,
                "Map2 fork preview must stop at a Warp tile.");

            int movements = 0;
            int teleports = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                MatchController controller = new MatchController(graph, seed);
                SimpleMatchAutoplayer.Run(controller, maximumTurns: 400);

                Dictionary<int, int> lastCell = new Dictionary<int, int>();
                Dictionary<int, int> previousCell = new Dictionary<int, int>();
                foreach (GameEvent gameEvent in controller.Events.History)
                {
                    if (gameEvent is PlayerMovedEvent moved)
                    {
                        if (lastCell.TryGetValue(moved.PlayerId, out int current) &&
                            previousCell.TryGetValue(moved.PlayerId, out int previous))
                        {
                            Assert(
                                moved.FromNodeId == current,
                                "Map2 movement events must be continuous.");
                            Assert(
                                moved.ToNodeId != previous,
                                "Map2 movement must not reverse immediately.");
                            Assert(
                                graph.LegalNextNodes(moved.FromNodeId, previous)
                                    .Contains(moved.ToNodeId),
                                "Map2 movement must use a legal forward exit.");
                        }

                        previousCell[moved.PlayerId] = moved.FromNodeId;
                        lastCell[moved.PlayerId] = moved.ToNodeId;
                        Assert(
                            moved.ToNodeId != moved.FromNodeId,
                            "Every Map2 movement step must change tiles.");
                        movements++;
                    }
                    else if (gameEvent is TurnStartedEvent started)
                    {
                        lastCell.Remove(started.PlayerId);
                        previousCell.Remove(started.PlayerId);
                    }
                    else if (gameEvent is PlayerTeleportedEvent teleport)
                    {
                        lastCell.Remove(teleport.PlayerId);
                        previousCell.Remove(teleport.PlayerId);
                        teleports++;
                    }
                }
            }

            Assert(movements > 0, "Map2 regression runs must exercise movement.");
            Console.WriteLine(
                "[sweetring] moves=" + movements.ToString()
                + " teleports=" + teleports.ToString());
        }

        private static MapGraph CreateSweetRingGraph()
        {
            string[] sketch =
            {
                "....GBY....",
                "....Y.P....",
                "...HRYGH...",
                ".YPG...RYG.",
                ".B.Y...Y.B.",
                ".GYR...GPY.",
                "...HGYRH...",
                "....P.Y....",
                "....YBG...."
            };

            const int stride = 16;
            Dictionary<int, (int Row, int Column, char Code)> byId =
                new Dictionary<int, (int Row, int Column, char Code)>();
            for (int row = 0; row < sketch.Length; row++)
            {
                for (int column = 0; column < sketch[row].Length; column++)
                {
                    char code = sketch[row][column];
                    if (code != '.')
                    {
                        byId[row * stride + column] = (row, column, code);
                    }
                }
            }

            int[] rowOffsets = { -1, 0, 1, 0 };
            int[] columnOffsets = { 0, 1, 0, -1 };
            List<MapNode> nodes = new List<MapNode>();
            foreach (KeyValuePair<int, (int Row, int Column, char Code)> pair in byId
                         .OrderBy(pair => pair.Key))
            {
                (int row, int column, char code) = pair.Value;
                List<int> outgoing = new List<int>();
                for (int index = 0; index < 4; index++)
                {
                    int nextRow = row + rowOffsets[index];
                    int nextColumn = column + columnOffsets[index];
                    if (nextRow < 0 || nextRow >= sketch.Length ||
                        nextColumn < 0 || nextColumn >= sketch[nextRow].Length)
                    {
                        continue;
                    }

                    int nextId = nextRow * stride + nextColumn;
                    if (byId.ContainsKey(nextId))
                    {
                        outgoing.Add(nextId);
                    }
                }

                outgoing.Sort();
                TileType type = SweetRingTileType(code);
                int owner = type != TileType.Home
                    ? -1
                    : row == 2 && column == 3 ? 0
                    : row == 6 && column == 3 ? 1
                    : row == 6 && column == 7 ? 2
                    : 3;
                nodes.Add(
                    new MapNode(
                        pair.Key,
                        type,
                        owner,
                        new MapCell(row, column),
                        outgoing.ToArray()));
            }

            return MapGraph.Create(
                nodes,
                playerCount: 4,
                teleportGroups: new[]
                {
                    new[] { 1 * stride + 6, 7 * stride + 4 },
                    new[] { 3 * stride + 2, 5 * stride + 8 }
                });
        }

        private static TileType SweetRingTileType(char code)
        {
            switch (code)
            {
                case 'H':
                    return TileType.Home;
                case 'R':
                    return TileType.Monster;
                case 'G':
                    return TileType.Shop;
                case 'P':
                    return TileType.Warp;
                case 'B':
                    return TileType.Event;
                default:
                    return TileType.Reward;
            }
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
        /// These seeds used to fail when the CPU chose a reward that did not fit, or when
        /// fragmented bag gaps blocked a card despite enough total bag width.
        /// </summary>
        private static void CheckCpuRewardFitRegression()
        {
            ulong[] seeds = { 20261070UL, 20261074UL, 20261149UL };
            foreach (ulong seed in seeds)
            {
                MatchController controller =
                    new MatchController(CreateCpuAcceptanceGraph(), seed);
                CpuMatchResult result = new CpuMatchRunner(controller).Run();

                Assert(result.Finished, $"{seed}: reward-fit regression must finish.");
                Assert(
                    result.StallReason.Length == 0,
                    $"{seed}: reward-fit regression stalled. " + result.StallReason);
                Console.WriteLine(
                    $"[cpu-reward] seed={seed} finished=True rounds={result.Rounds}");
            }
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

        /// <summary>
        /// Same 24-tile acceptance board used by Tools/MatchSim. Kept here so the CPU
        /// reward regression exercises the exact map topology that exposed the bug.
        /// </summary>
        private static MapGraph CreateCpuAcceptanceGraph()
        {
            TileType[] layout =
            {
                TileType.Home, TileType.Monster, TileType.Reward, TileType.Shop,
                TileType.Monster, TileType.Warp, TileType.Home, TileType.Monster,
                TileType.Reward, TileType.Shop, TileType.Monster, TileType.Warp,
                TileType.Home, TileType.Monster, TileType.Reward, TileType.Shop,
                TileType.Monster, TileType.Boss, TileType.Home, TileType.Monster,
                TileType.Reward, TileType.Shop, TileType.Monster, TileType.Warp
            };

            List<MapNode> nodes = new List<MapNode>(layout.Length);
            for (int nodeId = 0; nodeId < layout.Length; nodeId++)
            {
                TileType tileType = layout[nodeId];
                int owner = tileType == TileType.Home
                    ? CpuAcceptanceHomeOwner(nodeId)
                    : -1;
                nodes.Add(
                    new MapNode(
                        nodeId,
                        tileType,
                        owner,
                        (nodeId + 1) % layout.Length));
            }

            int[] warps = Enumerable.Range(0, layout.Length)
                .Where(nodeId => layout[nodeId] == TileType.Warp)
                .ToArray();

            return MapGraph.Create(
                nodes,
                playerCount: 4,
                teleportGroups: new[] { warps });
        }

        private static int CpuAcceptanceHomeOwner(int nodeId)
        {
            switch (nodeId)
            {
                case 0:
                    return 0;
                case 6:
                    return 1;
                case 12:
                    return 2;
                default:
                    return 3;
            }
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
