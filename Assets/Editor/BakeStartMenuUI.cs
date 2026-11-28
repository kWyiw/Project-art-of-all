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
    public static class BakeStartMenuUI
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

            CardCodexView cardCodex = CardCodexView.Create(canvas, font);
            cardCodex.gameObject.SetActive(false);
            menu.EditorCardCodex = cardCodex;

            CardTooltipView cardTooltip = CardTooltipView.Create(canvas, font);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ValidateScene(scene);
            Debug.Log("[StartMenu] Complete UI baked into scene components.");
        }

        public static void RebakeBatch()
        {
            RebakeFromMenu();
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

        public static void ValidateBatch()
        {
            ValidateFromMenu();
        }

        private static void ValidateScene(Scene scene)
        {
            LeftMenuColumn menu = Object.FindObjectOfType<LeftMenuColumn>();
            if (menu == null)
            {
                throw new System.InvalidOperationException("Missing LeftMenuColumn.");
            }

            Transform canvas = menu.transform.Find("LeftMenuCanvas");
            Transform menuColumn = canvas != null ? canvas.Find("MenuColumn") : null;
            if (canvas == null || menuColumn == null ||
                canvas.Find("Background") == null ||
                canvas.Find("SnowField") == null ||
                canvas.Find("Title") == null ||
                canvas.Find("Version") == null ||
                canvas.Find("ModePanel") == null ||
                canvas.Find("GameSettingsPanel") == null ||
                canvas.Find("CombatLab") == null ||
                canvas.Find("CardCodex") == null ||
                canvas.Find("CardTooltip") == null)
            {
                throw new System.InvalidOperationException(
                    "StartMenu is missing baked UI roots.");
            }

            Image background = canvas.Find("Background").GetComponent<Image>();
            if (background == null || background.sprite == null ||
                background.GetComponent<StartMenuParallaxBackground>() == null)
            {
                throw new System.InvalidOperationException(
                    "StartMenu background image or parallax component is missing.");
            }

            Transform snowTransform = canvas.Find("SnowField");
            StartMenuSnowField snow =
                snowTransform != null
                    ? snowTransform.GetComponent<StartMenuSnowField>()
                    : null;
            if (snow == null ||
                snow.raycastTarget ||
                snow.ParticleCount != 350 ||
                snow.FarParticleCount != 200 ||
                snow.MidParticleCount != 125 ||
                snow.NearParticleCount != 25 ||
                snow.mainTexture == null)
            {
                throw new System.InvalidOperationException(
                    "StartMenu snow field is missing or has the wrong particle setup.");
            }

            if (snowTransform.GetSiblingIndex() != 1)
            {
                throw new System.InvalidOperationException(
                    "StartMenu snow field must sit directly above the background.");
            }

            Transform bgmTransform = menu.transform.Find("StartMenuBgm");
            StartMenuBgm bgm =
                bgmTransform != null
                    ? bgmTransform.GetComponent<StartMenuBgm>()
                    : null;
            if (bgm == null)
            {
                throw new System.InvalidOperationException(
                    "StartMenu background music component is missing.");
            }

            bgm.EnsureConfigured();
            if (bgm.Source == null ||
                bgm.Clip == null ||
                !bgm.Source.loop ||
                !Mathf.Approximately(bgm.Source.spatialBlend, 0f) ||
                Mathf.Abs(bgm.Clip.length - 55f) > 0.1f)
            {
                throw new System.InvalidOperationException(
                    "StartMenu background music is missing or configured incorrectly.");
            }

            RequireReference(bgm, "source", "StartMenuBgm");
            RequireReference(bgm, "clip", "StartMenuBgm");

            Text title = canvas.Find("Title").GetComponent<Text>();
            if (title == null || title.text != menu.EditorTitleText ||
                title.font == null ||
                title.font.name != menu.EditorTitleFontName ||
                title.GetComponent<StartMenuTextEffect>() == null)
            {
                throw new System.InvalidOperationException(
                    "StartMenu title is not baked with the expected text, font, and text effect.");
            }

            Text version = canvas.Find("Version").GetComponent<Text>();
            if (version == null || string.IsNullOrWhiteSpace(version.text))
            {
                throw new System.InvalidOperationException(
                    "StartMenu version label is missing.");
            }

            string[] labels = menu.EditorMenuLabels;
            StartMenuOptionView[] options =
                menuColumn.GetComponentsInChildren<StartMenuOptionView>(true);
            if (options.Length != labels.Length)
            {
                throw new System.InvalidOperationException(
                    string.Concat(
                        "StartMenu option count mismatch: ",
                        options.Length,
                        " baked, ",
                        labels.Length,
                        " defined."));
            }

            for (int index = 0; index < options.Length; index++)
            {
                if (options[index].Label != labels[index])
                {
                    throw new System.InvalidOperationException(
                        string.Concat(
                            "StartMenu option order mismatch at ",
                            index,
                            ": ",
                            options[index].Label,
                            " != ",
                            labels[index],
                            "."));
                }

                string[] optionFields =
                {
                    "label", "textEffect", "hoverBackground",
                    "hoverBackgroundGroup", "goldLine", "goldLineGroup", "optionGroup"
                };
                for (int field = 0; field < optionFields.Length; field++)
                {
                    RequireReference(
                        options[index],
                        optionFields[field],
                        "StartMenuOptionView." + options[index].Label);
                }

                Transform hover = options[index].transform.Find("HoverBackground");
                Transform line = options[index].transform.Find("GoldLine");
                Transform hitArea = options[index].transform.Find("HitArea");
                if (hover == null ||
                    hover.GetComponent<TrapezoidGraphic>() == null ||
                    line == null ||
                    line.GetComponent<StartMenuWipeGraphic>() == null ||
                    hitArea == null ||
                    hitArea.GetComponent<TrapezoidGraphic>() == null ||
                    hitArea.GetComponent<StartMenuOptionHitArea>() == null)
                {
                    throw new System.InvalidOperationException(
                        string.Concat(
                            "StartMenu option ",
                            options[index].Label,
                            " is missing wipe or hit-area graphics."));
                }

                RectTransform hoverRect = hover as RectTransform;
                RectTransform hitRect = hitArea as RectTransform;
                TrapezoidGraphic hoverGraphic = hover.GetComponent<TrapezoidGraphic>();
                TrapezoidGraphic hitGraphic = hitArea.GetComponent<TrapezoidGraphic>();
                if (hoverRect == null || hitRect == null ||
                    !Mathf.Approximately(
                        hoverRect.anchoredPosition.x,
                        hitRect.anchoredPosition.x) ||
                    !Mathf.Approximately(
                        hoverRect.sizeDelta.x,
                        hitRect.sizeDelta.x) ||
                    !Mathf.Approximately(
                        hoverRect.sizeDelta.y,
                        hitRect.sizeDelta.y) ||
                    !Mathf.Approximately(
                        hoverGraphic.RightSlant,
                        hitGraphic.RightSlant) ||
                    !hitGraphic.raycastTarget)
                {
                    throw new System.InvalidOperationException(
                        string.Concat(
                            "StartMenu option ",
                            options[index].Label,
                            " hit area does not match its hover background."));
                }

                if (options[index].GetComponent<Image>() != null)
                {
                    throw new System.InvalidOperationException(
                        string.Concat(
                            "StartMenu option ",
                            options[index].Label,
                            " still has a full-row Image on its root."));
                }

                StartMenuOptionHitArea hitComponent =
                    hitArea.GetComponent<StartMenuOptionHitArea>();
                RequireReference(
                    hitComponent,
                    "option",
                    "StartMenuOptionHitArea." + options[index].Label);
                RequireReference(
                    hitComponent,
                    "button",
                    "StartMenuOptionHitArea." + options[index].Label);
            }

            string[] fields =
            {
                "canvasRect", "menuColumn", "settingsPanel", "modePanel",
                "combatLab", "cardCodex", "hoverAudioSource"
            };
            for (int index = 0; index < fields.Length; index++)
            {
                RequireReference(menu, fields[index], "LeftMenuColumn");
            }

            if (Resources.Load<AudioClip>("SFX/paper_hover") == null)
            {
                throw new System.InvalidOperationException(
                    "Missing hover audio Resources/SFX/paper_hover.wav.");
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

            Debug.Log(
                $"[StartMenu] SnowField particles={snow.ParticleCount} "
                + $"layers={snow.FarParticleCount}/{snow.MidParticleCount}/"
                + $"{snow.NearParticleCount}");
            Debug.Log(
                $"[StartMenu] BGM clip={bgm.Clip.name} "
                + $"seconds={bgm.Clip.length:0.00} loop={bgm.Source.loop}");
        }

        private static void RequireReference(
            Object target,
            string field,
            string context)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || property.objectReferenceValue == null)
            {
                throw new System.InvalidOperationException(
                    string.Concat("Missing ", context, " reference ", field, "."));
            }
        }
    }
}
