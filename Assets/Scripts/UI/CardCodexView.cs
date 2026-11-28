using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>Runtime-built, filterable catalog for every base and upgraded card definition.</summary>
    [DisallowMultipleComponent]
    public sealed class CardCodexView : MonoBehaviour
    {
        private const float FilterPanelWidth = 280f;
        private const float GalleryLeft = 332f;
        private const float GalleryTop = 124f;
        private const float GalleryHeight = 888f;
        private const int GridColumns = 3;

        private static readonly Color BackgroundColor = new Color(0.025f, 0.036f, 0.044f, 1f);
        private static readonly Color PanelColor = new Color(0.052f, 0.073f, 0.084f, 0.98f);
        private static readonly Color TileColor = new Color(0.082f, 0.108f, 0.118f, 1f);
        private static readonly Color EventTileColor = new Color(0.19f, 0.145f, 0.075f, 1f);
        private static readonly Color UpgradeTileColor = new Color(0.085f, 0.145f, 0.145f, 1f);
        private static readonly Color ButtonColor = new Color(0.13f, 0.17f, 0.18f, 1f);
        private static readonly Color Brass = new Color(0.92f, 0.70f, 0.31f, 1f);
        private static readonly Color TextColor = new Color(0.94f, 0.93f, 0.88f, 1f);
        private static readonly Color MutedText = new Color(0.62f, 0.66f, 0.65f, 1f);
        private static readonly Color Accent = new Color(0.39f, 0.72f, 0.76f, 1f);
        private static readonly Color UpgradeAccent = new Color(0.51f, 0.76f, 0.58f, 1f);

        private readonly List<GameObject> tileObjects = new List<GameObject>();
        private readonly List<FilterBinding> filterBindings = new List<FilterBinding>();

        [SerializeField] private Font font;
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text galleryTitle;
        [SerializeField] private Text galleryCount;
        private int sizeFilter;
        private CardTag tagFilter = CardTag.None;
        private CardUnlockKind? acquisitionFilter;
        private Func<CardDefinition, string> acquireHandler;
        private string closeLabel = "返回主页面";
        private bool closeOnEscape = true;

        private sealed class FilterBinding
        {
            public Button Button;
            public Image Background;
            public Func<bool> Selected;
        }

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            Bind();
        }

        public static CardCodexView Create(RectTransform canvas, Font font)
        {
            GameObject rootObject = new GameObject(
                "CardCodex",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image background = rootObject.GetComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = true;

            CardCodexView view = rootObject.AddComponent<CardCodexView>();
            view.font = font;
            view.root = rect;
            view.Build();
            rootObject.SetActive(false);
            return view;
        }

        private void Update()
        {
            if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Bind();
            RefreshGallery();
            FocusFirstFilter();
        }

        /// <summary>
        /// 棋盘内开发者图鉴：每张牌出现「获得」按钮，并回调内核发牌。
        /// </summary>
        public void ConfigureDeveloper(
            bool enabled,
            Func<CardDefinition, string> onAcquire)
        {
            acquireHandler = enabled ? onAcquire : null;
            closeLabel = enabled ? "返回游戏" : "返回主页面";
            closeOnEscape = !enabled;
            if (IsOpen)
            {
                Bind();
                RefreshGallery();
            }
        }

        public void Hide()
        {
            CardTooltipHost.Hide();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Baked UnityEvent lambdas are not serialized. Rebuild every button callback when the
        /// scene loads and again when the view opens, matching GameSettingsPanel / CombatLabView.
        /// </summary>
        public void Bind()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (font == null)
            {
                font = BoardHudOverlayFactory.CreateFont();
            }

            if (root == null)
            {
                return;
            }

            filterBindings.Clear();
            BindButton("Close", Hide);
            SetButtonLabel("Close", closeLabel);

            BindFilterButton("SizeAll", "全部", () => sizeFilter == 0, () =>
            {
                sizeFilter = 0;
                RefreshGallery();
            });
            BindFilterButton(
                "Size1",
                "1c",
                () => sizeFilter == 1,
                () => { sizeFilter = 1; RefreshGallery(); });
            BindFilterButton(
                "Size2",
                "2c",
                () => sizeFilter == 2,
                () => { sizeFilter = 2; RefreshGallery(); });
            BindFilterButton(
                "Size3",
                "3c",
                () => sizeFilter == 3,
                () => { sizeFilter = 3; RefreshGallery(); });

            BindFilterButton(
                "TypeAll",
                "全部",
                () => tagFilter == CardTag.None,
                () => { tagFilter = CardTag.None; RefreshGallery(); });
            foreach (CardTag tag in CardCatalogMetadata.TypeFilters)
            {
                CardTag captured = tag;
                BindFilterButton(
                    "Type_" + captured,
                    CardText.TagName(captured),
                    () => tagFilter == captured,
                    () => { tagFilter = captured; RefreshGallery(); });
            }

            BindFilterButton(
                "AcqAll",
                "全部",
                () => !acquisitionFilter.HasValue,
                () => { acquisitionFilter = null; RefreshGallery(); });
            BindFilterButton(
                "AcqNormal",
                "正常获得",
                () => acquisitionFilter == CardUnlockKind.Normal,
                () => { acquisitionFilter = CardUnlockKind.Normal; RefreshGallery(); });
            BindFilterButton(
                "AcqEvent",
                "事件限定",
                () => acquisitionFilter == CardUnlockKind.EventLimited,
                () => { acquisitionFilter = CardUnlockKind.EventLimited; RefreshGallery(); });
            BindFilterButton(
                "AcqUpgrade",
                "融合升级",
                () => acquisitionFilter == CardUnlockKind.FusionUpgrade,
                () => { acquisitionFilter = CardUnlockKind.FusionUpgrade; RefreshGallery(); });
        }

        private void BindButton(string name, Action action)
        {
            Button button = FindButton(name);
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private void BindFilterButton(
            string name,
            string label,
            Func<bool> selected,
            Action action)
        {
            Button button = FindButton(name);
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());

            Text labelText = button.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                labelText.text = label;
                labelText.font = font;
            }

            filterBindings.Add(new FilterBinding
            {
                Button = button,
                Background = button.GetComponent<Image>(),
                Selected = selected
            });
        }

        private Button FindButton(string name)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                if (buttons[index].gameObject.name == name)
                {
                    return buttons[index];
                }
            }

            return null;
        }

        private void SetButtonLabel(string name, string label)
        {
            Button button = FindButton(name);
            Text text = button == null ? null : button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
                text.font = font;
            }
        }

        private void Build()
        {
            AddBar(
                root,
                "TopRule",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -118f),
                new Vector2(0f, -116f),
                new Color(0.86f, 0.62f, 0.20f, 0.45f));

            AddText(root, "Title", "卡牌图鉴", 40f, 24f, 620f, 52f,
                34, FontStyle.Bold, TextAnchor.MiddleLeft, Brass);
            AddText(root, "Subtitle",
                "按尺寸、类型与获取方式筛选；悬停任意卡牌查看完整设计稿效果。",
                42f, 75f, 1300f, 26f,
                16, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

            CreateButton(root, "Close", "返回主页面", -40f, 24f, 210f, 48f,
                new Color(0.30f, 0.16f, 0.12f, 0.96f), Hide);

            BuildFilterPanel();
            BuildGalleryPanel();
        }

        private void BuildFilterPanel()
        {
            RectTransform panel = CreatePanel(
                "CodexFilters",
                32f,
                GalleryTop,
                FilterPanelWidth,
                GalleryHeight);

            AddText(panel, "Header", "筛选", 18f, 14f, 220f, 34f,
                24, FontStyle.Bold, TextAnchor.MiddleLeft, Brass);

            AddSectionLabel(panel, "尺寸", 58f);
            AddFilterButton(panel, "SizeAll", "全部", 18f, 88f, 112f, 34f,
                () => sizeFilter == 0,
                () => { sizeFilter = 0; RefreshGallery(); });
            AddFilterButton(panel, "Size1", "1c", 142f, 88f, 112f, 34f,
                () => sizeFilter == 1,
                () => { sizeFilter = 1; RefreshGallery(); });
            AddFilterButton(panel, "Size2", "2c", 18f, 130f, 112f, 34f,
                () => sizeFilter == 2,
                () => { sizeFilter = 2; RefreshGallery(); });
            AddFilterButton(panel, "Size3", "3c", 142f, 130f, 112f, 34f,
                () => sizeFilter == 3,
                () => { sizeFilter = 3; RefreshGallery(); });

            AddDivider(panel, 184f);
            AddSectionLabel(panel, "类型", 204f);
            AddFilterButton(panel, "TypeAll", "全部", 18f, 234f, 112f, 32f,
                () => tagFilter == CardTag.None,
                () => { tagFilter = CardTag.None; RefreshGallery(); });

            IReadOnlyList<CardTag> tags = CardCatalogMetadata.TypeFilters;
            for (int index = 0; index < tags.Count; index++)
            {
                CardTag captured = tags[index];
                int column = index % 2;
                int row = index / 2;
                AddFilterButton(
                    panel,
                    "Type_" + captured,
                    CardText.TagName(captured),
                    18f + column * 124f,
                    234f + (row + 1) * 38f,
                    112f,
                    32f,
                    () => tagFilter == captured,
                    () => { tagFilter = captured; RefreshGallery(); });
            }

            float acquisitionTop = 492f;
            AddDivider(panel, acquisitionTop);
            AddSectionLabel(panel, "获取方式", acquisitionTop + 20f);
            AddFilterButton(panel, "AcqAll", "全部", 18f, acquisitionTop + 50f, 234f, 34f,
                () => !acquisitionFilter.HasValue,
                () => { acquisitionFilter = null; RefreshGallery(); });
            AddFilterButton(panel, "AcqNormal", "正常获得", 18f, acquisitionTop + 92f, 234f, 34f,
                () => acquisitionFilter == CardUnlockKind.Normal,
                () => { acquisitionFilter = CardUnlockKind.Normal; RefreshGallery(); });
            AddFilterButton(panel, "AcqEvent", "事件限定", 18f, acquisitionTop + 134f, 234f, 34f,
                () => acquisitionFilter == CardUnlockKind.EventLimited,
                () => { acquisitionFilter = CardUnlockKind.EventLimited; RefreshGallery(); });
            AddFilterButton(panel, "AcqUpgrade", "融合升级", 18f, acquisitionTop + 176f, 234f, 34f,
                () => acquisitionFilter == CardUnlockKind.FusionUpgrade,
                () => { acquisitionFilter = CardUnlockKind.FusionUpgrade; RefreshGallery(); });

            AddText(panel, "Legend",
                "事件限定：只在事件中获得\n融合升级：获得同名牌后自动升级",
                18f,
                GalleryHeight - 74f,
                244f,
                50f,
                13,
                FontStyle.Normal,
                TextAnchor.UpperLeft,
                MutedText);
        }

        private void BuildGalleryPanel()
        {
            RectTransform panel = CreatePanel(
                "CodexGallery",
                GalleryLeft,
                GalleryTop,
                1556f,
                GalleryHeight);

            galleryTitle = AddText(panel, "Header", "全部卡牌", 18f, 14f, 520f, 34f,
                24, FontStyle.Bold, TextAnchor.MiddleLeft, Brass);
            galleryCount = AddText(panel, "Count", string.Empty, 18f, 46f, 520f, 22f,
                14, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

            RectTransform viewport = CreateRect("Viewport", panel);
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(14f, 14f);
            viewport.offsetMax = new Vector2(-14f, -82f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0.018f, 0.026f, 0.031f, 0.7f);

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.spacing = new Vector2(14f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            grid.cellSize = new Vector2(486f, 158f);
            grid.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 34f;
        }

        private void RefreshGallery()
        {
            if (content == null)
            {
                return;
            }

            for (int index = 0; index < tileObjects.Count; index++)
            {
                if (tileObjects[index] != null)
                {
                    tileObjects[index].SetActive(false);
                    if (Application.isPlaying)
                    {
                        Destroy(tileObjects[index]);
                    }
                    else
                    {
                        DestroyImmediate(tileObjects[index]);
                    }
                }
            }

            tileObjects.Clear();

            // Defensive cleanup for editor previews and interrupted refreshes.
            for (int index = content.childCount - 1; index >= 0; index--)
            {
                Transform child = content.GetChild(index);
                if (!child.name.StartsWith("Codex_", StringComparison.Ordinal))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            IReadOnlyList<CardDefinition> cards = CardCatalogMetadata.Filter(
                sizeFilter,
                tagFilter,
                acquisitionFilter);
            galleryTitle.text = BuildFilterTitle();
            galleryCount.text = string.Concat(
                cards.Count,
                " / ",
                CardCatalogMetadata.All.Count,
                " 张 · 基础牌与融合升级牌均计入",
                acquireHandler == null ? string.Empty : " · 开发者模式：可点击获得");

            for (int index = 0; index < cards.Count; index++)
            {
                BuildCardTile(cards[index]);
            }

            RefreshFilterColors();
        }

        private void BuildCardTile(CardDefinition card)
        {
            CardUnlockKind acquisition = CardCatalogMetadata.AcquisitionOf(card);
            Color tileColor = acquisition == CardUnlockKind.EventLimited
                ? EventTileColor
                : acquisition == CardUnlockKind.FusionUpgrade
                    ? UpgradeTileColor
                    : TileColor;

            RectTransform tile = CreateRect("Codex_" + card.Id, content);
            Image image = tile.gameObject.AddComponent<Image>();
            image.color = tileColor;

            AddBar(
                tile,
                "Edge",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(5f, 0f),
                CardRowRenderer.FrameColor(card.SlotSize));

            AddText(tile, "Name", card.DisplayName, 16f, 10f, 330f, 28f,
                21, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);
            AddText(tile, "Size", card.SlotSize + "c", 410f, 10f, 58f, 28f,
                16, FontStyle.Bold, TextAnchor.MiddleRight,
                CardRowRenderer.FrameColor(card.SlotSize));
            AddText(tile, "Meta", CardText.Meta(card), 16f, 39f, 380f, 20f,
                12, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

            Color acquisitionColor = acquisition == CardUnlockKind.EventLimited
                ? Brass
                : acquisition == CardUnlockKind.FusionUpgrade
                    ? UpgradeAccent
                    : Accent;
            AddText(
                tile,
                "Acquisition",
                CardCatalogMetadata.AcquisitionLabel(acquisition),
                378f,
                39f,
                90f,
                20f,
                12,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                acquisitionColor);
            AddText(tile, "Type", CardText.TagList(card.Tags), 16f, 62f, 454f, 20f,
                13, FontStyle.Normal, TextAnchor.MiddleLeft, TextColor);

            IReadOnlyList<string> body = CardDesignText.Lines(card);
            string summary = body.Count == 0
                ? CardText.GeneratedDescription(card)
                : string.Join("  ", body);
            if (summary.Length > 86)
            {
                summary = summary.Substring(0, 84) + "…";
            }

            float bodyWidth = acquireHandler == null ? 454f : 350f;
            AddText(tile, "Body", summary, 16f, 88f, bodyWidth, 54f,
                13, FontStyle.Normal, TextAnchor.UpperLeft, MutedText);

            if (acquireHandler != null)
            {
                CardDefinition captured = card;
                Button acquire = CreateButton(
                    tile,
                    "Acquire",
                    "获得",
                    378f,
                    102f,
                    90f,
                    38f,
                    new Color(0.55f, 0.38f, 0.10f, 1f),
                    () =>
                    {
                        string result = acquireHandler?.Invoke(captured);
                        if (!string.IsNullOrEmpty(result) && galleryCount != null)
                        {
                            galleryCount.text = result;
                        }
                    });
                acquire.transform.SetAsLastSibling();
            }

            CardHoverTarget hover = tile.gameObject.AddComponent<CardHoverTargetSceneView>();
            hover.Bind(card, CardTooltipHost.CanvasRect(root), font, false);
            tileObjects.Add(tile.gameObject);
        }

        private string BuildFilterTitle()
        {
            List<string> parts = new List<string>();
            parts.Add(sizeFilter <= 0 ? "全部尺寸" : sizeFilter + "c");
            parts.Add(tagFilter == CardTag.None ? "全部类型" : CardText.TagName(tagFilter));
            parts.Add(acquisitionFilter.HasValue
                ? CardCatalogMetadata.AcquisitionLabel(acquisitionFilter.Value)
                : "全部获取方式");
            return string.Join(" · ", parts);
        }

        private void AddFilterButton(
            Transform parent,
            string name,
            string label,
            float left,
            float top,
            float width,
            float height,
            Func<bool> selected,
            Action onClick)
        {
            Button button = CreateButton(parent, name, label, left, top, width, height, ButtonColor, onClick);
            Image background = button.GetComponent<Image>();
            filterBindings.Add(new FilterBinding
            {
                Button = button,
                Background = background,
                Selected = selected
            });
        }

        private void RefreshFilterColors()
        {
            for (int index = 0; index < filterBindings.Count; index++)
            {
                FilterBinding binding = filterBindings[index];
                bool selected = binding.Selected();
                Color color = selected
                    ? new Color(0.33f, 0.27f, 0.14f, 1f)
                    : ButtonColor;
                binding.Background.color = color;
                ColorBlock colors = binding.Button.colors;
                colors.normalColor = color;
                binding.Button.colors = colors;
            }
        }

        private void FocusFirstFilter()
        {
            if (filterBindings.Count == 0 || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(filterBindings[0].Button.gameObject);
        }

        private RectTransform CreatePanel(
            string name,
            float left,
            float top,
            float width,
            float height)
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

        private void AddSectionLabel(Transform parent, string label, float top)
        {
            AddText(parent, "Section_" + label, label, 18f, top, 220f, 24f,
                15, FontStyle.Bold, TextAnchor.MiddleLeft, Accent);
        }

        private void AddDivider(Transform parent, float top)
        {
            AddBar(
                parent,
                "Divider",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(16f, -top),
                new Vector2(-16f, -top + 1f),
                new Color(0.86f, 0.62f, 0.20f, 0.25f));
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
            Action onClick)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            if (left < 0f)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
            }

            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);

            Image background = rect.gameObject.AddComponent<Image>();
            background.color = color;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = new Color(
                Mathf.Min(1f, color.r + 0.12f),
                Mathf.Min(1f, color.g + 0.12f),
                Mathf.Min(1f, color.b + 0.12f),
                1f);
            colors.pressedColor = Brass;
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.07f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            AddText(rect, "Label", label, 0f, 0f, width, height,
                15, FontStyle.Bold, TextAnchor.MiddleCenter, TextColor);
            return button;
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
            TextAnchor alignment,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);

            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.text = content;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.color = color;
            return text;
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
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
