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
    /// </summary>
    public sealed class CombatPlaybackView : MonoBehaviour
    {
        private const int TicksPerSecond = CombatSimulation.TicksPerSecond;
        private const float PanelWidth = 1080f;
        private const float PanelHeight = 620f;
        private const float SideWidth = 470f;
        private const float ChipWidth = 82f;
        private const float ChipPitch = 88f;
        private const float ChipHeight = 112f;
        private const float FlashSeconds = 0.22f;
        private const float PopupSeconds = 0.8f;

        private static readonly Color PanelColor = new Color(0.043f, 0.070f, 0.082f, 0.99f);
        private static readonly Color BrassColor = new Color(0.92f, 0.69f, 0.27f, 1f);
        private static readonly Color InkColor = new Color(0.93f, 0.92f, 0.86f, 1f);
        private static readonly Color MutedColor = new Color(0.66f, 0.70f, 0.68f, 1f);
        private static readonly Color HealthColor = new Color(0.42f, 0.79f, 0.46f, 1f);
        private static readonly Color ShieldColor = new Color(0.45f, 0.72f, 0.86f, 1f);
        private static readonly Color ChipColor = new Color(0.28f, 0.19f, 0.12f, 0.96f);
        private static readonly Color ChipFlash = new Color(0.94f, 0.77f, 0.42f, 1f);
        private static readonly Color ChipDead = new Color(0.09f, 0.09f, 0.09f, 0.6f);
        private static readonly Color DamageColor = new Color(0.95f, 0.55f, 0.47f, 1f);
        private static readonly Color HealColor = new Color(0.55f, 0.88f, 0.60f, 1f);

        private sealed class SideRuntime
        {
            public RectTransform Root;
            public int PlayerId;
            public int MaxHealth;
            public int Health;
            public int Shield;
            public int Poison;
            public int Burn;
            public int Regeneration;
            public RectTransform HealthFill;
            public RectTransform ShieldFill;
            public Text HealthLabel;
            public Text ShieldLabel;
            public Text StatusLabel;
            public RectTransform CardRow;
            public readonly List<Chip> Chips = new List<Chip>();
        }

        private sealed class Chip
        {
            public int CardUid;
            public Image Background;
            public Text Label;
            public bool Destroyed;
            public float FlashUntil;
        }

        private readonly List<GameEvent> timeline = new List<GameEvent>();
        private SideRuntime leftSide;
        private SideRuntime rightSide;
        private Text subtitleLabel;
        private Text clockLabel;
        private Text logLabel;
        private RectTransform root;
        private RectTransform popupLayer;
        private Button fastButton;
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
            scrim.color = new Color(0.01f, 0.018f, 0.022f, 0.90f);
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
            Action onDone)
        {
            if (combat == null || combat.Result == null)
            {
                onDone?.Invoke();
                return;
            }

            CombatResult result = combat.Result;
            BattleSnapshot battle = result.Battle;
            onComplete = onDone;
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
            speed = 2f;
            duration = Mathf.Max(0.5f, result.EndTick / (float)TicksPerSecond);

            ClearBuildSide(leftSide);
            ClearBuildSide(rightSide);
            ClearPopups();

            leftSide = BuildSide(
                root,
                true,
                combat.AttackerId,
                battle?.Left,
                battle == null ? 100 : Core.Combat.BattleConfig.GetMaxHealth(battle.Left.Level));
            rightSide = BuildSide(
                root,
                false,
                combat.DefenderId,
                battle?.Right,
                battle == null ? 100 : Core.Combat.BattleConfig.GetMaxHealth(battle.Right.Level));

            subtitleLabel.text = string.Concat(
                leftTitle,
                "　对　",
                rightTitle,
                "　·　",
                result.EndSecond.ToString(),
                " 秒");
            logLabel.text = "战斗开始";
            RefreshMeters();
            RefreshClock();
            ApplySpeed(speed);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            StartCoroutine(Run());
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
            logLabel.text = string.Concat(
                "战斗结束 · ",
                (duration).ToString("0.0"),
                " 秒");
            yield return new WaitForSecondsRealtime(0.85f);
            gameObject.SetActive(false);
            Action callback = onComplete;
            onComplete = null;
            callback?.Invoke();
        }

        private void Update()
        {
            if (!finished)
            {
                return;
            }

            RefreshChips();
        }

        private void Apply(GameEvent gameEvent)
        {
            if (gameEvent is CardCastEvent cast)
            {
                SideRuntime side = SideOf(cast.PlayerId);
                if (side != null)
                {
                    Flash(side, cast.CardUid);
                    string cardName = CardNameOf(side, cast.CardUid);
                    logLabel.text = string.Concat(
                        SidePrefix(side),
                        " 施放 ",
                        cardName);
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
                int total = damage.Amount;
                SpawnPopup(
                    target,
                    string.Concat("-", total.ToString()),
                    DamageColor,
                    SideOf(damage.TargetPlayerId) == leftSide ? 1f : -1f);
                logLabel.text = string.Concat(
                    SidePrefix(SideOf(damage.SourcePlayerId) ?? target),
                    " → ",
                    SidePrefix(target),
                    "　",
                    total.ToString(),
                    " 伤害",
                    damage.ShieldAbsorbed > 0
                        ? string.Concat("（护盾吸 ", damage.ShieldAbsorbed.ToString(), "）")
                        : string.Empty,
                    damage.IgnoresShield ? "（无视护盾）" : string.Empty);
                RefreshMeters();
                return;
            }

            if (gameEvent is ShieldGainedEvent shield)
            {
                SideRuntime side = SideOf(shield.PlayerId);
                if (side == null)
                {
                    return;
                }

                side.Shield += shield.Amount;
                SpawnPopup(
                    side,
                    string.Concat("+", shield.Amount.ToString(), " 盾"),
                    ShieldColor,
                    side == leftSide ? 1f : -1f);
                logLabel.text = string.Concat(
                    SidePrefix(side),
                    " 获得 ",
                    shield.Amount.ToString(),
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
                    chip.Label.color = MutedColor;
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

        private void Build(RectTransform overlay, Font font)
        {
            root = overlay;
            GameObject panelObject = new GameObject(
                "CombatPlaybackPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(overlay, false);

            RectTransform panel = (RectTransform)panelObject.transform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

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
                new Vector2(0f, 4f),
                new Vector2(0f, 0f),
                BrassColor);

            BoardHudOverlayFactory.CreateText(
                panel,
                "Title",
                "战斗",
                font,
                30,
                FontStyle.Bold,
                new Vector2(36f, -18f),
                new Vector2(220f, 40f),
                TextAnchor.UpperLeft);

            subtitleLabel = BoardHudOverlayFactory.CreateText(
                panel,
                "Subtitle",
                string.Empty,
                font,
                16,
                FontStyle.Normal,
                new Vector2(36f, -62f),
                new Vector2(760f, 26f),
                TextAnchor.UpperLeft);
            subtitleLabel.color = MutedColor;

            clockLabel = BoardHudOverlayFactory.CreateText(
                panel,
                "Clock",
                "0.0s / 0.0s",
                font,
                20,
                FontStyle.Bold,
                new Vector2(-36f, -24f),
                new Vector2(300f, 30f),
                TextAnchor.UpperRight);
            AnchorTopRight(clockLabel.rectTransform, new Vector2(-36f, -24f));
            clockLabel.color = BrassColor;

            logLabel = BoardHudOverlayFactory.CreateText(
                panel,
                "Log",
                "战斗开始",
                font,
                20,
                FontStyle.Normal,
                Vector2.zero,
                new Vector2(900f, 34f),
                TextAnchor.MiddleCenter);
            RectTransform logRect = logLabel.rectTransform;
            logRect.anchorMin = new Vector2(0.5f, 0f);
            logRect.anchorMax = new Vector2(0.5f, 0f);
            logRect.pivot = new Vector2(0.5f, 0f);
            logRect.anchoredPosition = new Vector2(0f, 88f);
            logRect.sizeDelta = new Vector2(900f, 34f);
            logLabel.color = new Color(0.86f, 0.80f, 0.62f, 1f);

            popupLayer = MakeRect(panel, "Popups", Vector2.zero, new Vector2(PanelWidth, PanelHeight));

            CreateSpeedButton(panel, font, "速度 1x", 1f, 36f);
            CreateSpeedButton(panel, font, "速度 2x", 2f, 146f);
            fastButton = CreateSpeedButton(panel, font, "速度 4x", 4f, 256f);

            Button skip = MakeButton(
                panel,
                "Skip",
                "跳过",
                font,
                18,
                new Vector2(-36f, 26f),
                new Vector2(132f, 38f),
                () => skipped = true);
            AnchorBottomRight((RectTransform)skip.transform, new Vector2(-36f, 26f));
        }

        private Button CreateSpeedButton(
            RectTransform panel,
            Font font,
            string label,
            float value,
            float x)
        {
            Button button = MakeButton(
                panel,
                "Speed" + value.ToString("0"),
                label,
                font,
                18,
                new Vector2(x, 26f),
                new Vector2(96f, 38f),
                () => ApplySpeed(value));
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(x, 26f);
            return button;
        }

        private void ApplySpeed(float value)
        {
            speed = value;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
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

        private SideRuntime BuildSide(
            RectTransform panel,
            bool left,
            int playerId,
            CombatantSnapshot snapshot,
            int maxHealth)
        {
            SideRuntime side = new SideRuntime
            {
                PlayerId = playerId,
                MaxHealth = maxHealth,
                Health = maxHealth,
                Shield = 0
            };

            RectTransform container = MakeRect(
                panel,
                left ? "LeftSide" : "RightSide",
                Vector2.zero,
                new Vector2(SideWidth, 400f));
            container.anchorMin = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            container.anchorMax = container.anchorMin;
            container.pivot = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            container.anchoredPosition = new Vector2(left ? 36f : -36f, -104f);
            container.sizeDelta = new Vector2(SideWidth, 400f);
            side.Root = container;

            RectTransform head = MakeRect(container, "Head", Vector2.zero, new Vector2(SideWidth, 24f));
            AnchorTopLeft(head, new Vector2(0f, 0f));

            Text nameLabel = BoardHudOverlayFactory.CreateText(
                container,
                "Name",
                left ? "左侧" : "右侧",
                BoardHudOverlayFactory.CreateFont(),
                20,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(SideWidth, 26f),
                left ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
            AnchorTop(nameLabel.rectTransform, left, 0f);

            RectTransform healthBar = MakeRect(
                container,
                "HealthBar",
                Vector2.zero,
                new Vector2(SideWidth, 26f));
            AnchorTop(healthBar, left, -34f);
            MakeFill(healthBar, "Back", new Color(0.10f, 0.13f, 0.12f, 1f), SideWidth);
            RectTransform healthFill = MakeFill(healthBar, "Fill", HealthColor, SideWidth);
            side.HealthFill = healthFill;

            side.HealthLabel = BoardHudOverlayFactory.CreateText(
                healthBar,
                "HealthLabel",
                string.Empty,
                BoardHudOverlayFactory.CreateFont(),
                15,
                FontStyle.Bold,
                new Vector2(10f, -4f),
                new Vector2(SideWidth - 20f, 20f),
                left ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
            side.HealthLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            side.HealthLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            side.HealthLabel.rectTransform.pivot = new Vector2(0f, 1f);
            side.HealthLabel.rectTransform.anchoredPosition = new Vector2(10f, -4f);

            RectTransform shieldBar = MakeRect(
                container,
                "ShieldBar",
                Vector2.zero,
                new Vector2(SideWidth, 16f));
            AnchorTop(shieldBar, left, -64f);
            MakeFill(shieldBar, "Back", new Color(0.09f, 0.13f, 0.15f, 1f), SideWidth);
            RectTransform shieldFill = MakeFill(shieldBar, "Fill", ShieldColor, SideWidth);
            side.ShieldFill = shieldFill;

            side.ShieldLabel = BoardHudOverlayFactory.CreateText(
                container,
                "ShieldLabel",
                string.Empty,
                BoardHudOverlayFactory.CreateFont(),
                12,
                FontStyle.Normal,
                Vector2.zero,
                new Vector2(SideWidth - 20f, 16f),
                left ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
            side.ShieldLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            side.ShieldLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            side.ShieldLabel.rectTransform.pivot = new Vector2(0f, 1f);
            side.ShieldLabel.rectTransform.anchoredPosition = new Vector2(10f, -66f);
            side.ShieldLabel.color = MutedColor;

            side.StatusLabel = BoardHudOverlayFactory.CreateText(
                container,
                "Status",
                string.Empty,
                BoardHudOverlayFactory.CreateFont(),
                14,
                FontStyle.Normal,
                Vector2.zero,
                new Vector2(SideWidth, 20f),
                left ? TextAnchor.UpperLeft : TextAnchor.UpperRight);
            AnchorTop(side.StatusLabel.rectTransform, left, -88f);
            side.StatusLabel.color = new Color(0.80f, 0.74f, 0.60f, 1f);

            side.CardRow = MakeRect(
                container,
                "Cards",
                Vector2.zero,
                new Vector2(SideWidth, ChipHeight + 30f));
            AnchorTop(side.CardRow, left, -116f);

            if (snapshot != null)
            {
                int index = 0;
                foreach (CardInstance card in snapshot.BattleCards)
                {
                    CreateChip(side, card, index);
                    index++;
                }
            }

            return side;
        }

        private void CreateChip(SideRuntime side, CardInstance card, int index)
        {
            RectTransform chip = MakeRect(
                side.CardRow,
                "Chip" + index,
                new Vector2(index * ChipPitch, 0f),
                new Vector2(ChipWidth, ChipHeight));
            AnchorTopLeft(chip, new Vector2(index * ChipPitch, 0f));

            Image background = chip.gameObject.GetComponent<Image>();
            background.color = ChipColor;

            Outline outline = chip.gameObject.AddComponent<Outline>();
            outline.effectColor = FrameColor(card.Definition.SlotSize);
            outline.effectDistance = new Vector2(2f, -2f);

            Text label = BoardHudOverlayFactory.CreateText(
                chip,
                "Name",
                card.Definition.DisplayName,
                BoardHudOverlayFactory.CreateFont(),
                13,
                FontStyle.Bold,
                new Vector2(6f, -6f),
                new Vector2(ChipWidth - 12f, 44f),
                TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.color = InkColor;

            Text meta = BoardHudOverlayFactory.CreateText(
                chip,
                "Meta",
                string.Concat(
                    card.Definition.SlotSize.ToString(),
                    "c·",
                    CardText.Seconds(card.Definition.CooldownTicks)),
                BoardHudOverlayFactory.CreateFont(),
                11,
                FontStyle.Normal,
                new Vector2(6f, -56f),
                new Vector2(ChipWidth - 12f, 18f),
                TextAnchor.UpperLeft);
            meta.color = MutedColor;

            side.Chips.Add(new Chip
            {
                CardUid = card.Uid,
                Background = background,
                Label = label
            });
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

        private void ClearBuildSide(SideRuntime side)
        {
            if (side != null && side.Root != null)
            {
                Destroy(side.Root.gameObject);
            }
        }

        private void ClearPopups()
        {
            if (popupLayer == null)
            {
                return;
            }

            List<GameObject> stale = new List<GameObject>();
            foreach (Transform child in popupLayer)
            {
                stale.Add(child.gameObject);
            }

            foreach (GameObject gameObject in stale)
            {
                Destroy(gameObject);
            }
        }

        private void RefreshMeters()
        {
            RefreshMeter(leftSide);
            RefreshMeter(rightSide);
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
            side.HealthFill.sizeDelta = new Vector2(SideWidth * healthFraction, 0f);
            side.HealthLabel.text = string.Concat(
                side.Health.ToString(),
                " / ",
                side.MaxHealth.ToString());

            float shieldFraction = Mathf.Clamp01(side.Shield / (float)Mathf.Max(1, side.MaxHealth));
            side.ShieldFill.sizeDelta = new Vector2(SideWidth * shieldFraction, 0f);
            side.ShieldLabel.text = string.Concat("护盾 ", side.Shield.ToString());

            List<string> statuses = new List<string>();
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

            side.StatusLabel.text = string.Join(" · ", statuses);
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
            RefreshSideChips(leftSide);
            RefreshSideChips(rightSide);
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
                    chip.Background.color = ChipDead;
                    continue;
                }

                chip.Background.color = now < chip.FlashUntil ? ChipFlash : ChipColor;
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
                    return chip.Label.text;
                }
            }

            return "卡牌";
        }

        private void SpawnPopup(SideRuntime side, string content, Color color, float direction)
        {
            if (popupLayer == null || side == null)
            {
                return;
            }

            Text popup = BoardHudOverlayFactory.CreateText(
                popupLayer,
                "Popup",
                content,
                BoardHudOverlayFactory.CreateFont(),
                30,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(240f, 40f),
                TextAnchor.MiddleCenter);
            popup.color = color;
            popup.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            popup.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            popup.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            popup.rectTransform.anchoredPosition = new Vector2(
                direction * 240f + UnityEngine.Random.Range(-12f, 12f),
                -20f);
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
            if (leftSide != null && leftSide.PlayerId == playerId)
            {
                return leftSide;
            }

            if (rightSide != null && rightSide.PlayerId == playerId)
            {
                return rightSide;
            }

            return null;
        }

        private string SidePrefix(SideRuntime side)
        {
            if (side == null)
            {
                return "?";
            }

            return side == leftSide ? "我方" : "对手";
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
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private static void AnchorTopLeft(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
        }

        private static void AnchorTop(RectTransform rect, bool left, float y)
        {
            float side = left ? 0f : 1f;
            rect.anchorMin = new Vector2(side, 1f);
            rect.anchorMax = new Vector2(side, 1f);
            rect.pivot = new Vector2(side, 1f);
            rect.anchoredPosition = new Vector2(left ? 0f : 0f, y);
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
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
