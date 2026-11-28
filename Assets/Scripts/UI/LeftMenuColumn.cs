using ProjectArtOfAll.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 主页面：左上标题 + 数据驱动的左侧菜单列，以及开始游戏下面的模式选择。
    /// 菜单项由 <see cref="CreateMenuEntries"/> 统一创建，新增选项会自动复用布局、入场和悬停动画。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class LeftMenuColumn : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float MenuLeft = 96f;
        private const float TitleTop = 96f;
        private const float MenuTop = 292f;
        private const float ItemHeight = 80f;
        private const float ItemPitch = 96f;
        private const float ItemTextLeft = 28f;
        private const float HoverInset = 4f;
        private const float HoverLineWidth = 3f;
        private const float HoverSlant = 0f;
        private const float HoverRightBlankRatio = 0.28f;
        private const float HoverRightBlankExtra = 24f;
        private const int MenuTextSize = 38;
        private const int TitleTextSize = 64;
        private const string ProjectTitle = "Project : Art of All";
        private const string TitleFontName = "Times New Roman";

        private static readonly Color PanelColor = new Color(0.055f, 0.075f, 0.088f, 0.96f);
        private static readonly Color Brass = new Color(0.90f, 0.70f, 0.32f, 1f);
        private static readonly Color TextColor = new Color(0.93f, 0.92f, 0.87f, 1f);
        private static readonly Color MutedText = new Color(0.58f, 0.62f, 0.62f, 1f);
        private static readonly Color VersionColor = new Color(0.58f, 0.58f, 0.58f, 0.92f);

        private static Font menuFont;
        private static Font titleFont;

        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform menuColumn;
        [SerializeField] private GameSettingsPanel settingsPanel;
        [SerializeField] private GameObject modePanel;
        [SerializeField] private CombatLabView combatLab;
        [SerializeField] private CardCodexView cardCodex;
        [SerializeField] private AudioSource hoverAudioSource;

        private AudioClip hoverClip;

        private void Awake()
        {
            EnsureEventSystem();
            EnsureStartMenuBgm();
            EnsureHoverAudio();
            if (canvasRect == null || modePanel == null)
            {
                BuildMenu();
            }
            else
            {
                EnsureSnowField();
                EnsureMenuColumn();
            }

            BindMenu();

            if (settingsPanel != null)
            {
                settingsPanel.Bind();
            }

            ApplyMenuFont();
            ApplyTitleFont();
        }

        private void Start()
        {
            if (menuColumn == null)
            {
                return;
            }

            int introIndex = 0;
            for (int index = 0; index < menuColumn.childCount; index++)
            {
                StartMenuOptionView option =
                    menuColumn.GetChild(index).GetComponent<StartMenuOptionView>();
                if (option == null)
                {
                    continue;
                }

                option.PlayIntro(introIndex);
                introIndex++;
            }
        }

        private void BindMenu()
        {
            MenuEntry[] entries = CreateMenuEntries();
            for (int index = 0; index < entries.Length; index++)
            {
                BindButton("MenuButton_" + entries[index].Label, entries[index].Action);
            }

            BindButton("Mode_正常模式", () => GameSession.ChooseMode(GameMode.Normal));
            BindButton("Mode_无CPU测试模式", () => GameSession.ChooseMode(GameMode.NoCpuTest));
            BindButton("Button_返回", HideModePanel);
        }

        private void BindButton(string objectName, System.Action action)
        {
            Button button = FindButton(objectName);
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private Button FindButton(string objectName)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            Button inactiveMatch = null;
            for (int index = 0; index < buttons.Length; index++)
            {
                if (buttons[index].gameObject.name == objectName)
                {
                    if (buttons[index].gameObject.activeInHierarchy)
                    {
                        return buttons[index];
                    }

                    if (inactiveMatch == null)
                    {
                        inactiveMatch = buttons[index];
                    }
                }
            }

            return inactiveMatch;
        }

        private MenuEntry[] CreateMenuEntries()
        {
            return new[]
            {
                new MenuEntry("开始游戏", ShowModePanel),
                new MenuEntry("卡牌图鉴", OpenCodex),
                new MenuEntry("设置", OpenSettings),
                new MenuEntry("战斗模拟", OpenCombatLab),
                new MenuEntry("退出游戏", QuitGame)
            };
        }

        private void BuildMenu()
        {
            EnsureEventSystem();
            EnsureHoverAudio();

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
            BuildSnowField();
            BuildTitle();
            BuildVersion();
            BuildMenuOptions();
            modePanel = BuildModePanel();
            modePanel.SetActive(false);
        }

        // ---------------------------------------------------------------- 背景

        private void BuildBackground()
        {
            RectTransform background = CreateRectTransform("Background", canvasRect);
            background.anchorMin = new Vector2(0.5f, 0.5f);
            background.anchorMax = new Vector2(0.5f, 0.5f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

            Image image = background.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("StartMenu/StartMenuBackground");
            image.color = image.sprite != null
                ? Color.white
                : new Color(0.028f, 0.038f, 0.048f, 1f);
            image.preserveAspect = false;
            image.raycastTarget = false;

            StartMenuParallaxBackground parallax =
                background.gameObject.AddComponent<StartMenuParallaxBackground>();
            parallax.Configure(canvasRect, 1.08f, 18f, 0.12f);
        }

        private StartMenuSnowField BuildSnowField()
        {
            RectTransform snowRect = CreateRectTransform("SnowField", canvasRect);
            Stretch(snowRect);
            if (snowRect.GetComponent<CanvasRenderer>() == null)
            {
                snowRect.gameObject.AddComponent<CanvasRenderer>();
            }

            StartMenuSnowField snow =
                snowRect.gameObject.AddComponent<StartMenuSnowField>();
            snow.Configure(0.12f);
            snow.raycastTarget = false;
            snowRect.SetSiblingIndex(Mathf.Min(1, canvasRect.childCount - 1));
            return snow;
        }

        private void EnsureSnowField()
        {
            if (canvasRect == null)
            {
                return;
            }

            Transform existing = canvasRect.Find("SnowField");
            if (existing == null)
            {
                BuildSnowField();
                return;
            }

            StartMenuSnowField snow = existing.GetComponent<StartMenuSnowField>();
            if (existing.GetComponent<CanvasRenderer>() == null)
            {
                existing.gameObject.AddComponent<CanvasRenderer>();
            }

            if (snow == null)
            {
                snow = existing.gameObject.AddComponent<StartMenuSnowField>();
            }

            snow.Configure(0.12f);
            existing.SetSiblingIndex(Mathf.Min(1, canvasRect.childCount - 1));
        }

        private void BuildTitle()
        {
            Text title = CreateText(
                canvasRect, "Title", ProjectTitle,
                new Vector2(MenuLeft, -TitleTop), new Vector2(1200f, 82f),
                TitleTextSize, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.color = Color.white;
            title.font = GetTitleFont();
            AddTextEffect(title, Color.white, Color.white, 4f);
        }

        private void BuildVersion()
        {
            Text version = CreateText(
                canvasRect, "Version", "v0.4",
                Vector2.zero, new Vector2(200f, 24f),
                18, FontStyle.Normal, TextAnchor.MiddleLeft);
            RectTransform rect = version.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(MenuLeft, 56f);
            version.color = VersionColor;
        }

        // ---------------------------------------------------------------- 菜单

        private void EnsureMenuColumn()
        {
            MenuEntry[] entries = CreateMenuEntries();
            if (menuColumn == null && canvasRect != null)
            {
                menuColumn = canvasRect.Find("MenuColumn") as RectTransform;
            }

            if (menuColumn != null && MenuColumnMatches(menuColumn, entries))
            {
                return;
            }

            if (menuColumn != null)
            {
                menuColumn.gameObject.SetActive(false);
                Destroy(menuColumn.gameObject);
                menuColumn = null;
            }

            BuildMenuColumn(entries);
        }

        private static bool MenuColumnMatches(
            RectTransform column,
            MenuEntry[] entries)
        {
            StartMenuOptionView[] options =
                column.GetComponentsInChildren<StartMenuOptionView>(true);
            if (options.Length != entries.Length)
            {
                return false;
            }

            for (int index = 0; index < options.Length; index++)
            {
                if (options[index].Label != entries[index].Label)
                {
                    return false;
                }

                Transform item = options[index].transform;
                Transform hit = item.Find("HitArea");
                Transform hover = item.Find("HoverBackground");
                Transform line = item.Find("GoldLine");
                if (item.GetComponent<Image>() != null ||
                    hit == null || hover == null || line == null)
                {
                    return false;
                }

                TrapezoidGraphic hitGraphic = hit.GetComponent<TrapezoidGraphic>();
                TrapezoidGraphic hoverGraphic = hover.GetComponent<TrapezoidGraphic>();
                StartMenuWipeGraphic lineGraphic =
                    line.GetComponent<StartMenuWipeGraphic>();
                RectTransform hitRect = hit as RectTransform;
                RectTransform hoverRect = hover as RectTransform;
                if (hitGraphic == null || hoverGraphic == null ||
                    lineGraphic == null ||
                    hit.GetComponent<StartMenuOptionHitArea>() == null ||
                    hitRect == null || hoverRect == null ||
                    hitGraphic.raycastTarget == false ||
                    !Mathf.Approximately(hoverGraphic.RightSlant, HoverSlant) ||
                    hoverGraphic.color != StartMenuOptionView.HoverGray ||
                    lineGraphic.color != StartMenuOptionView.GoldLine ||
                    !Mathf.Approximately(
                        hitRect.anchoredPosition.x,
                        hoverRect.anchoredPosition.x) ||
                    !Mathf.Approximately(
                        hitRect.anchoredPosition.y,
                        hoverRect.anchoredPosition.y) ||
                    !Mathf.Approximately(
                        hitRect.sizeDelta.x,
                        hoverRect.sizeDelta.x) ||
                    !Mathf.Approximately(
                        hitRect.sizeDelta.y,
                        hoverRect.sizeDelta.y) ||
                    !Mathf.Approximately(
                        hitGraphic.RightSlant,
                        hoverGraphic.RightSlant))
                {
                    return false;
                }
            }

            return true;
        }

        private void BuildMenuOptions()
        {
            BuildMenuColumn(CreateMenuEntries());
        }

        private void BuildMenuColumn(MenuEntry[] entries)
        {
            menuColumn = CreateRectTransform("MenuColumn", canvasRect);
            menuColumn.anchorMin = new Vector2(0f, 1f);
            menuColumn.anchorMax = new Vector2(0f, 1f);
            menuColumn.pivot = new Vector2(0f, 1f);
            menuColumn.anchoredPosition = new Vector2(MenuLeft, -MenuTop);
            menuColumn.sizeDelta = new Vector2(760f, entries.Length * ItemPitch);

            for (int index = 0; index < entries.Length; index++)
            {
                CreateMenuOption(menuColumn, entries[index], index);
            }
        }

        private void CreateMenuOption(RectTransform parent, MenuEntry entry, int index)
        {
            float effectHeight = ItemHeight - HoverInset * 2f;

            RectTransform item = CreateRectTransform("MenuButton_" + entry.Label, parent);
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(0f, 1f);
            item.pivot = new Vector2(0f, 1f);
            item.anchoredPosition = new Vector2(0f, -index * ItemPitch);
            item.sizeDelta = new Vector2(600f, ItemHeight);

            CanvasGroup optionGroup = item.gameObject.AddComponent<CanvasGroup>();

            Button button = item.gameObject.AddComponent<Button>();
            button.targetGraphic = null;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            RectTransform hoverBackground =
                CreateRectTransform("HoverBackground", item);
            hoverBackground.anchorMin = new Vector2(0f, 0.5f);
            hoverBackground.anchorMax = new Vector2(0f, 0.5f);
            hoverBackground.pivot = new Vector2(0f, 0.5f);
            hoverBackground.anchoredPosition = new Vector2(12f, 0f);
            hoverBackground.sizeDelta = new Vector2(0f, effectHeight);

            TrapezoidGraphic hoverGraphic =
                hoverBackground.gameObject.AddComponent<TrapezoidGraphic>();
            if (hoverBackground.GetComponent<CanvasRenderer>() == null)
            {
                hoverBackground.gameObject.AddComponent<CanvasRenderer>();
            }

            hoverGraphic.color = StartMenuOptionView.HoverGray;
            hoverGraphic.RightSlant = HoverSlant;
            hoverGraphic.raycastTarget = false;
            hoverGraphic.SetHorizontalWipe(0f, 0f);

            CanvasGroup hoverBackgroundGroup =
                hoverBackground.gameObject.AddComponent<CanvasGroup>();
            hoverBackgroundGroup.alpha = 0f;

            RectTransform goldLine = CreateRectTransform("GoldLine", item);
            goldLine.anchorMin = new Vector2(0f, 0.5f);
            goldLine.anchorMax = new Vector2(0f, 0.5f);
            goldLine.pivot = new Vector2(0.5f, 0f);
            goldLine.anchoredPosition = new Vector2(0f, -effectHeight * 0.5f);
            goldLine.sizeDelta = new Vector2(HoverLineWidth, effectHeight);

            StartMenuWipeGraphic goldLineImage =
                goldLine.gameObject.AddComponent<StartMenuWipeGraphic>();
            goldLineImage.color = StartMenuOptionView.GoldLine;
            goldLineImage.raycastTarget = false;
            goldLineImage.SetWipe(0f, 0f);

            CanvasGroup goldLineGroup = goldLine.gameObject.AddComponent<CanvasGroup>();
            goldLineGroup.alpha = 0f;

            Text labelText = CreateText(
                item, "Label", entry.Label,
                new Vector2(ItemTextLeft, 0f), new Vector2(400f, ItemHeight),
                MenuTextSize, FontStyle.Normal, TextAnchor.MiddleLeft);
            labelText.color = Color.white;
            StartMenuTextEffect effect = AddTextEffect(
                labelText, Color.white, Color.white, 3f);

            float textWidth = Mathf.Max(1f, labelText.preferredWidth);
            float backgroundWidth =
                textWidth + ItemTextLeft +
                textWidth * HoverRightBlankRatio + HoverRightBlankExtra;
            hoverBackground.sizeDelta = new Vector2(backgroundWidth, effectHeight);

            float itemWidth = Mathf.Max(600f, backgroundWidth + 60f);
            item.sizeDelta = new Vector2(itemWidth, ItemHeight);
            labelText.rectTransform.sizeDelta =
                new Vector2(itemWidth - ItemTextLeft - 16f, ItemHeight);

            StartMenuOptionView view =
                item.gameObject.AddComponent<StartMenuOptionView>();
            view.Configure(
                entry.Label,
                index,
                labelText,
                effect,
                hoverBackground,
                hoverBackgroundGroup,
                goldLine,
                goldLineGroup,
                optionGroup);

            RectTransform hitArea = CreateRectTransform("HitArea", item);
            hitArea.anchorMin = new Vector2(0f, 0.5f);
            hitArea.anchorMax = new Vector2(0f, 0.5f);
            hitArea.pivot = new Vector2(0f, 0.5f);
            hitArea.anchoredPosition = new Vector2(12f, 0f);
            hitArea.sizeDelta = new Vector2(backgroundWidth, effectHeight);

            TrapezoidGraphic hitGraphic =
                hitArea.gameObject.AddComponent<TrapezoidGraphic>();
            if (hitArea.GetComponent<CanvasRenderer>() == null)
            {
                hitArea.gameObject.AddComponent<CanvasRenderer>();
            }

            hitGraphic.color = Color.clear;
            hitGraphic.RightSlant = HoverSlant;
            hitGraphic.raycastTarget = true;
            hitGraphic.SetHorizontalWipe(0f, 1f);

            StartMenuOptionHitArea hit =
                hitArea.gameObject.AddComponent<StartMenuOptionHitArea>();
            hit.Configure(view, button);
        }

        private StartMenuTextEffect AddTextEffect(
            Text text,
            Color bottom,
            Color top,
            float outlineWidth)
        {
            StartMenuTextEffect effect =
                text.gameObject.AddComponent<StartMenuTextEffect>();
            float shadowX = outlineWidth + 2.5f;
            float shadowY = outlineWidth + 3f;
            float shadowSoftness = outlineWidth >= 4f ? 3.6f : 2.9f;
            effect.Configure(
                bottom,
                top,
                StartMenuOptionView.Outline,
                outlineWidth,
                StartMenuOptionView.Shadow,
                new Vector2(shadowX, -shadowY),
                shadowSoftness);
            return effect;
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
                () => GameSession.ChooseMode(GameMode.Normal));

            BuildModeOption(
                panel,
                "无CPU测试模式",
                "四个座位都由你一个人操作，用来验证规则、复现问题、排查 AI。",
                "测试",
                268f,
                () => GameSession.ChooseMode(GameMode.NoCpuTest));

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

        /// <summary>自定义战斗模拟：只开战斗页面，自己组两套战斗栏对打（不碰棋盘层）。</summary>
        public void OpenCombatLab()
        {
            if (combatLab == null)
            {
                combatLab = CombatLabView.Create(canvasRect, GetMenuFont());
            }

            combatLab.Show();
        }

        public void OpenCodex()
        {
            if (cardCodex == null)
            {
                cardCodex = CardCodexView.Create(canvasRect, GetMenuFont());
            }

            cardCodex.Show();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void PlayHoverSound()
        {
            if (hoverAudioSource != null && hoverClip != null)
            {
                hoverAudioSource.PlayOneShot(hoverClip, 0.65f);
            }
        }

        private StartMenuBgm EnsureStartMenuBgm()
        {
            Transform existing = transform.Find("StartMenuBgm");
            GameObject bgmObject = existing != null ? existing.gameObject : null;
            if (bgmObject == null)
            {
                bgmObject = new GameObject(
                    "StartMenuBgm",
                    typeof(AudioSource));
                bgmObject.transform.SetParent(transform, false);
            }

            StartMenuBgm bgm = bgmObject.GetComponent<StartMenuBgm>();
            if (bgm == null)
            {
                bgm = bgmObject.AddComponent<StartMenuBgm>();
            }

            bgm.EnsureConfigured();
            return bgm;
        }

        private void EnsureHoverAudio()
        {
            if (hoverAudioSource == null)
            {
                hoverAudioSource = GetComponent<AudioSource>();
            }

            if (hoverAudioSource == null)
            {
                hoverAudioSource = gameObject.AddComponent<AudioSource>();
            }

            hoverAudioSource.playOnAwake = false;
            hoverAudioSource.loop = false;
            hoverAudioSource.spatialBlend = 0f;
            hoverAudioSource.volume = 1f;

            if (hoverClip == null)
            {
                hoverClip = Resources.Load<AudioClip>("SFX/paper_hover");
            }
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

        private static Font GetTitleFont()
        {
            if (titleFont != null)
            {
                return titleFont;
            }

            titleFont = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    TitleFontName,
                    "Times",
                    "Liberation Serif",
                    "DejaVu Serif"
                },
                TitleTextSize);

            if (titleFont == null)
            {
                titleFont = GetMenuFont();
            }

            return titleFont;
        }

        private void ApplyMenuFont()
        {
            Font font = GetMenuFont();
            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                text.font = font;
            }
        }

        private void ApplyTitleFont()
        {
            if (canvasRect == null)
            {
                return;
            }

            Transform titleTransform = canvasRect.Find("Title");
            Text title = titleTransform != null
                ? titleTransform.GetComponent<Text>()
                : null;
            if (title != null)
            {
                title.text = ProjectTitle;
                title.font = GetTitleFont();
            }
        }

        private sealed class MenuEntry
        {
            public MenuEntry(string label, System.Action action)
            {
                Label = label;
                Action = action;
            }

            public string Label { get; }
            public System.Action Action { get; }
        }

#if UNITY_EDITOR
        public RectTransform EditorCanvasRect => canvasRect;
        public Font EditorFont => GetMenuFont();
        public string EditorTitleText => ProjectTitle;
        public string EditorTitleFontName => TitleFontName;
        public string[] EditorMenuLabels
        {
            get
            {
                MenuEntry[] entries = CreateMenuEntries();
                string[] labels = new string[entries.Length];
                for (int index = 0; index < entries.Length; index++)
                {
                    labels[index] = entries[index].Label;
                }

                return labels;
            }
        }

        public GameSettingsPanel EditorSettingsPanel
        {
            get => settingsPanel;
            set => settingsPanel = value;
        }

        public CombatLabView EditorCombatLab
        {
            get => combatLab;
            set => combatLab = value;
        }

        public CardCodexView EditorCardCodex
        {
            get => cardCodex;
            set => cardCodex = value;
        }

        public void BuildMenuForEditor()
        {
            Transform existingCanvas = transform.Find("LeftMenuCanvas");
            if (existingCanvas != null)
            {
                DestroyImmediate(existingCanvas.gameObject);
            }

            settingsPanel = null;
            combatLab = null;
            cardCodex = null;
            EnsureStartMenuBgm();
            BuildMenu();
            BindMenu();
            ApplyMenuFont();
            ApplyTitleFont();
        }
#endif
    }
}
