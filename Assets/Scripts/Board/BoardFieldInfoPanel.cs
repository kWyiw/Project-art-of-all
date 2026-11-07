using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Core.Match;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 场地信息卡（§2.1）：常驻左上角，显示地图名、当前轮次/手数、本局全场规则。
    /// 场地事件层还没接进来，这里先把"每 N 轮全员发牌"这类固定规则摆出来，
    /// 后续 <c>MatchState</c> 有了事件列表，直接往 <see cref="eventLine"/> 上叠。
    /// </summary>
    public sealed class BoardFieldInfoPanel : MonoBehaviour
    {
        private const float PanelWidth = 360f;
        private const float PanelHeight = 158f;
        private const float Margin = 16f;
        private const float Top = 222f;

        private static readonly Color PanelColor = new Color(0.04f, 0.07f, 0.08f, 0.78f);
        private static readonly Color BrassColor = new Color(0.94f, 0.74f, 0.32f, 1f);
        private static readonly Color TextColor = new Color(0.90f, 0.91f, 0.87f, 1f);
        private static readonly Color MutedColor = new Color(0.66f, 0.68f, 0.66f, 1f);

        [SerializeField] private Text titleLabel;
        [SerializeField] private Text turnLabel;
        [SerializeField] private Text ruleLabel;
        [SerializeField] private Text normaLabel;
        [SerializeField] private Text hintLabel;

        public static BoardFieldInfoPanel Create(RectTransform parent, Font font)
        {
            GameObject panelObject = BoardHudOverlayFactory.CreateBar(
                parent,
                "BoardFieldInfoPanel",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(Margin, -Top - PanelHeight),
                new Vector2(Margin + PanelWidth, -Top),
                PanelColor);

            BoardFieldInfoPanel panel = panelObject.AddComponent<BoardFieldInfoPanel>();
            panel.Build(font);
            return panel;
        }

        private void Build(Font font)
        {
            titleLabel = BoardHudOverlayFactory.CreateText(
                transform, "Title", "场地 · —", font, 16, FontStyle.Bold,
                new Vector2(12f, -8f), new Vector2(PanelWidth - 24f, 22f), TextAnchor.UpperLeft);
            titleLabel.color = BrassColor;

            turnLabel = BoardHudOverlayFactory.CreateText(
                transform, "Turn", "第 1 轮 · 第 1 手", font, 13, FontStyle.Normal,
                new Vector2(12f, -32f), new Vector2(PanelWidth - 24f, 20f), TextAnchor.UpperLeft);
            turnLabel.color = TextColor;

            ruleLabel = BoardHudOverlayFactory.CreateText(
                transform, "Rules", string.Empty, font, 12, FontStyle.Normal,
                new Vector2(12f, -56f), new Vector2(PanelWidth - 24f, 62f), TextAnchor.UpperLeft);
            ruleLabel.color = MutedColor;
            ruleLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            ruleLabel.lineSpacing = 1.14f;

            normaLabel = BoardHudOverlayFactory.CreateText(
                transform, "Norma", "你的目标：—", font, 12, FontStyle.Normal,
                new Vector2(12f, -120f), new Vector2(PanelWidth - 24f, 18f), TextAnchor.UpperLeft);
            normaLabel.color = TextColor;

            hintLabel = BoardHudOverlayFactory.CreateText(
                transform, "Hint", "鼠标悬停地块查看效果", font, 11, FontStyle.Normal,
                new Vector2(12f, -138f), new Vector2(PanelWidth - 24f, 16f), TextAnchor.UpperLeft);
            hintLabel.color = MutedColor;
        }

        /// <summary>每次状态刷新都调；只改文本，不重建对象。</summary>
        public void Refresh(MatchState state, string mapName, int humanPlayerId)
        {
            if (state == null)
            {
                return;
            }

            if (titleLabel != null)
            {
                titleLabel.text = "场地 · " + (string.IsNullOrEmpty(mapName) ? "未知地图" : mapName);
            }

            if (turnLabel != null)
            {
                turnLabel.text = "第 " + state.RoundNumber + " 轮 · 第 " + state.TurnNumber + " 手";
            }

            if (ruleLabel != null)
            {
                ruleLabel.text =
                    "每 " + MatchController.GlobalGrantInterval + " 轮：全员各得 " +
                    MatchController.GlobalShardGrant + " 碎片\n" +
                    "踩到奖励/商店/怪物会当场结算\n" +
                    "某家升到 4 级后所有怪物变首领";
            }

            if (normaLabel != null)
            {
                MatchPlayerState human = state.Players.FirstOrDefault(
                    player => player.Id == humanPlayerId);
                if (human == null)
                {
                    normaLabel.text = "你的目标：—";
                }
                else
                {
                    int shardTarget = NormaRules.GetThreshold(human.Level, NormaType.Shard);
                    int winTarget = NormaRules.GetThreshold(human.Level, NormaType.Wins);
                    normaLabel.text =
                        "你的目标：碎片 " + human.Shards + "/" + shardTarget +
                        " 或 胜场 " + human.Wins + "/" + winTarget;
                }
            }
        }
    }

    /// <summary>
    /// 地块悬停说明（§2.1）：不用碰撞体，把鼠标射线打到棋盘平面上，
    /// 再取最近的地块，因此场景板与运行时生成的板都能用。
    /// </summary>
    public class BoardTileTooltip : MonoBehaviour
    {
        private const float MaxWidth = 340f;
        private const float PaddingX = 12f;
        private const float PaddingY = 10f;
        private const int TitleSize = 17;
        private const int BodySize = 13;
        private const float CursorOffsetX = 18f;
        private const float CursorOffsetY = -18f;
        private const float EdgeMargin = 8f;

        private static readonly Color PanelColor = new Color(0.075f, 0.068f, 0.058f, 0.98f);
        private static readonly Color FrameColor = new Color(0.85f, 0.63f, 0.25f, 1f);
        private static readonly Color TitleColor = new Color(0.97f, 0.94f, 0.86f, 1f);
        private static readonly Color BodyColor = new Color(0.90f, 0.88f, 0.82f, 1f);

        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private CanvasGroup group;
        private BoardGameplayController gameplay;
        private bool visible;

        public static BoardTileTooltip Create(RectTransform parent, Font font)
        {
            GameObject rootObject = new GameObject(
                "BoardTileTooltip",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline),
                typeof(CanvasGroup));
            rootObject.transform.SetParent(parent, false);

            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(MaxWidth, 74f);

            Image image = rootObject.GetComponent<Image>();
            image.color = PanelColor;
            image.raycastTarget = false;

            Outline outline = rootObject.GetComponent<Outline>();
            outline.effectColor = FrameColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            BoardTileTooltip tooltip =
                rootObject.AddComponent<BoardTileTooltipSceneView>();
            tooltip.root = rect;
            tooltip.canvasRect = parent;
            tooltip.group = rootObject.GetComponent<CanvasGroup>();
            tooltip.group.alpha = 0f;
            tooltip.group.blocksRaycasts = false;
            tooltip.group.interactable = false;
            tooltip.Build(font);
            return tooltip;
        }

        private void Build(Font font)
        {
            titleLabel = BoardHudOverlayFactory.CreateText(
                transform, "Title", "地块", font, TitleSize, FontStyle.Bold,
                new Vector2(PaddingX, -PaddingY), new Vector2(MaxWidth - PaddingX * 2f, 24f),
                TextAnchor.UpperLeft);
            titleLabel.color = TitleColor;

            bodyLabel = BoardHudOverlayFactory.CreateText(
                transform, "Body", string.Empty, font, BodySize, FontStyle.Normal,
                new Vector2(PaddingX, -PaddingY - 24f),
                new Vector2(MaxWidth - PaddingX * 2f, 60f), TextAnchor.UpperLeft);
            bodyLabel.color = BodyColor;
            bodyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyLabel.lineSpacing = 1.12f;
        }

        public void Bind(BoardGameplayController controller)
        {
            gameplay = controller;
        }

        private void Update()
        {
            if (gameplay == null)
            {
                Hide();
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // 指针在 HUD 面板/卡槽带上时不出地块说明，否则会跟面板叠在一起。
                Hide();
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                Hide();
                return;
            }

            if (!TryFindTileAt(camera, out BoardTileView tile))
            {
                Hide();
                return;
            }

            Show(tile);
        }

        /// <summary>把射线打到地块所在的水平面上，再取棋盘里离落点最近的一块。</summary>
        private bool TryFindTileAt(Camera camera, out BoardTileView tile)
        {
            tile = null;
            IReadOnlyCollection<BoardTileView> tiles = gameplay.Tiles;
            if (tiles == null || tiles.Count == 0)
            {
                return false;
            }

            float planeHeight = 0.1f;
            Plane plane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            if (!plane.Raycast(ray, out float distance) || distance <= 0f)
            {
                return false;
            }

            Vector3 hit = ray.GetPoint(distance);
            float best = float.MaxValue;
            foreach (BoardTileView candidate in tiles)
            {
                if (candidate == null)
                {
                    continue;
                }

                Vector3 position = candidate.transform.position;
                float deltaX = position.x - hit.x;
                float deltaZ = position.z - hit.z;
                float distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
                if (distanceSquared < best)
                {
                    best = distanceSquared;
                    tile = candidate;
                }
            }

            // 半格以内才算命中，避免鼠标挪到棋盘外面还在弹说明。
            return tile != null && best <= 0.85f * 0.85f;
        }

        private void Show(BoardTileView tile)
        {
            if (root == null || titleLabel == null)
            {
                return;
            }

            string title = TitleOf(tile.TileType, tile.OwnerPlayerId);
            string body = BodyOf(tile.TileType);
            if (titleLabel.text != title)
            {
                titleLabel.text = title;
            }

            if (bodyLabel.text != body)
            {
                bodyLabel.text = body;
            }

            if (!visible)
            {
                if (group != null)
                {
                    group.alpha = 1f;
                }

                visible = true;
            }

            PlaceAt(Input.mousePosition);
        }

        private void PlaceAt(Vector2 screenPosition)
        {
            if (canvasRect == null)
            {
                return;
            }

            if (root == null)
            {
                return;
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPosition, null, out Vector2 local))
            {
                return;
            }

            Vector2 canvasSize = canvasRect.rect.size;
            float height = root.rect.height;
            Vector2 origin = new Vector2(canvasSize.x * 0.5f, canvasSize.y * 0.5f);
            Vector2 point = local + origin;

            float x = point.x + CursorOffsetX;
            float y = point.y + CursorOffsetY;
            if (x + root.rect.width > canvasSize.x - EdgeMargin)
            {
                x = point.x - CursorOffsetX - root.rect.width;
            }

            if (y - height < EdgeMargin)
            {
                y = point.y + Mathf.Abs(CursorOffsetY) + height;
            }

            x = Mathf.Clamp(x, EdgeMargin, Mathf.Max(EdgeMargin, canvasSize.x - root.rect.width - EdgeMargin));
            y = Mathf.Clamp(y, height + EdgeMargin, canvasSize.y - EdgeMargin);
            root.anchoredPosition = new Vector2(x, y);
        }

        private void Hide()
        {
            if (root == null || !visible)
            {
                return;
            }

            if (group != null)
            {
                group.alpha = 0f;
            }

            visible = false;
        }

        public static string TitleOf(TileType type, int ownerPlayerId)
        {
            switch (type)
            {
                case TileType.Home:
                    return ownerPlayerId >= 0
                        ? "家 · P" + (ownerPlayerId + 1)
                        : "家";
                case TileType.Reward:
                    return "奖励";
                case TileType.Shop:
                    return "商店";
                case TileType.Monster:
                    return "怪物";
                case TileType.Boss:
                    return "首领";
                case TileType.Warp:
                    return "传送";
                case TileType.Event:
                    return "事件";
                case TileType.Neutral:
                    return "空地";
                default:
                    return "地块";
            }
        }

        public static string BodyOf(TileType type)
        {
            switch (type)
            {
                case TileType.Home:
                    return "经过这里算一圈；达标时停在这里可以升级装配等级。";
                case TileType.Reward:
                    return "两张随机卡里挑一张带走，背包放不下就拿不到。";
                case TileType.Shop:
                    return "三张随机货可买，每件有标价；可花 2 碎片重掷一次。";
                case TileType.Monster:
                    return "打一场：赢了拿战利品和 1 点胜场，输了碎片减半。";
                case TileType.Boss:
                    return "打首领：赢了得 3 点胜场，输了碎片减半。";
                case TileType.Warp:
                    return "随机传送到另一处传送格，并在落点再结算一次。";
                case TileType.Event:
                    return "本章版本暂未配置效果。";
                case TileType.Neutral:
                    return "无事发生。";
                default:
                    return "无事发生。";
            }
        }
    }

    /// <summary>
    /// 回合横幅（§2.1）：轮次变化时在中上位置弹出一次并淡出。
    /// </summary>
    public class BoardRoundBanner : MonoBehaviour
    {
        private const float HoldSeconds = 1.1f;
        private const float FadeSeconds = 0.7f;

        private static readonly Color TitleColor = new Color(0.96f, 0.80f, 0.42f, 1f);
        private static readonly Color SubColor = new Color(0.82f, 0.84f, 0.80f, 1f);

        [SerializeField] private Text titleLabel;
        [SerializeField] private Text subLabel;
        [SerializeField] private CanvasGroup group;
        private float shownAt = -1f;

        public static BoardRoundBanner Create(RectTransform parent, Font font)
        {
            GameObject rootObject = new GameObject(
                "BoardRoundBanner",
                typeof(RectTransform),
                typeof(CanvasGroup));
            rootObject.transform.SetParent(parent, false);

            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -66f);
            rect.sizeDelta = new Vector2(520f, 76f);

            BoardRoundBanner banner =
                rootObject.AddComponent<BoardRoundBannerSceneView>();
            banner.group = rootObject.GetComponent<CanvasGroup>();
            banner.group.alpha = 0f;
            banner.group.blocksRaycasts = false;
            banner.group.interactable = false;
            banner.Build(font);
            return banner;
        }

        private void Build(Font font)
        {
            titleLabel = BoardHudOverlayFactory.CreateText(
                transform, "Title", string.Empty, font, 40, FontStyle.Bold,
                new Vector2(0f, 0f), new Vector2(520f, 46f), TextAnchor.MiddleCenter);
            titleLabel.color = TitleColor;
            titleLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            titleLabel.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            titleLabel.rectTransform.sizeDelta = new Vector2(0f, 46f);

            subLabel = BoardHudOverlayFactory.CreateText(
                transform, "Sub", string.Empty, font, 14, FontStyle.Normal,
                new Vector2(0f, -46f), new Vector2(520f, 20f), TextAnchor.MiddleCenter);
            subLabel.color = SubColor;
            subLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            subLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            subLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            subLabel.rectTransform.anchoredPosition = new Vector2(0f, -46f);
            subLabel.rectTransform.sizeDelta = new Vector2(0f, 20f);
        }

        public void Show(int round, string subtitle)
        {
            if (titleLabel == null)
            {
                return;
            }

            titleLabel.text = "第 " + round + " 轮";
            if (subLabel != null)
            {
                subLabel.text = subtitle ?? string.Empty;
            }

            shownAt = Time.unscaledTime;
            if (group != null)
            {
                group.alpha = 1f;
            }
        }

        private void Update()
        {
            if (group == null || shownAt < 0f)
            {
                return;
            }

            float elapsed = Time.unscaledTime - shownAt;
            if (elapsed <= HoldSeconds)
            {
                group.alpha = 1f;
                return;
            }

            float fade = (elapsed - HoldSeconds) / FadeSeconds;
            group.alpha = Mathf.Clamp01(1f - fade);
            if (fade >= 1f)
            {
                shownAt = -1f;
                group.alpha = 0f;
            }
        }
    }
}
