using System.Collections.Generic;
using ProjectArtOfAll.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 战斗栏/背包栏的绘制：格子、卡牌、尺寸边框、价值角标、相邻加成角标。
    /// 常年显示的卡槽带（<see cref="CardBandController"/>）和战斗回放里的双方战斗栏
    /// 共用这一份绘制代码，保证"战斗中看到的就是把上下两条常驻栏位对在一起"。
    /// </summary>
    public sealed class CardRowRenderer
    {
        public const float BaseSlotWidth = 88f;
        public const float BaseCardHeight = 118f;
        public const float BaseSlotGap = 8f;
        public const float RowInset = 16f;
        public const int SlotsAcrossRow = 10;

        public static readonly Color BagColor = new Color(0.14f, 0.22f, 0.27f, 0.97f);
        public static readonly Color BattleColor = new Color(0.30f, 0.20f, 0.12f, 0.97f);
        public static readonly Color Brass = new Color(0.92f, 0.69f, 0.27f, 1f);
        public static readonly Color TextColor = new Color(0.94f, 0.93f, 0.88f, 1f);
        public static readonly Color MutedColor = new Color(0.66f, 0.68f, 0.66f, 1f);

        private readonly Font font;

        public CardRowRenderer(Font font)
        {
            this.font = font;
        }

        public float Scale { get; set; } = 1f;
        public float RowWidth { get; set; } = 800f;
        public bool InBattle { get; set; } = true;

        /// <summary>从上边缘往下留的余量（战斗回放里卡比行矮时用来垂直居中）。</summary>
        public float TopInset { get; set; }

        public float SlotGap => BaseSlotGap * Scale;
        public float CardHeight => BaseCardHeight * Scale;

        /// <summary>单格宽度：跟着带子放大，但保证 10 格一定塞得进行宽。</summary>
        public float SlotWidth
        {
            get
            {
                float maxWidth = (Mathf.Max(320f, RowWidth) - 32f) / SlotsAcrossRow - SlotGap;
                return Mathf.Max(40f, Mathf.Min(BaseSlotWidth * Scale, maxWidth));
            }
        }

        public float WidthOf(CardInstance card)
        {
            int slots = Mathf.Max(1, card.Definition.SlotSize);
            return SlotWidth * slots + SlotGap * (slots - 1);
        }

        /// <summary>按"高度上限 + 行宽"反推一个不变形的缩放系数。</summary>
        public float FitScale(float maxCardHeight)
        {
            float byWidth = (RowWidth - RowInset * 2f) /
                (SlotsAcrossRow * BaseSlotWidth + (SlotsAcrossRow - 1) * BaseSlotGap);
            float byHeight = maxCardHeight / BaseCardHeight;
            return Mathf.Clamp(Mathf.Min(byWidth, byHeight), 0.4f, 3.5f);
        }

        /// <summary>画空槽位：暗底 + 细边框 + 四角短刻线。</summary>
        public void BuildCells(RectTransform row, List<GameObject> bucket, int cellCount)
        {
            foreach (GameObject cell in bucket)
            {
                if (cell != null)
                {
                    Object.Destroy(cell);
                }
            }

            bucket.Clear();
            if (cellCount <= 0)
            {
                return;
            }

            float cellWidth = SlotWidth;
            float cellHeight = CardHeight;
            float x = RowInset;
            float thickness = Mathf.Max(1f, 1.4f * Scale);
            Color line = InBattle
                ? new Color(0.62f, 0.47f, 0.24f, 0.55f)
                : new Color(0.38f, 0.47f, 0.50f, 0.45f);
            Color plate = InBattle
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
                rect.anchoredPosition = new Vector2(x, -TopInset);

                Image image = cell.GetComponent<Image>();
                image.color = plate;
                image.raycastTarget = false;

                AddFramedBorder(cell.transform, line, thickness, 8f * Scale);
                bucket.Add(cell);
                x += cellWidth + SlotGap;
            }
        }

        /// <summary>画一张卡：尺寸边框 + 价值角标 + 名字/数值/效果。</summary>
        public GameObject AddCard(
            RectTransform row,
            CardInstance card,
            float x,
            float width,
            string bonusBadge)
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
            // 和空槽位对齐：槽位画在 -TopInset，卡也画在 -TopInset，
            // 否则卡片会高出卡牌栏位一截。
            rect.anchoredPosition = new Vector2(x, -TopInset);
            Color plateColor = InBattle ? BattleColor : BagColor;
            widget.GetComponent<Image>().color = plateColor;
            CardArtLibrary.Attach(widget.transform, card.Definition.SlotSize, plateColor);

            float thickness = Mathf.Max(2f, 2.2f * Scale);
            AddFramedBorder(widget.transform, FrameColor(card.Definition.SlotSize), thickness, 13f * Scale);
            AddValueBadge(widget.transform, card.Value);
            if (!string.IsNullOrEmpty(bonusBadge))
            {
                AddBonusBadge(widget.transform, width, bonusBadge);
            }

            int fontSize = Mathf.RoundToInt(16f * Mathf.Min(Scale, 1.6f));
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
            meta.color = InBattle ? Brass : MutedColor;

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
            // 卡面短句里的关键词和悬停面板用同一份配色（内核 CardKeywords）。
            body.supportRichText = true;
            body.text = CardKeywords.Colorize(CardText.Description(card.Definition));

            return widget;
        }

        /// <summary>尺寸决定边框颜色：1c 铜、2c 青钢、3c 金。</summary>
        public static Color FrameColor(int slotSize)
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

        /// <summary>相邻加成的角标文字：骰值 +N / 额外 N 颗骰 / MAX。</summary>
        /// <summary>第 column 格左边缘在行内的 x（行 pivot 在左上）。</summary>
        public float ColumnX(int column)
        {
            return RowInset + column * (SlotWidth + SlotGap);
        }

        /// <summary>按"连续摆放"的旧口径算角标：内部先打包成列位再求值。</summary>
        public static string BonusBadge(IReadOnlyList<CardInstance> rowCards, int rowIndex)
        {
            if (rowCards == null || rowIndex < 0 || rowIndex >= rowCards.Count)
            {
                return null;
            }

            IReadOnlyList<CardSlot> slots = CardSlotRules.Unpack(rowCards);
            return rowIndex >= slots.Count ? null : BonusBadge(slots, rowCards[rowIndex].Uid);
        }

        /// <summary>按栏位列位算角标：中间留空就不相邻，加成自然掉（R-CARD-011）。</summary>
        public static string BonusBadge(IReadOnlyList<CardSlot> slots, int cardUid)
        {
            if (slots == null)
            {
                return null;
            }

            int dice = LoadoutPreview.DiceModifier(slots, cardUid);
            int extra = LoadoutPreview.BonusDice(slots, cardUid);
            bool maxRoll = LoadoutPreview.HasMaxRoll(slots, cardUid);
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

        /// <summary>四条边 + 八个角刻线的"装饰边框"。</summary>
        public static void AddFramedBorder(Transform parent, Color color, float thickness, float tick)
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

        /// <summary>卡牌左下角的价值牌：卖出能拿到的碎片数。</summary>
        private void AddValueBadge(Transform parent, int value)
        {
            float height = Mathf.Max(20f, 22f * Scale);
            float width = Mathf.Max(38f, 46f * Scale);
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
                Mathf.RoundToInt(13f * Mathf.Min(Scale, 1.5f)),
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

        private void AddBonusBadge(Transform parent, float cardWidth, string text)
        {
            float height = Mathf.Max(18f, 20f * Scale);
            float width = Mathf.Max(46f, Mathf.Min(cardWidth - 12f, 64f * Scale));
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
                Mathf.Max(1f, 1.4f * Scale),
                5f * Scale);

            Text label = BoardHudOverlayFactory.CreateText(
                plate.transform,
                "Bonus",
                text,
                font,
                Mathf.RoundToInt(12f * Mathf.Min(Scale, 1.5f)),
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
    }
}
