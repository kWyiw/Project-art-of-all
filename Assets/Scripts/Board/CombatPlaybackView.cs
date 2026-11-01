using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Combat;
using ProjectArtOfAll.Core.Events;
using ProjectArtOfAll.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 战斗回放：把 <see cref="CombatResult.Timeline"/> 按 60 tick = 1 秒重播成血条、护盾、
    /// 施放闪动与伤害跳字。纯表现层——结算早已在 CombatSimulation 里跑完，
    /// 这里只沿着事件时间轴复现一遍，所以不会影响任何对局结果。
    ///
    /// 版式（铺满全屏）：
    ///   对手生命（最上，红）→ 对手战斗栏 → 自己战斗栏 → 自己生命（最下，红）。
    /// </summary>
    public sealed class CombatPlaybackView : MonoBehaviour
    {
        private const int TicksPerSecond = CombatSimulation.TicksPerSecond;
        private const float FlashSeconds = 0.22f;
        private const float PopupSeconds = 0.8f;
        private const float Pad = 26f;
        private const float HeaderHeight = 34f;
        private const float HealthHeight = 26f;
        private const float ShieldHeight = 8f;
        private const float StatusHeight = 18f;
        private const float ControlsHeight = 44f;
        private const float BlockGap = 10f;

        private static readonly Color PanelColor = new Color(0.043f, 0.070f, 0.082f, 0.98f);
        private static readonly Color BrassColor = new Color(0.92f, 0.69f, 0.27f, 1f);
        private static readonly Color InkColor = new Color(0.93f, 0.92f, 0.86f, 1f);
        private static readonly Color MutedColor = new Color(0.66f, 0.70f, 0.68f, 1f);
        private static readonly Color HealthColor = new Color(0.82f, 0.26f, 0.22f, 1f);
        private static readonly Color HealthBack = new Color(0.16f, 0.07f, 0.06f, 1f);
        private static readonly Color ShieldColor = new Color(0.45f, 0.72f, 0.86f, 1f);
        private static readonly Color ShieldBack = new Color(0.08f, 0.12f, 0.15f, 1f);
        private static readonly Color ChipColor = new Color(0.28f, 0.19f, 0.12f, 0.96f);
        private static readonly Color ChipFlash = new Color(0.94f, 0.77f, 0.42f, 1f);
        private static readonly Color ChipDead = new Color(0.09f, 0.09f, 0.09f, 0.6f);
        private static readonly Color DamageColor = new Color(0.97f, 0.58f, 0.50f, 1f);

        private sealed class SideRuntime
        {
            public RectTransform Root;
            public RectTransform CardRow;
            public RectTransform PopupAnchor;
            public int PlayerId;
            public int MaxHealth;
            public int Health;
            public int Shield;
            public int Poison;
            public int Burn;
            public int Regeneration;
            public bool IsSelf;
            public float HealthWidth;
            public RectTransform HealthFill;
            public RectTransform ShieldFill;
            public Text HealthLabel;
            public Text ShieldLabel;
            public Text StatusLabel;
            public readonly List<Chip> Chips = new List<Chip>();
            public readonly List<GameObject> Cells = new List<GameObject>();
        }

        private sealed class Chip
        {
            public int CardUid;
            public Image Background;
            public CanvasGroup Group;
            public string CardName;
            public bool Destroyed;
            public float FlashUntil;
        }

        private readonly List<GameEvent> timeline = new List<GameEvent>();
        private SideRuntime selfSide;
        private SideRuntime foeSide;
        private Text subtitleLabel;
        private Text clockLabel;
        private Text logLabel;
        private RectTransform root;
        private RectTransform panel;
        private RectTransform content;
        private int cursor;
        private float clock;
        private float duration;
        private float speed = 2f;
        private bool skipped;
        private bool finished;
        private Action onComplete;

        public static CombatPlaybackView Create(Transform parent, Font font)
        {
            GameObject overlay = new GameObject(
                "CombatPlaybackOverlay",
                typeof(RectTransform),
                typeof(Image));
            overlay.transform.SetParent(parent, false);

            RectTransform overlayRect = (RectTransform)overlay.transform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Image scrim = overlay.GetComponent<Image>();
            scrim.color = new Color(0.01f, 0.018f, 0.022f, 0.92f);
            scrim.raycastTarget = true;

            CombatPlaybackView view = overlay.AddComponent<CombatPlaybackView>();
            view.Build(overlayRect, font);
            overlay.SetActive(false);
            return view;
        }

        public void Play(
            CombatResolvedEvent combat,
            string leftTitle,
            string rightTitle,
            bool leftIsHuman,
            Action onDone,
            float startSpeed = 2f)
        {
            if (combat == null || combat.Result == null)
            {
                onDone?.Invoke();
                return;
            }

            CombatResult result = combat.Result;
            BattleSnapshot battle = result.Battle;
            onComplete = onDone;
            if (startSpeed > 0f)
            {
                speed = startSpeed;
            }
            timeline.Clear();
            if (result.Timeline != null)
            {
                timeline.AddRange(result.Timeline
                    .OrderBy(gameEvent => gameEvent.Tick)
                    .ThenBy(gameEvent => gameEvent.Sequence));
            }

            cursor = 0;
            clock = 0f;
            skipped = false;
            finished = false;
            duration = Mathf.Max(0.5f, result.EndTick / (float)TicksPerSecond);

            // 全屏：面板直接铺满画布，尺寸从画布实时读出来。
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            ClearContent();
            BuildBody(result, battle, CanvasWidth(), CanvasHeight(), leftIsHuman, leftTitle, rightTitle);

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RefreshMeters();
            RefreshClock();
            ApplySpeed(speed);
            StartCoroutine(Run());
        }

        private float CanvasWidth()
        {
            float width = root != null && root.rect.width > 1f ? root.rect.width : Screen.width;
            return Mathf.Max(720f, width);
        }

        private float CanvasHeight()
        {
            float height = root != null && root.rect.height > 1f ? root.rect.height : Screen.height;
            return Mathf.Max(480f, height);
        }

        private void Build(RectTransform overlay, Font font)
        {
            root = overlay;
            GameObject panelObject = new GameObject(
                "CombatPlaybackPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(overlay, false);

            panel = (RectTransform)panelObject.transform;
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;

            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.70f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            BoardHudOverlayFactory.CreateBar(
                panel,
                "BrassRule",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 5f),
                new Vector2(0f, 0f),
                BrassColor);

            content = MakeRect(panel, "Content", Vector2.zero, new Vector2(1000f, 600f));
            Stretch(content);
        }

        private void ClearContent()
        {
            if (content == null)
            {
                return;
            }

            List<GameObject> stale = new List<GameObject>();
            foreach (Transform child in content)
            {
                stale.Add(child.gameObject);
            }

            foreach (GameObject gameObject in stale)
            {
                Destroy(gameObject);
            }
        }

        private void BuildBody(
            CombatResult result,
            BattleSnapshot battle,
            float panelWidth,
            float panelHeight,
            bool leftIsHuman,
            string leftTitle,
            string rightTitle)
        {
            Font font = BoardHudOverlayFactory.CreateFont();
            float innerWidth = panelWidth - Pad * 2f;

            float sideBlock = HealthHeight + ShieldHeight + StatusHeight + BlockGap * 2f;
            float rows = panelHeight
                - Pad * 2f
                - HeaderHeight
                - sideBlock * 2f
                - ControlsHeight
                - BlockGap * 3f;
            float rowHeight = Mathf.Max(90f, rows * 0.5f);

            // ---- 顶部：标题 + 计时 ----
            subtitleLabel = BoardHudOverlayFactory.CreateText(
                content,
                "Subtitle",
                string.Concat(leftTitle, "　对　", rightTitle),
                font,
                24,
                FontStyle.Bold,
                new Vector2(Pad, 0f),
                new Vector2(innerWidth * 0.62f, HeaderHeight),
                TextAnchor.UpperLeft);
            AnchorTopLeft(subtitleLabel.rectTransform, new Vector2(Pad, -4f));

            clockLabel = BoardHudOverlayFactory.CreateText(
                content,
                "Clock",
                "0.0s / 0.0s",
                font,
                22,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(innerWidth * 0.38f, HeaderHeight),
                TextAnchor.UpperRight);
            AnchorTopRight(clockLabel.rectTransform, new Vector2(-Pad, -4f));
            clockLabel.color = BrassColor;

            float top = -(HeaderHeight + BlockGap);

            // ---- 上半：对手（生命在最上，战斗栏在下）----
            int leftId = battle != null ? battle.Left.PlayerId : result.WinnerPlayerId;
            int rightId = battle != null ? battle.Right.PlayerId : -1;
            CombatantSnapshot leftSnapshot = battle?.Left;
            CombatantSnapshot rightSnapshot = battle?.Right;
            CombatantSnapshot selfSnapshot = leftIsHuman ? leftSnapshot : rightSnapshot;
            CombatantSnapshot foeSnapshot = leftIsHuman ? rightSnapshot : leftSnapshot;
            int selfId = leftIsHuman ? leftId : rightId;
            int foeId = leftIsHuman ? rightId : leftId;

            int selfMaxHealth = selfSnapshot == null ? 100 : selfSnapshot.MaxHealth;
            int foeMaxHealth = foeSnapshot == null ? 100 : foeSnapshot.MaxHealth;

            foeSide = BuildSide(
                font,
                innerWidth,
                rowHeight,
                false,
                foeId,
                foeSnapshot,
                foeMaxHealth,
                out float foeHeight);
            AnchorTopLeft(foeSide.Root, new Vector2(Pad, top));
            top -= foeHeight + BlockGap;

            // ---- 下半：自己（战斗栏在上，生命在最下）----
            selfSide = BuildSide(
                font,
                innerWidth,
                rowHeight,
                true,
                selfId,
                selfSnapshot,
                selfMaxHealth,
                out float selfHeight);
            float selfTop = -panelHeight + Pad + ControlsHeight + BlockGap + selfHeight;
            AnchorTopLeft(selfSide.Root, new Vector2(Pad, selfTop));

            // ---- 底部：日志 + 速度 ----
            logLabel = BoardHudOverlayFactory.CreateText(
                content,
                "Log",
                "战斗开始",
                font,
                18,
                FontStyle.Normal,
                Vector2.zero,
                new Vector2(innerWidth - 420f, ControlsHeight),
                TextAnchor.MiddleLeft);
            AnchorBottomLeft(logLabel.rectTransform, new Vector2(Pad, Pad * 0.5f));
            logLabel.color = new Color(0.86f, 0.80f, 0.62f, 1f);

            const float buttonWidth = 78f;
            float skipWidth = 150f;
            float startX = -Pad - skipWidth - BlockGap - buttonWidth * 3f - BlockGap;
            CreateSpeedButton(font, "1x", 1f, startX);
            CreateSpeedButton(font, "2x", 2f, startX + buttonWidth + 6f);
            CreateSpeedButton(font, "4x", 4f, startX + (buttonWidth + 6f) * 2f);

            Button skip = MakeButton(
                content,
                "Skip",
                "跳过",
                font,
                18,
                Vector2.zero,
                new Vector2(skipWidth, ControlsHeight - 6f),
                () => skipped = true);
            AnchorBottomRight((RectTransform)skip.transform, new Vector2(-Pad, Pad * 0.5f));
        }

        private SideRuntime BuildSide(
            Font font,
            float innerWidth,
            float rowHeight,
            bool isSelf,
            int playerId,
            CombatantSnapshot snapshot,
            int maxHealth,
            out float height)
        {
            SideRuntime side = new SideRuntime
            {
                PlayerId = playerId,
                MaxHealth = maxHealth,
                Health = maxHealth,
                Shield = 0,
                IsSelf = isSelf,
                HealthWidth = innerWidth
            };

            RectTransform rootRect = MakeRect(
                content,
                isSelf ? "SelfSide" : "FoeSide",
                Vector2.zero,
                new Vector2(innerWidth, 200f));
            side.Root = rootRect;

            float used = 0f;
            if (isSelf)
            {
                // 自己：战斗栏在上，生命在最下。
                RectTransform row = MakeRect(
                    rootRect,
                    "Cards",
                    Vector2.zero,
                    new Vector2(innerWidth, rowHeight));
                AnchorTopLeft(row, Vector2.zero);
                side.CardRow = row;
                used += rowHeight + BlockGap;

                side.StatusLabel = BoardHudOverlayFactory.CreateText(
                    rootRect,
                    "Status",
                    string.Empty,
                    font,
                    16,
                    FontStyle.Normal,
                    Vector2.zero,
                    new Vector2(innerWidth, StatusHeight),
                    TextAnchor.UpperLeft);
                AnchorTopLeft(side.StatusLabel.rectTransform, new Vector2(2f, -used));
                side.StatusLabel.color = new Color(0.80f, 0.74f, 0.60f, 1f);
                used += StatusHeight;

                RectTransform shield = MakeRect(
                    rootRect,
                    "ShieldBar",
                    Vector2.zero,
                    new Vector2(innerWidth, ShieldHeight));
                AnchorTopLeft(shield, new Vector2(0f, -used));
                MakeFill(shield, "Back", ShieldBack, innerWidth);
                side.ShieldFill = MakeFill(shield, "Fill", ShieldColor, innerWidth);
                used += ShieldHeight + 3f;

                RectTransform health = MakeRect(
                    rootRect,
                    "HealthBar",
                    Vector2.zero,
                    new Vector2(innerWidth, HealthHeight));
                AnchorTopLeft(health, new Vector2(0f, -used));
                MakeFill(health, "Back", HealthBack, innerWidth);
                side.HealthFill = MakeFill(health, "Fill", HealthColor, innerWidth);
                side.HealthLabel = BoardHudOverlayFactory.CreateText(
                    health,
                    "HealthLabel",
                    string.Empty,
                    font,
                    20,
                    FontStyle.Bold,
                    Vector2.zero,
                    new Vector2(innerWidth - 16f, HealthHeight),
                    TextAnchor.MiddleCenter);
                Stretch(side.HealthLabel.rectTransform);
                side.HealthLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
                side.HealthLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
                side.HealthLabel.color = InkColor;
                used += HealthHeight;
            }
            else
            {
                // 对手：生命在最上，战斗栏在下。
                RectTransform health = MakeRect(
                    rootRect,
                    "HealthBar",
                    Vector2.zero,
                    new Vector2(innerWidth, HealthHeight));
                AnchorTopLeft(health, Vector2.zero);
                MakeFill(health, "Back", HealthBack, innerWidth);
                side.HealthFill = MakeFill(health, "Fill", HealthColor, innerWidth);
                side.HealthLabel = BoardHudOverlayFactory.CreateText(
                    health,
                    "HealthLabel",
                    string.Empty,
                    font,
                    20,
                    FontStyle.Bold,
                    Vector2.zero,
                    new Vector2(innerWidth - 16f, HealthHeight),
                    TextAnchor.MiddleCenter);
                Stretch(side.HealthLabel.rectTransform);
                side.HealthLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
                side.HealthLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
                side.HealthLabel.color = InkColor;
                used += HealthHeight + 3f;

                RectTransform shield = MakeRect(
                    rootRect,
                    "ShieldBar",
                    Vector2.zero,
                    new Vector2(innerWidth, ShieldHeight));
                AnchorTopLeft(shield, new Vector2(0f, -used));
                MakeFill(shield, "Back", ShieldBack, innerWidth);
                side.ShieldFill = MakeFill(shield, "Fill", ShieldColor, innerWidth);
                used += ShieldHeight;

                side.StatusLabel = BoardHudOverlayFactory.CreateText(
                    rootRect,
                    "Status",
                    string.Empty,
                    font,
                    16,
                    FontStyle.Normal,
                    Vector2.zero,
                    new Vector2(innerWidth, StatusHeight),
                    TextAnchor.UpperLeft);
                AnchorTopLeft(side.StatusLabel.rectTransform, new Vector2(2f, -used));
                side.StatusLabel.color = new Color(0.80f, 0.74f, 0.60f, 1f);
                used += StatusHeight;

                RectTransform row = MakeRect(
                    rootRect,
                    "Cards",
                    Vector2.zero,
                    new Vector2(innerWidth, rowHeight));
                AnchorTopLeft(row, new Vector2(0f, -used));
                side.CardRow = row;
                used += rowHeight;
            }

            BuildChips(side, snapshot, innerWidth, rowHeight);

            height = used + 2f;
            rootRect.sizeDelta = new Vector2(innerWidth, height);

            RectTransform popup = MakeRect(
                rootRect,
                "PopupAnchor",
                Vector2.zero,
                new Vector2(innerWidth, height));
            Stretch(popup);
            side.PopupAnchor = popup;

            side.ShieldLabel = BoardHudOverlayFactory.CreateText(
                rootRect,
                "ShieldLabel",
                string.Empty,
                font,
                14,
                FontStyle.Normal,
                Vector2.zero,
                new Vector2(innerWidth, ShieldHeight + 4f),
                TextAnchor.UpperLeft);

            // 护盾读数挂在盾条左侧，避免压住血条上的字。
            if (isSelf)
            {
                AnchorBottomLeft(side.ShieldLabel.rectTransform, new Vector2(6f, HealthHeight + ShieldHeight + 2f));
            }
            else
            {
                AnchorTopLeft(side.ShieldLabel.rectTransform, new Vector2(6f, -(HealthHeight + 1f)));
            }

            side.ShieldLabel.color = ShieldColor;
            return side;
        }

        /// <summary>把这一侧的战斗栏画成一排小卡，横向居中；牌多时自动缩小。</summary>
        private void BuildChips(
            SideRuntime side,
            CombatantSnapshot snapshot,
            float innerWidth,
            float rowHeight)
        {
            Font font = BoardHudOverlayFactory.CreateFont();
            if (snapshot == null || snapshot.BattleCards.Count == 0)
            {
                Text empty = BoardHudOverlayFactory.CreateText(
                    side.CardRow,
                    "Empty",
                    "空战斗栏",
                    font,
                    18,
                    FontStyle.Normal,
                    Vector2.zero,
                    new Vector2(innerWidth, rowHeight),
                    TextAnchor.MiddleCenter);
                Stretch(empty.rectTransform);
                empty.color = MutedColor;
                return;
            }

            List<CardInstance> cards = snapshot.BattleCards.ToList();
            int capacity = snapshot.Level >= 6
                ? 10
                : LoadoutRules.GetBattleCapacity(snapshot.Level);
            CardRowRenderer renderer = new CardRowRenderer(BoardHudOverlayFactory.CreateFont())
            {
                RowWidth = innerWidth,
                InBattle = true
            };
            renderer.Scale = renderer.FitScale(Mathf.Max(90f, rowHeight - 8f));
            renderer.TopInset = Mathf.Max(0f, (rowHeight - renderer.CardHeight) * 0.5f);
            renderer.BuildCells(side.CardRow, side.Cells, Mathf.Max(capacity, cards.Count));
            float startX = CardRowRenderer.RowInset;

            for (int index = 0; index < cards.Count; index++)
            {
                CardInstance card = cards[index];
                float width = renderer.WidthOf(card);
                GameObject widget = renderer.AddCard(
                    side.CardRow,
                    card,
                    startX,
                    width,
                    CardRowRenderer.BonusBadge(cards, index));
                startX += width + renderer.SlotGap;

                side.Chips.Add(new Chip
                {
                    CardUid = card.Uid,
                    Background = widget.GetComponent<Image>(),
                    Group = widget.GetComponent<CanvasGroup>(),
                    CardName = card.Definition.DisplayName
                });
            }
        }

        private static Color FrameColor(int slotSize)
        {
            switch (slotSize)
            {
                case 1:
                    return new Color(0.72f, 0.55f, 0.30f, 1f);
                case 2:
                    return new Color(0.48f, 0.66f, 0.70f, 1f);
                default:
                    return new Color(0.93f, 0.71f, 0.28f, 1f);
            }
        }

        private IEnumerator Run()
        {
            while (!finished)
            {
                if (!skipped)
                {
                    clock += Time.unscaledDeltaTime * speed;
                }

                if (clock >= duration || skipped)
                {
                    clock = duration;
                    while (cursor < timeline.Count)
                    {
                        Apply(timeline[cursor++]);
                    }

                    finished = true;
                    break;
                }

                while (cursor < timeline.Count && TickSeconds(timeline[cursor]) <= clock)
                {
                    Apply(timeline[cursor++]);
                }

                RefreshMeters();
                RefreshClock();
                RefreshChips();
                yield return null;
            }

            RefreshMeters();
            RefreshClock();
            RefreshChips();
            logLabel.text = string.Concat("战斗结束 · ", duration.ToString("0.0"), " 秒");
            yield return new WaitForSecondsRealtime(0.85f);
            gameObject.SetActive(false);
            Action callback = onComplete;
            onComplete = null;
            callback?.Invoke();
        }

        private void Update()
        {
            if (finished)
            {
                RefreshChips();
            }
        }

        private void Apply(GameEvent gameEvent)
        {
            if (gameEvent is CardCastEvent cast)
            {
                SideRuntime side = SideOf(cast.PlayerId);
                if (side != null)
                {
                    Flash(side, cast.CardUid);
                    logLabel.text = string.Concat(
                        SidePrefix(side),
                        " 施放 ",
                        CardNameOf(side, cast.CardUid));
                }

                return;
            }

            if (gameEvent is DamageDealtEvent damage)
            {
                SideRuntime target = SideOf(damage.TargetPlayerId);
                if (target == null)
                {
                    return;
                }

                target.Shield = Mathf.Max(0, target.Shield - damage.ShieldAbsorbed);
                int healthDamage = Mathf.Max(0, damage.Amount - damage.ShieldAbsorbed);
                target.Health = Mathf.Max(0, target.Health - healthDamage);
                SpawnPopup(target, string.Concat("-", damage.Amount.ToString()), DamageColor);
                logLabel.text = string.Concat(
                    SidePrefix(SideOf(damage.SourcePlayerId) ?? target),
                    " → ",
                    SidePrefix(target),
                    "　",
                    damage.Amount.ToString(),
                    " 伤害",
                    damage.ShieldAbsorbed > 0
                        ? string.Concat("（护盾吸 ", damage.ShieldAbsorbed.ToString(), "）")
                        : string.Empty,
                    damage.IgnoresShield ? "（无视护盾）" : string.Empty);
                RefreshMeters();
                return;
            }

            if (gameEvent is ShieldGainedEvent shieldGain)
            {
                SideRuntime side = SideOf(shieldGain.PlayerId);
                if (side == null)
                {
                    return;
                }

                side.Shield += shieldGain.Amount;
                SpawnPopup(side, string.Concat("+", shieldGain.Amount.ToString(), " 盾"), ShieldColor);
                logLabel.text = string.Concat(
                    SidePrefix(side),
                    " 获得 ",
                    shieldGain.Amount.ToString(),
                    " 点护盾");
                RefreshMeters();
                return;
            }

            if (gameEvent is StatusAppliedEvent status)
            {
                SideRuntime side = SideOf(status.PlayerId);
                if (side == null)
                {
                    return;
                }

                switch (status.Status)
                {
                    case StatusKind.Poison:
                        side.Poison = status.Layers;
                        break;
                    case StatusKind.Burn:
                        side.Burn = status.Layers;
                        break;
                    case StatusKind.Regeneration:
                        side.Regeneration = status.Layers;
                        break;
                }

                logLabel.text = string.Concat(
                    SidePrefix(side),
                    " ",
                    StatusName(status.Status),
                    " ",
                    status.Layers.ToString());
                RefreshMeters();
                return;
            }

            if (gameEvent is CardDestroyedEvent destroyed)
            {
                SideRuntime side = SideOf(destroyed.PlayerId);
                if (side == null)
                {
                    return;
                }

                foreach (Chip chip in side.Chips)
                {
                    if (chip.CardUid != destroyed.CardUid)
                    {
                        continue;
                    }

                    chip.Destroyed = true;
                    if (chip.Group != null)
                    {
                        chip.Group.alpha = 0.32f;
                    }
                }

                logLabel.text = string.Concat(
                    SidePrefix(side),
                    " 的 ",
                    CardNameOf(side, destroyed.CardUid),
                    " 被摧毁");
                return;
            }

            if (gameEvent is ChargeAppliedEvent charge)
            {
                SideRuntime side = SideOf(charge.PlayerId);
                if (side == null)
                {
                    return;
                }

                Flash(side, charge.CardUid);
                logLabel.text = string.Concat(
                    SidePrefix(side),
                    " 的 ",
                    CardNameOf(side, charge.CardUid),
                    " 充能 ",
                    (charge.Ticks / (float)TicksPerSecond).ToString("0.0"),
                    " 秒");
                return;
            }

            if (gameEvent is HasteAppliedEvent haste)
            {
                SideRuntime side = SideOf(haste.PlayerId);
                if (side == null)
                {
                    return;
                }

                Flash(side, haste.CardUid);
                logLabel.text = string.Concat(
                    SidePrefix(side),
                    " 的 ",
                    CardNameOf(side, haste.CardUid),
                    " 加速 ",
                    (haste.DurationTicks / (float)TicksPerSecond).ToString("0.0"),
                    " 秒");
                return;
            }

            if (gameEvent is SlowAppliedEvent slow)
            {
                SideRuntime side = SideOf(slow.PlayerId);
                if (side == null)
                {
                    return;
                }

                Flash(side, slow.CardUid);
                logLabel.text = string.Concat(
                    SidePrefix(side),
                    " 的 ",
                    CardNameOf(side, slow.CardUid),
                    " 被缓速 ",
                    (slow.DurationTicks / (float)TicksPerSecond).ToString("0.0"),
                    " 秒");
            }
        }

        private void RefreshMeters()
        {
            RefreshMeter(selfSide);
            RefreshMeter(foeSide);
        }

        private void RefreshMeter(SideRuntime side)
        {
            if (side == null || side.HealthFill == null)
            {
                return;
            }

            float healthFraction = side.MaxHealth <= 0
                ? 0f
                : Mathf.Clamp01(side.Health / (float)side.MaxHealth);
            side.HealthFill.sizeDelta = new Vector2(side.HealthWidth * healthFraction, 0f);

            string tag = side.IsSelf ? "自己" : "对手";
            side.HealthLabel.text = string.Concat(
                tag,
                "　生命 ",
                side.Health.ToString(),
                " / ",
                side.MaxHealth.ToString());

            float shieldFraction = Mathf.Clamp01(
                side.Shield / (float)Mathf.Max(1, side.MaxHealth));
            side.ShieldFill.sizeDelta = new Vector2(side.HealthWidth * shieldFraction, 0f);

            List<string> statuses = new List<string>();
            if (side.Shield > 0)
            {
                statuses.Add(string.Concat("护盾 ", side.Shield.ToString()));
            }

            if (side.Poison > 0)
            {
                statuses.Add(string.Concat("中毒 ", side.Poison.ToString()));
            }

            if (side.Burn > 0)
            {
                statuses.Add(string.Concat("灼烧 ", side.Burn.ToString()));
            }

            if (side.Regeneration > 0)
            {
                statuses.Add(string.Concat("再生 ", side.Regeneration.ToString()));
            }

            side.StatusLabel.text = string.Join("   ", statuses);
            if (side.ShieldLabel != null)
            {
                side.ShieldLabel.text = side.Shield > 0
                    ? string.Concat("护盾 ", side.Shield.ToString())
                    : string.Empty;
            }
        }

        private void RefreshClock()
        {
            clockLabel.text = string.Concat(
                Mathf.Min(clock, duration).ToString("0.0"),
                "s / ",
                duration.ToString("0.0"),
                "s　x",
                speed.ToString("0"));
        }

        private void RefreshChips()
        {
            RefreshSideChips(selfSide);
            RefreshSideChips(foeSide);
        }

        private void RefreshSideChips(SideRuntime side)
        {
            if (side == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            foreach (Chip chip in side.Chips)
            {
                if (chip.Background == null)
                {
                    continue;
                }

                if (chip.Destroyed)
                {
                    if (chip.Group != null)
                    {
                        chip.Group.alpha = 0.32f;
                    }

                    chip.Background.color = ChipDead;
                    continue;
                }

                if (chip.Group != null)
                {
                    chip.Group.alpha = 1f;
                }

                chip.Background.color = now < chip.FlashUntil
                    ? ChipFlash
                    : CardRowRenderer.BattleColor;
            }
        }

        private void Flash(SideRuntime side, int cardUid)
        {
            foreach (Chip chip in side.Chips)
            {
                if (chip.CardUid == cardUid)
                {
                    chip.FlashUntil = Time.unscaledTime + FlashSeconds;
                    return;
                }
            }
        }

        private string CardNameOf(SideRuntime side, int cardUid)
        {
            foreach (Chip chip in side.Chips)
            {
                if (chip.CardUid == cardUid)
                {
                    return string.IsNullOrEmpty(chip.CardName) ? "卡牌" : chip.CardName;
                }
            }

            return "卡牌";
        }

        private void SpawnPopup(SideRuntime side, string content, Color color)
        {
            if (side == null || side.PopupAnchor == null)
            {
                return;
            }

            Text popup = BoardHudOverlayFactory.CreateText(
                side.PopupAnchor,
                "Popup",
                content,
                BoardHudOverlayFactory.CreateFont(),
                34,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(260f, 48f),
                TextAnchor.MiddleCenter);
            popup.color = color;
            popup.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            popup.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            popup.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            popup.rectTransform.anchoredPosition = new Vector2(
                UnityEngine.Random.Range(-90f, 90f),
                UnityEngine.Random.Range(-10f, 10f));
            StartCoroutine(FloatPopup(popup));
        }

        private IEnumerator FloatPopup(Text popup)
        {
            RectTransform rect = popup.rectTransform;
            Vector2 start = rect.anchoredPosition;
            float elapsed = 0f;
            Color color = popup.color;
            while (elapsed < PopupSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / PopupSeconds);
                rect.anchoredPosition = start + new Vector2(0f, 46f * t);
                color.a = 1f - t;
                if (popup != null)
                {
                    popup.color = color;
                }

                yield return null;
            }

            if (popup != null)
            {
                Destroy(popup.gameObject);
            }
        }

        private SideRuntime SideOf(int playerId)
        {
            if (selfSide != null && selfSide.PlayerId == playerId)
            {
                return selfSide;
            }

            if (foeSide != null && foeSide.PlayerId == playerId)
            {
                return foeSide;
            }

            return null;
        }

        private string SidePrefix(SideRuntime side)
        {
            if (side == null)
            {
                return "?";
            }

            return side.IsSelf ? "我方" : "对手";
        }

        private static string StatusName(StatusKind status)
        {
            switch (status)
            {
                case StatusKind.Poison:
                    return "中毒";
                case StatusKind.Burn:
                    return "灼烧";
                case StatusKind.Regeneration:
                    return "再生";
                default:
                    return "护盾";
            }
        }

        private static float TickSeconds(GameEvent gameEvent)
        {
            return gameEvent.Tick / (float)TicksPerSecond;
        }

        private void CreateSpeedButton(Font font, string label, float value, float xFromRight)
        {
            Button button = MakeButton(
                content,
                "Speed" + value.ToString("0"),
                label,
                font,
                18,
                Vector2.zero,
                new Vector2(78f, ControlsHeight - 6f),
                () => ApplySpeed(value));
            AnchorBottomRight((RectTransform)button.transform, new Vector2(xFromRight, Pad * 0.5f));
        }

        private void ApplySpeed(float value)
        {
            speed = value;
            foreach (Button button in content.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "Skip")
                {
                    continue;
                }

                Image image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = button.name == "Speed" + value.ToString("0")
                        ? new Color(0.36f, 0.27f, 0.13f, 1f)
                        : new Color(0.16f, 0.18f, 0.18f, 1f);
                }
            }
        }

        private static RectTransform MakeRect(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)gameObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = gameObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform MakeFill(
            Transform parent,
            string name,
            Color color,
            float width)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)gameObject.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 0f);

            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private Button MakeButton(
            Transform parent,
            string name,
            string label,
            Font font,
            int fontSize,
            Vector2 position,
            Vector2 size,
            Action onClick)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            gameObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)gameObject.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = gameObject.GetComponent<Image>();
            image.color = new Color(0.16f, 0.18f, 0.18f, 1f);

            Button button = gameObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick?.Invoke());

            Text text = BoardHudOverlayFactory.CreateText(
                gameObject.transform,
                "Label",
                label,
                font,
                fontSize,
                FontStyle.Bold,
                Vector2.zero,
                size,
                TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AnchorTopLeft(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
        }

        private static void AnchorBottomLeft(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = position;
        }

        private static void AnchorBottomRight(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = position;
        }
    }
}
