using ProjectArtOfAll.Board;
using ProjectArtOfAll.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 通用设置面板：主菜单与游戏内暂停菜单共用同一份（同名、同布局、同 <see cref="GameSettings"/>）。
    /// 所有设置只影响表现——卡槽大小、路径线显隐、镜头跟随、自动降档——不改任何规则数值。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSettingsPanel : MonoBehaviour
    {
        private const float PanelWidth = 560f;
        private const float PanelHeight = 430f;

        private static readonly Color ScrimColor = new Color(0.012f, 0.02f, 0.024f, 0.72f);
        private static readonly Color PanelColor = new Color(0.048f, 0.078f, 0.09f, 0.99f);
        private static readonly Color Brass = new Color(0.92f, 0.69f, 0.27f, 1f);
        private static readonly Color TextColor = new Color(0.94f, 0.93f, 0.88f, 1f);
        private static readonly Color MutedText = new Color(0.64f, 0.66f, 0.63f, 1f);

        private Font font;
        private Slider cardScaleSlider;
        private Text cardScaleValue;
        private Button pathButton;
        private Text pathLabel;
        private Button followButton;
        private Text followLabel;
        private Button autoButton;
        private Text autoLabel;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>在给定画布下建一个隐藏的设置面板；<paramref name="onClose"/> 在点「返回」时回调。</summary>
        public static GameSettingsPanel Create(
            Transform parent,
            Font font,
            System.Action onClose = null)
        {
            GameObject rootObject = new GameObject(
                "GameSettingsPanel",
                typeof(RectTransform),
                typeof(Image));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image scrim = rootObject.GetComponent<Image>();
            scrim.color = ScrimColor;
            scrim.raycastTarget = true;

            GameSettingsPanel panel = rootObject.AddComponent<GameSettingsPanel>();
            panel.font = font;
            panel.Build(onClose);
            rootObject.SetActive(false);
            return panel;
        }

        private void Build(System.Action onClose)
        {
            GameObject panelObject = BoardHudOverlayFactory.CreateBar(
                transform, "Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-PanelWidth * 0.5f, -PanelHeight * 0.5f),
                new Vector2(PanelWidth * 0.5f, PanelHeight * 0.5f),
                PanelColor);

            Label(panelObject.transform, "Title", "设置", 24f, 22f, PanelWidth - 48f, 34f,
                26, FontStyle.Bold, TextAnchor.MiddleCenter).color = Brass;

            Label(panelObject.transform, "Hint", "只影响表现，不改任何规则数值",
                    24f, 58f, PanelWidth - 48f, 22f,
                    13, FontStyle.Normal, TextAnchor.MiddleCenter).color = MutedText;

            // 卡槽大小 --------------------------------------------------------
            Label(panelObject.transform, "CardScaleLabel", "卡槽大小", 24f, 104f, 110f, 26f,
                14, FontStyle.Normal, TextAnchor.MiddleLeft).color = TextColor;

            cardScaleSlider = MakeSlider(
                panelObject.transform,
                new Vector2(140f, -105f),
                new Vector2(240f, 24f),
                GameSettings.MinimumCardScale,
                GameSettings.MaximumCardScale,
                GameSettings.CardScale);

            cardScaleValue = Label(panelObject.transform, "CardScaleValue", "100%",
                396f, 104f, 70f, 26f, 14, FontStyle.Normal, TextAnchor.MiddleRight);
            cardScaleValue.color = Brass;

            cardScaleSlider.onValueChanged.AddListener(value =>
            {
                cardScaleValue.text = Mathf.RoundToInt(value * 100f) + "%";
                GameSettings.CardScale = value;
            });

            // 四个开关 --------------------------------------------------------
            pathButton = MakeToggle(panelObject.transform, "地图路径线", 150f, () =>
                GameSettings.PathLinesVisible = !GameSettings.PathLinesVisible);
            pathLabel = pathButton.GetComponentInChildren<Text>();

            followButton = MakeToggle(panelObject.transform, "镜头跟随", 194f, () =>
                GameSettings.CameraFollow = !GameSettings.CameraFollow);
            followLabel = followButton.GetComponentInChildren<Text>();

            autoButton = MakeToggle(panelObject.transform, "掉帧自动降档", 238f, () =>
                GameSettings.AutoQuality = !GameSettings.AutoQuality);
            autoLabel = autoButton.GetComponentInChildren<Text>();

            BoardHudOverlayFactory.CreateBar(
                panelObject.transform, "Divider",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -286f), new Vector2(PanelWidth - 24f, -285f),
                new Color(0.86f, 0.62f, 0.20f, 0.55f));

            MakeButton(panelObject.transform, "恢复默认", 24f, 306f, 200f, 44f, () =>
            {
                GameSettings.ResetToDefaults();
                RefreshControls();
            });

            MakeButton(panelObject.transform, "返回", PanelWidth - 24f - 200f, 306f, 200f, 44f,
                () =>
                {
                    Hide();
                    onClose?.Invoke();
                });

            Label(panelObject.transform, "Footer",
                    "设置会保存到 PlayerPrefs，下次启动沿用",
                    24f, 366f, PanelWidth - 48f, 22f,
                    12, FontStyle.Normal, TextAnchor.MiddleCenter).color = MutedText;

            RefreshControls();
            GameSettings.Changed += OnSettingsChanged;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= OnSettingsChanged;
        }

        private void OnSettingsChanged(string _)
        {
            RefreshControls();
        }

        /// <summary>把控件状态同步回当前设置值（恢复默认、外部改动后调用）。</summary>
        public void RefreshControls()
        {
            GameSettings.EnsureLoaded();

            if (cardScaleSlider != null)
            {
                cardScaleSlider.SetValueWithoutNotify(GameSettings.CardScale);
            }

            if (cardScaleValue != null)
            {
                cardScaleValue.text = Mathf.RoundToInt(GameSettings.CardScale * 100f) + "%";
            }

            if (pathLabel != null)
            {
                pathLabel.text = GameSettings.PathLinesVisible ? "显示" : "隐藏";
            }

            if (followLabel != null)
            {
                followLabel.text = GameSettings.CameraFollow ? "开" : "关";
            }

            if (autoLabel != null)
            {
                autoLabel.text = GameSettings.AutoQuality ? "开" : "关";
            }
        }

        public void Show()
        {
            RefreshControls();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        private Text Label(
            Transform parent,
            string name,
            string content,
            float x,
            float y,
            float width,
            float height,
            int fontSize,
            FontStyle style,
            TextAnchor alignment)
        {
            return BoardHudOverlayFactory.CreateText(
                parent, name, content, font, fontSize, style,
                new Vector2(x, -y), new Vector2(width, height), alignment);
        }

        /// <summary>一行「标签 + 开关按钮」，返回按钮本身。</summary>
        private Button MakeToggle(Transform parent, string label, float y, System.Action onClick)
        {
            Label(parent, label + "Label", label, 24f, y, 200f, 26f,
                14, FontStyle.Normal, TextAnchor.MiddleLeft).color = TextColor;

            return MakeButton(parent, "开", PanelWidth - 24f - 120f, y - 2f, 120f, 30f, onClick);
        }

        private Button MakeButton(
            Transform parent,
            string label,
            float x,
            float y,
            float width,
            float height,
            System.Action onClick)
        {
            GameObject buttonObject = BoardHudOverlayFactory.CreateBar(
                parent, "Button_" + label,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -(y + height)), new Vector2(x + width, -y),
                new Color(0.89f, 0.64f, 0.20f, 1f));
            Image image = buttonObject.GetComponent<Image>();
            image.raycastTarget = true;

            Button button = buttonObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick?.Invoke());

            Text text = BoardHudOverlayFactory.CreateText(
                buttonObject.transform, "Label", label, font, 15, FontStyle.Bold,
                Vector2.zero, new Vector2(width, height), TextAnchor.MiddleCenter);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            text.color = new Color(0.09f, 0.08f, 0.05f, 1f);
            return button;
        }

        private static Slider MakeSlider(
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            float minimum,
            float maximum,
            float value)
        {
            GameObject sliderObject = new GameObject(
                "Slider",
                typeof(RectTransform),
                typeof(Slider));
            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            GameObject background = BoardHudOverlayFactory.CreateBar(
                sliderObject.transform, "Background",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -5f), new Vector2(0f, 5f),
                new Color(0.16f, 0.20f, 0.22f, 1f));
            background.GetComponent<Image>().raycastTarget = false;

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.SetParent(sliderObject.transform, false);
            fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRect.offsetMin = new Vector2(0f, -5f);
            fillAreaRect.offsetMax = new Vector2(0f, 5f);

            GameObject fill = BoardHudOverlayFactory.CreateBar(
                fillArea.transform, "Fill",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.89f, 0.64f, 0.20f, 0.95f));
            fill.GetComponent<Image>().raycastTarget = false;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.SetParent(sliderObject.transform, false);
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            GameObject handle = BoardHudOverlayFactory.CreateBar(
                handleArea.transform, "Handle",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(-9f, -12f), new Vector2(9f, 12f),
                new Color(0.98f, 0.86f, 0.52f, 1f));
            Image handleImage = handle.GetComponent<Image>();
            handleImage.raycastTarget = true;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.value = value;
            return slider;
        }
    }
}
