using ProjectArtOfAll.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectArtOfAll.Editor
{
    internal static class CreateM3BoardUI
    {
        private const string MainGamePath = "Assets/Scenes/MainGame.unity";
        private const string UiRootName = "M3BoardUI";

        [MenuItem("Tools/Project Art of All/Create or Rebuild M3 Board UI")]
        private static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = OpenMainGameScene();
            GameObject existing = GameObject.Find(UiRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            Build(scene);
        }

        private static Scene OpenMainGameScene()
        {
            Scene active = SceneManager.GetActiveScene();
            return active.path == MainGamePath
                ? active
                : EditorSceneManager.OpenScene(MainGamePath, OpenSceneMode.Single);
        }

        private static void Build(Scene scene)
        {
            Font font = CreateFont();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create M3 tile focus UI");

            GameObject uiRoot = new GameObject(
                UiRootName,
                typeof(BoardTileFocusController));
            Undo.RegisterCreatedObjectUndo(uiRoot, "Create tile focus UI");

            GameObject canvasObject = new GameObject(
                "HUDCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(uiRoot.transform, false);
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create tile focus canvas");

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject focusPanel = new GameObject(
                "FocusPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            focusPanel.transform.SetParent(canvasObject.transform, false);

            RectTransform focusRect = focusPanel.GetComponent<RectTransform>();
            focusRect.anchorMin = new Vector2(1f, 1f);
            focusRect.anchorMax = new Vector2(1f, 1f);
            focusRect.pivot = new Vector2(0f, 1f);
            focusRect.anchoredPosition = new Vector2(-414f, -24f);
            focusRect.sizeDelta = new Vector2(390f, 176f);

            Image background = focusPanel.GetComponent<Image>();
            background.color = new Color(0.035f, 0.05f, 0.065f, 0.92f);
            background.raycastTarget = false;

            Outline outline = focusPanel.GetComponent<Outline>();
            outline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject accent = new GameObject(
                "Accent",
                typeof(RectTransform),
                typeof(Image));
            accent.transform.SetParent(focusPanel.transform, false);
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.offsetMin = Vector2.zero;
            accentRect.offsetMax = new Vector2(5f, 0f);
            Image accentImage = accent.GetComponent<Image>();
            accentImage.color = new Color(0.95f, 0.75f, 0.30f, 1f);
            accentImage.raycastTarget = false;

            Text focusTitle = CreateText(
                focusPanel.transform,
                "Title",
                string.Empty,
                font,
                23,
                FontStyle.Bold,
                new Vector2(22f, -16f),
                new Vector2(342f, 32f));
            Text focusDetails = CreateText(
                focusPanel.transform,
                "Details",
                string.Empty,
                font,
                18,
                FontStyle.Normal,
                new Vector2(22f, -54f),
                new Vector2(342f, 104f));
            focusDetails.horizontalOverflow = HorizontalWrapMode.Wrap;
            focusDetails.verticalOverflow = VerticalWrapMode.Truncate;

            BoardTileFocusController controller =
                uiRoot.GetComponent<BoardTileFocusController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("boardCamera").objectReferenceValue = Camera.main;
            serializedController.FindProperty("canvas").objectReferenceValue = canvas;
            serializedController.FindProperty("focusPanel").objectReferenceValue = focusPanel;
            serializedController.FindProperty("focusRect").objectReferenceValue = focusRect;
            serializedController.FindProperty("focusTitle").objectReferenceValue = focusTitle;
            serializedController.FindProperty("focusDetails").objectReferenceValue = focusDetails;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            focusPanel.SetActive(false);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = focusPanel;
            Debug.Log("[M3BoardUI] Created an editor-visible tile text panel.");
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string content,
            Font font,
            int fontSize,
            FontStyle style,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = new Color(0.95f, 0.94f, 0.89f, 1f);
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
            return text;
        }

        private static Font CreateFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Microsoft YaHei UI",
                    "Microsoft YaHei",
                    "PingFang SC",
                    "Noto Sans CJK SC",
                    "Arial"
                },
                22);
            return font != null
                ? font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
