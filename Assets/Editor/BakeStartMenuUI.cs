using ProjectArtOfAll.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Editor
{
    /// <summary>
    /// 主页面（`StartMenu`）的版式现在完全由 `LeftMenuColumn` 在运行时搭出来，
    /// 场景里只该留一个挂着组件、没有子物体的 GameObject。
    /// 早期烘焙进场景的旧界面（旧按钮绑着已经被删掉的方法）用这个工具清掉。
    /// </summary>
    internal static class BakeStartMenuUI
    {
        private const string StartMenuPath = "Assets/Scenes/StartMenu.unity";

        [MenuItem("Tools/Project Art of All/Clean Start Menu UI")]
        private static void CleanFromMenu()
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

            int removed = 0;
            Transform stale = menu.transform.Find("LeftMenuCanvas");
            while (stale != null)
            {
                Object.DestroyImmediate(stale.gameObject);
                removed++;
                stale = menu.transform.Find("LeftMenuCanvas");
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log(
                $"[StartMenu] Removed {removed} baked canvas(es). " +
                "The menu hierarchy is built by LeftMenuColumn at runtime.");
        }
    }
}
