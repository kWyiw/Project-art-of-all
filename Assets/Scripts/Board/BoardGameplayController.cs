using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Commands;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Events;
using ProjectArtOfAll.Core.Match;
using ProjectArtOfAll.Flow;
using ProjectArtOfAll.View;
using UnityEngine;

namespace ProjectArtOfAll.Board
{
    public readonly struct BoardForkChoice
    {
        public BoardForkChoice(Vector3 screenPosition, string arrow)
        {
            ScreenPosition = screenPosition;
            Arrow = arrow;
        }

        public Vector3 ScreenPosition { get; }
        public string Arrow { get; }
    }

    [DisallowMultipleComponent]
    public sealed class BoardGameplayController : MonoBehaviour
    {
        [Header("Match")]
        [Tooltip("打开后每局都用下面这个固定种子（调试、复现某一局时用）；关闭则每局随机种子。")]
        [SerializeField] private bool useFixedSeed;

        [Tooltip("固定种子的值，仅在 useFixedSeed 打开时生效。")]
        [SerializeField] private ulong matchSeed;

        /// <summary>本局实际使用的根种子（固定或随机，开局时决定一次）。</summary>
        private ulong activeSeed;

        /// <summary>本局实际使用的根种子（0 表示还没开局）。</summary>
        public ulong ActiveSeed => activeSeed;

        /// <summary>是否使用固定种子（调试/复现用）。</summary>
        public bool UsesFixedSeed => useFixedSeed && matchSeed != 0UL;

        /// <summary>
        /// CPU 思考延迟区间（秒）。R-CPU-003 要求 0.8~1.5 秒的随机停顿，
        /// 决策本身在 300ms 内完成，停顿只是让对局看起来像人在想。
        /// </summary>
        private const float CpuThinkMinSeconds = 0.8f;
        private const float CpuThinkMaxSeconds = 1.5f;

        [Header("Movement")]
        [SerializeField, Min(0.05f)] private float stepDuration = 0.2f;
        [SerializeField, Min(0f)] private float hopHeight = 0.18f;

        private readonly Dictionary<int, BoardTileView> tilesByNode =
            new Dictionary<int, BoardTileView>();
        private readonly Dictionary<int, BoardPlayerToken> tokensByPlayer =
            new Dictionary<int, BoardPlayerToken>();
        private readonly List<int> pendingForkEdges = new List<int>();

        private BoardHudController hud;
        private int humanPlayerId;
        private BoardCameraController cameraController;
        private bool cameraFollowEnabled = true;
        private BoardPlayerToken activeToken;
        private MapGraph graph;
        private MatchController match;
        private GameObject pathLines;
        private int pendingForkSelection = -1;
        private bool isMoving;
        private bool awaitingFork;
        private bool awaitingRewardChoice;
        private bool awaitingShop;
        private bool awaitingEncounterAcceptance;

        /// <summary>真人席位数：正常模式固定 0；无CPU测试模式为 -1（表示"当前行动者"）。</summary>
        private int humanSeat = -1;

        /// <summary>本回合由 CPU 自动推进：跳过岔路弹窗与遭遇确认，岔路取合法列表第一项。</summary>
        private bool automaticTurn;

        /// <summary>CPU 下一次行动的时间点（表现延迟，便于看清它做了什么）。</summary>
        private float cpuActAfter;
        private readonly Dictionary<int, CpuBrain> cpuBrains =
            new Dictionary<int, CpuBrain>();

        private void Start()
        {
            // 选地图页选中的是生成棋盘时，先把场景里的地块换成新布局，
            // 再建 tile 索引与图——这样后面的所有逻辑（渲染、寻路、AI）都不用区分地图。
            BoardMapDefinition generatedMap = BoardMapLibrary.For(GameSession.SelectedMapId);
            if (generatedMap != null)
            {
                BoardMapBuilder.Build(this, generatedMap);
            }

            BuildSceneLookup();
            // 生成棋盘自带连线（菱形环带不是方阵邻接），场景棋盘则用网格邻接。
            graph = generatedMap != null
                ? UnityMapGraphFactory.BuildFromDefinition(generatedMap)
                : UnityMapGraphFactory.Build(tilesByNode.Values);
            pathLines = BoardPathLines.Build(transform, graph, tilesByNode);
            // 正常模式让真人先手（从主页面进来的第一回合就能操作）；
            // 无CPU测试模式没有固定真人，沿用准备室选的门。
            int startingPlayer = GameSession.HumanSeat >= 0
                ? GameSession.HumanSeat
                : ResolveEntryPlayerId();
            activeSeed = useFixedSeed && matchSeed != 0UL
                ? matchSeed
                : MatchSeed.Create();
            Debug.Log(string.Concat(
                "[Board] match seed ", MatchSeed.Describe(activeSeed),
                useFixedSeed ? " (fixed)" : " (random)"));
            match = new MatchController(
                graph,
                activeSeed,
                startingPlayerId: startingPlayer);

            cameraController = Camera.main != null
                ? Camera.main.GetComponent<BoardCameraController>()
                : null;
            humanSeat = GameSession.HumanSeat;
            BuildCpuBrains();
            humanPlayerId = humanSeat >= 0
                ? humanSeat
                : match.State.CurrentPlayer.Id;
            BindHud();
            SynchronizeTokensFromCore();
            ActivateCurrentPlayer();
            UpdateHudStatus();
            hud?.ShowOpeningHints();
        }

        /// <summary>设置：镜头是否跟随当前行动的棋子（GameSettings.CameraFollow）。</summary>
        public void SetCameraFollow(bool enabled)
        {
            cameraFollowEnabled = enabled;
            if (match == null || match.IsFinished)
            {
                return;
            }

            if (!enabled)
            {
                cameraController?.SetFollowTarget(null);
                return;
            }

            if (!tokensByPlayer.TryGetValue(match.State.CurrentPlayer.Id, out BoardPlayerToken token)
                || token == null
                || cameraController == null)
            {
                return;
            }

            // token == null 走的是 Unity 重载的 ==：棋子所在的场景已经卸载时这里为 true，
            // 不加这个判断就会去读 token.transform 并抛 MissingReferenceException。
            cameraController.SetFollowTarget(token.transform);
        }

        public void SetPathLinesVisible(bool visible)
        {
            if (this == null)
            {
                return;
            }

            if (pathLines != null)
            {
                pathLines.SetActive(visible);
            }
        }

        /// <summary>人类席位；卡槽带始终显示这一位玩家的牌。</summary>
        public MatchPlayerState HumanPlayer => match == null
            ? null
            : match.State.Players[humanPlayerId];

        /// <summary>跨区移动 / 同排重排（R-TURN-002 免费动作）。</summary>
        public void SubmitCardMove(int cardUid, bool toBattle, int column)
        {
            if (match == null || match.IsFinished)
            {
                return;
            }

            try
            {
                match.Submit(new MoveCardCommand(humanPlayerId, cardUid, toBattle, column));
            }
            catch (System.InvalidOperationException error)
            {
                hud?.SetMatchStatus(string.Concat("卡牌放不下：", error.Message));
                return;
            }

            UpdateHudStatus();
        }

        /// <summary>拖进出售区松手 / 详情面板点「出售」= 立即出售，无确认无撤销（UI-4.1.1）。</summary>
        public void SubmitSellCard(int cardUid)
        {
            if (match == null)
            {
                return;
            }

            if (match.IsFinished)
            {
                hud?.SetMatchStatus("对局已经结束，不能再出售卡牌。");
                return;
            }

            int eventStart = match.Events.History.Count;
            try
            {
                match.Submit(new SellCardCommand(humanPlayerId, cardUid));
            }
            catch (System.InvalidOperationException error)
            {
                hud?.SetMatchStatus(string.Concat("出售失败：", error.Message));
                return;
            }

            CardSoldEvent sold = match.Events.History
                .Skip(eventStart)
                .OfType<CardSoldEvent>()
                .LastOrDefault();
            string soldName = sold == null
                ? "卡牌"
                : CardDatabase.Instance.Get(sold.CardId).DisplayName;
            int refund = sold == null ? 0 : sold.Shards;

            hud?.RefreshCardBand(HumanPlayer);
            UpdateHudStatus(string.Concat(
                "玩家 ",
                (humanPlayerId + 1).ToString(),
                " 出售 ",
                soldName,
                "  +",
                refund.ToString(),
                " 碎片"));
        }

        public void RefreshCardBand()
        {
            hud?.RefreshCardBand(HumanPlayer);
        }

        public void RollDice()
        {
            if (isMoving || awaitingFork || match == null)
            {
                return;
            }

            ResolveDeferredCorePhase();
            if (match.IsFinished)
            {
                hud?.SetDiceInteractable(false);
                return;
            }

            if (match.State.Phase != MatchPhase.AwaitRoll)
            {
                return;
            }

            MatchPlayerState player = match.State.CurrentPlayer;
            match.Submit(new RollCommand(player.Id));
            int roll = match.State.LastRoll;
            hud?.SetDiceResult(roll);
            UpdateHudStatus($"掷出 {roll}");
            StartCoroutine(PlanAndSubmitMove(player.Id, roll));
        }

        public void SelectFork(int index)
        {
            if (!awaitingFork || index < 0 || index >= pendingForkEdges.Count)
            {
                return;
            }

            pendingForkSelection = index;
        }

        private void BuildSceneLookup()
        {
            tilesByNode.Clear();
            tokensByPlayer.Clear();

            foreach (BoardTileView tile in GetComponentsInChildren<BoardTileView>(true))
            {
                tilesByNode[UnityMapGraphFactory.NodeId(tile.Row, tile.Column)] = tile;
            }

            foreach (BoardPlayerToken token in GetComponentsInChildren<BoardPlayerToken>(true))
            {
                tokensByPlayer[token.PlayerId] = token;
            }
        }

        private void BindHud()
        {
            hud = FindObjectOfType<BoardHudController>();
            if (hud != null)
            {
                hud.BindGameplayController(this);
            }
        }

        private IEnumerator PlanAndSubmitMove(
            int playerId,
            int steps,
            CpuDecision plan = null)
        {
            isMoving = true;
            hud?.SetDiceInteractable(false);
            cameraController?.SetPlayerMoving(true);

            MatchPlayerState movingPlayer = match.State.CurrentPlayer;
            int current = movingPlayer.CurrentNode;
            int previous = movingPlayer.LastArrivalNode;
            List<int> forkDirections = new List<int>();
            int encounterNode = -1;
            int[] plannedForks = plan != null ? plan.ForkDirections : null;
            int plannedForkCursor = 0;
            int targetNodeId = plan != null ? plan.NodeId : -1;

            for (int step = 0; step < steps; step++)
            {
                MapNode node = graph.GetNode(current);
                IReadOnlyList<int> legalEdges = match.LegalNextNodes(current, previous);
                if (legalEdges.Count == 0)
                {
                    break;
                }

                int selectedEdgeIndex;
                if (legalEdges.Count == 1)
                {
                    selectedEdgeIndex = 0;
                }
                else if (automaticTurn)
                {
                    // CPU 走 AI 规划好的岔路；没规划（无 CPU 测试模式）时取合法列表第一项。
                    selectedEdgeIndex =
                        plannedForks != null && plannedForkCursor < plannedForks.Length
                            ? plannedForks[plannedForkCursor]
                            : 0;
                    if (selectedEdgeIndex < 0 || selectedEdgeIndex >= legalEdges.Count)
                    {
                        selectedEdgeIndex = 0;
                    }

                    plannedForkCursor++;
                    forkDirections.Add(selectedEdgeIndex);
                }
                else
                {
                    pendingForkEdges.Clear();
                    pendingForkEdges.AddRange(legalEdges);
                    pendingForkSelection = -1;
                    awaitingFork = true;

                    hud?.ShowForkChoices(
                        CreateForkChoices(current, legalEdges),
                        SelectFork);

                    while (pendingForkSelection < 0)
                    {
                        yield return null;
                    }

                    selectedEdgeIndex = pendingForkSelection;
                    awaitingFork = false;
                    hud?.HideForkChoices();
                    forkDirections.Add(selectedEdgeIndex);
                }

                int next = legalEdges[selectedEdgeIndex];
                BoardTileView fromTile = tilesByNode[current];
                BoardTileView toTile = tilesByNode[next];
                UpdateHudStatus(
                    $"掷出 {match.State.LastRoll}  ·  移动 {step + 1}/{steps}");
                yield return AnimateStep(activeToken, fromTile, toTile);

                previous = current;
                current = next;
                Vector2Int currentCell =
                    UnityMapGraphFactory.CellFromNodeId(current);
                activeToken.SetBoardCell(currentCell.x, currentCell.y);

                if (targetNodeId >= 0 && current == targetNodeId)
                {
                    break;
                }

                MatchPlayerState defender = FindPlayerAtNode(
                    current,
                    movingPlayer.Id);
                if (defender == null)
                {
                    continue;
                }

                encounterNode = current;
                if (!automaticTurn && hud != null)
                {
                    awaitingEncounterAcceptance = hud.ShowPlayerEncounter(
                        movingPlayer,
                        defender,
                        () => awaitingEncounterAcceptance = false);
                }

                while (awaitingEncounterAcceptance)
                {
                    yield return null;
                }

                break;
            }

            int eventStart = match.Events.History.Count;
            int shardsBefore = movingPlayer.Shards;
            int winsBefore = movingPlayer.Wins;
            match.Submit(
                new MoveCommand(
                    playerId,
                    targetNodeId: targetNodeId >= 0
                        ? targetNodeId
                        : encounterNode >= 0 ? encounterNode : (int?)null,
                    forkDirections: forkDirections));

            PlayerTeleportedEvent teleport = match.Events.History
                .Skip(eventStart)
                .OfType<PlayerTeleportedEvent>()
                .LastOrDefault();
            if (teleport != null)
            {
                // R-MOVE-006：只有移动结束后停在这格才传送；目标在其余传送格里随机。
                UpdateHudStatus(string.Concat(
                    "传送 ",
                    NodeLabel(teleport.FromNodeId),
                    " → ",
                    NodeLabel(teleport.ToNodeId)));
                Debug.Log(string.Concat(
                    "[Board] warp p",
                    (movingPlayer.Id + 1).ToString(),
                    ": ",
                    NodeLabel(teleport.FromNodeId),
                    " -> ",
                    NodeLabel(teleport.ToNodeId)));

                Vector3 baseScale = activeToken.transform.localScale;
                activeToken.gameObject.SetActive(false);
                yield return new WaitForSeconds(0.15f);
                activeToken.transform.position = GetStandingPosition(
                    activeToken,
                    tilesByNode[teleport.ToNodeId]);
                activeToken.gameObject.SetActive(true);
                activeToken.transform.localScale = baseScale * 1.25f;
                yield return new WaitForSeconds(0.18f);
                activeToken.transform.localScale = baseScale;
                yield return new WaitForSeconds(0.35f);
            }

            SynchronizeTokensFromCore();
            LogPlayerCells("移动结束");
            cameraController?.SetPlayerMoving(false);
            cameraController?.NotifyPlayerMoved();

            CombatResolvedEvent combat = match.Events.History
                .Skip(eventStart)
                .OfType<CombatResolvedEvent>()
                .LastOrDefault();
            if (combat != null)
            {
                yield return PlayCombat(combat, movingPlayer);
            }

            ResolveDeferredCorePhase();
            isMoving = false;
            automaticTurn = false;
            hud?.SetDiceInteractable(
                !match.IsFinished &&
                !awaitingRewardChoice &&
                !awaitingShop &&
                IsHumanSeat(match.State.CurrentPlayer) &&
                match.State.Phase == MatchPhase.AwaitRoll);
            ActivateCurrentPlayer();

            string action = combat == null
                ? null
                : combat.Result.IsDraw
                    ? "战斗平局"
                    : $"战斗胜者：玩家 {combat.Result.WinnerPlayerId + 1}";

            bool rewardFailed = match.Events.History
                .Skip(eventStart)
                .OfType<RewardFailedEvent>()
                .Any();
            if (rewardFailed)
            {
                action = "背包已满，这次奖励没拿到（R-ECO-006）";
            }

            UpdateHudStatus(action);

            if (combat != null)
            {
                ReportCombat(combat, movingPlayer, shardsBefore, winsBefore);
            }
        }

        /// <summary>
        /// 战斗可视化：只有涉及玩家的战斗才回放（CPU 互殴只看状态行），
        /// 播完再继续后面的地块结算与回合流转。
        /// </summary>
        private IEnumerator PlayCombat(
            CombatResolvedEvent combat,
            MatchPlayerState attacker)
        {
            if (hud == null || combat == null || combat.Result == null)
            {
                yield break;
            }

            MatchPlayerState defenderPlayer = match.State.Players
                .FirstOrDefault(player => player.Id == combat.DefenderId);
            bool defenderIsHuman = defenderPlayer != null && IsHumanSeat(defenderPlayer);
            if (!IsHumanSeat(attacker) && !defenderIsHuman)
            {
                yield break;
            }

            string leftTitle = string.Concat(
                "玩家 ",
                (attacker.Id + 1).ToString(),
                " · Lv.",
                attacker.Level.ToString());
            string rightTitle = defenderPlayer == null
                ? "怪物"
                : string.Concat(
                    "玩家 ",
                    (defenderPlayer.Id + 1).ToString(),
                    " · Lv.",
                    defenderPlayer.Level.ToString());

            bool attackerIsHuman = IsHumanSeat(attacker);
            bool done = false;
            hud.PlayCombat(combat, leftTitle, rightTitle, attackerIsHuman, () => done = true);
            while (!done)
            {
                yield return null;
            }
        }

        private void ReportCombat(
            CombatResolvedEvent combat,
            MatchPlayerState player,
            int shardsBefore,
            int winsBefore)
        {
            if (hud == null)
            {
                return;
            }

            CombatResult result = combat.Result;
            bool opponentIsPlayer = match.State.Players
                .Any(other => other.Id == combat.DefenderId);
            string title = result.IsDraw
                ? "战斗平局"
                : result.WinnerPlayerId == player.Id
                    ? "战斗胜利"
                    : "战斗失败";
            string subtitle = string.Concat(
                "用时 ",
                result.EndSecond.ToString(),
                " 秒 · ",
                ReasonLabel(result.Reason),
                opponentIsPlayer ? " · 玩家对战" : " · 怪物战",
                " · 防守方 ",
                opponentIsPlayer
                    ? string.Concat("玩家 ", (combat.DefenderId + 1).ToString())
                    : "怪物");

            int shardDelta = player.Shards - shardsBefore;
            int winDelta = player.Wins - winsBefore;
            System.Text.StringBuilder body = new System.Text.StringBuilder();
            body.AppendLine(string.Concat(
                "玩家 ",
                (player.Id + 1).ToString(),
                "　生命 ",
                result.LeftHealth.ToString(),
                "　护盾 ",
                result.LeftShield.ToString()));
            body.AppendLine(string.Concat(
                "对手　　生命 ",
                result.RightHealth.ToString(),
                "　护盾 ",
                result.RightShield.ToString()));
            body.AppendLine();
            body.AppendLine(string.Concat(
                "碎片 ",
                Signed(shardDelta),
                "　　胜场 ",
                Signed(winDelta)));
            body.AppendLine();
            body.Append(string.Concat("装配：", FormatLoadout(player)));

            if (!IsHumanSeat(player))
            {
                // CPU 的战斗不弹结算面板：只把结果留在状态行，否则面板会一直盖住棋盘。
                hud.HideCombatResult();
                return;
            }

            hud.ShowCombatResult(title, subtitle, body.ToString());
            hud.SetCombatResultTint(
                result.IsDraw
                    ? new Color(0.86f, 0.82f, 0.66f, 1f)
                    : result.WinnerPlayerId == player.Id
                        ? new Color(0.62f, 0.88f, 0.58f, 1f)
                        : new Color(0.92f, 0.52f, 0.46f, 1f));
        }

        private static string Signed(int value)
        {
            return value > 0 ? string.Concat("+", value.ToString()) : value.ToString();
        }

        private static string ReasonLabel(Core.Events.CombatEndReason reason)
        {
            switch (reason)
            {
                case Core.Events.CombatEndReason.Knockout:
                    return "击倒";
                case Core.Events.CombatEndReason.TimeoutKnockout:
                    return "超时击倒";
                case Core.Events.CombatEndReason.HardTimeoutDraw:
                    return "超时平局";
                default:
                    return "结算";
            }
        }

        private static string FormatLoadout(MatchPlayerState player)
        {
            if (player.BattleCards.Count == 0)
            {
                return "没有装配战斗卡牌";
            }

            return string.Join(
                "  ·  ",
                player.BattleCards.Select(
                    card => string.Concat(
                        card.Definition.DisplayName,
                        " ",
                        card.Definition.SlotSize.ToString(),
                        "c")));
        }

        private MatchPlayerState FindPlayerAtNode(int nodeId, int movingPlayerId)
        {
            return match.State.Players
                .Where(player => player.Id != movingPlayerId)
                .Where(player => player.CurrentNode == nodeId)
                .OrderBy(player => player.Id)
                .FirstOrDefault();
        }

        private void ResolveDeferredCorePhase()
        {
            if (awaitingRewardChoice || awaitingShop)
            {
                return;
            }

            for (int guard = 0; guard < 8; guard++)
            {
                MatchPlayerState player = match.State.CurrentPlayer;
                switch (match.State.Phase)
                {
                    case MatchPhase.AwaitNorma:
                        match.Submit(new SetNormaCommand(player.Id, NormaType.Shard));
                        continue;
                    case MatchPhase.AwaitReward:
                        if (hud != null &&
                            IsHumanSeat(player) &&
                            match.CurrentRewardChoices.Count > 0)
                        {
                            awaitingRewardChoice = true;
                            hud?.SetDiceInteractable(false);
                            hud.ShowRewardChoices(
                                player,
                                match.CurrentRewardChoices,
                                OnRewardChosen);
                            return;
                        }

                        int rewardIndex = FindFirstFittingReward(player);
                        if (rewardIndex < 0)
                        {
                            return;
                        }

                        match.Submit(new ChooseRewardCommand(player.Id, rewardIndex));
                        continue;
                    case MatchPhase.AwaitShop:
                        if (hud != null &&
                            IsHumanSeat(player) &&
                            match.CurrentShop != null)
                        {
                            awaitingShop = true;
                            hud.SetDiceInteractable(false);
                            hud.ShowShop(
                                player,
                                match.CurrentShop,
                                OnShopBuy,
                                OnShopReroll,
                                OnShopLeave);
                            return;
                        }

                        match.Submit(new ShopActionCommand(player.Id, ShopActionType.Leave));
                        continue;
                    default:
                        return;
                }
            }
        }

        private void OnRewardChosen(int index)
        {
            if (!awaitingRewardChoice ||
                match == null ||
                match.State.Phase != MatchPhase.AwaitReward)
            {
                return;
            }

            MatchPlayerState player = match.State.CurrentPlayer;
            if (index < 0 || index >= match.CurrentRewardChoices.Count)
            {
                return;
            }

            int size = match.CurrentRewardChoices[index].SlotSize;
            if (player.OwnedCardWidth + size > LoadoutRules.BagCapacity)
            {
                return;
            }

            string chosenName = match.CurrentRewardChoices[index].DisplayName;
            awaitingRewardChoice = false;
            hud?.HideRewardChoices();
            match.Submit(new ChooseRewardCommand(player.Id, index));
            FinishDeferredPhase(string.Concat(
                "玩家 ",
                (player.Id + 1).ToString(),
                " 获得 ",
                chosenName));
        }

        private void OnShopBuy(int offerIndex)
        {
            if (!awaitingShop ||
                match == null ||
                match.State.Phase != MatchPhase.AwaitShop ||
                match.CurrentShop == null)
            {
                return;
            }

            MatchPlayerState player = match.State.CurrentPlayer;
            if (offerIndex < 0 || offerIndex >= match.CurrentShop.Offers.Count)
            {
                return;
            }

            ShopOffer offer = match.CurrentShop.Offers[offerIndex];
            if (!offer.IsAvailable ||
                player.Shards < offer.Price ||
                player.OwnedCardWidth + offer.Definition.SlotSize >
                    LoadoutRules.BagCapacity)
            {
                return;
            }

            try
            {
                match.Submit(new ShopActionCommand(
                    player.Id,
                    ShopActionType.Buy,
                    offerIndex));
            }
            catch (InvalidOperationException exception)
            {
                hud?.SetMatchStatus(string.Concat("买不了：", exception.Message));
                return;
            }

            hud?.SetMatchStatus(string.Concat(
                "玩家 ",
                (player.Id + 1).ToString(),
                " 买下 ",
                offer.Definition.DisplayName,
                "（-",
                offer.Price.ToString(),
                " 碎片）"));
            hud?.RefreshCardBand(HumanPlayer);
            RefreshShopPanel();
        }

        private void OnShopReroll()
        {
            if (!awaitingShop ||
                match == null ||
                match.State.Phase != MatchPhase.AwaitShop ||
                match.CurrentShop == null)
            {
                return;
            }

            MatchPlayerState player = match.State.CurrentPlayer;
            if (match.CurrentShop.RerollUsed ||
                player.Shards < MatchController.ShopRerollCost)
            {
                return;
            }

            try
            {
                match.Submit(new ShopActionCommand(player.Id, ShopActionType.Reroll));
            }
            catch (InvalidOperationException exception)
            {
                hud?.SetMatchStatus(string.Concat("重掷失败：", exception.Message));
                return;
            }

            hud?.SetMatchStatus("货架已重掷（-2 碎片）");
            RefreshShopPanel();
        }

        private void OnShopLeave()
        {
            if (!awaitingShop ||
                match == null ||
                match.State.Phase != MatchPhase.AwaitShop)
            {
                return;
            }

            MatchPlayerState player = match.State.CurrentPlayer;
            awaitingShop = false;
            hud?.HideShop();
            match.Submit(new ShopActionCommand(player.Id, ShopActionType.Leave));
            FinishDeferredPhase(string.Concat(
                "玩家 ",
                (player.Id + 1).ToString(),
                " 离开商店"));
        }

        /// <summary>买完 / 重掷后按当前货架重画；商店已结束就收起面板。</summary>
        private void RefreshShopPanel()
        {
            if (match.State.Phase == MatchPhase.AwaitShop &&
                match.CurrentShop != null)
            {
                MatchPlayerState player = match.State.CurrentPlayer;
                hud?.ShowShop(
                    player,
                    match.CurrentShop,
                    OnShopBuy,
                    OnShopReroll,
                    OnShopLeave);
                hud?.RefreshPlayerPanels(match.State);
                return;
            }

            awaitingShop = false;
            hud?.HideShop();
            FinishDeferredPhase(null);
        }

        private void FinishDeferredPhase(string message)
        {
            awaitingShop = false;
            hud?.HideShop();
            ResolveDeferredCorePhase();
            isMoving = false;
            automaticTurn = false;
            hud?.SetDiceInteractable(
                !match.IsFinished &&
                IsHumanSeat(match.State.CurrentPlayer) &&
                match.State.Phase == MatchPhase.AwaitRoll);
            ActivateCurrentPlayer();
            UpdateHudStatus(message);
        }

        private static string NodeLabel(int nodeId)
        {
            Vector2Int cell = UnityMapGraphFactory.CellFromNodeId(nodeId);
            return "R" + (cell.x + 1).ToString("D2") + " C" +
                (cell.y + 1).ToString("D2");
        }

        private int FindFirstFittingReward(MatchPlayerState player)
        {
            for (int index = 0; index < match.CurrentRewardChoices.Count; index++)
            {
                int size = match.CurrentRewardChoices[index].SlotSize;
                if (player.OwnedCardWidth + size <= LoadoutRules.BagCapacity)
                {
                    return index;
                }
            }

            return -1;
        }

        private IReadOnlyList<BoardForkChoice> CreateForkChoices(
            int currentNodeId,
            IReadOnlyList<int> edges)
        {
            List<BoardForkChoice> choices = new List<BoardForkChoice>(edges.Count);
            Vector2Int currentCell = UnityMapGraphFactory.CellFromNodeId(currentNodeId);

            foreach (int edge in edges)
            {
                Vector2Int targetCell = UnityMapGraphFactory.CellFromNodeId(edge);
                BoardTileView targetTile = tilesByNode[edge];
                Vector3 screen = Camera.main.WorldToScreenPoint(
                    targetTile.transform.position + Vector3.up * 1.2f);
                choices.Add(
                    new BoardForkChoice(
                        screen,
                        GetArrowSymbol(currentCell, targetCell)));
            }

            return choices;
        }

        private IEnumerator AnimateStep(
            BoardPlayerToken token,
            BoardTileView from,
            BoardTileView to)
        {
            Vector3 start = GetStandingPosition(token, from);
            Vector3 end = GetStandingPosition(token, to);
            float elapsed = 0f;

            while (elapsed < stepDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / stepDuration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                Vector3 position = Vector3.LerpUnclamped(start, end, eased);
                position.y += Mathf.Sin(progress * Mathf.PI) * hopHeight;
                token.transform.position = position;
                yield return null;
            }

            token.transform.position = end;
        }

        private void LogPlayerCells(string tag)
        {
            if (match == null)
            {
                return;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(
                "[Board] ");

            builder.Append(tag);

            foreach (MatchPlayerState player in match.State.Players)
            {
                builder.Append("  |  p");
                builder.Append((player.Id + 1).ToString());
                builder.Append('@');
                builder.Append(NodeLabel(player.CurrentNode));

                if (graph != null)
                {
                    builder.Append('(');
                    builder.Append(graph.GetNode(player.CurrentNode).TileType.ToString());
                    builder.Append(')');
                }
            }

            Debug.Log(builder.ToString());
        }

        private void SynchronizeTokensFromCore()
        {
            foreach (MatchPlayerState player in match.State.Players)
            {
                if (!tokensByPlayer.TryGetValue(player.Id, out BoardPlayerToken token) ||
                    !tilesByNode.TryGetValue(player.CurrentNode, out BoardTileView tile))
                {
                    continue;
                }

                token.transform.position = GetStandingPosition(token, tile);
                Vector2Int cell = UnityMapGraphFactory.CellFromNodeId(player.CurrentNode);
                token.SetBoardCell(cell.x, cell.y);
            }
        }

        private void ActivateCurrentPlayer()
        {
            if (match == null || match.IsFinished)
            {
                hud?.SetDiceInteractable(false);
                return;
            }

            MatchPlayerState actor = match.State.CurrentPlayer;
            if (humanSeat < 0)
            {
                // 无 CPU 测试模式：卡槽跟着当前行动者走，四个座位都由玩家一人操作。
                humanPlayerId = actor.Id;
            }

            ScheduleCpuTurn(actor);

            if (!tokensByPlayer.TryGetValue(actor.Id, out activeToken))
            {
                return;
            }

            cameraController?.SetFollowTarget(
                cameraFollowEnabled ? activeToken.transform : null);

            hud?.SetDiceInteractable(
                !match.IsFinished &&
                !awaitingRewardChoice &&
                !awaitingShop &&
                IsHumanSeat(actor) &&
                match.State.Phase == MatchPhase.AwaitRoll);
        }

        /// <summary>当前席位是否由玩家操作（无 CPU 测试模式下恒为真）。</summary>
        private bool IsHumanSeat(MatchPlayerState player)
        {
            return humanSeat < 0 || player == null || player.Id == humanSeat;
        }

        /// <summary>
        /// 给每个非真人席位建一个 AI。人格按座位号确定性分配（R-CPU-004），
        /// 难度取设置里的档位；无 CPU 测试模式（humanSeat < 0）下没有席位需要 AI。
        /// </summary>
        private void BuildCpuBrains()
        {
            cpuBrains.Clear();
            if (match == null || humanSeat < 0)
            {
                return;
            }

            foreach (MatchPlayerState player in match.State.Players)
            {
                if (player.Id == humanSeat)
                {
                    continue;
                }

                // 准备房间里每个座位都可以单独挑风格与难度；没有房间信息时
                // （例如直接在编辑器里开对局场景）回退到按座位号确定性分配。
                CpuPersonality personality = LobbyState.PersonalityFor(player.Id);
                cpuBrains[player.Id] = new CpuBrain(
                    player.Id,
                    personality,
                    MatchSeed.Derive(activeSeed, 0x43505500UL + (ulong)player.Id));
                Debug.Log(string.Concat(
                    "[Board] cpu seat ", player.Id.ToString(),
                    " personality=", personality.Label));
            }

            Debug.Log(string.Concat(
                "[Board] cpu brains ", cpuBrains.Count.ToString(),
                " map=", GameSession.SelectedMapId));
        }

        /// <summary>状态行里显示的 CPU 人格标签。</summary>
        private string CpuLabel(MatchPlayerState player)
        {
            CpuBrain brain;
            return cpuBrains.TryGetValue(player.Id, out brain) && brain != null
                ? brain.Personality.Label
                : "CPU";
        }

        /// <summary>轮到 CPU 时把骰子锁掉，并排一次带表现延迟的自动行动。</summary>
        private void ScheduleCpuTurn(MatchPlayerState actor)
        {
            if (actor == null || match.IsFinished || IsHumanSeat(actor))
            {
                cpuActAfter = 0f;
                return;
            }

            cpuActAfter = Time.unscaledTime +
                UnityEngine.Random.Range(CpuThinkMinSeconds, CpuThinkMaxSeconds);
            hud?.SetDiceInteractable(false);
            hud?.SetMatchStatus(string.Concat(
                "CPU P", (actor.Id + 1).ToString(),
                "（", CpuLabel(actor), "）正在思考…"));
        }

        private void Update()
        {
            if (!Application.isPlaying || match == null || match.IsFinished)
            {
                return;
            }

            if (isMoving || awaitingFork || awaitingRewardChoice ||
                awaitingShop || awaitingEncounterAcceptance)
            {
                return;
            }

            MatchPlayerState actor = match.State.CurrentPlayer;
            if (IsHumanSeat(actor) || match.State.Phase == MatchPhase.AwaitMove)
            {
                return;
            }

            if (Time.unscaledTime < cpuActAfter)
            {
                return;
            }

            cpuActAfter = Time.unscaledTime +
                UnityEngine.Random.Range(CpuThinkMinSeconds, CpuThinkMaxSeconds);
            AdvanceCpuTurn(actor);
        }

        /// <summary>
        /// CPU 的一步：让 AI 看当前信息集做出决策，再把决策翻成一条合法命令。
        /// AI 只能读 <see cref="CpuObservation"/>（公共信息 + 自己的卡槽），也只能 Submit，
        /// 不碰 state —— 这是 R-CPU-001 / R-CPU-002 的硬性要求。
        /// </summary>
        private void AdvanceCpuTurn(MatchPlayerState actor)
        {
            CpuBrain brain;
            if (!cpuBrains.TryGetValue(actor.Id, out brain) || brain == null)
            {
                return;
            }

            CpuDecision decision = brain.Decide(CpuObservation.Capture(match));
            switch (decision.Action)
            {
                case CpuAction.Roll:
                    automaticTurn = true;
                    isMoving = true;
                    hud?.SetDiceInteractable(false);
                    match.Submit(new RollCommand(actor.Id));
                    int roll = match.State.LastRoll;
                    hud?.SetDiceResult(roll);
                    UpdateHudStatus(string.Concat(
                        "CPU P", (actor.Id + 1).ToString(),
                        "（", brain.Personality.Label, "）掷出 ", roll.ToString()));

                    // 掷完才轮到"走哪条路"，此时 Phase 已经是 AwaitMove，重新观察一次。
                    CpuDecision move = brain.Decide(CpuObservation.Capture(match));
                    StartCoroutine(PlanAndSubmitMove(
                        actor.Id,
                        roll,
                        move.Action == CpuAction.Move ? move : null));
                    break;
                case CpuAction.Move:
                    // 没有先掷骰就拿到 Move（不该发生）：丢掉，下一拍重新决策。
                    break;
                case CpuAction.SetNorma:
                    match.Submit(new SetNormaCommand(actor.Id, decision.Norma));
                    UpdateHudStatus(string.Concat(
                        "CPU P", (actor.Id + 1).ToString(), " 选了",
                        decision.Norma == NormaType.Shard ? "碎片" : "胜场",
                        "路线"));
                    break;
                case CpuAction.ChooseReward:
                    if (decision.Index >= 0 &&
                        decision.Index < match.CurrentRewardChoices.Count)
                    {
                        match.Submit(new ChooseRewardCommand(actor.Id, decision.Index));
                    }

                    UpdateHudStatus();
                    break;
                case CpuAction.ShopBuy:
                    match.Submit(new ShopActionCommand(
                        actor.Id, ShopActionType.Buy, decision.Index));
                    UpdateHudStatus();
                    break;
                case CpuAction.ShopReroll:
                    match.Submit(new ShopActionCommand(
                        actor.Id, ShopActionType.Reroll, -1));
                    UpdateHudStatus();
                    break;
                case CpuAction.ShopLeave:
                    match.Submit(new ShopActionCommand(
                        actor.Id, ShopActionType.Leave, -1));
                    UpdateHudStatus();
                    break;
                case CpuAction.EquipCard:
                    match.Submit(new EquipCardCommand(actor.Id, decision.CardUid));
                    UpdateHudStatus();
                    break;
            }
        }

        private void UpdateHudStatus(string action = null)
        {
            if (match == null || hud == null)
            {
                return;
            }

            if (match.IsFinished)
            {
                hud.SetMatchStatus(
                    $"对局结束  ·  玩家 {match.State.WinnerPlayerId + 1} 获胜");
                hud.PumpEvents(match);
                return;
            }

            MatchPlayerState player = match.State.CurrentPlayer;
            int battleCapacity = player.Level <= 5
                ? LoadoutRules.GetBattleCapacity(player.Level)
                : 10;
            string status =
                $"{GameSession.CurrentModeLabel}  ·  " +
                $"回合 {match.State.TurnNumber}  ·  轮次 {match.State.RoundNumber}  ·  " +
                $"玩家 {player.Id + 1}  ·  碎片 {player.Shards}  ·  胜场 {player.Wins}  ·  " +
                $"装配 {player.BattleCardWidth}/{battleCapacity}c  ·  " +
                $"仓库 {player.OwnedCardWidth}/{LoadoutRules.BagCapacity}c";
            if (!string.IsNullOrWhiteSpace(action))
            {
                status += $"  ·  {action}";
            }

            hud.SetMatchStatus(status);
            // 卡槽带永远只显示本地玩家自己的藏品：轮到别人时也不能变成别人的牌，
            // 这样玩家在自己的回合之外也能整理装配（R-TURN-002 免费动作）。
            hud.ShowCards(HumanPlayer);
            hud.SetAssemblyLevel(player.Level);
            hud.RefreshPlayerPanels(match.State);
            hud.PumpEvents(match);
        }

        private int ResolveEntryPlayerId()
        {
            switch (GameEntryContext.EntryAngleDegrees)
            {
                case 0:
                    return 1;
                case 90:
                    return 0;
                case 180:
                    return 3;
                case 270:
                    return 2;
                default:
                    return 0;
            }
        }

        private static string GetArrowSymbol(Vector2Int from, Vector2Int to)
        {
            int rowDelta = to.x - from.x;
            int columnDelta = to.y - from.y;
            if (Mathf.Abs(columnDelta) > Mathf.Abs(rowDelta))
            {
                return columnDelta > 0 ? "→" : "←";
            }

            return rowDelta > 0 ? "↓" : "↑";
        }

        private static Vector3 GetStandingPosition(
            BoardPlayerToken token,
            BoardTileView tile)
        {
            Vector3 tilePosition = tile.transform.position;
            float tileTop = tile.transform.lossyScale.y * 0.5f;
            float tokenHalfHeight = token.transform.lossyScale.y * 0.5f;
            tilePosition.y += tileTop + tokenHalfHeight + 0.01f;
            return tilePosition;
        }
    }
}
