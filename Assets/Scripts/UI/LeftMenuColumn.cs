using ProjectArtOfAll.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 主页面：标题 + 开始游戏 / 设置 / 退出游戏，以及开始游戏下面的模式选择（正常模式 / 无CPU测试模式）。
    /// 整个页面在运行时用代码搭出来（场景里只放这一个组件），这样改版式不用碰 .unity 文件。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class LeftMenuColumn : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private static readonly Color BackgroundColor = new Color(0.028f, 0.038f, 0.048f, 1f);
        private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.088f, 0.96f);
        private static readonly Color Brass = new Color(0.90f, 0.70f, 0.32f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.92f, 0.87f, 1f);
        private static readonly Color MutedText = new Color(0.58f, 0.62f, 0.62f, 1f);

        private static Font menuFont;

        private RectTransform canvasRect;
        private GameSettingsPanel settingsPanel;
        private GameObject modePanel;

        private void Awake()
        {
            // 版式只认代码这一份。场景里如果还留着早期烘焙出来的旧界面（旧按钮绑着已经删掉的方法），
            // 直接销毁重建，避免"看到的是旧主页面"。
            Transform staleCanvas = transform.Find("LeftMenuCanvas");
            if (staleCanvas != null)
            {
                Destroy(staleCanvas.gameObject);
            }

            BuildMenu();
            ApplyMenuFont();
        }

        private void BuildMenu()
        {
            EnsureEventSystem();

            GameObject canvasObject = new GameObject(
                "LeftMenuCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRect = canvasObject.GetComponent<RectTransform>();

            BuildBackground();
            BuildTitle();
            BuildButtons();
            BuildFooter();
            modePanel = BuildModePanel();
            modePanel.SetActive(false);
        }

        // ---------------------------------------------------------------- 背景

        private void BuildBackground()
        {
            RectTransform background = CreateRectTransform("Background", canvasRect);
            Stretch(background);

            Image image = background.gameObject.AddComponent<Image>();
            image.color = BackgroundColor;
            image.raycastTarget = false;

            // 上下两条黄铜细线当作版式骨架。
            AddBar(canvasRect, "TopRule",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -96f), new Vector2(0f, -94f),
                new Color(0.86f, 0.62f, 0.20f, 0.55f));

            AddBar(canvasRect, "BottomRule",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 84f), new Vector2(0f, 86f),
                new Color(0.86f, 0.62f, 0.20f, 0.35f));

            // 右侧装饰框：给标题与按钮让出左侧呼吸空间。
            RectTransform frame = CreateRectTransform("TitleFrame", canvasRect);
            frame.anchorMin = new Vector2(0f, 0.5f);
            frame.anchorMax = new Vector2(0f, 0.5f);
            frame.pivot = new Vector2(0f, 0.5f);
            frame.anchoredPosition = new Vector2(96f, 210f);
            frame.sizeDelta = new Vector2(8f, 220f);

            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(0.86f, 0.62f, 0.20f, 0.85f);
            frameImage.raycastTarget = false;
        }

        private void BuildTitle()
        {
            Text title = CreateText(
                canvasRect, "Title", "Project Art of All",
                new Vector2(140f, -520f), new Vector2(1000f, 96f),
                64, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.color = Brass;

            Text subtitle = CreateText(
                canvasRect, "Subtitle", "骰子 · 卡牌 · 大富翁 · 四人派对",
                new Vector2(144f, -612f), new Vector2(1000f, 40f),
                24, FontStyle.Normal, TextAnchor.MiddleLeft);
            subtitle.color = TextColor;

            Text hint = CreateText(
                canvasRect, "Hint", "阶段 A · 1 名真人 + 3 名 CPU 的完整一局",
                new Vector2(144f, -658f), new Vector2(1000f, 32f),
                18, FontStyle.Normal, TextAnchor.MiddleLeft);
            hint.color = MutedText;
        }

        // ---------------------------------------------------------------- 按钮

        private void BuildButtons()
        {
            CreateMenuButton("01", "开始游戏", 400f, ShowModePanel);
            CreateMenuButton("02", "设置", 492f, OpenSettings);
            CreateMenuButton("03", "退出游戏", 584f, QuitGame);
        }

        private void CreateMenuButton(string index, string label, float top, System.Action onClick)
        {
            const float width = 420f;
            const float height = 72f;

            RectTransform buttonRect = CreateRectTransform("MenuButton_" + label, canvasRect);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(144f, -top);
            buttonRect.sizeDelta = new Vector2(width, height);

            Image background = buttonRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.10f, 0.13f, 0.15f, 0.94f);

            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.10f, 0.13f, 0.15f, 0.94f);
            colors.highlightedColor = new Color(0.19f, 0.24f, 0.26f, 1f);
            colors.pressedColor = new Color(0.73f, 0.56f, 0.28f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.12f, 0.13f, 0.15f, 0.75f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            AddBar(buttonRect, "Accent",
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(4f, 0f),
                new Color(0.86f, 0.62f, 0.20f, 0.95f));

            Text number = CreateText(
                buttonRect, "Index", index,
                new Vector2(24f, -12f), new Vector2(60f, 48f),
                16, FontStyle.Bold, TextAnchor.MiddleLeft);
            number.color = new Color(0.62f, 0.50f, 0.28f, 1f);

            Text labelText = CreateText(
                buttonRect, "Label", label,
                new Vector2(84f, -12f), new Vector2(width - 110f, 48f),
                26, FontStyle.Normal, TextAnchor.MiddleLeft);
            labelText.color = TextColor;
        }

        private void BuildFooter()
        {
            Text mode = CreateText(
                canvasRect, "ModeHint", "开始游戏后可以选择正常模式或「无CPU测试模式」",
                new Vector2(144f, -948f), new Vector2(1200f, 28f),
                16, FontStyle.Normal, TextAnchor.MiddleLeft);
            mode.color = MutedText;

            Text version = CreateText(
                canvasRect, "Version", "v0.4 · 阶段 A 原型",
                new Vector2(144f, -986f), new Vector2(600f, 26f),
                14, FontStyle.Normal, TextAnchor.MiddleLeft);
            version.color = new Color(0.42f, 0.46f, 0.46f, 1f);
        }

        // ------------------------------------------------------------ 模式选择

        private GameObject BuildModePanel()
        {
            const float panelWidth = 820f;
            const float panelHeight = 460f;

            RectTransform root = CreateRectTransform("ModePanel", canvasRect);
            Stretch(root);

            Image scrim = root.gameObject.AddComponent<Image>();
            scrim.color = new Color(0.01f, 0.015f, 0.02f, 0.78f);
            scrim.raycastTarget = true;

            RectTransform panel = CreateRectTransform("Panel", root);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(panelWidth, panelHeight);

            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;

            Text title = CreateText(panel, "Title", "选择开局模式",
                new Vector2(40f, -30f), new Vector2(panelWidth - 80f, 48f),
                30, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.color = Brass;

            Text hint = CreateText(panel, "Hint", "两种模式跑的是同一套规则与同一个内核，只差在谁来操作另外三个座位",
                new Vector2(40f, -78f), new Vector2(panelWidth - 80f, 30f),
                15, FontStyle.Normal, TextAnchor.MiddleCenter);
            hint.color = MutedText;

            BuildModeOption(
                panel,
                "正常模式",
                "1 名真人 + 3 名 CPU。正式玩法的形态，卡槽始终是你的 P1。",
                "推荐",
                128f,
                () => GameSession.StartNewGame(GameMode.Normal));

            BuildModeOption(
                panel,
                "无CPU测试模式",
                "四个座位都由你一个人操作，用来验证规则、复现问题、排查 AI。",
                "测试",
                268f,
                () => GameSession.StartNewGame(GameMode.NoCpuTest));

            CreatePanelButton(panel, "返回", (panelWidth - 220f) * 0.5f, panelHeight - 76f, 220f, 48f,
                () => modePanel.SetActive(false));

            return root.gameObject;
        }

        private void BuildModeOption(
            RectTransform parent,
            string title,
            string description,
            string badge,
            float top,
            System.Action onSelect)
        {
            RectTransform card = CreateRectTransform("Mode_" + title, parent);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(40f, -top);
            card.sizeDelta = new Vector2(740f, 120f);

            Image background = card.gameObject.AddComponent<Image>();
            background.color = new Color(0.10f, 0.13f, 0.15f, 0.96f);

            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.10f, 0.13f, 0.15f, 0.96f);
            colors.highlightedColor = new Color(0.20f, 0.25f, 0.27f, 1f);
            colors.pressedColor = new Color(0.73f, 0.56f, 0.28f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => onSelect?.Invoke());

            AddBar(card, "Accent",
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(5f, 0f),
                new Color(0.86f, 0.62f, 0.20f, 0.95f));

            Text cardTitle = CreateText(card, "Title", title,
                new Vector2(28f, -20f), new Vector2(520f, 40f),
                26, FontStyle.Bold, TextAnchor.MiddleLeft);
            cardTitle.color = TextColor;

            Text cardDescription = CreateText(card, "Description", description,
                new Vector2(28f, -64f), new Vector2(620f, 40f),
                15, FontStyle.Normal, TextAnchor.MiddleLeft);
            cardDescription.color = MutedText;

            Text badgeText = CreateText(card, "Badge", badge,
                new Vector2(620f, -34f), new Vector2(96f, 34f),
                14, FontStyle.Bold, TextAnchor.MiddleCenter);
            badgeText.color = Brass;
        }

        private void CreatePanelButton(
            RectTransform parent,
            string label,
            float left,
            float top,
            float width,
            float height,
            System.Action onClick)
        {
            RectTransform rect = CreateRectTransform("Button_" + label, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);

            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.16f, 0.20f, 0.22f, 1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => onClick?.Invoke());

            Text text = CreateText(rect, "Label", label,
                Vector2.zero, new Vector2(width, height),
                18, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.color = TextColor;
            Stretch(text.rectTransform);
        }

        // ------------------------------------------------------------ 对外操作

        public void ShowModePanel()
        {
            if (modePanel != null)
            {
                modePanel.SetActive(true);
                modePanel.transform.SetAsLastSibling();
            }
        }

        public void HideModePanel()
        {
            if (modePanel != null)
            {
                modePanel.SetActive(false);
            }
        }

        /// <summary>兼容旧入口：等同于「开始游戏」。</summary>
        public void StartGame()
        {
            ShowModePanel();
        }

        public void OpenSettings()
        {
            if (settingsPanel == null)
            {
                settingsPanel = GameSettingsPanel.Create(canvasRect, GetMenuFont());
            }

            settingsPanel.Show();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- 工具

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            RectTransform rect = CreateRectTransform(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>左上角锚点的文字（x/y 从父级左上角量起，y 向下为负）。</summary>
        private Text CreateText(
            Transform parent,
            string name,
            string content,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            FontStyle style,
            TextAnchor alignment)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = GetMenuFont();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.text = content;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));

            eventSystemObject.transform.SetParent(transform, false);
        }

        private static RectTransform CreateRectTransform(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Font GetMenuFont()
        {
            if (menuFont != null)
            {
                return menuFont;
            }

            menuFont = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Microsoft YaHei UI",
                    "Microsoft YaHei",
                    "PingFang SC",
                    "Noto Sans CJK SC",
                    "Arial"
                },
                28);

            if (menuFont == null)
            {
                menuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return menuFont;
        }

        private void ApplyMenuFont()
        {
            Font font = GetMenuFont();
            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                text.font = font;
            }
        }

#if UNITY_EDITOR
        public void BuildMenuForEditor()
        {
            Transform existingCanvas = transform.Find("LeftMenuCanvas");
            if (existingCanvas != null)
            {
                DestroyImmediate(existingCanvas.gameObject);
            }

            settingsPanel = null;
            BuildMenu();
            ApplyMenuFont();
        }
#endif
    }
}
