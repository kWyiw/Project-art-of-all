using ProjectArtOfAll.Board;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectArtOfAll.Editor
{
    internal static class CreateBoardGameplayUI
    {
        private const string MainGamePath = "Assets/Scenes/MainGame.unity";
        private const string CanvasName = "HUDCanvas";
        private const string HudRootName = "GameplayHUD";
        private const float SlotWidth = 86f;
        private const float SlotStartX = 140f;

        [MenuItem("Tools/Project Art of All/Create or Rebuild Gameplay HUD")]
        private static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = OpenMainGameScene();
            GameObject existing = GameObject.Find(HudRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            Build(scene);
        }

        public static void RebuildForAutomation()
        {
            Scene scene = OpenMainGameScene();
            GameObject existing = GameObject.Find(HudRootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            Build(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[GameplayHUD] Automation rebuild completed.");
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
            GameObject canvasObject = GameObject.Find(CanvasName);
            if (canvasObject == null)
            {
                Debug.LogError("[GameplayHUD] HUDCanvas is missing.");
                return;
            }

            EnsureEventSystem();
            Font font = CreateFont();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create gameplay HUD");

            GameObject hudRoot = new GameObject(
                HudRootName,
                typeof(RectTransform),
                typeof(BoardHudController));
            hudRoot.transform.SetParent(canvasObject.transform, false);
            Undo.RegisterCreatedObjectUndo(hudRoot, "Create gameplay HUD");

            RectTransform hudRect = hudRoot.GetComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            Text matchStatus = CreateMatchStatus(
                hudRoot.transform,
                font);
            GameObject drawerPanel = CreateDrawer(
                hudRoot.transform,
                font);
            GameObject handle = CreateDrawerHandle(
                hudRoot.transform,
                font);
            GameObject dicePanel = CreateDicePanel(
                hudRoot.transform,
                font,
                out Text diceResult);
            GameObject forkArrowLayer = CreateForkArrowLayer(
                hudRoot.transform,
                font,
                out Button[] forkArrowButtons,
                out Text[] forkArrowLabels);
            GameObject cardDrawerScrim =
                BoardHudOverlayFactory.CreateCardDrawerScrim(hudRoot.transform);
            BoardHudEncounterViews encounterViews =
                BoardHudOverlayFactory.CreateEncounterOverlay(
                    hudRoot.transform,
                    font);

            BoardHudController controller = hudRoot.GetComponent<BoardHudController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("drawerPanel").objectReferenceValue =
                drawerPanel.GetComponent<RectTransform>();
            serializedController.FindProperty("cardDrawerScrim").objectReferenceValue =
                cardDrawerScrim.GetComponent<Image>();
            serializedController.FindProperty("drawerHandleLabel").objectReferenceValue =
                handle.GetComponentInChildren<Text>(true);
            serializedController.FindProperty("diceResult").objectReferenceValue =
                diceResult;
            serializedController.FindProperty("matchStatus").objectReferenceValue =
                matchStatus;
            serializedController.FindProperty("diceButton").objectReferenceValue =
                dicePanel.GetComponent<Button>();
            serializedController.FindProperty("forkArrowLayer").objectReferenceValue =
                forkArrowLayer.GetComponent<RectTransform>();
            serializedController.FindProperty("encounterOverlay").objectReferenceValue =
                encounterViews.Overlay;
            serializedController.FindProperty("encounterPanel").objectReferenceValue =
                encounterViews.Panel;
            serializedController.FindProperty("encounterTitle").objectReferenceValue =
                encounterViews.Title;
            serializedController.FindProperty("encounterSubtitle").objectReferenceValue =
                encounterViews.Subtitle;
            serializedController.FindProperty("encounterAttackerName").objectReferenceValue =
                encounterViews.AttackerName;
            serializedController.FindProperty("encounterAttackerStats").objectReferenceValue =
                encounterViews.AttackerStats;
            serializedController.FindProperty("encounterAttackerLoadout").objectReferenceValue =
                encounterViews.AttackerLoadout;
            serializedController.FindProperty("encounterDefenderName").objectReferenceValue =
                encounterViews.DefenderName;
            serializedController.FindProperty("encounterDefenderStats").objectReferenceValue =
                encounterViews.DefenderStats;
            serializedController.FindProperty("encounterDefenderLoadout").objectReferenceValue =
                encounterViews.DefenderLoadout;
            serializedController.FindProperty("encounterStakes").objectReferenceValue =
                encounterViews.Stakes;
            serializedController.FindProperty("encounterAcceptButton").objectReferenceValue =
                encounterViews.AcceptButton;
            serializedController.FindProperty("encounterAcceptLabel").objectReferenceValue =
                encounterViews.AcceptLabel;
            serializedController.FindProperty("assemblyLabel").objectReferenceValue =
                drawerPanel.transform.Find("AssemblyLabel").GetComponent<Text>();
            serializedController.FindProperty("assemblyLockOverlay").objectReferenceValue =
                drawerPanel.transform.Find("AssemblyLockHatch").GetComponent<RectTransform>();
            serializedController.FindProperty("assemblyLockHatch").objectReferenceValue =
                drawerPanel.transform.Find("AssemblyLockHatch").GetComponent<RawImage>();
            serializedController.FindProperty("assemblySlotStartX").floatValue =
                SlotStartX;
            serializedController.FindProperty("assemblySlotWidth").floatValue =
                SlotWidth;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            SetButtonArray(
                serializedController.FindProperty("forkArrowButtons"),
                forkArrowButtons);
            SetTextArray(
                serializedController.FindProperty("forkArrowLabels"),
                forkArrowLabels);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            controller.SetDrawerOpen(!Application.isPlaying);
            controller.SetAssemblyLevel(1);
            controller.HideForkChoices();
            controller.HideEncounter();

            int scrimIndex = cardDrawerScrim.transform.GetSiblingIndex();
            int drawerIndex = drawerPanel.transform.GetSiblingIndex();
            if (scrimIndex > drawerIndex)
            {
                cardDrawerScrim.transform.SetSiblingIndex(drawerIndex);
            }

            UnityEventTools.AddPersistentListener(
                handle.GetComponent<Button>().onClick,
                controller.ToggleDrawer);
            UnityEventTools.AddPersistentListener(
                dicePanel.GetComponent<Button>().onClick,
                controller.RollDice);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = hudRoot;
            Debug.Log("[GameplayHUD] Created dice button and card drawer.");
        }

        private static Text CreateMatchStatus(Transform parent, Font font)
        {
            GameObject statusObject = new GameObject(
                "MatchStatus",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            statusObject.transform.SetParent(parent, false);

            RectTransform rect = statusObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(900f, 48f);

            Image image = statusObject.GetComponent<Image>();
            image.color = new Color(0.035f, 0.05f, 0.065f, 0.88f);
            image.raycastTarget = false;

            Outline outline = statusObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.38f);
            outline.effectDistance = new Vector2(1f, -1f);

            Text status = CreateText(
                statusObject.transform,
                "Text",
                "正在初始化 M1/M2 对局…",
                font,
                18,
                FontStyle.Normal,
                new Vector2(16f, 0f),
                new Vector2(868f, 48f),
                TextAnchor.MiddleLeft);
            RectTransform textRect = status.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);
            return status;
        }

        private static GameObject CreateForkArrowLayer(
            Transform parent,
            Font font,
            out Button[] buttons,
            out Text[] labels)
        {
            GameObject layer = new GameObject("ForkArrowLayer", typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;

            buttons = new Button[4];
            labels = new Text[4];

            for (int index = 0; index < buttons.Length; index++)
            {
                GameObject arrow = new GameObject(
                    $"ForkArrow_{index + 1:00}",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(Outline));
                arrow.transform.SetParent(layer.transform, false);

                RectTransform rect = arrow.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(76f, 76f);

                Image image = arrow.GetComponent<Image>();
                image.color = new Color(0.96f, 0.70f, 0.16f, 0.96f);

                Outline outline = arrow.GetComponent<Outline>();
                outline.effectColor = new Color(1f, 0.96f, 0.70f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);

                Button button = arrow.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.92f, 0.58f, 1f);
                colors.pressedColor = new Color(0.78f, 0.50f, 0.08f, 1f);
                button.colors = colors;
                buttons[index] = button;

                Text label = CreateText(
                    arrow.transform,
                    "Arrow",
                    "↑",
                    font,
                    42,
                    FontStyle.Bold,
                    Vector2.zero,
                    new Vector2(76f, 76f),
                    TextAnchor.MiddleCenter);
                label.color = new Color(0.06f, 0.08f, 0.09f, 1f);
                RectTransform labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                labels[index] = label;

                arrow.SetActive(false);
            }

            return layer;
        }

        private static void SetButtonArray(SerializedProperty property, Button[] values)
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void SetTextArray(SerializedProperty property, Text[] values)
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static GameObject CreateDrawer(Transform parent, Font font)
        {
            GameObject drawer = new GameObject(
                "CardDrawerRoot",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            drawer.transform.SetParent(parent, false);
            RectTransform rect = drawer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 58f);
            rect.sizeDelta = new Vector2(1200f, 350f);

            Image image = drawer.GetComponent<Image>();
            image.color = new Color(0.035f, 0.05f, 0.065f, 0.94f);
            image.raycastTarget = true;

            Outline outline = drawer.GetComponent<Outline>();
            outline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.46f);
            outline.effectDistance = new Vector2(1f, -1f);

            CreateText(
                drawer.transform,
                "AssemblyLabel",
                "装配区  4c / 10c",
                font,
                19,
                FontStyle.Bold,
                new Vector2(24f, -64f),
                new Vector2(112f, 86f),
                TextAnchor.MiddleLeft);
            CreateText(
                drawer.transform,
                "WarehouseLabel",
                "仓库  10c",
                font,
                19,
                FontStyle.Bold,
                new Vector2(24f, -190f),
                new Vector2(112f, 86f),
                TextAnchor.MiddleLeft);

            for (int index = 0; index < 10; index++)
            {
                CreateSlot(
                    drawer.transform,
                    font,
                    $"AssemblySlot_{index + 1:00}",
                    new Vector2(SlotStartX + index * SlotWidth, -54f),
                    index + 1);
                CreateSlot(
                    drawer.transform,
                    font,
                    $"WarehouseSlot_{index + 1:00}",
                    new Vector2(SlotStartX + index * SlotWidth, -180f),
                    index + 1);
            }

            CreateAssemblyLockOverlay(drawer.transform);

            return drawer;
        }

        private static GameObject CreateDrawerHandle(Transform parent, Font font)
        {
            GameObject handle = new GameObject(
                "DrawerHandle",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            handle.transform.SetParent(parent, false);

            RectTransform rect = handle.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            rect.sizeDelta = new Vector2(300f, 48f);

            Image image = handle.GetComponent<Image>();
            image.color = new Color(0.08f, 0.12f, 0.15f, 0.98f);

            Outline outline = handle.GetComponent<Outline>();
            outline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);

            Text label = CreateText(
                handle.transform,
                "Label",
                "▼  收起卡组  ▼",
                font,
                20,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(300f, 48f),
                TextAnchor.MiddleCenter);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return handle;
        }

        private static GameObject CreateDicePanel(
            Transform parent,
            Font font,
            out Text diceResult)
        {
            GameObject dice = new GameObject(
                "DicePanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            dice.transform.SetParent(parent, false);

            RectTransform rect = dice.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-32f, 32f);
            rect.sizeDelta = new Vector2(190f, 116f);

            Image image = dice.GetComponent<Image>();
            image.color = new Color(0.12f, 0.15f, 0.17f, 0.98f);

            Outline outline = dice.GetComponent<Outline>();
            outline.effectColor = new Color(0.9f, 0.68f, 0.28f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateText(
                dice.transform,
                "Title",
                "投骰子",
                font,
                24,
                FontStyle.Bold,
                new Vector2(16f, -12f),
                new Vector2(108f, 36f),
                TextAnchor.MiddleLeft);
            diceResult = CreateText(
                dice.transform,
                "Result",
                "—",
                font,
                38,
                FontStyle.Bold,
                new Vector2(128f, -28f),
                new Vector2(48f, 58f),
                TextAnchor.MiddleCenter);
            diceResult.color = new Color(0.96f, 0.75f, 0.32f, 1f);
            CreateText(
                dice.transform,
                "Hint",
                "D6",
                font,
                15,
                FontStyle.Normal,
                new Vector2(16f, -64f),
                new Vector2(108f, 26f),
                TextAnchor.MiddleLeft);
            return dice;
        }

        private static void CreateSlot(
            Transform parent,
            Font font,
            string name,
            Vector2 anchoredPosition,
            int number)
        {
            GameObject slot = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            slot.transform.SetParent(parent, false);

            RectTransform rect = slot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(SlotWidth, 98f);

            Image image = slot.GetComponent<Image>();
            image.color = new Color(0.13f, 0.18f, 0.20f, 0.96f);

            Outline outline = slot.GetComponent<Outline>();
            outline.effectColor = new Color(0.62f, 0.72f, 0.73f, 0.5f);
            outline.effectDistance = new Vector2(1f, -1f);

            Text label = CreateText(
                slot.transform,
                "Label",
                number.ToString(),
                font,
                15,
                FontStyle.Normal,
                Vector2.zero,
                new Vector2(SlotWidth, 98f),
                TextAnchor.MiddleCenter);
            label.color = new Color(0.76f, 0.81f, 0.80f, 1f);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private static void CreateAssemblyLockOverlay(Transform drawer)
        {
            GameObject overlay = new GameObject(
                "AssemblyLockHatch",
                typeof(RectTransform),
                typeof(RawImage));
            overlay.transform.SetParent(drawer, false);

            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(SlotStartX + 4f * SlotWidth, -54f);
            rect.sizeDelta = new Vector2(6f * SlotWidth, 98f);

            RawImage rawImage = overlay.GetComponent<RawImage>();
            rawImage.texture = LoadOrCreateHatchTexture();
            rawImage.uvRect = new Rect(0f, 0f, 6f * SlotWidth / 64f, 98f / 64f);
            rawImage.raycastTarget = false;
        }

        private static Texture2D LoadOrCreateHatchTexture()
        {
            const string folder = "Assets/UI";
            const string path = "Assets/UI/DiagonalHatch.asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "UI");
            }

            const int size = 32;
            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                mipChain: false)
            {
                name = "DiagonalHatch",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };

            Color32 background = new Color32(8, 12, 14, 220);
            Color32 stripe = new Color32(186, 192, 190, 145);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] =
                        (x + y) % 16 < 5 ? stripe : background;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string content,
            Font font,
            int fontSize,
            FontStyle style,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment)
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
            text.color = new Color(0.94f, 0.93f, 0.88f, 1f);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
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
