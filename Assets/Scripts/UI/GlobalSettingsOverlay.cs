using ProjectArtOfAll.Board;
using ProjectArtOfAll.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 全局 Esc 设置入口。除 StartMenu、MainGame 和跨场景加载黑幕外，
    /// 任意页面都打开同一份 <see cref="GameSettingsPanel"/>，暂不另做页面专属样式。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GlobalSettingsOverlay : MonoBehaviour
    {
        private const int SortingOrder = 30000;
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private static GlobalSettingsOverlay instance;

        private GameSettingsPanel settingsPanel;
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;
        private bool cursorCaptured;

        public static bool IsOpen =>
            instance != null &&
            instance.settingsPanel != null &&
            instance.settingsPanel.IsOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject root = new GameObject(
                "GlobalSettingsOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            instance = root.AddComponent<GlobalSettingsOverlay>();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            instance?.HideSettings();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!CanHandleEscape())
            {
                HideSettings();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                KeepCursorAvailable();
                return;
            }

            if (IsOpen)
            {
                HideSettings();
            }
            else
            {
                ShowSettings();
            }
        }

        private static bool CanHandleEscape()
        {
            if (SceneTransitionOverlay.IsActive || !Application.isPlaying)
            {
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() &&
                   scene.isLoaded &&
                   scene.name != GameSession.MenuSceneName &&
                   scene.name != GameSession.MatchSceneName;
        }

        private void ShowSettings()
        {
            EnsurePanel();
            if (settingsPanel == null)
            {
                return;
            }

            EnsureEventSystem();
            CaptureCursor();
            settingsPanel.Show();
            KeepCursorAvailable();
        }

        private void HideSettings()
        {
            if (settingsPanel != null && settingsPanel.IsOpen)
            {
                settingsPanel.Hide();
            }

            RestoreCursor();
        }

        private void EnsurePanel()
        {
            if (settingsPanel != null)
            {
                return;
            }

            settingsPanel = GameSettingsPanel.Create(
                (RectTransform)transform,
                BoardHudOverlayFactory.CreateFont(),
                RestoreCursor);
            settingsPanel.Bind();
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject(
                "GlobalSettingsEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        private void CaptureCursor()
        {
            if (cursorCaptured)
            {
                return;
            }

            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            cursorCaptured = true;
        }

        private void KeepCursorAvailable()
        {
            if (!IsOpen)
            {
                RestoreCursor();
                return;
            }

            if (cursorCaptured)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void RestoreCursor()
        {
            if (!cursorCaptured)
            {
                return;
            }

            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;
            cursorCaptured = false;
        }
    }
}
