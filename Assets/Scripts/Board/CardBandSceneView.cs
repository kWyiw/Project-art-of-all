using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// Editor-authored shell for the persistent card band. Card cell contents remain
    /// data-driven at runtime, but every stable panel, row, label and drop zone is a
    /// real serialized scene component.
    /// </summary>
    public sealed class CardBandSceneView : MonoBehaviour
    {
        [SerializeField] private RectTransform bandRoot;
        [SerializeField] private RectTransform bagRow;
        [SerializeField] private RectTransform battleRow;
        [SerializeField] private RectTransform sellZone;
        [SerializeField] private Image sellImage;
        [SerializeField] private Text sellLabel;
        [SerializeField] private Text bagLabel;
        [SerializeField] private Text battleLabel;
        [SerializeField] private Text battleCapacityLabel;
        [SerializeField] private Text handleLabel;
        [SerializeField] private Font font;

        internal RectTransform BandRoot => bandRoot;
        internal RectTransform BagRow => bagRow;
        internal RectTransform BattleRow => battleRow;
        internal RectTransform SellZone => sellZone;
        internal Image SellImage => sellImage;
        internal Text SellLabel => sellLabel;
        internal Text BagLabel => bagLabel;
        internal Text BattleLabel => battleLabel;
        internal Text BattleCapacityLabel => battleCapacityLabel;
        internal Text HandleLabel => handleLabel;
        internal Font Font => font;

        public CardBandController CreateController(BoardGameplayController gameplay)
        {
            return CardBandController.Bind(gameplay, this);
        }

        public static CardBandSceneView Create(RectTransform parent, Font font)
        {
            GameObject rootObject = new GameObject(
                "CardBand",
                typeof(RectTransform),
                typeof(Image));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(1180f, CardBandController.BaseBandHeight);
            root.anchoredPosition = new Vector2(0f, 10f);

            Image background = rootObject.GetComponent<Image>();
            background.color = new Color(0.03f, 0.05f, 0.06f, 0.94f);
            background.raycastTarget = false;

            GameObject handleObject = BoardHudOverlayFactory.CreateBar(
                root,
                "Handle",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -30f),
                Vector2.zero,
                new Color(0.06f, 0.09f, 0.10f, 0.99f));
            Text handleLabel = BoardHudOverlayFactory.CreateText(
                handleObject.transform,
                "Label",
                "▾ 卡槽",
                font,
                16,
                FontStyle.Bold,
                new Vector2(18f, -2f),
                new Vector2(220f, 26f),
                TextAnchor.MiddleLeft);
            handleLabel.color = new Color(0.92f, 0.69f, 0.27f, 1f);
            handleObject.GetComponent<Image>().raycastTarget = true;
            handleObject.AddComponent<Button>().transition = Selectable.Transition.None;

            Text bagLabel = CreateRowLabel(root, "背包", font, false);
            Text battleLabel = CreateRowLabel(root, "战斗", font, true);
            Text battleCapacityLabel = CreateRowCaption(root, font);
            RectTransform bagRow = CreateRow(root, "Bag");
            RectTransform battleRow = CreateRow(root, "Battle");

            const float Top = 42f;
            const float CardHeight = CardBandController.BaseCardHeight;
            const float LabelColumn = 64f;
            const float Reserve = 136f;
            float battleTop = Top + CardHeight + 8f;
            bagLabel.rectTransform.anchoredPosition = new Vector2(14f, -(Top + 8f));
            bagRow.anchoredPosition = new Vector2(LabelColumn, -(Top + 2f));
            bagRow.sizeDelta = new Vector2(-Reserve - LabelColumn, CardHeight);
            battleLabel.rectTransform.anchoredPosition =
                new Vector2(14f, -battleTop - 4f);
            battleCapacityLabel.rectTransform.anchoredPosition =
                new Vector2(14f, -battleTop - 22f);
            battleRow.anchoredPosition = new Vector2(LabelColumn, -battleTop);
            battleRow.sizeDelta = new Vector2(-Reserve - LabelColumn, CardHeight);

            GameObject sellObject = BoardHudOverlayFactory.CreateBar(
                root,
                "SellZone",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-104f, -114f),
                new Vector2(-24f, -34f),
                new Color(0.20f, 0.16f, 0.10f, 0.96f));
            RectTransform sellZone = sellObject.GetComponent<RectTransform>();
            Image sellImage = sellObject.GetComponent<Image>();
            Text sellLabel = BoardHudOverlayFactory.CreateText(
                sellObject.transform,
                "Label",
                "出售",
                font,
                15,
                FontStyle.Bold,
                new Vector2(0f, -26f),
                new Vector2(80f, 22f),
                TextAnchor.MiddleCenter);
            sellLabel.color = new Color(0.66f, 0.68f, 0.66f, 1f);

            CardBandSceneView view = rootObject.AddComponent<CardBandSceneView>();
            view.bandRoot = root;
            view.bagRow = bagRow;
            view.battleRow = battleRow;
            view.sellZone = sellZone;
            view.sellImage = sellImage;
            view.sellLabel = sellLabel;
            view.bagLabel = bagLabel;
            view.battleLabel = battleLabel;
            view.battleCapacityLabel = battleCapacityLabel;
            view.handleLabel = handleLabel;
            view.font = font;
            return view;
        }

        private static RectTransform CreateRow(RectTransform parent, string name)
        {
            GameObject rowObject = new GameObject(name, typeof(RectTransform));
            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(0f, CardBandController.BaseCardHeight);
            return row;
        }

        private static Text CreateRowLabel(
            RectTransform parent,
            string label,
            Font font,
            bool battle)
        {
            Text text = BoardHudOverlayFactory.CreateText(
                parent,
                "Label" + label,
                label,
                font,
                15,
                FontStyle.Bold,
                new Vector2(14f, 0f),
                new Vector2(56f, 24f),
                TextAnchor.UpperLeft);
            text.color = battle
                ? new Color(0.92f, 0.69f, 0.27f, 1f)
                : new Color(0.66f, 0.68f, 0.66f, 1f);
            return text;
        }

        private static Text CreateRowCaption(RectTransform parent, Font font)
        {
            Text caption = BoardHudOverlayFactory.CreateText(
                parent,
                "Caption",
                "能力",
                font,
                12,
                FontStyle.Normal,
                new Vector2(14f, 0f),
                new Vector2(56f, 20f),
                TextAnchor.UpperLeft);
            caption.color = new Color(0.66f, 0.68f, 0.66f, 1f);
            return caption;
        }
    }
}
