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
        private readonly Button detailSellButton;
        private readonly Text detailSellLabel;
        private readonly Image sellImage;
        private readonly Text sellLabel;
        private readonly Text bagLabel;
        private readonly Text battleLabel;
        private readonly Text battleCapacityLabel;
        private readonly Text handleLabel;
        private readonly Font font;

        private readonly List<GameObject> bagWidgets = new List<GameObject>();
        private readonly List<GameObject> battleWidgets = new List<GameObject>();
        private readonly List<GameObject> bagCells = new List<GameObject>();
        private readonly List<GameObject> battleCells = new List<GameObject>();

        private CardInstance dragging;
        private List<CardInstance> battleSnapshot = new List<CardInstance>();
        private bool collapsed;
        private float scale = 1f;
        private MatchPlayerState lastPlayer;
        private CardInstance selectedCard;

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
                new Vector2(-16f, 318f),
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
                new Vector2(404f, 196f),
                TextAnchor.UpperLeft);
            detailText.color = TextColor;
            detailText.verticalOverflow = VerticalWrapMode.Truncate;

            GameObject sellButtonObject = new GameObject(
                "SellButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            sellButtonObject.transform.SetParent(detailObject.transform, false);
            RectTransform sellButtonRect =
                sellButtonObject.GetComponent<RectTransform>();
            sellButtonRect.anchorMin = new Vector2(0f, 0f);
            sellButtonRect.anchorMax = new Vector2(1f, 0f);
            sellButtonRect.pivot = new Vector2(0.5f, 0f);
            sellButtonRect.offsetMin = new Vector2(18f, 12f);
            sellButtonRect.offsetMax = new Vector2(-18f, 54f);

            sellButtonObject.GetComponent<Image>().color =
                new Color(0.32f, 0.16f, 0.11f, 1f);
            sellButtonObject.GetComponent<Outline>().effectColor =
                new Color(0.92f, 0.45f, 0.28f, 0.7f);
            detailSellButton = sellButtonObject.GetComponent<Button>();
            detailSellLabel = BoardHudOverlayFactory.CreateText(
                sellButtonObject.transform,
                "Label",
                "出售",
                font,
                17,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(404f, 42f),
                TextAnchor.MiddleCenter);
            detailSellLabel.rectTransform.anchorMin = Vector2.zero;
            detailSellLabel.rectTransform.anchorMax = Vector2.one;
            detailSellLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            detailSellLabel.rectTransform.offsetMin = Vector2.zero;
            detailSellLabel.rectTransform.offsetMax = Vector2.zero;
            detailSellLabel.color = new Color(1f, 0.88f, 0.7f, 1f);
            detailSellButton.onClick.AddListener(SellSelectedCard);

            detailObject.SetActive(false);

            root.gameObject.AddComponent<CardBandLayoutDriver>().Bind(this);
        }

        public static CardBandController Create(BoardGameplayController gameplay, RectTransform parent, Font font)
        {
            GameObject rootObject = new GameObject("CardBand", typeof(RectTransform), typeof(Image));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(1180f, BaseBandHeight);
            rect.anchoredPosition = new Vector2(0f, 10f);
            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0.03f, 0.05f, 0.06f, 0.94f);
            background.raycastTarget = false;
            return new CardBandController(gameplay, rect, font);
        }

        /// <summary>掷骰子按钮：由 HUD 注进来，跟着带高一起挪到带上方右侧的空地。</summary>
        public RectTransform DiceButtonTarget { get; set; }

        /// <summary>带子当前高度。</summary>
        public float CurrentBandHeight => root.rect.height;

        /// <summary>设置面板里的「卡槽大小」，乘在自动算出的缩放上。</summary>
        public float UserScale { get; set; } = 1f;

        /// <summary>「展开卡组」按钮：由 HUD 注进来，挂到带子左侧外沿。</summary>
        public RectTransform HandleButtonTarget { get; set; }

        /// <summary>带高 = 画布高度的一半（盖满下半屏）。</summary>
        public void ApplyScreenLayout()
        {
            RectTransform canvasRect = root.parent as RectTransform;
            float canvasWidth = canvasRect != null && canvasRect.rect.width > 1f
                ? canvasRect.rect.width
                : Screen.width;
            float canvasHeight = canvasRect != null && canvasRect.rect.height > 1f
                ? canvasRect.rect.height
                : Screen.height;

            float width = Mathf.Clamp(canvasWidth * 0.78f, 720f, 1560f);
            float height = Mathf.Clamp(canvasHeight * 0.42f, BaseBandHeight, 560f);
            scale = Mathf.Clamp(UserScale, 0.75f, 1.25f)
                * Mathf.Clamp((height - HandleHeight - 38f) * 0.5f / BaseCardHeight, 1f, MaxScale);

            Vector2 size = root.sizeDelta;
            size.x = width;
            root.sizeDelta = size;

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
            DiceButtonTarget.anchoredPosition = new Vector2(-24f, bandHeight + 18f);

            if (HandleButtonTarget == null)
            {
                return;
            }

            float bandWidth = root.sizeDelta.x;
            HandleButtonTarget.anchorMin = new Vector2(0.5f, 0f);
            HandleButtonTarget.anchorMax = new Vector2(0.5f, 0f);
            HandleButtonTarget.pivot = new Vector2(1f, 0.5f);
            HandleButtonTarget.sizeDelta = new Vector2(150f, 34f);
            HandleButtonTarget.anchoredPosition =
                new Vector2(-(bandWidth * 0.5f) - 8f, bandHeight * 0.55f);
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
            float top = HandleHeight + 12f;
            float rowWidth = root.rect.width;
            float sellWidth = Mathf.Clamp(rowWidth * 0.10f, 64f, 108f);
            // 出售区挂在两排的右边缘外侧，中间留一道缝，不压住最右一格。
            float reserve = sellWidth + 26f;

            // 两排紧挨着：行名放到左侧专用列里，不再靠"中间空一行"来放标签。
            float labelColumn = 64f;
            float battleTop = top + 2f + cardHeight + 6f;

            bagLabel.rectTransform.anchoredPosition = new Vector2(14f, -(top + 8f));
            bagRow.anchoredPosition = new Vector2(labelColumn, -(top + 2f));
            bagRow.sizeDelta = new Vector2(-reserve - labelColumn, cardHeight);

            battleLabel.rectTransform.anchoredPosition = new Vector2(14f, -battleTop - 4f);
            battleCapacityLabel.rectTransform.anchoredPosition = new Vector2(14f, -battleTop - 22f);
            battleRow.anchoredPosition = new Vector2(labelColumn, -battleTop);
            battleRow.sizeDelta = new Vector2(-reserve - labelColumn, cardHeight);

            RectTransform sell = sellZone;
            sell.anchorMin = new Vector2(1f, 0f);
            sell.anchorMax = new Vector2(1f, 1f);
            sell.pivot = new Vector2(1f, 0f);
            sell.offsetMin = new Vector2(-sellWidth - 12f, 10f);
            sell.offsetMax = new Vector2(-12f, -(HandleHeight + 10f));

            // 两排共用同一个格宽（按最宽的 10 格算），这样上下两排的格子严格对齐，
            // 拖动换位时的落点换算也只需要一个宽度。
            slotWidth = CellWidthFor(bagRow.rect.width, 10);
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
            if (selectedCard != null &&
                (player == null || !player.OwnedCards.Contains(selectedCard)))
            {
                HideDetail();
            }

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
            RenderRow(bagRow, bagCells, bagWidgets, bag, false, LoadoutRules.BagCapacity);
            RenderRow(battleRow, battleCells, battleWidgets, battle, true, capacity);
        }

        private void RenderRow(
            RectTransform row,
            List<GameObject> cellBucket,
            List<GameObject> bucket,
            List<CardInstance> cards,
            bool inBattle,
            int cellCount)
        {
            float rowWidth = row.rect.width;
            if (rowWidth < 1f)
            {
                rowWidth = root.rect.width;
            }

            BuildSlotCells(row, cellBucket, rowWidth, cellCount, inBattle);

            List<float> widths = cards.Select(WidthOf).ToList();
            float total = widths.Sum() + Mathf.Max(0, cards.Count - 1) * SlotGap;
            float x = 16f;

            if (inBattle)
            {
                battleSnapshot = cards;
            }

            for (int index = 0; index < cards.Count; index++)
            {
                AddWidget(row, bucket, cards[index], x, widths[index], inBattle, cards, index);
                x += widths[index] + SlotGap;
            }
        }

        /// <summary>空槽位画成"格子"：暗底 + 铜线，和参考图一样能看出还能放几张。</summary>
        private void BuildSlotCells(
            RectTransform row,
            List<GameObject> cellBucket,
            float rowWidth,
            int cellCount,
            bool inBattle)
        {
            ClearWidgets(cellBucket);
            if (cellCount <= 0)
            {
                return;
            }

            float cellWidth = SlotWidth;
            float cellHeight = BaseCardHeight * scale;
            float gap = SlotGap;
            float total = cellCount * cellWidth + Mathf.Max(0, cellCount - 1) * gap;
            float x = 16f;
            float thickness = Mathf.Max(1f, 1.4f * scale);
            Color line = inBattle
                ? new Color(0.62f, 0.47f, 0.24f, 0.55f)
                : new Color(0.38f, 0.47f, 0.50f, 0.45f);
            Color plate = inBattle
                ? new Color(0.10f, 0.07f, 0.04f, 0.55f)
                : new Color(0.05f, 0.09f, 0.11f, 0.55f);

            for (int index = 0; index < cellCount; index++)
            {
                GameObject cell = new GameObject(
                    "Cell_" + index.ToString(),
                    typeof(RectTransform),
                    typeof(Image));
                RectTransform rect = cell.GetComponent<RectTransform>();
                rect.SetParent(row, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(cellWidth, cellHeight);
                rect.anchoredPosition = new Vector2(x, 0f);
                Image image = cell.GetComponent<Image>();
                image.color = plate;
                image.raycastTarget = false;
                AddFramedBorder(cell.transform, line, thickness, 8f * scale);
                cellBucket.Add(cell);
                x += cellWidth + gap;
            }
        }

        /// <summary>尺寸决定边框颜色：1c 铜、2c 青钢、3c 金。边角再加四段短刻线。</summary>
        private static Color FrameColor(int slotSize)
        {
            switch (slotSize)
            {
                case 1:
                    return new Color(0.72f, 0.55f, 0.30f, 1f);
                case 2:
                    return new Color(0.48f, 0.66f, 0.70f, 1f);
                default:
                    return new Color(0.93f, 0.71f, 0.28f, 1f);
            }
        }

        private void AddFramedBorder(Transform parent, Color color, float thickness, float tick)
        {
            AddEdge(parent, "EdgeT", color, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -thickness), Vector2.zero);
            AddEdge(parent, "EdgeB", color, new Vector2(0f, 0f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(0f, thickness));
            AddEdge(parent, "EdgeL", color, new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(thickness, 0f));
            AddEdge(parent, "EdgeR", color, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-thickness, 0f), Vector2.zero);

            float leg = thickness * 1.7f;
            AddEdge(parent, "TickTLh", color, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -tick), new Vector2(tick, 0f));
            AddEdge(parent, "TickTLv", color, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -tick), new Vector2(leg, 0f));
            AddEdge(parent, "TickTRh", color, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-tick, -tick), Vector2.zero);
            AddEdge(parent, "TickTRv", color, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-leg, -tick), Vector2.zero);
            AddEdge(parent, "TickBLh", color, new Vector2(0f, 0f), new Vector2(0f, 0f),
                Vector2.zero, new Vector2(tick, tick));
            AddEdge(parent, "TickBLv", color, new Vector2(0f, 0f), new Vector2(0f, 0f),
                Vector2.zero, new Vector2(leg, tick));
            AddEdge(parent, "TickBRh", color, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-tick, 0f), new Vector2(0f, tick));
            AddEdge(parent, "TickBRv", color, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-leg, 0f), new Vector2(0f, tick));
        }

        private static void AddEdge(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject edge = BoardHudOverlayFactory.CreateBar(
                parent, name, anchorMin, anchorMax, offsetMin, offsetMax, color);
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>当前这一行的单格宽度，由 <see cref="CellWidthFor"/> 按行宽算出来后写入。</summary>
        private float slotWidth = BaseSlotWidth;

        private float SlotWidth => slotWidth;

        /// <summary>
        /// 单格宽度：跟着带子缩放，但一定让 cellCount 格塞得进这一行——
        /// 行右侧已经给出售区留了位置，格子溢出就会压到出售区上。
        /// </summary>
        private float CellWidthFor(float rowWidth, int cellCount)
        {
            float available = Mathf.Max(160f, rowWidth - 32f);
            float gap = SlotGap;
            int cells = Mathf.Max(1, cellCount);
            float fit = (available - gap * (cells - 1)) / cells;
            return Mathf.Max(40f, Mathf.Min(BaseSlotWidth * scale, fit));
        }

        private float WidthOf(CardInstance card)
        {
            int slots = card.Definition.SlotSize;
            return SlotWidth * slots + SlotGap * (slots - 1);
        }

        private float CardHeight => BaseCardHeight * scale;
        private float SlotGap => BaseSlotGap * scale;

        /// <summary>卡牌左下角的价值牌：卖出能拿到的碎片数（参考图里角上的数字）。</summary>
        private void AddValueBadge(Transform parent, int value)
        {
            float height = Mathf.Max(20f, 22f * scale);
            float width = Mathf.Max(38f, 46f * scale);
            GameObject plate = BoardHudOverlayFactory.CreateBar(
                parent,
                "ValuePlate",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(4f, 4f),
                new Vector2(4f + width, 4f + height),
                new Color(0.05f, 0.04f, 0.03f, 0.88f));

            Text label = BoardHudOverlayFactory.CreateText(
                plate.transform,
                "Value",
                value.ToString(),
                font,
                Mathf.RoundToInt(13f * Mathf.Min(scale, 1.5f)),
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(width, height),
                TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = Brass;
        }

        private void AddWidget(
            RectTransform row,
            List<GameObject> bucket,
            CardInstance card,
            float x,
            float width,
            bool inBattle,
            System.Collections.Generic.IReadOnlyList<CardInstance> rowCards,
            int rowIndex)
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
            Color plateColor = inBattle ? BattleColor : BagColor;
            widget.GetComponent<Image>().color = plateColor;
            CardArtLibrary.Attach(widget.transform, card.Definition.SlotSize, plateColor);

            float thickness = Mathf.Max(2f, 2.2f * scale);
            AddFramedBorder(widget.transform, FrameColor(card.Definition.SlotSize), thickness, 13f * scale);
            AddValueBadge(widget.transform, card.Value);
            if (inBattle)
            {
                string bonus = BonusBadge(rowCards, rowIndex);
                if (bonus != null)
                {
                    AddBonusBadge(widget.transform, width, bonus);
                }
            }

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

        /// <summary>相邻加成的角标文字：骰值 +N / 额外 N 颗骰 / MAX。</summary>
        private static string BonusBadge(
            System.Collections.Generic.IReadOnlyList<CardInstance> rowCards,
            int rowIndex)
        {
            if (rowCards == null || rowIndex < 0 || rowIndex >= rowCards.Count)
            {
                return null;
            }

            int dice = LoadoutPreview.DiceModifier(rowCards, rowIndex);
            int extra = LoadoutPreview.BonusDice(rowCards, rowIndex);
            bool maxRoll = LoadoutPreview.HasMaxRoll(rowCards, rowIndex);
            if (dice == 0 && extra == 0 && !maxRoll)
            {
                return null;
            }

            List<string> parts = new List<string>(3);
            if (dice != 0)
            {
                parts.Add(dice > 0 ? "+" + dice : dice.ToString());
            }

            if (extra != 0)
            {
                parts.Add((extra > 0 ? "+" + extra : extra.ToString()) + "骰");
            }

            if (maxRoll)
            {
                parts.Add("MAX");
            }

            return string.Join(" ", parts);
        }

        private void AddBonusBadge(Transform parent, float cardWidth, string text)
        {
            float height = Mathf.Max(18f, 20f * scale);
            float width = Mathf.Max(46f, Mathf.Min(cardWidth - 12f, 64f * scale));
            GameObject plate = BoardHudOverlayFactory.CreateBar(
                parent,
                "BonusPlate",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-width - 4f, -height - 4f),
                new Vector2(-4f, -4f),
                new Color(0.22f, 0.15f, 0.03f, 0.92f));

            AddFramedBorder(
                plate.transform,
                new Color(0.98f, 0.80f, 0.36f, 0.95f),
                Mathf.Max(1f, 1.4f * scale),
                5f * scale);

            Text label = BoardHudOverlayFactory.CreateText(
                plate.transform,
                "Bonus",
                text,
                font,
                Mathf.RoundToInt(12f * Mathf.Min(scale, 1.5f)),
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(width, height),
                TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = new Color(1f, 0.90f, 0.58f, 1f);
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
            HideTooltip();
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

            bool overSell = ContainsPadded(sellZone, eventData, 18f);
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

            if (ContainsPadded(sellZone, eventData, 18f))
            {
                HideDetail();
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

        public void ShowTooltip(CardBandDragHandler handler, Vector2 screenPosition)
        {
            if (handler == null || handler.Card == null)
            {
                return;
            }

            CardTooltipHost.Show(
                CardTooltipHost.CanvasRect(root),
                font,
                handler.Card.Definition,
                screenPosition);
        }

        public void HideTooltip()
        {
            CardTooltipHost.Hide();
        }

        public void Select(CardBandDragHandler handler)
        {
            CardInstance card = handler.Card;
            if (card == null)
            {
                return;
            }

            selectedCard = card;
            detailPanel.gameObject.SetActive(true);
            detailSellButton.interactable = true;
            detailSellLabel.text = string.Concat(
                "出售 · +",
                card.Value.ToString(),
                " 碎片");
            detailTitle.text = string.Concat(
                card.Definition.DisplayName,
                "   ",
                card.Definition.SlotSize.ToString(),
                "c");
            string bonus = DescribeBonus(card);
            detailText.text = string.Concat(
                CardText.Meta(card.Definition),
                "\n\n",
                CardText.Description(card.Definition),
                "\n\n当前价值 ",
                card.Value.ToString(),
                " 碎片",
                bonus == null ? string.Empty : "\n相邻加成 " + bonus);
        }

        /// <summary>这张牌（在战斗栏里时）当前吃到的相邻加成，没有则返回 null。</summary>
        private string DescribeBonus(CardInstance card)
        {
            for (int i = 0; i < battleSnapshot.Count; i++)
            {
                if (battleSnapshot[i] != null && battleSnapshot[i].Uid == card.Uid)
                {
                    return LoadoutPreview.Describe(battleSnapshot, i);
                }
            }

            return null;
        }

        /// <summary>详情面板里的「出售」：和拖进出售区等价，无确认无撤销（UI-4.1.1）。</summary>
        private void SellSelectedCard()
        {
            if (selectedCard == null)
            {
                return;
            }

            gameplay.SubmitSellCard(selectedCard.Uid);
        }

        private void HideDetail()
        {
            selectedCard = null;
            detailPanel.gameObject.SetActive(false);
        }

        private bool Contains(RectTransform rect, PointerEventData eventData)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, CameraFor());
        }

        /// <summary>出售热区放一点余量，免得差几个像素就判成"没拖到"。</summary>
        private bool ContainsPadded(
            RectTransform rect,
            PointerEventData eventData,
            float padding)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min;
            Vector2 max;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect,
                    corners[0],
                    CameraFor(),
                    out min) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect,
                    corners[2],
                    CameraFor(),
                    out max))
            {
                return Contains(rect, eventData);
            }

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect,
                    eventData.position,
                    CameraFor(),
                    out local))
            {
                return false;
            }

            float pad = padding / Mathf.Max(0.0001f, scale);
            return local.x >= min.x - pad &&
                local.x <= max.x + pad &&
                local.y >= min.y - pad &&
                local.y <= max.y + pad;
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
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.ShowTooltip(this, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.HideTooltip();
        }
    }
}
