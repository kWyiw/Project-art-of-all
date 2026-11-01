using System.Collections.Generic;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Match;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 棋盘层常驻双排卡槽带（`棋盘层双栏卡槽UI与拖拽交互规格.md` §2–§5）。
    /// 上排背包 10c、下排战斗栏 4/6/8/10c，卡牌按格位宽度排版；
    /// 拖到另一排 = 跨区移动，拖到同一排的插入位 = 重排，拖进右侧出售区 = 立即出售。
    /// 所有落点都只是"提议"：真正生效的是 <see cref="MoveCardCommand"/> / <see cref="SellCardCommand"/>。
    /// </summary>
    public sealed class CardBandController
    {
        public const int CardHeight = 118;
        public const int SlotWidth = 88;
        public const int SlotGap = 8;
        public const int LabelWidth = 60;
        public const int BandWidth = 1020;
        public const int BandHeight = 272;

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
        private readonly Text battleCapacityLabel;
        private readonly Text handleLabel;
        private readonly Button handleButton;
        private readonly Font font;

        private readonly List<GameObject> bagWidgets = new List<GameObject>();
        private readonly List<GameObject> battleWidgets = new List<GameObject>();
        private CardInstance dragging;
        private bool collapsed;

        private CardBandController(BoardGameplayController gameplay, RectTransform root, Font font)
        {
            this.gameplay = gameplay;
            this.root = root;
            this.font = font;

            Canvas canvas = root.GetComponentInParent<Canvas>();
            GameObject handleObject = BoardHudOverlayFactory.CreateBar(
                root,
                "Handle",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -30f),
                Vector2.zero,
                new Color(0.06f, 0.09f, 0.10f, 0.99f));
            handleObject.transform.SetAsFirstSibling();
            handleLabel = BoardHudOverlayFactory.CreateText(
                handleObject.transform,
                "Label",
                "▾ 卡槽",
                font,
                16,
                FontStyle.Bold,
                new Vector2(16f, 0f),
                new Vector2(220f, 28f),
                TextAnchor.MiddleLeft);
            handleLabel.color = Brass;
            Image handleHit = handleObject.GetComponent<Image>();
            handleHit.raycastTarget = true;
            handleButton = handleObject.AddComponent<Button>();
            handleButton.transition = Selectable.Transition.None;
            handleButton.onClick.AddListener(ToggleCollapsed);

            bagRow = CreateRow(root, "Bag", 24f);
            battleRow = CreateRow(root, "Battle", 24f + CardHeight + 8f);

            Text bagLabel = CreateRowLabel(root, "背包", 24f);
            bagLabel.color = MutedColor;
            Text battleLabel = CreateRowLabel(root, "战斗", 24f + CardHeight + 8f);
            battleLabel.color = Brass;
            battleCapacityLabel = CreateRowCaption(root, 24f + CardHeight + 8f);

            GameObject sellObject = BoardHudOverlayFactory.CreateBar(
                root,
                "SellZone",
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(BandWidth + 12f, 36f),
                new Vector2(BandWidth + 84f, 108f),
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
                Vector2.zero,
                new Vector2(72f, 24f),
                TextAnchor.MiddleCenter);
            sellLabel.color = MutedColor;
            sellLabel.rectTransform.anchoredPosition = new Vector2(36f, 36f);

            GameObject detailObject = BoardHudOverlayFactory.CreateBar(
                root,
                "Detail",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(BandWidth + 12f, BandHeight - 140f),
                new Vector2(BandWidth + 412f, BandHeight + 36f),
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
                new Vector2(360f, 30f),
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
                new Vector2(364f, 190f),
                TextAnchor.UpperLeft);
            detailText.color = TextColor;
            detailObject.SetActive(false);

            // 拖拽中的卡牌要盖在其它 UI 之上：单独一层，覆盖整个画布。
            Debug.Log("[CardBand] ready");
            _ = canvas;
        }

        public static CardBandController Create(BoardGameplayController gameplay, RectTransform parent, Font font)
        {
            GameObject rootObject = new GameObject("CardBand", typeof(RectTransform), typeof(Image));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(BandWidth, BandHeight);
            rect.anchoredPosition = new Vector2(0f, 0f);
            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0.03f, 0.05f, 0.06f, 0.92f);
            return new CardBandController(gameplay, rect, font);
        }

        private RectTransform CreateRow(RectTransform parent, string name, float top)
        {
            GameObject rowObject = new GameObject(name, typeof(RectTransform));
            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(BandWidth - LabelWidth, CardHeight);
            row.anchoredPosition = new Vector2(LabelWidth, -top);
            return row;
        }

        private Text CreateRowLabel(RectTransform parent, string label, float top)
        {
            return BoardHudOverlayFactory.CreateText(
                parent,
                "Label" + label,
                label,
                font,
                16,
                FontStyle.Bold,
                new Vector2(10f, -top),
                new Vector2(48f, 28f),
                TextAnchor.UpperLeft);
        }

        private Text CreateRowCaption(RectTransform parent, float top)
        {
            Text caption = BoardHudOverlayFactory.CreateText(
                parent,
                "BattleCapacity",
                string.Empty,
                font,
                13,
                FontStyle.Normal,
                new Vector2(10f, -top - 24f),
                new Vector2(48f, 22f),
                TextAnchor.UpperLeft);
            caption.color = MutedColor;
            return caption;
        }

        /// <summary>S1 全展开 ↔ S3 摘要条（自动折叠留给掷骰动画，见 UI-2.4.2）。</summary>
        public void ToggleCollapsed()
        {
            collapsed = !collapsed;
            root.sizeDelta = new Vector2(BandWidth, collapsed ? 48f : BandHeight);
            handleLabel.text = collapsed ? "▸ 卡槽" : "▾ 卡槽";
            bagRow.gameObject.SetActive(!collapsed);
            battleRow.gameObject.SetActive(!collapsed);
            sellZone.gameObject.SetActive(!collapsed);
            detailPanel.gameObject.SetActive(!collapsed && detailPanel.gameObject.activeSelf);
        }

        public void Refresh(MatchPlayerState player)
        {
            ClearWidgets(bagWidgets);
            ClearWidgets(battleWidgets);
            if (player == null)
            {
                battleCapacityLabel.text = "—";
                return;
            }

            int capacity = player.Level >= 6 ? 10 : LoadoutRules.GetBattleCapacity(player.Level);
            battleCapacityLabel.text = string.Concat(player.BattleCardWidth.ToString(), "/", capacity.ToString(), "c");

            int bagX = 0;
            foreach (CardInstance card in player.OwnedCards)
            {
                if (player.IsEquipped(card))
                {
                    continue;
                }

                bagX = AddWidget(bagRow, bagWidgets, player, card, bagX, false);
            }

            int battleX = 0;
            foreach (CardInstance card in player.BattleCards)
            {
                battleX = AddWidget(battleRow, battleWidgets, player, card, battleX, true);
            }

            root.gameObject.SetActive(true);
        }

        private int AddWidget(
            RectTransform row,
            List<GameObject> bucket,
            MatchPlayerState player,
            CardInstance card,
            int x,
            bool inBattle)
        {
            int width = SlotWidth * card.Definition.SlotSize + SlotGap * (card.Definition.SlotSize - 1);
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

            Text name = BoardHudOverlayFactory.CreateText(
                widget.transform,
                "Name",
                card.Definition.DisplayName,
                font,
                18,
                FontStyle.Bold,
                new Vector2(10f, -6f),
                new Vector2(width - 20f, 26f),
                TextAnchor.UpperLeft);
            name.color = TextColor;

            Text meta = BoardHudOverlayFactory.CreateText(
                widget.transform,
                "Meta",
                CardText.Meta(card.Definition),
                font,
                13,
                FontStyle.Normal,
                new Vector2(10f, -32f),
                new Vector2(width - 20f, 20f),
                TextAnchor.UpperLeft);
            meta.color = inBattle ? Brass : MutedColor;

            Text body = BoardHudOverlayFactory.CreateText(
                widget.transform,
                "Body",
                CardText.Description(card.Definition),
                font,
                12,
                FontStyle.Normal,
                new Vector2(10f, -54f),
                new Vector2(width - 20f, CardHeight - 60f),
                TextAnchor.UpperLeft);
            body.color = new Color(0.80f, 0.81f, 0.78f, 1f);

            CardBandDragHandler drag = widget.AddComponent<CardBandDragHandler>();
            drag.Bind(this, player.Id, card, inBattle);
            bucket.Add(widget);
            return x + width + SlotGap;
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
                    root, eventData.position, CameraFor(), out world))
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

            // 拖到卡槽外但没落进出售区：回弹（UI-4.1.3），状态本来就没变，重画即可。
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
                "\n\n价值 ",
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

                float x = RectTransformUtility.WorldToScreenPoint(CameraFor(), child.position).x;
                if (eventData.position.x > x + child.rect.width * 0.5f)
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

    /// <summary>一张卡的拖拽把手：只负责转发指针事件，状态改变一律走命令。</summary>
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

        public void Bind(CardBandController controller, int playerId, CardInstance instance, bool battle)
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
