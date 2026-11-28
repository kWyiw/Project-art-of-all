using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 跨场景黑幕：从右侧滑入，完全覆盖后定格一秒，再加载目标场景并从左侧滑出。
    /// 自己挂在 DontDestroyOnLoad 的独立画布上，因此切场景时动画不会中断。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneTransitionOverlay : MonoBehaviour
    {
        public const float SlideDuration = 0.44f;
        public const float HoldDuration = 1f;

        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private static SceneTransitionOverlay active;

        private RectTransform mask;

        public static bool Play(string sceneName, Action beforeLoad)
        {
            if (active != null || string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            GameObject root = new GameObject(
                "SceneTransitionOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            SceneTransitionOverlay overlay =
                root.AddComponent<SceneTransitionOverlay>();
            active = overlay;
            overlay.EnsureMask();
            overlay.StartCoroutine(overlay.Run(sceneName, beforeLoad));
            return true;
        }

        private void Awake()
        {
            EnsureMask();
        }

        private void OnDestroy()
        {
            if (active == this)
            {
                active = null;
            }
        }

        private void EnsureMask()
        {
            if (mask != null)
            {
                return;
            }

            GameObject maskObject = new GameObject(
                "Mask",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            mask = maskObject.GetComponent<RectTransform>();
            mask.SetParent(transform, false);
            mask.anchorMin = new Vector2(0.5f, 0.5f);
            mask.anchorMax = new Vector2(0.5f, 0.5f);
            mask.pivot = new Vector2(0.5f, 0.5f);
            mask.anchoredPosition = new Vector2(ReferenceWidth, 0f);
            mask.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

            Image image = maskObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;
        }

        private IEnumerator Run(string sceneName, Action beforeLoad)
        {
            beforeLoad?.Invoke();
            AsyncOperation loading = SceneManager.LoadSceneAsync(
                sceneName,
                LoadSceneMode.Single);
            if (loading != null)
            {
                loading.allowSceneActivation = false;
            }

            yield return Slide(ReferenceWidth, 0f);
            yield return new WaitForSecondsRealtime(HoldDuration);

            if (loading != null)
            {
                while (loading.progress < 0.9f)
                {
                    yield return null;
                }

                loading.allowSceneActivation = true;
                while (!loading.isDone)
                {
                    yield return null;
                }
            }

            yield return null;
            yield return Slide(0f, -ReferenceWidth);

            active = null;
            Destroy(gameObject);
        }

        private IEnumerator Slide(float fromX, float toX)
        {
            float elapsed = 0f;
            while (elapsed < SlideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SlideDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                mask.anchoredPosition = new Vector2(
                    Mathf.Lerp(fromX, toX, eased),
                    0f);
                yield return null;
            }

            mask.anchoredPosition = new Vector2(toX, 0f);
        }
    }
}
