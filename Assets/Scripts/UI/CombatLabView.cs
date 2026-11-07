using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Match;
using UnityEngine;
using UnityEngine.EventSystems;
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

        /// <summary>战斗栏整行的宽度（面板 1260 减去左右各 20 的内边距）。</summary>
        private const float DeckRowWidth = 1220f;

        /// <summary>战斗栏行高上限；实际格子高度由 <see cref="lane"/> 的缩放决定。</summary>
        private const float LaneHeight = 132f;

        /// <summary>
        /// 战斗栏与常驻卡槽带共用同一份绘制（格子、尺寸边框、价值角标、文字），
        /// 所以这里只要把宽度和缩放交给它，两边看上去就是同一条栏位。
        /// </summary>
        private CardRowRenderer lane;

        private float SlotWidth => lane.SlotWidth;

        private float CardHeight => lane.CardHeight;

        private float WidthOf(CardDefinition card)
        {
            int slots = Mathf.Max(1, card.SlotSize);
            return slots * SlotWidth + (slots - 1) * lane.SlotGap;
        }

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
        private readonly List<GameObject> leftCells = new List<GameObject>();
        private readonly List<GameObject> rightCells = new List<GameObject>();

        [SerializeField] private Font font;
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform leftDeckRow;
        [SerializeField] private RectTransform rightDeckRow;
        [SerializeField] private Text leftDeckLabel;
        [SerializeField] private Text rightDeckLabel;
        [SerializeField] private Text leftHealthLabel;
        [SerializeField] private Text rightHealthLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private CombatPlaybackView playback;
        [SerializeField] private GameObject speedHighlight;
        private int leftHealth = DefaultHealth;
        private int rightHealth = DefaultHealth;
        private float preferredSpeed = 2f;
        private int nextUid = 1;
        private bool busy;
        private GameObject ghostObject;
        private CardDefinition dragCard;
        private bool dragging;

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

        private void Awake()
        {
            Bind();
        }

        public void Bind()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (font == null)
            {
                font = CardTooltipHost.CanvasRect(root) != null
                    ? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    : null;
            }

            if (lane == null && font != null)
            {
                lane = new CardRowRenderer(font)
                {
                    RowWidth = DeckRowWidth,
                    InBattle = true,
                    TopInset = 0f
                };
                lane.Scale = lane.FitScale(LaneHeight);
            }

            BindButton("BackHome", BackToMainMenu);
            BindButton("LeftMinus", () => AdjustHealth(true, -HealthStep));
            BindButton("LeftPlus", () => AdjustHealth(true, HealthStep));
            BindButton("RightMinus", () => AdjustHealth(false, -HealthStep));
            BindButton("RightPlus", () => AdjustHealth(false, HealthStep));
            BindButton("Speed1", () => SetSpeed(1f));
            BindButton("Speed2", () => SetSpeed(2f));
            BindButton("Speed4", () => SetSpeed(4f));
            BindButton("Fight", StartBattle);
            BindButton("Copy", CopyLeftToRight);
            BindButton("Back", BackToMainMenu);
            BindButton("Clear", () => ClearDeck(true), "LeftDeckPanel");
            BindButton("Clear", () => ClearDeck(false), "RightDeckPanel");
            BindCardLibrary();
        }

        private void BindButton(
            string buttonName,
            System.Action action,
            string parentName = null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (button.gameObject.name != buttonName)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(parentName) &&
                    (button.transform.parent == null ||
                     button.transform.parent.parent == null ||
                     button.transform.parent.parent.gameObject.name != parentName))
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => action?.Invoke());
            }
        }

        private void BindCardLibrary()
        {
            Transform library = transform.Find("CardLibrary");
            if (library == null)
            {
                return;
            }

            int index = 0;
            foreach (Transform row in library)
            {
                if (!row.name.StartsWith("Card_"))
                {
                    continue;
                }

                string id = row.name.Substring("Card_".Length);
                CardDefinition card = CardDatabase.Instance.Get(id);
                if (card == null)
                {
                    continue;
                }

                CombatLabDragHandler drag = row.GetComponent<CombatLabDragHandler>();
                if (drag != null)
                {
                    drag.Bind(this, card, true, true, index);
                }

                CardHoverTarget hover = row.GetComponent<CardHoverTarget>();
                if (hover != null)
                {
                    hover.Bind(card, CardTooltipHost.CanvasRect(root), font, false);
                }

                BindRowButton(row, "Left", () => AddCard(card, true));
                BindRowButton(row, "Right", () => AddCard(card, false));
                index++;
            }
        }

        private static void BindRowButton(
            Transform row,
            string name,
            System.Action action)
        {
            Transform child = row.Find(name);
            Button button = child == null ? null : child.GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RebuildDecks();
        }

        public void Hide()
        {
            CardTooltipHost.Hide();
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ 版式

        private void Build()
        {
            lane = new CardRowRenderer(font)
            {
                RowWidth = DeckRowWidth,
                InBattle = true,
                TopInset = 0f
            };
            lane.Scale = lane.FitScale(LaneHeight);

            Image background = gameObject.AddComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = true;

            AddBar(root, "TopRule", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -118f), new Vector2(0f, -116f),
                new Color(0.86f, 0.62f, 0.20f, 0.45f));

            AddText(root, "Title", "战斗模拟 · 自定义对局", 48f, 26f, 900f, 48f,
                32, FontStyle.Bold, TextAnchor.MiddleLeft, Brass);
            AddText(root, "Hint",
                "按住卡牌库里的任意一张拖进左边或右边的战斗栏就能放牌，栏内拖动可换顺序、拖出栏外就是移除（战斗栏上限 "
                    + BattleCapacity + "c）；各自设定最大生命值，开打后可用 1x / 2x / 4x 调速。",
                48f, 74f, 1700f, 28f, 16, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

            // 右上角常驻「返回主页面」：不管从哪进来都能一步退回主菜单。
            CreateButton(root, "BackHome", "返回主页面", -48f, 26f, 200f, 48f,
                new Color(0.32f, 0.16f, 0.11f, 0.95f), BackToMainMenu);

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
            AddText(panel, "Sub", "直接拖进战斗栏，或用「左」「右」按钮快速加入；同一张可以重复拿",
                20f, 46f, 520f, 24f, 14, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

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

                // 整行都可以按住往战斗栏里拖。
                CombatLabDragHandler drag =
                    rowRect.gameObject.AddComponent<CombatLabDragHandler>();
                drag.Bind(this, card, true, true, index);

                CardHoverTarget hover =
                    rowRect.gameObject.AddComponent<CardHoverTargetSceneView>();
                hover.Bind(card, CardTooltipHost.CanvasRect(root), font, false);
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
            CreateButton(bar, "Back", "返回主页面", -20f, 70f, 220f, 48f, RowColor, BackToMainMenu);

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
            CardTooltipHost.Hide();
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
                    + "c   （拖动换顺序，拖出栏外或点击移除）";
                label.color = used > BattleCapacity ? Brass : MutedText;
            }

            // 先画空槽位，再把卡摆上去：和常驻卡槽带是同一套格子。
            lane.BuildCells(row, isLeft ? leftCells : rightCells, BattleCapacity);
            CardTooltipHost.Hide();

            float x = CardRowRenderer.RowInset;

            for (int index = 0; index < deck.Count; index++)
            {
                CardDefinition card = deck[index];
                float width = WidthOf(card);

                GameObject chip = lane.AddCard(row, new CardInstance(index, card), x, width, null);
                chip.name = "Chip_" + index;
                x += width + lane.SlotGap;

                CombatLabDragHandler drag =
                    chip.AddComponent<CombatLabDragHandler>();
                drag.Bind(this, card, false, isLeft, index);

                Button button = chip.AddComponent<Button>();
                button.targetGraphic = chip.GetComponent<Image>();
                int captured = index;
                button.onClick.AddListener(() => RemoveCard(isLeft, captured));

                CardHoverTarget hover = chip.AddComponent<CardHoverTarget>();
                hover.Bind(card, CardTooltipHost.CanvasRect(root), font);

                chips.Add(chip);
            }

            if (deck.Count == 0)
            {
                Text empty = AddText(row, "Empty",
                    isLeft ? "把卡牌库里的牌拖到这里" : "把卡牌库里的牌拖到这里",
                    2f, 6f, 600f, 26f, 15, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);
                chips.Add(empty.gameObject);
            }
        }

        // ------------------------------------------------------------------ 拖拽排版

        private void InsertCard(CardDefinition card, bool toLeft, int index)
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

            int insert = Mathf.Clamp(index, 0, deck.Count);
            deck.Insert(insert, card);
            SetStatus("已把 " + card.DisplayName + " 放进" + (toLeft ? "左方" : "右方")
                + "第 " + (insert + 1) + " 位。");
            RebuildDecks();
        }

        private void MoveCard(bool fromLeft, int index, bool toLeft, int targetIndex)
        {
            if (busy)
            {
                return;
            }

            List<CardDefinition> source = fromLeft ? leftDeck : rightDeck;
            if (index < 0 || index >= source.Count)
            {
                return;
            }

            CardDefinition card = source[index];
            if (fromLeft == toLeft)
            {
                source.RemoveAt(index);
                int insert = Mathf.Clamp(
                    targetIndex > index ? targetIndex - 1 : targetIndex, 0, source.Count);
                source.Insert(insert, card);
                SetStatus("已把 " + card.DisplayName + " 挪到第 " + (insert + 1) + " 位。");
                RebuildDecks();
                return;
            }

            List<CardDefinition> target = toLeft ? leftDeck : rightDeck;
            int used = target.Sum(entry => entry.SlotSize);
            if (used + card.SlotSize > BattleCapacity)
            {
                SetStatus((toLeft ? "左方" : "右方") + "战斗栏放不下 " + card.DisplayName + "。");
                return;
            }

            source.RemoveAt(index);
            int targetInsert = Mathf.Clamp(targetIndex, 0, target.Count);
            target.Insert(targetInsert, card);
            SetStatus("已把 " + card.DisplayName + " 移到" + (toLeft ? "左方" : "右方")
                + "第 " + (targetInsert + 1) + " 位。");
            RebuildDecks();
        }

        private bool TryResolveDrop(PointerEventData eventData, out bool toLeft, out int index)
        {
            toLeft = false;
            index = 0;
            if (eventData == null)
            {
                return false;
            }

            Camera camera = eventData.pressEventCamera;
            if (leftDeckRow != null
                && RectTransformUtility.RectangleContainsScreenPoint(leftDeckRow, eventData.position, camera))
            {
                toLeft = true;
                index = IndexAt(leftDeckRow, leftDeck, eventData);
                return true;
            }

            if (rightDeckRow != null
                && RectTransformUtility.RectangleContainsScreenPoint(rightDeckRow, eventData.position, camera))
            {
                toLeft = false;
                index = IndexAt(rightDeckRow, rightDeck, eventData);
                return true;
            }

            return false;
        }

        /// <summary>落点索引：按每张牌实际占的格数累加宽度，指针落在哪张牌前半段就插在它前面。</summary>
        private int IndexAt(
            RectTransform row,
            IReadOnlyList<CardDefinition> deck,
            PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    row, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return deck.Count;
            }

            float x = CardRowRenderer.RowInset;
            for (int index = 0; index < deck.Count; index++)
            {
                float width = WidthOf(deck[index]);
                if (local.x < x + width * 0.5f)
                {
                    return index;
                }

                x += width + lane.SlotGap;
            }

            return deck.Count;
        }

        internal void BeginDrag(
            CardDefinition card,
            bool fromLibrary,
            bool fromLeft,
            PointerEventData eventData)
        {
            if (busy)
            {
                return;
            }

            CardTooltipHost.Hide();
            dragging = true;
            ShowGhost(card, fromLeft || fromLibrary);
            MoveGhostTo(eventData);
        }

        internal void DragTo(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            MoveGhostTo(eventData);
        }

        internal void EndDrag(
            bool fromLibrary,
            bool fromLeft,
            int index,
            PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            HideGhost();
            if (busy)
            {
                return;
            }

            bool hasTarget = TryResolveDrop(eventData, out bool toLeft, out int targetIndex);
            if (fromLibrary)
            {
                if (!hasTarget)
                {
                    SetStatus("把卡牌拖进左边或右边的战斗栏才会放进去。");
                    return;
                }

                InsertCard(dragCard, toLeft, targetIndex);
                return;
            }

            if (hasTarget)
            {
                MoveCard(fromLeft, index, toLeft, targetIndex);
                return;
            }

            RemoveCard(fromLeft, index);
        }

        private void ShowGhost(CardDefinition card, bool leftTint)
        {
            HideGhost();
            dragCard = card;

            RectTransform ghost = CreateRect("DragGhost", root);
            ghost.anchorMin = new Vector2(0.5f, 0.5f);
            ghost.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.pivot = new Vector2(0.5f, 0.5f);
            ghost.sizeDelta = new Vector2(WidthOf(card), CardHeight);

            Image image = ghost.gameObject.AddComponent<Image>();
            image.color = leftTint ? LeftTint : RightTint;
            image.raycastTarget = false;

            CanvasGroup group = ghost.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0.94f;

            CardRowRenderer.AddFramedBorder(
                ghost, CardRowRenderer.FrameColor(card.SlotSize), 2f, 9f);
            AddText(ghost, "Name", card.DisplayName, 10f, 8f, WidthOf(card) - 20f, 30f,
                17, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            AddText(ghost, "Meta", CardText.Meta(card), 10f, 42f, WidthOf(card) - 20f, 22f,
                12, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);
            AddText(ghost, "Slots", card.SlotSize + "c", 10f, 102f, 40f, 20f,
                13, FontStyle.Bold, TextAnchor.MiddleLeft, CardRowRenderer.FrameColor(card.SlotSize));

            ghost.SetAsLastSibling();
            ghostObject = ghost.gameObject;
        }

        private void MoveGhostTo(PointerEventData eventData)
        {
            if (ghostObject == null || root == null || eventData == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                ((RectTransform)ghostObject.transform).anchoredPosition = local;
            }
        }

        private void HideGhost()
        {
            if (ghostObject != null)
            {
                Destroy(ghostObject);
                ghostObject = null;
            }
        }

        /// <summary>卡牌库的一行和战斗栏里的卡牌都挂这个，交给 <see cref="CombatLabView"/> 处理落点。</summary>
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

            if (playback != null)
            {
                Destroy(playback.gameObject);
                playback = null;
            }

            playback = CombatPlaybackView.Create(transform, font);

            busy = true;
            playback.Play(resolved, leftLabel, rightLabel, true, () =>
            {
                busy = false;
                SetStatus("这一场打完了，可以改卡组再打一次。");
                if (playback != null)
                {
                    Destroy(playback.gameObject);
                    playback = null;
                }
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

        /// <summary>退回主页面：收掉回放层与模拟面板，必要时直接载入主页面场景。</summary>
        private void BackToMainMenu()
        {
            if (playback != null)
            {
                Destroy(playback.gameObject);
                playback = null;
            }

            bool inMenu = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                == ProjectArtOfAll.Flow.GameSession.MenuSceneName;

            Close();

            if (!inMenu)
            {
                ProjectArtOfAll.Flow.GameSession.BackToMenu();
            }
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
