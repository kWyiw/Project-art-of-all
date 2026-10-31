using ProjectArtOfAll.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectArtOfAll.Editor
{
    /// <summary>
    /// Explicitly rebuilds the start-menu hierarchy as persistent scene objects.
    /// The baked Canvas, layouts, buttons and labels remain editable in the scene.
    /// </summary>
    internal static class BakeStartMenuUI
    {
        private const string StartMenuPath = "Assets/Scenes/StartMenu.unity";

        [MenuItem("Tools/Project Art of All/Rebake Start Menu UI")]
        private static void BakeFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene activeScene = EditorSceneManager.OpenScene(
                StartMenuPath,
                OpenSceneMode.Single);

            LeftMenuColumn menu = Object.FindObjectOfType<LeftMenuColumn>();
            if (menu == null)
            {
                Debug.LogError("[StartMenu] Could not find LeftMenuColumn.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(menu.gameObject, "Bake Start Menu UI");
            menu.BuildMenuForEditor();

            Transform canvasRoot = menu.transform.Find("LeftMenuCanvas");
            if (canvasRoot == null)
            {
                Debug.LogError("[StartMenu] Failed to bake the menu canvas.");
                return;
            }

            WireButton(canvasRoot, "MenuButton01", menu.StartGame);
            WireButton(canvasRoot, "MenuButton02", menu.OpenMultiplayer);
            WireButton(canvasRoot, "MenuButton03", menu.OpenSettings);
            WireButton(canvasRoot, "MenuButton04", menu.QuitGame);

            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Selection.activeGameObject = canvasRoot.gameObject;
            Debug.Log("[StartMenu] Baked the menu hierarchy into StartMenu.unity.");
        }

        private static void WireButton(
            Transform canvasRoot,
            string buttonName,
            UnityAction action)
        {
            Transform buttonTransform = canvasRoot.Find($"MenuColumn/Panel/{buttonName}");
            if (buttonTransform == null)
            {
                Debug.LogError($"[StartMenu] Could not find {buttonName} while baking.");
                return;
            }

            Button button = buttonTransform.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(button.onClick, action);
            EditorUtility.SetDirty(button);
        }
    }
}
