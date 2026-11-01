using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Match;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 棋盘层常驻双排卡槽带（`棋盘层双栏卡槽UI与拖拽交互规格.md` §2–§5）。
    /// 上排背包 10c、下排战斗栏 4/6/8/10c，按格位宽度排版；
    /// 拖到另一排 = 跨区移动，拖到同排插入位 = 重排，拖进出售区 = 立即出售。
    /// 所有落点都只是"提议"，真正生效的是 <see cref="MoveCardCommand"/> / <see cref="SellCardCommand"/>。
    /// 带高占画布下半部分，卡牌尺寸随带高等比放大。
    /// </summary>
    public sealed class CardBandController
    {
        public const float BaseBandHeight = 272f;
        public const int BaseSlotWidth = 88;
        public const int BaseCardHeight = 118;
        public const int BaseSlotGap = 8;

        private const float MaxScale = 2.2f;
        private const int HandleHeight = 30;

        private static readonly Color BagColor = new Color(0.14f, 0.22f, 0.27f, 0.97f);
        private static readonly Color BattleColor = new Color(0.30f, 0.20f, 0.12f, 0.97f);
        private static readonly Color Brass = new Color(0.92f, 0.69f, 0.27f, 1f);
        private static readonly Color TextColor = new Color(0.94f, 0.93f, 0.88f, 1f);
        private static readonly Color MutedColor = new Color(0.66f, 0.68f, 0.66f, 1f);
        private static readonly Color SellColor = new Color(0.20f, 0.16f, 0.10f, 0.96f);
        private static readonly Color SellHotColor = new Color(0.62f, 0.20f, 0.14f, 0.98f);

        private readonly BoardGameplayController gameplay;
        private readonly RectTransform root;
        private readonly RectTransform bagRow;
        private readonly RectTransform battleRow;
        private readonly RectTransform sellZone;
        private readonly RectTransform detailPanel;
        private readonly Text detailText;
        private readonly Text detailTitle;
        private readonly Image sellImage;
        private readonly Text sellLabel;
        private readonly Text bagLabel;
        private readonly Text battleLabel;
        private readonly Text battleCapacityLabel;
        private readonly Text handleLabel;
        private readonly Font font;

        private readonly List<GameObject> bagWidgets = new List<GameObject>();
        private readonly List<GameObject> battleWidgets = new List<GameObject>();

        private CardInstance dragging;
        private bool collapsed;
        private float scale = 1f;
        private MatchPlayerState lastPlayer;

        private CardBandController(BoardGameplayController gameplay, RectTransform root, Font font)
        {
            this.gameplay = gameplay;
            this.root = root;
            this.font = font;

            GameObject handleObject = BoardHudOverlayFactory.CreateBar(
                root,
                "Handle",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -HandleHeight),
                Vector2.zero,
                new Color(0.06f, 0.09f, 0.10f, 0.99f));
            handleLabel = BoardHudOverlayFactory.CreateText(
                handleObject.transform,
                "Label",
                "▾ 卡槽",
                font,
                16,
                FontStyle.Bold,
                new Vector2(18f, -2f),
                new Vector2(220f, 26f),
                TextAnchor.MiddleLeft);
            handleLabel.color = Brass;
            Image handleHit = handleObject.GetComponent<Image>();
            handleHit.raycastTarget = true;
            Button handleButton = handleObject.AddComponent<Button>();
            handleButton.transition = Selectable.Transition.None;
            handleButton.onClick.AddListener(ToggleCollapsed);

            bagLabel = CreateRowLabel(root, "背包");
            battleLabel = CreateRowLabel(root, "战斗");
            battleCapacityLabel = CreateRowCaption(root, "能力");
            bagRow = CreateRow(root, "Bag");
            battleRow = CreateRow(root, "Battle");

            GameObject sellObject = BoardHudOverlayFactory.CreateBar(
                root,
                "SellZone",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-104f, -114f),
                new Vector2(-24f, -34f),
                SellColor);
            sellZone = sellObject.GetComponent<RectTransform>();
            sellImage = sellObject.GetComponent<Image>();
            sellLabel = BoardHudOverlayFactory.CreateText(
                sellObject.transform,
                "Label",
                "出售",
                font,
                15,
                FontStyle.Bold,
                new Vector2(0f, -26f),
                new Vector2(80f, 22f),
                TextAnchor.MiddleCenter);
            sellLabel.color = MutedColor;

            GameObject detailObject = BoardHudOverlayFactory.CreateBar(
                root,
                "Detail",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-456f, 20f),
                new Vector2(-16f, 260f),
                new Color(0.048f, 0.078f, 0.09f, 0.98f));
            detailPanel = detailObject.GetComponent<RectTransform>();
            detailTitle = BoardHudOverlayFactory.CreateText(
                detailObject.transform,
                "Title",
                "卡牌",
                font,
                20,
                FontStyle.Bold,
                new Vector2(18f, -14f),
                new Vector2(404f, 30f),
                TextAnchor.MiddleLeft);
            detailTitle.color = Brass;
            detailText = BoardHudOverlayFactory.CreateText(
                detailObject.transform,
                "Body",
                string.Empty,
                font,
                15,
                FontStyle.Normal,
                new Vector2(18f, -50f),
                new Vector2(404f, 190f),
                TextAnchor.UpperLeft);
            detailText.color = TextColor;
            detailObject.SetActive(false);

            root.gameObject.AddComponent<CardBandLayoutDriver>().Bind(this);
        }

        public static CardBandController Create(BoardGameplayController gameplay, RectTransform parent, Font font)
        {
            GameObject rootObject = new GameObject("CardBand", typeof(RectTransform), typeof(Image));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, BaseBandHeight);
            rect.anchoredPosition = Vector2.zero;
            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0.03f, 0.05f, 0.06f, 0.94f);
            background.raycastTarget = false;
            return new CardBandController(gameplay, rect, font);
        }

        /// <summary>掷骰子按钮：由 HUD 注进来，跟着带高一起挪到带上方右侧的空地。</summary>
        public RectTransform DiceButtonTarget { get; set; }

        /// <summary>带高 = 画布高度的一半（盖满下半屏）。</summary>
        public void ApplyScreenLayout()
        {
            RectTransform canvasRect = root.parent as RectTransform;
            float canvasHeight = canvasRect != null && canvasRect.rect.height > 1f
                ? canvasRect.rect.height
                : Screen.height;
            float height = Mathf.Max(BaseBandHeight, canvasHeight * 0.5f);
            scale = Mathf.Clamp(height / BaseBandHeight, 1f, MaxScale);

            if (!collapsed)
            {
                SetBandHeight(height);
            }

            LayoutRows(height);
            PlaceDiceButton(collapsed ? HandleHeight + 12f : height);
            if (lastPlayer != null)
            {
                Refresh(lastPlayer);
            }
        }

        private void PlaceDiceButton(float bandHeight)
        {
            if (DiceButtonTarget == null)
            {
                return;
            }

            DiceButtonTarget.anchorMin = new Vector2(1f, 0f);
            DiceButtonTarget.anchorMax = new Vector2(1f, 0f);
            DiceButtonTarget.pivot = new Vector2(1f, 0f);
            DiceButtonTarget.anchoredPosition = new Vector2(-28f, bandHeight + 24f);
        }

        private void SetBandHeight(float height)
        {
            RectTransform rect = root;
            Vector2 max = rect.offsetMax;
            rect.offsetMax = new Vector2(max.x, height);
        }

        private void LayoutRows(float bandHeight)
        {
            float cardHeight = BaseCardHeight * scale;
            float top = HandleHeight + 16f;
            float rowWidth = root.rect.width;
            float available = bandHeight - HandleHeight - 24f;
            float rowGap = Mathf.Max(8f, (available - cardHeight * 2f) * 0.5f);

            bagLabel.rectTransform.anchoredPosition = new Vector2(14f, -top + 4f);
            bagRow.anchoredPosition = new Vector2(0f, -(top + 26f));
            bagRow.sizeDelta = new Vector2(0f, cardHeight);

            battleLabel.rectTransform.anchoredPosition = new Vector2(14f, -(top + cardHeight + rowGap + 26f));
            battleRow.anchoredPosition = new Vector2(0f, -(top + cardHeight + rowGap + 52f));
            battleRow.sizeDelta = new Vector2(0f, cardHeight);

            battleCapacityLabel.rectTransform.anchoredPosition =
                new Vector2(14f, -(top + cardHeight + rowGap + 26f) - 20f);
        }

        private RectTransform CreateRow(RectTransform parent, string name)
        {
            GameObject rowObject = new GameObject(name, typeof(RectTransform));
            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(0f, BaseCardHeight);
            row.anchoredPosition = Vector2.zero;
            return row;
        }

        private Text CreateRowLabel(RectTransform parent, string label)
        {
            Text text = BoardHudOverlayFactory.CreateText(
                parent,
                "Label" + label,
                label,
                font,
                15,
                FontStyle.Bold,
                new Vector2(14f, 0f),
                new Vector2(56f, 24f),
                TextAnchor.UpperLeft);
            text.color = label == "战斗" ? Brass : MutedColor;
            return text;
        }

        private Text CreateRowCaption(RectTransform parent, string label)
        {
            Text caption = BoardHudOverlayFactory.CreateText(
                parent,
                "Caption",
                string.Empty,
                font,
                12,
                FontStyle.Normal,
                new Vector2(14f, 0f),
                new Vector2(56f, 20f),
                TextAnchor.UpperLeft);
            caption.color = MutedColor;
            return caption;
        }

        public void ToggleCollapsed()
        {
            collapsed = !collapsed;
            handleLabel.text = collapsed ? "▸ 卡槽" : "▾ 卡槽";
            bagRow.gameObject.SetActive(!collapsed);
            battleRow.gameObject.SetActive(!collapsed);
            bagLabel.gameObject.SetActive(!collapsed);
            battleLabel.gameObject.SetActive(!collapsed);
            battleCapacityLabel.gameObject.SetActive(!collapsed);
            sellZone.gameObject.SetActive(!collapsed);
            if (collapsed)
            {
                detailPanel.gameObject.SetActive(false);
                SetBandHeight(HandleHeight + 12f);
            }
            else
            {
                ApplyScreenLayout();
            }
        }

        public void Refresh(MatchPlayerState player)
        {
            lastPlayer = player;
            ClearWidgets(bagWidgets);
            ClearWidgets(battleWidgets);
            if (player == null)
            {
                battleCapacityLabel.text = "—";
                return;
            }

            int capacity = player.Level >= 6 ? 10 : LoadoutRules.GetBattleCapacity(player.Level);
            battleCapacityLabel.text = string.Concat(
                player.BattleCardWidth.ToString(), "/", capacity.ToString(), "c");

            List<CardInstance> bag = player.OwnedCards.Where(card => !player.IsEquipped(card)).ToList();
            List<CardInstance> battle = player.BattleCards.ToList();
            RenderRow(bagRow, bagWidgets, bag, false);
            RenderRow(battleRow, battleWidgets, battle, true);
        }

        private void RenderRow(RectTransform row, List<GameObject> bucket, List<CardInstance> cards, bool inBattle)
        {
            float rowWidth = row.rect.width;
            if (rowWidth < 1f)
            {
                rowWidth = root.rect.width;
            }

            List<float> widths = cards.Select(WidthOf).ToList();
            float total = widths.Sum() + Mathf.Max(0, cards.Count - 1) * SlotGap;
            float x = Mathf.Max(16f, (rowWidth - total) * 0.5f);

            for (int index = 0; index < cards.Count; index++)
            {
                AddWidget(row, bucket, cards[index], x, widths[index], inBattle);
                x += widths[index] + SlotGap;
            }
        }

        private float WidthOf(CardInstance card)
        {
            int slots = card.Definition.SlotSize;
            return BaseSlotWidth * scale * slots + SlotGap * scale * (slots - 1);
        }

        private float CardHeight => BaseCardHeight * scale;
        private float SlotGap => BaseSlotGap * scale;

        private void AddWidget(
            RectTransform row,
            List<GameObject> bucket,
            CardInstance card,
            float x,
            float width,
            bool inBattle)
        {
            GameObject widget = new GameObject(
                "Card_" + card.Definition.Id,
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            RectTransform rect = widget.GetComponent<RectTransform>();
            rect.SetParent(row, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, CardHeight);
            rect.anchoredPosition = new Vector2(x, 0f);
            widget.GetComponent<Image>().color = inBattle ? BattleColor : BagColor;

            int fontSize = Mathf.RoundToInt(16f * Mathf.Min(scale, 1.6f));
            Text name = BoardHudOverlayFactory.CreateText(
                widget.transform,
                "Name",
                card.Definition.DisplayName,
                font,
                fontSize,
                FontStyle.Bold,
                new Vector2(10f, -8f),
                new Vector2(width - 20f, fontSize + 8f),
                TextAnchor.UpperLeft);
            name.color = TextColor;

            Text meta = BoardHudOverlayFactory.CreateText(
                widget.transform,
                "Meta",
                CardText.Meta(card.Definition),
                font,
                Mathf.Max(11, fontSize - 4),
                FontStyle.Normal,
                new Vector2(10f, -8f - fontSize - 10f),
                new Vector2(width - 20f, fontSize + 4f),
                TextAnchor.UpperLeft);
            meta.color = inBattle ? Brass : MutedColor;

            Text body = BoardHudOverlayFactory.CreateText(
                widget.transform,
                "Body",
                CardText.Description(card.Definition),
                font,
                Mathf.Max(10, fontSize - 5),
                FontStyle.Normal,
                new Vector2(10f, -(8f + fontSize * 2f + 22f)),
                new Vector2(width - 20f, CardHeight - fontSize * 2f - 46f),
                TextAnchor.UpperLeft);
            body.color = new Color(0.80f, 0.81f, 0.78f, 1f);

            CardBandDragHandler drag = widget.AddComponent<CardBandDragHandler>();
            drag.Bind(this, card, inBattle);
            bucket.Add(widget);
        }

        private static void ClearWidgets(List<GameObject> bucket)
        {
            foreach (GameObject widget in bucket)
            {
                if (widget != null)
                {
                    Object.Destroy(widget);
                }
            }

            bucket.Clear();
        }

        // ---- drag -------------------------------------------------------

        public void BeginDrag(CardBandDragHandler handler, PointerEventData eventData)
        {
            dragging = handler.Card;
            handler.transform.SetParent(root.parent, true);
            handler.transform.SetAsLastSibling();
            CanvasGroup group = handler.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.blocksRaycasts = false;
            }
        }

        public void Drag(CardBandDragHandler handler, PointerEventData eventData)
        {
            RectTransform rect = (RectTransform)handler.transform;
            Vector3 world;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    root,
                    eventData.position,
                    CameraFor(),
                    out world))
            {
                rect.position = world;
            }

            bool overSell = Contains(sellZone, eventData);
            sellImage.color = overSell ? SellHotColor : SellColor;
            sellLabel.text = overSell && dragging != null
                ? string.Concat("+", dragging.Value.ToString(), " 碎片")
                : "出售";
        }

        public void EndDrag(CardBandDragHandler handler, PointerEventData eventData)
        {
            CanvasGroup group = handler.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.blocksRaycasts = true;
            }

            sellImage.color = SellColor;
            sellLabel.text = "出售";

            CardInstance card = handler.Card;
            dragging = null;
            if (card == null)
            {
                return;
            }

            if (Contains(sellZone, eventData))
            {
                gameplay.SubmitSellCard(card.Uid);
                return;
            }

            if (Contains(battleRow, eventData))
            {
                gameplay.SubmitCardMove(card.Uid, true, IndexAt(battleRow, eventData));
                return;
            }

            if (Contains(bagRow, eventData))
            {
                gameplay.SubmitCardMove(card.Uid, false, IndexAt(bagRow, eventData));
                return;
            }

            gameplay.RefreshCardBand();
        }

        public void Select(CardBandDragHandler handler)
        {
            CardInstance card = handler.Card;
            if (card == null)
            {
                return;
            }

            detailPanel.gameObject.SetActive(true);
            detailTitle.text = string.Concat(
                card.Definition.DisplayName,
                "   ",
                card.Definition.SlotSize.ToString(),
                "c");
            detailText.text = string.Concat(
                CardText.Meta(card.Definition),
                "\n\n",
                CardText.Description(card.Definition),
                "\n\n当前价值 ",
                card.Value.ToString(),
                " 碎片");
        }

        private bool Contains(RectTransform rect, PointerEventData eventData)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, CameraFor());
        }

        private int IndexAt(RectTransform row, PointerEventData eventData)
        {
            int index = 0;
            for (int i = 0; i < row.childCount; i++)
            {
                RectTransform child = row.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf)
                {
                    continue;
                }

                float centerX = RectTransformUtility.WorldToScreenPoint(CameraFor(), child.position).x;
                if (eventData.position.x > centerX)
                {
                    index++;
                }
            }

            return index;
        }

        private Camera CameraFor()
        {
            Canvas canvas = root.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return canvas.worldCamera;
            }

            return null;
        }
    }

    /// <summary>画布尺寸变了（改窗口大小 / 分辨率）就重排一次，避免卡槽带错位。</summary>
    public sealed class CardBandLayoutDriver : MonoBehaviour
    {
        private CardBandController owner;
        private int lastWidth;
        private int lastHeight;

        public void Bind(CardBandController controller)
        {
            owner = controller;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            owner.ApplyScreenLayout();
        }

        private void Update()
        {
            if (owner == null || (Screen.width == lastWidth && Screen.height == lastHeight))
            {
                return;
            }

            lastWidth = Screen.width;
            lastHeight = Screen.height;
            owner.ApplyScreenLayout();
        }
    }

    /// <summary>一张卡的拖拽把手：只转发指针事件，状态改变一律走命令。</summary>
    public sealed class CardBandDragHandler : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler
    {
        private CardBandController owner;
        private CardInstance card;
        private bool inBattle;

        public CardInstance Card => card;
        public bool InBattle => inBattle;

        public void Bind(CardBandController controller, CardInstance instance, bool battle)
        {
            owner = controller;
            card = instance;
            inBattle = battle;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            owner?.BeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            owner?.Drag(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            owner?.EndDrag(this, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            owner?.Select(this);
        }
    }
}
