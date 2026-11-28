using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 主菜单单个选项：入场、悬停文字渐变、金线、梯形灰底和悬停音效。
    /// 菜单列只需按数据列表创建它，后续新增选项会自动复用全部行为。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class StartMenuOptionView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        public static readonly Color HoverBottom = new Color(0.769f, 0.537f, 0.184f, 1f);
        public static readonly Color HoverTop = new Color(0.957f, 0.894f, 0.722f, 1f);
        public static readonly Color GoldLine = new Color(0.91f, 0.776f, 0.294f, 1f);
        public static readonly Color HoverGray = new Color(0.424f, 0.424f, 0.424f, 0.55f);
        public static readonly Color Outline = Color.black;
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.46f);

        private const float HoverInDuration = 0.14f;
        private const float HoverOutDuration = 0.12f;
        private const float IntroDuration = 0.24f;
        private const float IntroStagger = 0.16f;
        private const float IntroRise = 18f;

        [SerializeField] private Text label;
        [SerializeField] private StartMenuTextEffect textEffect;
        [SerializeField] private RectTransform hoverBackground;
        [SerializeField] private CanvasGroup hoverBackgroundGroup;
        [SerializeField] private RectTransform goldLine;
        [SerializeField] private CanvasGroup goldLineGroup;
        [SerializeField] private CanvasGroup optionGroup;
        [SerializeField] private int introIndex;
        [SerializeField] private string labelText = string.Empty;
        [SerializeField] private float hoverBaseX = 12f;

        private RectTransform optionRect;
        private Vector2 finalPosition;
        private float effectHeight;
        private bool hovered;
        private bool introPlayed;
        private TrapezoidGraphic hoverTrapezoid;
        private StartMenuWipeGraphic goldWipe;
        private Coroutine lineRoutine;
        private Coroutine backgroundRoutine;
        private Coroutine textRoutine;
        private LeftMenuColumn menu;

        public string Label => labelText;
        public int IntroIndex => introIndex;

        public void Configure(
            string text,
            int index,
            Text labelComponent,
            StartMenuTextEffect effect,
            RectTransform background,
            CanvasGroup backgroundGroup,
            RectTransform line,
            CanvasGroup lineGroup,
            CanvasGroup rootGroup)
        {
            labelText = text;
            introIndex = index;
            label = labelComponent;
            textEffect = effect;
            hoverBackground = background;
            hoverBackgroundGroup = backgroundGroup;
            goldLine = line;
            goldLineGroup = lineGroup;
            optionGroup = rootGroup;
            hoverBaseX = background != null ? background.anchoredPosition.x : 12f;
            ResolveReferences();
            finalPosition = optionRect.anchoredPosition;
            SetRestState();
        }

        private void Awake()
        {
            ResolveReferences();
            finalPosition = optionRect.anchoredPosition;
            SetRestState();
            menu = GetComponentInParent<LeftMenuColumn>();
        }

        public void PlayIntro(int index)
        {
            if (introPlayed)
            {
                return;
            }

            introPlayed = true;
            introIndex = index;
            finalPosition = optionRect.anchoredPosition;
            StartCoroutine(IntroRoutine());
        }

        public void PlayIntroImmediate()
        {
            introPlayed = true;
            StopIntro();
            optionRect.anchoredPosition = finalPosition;
            optionGroup.alpha = 1f;
            optionGroup.blocksRaycasts = true;
            optionGroup.interactable = true;
        }

        public void SetHoverInstant(bool value)
        {
            StopHoverRoutines();
            hovered = value;
            if (value)
            {
                SetHoverProgress(1f);
            }
            else
            {
                SetRestState();
            }
        }

        public void SetHoverProgress(float value)
        {
            StopHoverRoutines();
            hovered = value > 0.001f;
            float t = Mathf.Clamp01(value);

            if (textEffect != null)
            {
                textEffect.SetFillColors(
                    Color.Lerp(Color.white, HoverBottom, t),
                    Color.Lerp(Color.white, HoverTop, t));
            }

            if (goldLineGroup != null)
            {
                goldLineGroup.alpha = 1f;
            }

            if (goldLine != null)
            {
                goldLine.localScale = Vector3.one;
                goldLine.anchoredPosition = new Vector2(0f, -effectHeight * 0.5f);
            }

            if (goldWipe != null)
            {
                goldWipe.SetWipe(0f, t);
            }

            if (hoverBackgroundGroup != null)
            {
                hoverBackgroundGroup.alpha = 1f;
            }

            if (hoverBackground != null)
            {
                hoverBackground.anchoredPosition = new Vector2(hoverBaseX, 0f);
            }

            if (hoverTrapezoid != null)
            {
                hoverTrapezoid.SetHorizontalWipe(0f, t);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hovered)
            {
                return;
            }

            hovered = true;
            menu?.PlayHoverSound();
            StartHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!hovered)
            {
                return;
            }

            hovered = false;
            StartHover(false);
        }

        private void OnDisable()
        {
            StopHoverRoutines();
            hovered = false;
            if (optionGroup != null)
            {
                optionGroup.alpha = 1f;
                optionGroup.blocksRaycasts = true;
                optionGroup.interactable = true;
            }
        }

        private void ResolveReferences()
        {
            optionRect = transform as RectTransform;
            if (label == null)
            {
                Transform child = transform.Find("Label");
                label = child != null ? child.GetComponent<Text>() : null;
            }

            if (textEffect == null && label != null)
            {
                textEffect = label.GetComponent<StartMenuTextEffect>();
            }

            if (hoverBackground == null)
            {
                hoverBackground = transform.Find("HoverBackground") as RectTransform;
            }

            if (hoverBackgroundGroup == null && hoverBackground != null)
            {
                hoverBackgroundGroup = hoverBackground.GetComponent<CanvasGroup>();
            }

            if (hoverTrapezoid == null && hoverBackground != null)
            {
                hoverTrapezoid = hoverBackground.GetComponent<TrapezoidGraphic>();
            }

            if (goldLine == null)
            {
                goldLine = transform.Find("GoldLine") as RectTransform;
            }

            if (goldLineGroup == null && goldLine != null)
            {
                goldLineGroup = goldLine.GetComponent<CanvasGroup>();
            }

            if (goldWipe == null && goldLine != null)
            {
                goldWipe = goldLine.GetComponent<StartMenuWipeGraphic>();
            }

            if (optionGroup == null)
            {
                optionGroup = GetComponent<CanvasGroup>();
            }

            if (goldLine != null)
            {
                effectHeight = goldLine.sizeDelta.y;
            }
            else if (hoverBackground != null)
            {
                effectHeight = hoverBackground.sizeDelta.y;
            }
        }

        private void SetRestState()
        {
            if (textEffect != null)
            {
                textEffect.SetFillColors(Color.white, Color.white);
            }

            if (goldLineGroup != null)
            {
                goldLineGroup.alpha = 1f;
            }

            if (goldLine != null)
            {
                goldLine.localScale = Vector3.one;
                goldLine.anchoredPosition = new Vector2(0f, -effectHeight * 0.5f);
            }

            if (goldWipe != null)
            {
                goldWipe.SetWipe(0f, 0f);
            }

            if (hoverBackgroundGroup != null)
            {
                hoverBackgroundGroup.alpha = 1f;
            }

            if (hoverBackground != null)
            {
                hoverBackground.anchoredPosition = new Vector2(hoverBaseX, 0f);
            }

            if (hoverTrapezoid != null)
            {
                hoverTrapezoid.SetHorizontalWipe(0f, 0f);
            }
        }

        private void StartHover(bool entering)
        {
            StopHoverRoutines();
            if (entering && hoverBackgroundGroup != null)
            {
                hoverBackgroundGroup.alpha = 1f;
            }

            if (entering && goldLineGroup != null)
            {
                goldLineGroup.alpha = 1f;
            }

            textRoutine = StartCoroutine(TextRoutine(entering));
            lineRoutine = StartCoroutine(LineRoutine(entering));
            backgroundRoutine = StartCoroutine(BackgroundRoutine(entering));
        }

        private void StopHoverRoutines()
        {
            if (textRoutine != null)
            {
                StopCoroutine(textRoutine);
                textRoutine = null;
            }

            if (lineRoutine != null)
            {
                StopCoroutine(lineRoutine);
                lineRoutine = null;
            }

            if (backgroundRoutine != null)
            {
                StopCoroutine(backgroundRoutine);
                backgroundRoutine = null;
            }
        }

        private void StopIntro()
        {
            StopAllCoroutines();
            lineRoutine = null;
            backgroundRoutine = null;
            textRoutine = null;
        }

        private IEnumerator IntroRoutine()
        {
            optionGroup.alpha = 0f;
            optionGroup.blocksRaycasts = false;
            optionGroup.interactable = false;
            optionRect.anchoredPosition = finalPosition + Vector2.up * IntroRise;

            float delay = introIndex * IntroStagger;
            float elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < IntroDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / IntroDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                optionRect.anchoredPosition =
                    Vector2.Lerp(finalPosition + Vector2.up * IntroRise, finalPosition, eased);
                optionGroup.alpha = eased;
                yield return null;
            }

            optionRect.anchoredPosition = finalPosition;
            optionGroup.alpha = 1f;
            optionGroup.blocksRaycasts = true;
            optionGroup.interactable = true;
        }

        private IEnumerator TextRoutine(bool entering)
        {
            Color startBottom = entering ? Color.white : HoverBottom;
            Color startTop = entering ? Color.white : HoverTop;
            Color endBottom = entering ? HoverBottom : Color.white;
            Color endTop = entering ? HoverTop : Color.white;
            float duration = entering ? HoverInDuration : HoverOutDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                textEffect.SetFillColors(
                    Color.Lerp(startBottom, endBottom, eased),
                    Color.Lerp(startTop, endTop, eased));
                yield return null;
            }

            textEffect.SetFillColors(endBottom, endTop);
            textRoutine = null;
        }

        private IEnumerator LineRoutine(bool entering)
        {
            if (goldWipe == null)
            {
                lineRoutine = null;
                yield break;
            }

            float duration = entering ? HoverInDuration : HoverOutDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                if (entering)
                {
                    goldWipe.SetWipe(0f, eased);
                }
                else
                {
                    goldWipe.SetWipe(eased, 1f);
                }

                yield return null;
            }

            if (entering)
            {
                goldWipe.SetWipe(0f, 1f);
            }
            else
            {
                goldWipe.SetWipe(0f, 0f);
            }

            lineRoutine = null;
        }

        private IEnumerator BackgroundRoutine(bool entering)
        {
            if (hoverTrapezoid == null)
            {
                backgroundRoutine = null;
                yield break;
            }

            float duration = entering ? HoverInDuration : HoverOutDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                if (entering)
                {
                    hoverTrapezoid.SetHorizontalWipe(0f, eased);
                }
                else
                {
                    hoverTrapezoid.SetHorizontalWipe(eased, 1f);
                }

                yield return null;
            }

            if (entering)
            {
                hoverTrapezoid.SetHorizontalWipe(0f, 1f);
            }
            else
            {
                hoverTrapezoid.SetHorizontalWipe(0f, 0f);
            }

            backgroundRoutine = null;
        }
    }
}
