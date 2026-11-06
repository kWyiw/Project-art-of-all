using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 卡面占位图：1c / 2c / 3c 各一张，放在 Resources/CardPlaceholders 下。
    /// 图已经裁成对应槽位宽度的卡面比例，所以铺满卡面即可，不需要再做等比缩放。
    /// </summary>
    public static class CardArtLibrary
    {
        private const string Folder = "CardPlaceholders/";

        /// <summary>盖在占位图上的底板透明度：太低看不清字，太高又盖掉画。</summary>
        private const float PlateAlpha = 0.58f;

        private static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

        /// <summary>取某个槽位宽度的占位卡图（1/2/3c），没有资源时返回 null。</summary>
        public static Sprite For(int slotSize)
        {
            int slots = slotSize <= 1 ? 1 : (slotSize >= 3 ? 3 : 2);
            if (cache.TryGetValue(slots, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = Load(slots);
            cache[slots] = sprite;
            return sprite;
        }

        /// <summary>
        /// 在卡面铺占位图，并在图上盖一层调暗的底板色，保证卡名/数值仍然看得清。
        /// 返回 true 表示画上去了。
        /// </summary>
        public static bool Attach(Transform parent, int slotSize, Color plateColor, float inset = 2f)
        {
            Sprite sprite = For(slotSize);
            if (sprite == null)
            {
                return false;
            }

            GameObject art = new GameObject("Art", typeof(RectTransform), typeof(Image));
            RectTransform artRect = art.GetComponent<RectTransform>();
            artRect.SetParent(parent, false);
            artRect.SetAsFirstSibling();
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = new Vector2(inset, inset);
            artRect.offsetMax = new Vector2(-inset, -inset);

            Image artImage = art.GetComponent<Image>();
            artImage.sprite = sprite;
            artImage.type = Image.Type.Simple;
            artImage.preserveAspect = false;
            artImage.raycastTarget = false;
            artImage.color = Color.white;

            GameObject scrim = new GameObject(
                "ArtScrim",
                typeof(RectTransform),
                typeof(Image));
            RectTransform scrimRect = scrim.GetComponent<RectTransform>();
            scrimRect.SetParent(parent, false);
            scrimRect.SetSiblingIndex(1);
            scrimRect.anchorMin = Vector2.zero;
            scrimRect.anchorMax = Vector2.one;
            scrimRect.offsetMin = new Vector2(inset, inset);
            scrimRect.offsetMax = new Vector2(-inset, -inset);

            Image scrimImage = scrim.GetComponent<Image>();
            scrimImage.color = new Color(plateColor.r, plateColor.g, plateColor.b, PlateAlpha);
            scrimImage.raycastTarget = false;
            return true;
        }

        private static Sprite Load(int slots)
        {
            string file = "placeholder_" + slots + "c";

            // 导入设置可能是 Sprite 也可能是 Default(Texture)，两种都接住。
            Sprite imported = Resources.Load<Sprite>(Folder + file);
            if (imported != null)
            {
                return imported;
            }

            Texture2D texture = Resources.Load<Texture2D>(Folder + file);
            if (texture == null)
            {
                Debug.LogWarning("[Cards] 缺少占位卡图：" + Folder + file);
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
