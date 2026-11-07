using System;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Match;
using ProjectArtOfAll.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Events;

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
        private BoardHudCardDrawerView cardView;
        private CardBandController cardBand;
        private BoardHudCombatResultView combatResultView;
        private BoardHudRewardChoiceView rewardView;
        private BoardHudShopView shopView;
        private Action<int> shopBuyHandler;
        private Action shopRerollHandler;
        private Action shopLeaveHandler;
        private Action<int> rewardChoiceHandler;
        private MatchPlayerState cachedPlayer;
        private MatchState cachedState;
        private int humanPlayerId = -1;
        private PlayerCornerPanels cornerPanels;
        private OpponentDossierView dossier;
        private PauseOverlayView pauseOverlay;
        private BoardEventFeed eventFeed;
        private BoardFieldInfoPanel fieldInfo;
        private BoardTileTooltip tileTooltip;
        private BoardRoundBanner roundBanner;
        private int bannerRound = -1;
        private int eventCursor = -1;
        private int qualityFrames;
        private float qualityElapsed;
        private bool autoDegraded;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                EnsureOverlayViews();
                ApplyGameSettings();
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
            GameSettings.Changed -= OnGameSettingsChanged;
            GameSettings.Changed += OnGameSettingsChanged;
            if (boardGameplay == null)
            {
                boardGameplay = FindObjectOfType<BoardGameplayController>();
            }
        }

        /// <summary>
        /// <see cref="GameSettings.Changed"/> 是静态事件。<c>GameSession</c> 用 LoadScene 切场景、
        /// 暂停菜单的「重开本局」也是 LoadScene，所以旧的 HUD 会被销毁；不在这里退订的话，
        /// 新场景里再点设置项仍会回调到已销毁的实例，进而去摸已经销毁的棋子，抛出
        /// MissingReferenceException（BoardPlayerToken has been destroyed）。
        /// </summary>
        private void OnDestroy()
        {
            GameSettings.Changed -= OnGameSettingsChanged;
        }

        private void Update()
        {
            if (Application.isPlaying && Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }

            TickAutoQuality();

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
            if (cardBand != null)
            {
                cardBand.ToggleCollapsed();
                return;
            }

            SetDrawerOpen(!isOpen);
        }

        public void SetDrawerOpen(bool open)
        {
            isOpen = open;
            ApplyDrawerLabel();
            RefreshDrawerScrim();

            if (!Application.isPlaying || cardView == null)
            {
                return;
            }

            cardView.Overlay.SetActive(open);
            if (open)
            {
                RenderCards();
            }
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

        /// <summary>卡槽预览用的当前玩家；棋盘层每次刷新状态时都会推进来。</summary>
        public void ShowCards(MatchPlayerState player)
        {
            cachedPlayer = player;
            RefreshCardBand(player);
            if (cardView != null && cardView.Overlay.activeSelf)
            {
                RenderCards();
            }
        }

        /// <summary>常驻卡槽带：没有就建，有就按当前玩家重画。</summary>
        public void RefreshCardBand(MatchPlayerState player)
        {
            if (cardBand == null)
            {
                if (boardGameplay == null)
                {
                    return;
                }

                cardBand = CardBandController.Create(
                    boardGameplay,
                    (RectTransform)transform,
                    BoardHudOverlayFactory.CreateFont());
                if (diceButton != null)
                {
                    cardBand.DiceButtonTarget = (RectTransform)diceButton.transform;
                }

                if (drawerHandleLabel != null)
                {
                    RectTransform handle = drawerHandleLabel.transform.parent as RectTransform;
                    if (handle != null && handle != (RectTransform)transform)
                    {
                        cardBand.HandleButtonTarget = handle;
                    }
                }

                cardBand.UserScale = Mathf.Clamp(GameSettings.CardScale, 0.75f, 1.25f);
                cardBand.ApplyScreenLayout();
            }

            cardBand.Refresh(player ?? boardGameplay?.HumanPlayer);
            if (cornerPanels != null)
            {
                cornerPanels.ApplyBandTop(cardBand.CurrentBandHeight);
            }
        }

        /// <summary>开局三句提示：让没看过规则书的人也知道该按什么、能拖什么。</summary>
        public void ShowOpeningHints()
        {
            if (eventFeed == null || !Application.isPlaying)
            {
                return;
            }

            Color hint = new Color(0.92f, 0.84f, 0.60f, 1f);
            eventFeed.Enqueue("掷骰子沿逆时针走廊前进；三岔口可以选择直行或逆时针拐弯", hint);
            eventFeed.Enqueue("把下排卡牌拖进右侧出售区就立刻卖出——出售没有撤销", hint);
            eventFeed.Enqueue("把牌拖进下排战斗栏即装配，相邻加成会实时亮起", hint);
        }

        public void ToggleCardView()
        {
            if (cardView == null)
            {
                return;
            }

            bool open = !cardView.Overlay.activeSelf;
            cardView.Overlay.SetActive(open);
            if (open)
            {
                RenderCards();
            }
        }

        private CombatPlaybackView combatPlayback;

        /// <summary>PlayCombat：把这一场战斗按事件时间轴重播一遍，播完回调 onComplete。</summary>
        public void PlayCombat(
            CombatResolvedEvent combat,
            string leftTitle,
            string rightTitle,
            bool leftIsHuman,
            System.Action onComplete)
        {
            if (combat == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (combatPlayback == null)
            {
                combatPlayback = CombatPlaybackView.Create(
                    transform,
                    BoardHudOverlayFactory.CreateFont());
            }

            combatPlayback.Play(combat, leftTitle, rightTitle, leftIsHuman, onComplete);
        }

        public void ShowCombatResult(string title, string subtitle, string body)
        {
            if (combatResultView == null)
            {
                return;
            }

            combatResultView.Title.text = title;
            combatResultView.Subtitle.text = subtitle;
            combatResultView.Body.text = body;
            combatResultView.Overlay.SetActive(true);
        }

        public void HideCombatResult()
        {
            if (combatResultView != null)
            {
                combatResultView.Overlay.SetActive(false);
            }
        }

        public void SetCombatResultTint(Color color)
        {
            if (combatResultView != null && combatResultView.Title != null)
            {
                combatResultView.Title.color = color;
            }
        }

        private void RenderCards()
        {
            if (cardView == null || cardView.Content == null)
            {
                return;
            }

            Transform content = cardView.Content;
            for (int index = content.childCount - 1; index >= 0; index--)
            {
                Transform child = content.GetChild(index);
                if (cardView.Empty != null &&
                    child == cardView.Empty.transform)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }

            if (cachedPlayer == null)
            {
                cardView.Empty.gameObject.SetActive(true);
                cardView.Title.text = "卡槽";
                FitCardContent(0f);
                return;
            }

            cardView.Empty.gameObject.SetActive(false);

            int battleCapacity = cachedPlayer.Level <= 5
                ? LoadoutRules.GetBattleCapacity(cachedPlayer.Level)
                : 10;
            List<CardInstance> battle = cachedPlayer.BattleCards.ToList();
            HashSet<int> equipped = new HashSet<int>(
                battle.Select(card => card.Uid));
            List<CardInstance> bag = cachedPlayer.OwnedCards
                .Where(card => !equipped.Contains(card.Uid))
                .ToList();

            cardView.Title.text = string.Concat(
                "卡槽 · 玩家 ",
                (cachedPlayer.Id + 1).ToString(),
                " · Lv.",
                cachedPlayer.Level.ToString());

            Font font = drawerHandleLabel != null
                ? drawerHandleLabel.font
                : BoardHudOverlayFactory.CreateFont();
            const float width = 780f;
            float top = 0f;

            top = AddCardSection(
                content,
                font,
                width,
                top,
                string.Concat(
                    "装配（战斗栏） ",
                    cachedPlayer.BattleCardWidth.ToString(),
                    "/",
                    battleCapacity.ToString(),
                    "c"),
                battle,
                "装配区还是空的。");

            top = AddCardSection(
                content,
                font,
                width,
                top,
                string.Concat(
                    "背包 ",
                    cachedPlayer.OwnedCardWidth.ToString(),
                    "/",
                    LoadoutRules.BagCapacity.ToString(),
                    "c"),
                bag,
                "背包是空的。");

            FitCardContent(top);
        }

        private void FitCardContent(float contentHeight)
        {
            if (cardView == null)
            {
                return;
            }

            cardView.Content.sizeDelta = new Vector2(
                0f,
                Mathf.Max(470f, contentHeight + 8f));

            if (cardView.Scroll != null)
            {
                cardView.Scroll.verticalNormalizedPosition = 1f;
            }
        }

        private static float AddCardSection(
            Transform content,
            Font font,
            float width,
            float top,
            string label,
            IReadOnlyList<CardInstance> cards,
            string emptyLabel)
        {
            BoardHudOverlayFactory.CreateCardSectionHeader(
                content,
                font,
                top,
                width,
                label);
            top += 32f;

            if (cards.Count == 0)
            {
                BoardHudOverlayFactory.CreateCardSectionHeader(
                    content,
                    font,
                    top,
                    width,
                    emptyLabel);
                return top + 34f;
            }

            foreach (CardInstance card in cards)
            {
                BoardHudOverlayFactory.CreateCardRow(
                    content,
                    font,
                    top,
                    width,
                    62f,
                    out Text name,
                    out Text meta,
                    out Text detail);
                name.text = card.Definition.DisplayName;
                meta.text = CardText.Meta(card.Definition);
                detail.text = CardText.Description(card.Definition);
                top += 68f;
            }

            return top + 6f;
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

            if (cardView == null)
            {
                cardView = BoardHudOverlayFactory.CreateCardDrawerView(transform, font);
                if (cardView.CloseButton != null)
                {
                    cardView.CloseButton.onClick.AddListener(ToggleCardView);
                }
            }

            if (rewardView == null)
            {
                rewardView = BoardHudOverlayFactory.CreateRewardChoiceView(transform, font);
                for (int index = 0; index < rewardView.Buttons.Length; index++)
                {
                    int captured = index;
                    rewardView.Buttons[index].onClick.AddListener(
                        () => OnRewardChosen(captured));
                }
            }

            if (shopView == null)
            {
                shopView = BoardHudOverlayFactory.CreateShopView(transform, font);
                for (int index = 0; index < shopView.Buttons.Length; index++)
                {
                    int captured = index;
                    shopView.Buttons[index].onClick.AddListener(
                        () => OnShopBuy(captured));
                }

                shopView.RerollButton.onClick.AddListener(OnShopReroll);
                shopView.LeaveButton.onClick.AddListener(OnShopLeave);
            }

            if (combatResultView == null)
            {
                combatResultView =
                    BoardHudOverlayFactory.CreateCombatResultView(transform, font);
                if (combatResultView.CloseButton != null)
                {
                    combatResultView.CloseButton.onClick.AddListener(HideCombatResult);
                }
            }

            if (cornerPanels == null)
            {
                cornerPanels = PlayerCornerPanels.Create((RectTransform)transform, font, ShowDossier);
            }

            if (dossier == null)
            {
                dossier = OpponentDossierView.Create((RectTransform)transform, font, HideDossier);
            }

            if (eventFeed == null)
            {
                eventFeed = BoardEventFeed.Create((RectTransform)transform, font);
            }

            if (fieldInfo == null)
            {
                fieldInfo = BoardFieldInfoPanel.Create((RectTransform)transform, font);
            }

            if (tileTooltip == null)
            {
                tileTooltip = BoardTileTooltip.Create((RectTransform)transform, font);
            }

            if (roundBanner == null)
            {
                roundBanner = BoardRoundBanner.Create((RectTransform)transform, font);
            }

            if (pauseOverlay == null)
            {
                pauseOverlay = PauseOverlayView.Create(
                    (RectTransform)transform,
                    font,
                    TogglePause);
            }
        }

        /// <summary>把 <see cref="GameSettings"/> 里的表现项落到实际对象上（启动、改动、自动降档后都走这里）。</summary>
        public void ApplyGameSettings()
        {
            // 已销毁（场景已卸载）时静态事件可能还压着一次回调，这里直接吞掉，
            // 否则下面的 boardGameplay?. 会因为 Unity 的"假 null"绕过去而踩到已销毁对象。
            if (this == null || !Application.isPlaying)
            {
                return;
            }

            GameSettings.EnsureLoaded();

            if (boardGameplay == null)
            {
                boardGameplay = FindObjectOfType<BoardGameplayController>();
            }

            if (cardBand != null && !autoDegraded)
            {
                cardBand.UserScale = GameSettings.CardScale;
                cardBand.ApplyScreenLayout();
            }

            if (boardGameplay == null)
            {
                return;
            }

            boardGameplay.SetPathLinesVisible(GameSettings.PathLinesVisible && !autoDegraded);
            boardGameplay.SetCameraFollow(GameSettings.CameraFollow);
        }

        private void OnGameSettingsChanged(string _)
        {
            if (this == null)
            {
                return;
            }

            ApplyGameSettings();
        }

        /// <summary>
        /// S2 自动降档：连续 3 秒平均帧率低于 35 就关掉路径线并把卡槽收到 80%，
        /// 每个进程只降一次（降了之后不再尝试恢复，避免在临界帧率上来回抖）。
        /// </summary>
        private void TickAutoQuality()
        {
            if (!Application.isPlaying || autoDegraded)
            {
                return;
            }

            GameSettings.EnsureLoaded();
            if (!GameSettings.AutoQuality)
            {
                qualityFrames = 0;
                qualityElapsed = 0f;
                return;
            }

            qualityFrames++;
            qualityElapsed += Time.unscaledDeltaTime;
            if (qualityElapsed < 3f)
            {
                return;
            }

            float fps = qualityFrames / qualityElapsed;
            qualityFrames = 0;
            qualityElapsed = 0f;
            if (fps >= 35f)
            {
                return;
            }

            autoDegraded = true;
            Debug.Log("[Board] 自动降档：平均 " + Mathf.RoundToInt(fps) + " FPS → 关闭路径线、卡槽 80%。");

            if (cardBand != null)
            {
                cardBand.UserScale = 0.8f;
                cardBand.ApplyScreenLayout();
            }

            boardGameplay?.SetPathLinesVisible(false);
        }

        /// <summary>Esc 暂停（只遮输入；内核没有暂停态，重开走重载场景）。</summary>
        public void TogglePause()
        {
            if (pauseOverlay == null || !Application.isPlaying)
            {
                return;
            }

            if (pauseOverlay.IsOpen)
            {
                pauseOverlay.Hide();
                return;
            }

            pauseOverlay.transform.SetAsLastSibling();
            if (boardGameplay != null)
            {
                pauseOverlay.SetSeed(boardGameplay.ActiveSeed, boardGameplay.UsesFixedSeed);
            }

            pauseOverlay.Show();
        }

        /// <summary>
        /// 把内核新产生的事件逐条翻译成播报。第一次调用只记游标不回溯历史，
        /// 之后每次把游标之后的 GameEvent 全部入队（由 BoardEventFeed 串行播放）。
        /// </summary>
        public void PumpEvents(MatchController match)
        {
            if (match == null || eventFeed == null)
            {
                return;
            }

            System.Collections.Generic.IReadOnlyList<GameEvent> history = match.Events.History;
            if (eventCursor < 0 || eventCursor > history.Count)
            {
                eventCursor = history.Count;
                return;
            }

            for (int i = eventCursor; i < history.Count; i++)
            {
                GameEvent gameEvent = history[i];
                string text = BoardEventText.Describe(gameEvent, match);
                if (text == null)
                {
                    continue;
                }

                eventFeed.Enqueue(text, BoardEventFeed.ColorOf(gameEvent));
            }

            eventCursor = history.Count;
        }

        /// <summary>
        /// 场地信息卡 + 轮次横幅（§2.1）：每帧刷新状态时推进来，
        /// 轮次变化时弹一次横幅，其余时间只更新文本。
        /// </summary>
        public void RefreshFieldInfo(MatchState state, string mapName)
        {
            if (state == null)
            {
                return;
            }

            if (fieldInfo != null)
            {
                fieldInfo.Refresh(state, mapName, humanPlayerId);
            }

            if (tileTooltip != null)
            {
                if (boardGameplay == null)
                {
                    boardGameplay = FindObjectOfType<BoardGameplayController>();
                }

                tileTooltip.Bind(boardGameplay);
            }

            if (roundBanner != null && state.RoundNumber != bannerRound)
            {
                bannerRound = state.RoundNumber;
                roundBanner.Show(
                    state.RoundNumber,
                    "每 " + MatchController.GlobalGrantInterval + " 轮：全员 +" +
                    MatchController.GlobalShardGrant + " 碎片");
            }
        }

        /// <summary>四角玩家栏：显示所有座位的等级、碎片、胜场与占用。</summary>
        public void RefreshPlayerPanels(MatchState state)
        {
            if (state == null)
            {
                return;
            }

            cachedState = state;
            if (cornerPanels == null)
            {
                return;
            }

            if (humanPlayerId < 0 && boardGameplay != null && boardGameplay.HumanPlayer != null)
            {
                humanPlayerId = boardGameplay.HumanPlayer.Id;
            }

            cornerPanels.Refresh(state, humanPlayerId);
            if (cardBand != null)
            {
                cornerPanels.ApplyBandTop(cardBand.CurrentBandHeight);
            }

            if (dossier != null && dossier.IsOpen)
            {
                MatchPlayerState shown = state.Players.FirstOrDefault(
                    player => player.Id == dossierPlayerId);
                if (shown != null)
                {
                    dossier.Show(shown);
                }
            }
        }

        private int dossierPlayerId = -1;

        /// <summary>对手档案（§2.7）：战斗栏按真实顺序，背包只给占用量与总价值。</summary>
        public void ShowDossier(int playerId)
        {
            if (cachedState == null || dossier == null)
            {
                return;
            }

            MatchPlayerState player = cachedState.Players.FirstOrDefault(
                candidate => candidate.Id == playerId);
            if (player == null)
            {
                return;
            }

            dossierPlayerId = playerId;
            dossier.Show(player);
        }

        public void HideDossier()
        {
            dossierPlayerId = -1;
            dossier?.Hide();
        }

        /// <summary>奖励格二选一：装不下的选项会置灰，不可放弃（R-TILE-006）。</summary>
        public void ShowRewardChoices(
            MatchPlayerState player,
            IReadOnlyList<CardDefinition> choices,
            Action<int> onChosen)
        {
            if (rewardView == null || choices == null || choices.Count == 0)
            {
                return;
            }

            rewardChoiceHandler = onChosen;
            rewardView.Title.text = string.Concat(
                "奖励 · 玩家 ",
                (player.Id + 1).ToString(),
                " · 选一张");
            rewardView.Subtitle.text = string.Concat(
                "免费获得 · 背包 ",
                player.OwnedCardWidth.ToString(),
                "/",
                LoadoutRules.BagCapacity.ToString(),
                "c · 装不下的牌会置灰（R-TILE-006、R-ECO-006）");

            for (int index = 0; index < rewardView.Buttons.Length; index++)
            {
                bool visible = index < choices.Count;
                rewardView.Buttons[index].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                CardDefinition definition = choices[index];
                bool fits = player.OwnedCardWidth + definition.SlotSize <=
                    LoadoutRules.BagCapacity;
                rewardView.Names[index].text = definition.DisplayName;
                rewardView.Metas[index].text = fits
                    ? CardText.Meta(definition)
                    : string.Concat(
                        CardText.Meta(definition),
                        "  ·  装不下（需要 ",
                        definition.SlotSize.ToString(),
                        "c）");
                rewardView.Details[index].text = CardText.Description(definition);
                rewardView.Buttons[index].interactable = fits;
                BindHover(rewardView.Buttons[index].gameObject, definition);
            }

            rewardView.Overlay.SetActive(true);
        }

        public void HideRewardChoices()
        {
            if (rewardView != null)
            {
                rewardView.Overlay.SetActive(false);
            }

            CardTooltipHost.Hide();

            rewardChoiceHandler = null;
        }

        /// <summary>商店面板：3 张货 + 重掷一次 + 离开（规则书 §2.5、R-ECO-004）。</summary>
        public void ShowShop(
            MatchPlayerState player,
            ShopSession shop,
            Action<int> onBuy,
            Action onReroll,
            Action onLeave)
        {
            if (!Application.isPlaying || player == null || shop == null)
            {
                return;
            }

            EnsureOverlayViews();
            if (shopView == null)
            {
                return;
            }

            shopBuyHandler = onBuy;
            shopRerollHandler = onReroll;
            shopLeaveHandler = onLeave;
            RenderShop(player, shop);
            shopView.Overlay.SetActive(true);
            shopView.Overlay.transform.SetAsLastSibling();
        }

        public void HideShop()
        {
            if (shopView != null)
            {
                shopView.Overlay.SetActive(false);
            }

            CardTooltipHost.Hide();
        }

        private Font HudFont =>
            drawerHandleLabel != null ? drawerHandleLabel.font : null;

        /// <summary>给商店/奖励的整行挂上悬停详情（复用同一块画布上的工具提示）。</summary>
        private void BindHover(GameObject row, CardDefinition definition)
        {
            if (row == null || definition == null)
            {
                return;
            }

            CardHoverTarget hover = row.GetComponent<CardHoverTarget>();
            if (hover == null)
            {
                hover = row.AddComponent<CardHoverTarget>();
            }

            // 商店/奖励行是整块列表行，不放大（放大只会糊住相邻行），只出详情。
            hover.Bind(definition, CardTooltipHost.CanvasRect(transform), HudFont, false);
        }

        private void RenderShop(MatchPlayerState player, ShopSession shop)
        {
            shopView.Title.text = string.Concat(
                "商店 · 玩家 ",
                (player.Id + 1).ToString(),
                " · 停留结算");
            shopView.Subtitle.text = string.Concat(
                "持有 ",
                player.Shards.ToString(),
                " 碎片 · 背包 ",
                player.OwnedCardWidth.ToString(),
                "/",
                LoadoutRules.BagCapacity.ToString(),
                "c · 价格 1c=2 / 2c=4 / 3c=8 碎片 · 每次停留刷新一次（§2.5）");

            for (int index = 0; index < shopView.Buttons.Length; index++)
            {
                bool visible = index < shop.Offers.Count;
                shopView.Buttons[index].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                ShopOffer offer = shop.Offers[index];
                CardDefinition definition = offer.Definition;
                bool fits = player.OwnedCardWidth + definition.SlotSize <=
                    LoadoutRules.BagCapacity;
                bool canPay = player.Shards >= offer.Price;
                bool purchasable = offer.IsAvailable && fits && canPay;

                shopView.Names[index].text = definition.DisplayName;
                shopView.Details[index].text = CardText.Description(definition);

                string meta = CardText.Meta(definition);
                if (offer.IsAvailable && !fits)
                {
                    meta = string.Concat(
                        meta,
                        "  ·  背包装不下（需要 ",
                        definition.SlotSize.ToString(),
                        "c）");
                }
                else if (offer.IsAvailable && !canPay)
                {
                    meta = string.Concat(meta, "  ·  碎片不足");
                }

                shopView.Metas[index].text = meta;
                shopView.Prices[index].text = offer.IsAvailable
                    ? string.Concat(offer.Price.ToString(), " 碎片")
                    : "已买下";
                shopView.Prices[index].color = purchasable
                    ? new Color(0.92f, 0.69f, 0.27f, 1f)
                    : new Color(0.6f, 0.58f, 0.54f, 1f);
                shopView.Buttons[index].interactable = purchasable;
                BindHover(shopView.Buttons[index].gameObject, definition);
            }

            bool canReroll = !shop.RerollUsed &&
                player.Shards >= MatchController.ShopRerollCost;
            shopView.RerollButton.interactable = canReroll;
            shopView.RerollLabel.text = string.Concat(
                "重掷 · ",
                MatchController.ShopRerollCost.ToString(),
                " 碎片");
            shopView.RerollHint.text = shop.RerollUsed
                ? "本次停留已经重掷过（R-ECO-004）"
                : "每次停留只能重掷 1 次";
        }

        private void OnShopBuy(int offerIndex)
        {
            shopBuyHandler?.Invoke(offerIndex);
        }

        private void OnShopReroll()
        {
            shopRerollHandler?.Invoke();
        }

        private void OnShopLeave()
        {
            shopLeaveHandler?.Invoke();
        }

        private void OnRewardChosen(int index)
        {
            Action<int> handler = rewardChoiceHandler;
            rewardChoiceHandler = null;
            handler?.Invoke(index);
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
