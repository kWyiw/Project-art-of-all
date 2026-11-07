using ProjectArtOfAll.Board;
using ProjectArtOfAll.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectArtOfAll.Editor
{
    /// <summary>
    /// Bakes the complete StartMenu hierarchy into the scene: main buttons, mode
    /// selection, shared settings and the combat lab. Runtime code only binds input.
    /// </summary>
    internal static class BakeStartMenuUI
    {
        private const string StartMenuPath = "Assets/Scenes/StartMenu.unity";

        [MenuItem("Tools/Project Art of All/Rebake Start Menu UI")]
        private static void RebakeFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(
                StartMenuPath,
                OpenSceneMode.Single);

            LeftMenuColumn menu = Object.FindObjectOfType<LeftMenuColumn>();
            if (menu == null)
            {
                throw new System.InvalidOperationException(
                    "StartMenu has no LeftMenuColumn.");
            }

            menu.BuildMenuForEditor();
            RectTransform canvas = menu.EditorCanvasRect;
            Font font = menu.EditorFont;

            GameSettingsPanel settings = GameSettingsPanel.Create(canvas, font);
            settings.Bind();
            menu.EditorSettingsPanel = settings;

            CombatLabView combatLab = CombatLabView.Create(canvas, font);
            combatLab.Bind();
            combatLab.gameObject.SetActive(false);
            menu.EditorCombatLab = combatLab;

            CardTooltipView cardTooltip = CardTooltipView.Create(canvas, font);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ValidateScene(scene);
            Debug.Log("[StartMenu] Complete UI baked into scene components.");
        }

        [MenuItem("Tools/Project Art of All/Validate Start Menu UI")]
        private static void ValidateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(
                StartMenuPath,
                OpenSceneMode.Single);
            ValidateScene(scene);
            Debug.Log("[StartMenu] Validation passed.");
        }

        private static void ValidateScene(Scene scene)
        {
            LeftMenuColumn menu = Object.FindObjectOfType<LeftMenuColumn>();
            if (menu == null)
            {
                throw new System.InvalidOperationException("Missing LeftMenuColumn.");
            }

            Transform canvas = menu.transform.Find("LeftMenuCanvas");
            if (canvas == null ||
                canvas.Find("MenuButton_开始游戏") == null ||
                canvas.Find("MenuButton_设置") == null ||
                canvas.Find("MenuButton_战斗模拟") == null ||
                canvas.Find("MenuButton_退出游戏") == null ||
                canvas.Find("ModePanel") == null ||
                canvas.Find("GameSettingsPanel") == null ||
                canvas.Find("CombatLab") == null ||
                canvas.Find("CardTooltip") == null)
            {
                throw new System.InvalidOperationException(
                    "StartMenu is missing baked UI roots.");
            }

            SerializedObject serialized = new SerializedObject(menu);
            string[] fields = { "canvasRect", "settingsPanel", "modePanel", "combatLab" };
            for (int index = 0; index < fields.Length; index++)
            {
                SerializedProperty property = serialized.FindProperty(fields[index]);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        string.Concat("Missing StartMenu reference ", fields[index], "."));
                }
            }

            if (Object.FindObjectsOfType<Text>(true).Length == 0 ||
                Object.FindObjectsOfType<Image>(true).Length == 0 ||
                Object.FindObjectsOfType<Button>(true).Length == 0)
            {
                throw new System.InvalidOperationException(
                    "StartMenu has no serialized Text/Image/Button UI.");
            }

            if (!scene.IsValid())
            {
                throw new System.InvalidOperationException("Invalid StartMenu scene.");
            }
        }
    }
}
