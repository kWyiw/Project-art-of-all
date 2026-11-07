using System.Collections;
using ProjectArtOfAll.Board;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectArtOfAll.Flow
{
    /// <summary>
    /// MainGame is the visual waypoint between the lobby and the two baked map
    /// dimensions. It displays the selected dimension briefly, then replaces itself
    /// with Map1 or Map2.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapTransitController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float minimumTransitSeconds = 0.65f;

        [Header("Scene UI")]
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private Image glow;
        [SerializeField] private Image upperRule;
        [SerializeField] private Text mapTitle;
        [SerializeField] private Text mapSubtitle;
        [SerializeField] private Image progressFill;

        private void Start()
        {
            if (!HasSceneUi())
            {
                Debug.LogError(
                    "[MapTransit] Scene UI is missing. Rebuild the MainGame transit scene.");
                enabled = false;
                return;
            }

            BoardMapInfo map = MapCatalog.Selected;
            ApplyMapStyle(map);
            overlay.alpha = 0f;
            StartCoroutine(EnterMap(map));
        }

        private bool HasSceneUi()
        {
            return overlay != null &&
                   glow != null &&
                   upperRule != null &&
                   mapTitle != null &&
                   mapSubtitle != null &&
                   progressFill != null;
        }

        private void ApplyMapStyle(BoardMapInfo map)
        {
            Color accent = map != null && map.Kind == BoardMapKind.SweetRing
                ? new Color(0.72f, 0.47f, 0.96f)
                : new Color(0.96f, 0.73f, 0.34f);
            Color glowColor = glow.color;
            glowColor.r = accent.r;
            glowColor.g = accent.g;
            glowColor.b = accent.b;
            glow.color = glowColor;

            Color ruleColor = upperRule.color;
            ruleColor.r = accent.r;
            ruleColor.g = accent.g;
            ruleColor.b = accent.b;
            upperRule.color = ruleColor;

            Color fillColor = progressFill.color;
            fillColor.r = accent.r;
            fillColor.g = accent.g;
            fillColor.b = accent.b;
            progressFill.color = fillColor;

            mapSubtitle.color = accent;
            mapTitle.text = map == null ? "未知维度" : map.DisplayName;
            mapSubtitle.text = map == null ? string.Empty : map.Subtitle;
        }

        private IEnumerator EnterMap(BoardMapInfo map)
        {
            string sceneName = map == null ? string.Empty : map.SceneName;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(string.Concat(
                    "[MapTransit] Scene '",
                    sceneName,
                    "' is missing from Build Settings."));
                yield break;
            }

            float elapsed = 0f;
            const float FadeSeconds = 0.2f;
            while (elapsed < FadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                overlay.alpha = Mathf.Clamp01(elapsed / FadeSeconds);
                yield return null;
            }

            overlay.alpha = 1f;
            yield return new WaitForSecondsRealtime(minimumTransitSeconds);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError(string.Concat("[MapTransit] Could not start loading ", sceneName, "."));
                yield break;
            }

            while (!operation.isDone)
            {
                float normalized = Mathf.Clamp01(operation.progress / 0.9f);
                progressFill.rectTransform.anchorMax = new Vector2(
                    Mathf.Lerp(0.12f, 1f, normalized),
                    1f);
                yield return null;
            }
        }
    }
}
