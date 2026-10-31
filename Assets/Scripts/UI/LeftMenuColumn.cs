using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// Builds a responsive, left-aligned vertical menu over the current scene.
    /// The column is offset below the top-left corner so artwork or branding can occupy that space.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class LeftMenuColumn : MonoBehaviour
    {
        private static readonly string[] DefaultLabels =
        {
            "开始游戏",
            "多人游戏",
            "设置",
            "退出游戏"
        };

        [Header("布局")]
        [SerializeField, Min(0f)] private float leftInset = 48f;
        [SerializeField] private float verticalOffset = -24f;
        [SerializeField, Min(220f)] private float columnWidth = 340f;
        [SerializeField, Min(56f)] private float buttonHeight = 80f;
        [SerializeField, Min(0f)] private float buttonSpacing = 16f;
        [SerializeField] private string[] buttonLabels = DefaultLabels;

        private static Font menuFont;

        private void Awake()
        {
            if (transform.Find("LeftMenuCanvas") == null)
            {
                BuildMenu();
            }

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
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            float contentHeight =
                buttonHeight * DefaultLabels.Length +
                buttonSpacing * (DefaultLabels.Length - 1) +
                48f;

            RectTransform column = CreateRectTransform("MenuColumn", canvasObject.transform);
            column.anchorMin = new Vector2(0f, 0.5f);
            column.anchorMax = new Vector2(0f, 0.5f);
            column.pivot = new Vector2(0f, 0.5f);
            column.anchoredPosition = new Vector2(leftInset, verticalOffset);
            column.sizeDelta = new Vector2(columnWidth, contentHeight);

            GameObject panelObject = new GameObject(
                "Panel",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(Outline));

            panelObject.transform.SetParent(column, false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.035f, 0.045f, 0.06f, 0.88f);
            panelImage.raycastTarget = false;

            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(0.72f, 0.58f, 0.32f, 0.34f);
            panelOutline.effectDistance = new Vector2(1f, -1f);

            VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = buttonSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform accent = CreateRectTransform("AccentLine", column);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0.5f, 0.5f);
            accent.anchoredPosition = new Vector2(2f, 0f);
            accent.sizeDelta = new Vector2(4f, -32f);

            Image accentImage = accent.gameObject.AddComponent<Image>();
            accentImage.color = new Color(0.82f, 0.67f, 0.39f, 0.95f);
            accentImage.raycastTarget = false;

            for (int index = 0; index < DefaultLabels.Length; index++)
            {
                CreateButton(panelObject.transform, index, ResolveLabel(index));
            }
        }

        private void CreateButton(Transform parent, int index, string label)
        {
            GameObject buttonObject = new GameObject(
                $"MenuButton{index + 1:00}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));

            buttonObject.transform.SetParent(parent, false);

            LayoutElement element = buttonObject.GetComponent<LayoutElement>();
            element.preferredHeight = buttonHeight;
            element.flexibleWidth = 1f;

            Image background = buttonObject.GetComponent<Image>();
            background.color = Color.white;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.10f, 0.12f, 0.15f, 0.94f);
            colors.highlightedColor = new Color(0.18f, 0.22f, 0.25f, 1f);
            colors.pressedColor = new Color(0.73f, 0.56f, 0.28f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.12f, 0.13f, 0.15f, 0.75f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            int capturedIndex = index;
            button.onClick.AddListener(() => HandleSelection(capturedIndex, label));

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(Text));

            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 0f);
            labelRect.offsetMax = new Vector2(-18f, 0f);

            Text text = labelObject.GetComponent<Text>();
            text.font = GetMenuFont();
            text.fontSize = 28;
            text.fontStyle = FontStyle.Normal;
            text.text = $"{index + 1:00}    {label}";
            text.color = new Color(0.94f, 0.92f, 0.86f, 1f);
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        public void StartGame()
        {
            SceneManager.LoadScene("ready room");
        }

        public void OpenMultiplayer()
        {
            Debug.Log("[StartMenu] 多人游戏暂未实现。");
        }

        public void OpenSettings()
        {
            Debug.Log("[StartMenu] 设置暂未实现。");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleSelection(int index, string label)
        {
            switch (index)
            {
                case 0:
                    StartGame();
                    break;
                case 1:
                    OpenMultiplayer();
                    break;
                case 2:
                    OpenSettings();
                    break;
                case 3:
                    QuitGame();
                    break;
            }
        }

        private string ResolveLabel(int index)
        {
            if (buttonLabels != null &&
                index < buttonLabels.Length &&
                !string.IsNullOrWhiteSpace(buttonLabels[index]))
            {
                return buttonLabels[index];
            }

            return DefaultLabels[index];
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

            BuildMenu();
            ApplyMenuFont();
        }
#endif
    }
}
