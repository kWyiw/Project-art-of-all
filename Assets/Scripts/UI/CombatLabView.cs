using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 自定义战斗模拟：只有战斗页面。从全部卡牌里复制任意张塞进左右两侧战斗栏，
    /// 各自设定最大生命值，点开始就按真实内核打一场，并逐帧回放（1x / 2x / 4x 可调）。
    /// 版式全部由代码搭出来，不依赖场景。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatLabView : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        /// <summary>模拟模式双方都用 10c 的战斗栏上限（等于 5 级容量）。</summary>
        private const int LabLevel = 5;
        private const int BattleCapacity = 10;
        private const int DefaultHealth = 200;
        private const int HealthStep = 25;
        private const int MinimumHealth = 25;
        private const int MaximumHealth = 999;

        private static readonly Color BackgroundColor = new Color(0.028f, 0.038f, 0.048f, 1f);
        private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.088f, 0.97f);
        private static readonly Color RowColor = new Color(0.09f, 0.115f, 0.13f, 0.95f);
        private static readonly Color Brass = new Color(0.90f, 0.70f, 0.32f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.92f, 0.87f, 1f);
        private static readonly Color MutedText = new Color(0.58f, 0.62f, 0.62f, 1f);
        private static readonly Color LeftTint = new Color(0.16f, 0.26f, 0.32f, 0.95f);
        private static readonly Color RightTint = new Color(0.34f, 0.22f, 0.14f, 0.95f);

        private readonly List<CardDefinition> leftDeck = new List<CardDefinition>();
        private readonly List<CardDefinition> rightDeck = new List<CardDefinition>();
        private readonly List<GameObject> leftChips = new List<GameObject>();
        private readonly List<GameObject> rightChips = new List<GameObject>();

        private Font font;
        private RectTransform root;
        private RectTransform leftDeckRow;
        private RectTransform rightDeckRow;
        private Text leftDeckLabel;
        private Text rightDeckLabel;
        private Text leftHealthLabel;
        private Text rightHealthLabel;
        private Text statusLabel;
        private CombatPlaybackView playback;
        private GameObject speedHighlight;
        private int leftHealth = DefaultHealth;
        private int rightHealth = DefaultHealth;
        private float preferredSpeed = 2f;
        private int nextUid = 1;
        private bool busy;

        public static CombatLabView Create(RectTransform canvas, Font font)
        {
            GameObject rootObject = new GameObject(
                "CombatLab",
                typeof(RectTransform),
                typeof(CanvasGroup));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CombatLabView view = rootObject.AddComponent<CombatLabView>();
            view.font = font;
            view.root = rect;
            view.Build();
            return view;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RebuildDecks();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 版式

        private void Build()
        {
            Image background = gameObject.AddComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = true;

            AddBar(root, "TopRule", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -118f), new Vector2(0f, -116f),
                new Color(0.86f, 0.62f, 0.20f, 0.45f));

            AddText(root, "Title", "战斗模拟 · 自定义对局", 48f, 26f, 900f, 48f,
                32, FontStyle.Bold, TextAnchor.MiddleLeft, Brass);
            AddText(root, "Hint",
                "从卡牌库复制任意张进左右战斗栏（战斗栏上限 "
                    + BattleCapacity + "c），各自设定最大生命值，开打后可用 1x / 2x / 4x 调速。",
                48f, 74f, 1500f, 28f, 16, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

            BuildCardLibrary();
            BuildDeckPanel(true);
            BuildDeckPanel(false);
            BuildFooter();
        }

        private void BuildCardLibrary()
        {
            RectTransform panel = CreatePanel("CardLibrary", 40f, 118f, 560f, 880f);

            AddText(panel, "Header", "卡牌库", 20f, 14f, 300f, 32f,
                24, FontStyle.Bold, TextAnchor.MiddleLeft, Brass);
            AddText(panel, "Sub", "「左」「右」各加一份，可以重复拿同一张", 20f, 46f, 520f, 24f,
                14, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

            List<CardDefinition> cards = CardDatabase.Instance.All
                .OrderBy(card => card.SlotSize)
                .ThenBy(card => card.DisplayName)
                .ToList();

            const float rowHeight = 50f;
            const float columnWidth = 262f;
            float listTop = 82f;

            for (int index = 0; index < cards.Count; index++)
            {
                CardDefinition card = cards[index];
                int column = index % 2;
                int row = index / 2;
                float x = 14f + column * (columnWidth + 8f);
                float top = listTop + row * rowHeight;

                RectTransform rowRect = CreateRect("Card_" + card.Id, panel);
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(0f, 1f);
                rowRect.pivot = new Vector2(0f, 1f);
                rowRect.anchoredPosition = new Vector2(x, -top);
                rowRect.sizeDelta = new Vector2(columnWidth, rowHeight - 6f);

                Image rowImage = rowRect.gameObject.AddComponent<Image>();
                rowImage.color = RowColor;

                AddBar(rowRect, "Edge", new Vector2(0f, 0f), new Vector2(0f, 1f),
                    Vector2.zero, new Vector2(3f, 0f), CardRowRenderer.FrameColor(card.SlotSize));

                AddText(rowRect, "Name", card.DisplayName, 10f, 4f, 150f, 22f,
                    15, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
                AddText(rowRect, "Meta", CardText.Meta(card), 10f, 22f, 160f, 18f,
                    12, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

                CardDefinition captured = card;
                CreateSmallButton(rowRect, "Left", "左", 172f, 10f, 40f, 26f, LeftTint,
                    () => AddCard(captured, true));
                CreateSmallButton(rowRect, "Right", "右", 216f, 10f, 40f, 26f, RightTint,
                    () => AddCard(captured, false));
            }
        }

        private void BuildDeckPanel(bool isLeft)
        {
            float top = isLeft ? 118f : 430f;
            RectTransform panel = CreatePanel(
                isLeft ? "LeftDeckPanel" : "RightDeckPanel",
                620f, top, 1260f, 300f);

            Text header = AddText(panel, "Header", isLeft ? "左方战斗栏" : "右方战斗栏",
                20f, 12f, 320f, 32f, 24, FontStyle.Bold, TextAnchor.MiddleLeft,
                isLeft ? new Color(0.62f, 0.82f, 0.90f, 1f) : new Color(0.95f, 0.76f, 0.48f, 1f));

            Text capacity = AddText(panel, "Capacity", string.Empty, 20f, 44f, 700f, 24f,
                15, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

            if (isLeft)
            {
                leftDeckLabel = capacity;
                leftDeckRow = CreateRect("Deck", panel);
            }
            else
            {
                rightDeckLabel = capacity;
                rightDeckRow = CreateRect("Deck", panel);
            }

            RectTransform row = isLeft ? leftDeckRow : rightDeckRow;
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(20f, -76f);
            row.sizeDelta = new Vector2(-40f, 210f);

            RectTransform clearBox = CreateRect("Clear", panel);
            clearBox.anchorMin = new Vector2(1f, 1f);
            clearBox.anchorMax = new Vector2(1f, 1f);
            clearBox.pivot = new Vector2(1f, 1f);
            clearBox.anchoredPosition = new Vector2(-16f, -14f);
            clearBox.sizeDelta = new Vector2(96f, 30f);
            CreateSmallButton(clearBox, "Clear", "清空", 0f, 0f, 96f, 30f,
                new Color(0.18f, 0.14f, 0.12f, 0.95f), () => ClearDeck(isLeft));

            if (header != null)
            {
                header.raycastTarget = false;
            }
        }

        private void BuildFooter()
        {
            RectTransform bar = CreatePanel("Footer", 620f, 748f, 1260f, 132f);

            AddText(bar, "LeftHealthLabel", "左方生命", 20f, 16f, 120f, 26f,
                18, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            leftHealthLabel = AddText(bar, "LeftHealth", leftHealth.ToString(), 148f, 12f, 80f, 34f,
                26, FontStyle.Bold, TextAnchor.MiddleCenter, Brass);
            CreateSmallButton(bar, "LeftMinus", "−", 96f, 16f, 42f, 28f, RowColor,
                () => AdjustHealth(true, -HealthStep));
            CreateSmallButton(bar, "LeftPlus", "+", 238f, 16f, 42f, 28f, RowColor,
                () => AdjustHealth(true, HealthStep));

            AddText(bar, "RightHealthLabel", "右方生命", 320f, 16f, 120f, 26f,
                18, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            rightHealthLabel = AddText(bar, "RightHealth", rightHealth.ToString(), 448f, 12f, 80f, 34f,
                26, FontStyle.Bold, TextAnchor.MiddleCenter, Brass);
            CreateSmallButton(bar, "RightMinus", "−", 396f, 16f, 42f, 28f, RowColor,
                () => AdjustHealth(false, -HealthStep));
            CreateSmallButton(bar, "RightPlus", "+", 538f, 16f, 42f, 28f, RowColor,
                () => AdjustHealth(false, HealthStep));

            AddText(bar, "SpeedLabel", "倍速", 620f, 16f, 70f, 26f,
                16, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);
            CreateSmallButton(bar, "Speed1", "1x", 668f, 14f, 54f, 28f, RowColor, () => SetSpeed(1f));
            CreateSmallButton(bar, "Speed2", "2x", 728f, 14f, 54f, 28f, RowColor, () => SetSpeed(2f));
            CreateSmallButton(bar, "Speed4", "4x", 788f, 14f, 54f, 28f, RowColor, () => SetSpeed(4f));
            speedHighlight = CreateHighlight(bar, 668f + (preferredSpeed >= 3f ? 120f : preferredSpeed >= 1.5f ? 60f : 0f));

            CreateButton(bar, "Fight", "开始战斗", 20f, 70f, 300f, 48f,
                new Color(0.62f, 0.44f, 0.14f, 0.98f), StartBattle);
            CreateButton(bar, "Copy", "把左边复制到右边", 336f, 70f, 220f, 48f, RowColor, CopyLeftToRight);
            CreateButton(bar, "Back", "返回主菜单", -20f, 70f, 220f, 48f, RowColor, Close);

            statusLabel = AddText(bar, "Status", "左方 / 右方各自最多 " + BattleCapacity + "c。",
                580f, 82f, 560f, 30f, 14, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);
            AnchorTopRight(statusLabel.rectTransform, new Vector2(-260f, -82f));
        }

        // ------------------------------------------------------------------ 卡组

        private void AddCard(CardDefinition card, bool toLeft)
        {
            if (busy)
            {
                return;
            }

            List<CardDefinition> deck = toLeft ? leftDeck : rightDeck;
            int used = deck.Sum(entry => entry.SlotSize);
            if (used + card.SlotSize > BattleCapacity)
            {
                SetStatus((toLeft ? "左方" : "右方") + "战斗栏只有 " + BattleCapacity
                    + "c，放不下 " + card.DisplayName + "（需要 " + card.SlotSize + "c）。");
                return;
            }

            deck.Add(card);
            SetStatus("已把 " + card.DisplayName + " 加进" + (toLeft ? "左方" : "右方") + "。");
            RebuildDecks();
        }

        private void RemoveCard(bool fromLeft, int index)
        {
            if (busy)
            {
                return;
            }

            List<CardDefinition> deck = fromLeft ? leftDeck : rightDeck;
            if (index < 0 || index >= deck.Count)
            {
                return;
            }

            string name = deck[index].DisplayName;
            deck.RemoveAt(index);
            SetStatus("已移除 " + name + "。");
            RebuildDecks();
        }

        private void ClearDeck(bool isLeft)
        {
            if (busy)
            {
                return;
            }

            (isLeft ? leftDeck : rightDeck).Clear();
            RebuildDecks();
        }

        private void CopyLeftToRight()
        {
            if (busy)
            {
                return;
            }

            rightDeck.Clear();
            rightDeck.AddRange(leftDeck);
            SetStatus("已把左方卡组复制到右方。");
            RebuildDecks();
        }

        private void AdjustHealth(bool isLeft, int delta)
        {
            if (busy)
            {
                return;
            }

            if (isLeft)
            {
                leftHealth = Mathf.Clamp(leftHealth + delta, MinimumHealth, MaximumHealth);
                leftHealthLabel.text = leftHealth.ToString();
            }
            else
            {
                rightHealth = Mathf.Clamp(rightHealth + delta, MinimumHealth, MaximumHealth);
                rightHealthLabel.text = rightHealth.ToString();
            }
        }

        private void SetSpeed(float speed)
        {
            preferredSpeed = speed;
            if (speedHighlight != null)
            {
                RectTransform highlight = speedHighlight.GetComponent<RectTransform>();
                float offset = speed >= 3f ? 120f : speed >= 1.5f ? 60f : 0f;
                highlight.anchoredPosition = new Vector2(668f + offset - 4f, -12f);
            }

            SetStatus("倍速设为 " + speed.ToString("0") + "x。");
        }

        private void RebuildDecks()
        {
            RebuildDeck(true);
            RebuildDeck(false);
        }

        private void RebuildDeck(bool isLeft)
        {
            if (leftDeckRow == null || rightDeckRow == null)
            {
                return;
            }

            RectTransform row = isLeft ? leftDeckRow : rightDeckRow;
            List<GameObject> chips = isLeft ? leftChips : rightChips;
            List<CardDefinition> deck = isLeft ? leftDeck : rightDeck;

            foreach (GameObject chip in chips)
            {
                if (chip != null)
                {
                    Destroy(chip);
                }
            }

            chips.Clear();

            int used = deck.Sum(entry => entry.SlotSize);
            Text label = isLeft ? leftDeckLabel : rightDeckLabel;
            if (label != null)
            {
                label.text = deck.Count + " 张 · 占 " + used + "c / " + BattleCapacity
                    + "c   （点击卡牌移除）";
                label.color = used > BattleCapacity ? Brass : MutedText;
            }

            const float chipWidth = 148f;
            const float chipHeight = 62f;
            const float gap = 8f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((1240f + gap) / (chipWidth + gap)));

            for (int index = 0; index < deck.Count; index++)
            {
                CardDefinition card = deck[index];
                int column = index % perRow;
                int rowIndex = index / perRow;

                RectTransform chip = CreateRect("Chip_" + index, row);
                chip.anchorMin = new Vector2(0f, 1f);
                chip.anchorMax = new Vector2(0f, 1f);
                chip.pivot = new Vector2(0f, 1f);
                chip.anchoredPosition = new Vector2(
                    column * (chipWidth + gap),
                    -rowIndex * (chipHeight + gap));
                chip.sizeDelta = new Vector2(chipWidth, chipHeight);

                Image chipImage = chip.gameObject.AddComponent<Image>();
                chipImage.color = isLeft ? LeftTint : RightTint;

                Button button = chip.gameObject.AddComponent<Button>();
                button.targetGraphic = chipImage;
                int captured = index;
                button.onClick.AddListener(() => RemoveCard(isLeft, captured));

                CardRowRenderer.AddFramedBorder(chip, CardRowRenderer.FrameColor(card.SlotSize),
                    2f, 9f);

                AddText(chip, "Name", card.DisplayName, 8f, 6f, chipWidth - 16f, 22f,
                    15, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
                AddText(chip, "Meta", CardText.Meta(card), 8f, 28f, chipWidth - 16f, 18f,
                    12, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);
                AddText(chip, "Value", card.Value + "g", chipWidth - 40f, 42f, 34f, 16f,
                    11, FontStyle.Normal, TextAnchor.MiddleRight, Brass);

                chips.Add(chip.gameObject);
            }

            if (deck.Count == 0)
            {
                AddText(row, "Empty", isLeft ? "还没有牌，从左边卡牌库点「左」" : "还没有牌，从左边卡牌库点「右」",
                    2f, 6f, 600f, 26f, 15, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);
            }
        }

        // ------------------------------------------------------------------ 开打

        private void StartBattle()
        {
            if (busy)
            {
                return;
            }

            if (leftDeck.Count == 0 || rightDeck.Count == 0)
            {
                SetStatus("两边都要至少放一张牌才能开打。");
                return;
            }

            CombatantSnapshot left = BuildCombatant(0, leftDeck, leftHealth);
            CombatantSnapshot right = BuildCombatant(1, rightDeck, rightHealth);
            CombatSimulation simulation = new CombatSimulation(
                new BattleSnapshot(left, right),
                (ulong)System.DateTime.UtcNow.Ticks);
            CombatResult result = simulation.RunToCompletion();
            CombatResolvedEvent resolved = new CombatResolvedEvent(0, 1, result);

            string leftLabel = "左方 · " + leftDeck.Count + " 张 · " + leftHealth + " 生命";
            string rightLabel = "右方 · " + rightDeck.Count + " 张 · " + rightHealth + " 生命";
            SetStatus("开始战斗：左 " + leftDeck.Count + " 张 / 右 " + rightDeck.Count + " 张。");

            if (playback == null)
            {
                playback = CombatPlaybackView.Create(transform, font);
            }

            busy = true;
            playback.Play(resolved, leftLabel, rightLabel, true, () =>
            {
                busy = false;
                SetStatus("这一场打完了，可以改卡组再打一次。");
            }, preferredSpeed);
        }

        private CombatantSnapshot BuildCombatant(
            int playerId,
            IReadOnlyList<CardDefinition> deck,
            int health)
        {
            List<CardInstance> cards = new List<CardInstance>(deck.Count);
            for (int index = 0; index < deck.Count; index++)
            {
                cards.Add(new CardInstance(nextUid++, deck[index]));
            }

            return new CombatantSnapshot(playerId, LabLevel, cards, health);
        }

        private void Close()
        {
            if (playback != null)
            {
                Destroy(playback.gameObject);
                playback = null;
            }

            Destroy(gameObject);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        // ------------------------------------------------------------------ 工具

        private RectTransform CreatePanel(string name, float left, float top, float width, float height)
        {
            RectTransform panel = CreateRect(name, root);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(left, -top);
            panel.sizeDelta = new Vector2(width, height);

            Image image = panel.gameObject.AddComponent<Image>();
            image.color = PanelColor;
            return panel;
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private Text AddText(
            Transform parent,
            string name,
            string content,
            float left,
            float top,
            float width,
            float height,
            int size,
            FontStyle style,
            TextAnchor anchor,
            Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.text = content;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.color = color;
            return text;
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string label,
            float left,
            float top,
            float width,
            float height,
            Color color,
            System.Action onClick)
        {
            RectTransform rect = CreateRect(name, parent);
            if (left < 0f)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(left, -top);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(left, -top);
            }

            rect.sizeDelta = new Vector2(width, height);

            Image background = rect.gameObject.AddComponent<Image>();
            background.color = color;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(
                Mathf.Min(1f, color.r + 0.14f),
                Mathf.Min(1f, color.g + 0.14f),
                Mathf.Min(1f, color.b + 0.14f),
                1f);
            colors.pressedColor = Brass;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            AddText(rect, "Label", label, 0f, 0f, width, height,
                18, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor)
                .rectTransform.offsetMin = Vector2.zero;
            return button;
        }

        private void CreateSmallButton(
            Transform parent,
            string name,
            string label,
            float left,
            float top,
            float width,
            float height,
            Color color,
            System.Action onClick)
        {
            CreateButton(parent, name, label, left, top, width, height, color, onClick);
        }

        private GameObject CreateHighlight(Transform parent, float left)
        {
            RectTransform rect = CreateRect("SpeedHighlight", parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left - 4f, -12f);
            rect.sizeDelta = new Vector2(62f, 32f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.90f, 0.70f, 0.32f, 0.28f);
            image.raycastTarget = false;
            rect.SetAsFirstSibling();
            return rect.gameObject;
        }

        private void AddBar(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = offset;
        }
    }
}
