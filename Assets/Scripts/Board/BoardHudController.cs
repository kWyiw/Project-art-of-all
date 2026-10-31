using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Match;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ProjectArtOfAll.Core.Cards;

namespace ProjectArtOfAll.Board
{
    [AddComponentMenu("Project Art of All/UI/Board HUD")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BoardHudController : MonoBehaviour
    {
        [Header("Card Drawer")]
        [SerializeField] private RectTransform drawerPanel;
        [SerializeField] private Text drawerHandleLabel;
        [SerializeField] private Image cardDrawerScrim;
        [SerializeField] private Vector2 drawerOpenPosition = new Vector2(0f, 58f);
        [SerializeField] private Vector2 drawerClosedPosition = new Vector2(0f, -342f);
        [SerializeField, Min(0.01f)] private float drawerAnimationSeconds = 0.24f;
        [SerializeField] private bool openInEditMode = true;
        [SerializeField] private Text assemblyLabel;
        [SerializeField] private RectTransform assemblyLockOverlay;
        [SerializeField] private RawImage assemblyLockHatch;
        [SerializeField, Min(1f)] private float assemblySlotStartX = 140f;
        [SerializeField, Min(1f)] private float assemblySlotWidth = 86f;
        [SerializeField, Min(1f)] private float hatchPatternSize = 64f;

        [Header("Dice")]
        [SerializeField] private Text diceResult;
        [SerializeField] private Button diceButton;
        [SerializeField] private BoardGameplayController boardGameplay;
        [SerializeField] private Text matchStatus;

        [Header("Fork Arrows")]
        [SerializeField] private RectTransform forkArrowLayer;
        [SerializeField] private Button[] forkArrowButtons;
        [SerializeField] private Text[] forkArrowLabels;

        [Header("Player Encounter")]
        [SerializeField] private GameObject encounterOverlay;
        [SerializeField] private RectTransform encounterPanel;
        [SerializeField] private Text encounterTitle;
        [SerializeField] private Text encounterSubtitle;
        [SerializeField] private Text encounterAttackerName;
        [SerializeField] private Text encounterAttackerStats;
        [SerializeField] private Text encounterAttackerLoadout;
        [SerializeField] private Text encounterDefenderName;
        [SerializeField] private Text encounterDefenderStats;
        [SerializeField] private Text encounterDefenderLoadout;
        [SerializeField] private Text encounterStakes;
        [SerializeField] private Button encounterAcceptButton;
        [SerializeField] private Text encounterAcceptLabel;

        private bool isOpen;
        private Vector2 animationVelocity;
        private Action<int> forkSelectionHandler;
        private bool forkArrowsVisible;
        private Action encounterAcceptedHandler;
        private bool encounterVisible;
        private float encounterShownAt;
        private bool encounterButtonBound;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                EnsureOverlayViews();
            }

            isOpen = !Application.isPlaying && openInEditMode;
            ApplyDrawerLabel();
            if (drawerPanel != null)
            {
                drawerPanel.anchoredPosition =
                    isOpen ? drawerOpenPosition : drawerClosedPosition;
            }

            if (diceResult != null)
            {
                diceResult.text = "—";
            }

            HideForkChoices();
            HideEncounter();
            RefreshDrawerScrim();
            if (boardGameplay == null)
            {
                boardGameplay = FindObjectOfType<BoardGameplayController>();
            }
        }

        private void Update()
        {
            if (drawerPanel == null)
            {
                return;
            }

            Vector2 target = isOpen ? drawerOpenPosition : drawerClosedPosition;
            if (Application.isPlaying)
            {
                drawerPanel.anchoredPosition = Vector2.SmoothDamp(
                    drawerPanel.anchoredPosition,
                    target,
                    ref animationVelocity,
                    drawerAnimationSeconds,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }
            else
            {
                drawerPanel.anchoredPosition = target;
            }

            if (forkArrowsVisible && forkArrowButtons != null)
            {
                float scale = 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.08f;
                foreach (Button button in forkArrowButtons)
                {
                    if (button != null && button.gameObject.activeSelf)
                    {
                        button.transform.localScale = Vector3.one * scale;
                    }
                }
            }

            if (encounterVisible && encounterPanel != null)
            {
                float progress = Mathf.Clamp01(
                    (Time.unscaledTime - encounterShownAt) / 0.16f);
                float scale = Mathf.Lerp(
                    0.97f,
                    1f,
                    Mathf.SmoothStep(0f, 1f, progress));
                encounterPanel.localScale = Vector3.one * scale;
            }
        }

        public void ToggleDrawer()
        {
            isOpen = !isOpen;
            ApplyDrawerLabel();
            RefreshDrawerScrim();
        }

        public void SetDrawerOpen(bool open)
        {
            isOpen = open;
            ApplyDrawerLabel();
            RefreshDrawerScrim();
        }

        public void RollDice()
        {
            SetDrawerOpen(false);
            if (boardGameplay != null)
            {
                boardGameplay.RollDice();
                return;
            }

            if (diceResult == null)
            {
                return;
            }

            diceResult.text = UnityEngine.Random.Range(1, 7).ToString();
        }

        public void SetDiceResult(int value)
        {
            if (diceResult != null)
            {
                diceResult.text = value.ToString();
            }
        }

        public void SetDiceInteractable(bool interactable)
        {
            if (diceButton != null)
            {
                diceButton.interactable = interactable;
            }
        }

        public void BindGameplayController(BoardGameplayController controller)
        {
            boardGameplay = controller;
        }

        public void SetMatchStatus(string status)
        {
            if (matchStatus != null)
            {
                matchStatus.text = status;
            }
        }

        public void ShowForkChoices(
            IReadOnlyList<BoardForkChoice> choices,
            Action<int> onSelected)
        {
            if (forkArrowLayer == null ||
                forkArrowButtons == null ||
                forkArrowLabels == null)
            {
                return;
            }

            forkArrowLayer.gameObject.SetActive(true);
            forkArrowsVisible = true;
            forkSelectionHandler = onSelected;

            for (int index = 0; index < forkArrowButtons.Length; index++)
            {
                Button button = forkArrowButtons[index];
                if (button == null)
                {
                    continue;
                }

                bool visible = index < choices.Count && choices[index].ScreenPosition.z > 0f;
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                RectTransform rect = button.GetComponent<RectTransform>();
                rect.position = choices[index].ScreenPosition;
                rect.localScale = Vector3.one;
                forkArrowLabels[index].text = choices[index].Arrow;

                button.onClick.RemoveAllListeners();
                int capturedIndex = index;
                button.onClick.AddListener(() => forkSelectionHandler?.Invoke(capturedIndex));
            }
        }

        public void HideForkChoices()
        {
            forkArrowsVisible = false;
            forkSelectionHandler = null;

            if (forkArrowButtons != null)
            {
                foreach (Button button in forkArrowButtons)
                {
                    if (button != null)
                    {
                        button.gameObject.SetActive(false);
                    }
                }
            }

            if (forkArrowLayer != null)
            {
                forkArrowLayer.gameObject.SetActive(false);
            }
        }

        public bool ShowPlayerEncounter(
            MatchPlayerState attacker,
            MatchPlayerState defender,
            Action onAccepted)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (defender == null)
            {
                throw new ArgumentNullException(nameof(defender));
            }

            EnsureOverlayViews();
            if (encounterOverlay == null || encounterAcceptButton == null)
            {
                return false;
            }

            encounterVisible = true;
            encounterAcceptedHandler = onAccepted;
            encounterShownAt = Time.unscaledTime;
            encounterOverlay.SetActive(true);
            encounterOverlay.transform.SetAsLastSibling();
            if (encounterPanel != null)
            {
                encounterPanel.localScale = Vector3.one * 0.97f;
            }

            if (encounterTitle != null)
            {
                encounterTitle.text = "遭遇战";
            }

            if (encounterSubtitle != null)
            {
                encounterSubtitle.text =
                    $"玩家 {attacker.Id + 1} 与玩家 {defender.Id + 1} 进入同一地块。确认迎战后，战斗立即结算。";
            }

            if (encounterAttackerName != null)
            {
                encounterAttackerName.text = $"玩家 {attacker.Id + 1}";
            }

            if (encounterAttackerStats != null)
            {
                encounterAttackerStats.text = FormatStats(attacker);
            }

            if (encounterAttackerLoadout != null)
            {
                encounterAttackerLoadout.text = FormatLoadout(attacker);
            }

            if (encounterDefenderName != null)
            {
                encounterDefenderName.text = $"玩家 {defender.Id + 1}";
            }

            if (encounterDefenderStats != null)
            {
                encounterDefenderStats.text = FormatStats(defender);
            }

            if (encounterDefenderLoadout != null)
            {
                encounterDefenderLoadout.text = FormatLoadout(defender);
            }

            if (encounterStakes != null)
            {
                encounterStakes.text =
                    "败者失去最多 15 碎片 · 胜者获得这些碎片与 2 胜场";
            }

            if (encounterAcceptLabel != null)
            {
                encounterAcceptLabel.text = "应战";
            }

            if (encounterAcceptButton != null)
            {
                encounterAcceptButton.interactable = true;
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(
                        encounterAcceptButton.gameObject);
                }
            }

            HideForkChoices();
            return true;
        }

        public void HideEncounter()
        {
            encounterVisible = false;
            encounterAcceptedHandler = null;
            if (encounterOverlay != null)
            {
                encounterOverlay.SetActive(false);
            }
        }

        public void SetAssemblyLevel(int level)
        {
            int capacity = level >= 6 ? 10 : LoadoutRules.GetBattleCapacity(level);
            capacity = Mathf.Clamp(capacity, 1, 10);

            if (assemblyLabel != null)
            {
                assemblyLabel.text = $"装配区  {capacity}c / 10c";
            }

            if (assemblyLockOverlay == null)
            {
                return;
            }

            float lockedWidth = (10 - capacity) * assemblySlotWidth;
            assemblyLockOverlay.gameObject.SetActive(lockedWidth > 0f);
            assemblyLockOverlay.anchoredPosition = new Vector2(
                assemblySlotStartX + capacity * assemblySlotWidth,
                assemblyLockOverlay.anchoredPosition.y);
            assemblyLockOverlay.sizeDelta = new Vector2(
                lockedWidth,
                assemblyLockOverlay.sizeDelta.y);

            if (assemblyLockHatch != null)
            {
                assemblyLockHatch.uvRect = new Rect(
                    0f,
                    0f,
                    lockedWidth / hatchPatternSize,
                    assemblyLockOverlay.sizeDelta.y / hatchPatternSize);
            }
        }

        private void ApplyDrawerLabel()
        {
            if (drawerHandleLabel != null)
            {
                drawerHandleLabel.text = isOpen ? "▼  收起卡组  ▼" : "▲  展开卡组  ▲";
            }
        }

        private void AcceptEncounter()
        {
            if (!encounterVisible)
            {
                return;
            }

            Action accepted = encounterAcceptedHandler;
            encounterAcceptedHandler = null;
            encounterVisible = false;

            if (encounterAcceptButton != null)
            {
                encounterAcceptButton.interactable = false;
            }

            if (encounterOverlay != null)
            {
                encounterOverlay.SetActive(false);
            }

            accepted?.Invoke();
        }

        private void RefreshDrawerScrim()
        {
            if (cardDrawerScrim == null)
            {
                return;
            }

            cardDrawerScrim.gameObject.SetActive(isOpen && Application.isPlaying);
        }

        private void EnsureOverlayViews()
        {
            Font font = drawerHandleLabel != null
                ? drawerHandleLabel.font
                : BoardHudOverlayFactory.CreateFont();

            if (cardDrawerScrim == null)
            {
                GameObject scrim = BoardHudOverlayFactory.CreateCardDrawerScrim(
                    transform);
                cardDrawerScrim = scrim.GetComponent<Image>();
            }

            if (cardDrawerScrim != null && drawerPanel != null)
            {
                int scrimIndex = cardDrawerScrim.transform.GetSiblingIndex();
                int drawerIndex = drawerPanel.GetSiblingIndex();
                if (scrimIndex > drawerIndex)
                {
                    cardDrawerScrim.transform.SetSiblingIndex(drawerIndex);
                }
            }

            if (encounterOverlay == null)
            {
                BoardHudEncounterViews views =
                    BoardHudOverlayFactory.CreateEncounterOverlay(transform, font);
                encounterOverlay = views.Overlay;
                encounterPanel = views.Panel;
                encounterTitle = views.Title;
                encounterSubtitle = views.Subtitle;
                encounterAttackerName = views.AttackerName;
                encounterAttackerStats = views.AttackerStats;
                encounterAttackerLoadout = views.AttackerLoadout;
                encounterDefenderName = views.DefenderName;
                encounterDefenderStats = views.DefenderStats;
                encounterDefenderLoadout = views.DefenderLoadout;
                encounterStakes = views.Stakes;
                encounterAcceptButton = views.AcceptButton;
                encounterAcceptLabel = views.AcceptLabel;
            }

            if (encounterAcceptButton != null && !encounterButtonBound)
            {
                encounterAcceptButton.onClick.RemoveListener(AcceptEncounter);
                encounterAcceptButton.onClick.AddListener(AcceptEncounter);
                encounterButtonBound = true;
            }
        }

        private static string FormatStats(MatchPlayerState player)
        {
            return
                $"Lv.{player.Level}  ·  装配 {player.BattleCardWidth}c  ·  " +
                $"碎片 {player.Shards}  ·  胜场 {player.Wins}";
        }

        private static string FormatLoadout(MatchPlayerState player)
        {
            if (player.BattleCards.Count == 0)
            {
                return "当前没有装配战斗卡牌";
            }

            return string.Join(
                "  ·  ",
                player.BattleCards.Select(
                    card =>
                        $"{card.Definition.DisplayName} {card.Definition.SlotSize}c"));
        }
    }
}
