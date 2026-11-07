using System.Collections.Generic;
using System.Text;
using ProjectArtOfAll.Core.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 悬停卡牌的详情面板（§6.1 双通道的"轻"通道，单击仍然打开详情面板）。
    /// 版式对齐卡牌本体：中文名 + 英文名、尺寸/冷却/标签、逐条效果（前面一枚铜色小标记）、
    /// 一段关键词释义，文本里的关键词按语义上色（伤害红、护盾金、中毒与再生绿、灼烧橙…）。
    /// 高度按内容自适应，靠近屏幕边缘时翻到鼠标另一侧。
    /// </summary>
    public sealed class CardTooltipView : MonoBehaviour
    {
        private const float PanelWidth = 736f;
        private const float PaddingX = 32f;
        private const float PaddingTop = 28f;
        private const float PaddingBottom = 28f;
        private const float BadgeWidth = 124f;

        private static readonly Color PanelColor = new Color(0.075f, 0.068f, 0.058f, 0.985f);
        private static readonly Color FrameColor = new Color(0.85f, 0.63f, 0.25f, 0.95f);
        private static readonly Color TitleColor = new Color(0.97f, 0.94f, 0.86f, 1f);
        private static readonly Color EnglishColor = new Color(0.62f, 0.60f, 0.54f, 1f);
        private static readonly Color MetaColor = new Color(0.80f, 0.72f, 0.54f, 1f);
        private static readonly Color BodyColor = new Color(0.90f, 0.88f, 0.82f, 1f);
        private static readonly Color NoteColor = new Color(0.66f, 0.65f, 0.60f, 1f);
        private static readonly Color BulletColor = new Color(0.92f, 0.69f, 0.27f, 0.95f);
        private static readonly Color DividerColor = new Color(0.85f, 0.63f, 0.25f, 0.32f);

        private RectTransform root;
        private Font font;
        private readonly List<GameObject> generated = new List<GameObject>();
        private float cursorY;
        private float contentWidth;

        public static CardTooltipView Create(RectTransform parent, Font font)
        {
            GameObject rootObject = new GameObject("CardTooltip", typeof(RectTransform), typeof(Image));
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(PanelWidth, 120f);

            Image background = rootObject.GetComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;

            Outline frame = rootObject.AddComponent<Outline>();
            frame.effectColor = FrameColor;
            frame.effectDistance = new Vector2(1.5f, -1.5f);

            CardTooltipView view = rootObject.AddComponent<CardTooltipView>();
            view.root = rect;
            view.font = font;
            rootObject.SetActive(false);
            return view;
        }

        public void Show(
            CardDefinition card,
            Vector2 screenPosition,
            RectTransform canvas,
            Camera screenCamera = null)
        {
            if (card == null || canvas == null)
            {
                return;
            }

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ClearGenerated();

            contentWidth = PanelWidth - PaddingX * 2f;
            cursorY = PaddingTop;

            AddText("Title", card.DisplayName, 42, TitleColor, FontStyle.Bold, 6f,
                contentWidth - BadgeWidth, 0f, TextAnchor.UpperLeft);
            AddBadge(card.SlotSize.ToString() + "c");

            string english = CardTooltipText.EnglishName(card.Id);
            if (!string.IsNullOrEmpty(english))
            {
                AddText("English", english, 24, EnglishColor, FontStyle.Normal, 6f,
                    contentWidth - BadgeWidth, 0f, TextAnchor.UpperLeft);
            }

            string meta = string.Concat(
                CardText.Meta(card),
                " · 价值 ",
                card.Value.ToString());
            AddText("Meta", meta, 24, MetaColor, FontStyle.Normal, 0f,
                contentWidth, 0f, TextAnchor.UpperLeft);

            bool hasEffects = card.ActiveEffects != null && card.ActiveEffects.Count > 0;
            bool hasPassives = (card.Auras != null && card.Auras.Count > 0)
                || (card.Passives != null && card.Passives.Count > 0);

            float effectsTop = cursorY;

            if (hasEffects)
            {
                cursorY += 16f;
                foreach (CardEffect effect in card.ActiveEffects)
                {
                    AddBulletRow(CardTooltipText.Colorize(CardText.Effect(effect)));
                }
            }

            if (hasPassives)
            {
                cursorY += hasEffects ? 12f : 16f;
                if (card.Auras != null)
                {
                    foreach (AuraDefinition aura in card.Auras)
                    {
                        AddBulletRow(CardTooltipText.Colorize(
                            string.Concat("光环 · ", CardText.Aura(aura))));
                    }
                }

                if (card.Passives != null)
                {
                    foreach (PassiveDefinition passive in card.Passives)
                    {
                        AddBulletRow(CardTooltipText.Colorize(
                            string.Concat("被动 · ", CardText.Passive(passive))));
                    }
                }
            }

            if (hasEffects || hasPassives)
            {
                AddInnerBox(effectsTop + 4f, cursorY - 8f);
            }

            if (!hasEffects && !hasPassives)
            {
                AddDivider(12f);
                AddText("Plain", "这张牌没有触发效果。", 26, BodyColor, FontStyle.Normal, 0f,
                    contentWidth, 16f, TextAnchor.UpperLeft);
            }

            List<string> glossary = CardTooltipText.Glossary(card);
            if (glossary.Count > 2)
            {
                // 规格书 UI-6.2.1：词条解释最多 2 条，其余交给详情面板。
                glossary = glossary.GetRange(0, 2);
            }

            if (glossary.Count > 0)
            {
                AddDivider(16f);
                foreach (string line in glossary)
                {
                    AddText("Note", CardTooltipText.Colorize(line), 22, NoteColor, FontStyle.Normal, 4f,
                        contentWidth, 10f, TextAnchor.UpperLeft);
                }
            }

            float height = Mathf.Max(cursorY + PaddingBottom, 192f);
            root.sizeDelta = new Vector2(PanelWidth, height);
            PlaceAt(screenPosition, canvas, height, screenCamera);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>面板已经弹出、指针还在同一张牌上挪动时，只挪位置不重建内容。</summary>
        public void Follow(Vector2 screenPosition, RectTransform canvas, Camera screenCamera = null)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            PlaceAt(screenPosition, canvas, root.sizeDelta.y, screenCamera);
        }

        public bool IsShown => gameObject.activeSelf;

        private void OnDisable()
        {
            ClearGenerated();
        }

        private void PlaceAt(
            Vector2 screenPosition,
            RectTransform canvas,
            float height,
            Camera screenCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas, screenPosition, screenCamera, out Vector2 local))
            {
                return;
            }

            // 本地坐标是相对 canvas 轴心的，先换算成左下角原点的坐标系再夹取。
            float width = canvas.rect.width;
            float canvasHeight = canvas.rect.height;
            float x = local.x + canvas.rect.width * canvas.pivot.x;
            float y = local.y + canvas.rect.height * canvas.pivot.y;

            float offset = 40f;
            float targetX = x + offset;
            if (targetX + PanelWidth > width - 8f)
            {
                targetX = x - PanelWidth - offset;
            }

            float targetY = y + 32f;
            if (targetY + height > canvasHeight - 8f)
            {
                targetY = y - height - 32f;
            }

            root.anchoredPosition = new Vector2(
                Mathf.Clamp(targetX, 8f, Mathf.Max(8f, width - PanelWidth - 8f)),
                Mathf.Clamp(targetY, 8f, Mathf.Max(8f, canvasHeight - height - 8f)));
        }

        private void AddBadge(string content)
        {
            Text badge = BoardHudOverlayFactory.CreateText(
                transform,
                "Badge",
                content,
                font,
                30,
                FontStyle.Bold,
                new Vector2(-PaddingX, -PaddingTop),
                new Vector2(BadgeWidth, 44f),
                TextAnchor.UpperRight);
            badge.rectTransform.anchorMin = new Vector2(1f, 1f);
            badge.rectTransform.anchorMax = new Vector2(1f, 1f);
            badge.rectTransform.pivot = new Vector2(1f, 1f);
            badge.rectTransform.anchoredPosition = new Vector2(-PaddingX, -PaddingTop);
            badge.color = BulletColor;
            badge.raycastTarget = false;
            generated.Add(badge.gameObject);
        }

        private void AddBulletRow(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            BoardHudOverlayFactory.CreateBar(
                transform,
                "Bullet",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PaddingX, -(cursorY + 8f)),
                new Vector2(PaddingX + 6f, -(cursorY + 34f)),
                BulletColor);
            AddText("Row", content, 26, BodyColor, FontStyle.Normal, 10f,
                contentWidth - 32f, 24f, TextAnchor.UpperLeft);
        }

        private Text AddText(
            string name,
            string content,
            int size,
            Color color,
            FontStyle style,
            float gap,
            float width,
            float indent,
            TextAnchor alignment)
        {
            if (string.IsNullOrEmpty(content))
            {
                return null;
            }

            float textWidth = Mathf.Max(80f, width - indent);
            Text text = BoardHudOverlayFactory.CreateText(
                transform,
                name,
                content,
                font,
                size,
                style,
                new Vector2(PaddingX + indent, -cursorY),
                new Vector2(textWidth, 20f),
                alignment);
            text.supportRichText = true;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.08f;

            text.rectTransform.sizeDelta = new Vector2(textWidth, text.preferredHeight);
            text.rectTransform.anchoredPosition = new Vector2(PaddingX + indent, -cursorY);
            generated.Add(text.gameObject);

            cursorY += Mathf.Max(text.preferredHeight, 28f) + gap;
            // 上一行创建的 bullet 条也记进 generated（由 AddBulletRow 负责），这里统一清理文本即可。
            return text;
        }

        private void AddDivider(float topGap)
        {
            cursorY += topGap;
            generated.Add(BoardHudOverlayFactory.CreateBar(
                transform,
                "Divider",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PaddingX + 12f, -(cursorY + 2f)),
                new Vector2(PaddingX + contentWidth - 12f, -cursorY),
                DividerColor));
            cursorY += 16f;
        }

        /// <summary>效果区背后垫一层浅色内框，把「这块是效果」与标题、词条分开（参考图的红框区域）。</summary>
        private void AddInnerBox(float top, float bottom)
        {
            float height = bottom - top;
            if (height <= 8f)
            {
                return;
            }

            GameObject box = BoardHudOverlayFactory.CreateBar(
                transform,
                "InnerBox",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(PaddingX - 4f, -top),
                new Vector2(PaddingX + contentWidth + 4f, -bottom),
                new Color(0.22f, 0.19f, 0.15f, 0.55f));
            box.transform.SetSiblingIndex(1);
            generated.Add(box);
        }

        private void ClearGenerated()
        {
            for (int index = 0; index < generated.Count; index++)
            {
                if (generated[index] != null)
                {
                    Destroy(generated[index]);
                }
            }

            generated.Clear();
        }
    }

    /// <summary>
    /// 工具提示的文案层：英文名、关键词上色、关键词释义。全部从 <see cref="CardDefinition"/> 推导，
    /// 卡牌数据加一张不需要改这里。
    /// </summary>
    internal static class CardTooltipText
    {
        private const string DamageColor = "#E8705F";
        private const string ShieldColor = "#E7C05C";
        private const string PoisonColor = "#83CF6B";
        private const string BurnColor = "#EF9152";
        private const string HealColor = "#8FDC9F";
        private const string RegenerationColor = "#6FE3B4";
        private const string CooldownColor = "#6EC6E6";
        private const string SlowColor = "#7EA7EC";
        private const string MultiplyColor = "#BE8CEB";
        private const string DiceColor = "#7FCBBA";
        private const string ValueColor = "#E7C05C";

        private static readonly string[] KeywordOrder =
        {
            "生命再生", "再生", "多重释放", "额外施放", "骰值", "骰数", "剧毒", "中毒", "灼烧", "燃烧",
            "护盾", "格挡", "治疗", "充能", "加速", "缓速", "摧毁", "销毁", "价值", "修正"
        };

        private static readonly Dictionary<string, string> KeywordColors =
            new Dictionary<string, string>
            {
                { "生命再生", HealColor },
                { "多重释放", MultiplyColor },
                { "额外施放", MultiplyColor },
                { "骰值", DiceColor },
                { "骰数", DiceColor },
                { "修正", DiceColor },
                { "再生", RegenerationColor },
                { "剧毒", PoisonColor },
                { "中毒", PoisonColor },
                { "灼烧", BurnColor },
                { "燃烧", BurnColor },
                { "护盾", ShieldColor },
                { "格挡", ShieldColor },
                { "治疗", HealColor },
                { "充能", CooldownColor },
                { "加速", CooldownColor },
                { "缓速", SlowColor },
                { "摧毁", "#9C9A92" },
                { "销毁", "#9C9A92" },
                { "价值", ValueColor },
                { "伤害", DamageColor }
            };

        /// <summary>把关键字包上颜色与粗体；单次扫描，避免把已插入的富文本再替换一遍。</summary>
        public static string Colorize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length + 32);
            int index = 0;
            while (index < text.Length)
            {
                string matched = MatchAt(text, index);
                if (matched != null)
                {
                    builder.Append("<color=").Append(KeywordColors[matched]).Append("><b>")
                        .Append(matched).Append("</b></color>");
                    index += matched.Length;
                    continue;
                }

                builder.Append(text[index]);
                index++;
            }

            return builder.ToString();
        }

        private static string MatchAt(string text, int index)
        {
            for (int order = 0; order < KeywordOrder.Length; order++)
            {
                string keyword = KeywordOrder[order];
                if (string.CompareOrdinal(text, index, keyword, 0, keyword.Length) == 0)
                {
                    return keyword;
                }
            }

            return null;
        }

        /// <summary>`nuclear_water` → `Nuclear Water`。</summary>
        public static string EnglishName(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return string.Empty;
            }

            string[] parts = id.Split('_');
            StringBuilder builder = new StringBuilder(id.Length + parts.Length);
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index];
                if (part.Length == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    builder.Append(part.Substring(1));
                }
            }

            return builder.ToString();
        }


        public static List<string> Glossary(CardDefinition card)
        {
            List<string> lines = new List<string>();
            string text = CardText.Description(card);
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            if (text.Contains("中毒") || text.Contains("剧毒"))
            {
                lines.Add("中毒：每秒失去「层数」点生命，无视护盾。");
            }

            if (text.Contains("灼烧") || text.Contains("燃烧"))
            {
                lines.Add("灼烧：每 0.5 秒受到「层数」点伤害，随后层数 −1。");
            }

            if (text.Contains("再生"))
            {
                lines.Add("再生：每秒恢复「层数」点生命。");
            }

            if (text.Contains("护盾") || text.Contains("格挡"))
            {
                lines.Add("护盾：先于生命被扣除，战斗结束后清零。");
            }

            if (text.Contains("充能"))
            {
                lines.Add("充能：立刻把冷却读秒推进 N 秒。");
            }

            if (text.Contains("加速"))
            {
                lines.Add("加速：持续期间冷却读秒速度翻倍。");
            }

            if (text.Contains("缓速"))
            {
                lines.Add("缓速：持续期间冷却读秒速度减半。");
            }

            if (text.Contains("多重释放") || text.Contains("额外施放"))
            {
                lines.Add("多重释放：冷却走完后连续施放 N 次。");
            }

            if (text.Contains("摧毁"))
            {
                lines.Add("摧毁：本场战斗内不再生效。");
            }

            return lines;
        }
    }

    /// <summary>每块画布上只留一个工具提示实例，谁悬停就更新它；悬停满 0.35 秒才弹出。</summary>
    public static class CardTooltipHost
    {
        private const float ShowDelay = 0.35f;

        private static CardTooltipView view;
        private static RectTransform hostedCanvas;
        private static CardDefinition visibleCard;
        private static CardDefinition pendingCard;
        private static Vector2 pendingPosition;
        private static RectTransform pendingCanvas;
        private static Font pendingFont;
        private static Camera pendingCamera;
        private static float pendingSince;
        private static TooltipDelayDriver driver;

        public static void Show(
            RectTransform canvas,
            Font font,
            CardDefinition card,
            Vector2 screenPosition,
            Camera screenCamera = null)
        {
            if (canvas == null || card == null)
            {
                return;
            }

            if (view != null
                && hostedCanvas == canvas
                && visibleCard == card
                && view.IsShown)
            {
                pendingCard = null;
                view.Follow(screenPosition, canvas, screenCamera);
                return;
            }

            pendingCard = card;
            pendingPosition = screenPosition;
            pendingCanvas = canvas;
            pendingFont = font;
            pendingCamera = screenCamera;
            pendingSince = Time.unscaledTime;
            EnsureDriver(canvas);
        }

        public static void Hide()
        {
            pendingCard = null;

            if (view != null)
            {
                visibleCard = null;
                view.Hide();
            }
        }

        /// <summary>把任意层级里的控件换算到它所在画布的根 RectTransform，tooltip 一律挂在这上面。</summary>
        public static RectTransform CanvasRect(Transform from)
        {
            if (from == null)
            {
                return null;
            }

            Canvas canvas = from.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Canvas root = canvas.rootCanvas;
                return (RectTransform)(root != null ? root.transform : canvas.transform);
            }

            return from as RectTransform;
        }

        private static void EnsureDriver(RectTransform canvas)
        {
            if (driver != null && driver.transform.parent == canvas)
            {
                return;
            }

            if (driver != null)
            {
                Object.Destroy(driver.gameObject);
            }

            GameObject host = new GameObject("CardTooltipDriver", typeof(RectTransform));
            host.transform.SetParent(canvas, false);
            driver = host.AddComponent<TooltipDelayDriver>();
            driver.transform.SetParent(canvas, false);
        }

        internal static void TickDelay()
        {
            if (pendingCard == null || pendingCanvas == null)
            {
                return;
            }

            if (Time.unscaledTime - pendingSince < ShowDelay)
            {
                return;
            }

            CardDefinition card = pendingCard;
            Vector2 position = pendingPosition;
            RectTransform canvas = pendingCanvas;
            Font font = pendingFont;
            Camera camera = pendingCamera;
            pendingCard = null;

            if (view == null || hostedCanvas != canvas)
            {
                if (view != null)
                {
                    Object.Destroy(view.gameObject);
                }

                if (canvas == null)
                {
                    return;
                }

                view = CardTooltipView.Create(canvas, font);
                hostedCanvas = canvas;
            }

            visibleCard = card;
            view.Show(card, position, canvas, camera);
        }
    }

    /// <summary>挂在画布上的小驱动，只负责把「悬停了多久」算出来。</summary>
    internal sealed class TooltipDelayDriver : MonoBehaviour
    {
        private void Update()
        {
            CardTooltipHost.TickDelay();
        }
    }

    /// <summary>给任意可点击的卡牌行补上「悬停看详情」：截图里的商店、奖励选择都用它。</summary>
    public sealed class CardHoverTarget : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler
    {
        private CardDefinition card;
        private RectTransform canvas;
        private Font font;
        private bool hovering;

        public void Bind(CardDefinition definition, RectTransform tooltipCanvas, Font tooltipFont)
        {
            card = definition;
            canvas = tooltipCanvas;
            font = tooltipFont;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (card == null)
            {
                return;
            }

            hovering = true;
            CardTooltipHost.Show(canvas, font, card, eventData.position, eventData.enterEventCamera);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!hovering)
            {
                return;
            }

            CardTooltipHost.Show(canvas, font, card, eventData.position, eventData.enterEventCamera);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            CardTooltipHost.Hide();
        }

        private void OnDisable()
        {
            if (hovering)
            {
                hovering = false;
                CardTooltipHost.Hide();
            }
        }
    }
}
