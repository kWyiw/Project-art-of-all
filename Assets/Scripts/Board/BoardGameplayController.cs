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
        [SerializeField] private ulong matchSeed = 20261031UL;

        /// <summary>CPU 每一步之间的表现延迟（秒）。</summary>
        private const float CpuThinkSeconds = 0.7f;

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
        private bool awaitingEncounterAcceptance;

        /// <summary>真人席位数：正常模式固定 0；无CPU测试模式为 -1（表示"当前行动者"）。</summary>
        private int humanSeat = -1;

        /// <summary>本回合由 CPU 自动推进：跳过岔路弹窗与遭遇确认，岔路取合法列表第一项。</summary>
        private bool automaticTurn;

        /// <summary>CPU 下一次行动的时间点（表现延迟，便于看清它做了什么）。</summary>
        private float cpuActAfter;

        private void Start()
        {
            BuildSceneLookup();
            graph = UnityMapGraphFactory.Build(tilesByNode.Values);
            pathLines = BoardPathLines.Build(transform, graph, tilesByNode);
            // 正常模式让真人先手（从主页面进来的第一回合就能操作）；
            // 无CPU测试模式没有固定真人，沿用准备室选的门。
            int startingPlayer = GameSession.HumanSeat >= 0
                ? GameSession.HumanSeat
                : ResolveEntryPlayerId();
            match = new MatchController(
                graph,
                matchSeed,
                startingPlayerId: startingPlayer);

            cameraController = Camera.main != null
                ? Camera.main.GetComponent<BoardCameraController>()
                : null;
            humanSeat = GameSession.HumanSeat;
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
        public void SubmitCardMove(int cardUid, bool toBattle, int index)
        {
            if (match == null || match.IsFinished)
            {
                return;
            }

            try
            {
                match.Submit(new MoveCardCommand(humanPlayerId, cardUid, toBattle, index));
            }
            catch (System.InvalidOperationException error)
            {
                hud?.SetMatchStatus(string.Concat("卡牌放不下：", error.Message));
                return;
            }

            UpdateHudStatus();
        }

        /// <summary>拖进出售区松手 = 立即出售，无确认无撤销（UI-4.1.1）。</summary>
        public void SubmitSellCard(int cardUid)
        {
            if (match == null || match.IsFinished)
            {
                return;
            }

            try
            {
                match.Submit(new SellCardCommand(humanPlayerId, cardUid));
            }
            catch (System.InvalidOperationException error)
            {
                hud?.SetMatchStatus(string.Concat("出售失败：", error.Message));
                return;
            }

            UpdateHudStatus();
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

        private IEnumerator PlanAndSubmitMove(int playerId, int steps)
        {
            isMoving = true;
            hud?.SetDiceInteractable(false);
            cameraController?.SetPlayerMoving(true);

            MatchPlayerState movingPlayer = match.State.CurrentPlayer;
            int current = movingPlayer.CurrentNode;
            int previous = movingPlayer.LastArrivalNode;
            List<int> forkDirections = new List<int>();
            int encounterNode = -1;

            for (int step = 0; step < steps; step++)
            {
                MapNode node = graph.GetNode(current);
                IReadOnlyList<int> legalEdges = graph.LegalNextNodes(current, previous);
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
                    // CPU 与无 CPU 测试模式下的自动推进：取合法列表第一项（内核会照常校验）。
                    selectedEdgeIndex = 0;
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
                    targetNodeId: encounterNode >= 0 ? encounterNode : (int?)null,
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
            ResolveDeferredCorePhase();
            isMoving = false;
            automaticTurn = false;
            hud?.SetDiceInteractable(
                !match.IsFinished &&
                !awaitingRewardChoice &&
                IsHumanSeat(match.State.CurrentPlayer) &&
                match.State.Phase == MatchPhase.AwaitRoll);
            ActivateCurrentPlayer();

            CombatResolvedEvent combat = match.Events.History
                .Skip(eventStart)
                .OfType<CombatResolvedEvent>()
                .LastOrDefault();
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
            if (awaitingRewardChoice)
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

        private void FinishDeferredPhase(string message)
        {
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
                IsHumanSeat(actor) &&
                match.State.Phase == MatchPhase.AwaitRoll);
        }

        /// <summary>当前席位是否由玩家操作（无 CPU 测试模式下恒为真）。</summary>
        private bool IsHumanSeat(MatchPlayerState player)
        {
            return humanSeat < 0 || player == null || player.Id == humanSeat;
        }

        /// <summary>轮到 CPU 时把骰子锁掉，并排一次带表现延迟的自动行动。</summary>
        private void ScheduleCpuTurn(MatchPlayerState actor)
        {
            if (actor == null || match.IsFinished || IsHumanSeat(actor))
            {
                cpuActAfter = 0f;
                return;
            }

            cpuActAfter = Time.unscaledTime + CpuThinkSeconds;
            hud?.SetDiceInteractable(false);
            hud?.SetMatchStatus(string.Concat(
                "CPU P", (actor.Id + 1).ToString(), " 正在行动…"));
        }

        private void Update()
        {
            if (!Application.isPlaying || match == null || match.IsFinished)
            {
                return;
            }

            if (isMoving || awaitingFork || awaitingRewardChoice || awaitingEncounterAcceptance)
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

            cpuActAfter = Time.unscaledTime + CpuThinkSeconds;
            AdvanceCpuTurn(actor);
        }

        /// <summary>
        /// CPU 的一步：掷骰走正常表现协程（岔路取第一项、不弹遭遇确认），
        /// 其余阶段直接替它选最省事的那一项。
        /// </summary>
        private void AdvanceCpuTurn(MatchPlayerState actor)
        {
            switch (match.State.Phase)
            {
                case MatchPhase.AwaitRoll:
                    automaticTurn = true;
                    isMoving = true;
                    hud?.SetDiceInteractable(false);
                    match.Submit(new RollCommand(actor.Id));
                    int roll = match.State.LastRoll;
                    hud?.SetDiceResult(roll);
                    UpdateHudStatus($"CPU P{actor.Id + 1} 掷出 {roll}");
                    StartCoroutine(PlanAndSubmitMove(actor.Id, roll));
                    break;
                case MatchPhase.AwaitNorma:
                    match.Submit(new SetNormaCommand(actor.Id, NormaType.Shard));
                    UpdateHudStatus();
                    break;
                case MatchPhase.AwaitReward:
                    int rewardIndex = FindFirstFittingReward(actor);
                    if (rewardIndex >= 0)
                    {
                        match.Submit(new ChooseRewardCommand(actor.Id, rewardIndex));
                    }

                    UpdateHudStatus();
                    break;
                case MatchPhase.AwaitShop:
                    match.Submit(new ShopActionCommand(actor.Id, ShopActionType.Leave));
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
            hud.ShowCards(player);
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
