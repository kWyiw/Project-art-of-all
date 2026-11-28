using System;
using System.Linq;
using System.Reflection;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Flow;
using ProjectArtOfAll.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Regression probe for the persisted developer setting, board-only debug controls,
/// and the developer card-codex acquire buttons.
/// </summary>
public static class DeveloperModeProbe
{
    [MenuItem("Tools/Project Art of All/Probe Developer Mode")]
    public static void ProbeFromMenu()
    {
        Probe(false);
    }

    public static void ProbeBatch()
    {
        Probe(true);
    }

    private static void Probe(bool exitEditor)
    {
        bool original = GameSettings.DeveloperMode;
        int exitCode = 1;
        try
        {
            bool settingsOk = ProbeBakedSettings();
            bool controlsOk = ProbeBoardControls();
            bool acquireOk = ProbeAcquireButtons();
            bool passed = settingsOk && controlsOk && acquireOk;
            string message =
                $"[developer-probe] settings={(settingsOk ? "yes" : "no")} "
                + $"controls={(controlsOk ? "yes" : "no")} "
                + $"acquire={(acquireOk ? "yes" : "no")}";
            if (passed)
            {
                Debug.Log(message);
                exitCode = 0;
            }
            else
            {
                Debug.LogError(message);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            GameSettings.DeveloperMode = original;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (exitEditor)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }

    private static bool ProbeBakedSettings()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/StartMenu.unity",
            OpenSceneMode.Single);
        GameSettingsPanel panel = UnityEngine.Object
            .FindObjectsOfType<GameSettingsPanel>(true)
            .FirstOrDefault();
        if (panel == null)
        {
            return false;
        }

        panel.Bind();
        Transform label = panel.transform.Find("Panel/DeveloperModeLabel");
        Transform button = panel.transform.Find("Panel/Button_DeveloperMode");
        Text labelText = label == null ? null : label.GetComponent<Text>();
        GameSettings.DeveloperMode = true;
        panel.RefreshControls();
        bool showsOn = labelText != null && labelText.text == "开";
        GameSettings.DeveloperMode = false;
        panel.RefreshControls();
        bool showsOff = labelText != null && labelText.text == "关";
        bool result = labelText != null &&
                      button != null &&
                      button.GetComponent<Button>() != null &&
                      showsOn &&
                      showsOff;
        // EditMode does not dispatch OnDestroy for an ordinary MonoBehaviour.
        InvokePrivate(panel, "OnDestroy");
        return result;
    }

    private static bool ProbeBoardControls()
    {
        EditorSceneManager.OpenScene(
            "Assets/Scenes/Map1.unity",
            OpenSceneMode.Single);
        BoardHudController hud = UnityEngine.Object
            .FindObjectsOfType<BoardHudController>(true)
            .FirstOrDefault();
        if (hud == null)
        {
            return false;
        }

        GameSettings.DeveloperMode = true;
        InvokePrivate(hud, "ApplyDeveloperMode");
        bool shown = IsActive("TestUiToggle") &&
                     IsActive("DeveloperCodexToggle") &&
                     IsActive("DeveloperRollControls");

        GameSettings.DeveloperMode = false;
        InvokePrivate(hud, "ApplyDeveloperMode");
        bool hidden = !IsActive("TestUiToggle") &&
                      !IsActive("DeveloperCodexToggle") &&
                      !IsActive("DeveloperRollControls");
        return shown && hidden;
    }

    private static bool ProbeAcquireButtons()
    {
        GameObject canvasObject = new GameObject(
            "DeveloperProbeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        RectTransform canvas = canvasObject.GetComponent<RectTransform>();
        canvas.sizeDelta = new Vector2(1920f, 1080f);

        CardDefinition picked = null;
        CardCodexView codex = CardCodexView.Create(
            canvas,
            BoardHudOverlayFactory.CreateFont());
        codex.ConfigureDeveloper(
            true,
            card =>
            {
                picked = card;
                return "已获得【" + card.DisplayName + "】";
            });
        codex.Show();

        Button acquire = codex.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.gameObject.name == "Acquire");
        if (acquire != null)
        {
            acquire.onClick.Invoke();
        }

        Button close = codex.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button.gameObject.name == "Close");
        bool closeOk = close != null &&
                       close.GetComponentInChildren<Text>(true)?.text == "返回游戏";
        if (close != null)
        {
            close.onClick.Invoke();
        }

        codex.ConfigureDeveloper(false, null);
        codex.Show();
        bool hiddenWhenDisabled = !codex
            .GetComponentsInChildren<Button>(true)
            .Any(button => button.gameObject.name == "Acquire");
        codex.Hide();

        bool result = picked != null &&
                      closeOk &&
                      hiddenWhenDisabled &&
                      !codex.IsOpen;
        UnityEngine.Object.DestroyImmediate(canvasObject);
        return result;
    }

    private static bool IsActive(string objectName)
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
        return objects.Any(obj => obj.name == objectName && obj.activeInHierarchy);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException(
                "Missing method " + target.GetType().Name + "." + methodName);
        }

        method.Invoke(target, null);
    }
}
