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
    /// Builds the complete gameplay UI as serialized scene objects. The runtime HUD
    /// only binds callbacks and updates text/state; it never creates the stable panels.
    /// </summary>
    internal static class BakeGameplayUI
    {
        private const string Map1Path = "Assets/Scenes/Map1.unity";
        private const string Map2Path = "Assets/Scenes/Map2.unity";
        private const string RootName = "GameplayUI";
        private const string LegacyRootName = "M3BoardUI";
        private const string CanvasName = "HUDCanvas";
        private const string HudName = "GameplayHUD";
        private const string BakedRootName = "BakedOverlays";

        [MenuItem("Tools/Project Art of All/Rebake Gameplay UI")]
        public static void RebakeAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BakeScene(Map1Path);
            BakeScene(Map2Path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GameplayUI] Map1 and Map2 gameplay UI rebuilt as scene components.");
        }

        [MenuItem("Tools/Project Art of All/Validate Gameplay UI")]
        public static void ValidateAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            ValidateScene(Map1Path);
            ValidateScene(Map2Path);
            Debug.Log("[GameplayUI] Validation passed.");
        }

        private static void BakeScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find(RootName) ?? GameObject.Find(LegacyRootName);
            if (root == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat(scenePath, " has no gameplay UI root."));
            }

            root.name = RootName;
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

            Transform canvasTransform = root.transform.Find(CanvasName);
            if (canvasTransform == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat(scenePath, " has no HUDCanvas."));
            }

            Transform focusPanel = canvasTransform.Find("FocusPanel");
            if (focusPanel != null)
            {
                Object.DestroyImmediate(focusPanel.gameObject);
            }

            GameObject hud = GameObject.Find(HudName);
            BoardHudController controller = hud != null
                ? hud.GetComponent<BoardHudController>()
                : Object.FindObjectOfType<BoardHudController>();
            if (controller == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat(scenePath, " has no GameplayHUD."));
            }

            DestroyChild(controller.transform, "CardDrawerRoot");
            DestroyChild(controller.transform, "DrawerHandle");
            DestroyChild(controller.transform, "CardDrawerScrim");
            DestroyChild(controller.transform, "PlayerEncounterOverlay");
            DestroyChild(controller.transform, BakedRootName);
            DestroyChild(controller.transform, "CardBand");

            Font font = BoardHudOverlayFactory.CreateFont();
            GameObject baked = new GameObject(BakedRootName, typeof(RectTransform));
            RectTransform bakedRect = (RectTransform)baked.transform;
            bakedRect.SetParent(controller.transform, false);
            Stretch(bakedRect);

            BoardHudEncounterViews encounter =
                BoardHudOverlayFactory.CreateEncounterOverlay(bakedRect, font);
            encounter.Overlay.SetActive(false);
            BoardHudCardDrawerView cardView =
                BoardHudOverlayFactory.CreateCardDrawerView(bakedRect, font);
            BoardHomeStopView homeStop =
                BoardHudOverlayFactory.CreateHomeStopView(bakedRect, font);
            BoardHudRewardChoiceView reward =
                BoardHudOverlayFactory.CreateRewardChoiceView(bakedRect, font);
            BoardHudShopView shop =
                BoardHudOverlayFactory.CreateShopView(bakedRect, font);
            BoardHudEventView eventView =
                BoardHudOverlayFactory.CreateEventView(bakedRect, font);
            BoardHudCombatResultView combatResult =
                BoardHudOverlayFactory.CreateCombatResultView(bakedRect, font);
            PlayerCornerPanels cornerPanels =
                PlayerCornerPanels.Create(bakedRect, font, null);
            OpponentDossierView dossier =
                OpponentDossierView.Create(bakedRect, font, null);
            BoardEventFeed eventFeed = BoardEventFeed.Create(bakedRect, font);
            BoardFieldInfoPanel fieldInfo = BoardFieldInfoPanel.Create(bakedRect, font);
            BoardTileTooltip tileTooltip = BoardTileTooltip.Create(bakedRect, font);
            BoardRoundBanner roundBanner = BoardRoundBanner.Create(bakedRect, font);
            PauseOverlayView pause = PauseOverlayView.Create(bakedRect, font, null);
            GameSettingsPanel settingsPanel = GameSettingsPanel.Create(bakedRect, font);
            settingsPanel.Bind();
            CombatPlaybackView combatPlayback = CombatPlaybackView.Create(bakedRect, font);
            CardTooltipView cardTooltip = CardTooltipView.Create(bakedRect, font);
            CardBandSceneView cardBand = CardBandSceneView.Create((RectTransform)controller.transform, font);
            cardBand.transform.SetAsFirstSibling();

            SerializedObject serialized = new SerializedObject(controller);
            SetDirect(serialized, "encounterOverlay", encounter.Overlay);
            SetDirect(serialized, "encounterPanel", encounter.Panel);
            SetDirect(serialized, "encounterTitle", encounter.Title);
            SetDirect(serialized, "encounterSubtitle", encounter.Subtitle);
            SetDirect(serialized, "encounterAttackerName", encounter.AttackerName);
            SetDirect(serialized, "encounterAttackerStats", encounter.AttackerStats);
            SetDirect(serialized, "encounterAttackerLoadout", encounter.AttackerLoadout);
            SetDirect(serialized, "encounterDefenderName", encounter.DefenderName);
            SetDirect(serialized, "encounterDefenderStats", encounter.DefenderStats);
            SetDirect(serialized, "encounterDefenderLoadout", encounter.DefenderLoadout);
            SetDirect(serialized, "encounterStakes", encounter.Stakes);
            SetDirect(serialized, "encounterAcceptButton", encounter.AcceptButton);
            SetDirect(serialized, "encounterAcceptLabel", encounter.AcceptLabel);
            SetDirect(serialized, "encounterIgnoreButton", encounter.IgnoreButton);
            SetDirect(serialized, "encounterIgnoreLabel", encounter.IgnoreLabel);
            SetDirect(serialized, "cornerPanels", cornerPanels);
            SetDirect(serialized, "dossier", dossier);
            SetDirect(serialized, "eventFeed", eventFeed);
            SetDirect(serialized, "fieldInfo", fieldInfo);
            SetDirect(serialized, "tileTooltip", tileTooltip);
            SetDirect(serialized, "roundBanner", roundBanner);
            SetDirect(serialized, "pauseOverlay", pause);
            SetDirect(serialized, "combatPlayback", combatPlayback);
            SetDirect(serialized, "cardTooltip", cardTooltip);
            SetDirect(serialized, "cardBandView", cardBand);

            SerializedObject pauseSerialized = new SerializedObject(pause);
            pauseSerialized.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            pauseSerialized.ApplyModifiedPropertiesWithoutUndo();

            SetRelative(serialized, "cardView", "overlay", cardView.Overlay);
            SetRelative(serialized, "cardView", "panel", cardView.Panel);
            SetRelative(serialized, "cardView", "title", cardView.Title);
            SetRelative(serialized, "cardView", "content", cardView.Content);
            SetRelative(serialized, "cardView", "scroll", cardView.Scroll);
            SetRelative(serialized, "cardView", "empty", cardView.Empty);
            SetRelative(serialized, "cardView", "closeButton", cardView.CloseButton);
            SetRelative(serialized, "cardView", "closeLabel", cardView.CloseLabel);

            SetRelative(serialized, "homeStopView", "overlay", homeStop.Overlay);
            SetRelative(serialized, "homeStopView", "panel", homeStop.Panel);
            SetRelative(serialized, "homeStopView", "title", homeStop.Title);
            SetRelative(serialized, "homeStopView", "body", homeStop.Body);
            SetRelative(serialized, "homeStopView", "stopButton", homeStop.StopButton);
            SetRelative(serialized, "homeStopView", "stopLabel", homeStop.StopLabel);
            SetRelative(serialized, "homeStopView", "continueButton", homeStop.ContinueButton);
            SetRelative(serialized, "homeStopView", "continueLabel", homeStop.ContinueLabel);

            SetChoiceView(serialized, "rewardView", reward);
            SetEventView(serialized, eventView);
            SetShopView(serialized, shop);
            SetRelative(serialized, "combatResultView", "overlay", combatResult.Overlay);
            SetRelative(serialized, "combatResultView", "panel", combatResult.Panel);
            SetRelative(serialized, "combatResultView", "title", combatResult.Title);
            SetRelative(serialized, "combatResultView", "subtitle", combatResult.Subtitle);
            SetRelative(serialized, "combatResultView", "body", combatResult.Body);
            SetRelative(serialized, "combatResultView", "closeButton", combatResult.CloseButton);
            SetRelative(serialized, "combatResultView", "closeLabel", combatResult.CloseLabel);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ValidateScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find(RootName);
            if (root == null ||
                GameObject.Find(LegacyRootName) != null ||
                root.transform.Find("HUDCanvas/FocusPanel") != null)
            {
                throw new System.InvalidOperationException(
                    string.Concat(scenePath, " still contains legacy UI roots."));
            }

            MonoBehaviour[] rootBehaviours = root.GetComponents<MonoBehaviour>();
            for (int index = 0; index < rootBehaviours.Length; index++)
            {
                if (rootBehaviours[index] != null &&
                    rootBehaviours[index].GetType().FullName ==
                    "ProjectArtOfAll.Board.BoardTileFocusController")
                {
                    throw new System.InvalidOperationException(
                        string.Concat(scenePath, " still has BoardTileFocusController."));
                }
            }

            BoardHudController controller = Object.FindObjectOfType<BoardHudController>();
            if (controller == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat(scenePath, " has no baked GameplayHUD."));
            }

            if (controller.transform.Find("CardDrawerRoot") != null ||
                controller.transform.Find("DrawerHandle") != null ||
                controller.transform.Find(BakedRootName) == null ||
                controller.transform.Find("CardBand") == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat(scenePath, " still contains duplicate drawer UI."));
            }

            SerializedObject serialized = new SerializedObject(controller);
            string[] directProperties =
            {
                "encounterOverlay",
                "encounterPanel",
                "encounterAcceptButton",
                "encounterIgnoreButton",
                "cornerPanels",
                "dossier",
                "eventFeed",
                "fieldInfo",
                "tileTooltip",
                "roundBanner",
                "pauseOverlay",
                "combatPlayback",
                "cardTooltip",
                "cardBandView"
            };
            for (int index = 0; index < directProperties.Length; index++)
            {
                RequireObject(serialized, directProperties[index]);
            }

            string[] nestedOverlays =
            {
                "cardView",
                "homeStopView",
                "rewardView",
                "shopView",
                "eventView",
                "combatResultView"
            };
            for (int index = 0; index < nestedOverlays.Length; index++)
            {
                RequireRelative(serialized, nestedOverlays[index], "overlay");
            }

            PauseOverlayView pause = serialized.FindProperty("pauseOverlay")
                .objectReferenceValue as PauseOverlayView;
            SerializedObject pauseSerialized = new SerializedObject(pause);
            RequireObject(pauseSerialized, "settingsPanel");

            if (Object.FindObjectsOfType<Text>(true).Length == 0 ||
                Object.FindObjectsOfType<Image>(true).Length == 0)
            {
                throw new System.InvalidOperationException(
                    string.Concat(scenePath, " has no serialized Text/Image UI."));
            }
        }

        private static void RequireObject(SerializedObject root, string propertyName)
        {
            SerializedProperty property = root.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat("Missing baked UI reference ", propertyName, "."));
            }
        }

        private static void RequireRelative(
            SerializedObject root,
            string propertyName,
            string childName)
        {
            SerializedProperty property =
                root.FindProperty(propertyName)?.FindPropertyRelative(childName);
            if (property == null || property.objectReferenceValue == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat(
                        "Missing baked UI reference ",
                        propertyName,
                        ".",
                        childName,
                        "."));
            }
        }

        private static void SetChoiceView(
            SerializedObject root,
            string propertyName,
            BoardHudRewardChoiceView view)
        {
            SetRelative(root, propertyName, "overlay", view.Overlay);
            SetRelative(root, propertyName, "title", view.Title);
            SetRelative(root, propertyName, "subtitle", view.Subtitle);
            SetRelativeArray(root, propertyName, "buttons", view.Buttons);
            SetRelativeArray(root, propertyName, "names", view.Names);
            SetRelativeArray(root, propertyName, "metas", view.Metas);
            SetRelativeArray(root, propertyName, "details", view.Details);
        }

        private static void SetEventView(
            SerializedObject root,
            BoardHudEventView view)
        {
            SetRelative(root, "eventView", "overlay", view.Overlay);
            SetRelative(root, "eventView", "title", view.Title);
            SetRelative(root, "eventView", "subtitle", view.Subtitle);
            SetRelativeArray(root, "eventView", "buttons", view.Buttons);
            SetRelativeArray(root, "eventView", "names", view.Names);
            SetRelativeArray(root, "eventView", "metas", view.Metas);
            SetRelativeArray(root, "eventView", "details", view.Details);
            SetRelativeArray(root, "eventView", "costs", view.Costs);
            SetRelative(root, "eventView", "leaveButton", view.LeaveButton);
            SetRelative(root, "eventView", "leaveLabel", view.LeaveLabel);
        }

        private static void SetShopView(
            SerializedObject root,
            BoardHudShopView view)
        {
            SetRelative(root, "shopView", "overlay", view.Overlay);
            SetRelative(root, "shopView", "title", view.Title);
            SetRelative(root, "shopView", "subtitle", view.Subtitle);
            SetRelativeArray(root, "shopView", "buttons", view.Buttons);
            SetRelativeArray(root, "shopView", "names", view.Names);
            SetRelativeArray(root, "shopView", "metas", view.Metas);
            SetRelativeArray(root, "shopView", "details", view.Details);
            SetRelativeArray(root, "shopView", "prices", view.Prices);
            SetRelative(root, "shopView", "rerollButton", view.RerollButton);
            SetRelative(root, "shopView", "rerollLabel", view.RerollLabel);
            SetRelative(root, "shopView", "rerollHint", view.RerollHint);
            SetRelative(root, "shopView", "leaveButton", view.LeaveButton);
            SetRelative(root, "shopView", "leaveLabel", view.LeaveLabel);
        }

        private static void SetDirect(
            SerializedObject root,
            string propertyName,
            Object value)
        {
            SerializedProperty property = root.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat("Missing serialized property ", propertyName, "."));
            }

            property.objectReferenceValue = value;
        }

        private static void SetRelative(
            SerializedObject root,
            string propertyName,
            string childName,
            Object value)
        {
            SerializedProperty child =
                root.FindProperty(propertyName)?.FindPropertyRelative(childName);
            if (child == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat("Missing serialized property ", propertyName, ".", childName, "."));
            }

            child.objectReferenceValue = value;
        }

        private static void SetRelativeArray<T>(
            SerializedObject root,
            string propertyName,
            string childName,
            T[] values) where T : Object
        {
            SerializedProperty child =
                root.FindProperty(propertyName)?.FindPropertyRelative(childName);
            if (child == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat("Missing serialized property ", propertyName, ".", childName, "."));
            }

            child.arraySize = values == null ? 0 : values.Length;
            for (int index = 0; index < child.arraySize; index++)
            {
                child.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
