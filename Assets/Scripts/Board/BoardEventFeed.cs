using System.Collections.Generic;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Events;
using ProjectArtOfAll.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 事件流：把内核抛出的 GameEvent 串行成一行行看得懂的播报，替代散落在各处的临时状态文案。
    /// 逻辑永远是内核算完的，这里只负责"什么时候给玩家看什么"，因此完全不影响规则。
    /// </summary>
    public sealed class BoardEventFeed : MonoBehaviour
    {
        private const int RowCount = 6;
        private const float ItemInterval = 0.42f;
        private const float RowHeight = 24f;

        private static readonly Color PlainColor = new Color(0.86f, 0.88f, 0.84f, 1f);
        private static readonly Color GainColor = new Color(0.55f, 0.85f, 0.52f, 1f);
        private static readonly Color LossColor = new Color(0.92f, 0.48f, 0.40f, 1f);
        private static readonly Color BrassColor = new Color(0.94f, 0.74f, 0.32f, 1f);
        private static readonly Color PanelColor = new Color(0.04f, 0.07f, 0.08f, 0.78f);

        private struct Entry
        {
            public string Text;
            public Color Color;
        }

        private readonly Queue<Entry> pending = new Queue<Entry>();
        private readonly List<Entry> shown = new List<Entry>();
        private readonly List<Text> rows = new List<Text>();
        private float nextAt;

        public static BoardEventFeed Create(RectTransform parent, Font font)
        {
            GameObject rootObject = new GameObject("BoardEventFeed", typeof(RectTransform));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -54f);
            rect.sizeDelta = new Vector2(360f, RowCount * RowHeight + 12f);
            rootObject.AddComponent<BoardEventFeed>().Build(font);
            return rootObject.GetComponent<BoardEventFeed>();
        }

        private void Build(Font font)
        {
            BoardHudOverlayFactory.CreateBar(
                transform, "Backdrop",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                PanelColor);

            Image backdrop = transform.Find("Backdrop").GetComponent<Image>();
            backdrop.raycastTarget = false;

            for (int i = 0; i < RowCount; i++)
            {
                Text row = BoardHudOverlayFactory.CreateText(
                    transform, "Row" + i, string.Empty, font, 13, FontStyle.Normal,
                    new Vector2(10f, -(8f + i * RowHeight)), new Vector2(340f, RowHeight),
                    TextAnchor.MiddleLeft);
                row.color = PlainColor;
                rows.Add(row);
            }
        }

        /// <summary>把一条播报塞进队列，按 ItemInterval 串行显示（避免一次结算刷掉前一条）。</summary>
        public void Enqueue(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            pending.Enqueue(new Entry { Text = text, Color = color });
        }

        public void Enqueue(string text)
        {
            Enqueue(text, PlainColor);
        }

        public void Clear()
        {
            pending.Clear();
            shown.Clear();
            RefreshRows();
        }

        private void Update()
        {
            if (pending.Count == 0)
            {
                return;
            }

            if (Time.unscaledTime < nextAt)
            {
                return;
            }

            Bring(pending.Dequeue());
            nextAt = Time.unscaledTime + ItemInterval;
        }

        private void Bring(Entry entry)
        {
            shown.Insert(0, entry);
            while (shown.Count > RowCount)
            {
                shown.RemoveAt(shown.Count - 1);
            }

            RefreshRows();
        }

        private void RefreshRows()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (i < shown.Count)
                {
                    Entry entry = shown[i];
                    rows[i].text = entry.Text;
                    Color color = entry.Color;
                    // 越旧越淡，最上面一条最亮。
                    color.a = i == 0 ? 1f : Mathf.Lerp(0.32f, 0.8f, 1f - (i / (float)RowCount));
                    rows[i].color = color;
                }
                else
                {
                    rows[i].text = string.Empty;
                }
            }
        }

        internal static Color ColorOf(GameEvent gameEvent)
        {
            if (gameEvent is ShardsChangedEvent shards)
            {
                return shards.Delta >= 0 ? GainColor : LossColor;
            }

            if (gameEvent is WinsChangedEvent wins)
            {
                return wins.Delta >= 0 ? GainColor : LossColor;
            }

            if (gameEvent is PlayerLeveledUpEvent)
            {
                return BrassColor;
            }

            if (gameEvent is RewardFailedEvent || gameEvent is CardSoldEvent)
            {
                return LossColor;
            }

            if (gameEvent is CombatResolvedEvent)
            {
                return BrassColor;
            }

            return PlainColor;
        }
    }

    /// <summary>GameEvent → 中文播报。纯展示，不参与任何判定。</summary>
    public static class BoardEventText
    {
        public static string Describe(GameEvent gameEvent, MatchController match)
        {
            switch (gameEvent)
            {
                case TurnStartedEvent turn:
                    return "第 " + turn.RoundNumber + " 轮 · 玩家 " + (turn.PlayerId + 1) + " 行动";
                case DiceRolledEvent dice:
                    return "玩家 " + (dice.PlayerId + 1) + " 掷出 " + dice.Value;
                case PlayerTeleportedEvent teleport:
                    return "传送：R" + (teleport.FromNodeId + 1).ToString("D2") + " → R"
                        + (teleport.ToNodeId + 1).ToString("D2");
                case ShardsChangedEvent shards:
                    return "玩家 " + (shards.PlayerId + 1) + " 碎片 " + Signed(shards.Delta)
                        + "（" + shards.NewValue + "）";
                case WinsChangedEvent wins:
                    return "玩家 " + (wins.PlayerId + 1) + " 胜场 " + Signed(wins.Delta)
                        + "（" + wins.NewValue + "）";
                case PlayerLeveledUpEvent level:
                    return "玩家 " + (level.PlayerId + 1) + " 升到 Lv." + level.NewLevel;
                case CardAddedEvent added:
                    return "玩家 " + (added.PlayerId + 1) + " 获得【" + CardName(added.CardId) + "】"
                        + SourceSuffix(added.Source);
                case CardSoldEvent sold:
                    return "玩家 " + (sold.PlayerId + 1) + " 卖掉【" + CardName(sold.CardId)
                        + "】+" + sold.Shards + " 碎片";
                case RewardFailedEvent failed:
                    return "玩家 " + (failed.PlayerId + 1) + " 拿不到奖励：" + ReasonText(failed.Reason);
                case GlobalShardsGrantedEvent granted:
                    return "全员 +" + granted.Amount + " 碎片";
                case BossEvolutionEvent boss:
                    return "玩家 " + (boss.TriggeredByPlayerId + 1) + " 触发 Boss 演化：怪物格全部变 BOSS";
                case CombatResolvedEvent combat:
                    return CombatText(combat);
                default:
                    return null;
            }
        }

        private static string CombatText(CombatResolvedEvent combat)
        {
            CombatResult result = combat.Result;
            string who = "玩家 " + (combat.AttackerId + 1) + " 对 玩家 " + (combat.DefenderId + 1);
            if (result.IsDraw)
            {
                return who + " 平局（" + result.EndSecond + " 秒）";
            }

            return "玩家 " + (result.WinnerPlayerId + 1) + " 获胜（"
                + Relative(who, result.WinnerPlayerId) + " · " + result.EndSecond + " 秒）";
        }

        private static string Relative(string label, int winnerId)
        {
            return label.Contains("玩家 " + (winnerId + 1)) ? "主动方赢" : "防守方赢";
        }

        private static string SourceSuffix(string source)
        {
            switch (source)
            {
                case "Reward":
                    return "（奖励格）";
                case "Shop":
                    return "（商店）";
                case "Start":
                    return "（起始牌组）";
                case "Generate":
                    return "（生成）";
                default:
                    return string.Empty;
            }
        }

        private static string ReasonText(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return "背包已满";
            }

            return reason.Contains("Bag") || reason.Contains("Full") || reason.Contains("Capacity")
                ? "背包已满（R-ECO-006）"
                : reason;
        }

        private static string CardName(string cardId)
        {
            CardDefinition definition = CardDatabase.Instance.Get(cardId);
            return definition == null ? cardId : definition.DisplayName;
        }

        private static string Signed(int value)
        {
            return value >= 0 ? "+" + value : value.ToString();
        }
    }
}
