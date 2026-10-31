using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Commands;
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

        [Header("Movement")]
        [SerializeField, Min(0.05f)] private float stepDuration = 0.2f;
        [SerializeField, Min(0f)] private float hopHeight = 0.18f;

        private readonly Dictionary<int, BoardTileView> tilesByNode =
            new Dictionary<int, BoardTileView>();
        private readonly Dictionary<int, BoardPlayerToken> tokensByPlayer =
            new Dictionary<int, BoardPlayerToken>();
        private readonly List<int> pendingForkEdges = new List<int>();

        private BoardHudController hud;
        private BoardCameraController cameraController;
        private BoardPlayerToken activeToken;
        private MapGraph graph;
        private MatchController match;
        private int pendingForkSelection = -1;
        private bool isMoving;
        private bool awaitingFork;
        private bool awaitingEncounterAcceptance;

        private void Start()
        {
            BuildSceneLookup();
            graph = UnityMapGraphFactory.Build(tilesByNode.Values);
            match = new MatchController(
                graph,
                matchSeed,
                startingPlayerId: ResolveEntryPlayerId());

            cameraController = Camera.main != null
                ? Camera.main.GetComponent<BoardCameraController>()
                : null;
            BindHud();
            SynchronizeTokensFromCore();
            ActivateCurrentPlayer();
            UpdateHudStatus();
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

            MatchPlayerState movingPlayer = match.State.CurrentPlayer;
            int current = movingPlayer.CurrentNode;
            List<int> forkDirections = new List<int>();
            int encounterNode = -1;

            for (int step = 0; step < steps; step++)
            {
                MapNode node = graph.GetNode(current);
                if (node.OutEdges.Count == 0)
                {
                    break;
                }

                int selectedEdgeIndex;
                if (node.OutEdges.Count == 1)
                {
                    selectedEdgeIndex = 0;
                }
                else
                {
                    pendingForkEdges.Clear();
                    pendingForkEdges.AddRange(node.OutEdges);
                    pendingForkSelection = -1;
                    awaitingFork = true;

                    hud?.ShowForkChoices(
                        CreateForkChoices(current, node.OutEdges),
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

                int next = node.OutEdges[selectedEdgeIndex];
                BoardTileView fromTile = tilesByNode[current];
                BoardTileView toTile = tilesByNode[next];
                UpdateHudStatus(
                    $"掷出 {match.State.LastRoll}  ·  移动 {step + 1}/{steps}");
                yield return AnimateStep(activeToken, fromTile, toTile);

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
                awaitingEncounterAcceptance =
                    hud != null &&
                    hud.ShowPlayerEncounter(
                        movingPlayer,
                        defender,
                        () => awaitingEncounterAcceptance = false);
                while (awaitingEncounterAcceptance)
                {
                    yield return null;
                }

                break;
            }

            int eventStart = match.Events.History.Count;
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
                activeToken.transform.position = GetStandingPosition(
                    activeToken,
                    tilesByNode[teleport.ToNodeId]);
                yield return new WaitForSeconds(0.12f);
            }

            SynchronizeTokensFromCore();
            ResolveDeferredCorePhase();
            isMoving = false;
            hud?.SetDiceInteractable(!match.IsFinished);
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
            UpdateHudStatus(action);
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
            for (int guard = 0; guard < 8; guard++)
            {
                MatchPlayerState player = match.State.CurrentPlayer;
                switch (match.State.Phase)
                {
                    case MatchPhase.AwaitNorma:
                        match.Submit(new SetNormaCommand(player.Id, NormaType.Shard));
                        continue;
                    case MatchPhase.AwaitReward:
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

            if (tokensByPlayer.TryGetValue(
                    match.State.CurrentPlayer.Id,
                    out activeToken))
            {
                cameraController?.SetFollowTarget(activeToken.transform);
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
                return;
            }

            MatchPlayerState player = match.State.CurrentPlayer;
            int battleCapacity = player.Level <= 5
                ? LoadoutRules.GetBattleCapacity(player.Level)
                : 10;
            string status =
                $"回合 {match.State.TurnNumber}  ·  轮次 {match.State.RoundNumber}  ·  " +
                $"玩家 {player.Id + 1}  ·  碎片 {player.Shards}  ·  胜场 {player.Wins}  ·  " +
                $"装配 {player.BattleCardWidth}/{battleCapacity}c  ·  " +
                $"仓库 {player.OwnedCardWidth}/{LoadoutRules.BagCapacity}c";
            if (!string.IsNullOrWhiteSpace(action))
            {
                status += $"  ·  {action}";
            }

            hud.SetMatchStatus(status);
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
