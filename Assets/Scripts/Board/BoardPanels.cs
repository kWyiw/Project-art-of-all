using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProjectArtOfAll.Core.Cards;
using ProjectArtOfAll.Core.Match;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 四角玩家栏（`棋盘层双栏卡槽UI与拖拽交互规格.md` §2.5）。
    /// 每格显示等级、碎片、胜场、装配/背包占用；当前行动者高亮。
    /// 点击弹出对手档案（§2.7），这是界面上唯一能看别人战斗栏的入口。
    /// </summary>
    public sealed class PlayerCornerPanels : MonoBehaviour
    {
        private const float PanelWidth = 300f;
        private const float PanelHeight = 74f;
        private const float PanelGap = 6f;
        private const float Margin = 16f;

        private static readonly Color PanelColor = new Color(0.048f, 0.078f, 0.09f, 0.94f);
        private static readonly Color ActiveColor = new Color(0.16f, 0.14f, 0.07f, 0.97f);
        private static readonly Color Brass = new Color(0.92f, 0.69f, 0.27f, 1f);
        private static readonly Color TextColor = new Color(0.94f, 0.93f, 0.88f, 1f);
        private static readonly Color MutedColor = new Color(0.66f, 0.68f, 0.66f, 1f);

        private readonly List<PanelSlot> slots = new List<PanelSlot>();
        private Font font;
        private System.Action<int> onClick;

        private sealed class PanelSlot
        {
            public RectTransform Root;
            public Image Background;
            public Text Title;
            public Text Economy;
            public Text Loadout;
            public Button Button;
            public int PlayerId;
        }

        public static PlayerCornerPanels Create(RectTransform parent, Font font, System.Action<int> onClick)
        {
            GameObject rootObject = new GameObject("PlayerCornerPanels", typeof(RectTransform));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            PlayerCornerPanels panels = rootObject.AddComponent<PlayerCornerPanels>();
            panels.font = font;
            panels.onClick = onClick;
            panels.BuildSlots(rect);
            return panels;
        }

        private void BuildSlots(RectTransform root)
        {
            // 四块面板竖着叠在右上角，按座位顺序自上而下：P1、P2、P3、P4。
            for (int i = 0; i < 4; i++)
            {
                PanelSlot slot = new PanelSlot { PlayerId = i };
                slot.Root = CreatePanel(root, i, out slot.Background, out slot.Title,
                    out slot.Economy, out slot.Loadout, out slot.Button);
                int captured = i;
                slot.Button.onClick.AddListener(() => onClick?.Invoke(captured));
                slots.Add(slot);
            }
        }

        private RectTransform CreatePanel(
            RectTransform root,
            int index,
            out Image background,
            out Text title,
            out Text economy,
            out Text loadout,
            out Button button)
        {
            float top = Margin + index * (PanelHeight + PanelGap);
            GameObject panelObject = BoardHudOverlayFactory.CreateBar(
                root,
                "PlayerPanel",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-Margin - PanelWidth, -top - PanelHeight),
                new Vector2(-Margin, -top),
                PanelColor);
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            background = panelObject.GetComponent<Image>();
            button = panelObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            title = BoardHudOverlayFactory.CreateText(
                panelObject.transform, "Title", "P1", font, 15, FontStyle.Bold,
                new Vector2(12f, -7f), new Vector2(PanelWidth - 24f, 20f), TextAnchor.UpperLeft);
            title.color = Brass;

            economy = BoardHudOverlayFactory.CreateText(
                panelObject.transform, "Economy", "碎片 0 · 胜场 0", font, 13, FontStyle.Normal,
                new Vector2(12f, -28f), new Vector2(PanelWidth - 24f, 18f), TextAnchor.UpperLeft);
            economy.color = TextColor;

            loadout = BoardHudOverlayFactory.CreateText(
                panelObject.transform, "Loadout", "装配 0/4c · 背包 0/10c", font, 12, FontStyle.Normal,
                new Vector2(12f, -47f), new Vector2(PanelWidth - 24f, 18f), TextAnchor.UpperLeft);
            loadout.color = MutedColor;
            return rect;
        }

        /// <summary>把带子高度告诉四角栏：下方两块抬到带子上沿，不然会被卡槽盖住。</summary>
        /// <summary>面板已经集中在右上角，不再随卡槽带高度移动（保留接口，避免调用方分叉）。</summary>
        public void ApplyBandTop(float bandTop)
        {
        }

        public void Refresh(MatchState state, int humanPlayerId)
        {
            if (state == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                PanelSlot slot = slots[i];
                MatchPlayerState player = state.Players.FirstOrDefault(p => p.Id == slot.PlayerId);
                if (player == null)
                {
                    slot.Root.gameObject.SetActive(false);
                    continue;
                }

                slot.Root.gameObject.SetActive(true);
                bool active = state.CurrentPlayer.Id == player.Id;
                slot.Background.color = active ? ActiveColor : PanelColor;

                string who = player.Id == humanPlayerId ? "你" : "对手";
                slot.Title.text = string.Concat(
                    "P", (player.Id + 1).ToString(), "  ", who, "  Lv.", player.Level.ToString());
                slot.Title.color = active ? Brass : TextColor;

                slot.Economy.text = string.Concat(
                    "碎片 ", player.Shards.ToString(),
                    "  ·  胜场 ", player.Wins.ToString());

                int capacity = player.Level >= 6 ? 10 : LoadoutRules.GetBattleCapacity(player.Level);
                slot.Loadout.text = string.Concat(
                    "装配 ", player.BattleCardWidth.ToString(), "/", capacity.ToString(), "c",
                    "  ·  背包 ", player.OwnedCardWidth.ToString(), "/",
                    LoadoutRules.BagCapacity.ToString(), "c");
            }
        }

        internal void BindClick(int playerId, System.Action<int> handler)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].PlayerId == playerId)
                {
                    int captured = playerId;
                    slots[i].Button.onClick.RemoveAllListeners();
                    slots[i].Button.onClick.AddListener(() => handler?.Invoke(captured));
                }
            }
        }
    }

    /// <summary>
    /// 对手档案面板（`棋盘层双栏卡槽UI与拖拽交互规格.md` §2.7）。
    /// 战斗栏按真实顺序渲染；背包只给三个数字 + 占用条，永远不渲染牌名。
    /// </summary>
    public sealed class OpponentDossierView : MonoBehaviour
    {
        private const float PanelWidth = 372f;
        private const float PanelHeight = 452f;

        private static readonly Color PanelColor = new Color(0.048f, 0.078f, 0.09f, 0.99f);
        private static readonly Color Brass = new Color(0.92f, 0.69f, 0.27f, 1f);
        private static readonly Color TextColor = new Color(0.94f, 0.93f, 0.88f, 1f);
        private static readonly Color MutedColor = new Color(0.66f, 0.68f, 0.66f, 1f);
        private static readonly Color BarColor = new Color(0.20f, 0.16f, 0.10f, 1f);
        private static readonly Color BarFill = new Color(0.89f, 0.64f, 0.20f, 1f);

        private RectTransform root;
        private Text title;
        private Text subtitle;
        private Text battleHeader;
        private RectTransform battleRow;
        private Text bagHeader;
        private RectTransform bagFill;
        private Text bagFooter;
        private Button closeButton;
        private Font font;

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        public static OpponentDossierView Create(RectTransform parent, Font font, System.Action onClose)
        {
            GameObject rootObject = new GameObject("OpponentDossier", typeof(RectTransform));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rect.anchoredPosition = new Vector2(-16f, 40f);

            OpponentDossierView view = rootObject.AddComponent<OpponentDossierView>();
            view.root = rect;
            view.font = font;
            view.Build(onClose);
            rootObject.SetActive(false);
            return view;
        }

        private void Build(System.Action onClose)
        {
            Image background = gameObject.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = true;

            title = BoardHudOverlayFactory.CreateText(
                transform, "Title", "Player", font, 22, FontStyle.Bold,
                new Vector2(18f, -16f), new Vector2(PanelWidth - 36f, 30f), TextAnchor.UpperLeft);
            title.color = Brass;

            subtitle = BoardHudOverlayFactory.CreateText(
                transform, "Subtitle", string.Empty, font, 15, FontStyle.Normal,
                new Vector2(18f, -50f), new Vector2(PanelWidth - 36f, 22f), TextAnchor.UpperLeft);
            subtitle.color = TextColor;

            battleHeader = BoardHudOverlayFactory.CreateText(
                transform, "BattleHeader", "战斗栏", font, 15, FontStyle.Bold,
                new Vector2(18f, -88f), new Vector2(PanelWidth - 36f, 22f), TextAnchor.UpperLeft);
            battleHeader.color = Brass;

            GameObject rowObject = new GameObject("BattleRow", typeof(RectTransform));
            battleRow = rowObject.GetComponent<RectTransform>();
            battleRow.SetParent(transform, false);
            battleRow.anchorMin = new Vector2(0f, 1f);
            battleRow.anchorMax = new Vector2(1f, 1f);
            battleRow.pivot = new Vector2(0f, 1f);
            battleRow.anchoredPosition = new Vector2(18f, -116f);
            battleRow.sizeDelta = new Vector2(-36f, 34f);

            bagHeader = BoardHudOverlayFactory.CreateText(
                transform, "BagHeader", "背包", font, 15, FontStyle.Bold,
                new Vector2(18f, -292f), new Vector2(PanelWidth - 36f, 22f), TextAnchor.UpperLeft);
            bagHeader.color = Brass;

            GameObject barObject = BoardHudOverlayFactory.CreateBar(
                transform, "BagBar",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -320f), new Vector2(PanelWidth - 18f, -306f),
                BarColor);
            bagFill = BoardHudOverlayFactory.CreateBar(
                barObject.transform, "Fill",
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                Vector2.zero, Vector2.zero, BarFill).GetComponent<RectTransform>();

            bagFooter = BoardHudOverlayFactory.CreateText(
                transform, "BagFooter", string.Empty, font, 13, FontStyle.Normal,
                new Vector2(18f, -330f), new Vector2(PanelWidth - 36f, 60f), TextAnchor.UpperLeft);
            bagFooter.color = MutedColor;

            GameObject closeObject = BoardHudOverlayFactory.CreateBar(
                transform, "Close",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-118f, -14f), new Vector2(-14f, -44f),
                new Color(0.24f, 0.20f, 0.10f, 1f));
            closeButton = closeObject.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.None;
            closeButton.onClick.AddListener(() => onClose?.Invoke());
            Text closeLabel = BoardHudOverlayFactory.CreateText(
                closeObject.transform, "Label", "关闭", font, 14, FontStyle.Bold,
                new Vector2(0f, -6f), new Vector2(104f, 22f), TextAnchor.MiddleCenter);
            closeLabel.color = TextColor;
        }

        public void Show(MatchPlayerState player)
        {
            if (player == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            title.text = string.Concat("P", (player.Id + 1).ToString(), "  ", player.Name);
            subtitle.text = string.Concat(
                "Lv.", player.Level.ToString(),
                "    碎片 ", player.Shards.ToString(),
                "    胜场 ", player.Wins.ToString());

            int capacity = player.Level >= 6 ? 10 : LoadoutRules.GetBattleCapacity(player.Level);
            battleHeader.text = string.Concat(
                "战斗栏 ", player.BattleCardWidth.ToString(), "c / ", capacity.ToString(), "c");

            for (int i = battleRow.childCount - 1; i >= 0; i--)
            {
                Destroy(battleRow.GetChild(i).gameObject);
            }

            float x = 0f;
            foreach (CardInstance card in player.BattleCards)
            {
                float width = 30f * card.Definition.SlotSize;
                CreateBattleChip(card, x, width);
                x += width + 6f;
            }

            int remaining = capacity - player.BattleCardWidth;
            for (int i = 0; i < remaining; i++)
            {
                CreateLockedChip(x);
                x += 36f;
            }

            List<CardInstance> bag = player.OwnedCards.Where(card => !player.IsEquipped(card)).ToList();
            int bagWidth = bag.Sum(card => card.Definition.SlotSize);
            int bagCount = bag.Count;
            int bagValue = player.OwnedCards.Sum(card => card.Value);

            bagHeader.text = string.Concat(
                "背包 ", bagWidth.ToString(), "c / ", LoadoutRules.BagCapacity.ToString(), "c",
                "  ·  ", bagCount.ToString(), " 张",
                "  ·  总价值 ", bagValue.ToString());
            bagFooter.text = "（具体牌不公开：这里只有占用量与总价值）";

            float ratio = Mathf.Clamp01((float)bagWidth / LoadoutRules.BagCapacity);
            bagFill.anchorMin = new Vector2(0f, 0f);
            bagFill.anchorMax = new Vector2(ratio <= 0f ? 0f : ratio, 1f);
            bagFill.offsetMin = Vector2.zero;
            bagFill.offsetMax = Vector2.zero;
        }

        private void CreateBattleChip(CardInstance card, float x, float width)
        {
            GameObject chip = BoardHudOverlayFactory.CreateBar(
                battleRow, "Chip_" + card.Definition.Id,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -30f), new Vector2(x + width, 0f),
                new Color(0.30f, 0.20f, 0.12f, 1f));
            Text name = BoardHudOverlayFactory.CreateText(
                chip.transform, "Name", card.Definition.DisplayName, font, 12, FontStyle.Bold,
                new Vector2(5f, -4f), new Vector2(width - 10f, 18f), TextAnchor.UpperLeft);
            name.color = TextColor;
            Text meta = BoardHudOverlayFactory.CreateText(
                chip.transform, "Meta", CardText.Seconds(card.Definition.CooldownTicks), font, 10,
                FontStyle.Normal, new Vector2(5f, -19f), new Vector2(width - 10f, 16f), TextAnchor.UpperLeft);
            meta.color = MutedColor;
        }

        private void CreateLockedChip(float x)
        {
            GameObject chip = BoardHudOverlayFactory.CreateBar(
                battleRow, "Lock",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -30f), new Vector2(x + 30f, 0f),
                new Color(0.12f, 0.13f, 0.12f, 1f));
            Text label = BoardHudOverlayFactory.CreateText(
                chip.transform, "Label", "🔒", font, 12, FontStyle.Normal,
                new Vector2(0f, -6f), new Vector2(30f, 18f), TextAnchor.MiddleCenter);
            label.color = MutedColor;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 悬停 tooltip（§6.1 双通道的"轻"通道）。单击仍然是详情面板。
    /// </summary>
    public sealed class CardTooltipView : MonoBehaviour
    {
        private const float Width = 320f;
        private const float Height = 176f;

        private static readonly Color PanelColor = new Color(0.03f, 0.05f, 0.06f, 0.98f);
        private static readonly Color Brass = new Color(0.92f, 0.69f, 0.27f, 1f);
        private static readonly Color TextColor = new Color(0.86f, 0.86f, 0.82f, 1f);

        private RectTransform root;
        private Text title;
        private Text meta;
        private Text body;
        private Font font;

        public static CardTooltipView Create(RectTransform parent, Font font)
        {
            GameObject rootObject = new GameObject("CardTooltip", typeof(RectTransform));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(Width, Height);

            CardTooltipView view = rootObject.AddComponent<CardTooltipView>();
            view.root = rect;
            view.font = font;
            view.Build();
            rootObject.SetActive(false);
            return view;
        }

        private void Build()
        {
            Image background = gameObject.AddComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            title = BoardHudOverlayFactory.CreateText(
                transform, "Title", string.Empty, font, 18, FontStyle.Bold,
                new Vector2(14f, -10f), new Vector2(Width - 28f, 24f), TextAnchor.UpperLeft);
            title.color = Brass;

            meta = BoardHudOverlayFactory.CreateText(
                transform, "Meta", string.Empty, font, 13, FontStyle.Normal,
                new Vector2(14f, -36f), new Vector2(Width - 28f, 20f), TextAnchor.UpperLeft);
            meta.color = new Color(0.66f, 0.68f, 0.66f, 1f);

            body = BoardHudOverlayFactory.CreateText(
                transform, "Body", string.Empty, font, 13, FontStyle.Normal,
                new Vector2(14f, -58f), new Vector2(Width - 28f, Height - 68f), TextAnchor.UpperLeft);
            body.color = TextColor;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        public void Show(CardInstance card, Vector2 screenPosition, RectTransform canvas)
        {
            if (card == null)
            {
                return;
            }

            gameObject.SetActive(true);
            title.text = string.Concat(card.Definition.DisplayName, "   ", card.Definition.SlotSize.ToString(), "c");
            meta.text = CardText.Meta(card.Definition);
            body.text = CardText.Description(card.Definition);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas, screenPosition, null, out Vector2 local);
            float width = canvas.rect.width;
            float height = canvas.rect.height;
            float x = Mathf.Clamp(local.x + 22f, 0f, Mathf.Max(0f, width - Width));
            float y = Mathf.Clamp(local.y + 22f, 0f, Mathf.Max(0f, height - Height));
            root.anchoredPosition = new Vector2(x, y);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 暂停面板（Esc）：继续 / 重开本局。暂停只遮输入，不改对局状态——内核是纯函数，没有"暂停"这个概念。
    /// </summary>
    public sealed class PauseOverlayView : MonoBehaviour
    {
        private static readonly Color ScrimColor = new Color(0.012f, 0.02f, 0.024f, 0.72f);
        private static readonly Color PanelColor = new Color(0.048f, 0.078f, 0.09f, 0.99f);
        private static readonly Color Brass = new Color(0.92f, 0.69f, 0.27f, 1f);
        private static readonly Color TextColor = new Color(0.94f, 0.93f, 0.88f, 1f);

        private Font font;
        private Button resumeButton;
        private Text cardScaleValue;
        private Button pathToggleButton;
        private Text pathToggleLabel;
        private bool pathsVisible = true;

        public bool IsOpen => gameObject.activeSelf;

        public static PauseOverlayView Create(
            RectTransform parent,
            Font font,
            System.Action onResume,
            System.Action<float> onCardScale,
            System.Action<bool> onPathsVisible)
        {
            GameObject rootObject = new GameObject(
                "PauseOverlay",
                typeof(RectTransform),
                typeof(Image));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image scrim = rootObject.GetComponent<Image>();
            scrim.color = ScrimColor;
            scrim.raycastTarget = true;

            PauseOverlayView view = rootObject.AddComponent<PauseOverlayView>();
            view.font = font;
            view.Build(onResume, onCardScale, onPathsVisible);
            rootObject.SetActive(false);
            return view;
        }

        private void Build(
            System.Action onResume,
            System.Action<float> onCardScale,
            System.Action<bool> onPathsVisible)
        {
            GameObject panelObject = BoardHudOverlayFactory.CreateBar(
                transform, "Panel",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-250f, -230f), new Vector2(250f, 230f),
                PanelColor);

            Text title = BoardHudOverlayFactory.CreateText(
                panelObject.transform, "Title", "已暂停", font, 28, FontStyle.Bold,
                new Vector2(0f, -36f), new Vector2(460f, 36f), TextAnchor.MiddleCenter);
            title.color = Brass;

            Text hint = BoardHudOverlayFactory.CreateText(
                panelObject.transform, "Hint", "Esc 继续 / 关闭菜单", font, 13, FontStyle.Normal,
                new Vector2(0f, -72f), new Vector2(460f, 22f), TextAnchor.MiddleCenter);
            hint.color = new Color(0.66f, 0.68f, 0.66f, 1f);

            resumeButton = MakeButton(panelObject.transform, "继续游戏", -110f, onResume);
            MakeButton(panelObject.transform, "重开本局", -170f, RestartScene);
            MakeButton(panelObject.transform, "退出游戏", -230f, QuitGame);

            BoardHudOverlayFactory.CreateText(
                panelObject.transform, "SettingsTitle", "设置", font, 16, FontStyle.Bold,
                new Vector2(-200f, -296f), new Vector2(120f, 24f), TextAnchor.MiddleLeft)
                .color = new Color(0.72f, 0.68f, 0.52f, 1f);

            BoardHudOverlayFactory.CreateText(
                panelObject.transform, "CardScaleLabel", "卡槽大小", font, 14, FontStyle.Normal,
                new Vector2(-200f, -338f), new Vector2(96f, 24f), TextAnchor.MiddleLeft)
                .color = TextColor;

            Slider scaleSlider = MakeSlider(
                panelObject.transform, new Vector2(92f, -338f), new Vector2(210f, 26f),
                0.75f, 1.25f, 1f);
            cardScaleValue = BoardHudOverlayFactory.CreateText(
                panelObject.transform, "CardScaleValue", "100%", font, 14, FontStyle.Normal,
                new Vector2(196f, -338f), new Vector2(64f, 24f), TextAnchor.MiddleRight);
            cardScaleValue.color = Brass;
            scaleSlider.onValueChanged.AddListener(value =>
            {
                cardScaleValue.text = Mathf.RoundToInt(value * 100f) + "%";
                onCardScale?.Invoke(value);
            });

            BoardHudOverlayFactory.CreateText(
                panelObject.transform, "PathsLabel", "地图路径线", font, 14, FontStyle.Normal,
                new Vector2(-200f, -384f), new Vector2(110f, 24f), TextAnchor.MiddleLeft)
                .color = TextColor;

            pathToggleButton = MakeSmallButton(
                panelObject.transform, "显示", new Vector2(120f, -384f), new Vector2(120f, 30f), () =>
                {
                    pathsVisible = !pathsVisible;
                    pathToggleLabel.text = pathsVisible ? "显示" : "隐藏";
                    onPathsVisible?.Invoke(pathsVisible);
                });
            pathToggleLabel = pathToggleButton.GetComponentInChildren<Text>();
        }

        private Button MakeButton(Transform parent, string label, float offsetY, System.Action onClick)
        {
            GameObject buttonObject = BoardHudOverlayFactory.CreateBar(
                parent, "Button_" + label,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-92f, offsetY - 22f), new Vector2(92f, offsetY + 22f),
                new Color(0.89f, 0.64f, 0.20f, 1f));
            Button button = buttonObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick?.Invoke());
            Text text = BoardHudOverlayFactory.CreateText(
                buttonObject.transform, "Label", label, font, 15, FontStyle.Bold,
                new Vector2(0f, 0f), new Vector2(184f, 44f), TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.color = new Color(0.09f, 0.08f, 0.05f, 1f);
            return button;
        }

        private Button MakeSmallButton(
            Transform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            System.Action onClick)
        {
            GameObject buttonObject = BoardHudOverlayFactory.CreateBar(
                parent, "Button_" + label,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                anchoredPosition - size * 0.5f, anchoredPosition + size * 0.5f,
                new Color(0.89f, 0.64f, 0.20f, 0.92f));
            Button button = buttonObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick?.Invoke());
            Text text = BoardHudOverlayFactory.CreateText(
                buttonObject.transform, "Label", label, font, 14, FontStyle.Bold,
                Vector2.zero, size, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.color = new Color(0.09f, 0.08f, 0.05f, 1f);
            return button;
        }

        private Slider MakeSlider(
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            float minimum,
            float maximum,
            float value)
        {
            GameObject sliderObject = new GameObject(
                "Slider",
                typeof(RectTransform),
                typeof(Slider));
            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            GameObject background = BoardHudOverlayFactory.CreateBar(
                sliderObject.transform, "Background",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -5f), new Vector2(0f, 5f),
                new Color(0.16f, 0.20f, 0.22f, 1f));
            background.GetComponent<Image>().raycastTarget = false;

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.SetParent(sliderObject.transform, false);
            fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRect.offsetMin = new Vector2(0f, -5f);
            fillAreaRect.offsetMax = new Vector2(0f, 5f);

            GameObject fill = BoardHudOverlayFactory.CreateBar(
                fillArea.transform, "Fill",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero,
                new Color(0.89f, 0.64f, 0.20f, 0.95f));
            fill.GetComponent<Image>().raycastTarget = false;

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.SetParent(sliderObject.transform, false);
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            GameObject handle = BoardHudOverlayFactory.CreateBar(
                handleArea.transform, "Handle",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(-8f, -11f), new Vector2(8f, 11f),
                new Color(0.98f, 0.86f, 0.52f, 1f));

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.value = value;
            return slider;
        }

        private static void RestartScene()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (resumeButton != null)
            {
                resumeButton.Select();
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
