using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    public sealed class BoardHudEncounterViews
    {
        public BoardHudEncounterViews(
            GameObject overlay,
            RectTransform panel,
            Text title,
            Text subtitle,
            Text attackerName,
            Text attackerStats,
            Text attackerLoadout,
            Text defenderName,
            Text defenderStats,
            Text defenderLoadout,
            Text stakes,
            Button acceptButton,
            Text acceptLabel)
        {
            Overlay = overlay;
            Panel = panel;
            Title = title;
            Subtitle = subtitle;
            AttackerName = attackerName;
            AttackerStats = attackerStats;
            AttackerLoadout = attackerLoadout;
            DefenderName = defenderName;
            DefenderStats = defenderStats;
            DefenderLoadout = defenderLoadout;
            Stakes = stakes;
            AcceptButton = acceptButton;
            AcceptLabel = acceptLabel;
        }

        public GameObject Overlay { get; }
        public RectTransform Panel { get; }
        public Text Title { get; }
        public Text Subtitle { get; }
        public Text AttackerName { get; }
        public Text AttackerStats { get; }
        public Text AttackerLoadout { get; }
        public Text DefenderName { get; }
        public Text DefenderStats { get; }
        public Text DefenderLoadout { get; }
        public Text Stakes { get; }
        public Button AcceptButton { get; }
        public Text AcceptLabel { get; }
    }

    public sealed class BoardHudCardDrawerView
    {
        public BoardHudCardDrawerView(
            GameObject overlay,
            RectTransform panel,
            Text title,
            RectTransform content,
            ScrollRect scroll,
            Text empty,
            Button closeButton,
            Text closeLabel)
        {
            Overlay = overlay;
            Panel = panel;
            Title = title;
            Content = content;
            Scroll = scroll;
            Empty = empty;
            CloseButton = closeButton;
            CloseLabel = closeLabel;
        }

        public GameObject Overlay { get; }
        public RectTransform Panel { get; }
        public Text Title { get; }
        public RectTransform Content { get; }
        public ScrollRect Scroll { get; }
        public Text Empty { get; }
        public Button CloseButton { get; }
        public Text CloseLabel { get; }
    }

    public sealed class BoardHudRewardChoiceView
    {
        public BoardHudRewardChoiceView(
            GameObject overlay,
            Text title,
            Text subtitle,
            Button[] buttons,
            Text[] names,
            Text[] metas,
            Text[] details)
        {
            Overlay = overlay;
            Title = title;
            Subtitle = subtitle;
            Buttons = buttons;
            Names = names;
            Metas = metas;
            Details = details;
        }

        public GameObject Overlay { get; }
        public Text Title { get; }
        public Text Subtitle { get; }
        public Button[] Buttons { get; }
        public Text[] Names { get; }
        public Text[] Metas { get; }
        public Text[] Details { get; }
    }

    /// <summary>商店面板：3 张货、重掷一次、离开（R-ECO-004 / 规则书 §2.5）。</summary>
    public sealed class BoardHudShopView
    {
        public BoardHudShopView(
            GameObject overlay,
            Text title,
            Text subtitle,
            Button[] buttons,
            Text[] names,
            Text[] metas,
            Text[] details,
            Text[] prices,
            Button rerollButton,
            Text rerollLabel,
            Text rerollHint,
            Button leaveButton,
            Text leaveLabel)
        {
            Overlay = overlay;
            Title = title;
            Subtitle = subtitle;
            Buttons = buttons;
            Names = names;
            Metas = metas;
            Details = details;
            Prices = prices;
            RerollButton = rerollButton;
            RerollLabel = rerollLabel;
            RerollHint = rerollHint;
            LeaveButton = leaveButton;
            LeaveLabel = leaveLabel;
        }

        public GameObject Overlay { get; }
        public Text Title { get; }
        public Text Subtitle { get; }
        public Button[] Buttons { get; }
        public Text[] Names { get; }
        public Text[] Metas { get; }
        public Text[] Details { get; }
        public Text[] Prices { get; }
        public Button RerollButton { get; }
        public Text RerollLabel { get; }
        public Text RerollHint { get; }
        public Button LeaveButton { get; }
        public Text LeaveLabel { get; }
    }

    public sealed class BoardHudCombatResultView
    {
        public BoardHudCombatResultView(
            GameObject overlay,
            RectTransform panel,
            Text title,
            Text subtitle,
            Text body,
            Button closeButton,
            Text closeLabel)
        {
            Overlay = overlay;
            Panel = panel;
            Title = title;
            Subtitle = subtitle;
            Body = body;
            CloseButton = closeButton;
            CloseLabel = closeLabel;
        }

        public GameObject Overlay { get; }
        public RectTransform Panel { get; }
        public Text Title { get; }
        public Text Subtitle { get; }
        public Text Body { get; }
        public Button CloseButton { get; }
        public Text CloseLabel { get; }
    }

    public static class BoardHudOverlayFactory
    {
        public static GameObject CreateCardDrawerScrim(Transform parent)
        {
            GameObject scrim = new GameObject(
                "CardDrawerScrim",
                typeof(RectTransform),
                typeof(Image));
            scrim.transform.SetParent(parent, false);

            RectTransform rect = scrim.GetComponent<RectTransform>();
            Stretch(rect);

            Image image = scrim.GetComponent<Image>();
            image.color = new Color(0.012f, 0.02f, 0.024f, 0.72f);
            image.raycastTarget = true;

            scrim.SetActive(false);
            return scrim;
        }

        public static BoardHudEncounterViews CreateEncounterOverlay(
            Transform parent,
            Font font)
        {
            GameObject overlay = new GameObject(
                "PlayerEncounterOverlay",
                typeof(RectTransform),
                typeof(Image));
            overlay.transform.SetParent(parent, false);
            Stretch(overlay.GetComponent<RectTransform>());

            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0.01f, 0.018f, 0.022f, 0.86f);
            overlayImage.raycastTarget = true;

            GameObject panelObject = new GameObject(
                "EncounterPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(overlay.transform, false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(940f, 510f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.048f, 0.078f, 0.09f, 0.99f);
            panelImage.raycastTarget = true;

            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.72f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateBar(
                panel,
                "BrassRule",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 5f),
                new Vector2(0f, 0f),
                new Color(0.92f, 0.69f, 0.27f, 1f));
            CreateBar(
                panel,
                "ThreatRule",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0f),
                new Vector2(7f, 0f),
                new Color(0.76f, 0.24f, 0.20f, 1f));
            CreateBar(
                panel,
                "CenterRule",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 72f),
                new Vector2(1f, -86f),
                new Color(0.49f, 0.58f, 0.57f, 0.34f));
            CreateBar(
                panel,
                "FooterRule",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(28f, 124f),
                new Vector2(-28f, 125f),
                new Color(0.49f, 0.58f, 0.57f, 0.28f));

            Text title = CreateText(
                panel,
                "Title",
                "遭遇战",
                font,
                32,
                FontStyle.Bold,
                new Vector2(42f, -24f),
                new Vector2(420f, 44f),
                TextAnchor.MiddleLeft);
            title.color = new Color(0.97f, 0.94f, 0.86f, 1f);

            Text subtitle = CreateText(
                panel,
                "Subtitle",
                "双方进入同一地块。确认迎战后，战斗立即结算。",
                font,
                18,
                FontStyle.Normal,
                new Vector2(42f, -67f),
                new Vector2(680f, 28f),
                TextAnchor.MiddleLeft);
            subtitle.color = new Color(0.60f, 0.68f, 0.67f, 1f);

            Text attackerHeader = CreateText(
                panel,
                "AttackerHeader",
                "你的队伍",
                font,
                17,
                FontStyle.Bold,
                new Vector2(42f, -116f),
                new Vector2(350f, 26f),
                TextAnchor.MiddleLeft);
            attackerHeader.color = new Color(0.92f, 0.69f, 0.27f, 1f);

            Text defenderHeader = CreateText(
                panel,
                "DefenderHeader",
                "对手",
                font,
                17,
                FontStyle.Bold,
                new Vector2(505f, -116f),
                new Vector2(350f, 26f),
                TextAnchor.MiddleLeft);
            defenderHeader.color = new Color(0.88f, 0.40f, 0.34f, 1f);

            Text attackerName = CreateText(
                panel,
                "AttackerName",
                string.Empty,
                font,
                28,
                FontStyle.Bold,
                new Vector2(42f, -145f),
                new Vector2(360f, 42f),
                TextAnchor.MiddleLeft);
            Text attackerStats = CreateText(
                panel,
                "AttackerStats",
                string.Empty,
                font,
                18,
                FontStyle.Normal,
                new Vector2(42f, -190f),
                new Vector2(390f, 30f),
                TextAnchor.MiddleLeft);
            attackerStats.color = new Color(0.73f, 0.78f, 0.76f, 1f);
            Text attackerLoadout = CreateLoadoutText(
                panel,
                "AttackerLoadout",
                font,
                new Vector2(42f, -236f));

            Text defenderName = CreateText(
                panel,
                "DefenderName",
                string.Empty,
                font,
                28,
                FontStyle.Bold,
                new Vector2(505f, -145f),
                new Vector2(360f, 42f),
                TextAnchor.MiddleLeft);
            defenderName.color = new Color(0.96f, 0.78f, 0.71f, 1f);
            Text defenderStats = CreateText(
                panel,
                "DefenderStats",
                string.Empty,
                font,
                18,
                FontStyle.Normal,
                new Vector2(505f, -190f),
                new Vector2(390f, 30f),
                TextAnchor.MiddleLeft);
            defenderStats.color = new Color(0.73f, 0.78f, 0.76f, 1f);
            Text defenderLoadout = CreateLoadoutText(
                panel,
                "DefenderLoadout",
                font,
                new Vector2(505f, -236f));

            Text stakes = CreateText(
                panel,
                "Stakes",
                "败者失去最多 15 碎片 · 胜者获得这些碎片与 2 胜场",
                font,
                17,
                FontStyle.Normal,
                new Vector2(42f, -405f),
                new Vector2(620f, 34f),
                TextAnchor.MiddleLeft);
            stakes.color = new Color(0.68f, 0.73f, 0.71f, 1f);

            Button acceptButton = CreateButton(
                panel,
                "AcceptButton",
                "应战",
                font,
                new Vector2(736f, -398f),
                new Vector2(164f, 54f),
                out Text acceptLabel);

            overlay.SetActive(false);
            return new BoardHudEncounterViews(
                overlay,
                panel,
                title,
                subtitle,
                attackerName,
                attackerStats,
                attackerLoadout,
                defenderName,
                defenderStats,
                defenderLoadout,
                stakes,
                acceptButton,
                acceptLabel);
        }

        public static BoardHudCardDrawerView CreateCardDrawerView(
            Transform parent,
            Font font)
        {
            GameObject overlay = new GameObject(
                "CardViewOverlay",
                typeof(RectTransform),
                typeof(Image));
            overlay.transform.SetParent(parent, false);
            Stretch(overlay.GetComponent<RectTransform>());

            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0.01f, 0.018f, 0.022f, 0.88f);
            overlayImage.raycastTarget = true;

            GameObject panelObject = new GameObject(
                "CardViewPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(overlay.transform, false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(860f, 620f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.048f, 0.078f, 0.09f, 0.99f);
            panelImage.raycastTarget = true;

            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.72f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateBar(
                panel,
                "BrassRule",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 5f),
                new Vector2(0f, 0f),
                new Color(0.92f, 0.69f, 0.27f, 1f));

            Text title = CreateText(
                panel,
                "Title",
                "卡槽",
                font,
                28,
                FontStyle.Bold,
                new Vector2(34f, -24f),
                new Vector2(480f, 40f),
                TextAnchor.UpperLeft);
            title.color = new Color(0.96f, 0.88f, 0.69f, 1f);

            Text hint = CreateText(
                panel,
                "Hint",
                "只读预览 · 再按一次「展开卡槽」关闭",
                font,
                16,
                FontStyle.Normal,
                new Vector2(540f, -34f),
                new Vector2(280f, 24f),
                TextAnchor.UpperRight);
            hint.color = new Color(0.72f, 0.7f, 0.64f, 1f);

            GameObject viewportObject = new GameObject(
                "CardViewViewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect));
            viewportObject.transform.SetParent(panel, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = new Vector2(0f, 1f);
            viewport.anchorMax = new Vector2(0f, 1f);
            viewport.pivot = new Vector2(0f, 1f);
            viewport.anchoredPosition = new Vector2(34f, -84f);
            viewport.sizeDelta = new Vector2(792f, 470f);

            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImage.raycastTarget = true;

            GameObject contentObject = new GameObject(
                "CardViewContent",
                typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 470f);

            ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;

            Text empty = CreateText(
                content,
                "Empty",
                "当前玩家还没有任何卡牌。",
                font,
                18,
                FontStyle.Normal,
                new Vector2(4f, -8f),
                new Vector2(780f, 28f),
                TextAnchor.UpperLeft);
            empty.color = new Color(0.72f, 0.7f, 0.64f, 1f);

            Button closeButton = CreateButton(
                panel,
                "CloseButton",
                "关闭",
                font,
                new Vector2(340f, -542f),
                new Vector2(180f, 52f),
                out Text closeLabel);

            overlay.SetActive(false);
            return new BoardHudCardDrawerView(
                overlay,
                panel,
                title,
                content,
                scroll,
                empty,
                closeButton,
                closeLabel);
        }

        public static BoardHudRewardChoiceView CreateRewardChoiceView(
            Transform parent,
            Font font)
        {
            const int optionCount = 2;

            GameObject overlay = new GameObject(
                "RewardChoiceOverlay",
                typeof(RectTransform),
                typeof(Image));
            overlay.transform.SetParent(parent, false);
            Stretch(overlay.GetComponent<RectTransform>());

            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0.01f, 0.018f, 0.022f, 0.82f);
            overlayImage.raycastTarget = true;

            GameObject panelObject = new GameObject(
                "RewardChoicePanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(overlay.transform, false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(860f, 560f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.048f, 0.078f, 0.09f, 0.99f);
            panelImage.raycastTarget = true;

            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.72f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateBar(
                panel,
                "BrassRule",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 5f),
                new Vector2(0f, 0f),
                new Color(0.92f, 0.69f, 0.27f, 1f));

            Text title = CreateText(
                panel,
                "Title",
                "奖励 · 选一张",
                font,
                28,
                FontStyle.Bold,
                new Vector2(34f, -24f),
                new Vector2(520f, 40f),
                TextAnchor.UpperLeft);
            title.color = new Color(0.96f, 0.88f, 0.69f, 1f);

            Text subtitle = CreateText(
                panel,
                "Subtitle",
                "免费获得 · 装不下的牌会置灰（R-TILE-006、R-ECO-006）",
                font,
                16,
                FontStyle.Normal,
                new Vector2(34f, -62f),
                new Vector2(792f, 24f),
                TextAnchor.UpperLeft);
            subtitle.color = new Color(0.72f, 0.7f, 0.64f, 1f);

            Button[] buttons = new Button[optionCount];
            Text[] names = new Text[optionCount];
            Text[] metas = new Text[optionCount];
            Text[] details = new Text[optionCount];

            for (int index = 0; index < optionCount; index++)
            {
                GameObject optionObject = new GameObject(
                    "RewardOption" + index.ToString(),
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(Outline));
                optionObject.transform.SetParent(panel, false);

                RectTransform option = optionObject.GetComponent<RectTransform>();
                option.anchorMin = new Vector2(0f, 1f);
                option.anchorMax = new Vector2(0f, 1f);
                option.pivot = new Vector2(0f, 1f);
                option.anchoredPosition = new Vector2(34f, -100f - (index * 210f));
                option.sizeDelta = new Vector2(792f, 186f);

                Image optionImage = optionObject.GetComponent<Image>();
                optionImage.color = new Color(0.07f, 0.11f, 0.125f, 1f);

                Outline optionOutline = optionObject.GetComponent<Outline>();
                optionOutline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.42f);
                optionOutline.effectDistance = new Vector2(1f, -1f);

                Button button = optionObject.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.16f, 1.12f, 1.02f, 1f);
                colors.pressedColor = new Color(0.86f, 0.8f, 0.68f, 1f);
                colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.6f);
                button.colors = colors;

                CreateBar(
                    option,
                    "Accent",
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0f),
                    new Vector2(6f, 0f),
                    new Color(0.92f, 0.69f, 0.27f, 0.9f));

                names[index] = CreateText(
                    option,
                    "Name",
                    "-",
                    font,
                    26,
                    FontStyle.Bold,
                    new Vector2(26f, -16f),
                    new Vector2(430f, 36f),
                    TextAnchor.UpperLeft);
                names[index].color = new Color(0.96f, 0.92f, 0.82f, 1f);

                metas[index] = CreateText(
                    option,
                    "Meta",
                    "-",
                    font,
                    16,
                    FontStyle.Normal,
                    new Vector2(26f, -54f),
                    new Vector2(520f, 24f),
                    TextAnchor.UpperLeft);
                metas[index].color = new Color(0.72f, 0.7f, 0.64f, 1f);

                details[index] = CreateText(
                    option,
                    "Detail",
                    "-",
                    font,
                    16,
                    FontStyle.Normal,
                    new Vector2(26f, -82f),
                    new Vector2(736f, 92f),
                    TextAnchor.UpperLeft);
                details[index].color = new Color(0.88f, 0.86f, 0.8f, 1f);

                CreateText(
                    option,
                    "Take",
                    "选这张",
                    font,
                    18,
                    FontStyle.Bold,
                    new Vector2(620f, -16f),
                    new Vector2(150f, 30f),
                    TextAnchor.UpperRight).color =
                    new Color(0.92f, 0.69f, 0.27f, 1f);

                buttons[index] = button;
            }

            overlay.SetActive(false);
            return new BoardHudRewardChoiceView(
                overlay,
                title,
                subtitle,
                buttons,
                names,
                metas,
                details);
        }

        /// <summary>商店面板。3 张货 + 重掷 + 离开，价格与刷新规则取自规则书 §2.5。</summary>
        public static BoardHudShopView CreateShopView(Transform parent, Font font)
        {
            const int offerCount = 3;

            GameObject overlay = new GameObject(
                "ShopOverlay",
                typeof(RectTransform),
                typeof(Image));
            overlay.transform.SetParent(parent, false);
            Stretch(overlay.GetComponent<RectTransform>());

            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0.01f, 0.018f, 0.022f, 0.82f);
            overlayImage.raycastTarget = true;

            GameObject panelObject = new GameObject(
                "ShopPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(overlay.transform, false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(900f, 620f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.048f, 0.078f, 0.09f, 0.99f);
            panelImage.raycastTarget = true;

            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.72f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateBar(
                panel,
                "BrassRule",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 5f),
                new Vector2(0f, 0f),
                new Color(0.92f, 0.69f, 0.27f, 1f));

            Text title = CreateText(
                panel,
                "Title",
                "商店",
                font,
                28,
                FontStyle.Bold,
                new Vector2(34f, -22f),
                new Vector2(760f, 40f),
                TextAnchor.UpperLeft);
            title.color = new Color(0.96f, 0.88f, 0.69f, 1f);

            Text subtitle = CreateText(
                panel,
                "Subtitle",
                "",
                font,
                16,
                FontStyle.Normal,
                new Vector2(34f, -62f),
                new Vector2(832f, 24f),
                TextAnchor.UpperLeft);
            subtitle.color = new Color(0.72f, 0.7f, 0.64f, 1f);
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button[] buttons = new Button[offerCount];
            Text[] names = new Text[offerCount];
            Text[] metas = new Text[offerCount];
            Text[] details = new Text[offerCount];
            Text[] prices = new Text[offerCount];

            for (int index = 0; index < offerCount; index++)
            {
                GameObject optionObject = new GameObject(
                    "ShopOffer" + index.ToString(),
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(Outline));
                optionObject.transform.SetParent(panel, false);

                RectTransform option = optionObject.GetComponent<RectTransform>();
                option.anchorMin = new Vector2(0f, 1f);
                option.anchorMax = new Vector2(0f, 1f);
                option.pivot = new Vector2(0f, 1f);
                option.anchoredPosition = new Vector2(34f, -96f - (index * 146f));
                option.sizeDelta = new Vector2(832f, 132f);

                Image optionImage = optionObject.GetComponent<Image>();
                optionImage.color = new Color(0.07f, 0.11f, 0.125f, 1f);

                Outline optionOutline = optionObject.GetComponent<Outline>();
                optionOutline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.42f);
                optionOutline.effectDistance = new Vector2(1f, -1f);

                Button button = optionObject.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.16f, 1.12f, 1.02f, 1f);
                colors.pressedColor = new Color(0.86f, 0.8f, 0.68f, 1f);
                colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.6f);
                button.colors = colors;

                CreateBar(
                    option,
                    "Accent",
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0f),
                    new Vector2(6f, 0f),
                    new Color(0.92f, 0.69f, 0.27f, 0.9f));

                names[index] = CreateText(
                    option,
                    "Name",
                    "-",
                    font,
                    26,
                    FontStyle.Bold,
                    new Vector2(26f, -12f),
                    new Vector2(430f, 34f),
                    TextAnchor.UpperLeft);
                names[index].color = new Color(0.96f, 0.92f, 0.82f, 1f);

                metas[index] = CreateText(
                    option,
                    "Meta",
                    "-",
                    font,
                    16,
                    FontStyle.Normal,
                    new Vector2(26f, -50f),
                    new Vector2(600f, 22f),
                    TextAnchor.UpperLeft);
                metas[index].color = new Color(0.72f, 0.7f, 0.64f, 1f);

                details[index] = CreateText(
                    option,
                    "Detail",
                    "-",
                    font,
                    15,
                    FontStyle.Normal,
                    new Vector2(26f, -78f),
                    new Vector2(650f, 46f),
                    TextAnchor.UpperLeft);
                details[index].color = new Color(0.88f, 0.86f, 0.8f, 1f);
                details[index].horizontalOverflow = HorizontalWrapMode.Wrap;

                prices[index] = CreateText(
                    option,
                    "Price",
                    "-",
                    font,
                    22,
                    FontStyle.Bold,
                    new Vector2(690f, -14f),
                    new Vector2(120f, 34f),
                    TextAnchor.UpperRight);
                prices[index].color = new Color(0.92f, 0.69f, 0.27f, 1f);

                buttons[index] = button;
            }

            GameObject rerollObject = new GameObject(
                "RerollButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            rerollObject.transform.SetParent(panel, false);
            RectTransform rerollRect = rerollObject.GetComponent<RectTransform>();
            rerollRect.anchorMin = new Vector2(0f, 0f);
            rerollRect.anchorMax = new Vector2(0f, 0f);
            rerollRect.pivot = new Vector2(0f, 0f);
            rerollRect.anchoredPosition = new Vector2(34f, 30f);
            rerollRect.sizeDelta = new Vector2(250f, 48f);
            rerollObject.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.18f, 1f);
            rerollObject.GetComponent<Outline>().effectColor =
                new Color(0.86f, 0.68f, 0.35f, 0.42f);
            Button rerollButton = rerollObject.GetComponent<Button>();
            Text rerollLabel = CreateText(
                rerollObject.transform,
                "Label",
                "重掷",
                font,
                18,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(250f, 48f),
                TextAnchor.MiddleCenter);
            rerollLabel.rectTransform.anchorMin = Vector2.zero;
            rerollLabel.rectTransform.anchorMax = Vector2.one;
            rerollLabel.rectTransform.offsetMin = Vector2.zero;
            rerollLabel.rectTransform.offsetMax = Vector2.zero;
            rerollLabel.color = new Color(0.92f, 0.84f, 0.66f, 1f);

            Text rerollHint = CreateText(
                panel,
                "RerollHint",
                "",
                font,
                15,
                FontStyle.Normal,
                new Vector2(300f, 44f),
                new Vector2(360f, 22f),
                TextAnchor.UpperLeft);
            rerollHint.color = new Color(0.66f, 0.64f, 0.58f, 1f);

            GameObject leaveObject = new GameObject(
                "LeaveButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            leaveObject.transform.SetParent(panel, false);
            RectTransform leaveRect = leaveObject.GetComponent<RectTransform>();
            leaveRect.anchorMin = new Vector2(1f, 0f);
            leaveRect.anchorMax = new Vector2(1f, 0f);
            leaveRect.pivot = new Vector2(1f, 0f);
            leaveRect.anchoredPosition = new Vector2(-34f, 30f);
            leaveRect.sizeDelta = new Vector2(226f, 48f);
            leaveObject.GetComponent<Image>().color = new Color(0.2f, 0.16f, 0.1f, 1f);
            leaveObject.GetComponent<Outline>().effectColor =
                new Color(0.92f, 0.69f, 0.27f, 0.6f);
            Button leaveButton = leaveObject.GetComponent<Button>();
            Text leaveLabel = CreateText(
                leaveObject.transform,
                "Label",
                "离开商店",
                font,
                18,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(226f, 48f),
                TextAnchor.MiddleCenter);
            leaveLabel.rectTransform.anchorMin = Vector2.zero;
            leaveLabel.rectTransform.anchorMax = Vector2.one;
            leaveLabel.rectTransform.offsetMin = Vector2.zero;
            leaveLabel.rectTransform.offsetMax = Vector2.zero;
            leaveLabel.color = new Color(0.96f, 0.88f, 0.69f, 1f);

            overlay.SetActive(false);
            return new BoardHudShopView(
                overlay,
                title,
                subtitle,
                buttons,
                names,
                metas,
                details,
                prices,
                rerollButton,
                rerollLabel,
                rerollHint,
                leaveButton,
                leaveLabel);
        }

        public static BoardHudCombatResultView CreateCombatResultView(
            Transform parent,
            Font font)
        {
            GameObject overlay = new GameObject(
                "CombatResultOverlay",
                typeof(RectTransform),
                typeof(Image));
            overlay.transform.SetParent(parent, false);
            Stretch(overlay.GetComponent<RectTransform>());

            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0.01f, 0.018f, 0.022f, 0.86f);

            // 结算面板不挡操作：遮罩不吃射线，玩家看完可以继续掷骰。
            overlayImage.raycastTarget = false;

            GameObject panelObject = new GameObject(
                "CombatResultPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            panelObject.transform.SetParent(overlay.transform, false);

            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(720f, 470f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.048f, 0.078f, 0.09f, 0.99f);
            panelImage.raycastTarget = true;

            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.72f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateBar(
                panel,
                "BrassRule",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 5f),
                new Vector2(0f, 0f),
                new Color(0.92f, 0.69f, 0.27f, 1f));

            Text title = CreateText(
                panel,
                "Title",
                "战斗结算",
                font,
                34,
                FontStyle.Bold,
                new Vector2(40f, -28f),
                new Vector2(640f, 48f),
                TextAnchor.UpperLeft);
            title.color = new Color(0.96f, 0.88f, 0.69f, 1f);

            Text subtitle = CreateText(
                panel,
                "Subtitle",
                string.Empty,
                font,
                18,
                FontStyle.Normal,
                new Vector2(40f, -80f),
                new Vector2(640f, 26f),
                TextAnchor.UpperLeft);
            subtitle.color = new Color(0.8f, 0.78f, 0.7f, 1f);

            Text body = CreateText(
                panel,
                "Body",
                string.Empty,
                font,
                19,
                FontStyle.Normal,
                new Vector2(40f, -120f),
                new Vector2(640f, 250f),
                TextAnchor.UpperLeft);
            body.color = new Color(0.9f, 0.9f, 0.86f, 1f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            body.lineSpacing = 1.3f;

            Button closeButton = CreateButton(
                panel,
                "CloseButton",
                "继续",
                font,
                new Vector2(270f, -392f),
                new Vector2(180f, 52f),
                out Text closeLabel);

            overlay.SetActive(false);
            return new BoardHudCombatResultView(
                overlay,
                panel,
                title,
                subtitle,
                body,
                closeButton,
                closeLabel);
        }

        public static GameObject CreateCardSectionHeader(
            Transform content,
            Font font,
            float top,
            float width,
            string label)
        {
            GameObject header = new GameObject(
                "CardSectionHeader",
                typeof(RectTransform));
            header.transform.SetParent(content, false);

            RectTransform rect = header.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(width, 26f);

            Text text = CreateText(
                header.transform,
                "Label",
                label,
                font,
                17,
                FontStyle.Bold,
                Vector2.zero,
                new Vector2(width, 26f),
                TextAnchor.UpperLeft);
            text.color = new Color(0.92f, 0.69f, 0.27f, 1f);
            return header;
        }

        public static GameObject CreateCardRow(
            Transform content,
            Font font,
            float top,
            float width,
            float height,
            out Text name,
            out Text meta,
            out Text detail)
        {
            GameObject row = new GameObject(
                "CardRow",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            row.transform.SetParent(content, false);

            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(width, height);

            Image image = row.GetComponent<Image>();
            image.color = new Color(0.094f, 0.133f, 0.145f, 0.96f);
            image.raycastTarget = false;

            Outline outline = row.GetComponent<Outline>();
            outline.effectColor = new Color(0.86f, 0.68f, 0.35f, 0.3f);
            outline.effectDistance = new Vector2(1f, -1f);

            name = CreateText(
                row.transform,
                "Name",
                string.Empty,
                font,
                21,
                FontStyle.Bold,
                new Vector2(14f, -6f),
                new Vector2(310f, 26f),
                TextAnchor.UpperLeft);
            name.color = new Color(0.96f, 0.93f, 0.84f, 1f);

            meta = CreateText(
                row.transform,
                "Meta",
                string.Empty,
                font,
                15,
                FontStyle.Normal,
                new Vector2(14f, -32f),
                new Vector2(310f, 22f),
                TextAnchor.UpperLeft);
            meta.color = new Color(0.77f, 0.74f, 0.66f, 1f);

            detail = CreateText(
                row.transform,
                "Detail",
                string.Empty,
                font,
                15,
                FontStyle.Normal,
                new Vector2(332f, -8f),
                new Vector2(width - 348f, height - 12f),
                TextAnchor.UpperLeft);
            detail.color = new Color(0.86f, 0.86f, 0.8f, 1f);
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            detail.verticalOverflow = VerticalWrapMode.Truncate;
            return row;
        }

        public static Font CreateFont()
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

        private static Text CreateLoadoutText(
            Transform parent,
            string name,
            Font font,
            Vector2 anchoredPosition)
        {
            Text loadout = CreateText(
                parent,
                name,
                string.Empty,
                font,
                19,
                FontStyle.Normal,
                anchoredPosition,
                new Vector2(390f, 150f),
                TextAnchor.UpperLeft);
            loadout.color = new Color(0.88f, 0.88f, 0.83f, 1f);
            loadout.horizontalOverflow = HorizontalWrapMode.Wrap;
            loadout.verticalOverflow = VerticalWrapMode.Truncate;
            loadout.lineSpacing = 1.2f;
            return loadout;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string labelValue,
            Font font,
            Vector2 anchoredPosition,
            Vector2 size,
            out Text label)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.89f, 0.64f, 0.20f, 1f);

            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 0.91f, 0.65f, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.78f, 1f);
            colors.pressedColor = new Color(0.76f, 0.49f, 0.11f, 1f);
            colors.disabledColor = new Color(0.42f, 0.43f, 0.40f, 1f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            label = CreateText(
                buttonObject.transform,
                "Label",
                labelValue,
                font,
                23,
                FontStyle.Bold,
                Vector2.zero,
                size,
                TextAnchor.MiddleCenter);
            label.color = new Color(0.055f, 0.074f, 0.075f, 1f);
            Stretch(label.GetComponent<RectTransform>());
            return button;
        }

        internal static Text CreateText(
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

        internal static GameObject CreateBar(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Color color)
        {
            GameObject bar = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            bar.transform.SetParent(parent, false);

            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = bar.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return bar;
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
