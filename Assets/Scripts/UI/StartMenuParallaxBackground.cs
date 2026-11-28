using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 主菜单背景：按 cover 规则铺满画布，并跟随鼠标做很小的平滑位移。
    /// 额外放大 overscan，保证视差时不会露出边缘。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public sealed class StartMenuParallaxBackground : MonoBehaviour
    {
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private float overscan = 1.08f;
        [SerializeField] private float maxOffset = 18f;
        [SerializeField] private float smoothTime = 0.12f;

        private RectTransform rect;
        private Image image;
        private Vector2 currentOffset;
        private Vector2 velocity;
        private Vector2 lastCanvasSize;

        public void Configure(
            RectTransform targetCanvas,
            float targetOverscan,
            float targetMaxOffset,
            float targetSmoothTime)
        {
            canvasRect = targetCanvas;
            overscan = Mathf.Max(1f, targetOverscan);
            maxOffset = Mathf.Max(0f, targetMaxOffset);
            smoothTime = Mathf.Max(0.01f, targetSmoothTime);
            RefreshSize(true);
        }

        private void Awake()
        {
            rect = transform as RectTransform;
            image = GetComponent<Image>();
            if (canvasRect == null)
            {
                canvasRect = transform.parent as RectTransform;
            }
        }

        private void OnEnable()
        {
            if (rect == null)
            {
                rect = transform as RectTransform;
            }

            if (image == null)
            {
                image = GetComponent<Image>();
            }

            RefreshSize(true);
        }

        private void LateUpdate()
        {
            RefreshSize(false);
            if (maxOffset <= 0f)
            {
                return;
            }

            Vector2 normalized = new Vector2(
                Screen.width > 0 ? Input.mousePosition.x / Screen.width * 2f - 1f : 0f,
                Screen.height > 0 ? Input.mousePosition.y / Screen.height * 2f - 1f : 0f);
            normalized.x = Mathf.Clamp(normalized.x, -1f, 1f);
            normalized.y = Mathf.Clamp(normalized.y, -1f, 1f);

            Vector2 target = new Vector2(-normalized.x, -normalized.y) * maxOffset;
            currentOffset = Vector2.SmoothDamp(
                currentOffset,
                target,
                ref velocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            rect.anchoredPosition = currentOffset;
        }

        private void RefreshSize(bool force)
        {
            if (rect == null)
            {
                rect = transform as RectTransform;
            }

            if (image == null)
            {
                image = GetComponent<Image>();
            }

            if (rect == null || image == null || canvasRect == null ||
                image.sprite == null)
            {
                return;
            }

            Vector2 canvasSize = canvasRect.rect.size;
            if (canvasSize.x <= 0f || canvasSize.y <= 0f)
            {
                return;
            }

            if (!force && canvasSize == lastCanvasSize)
            {
                return;
            }

            lastCanvasSize = canvasSize;
            Rect spriteRect = image.sprite.rect;
            if (spriteRect.width <= 0f || spriteRect.height <= 0f)
            {
                return;
            }

            float cover = Mathf.Max(
                canvasSize.x / spriteRect.width,
                canvasSize.y / spriteRect.height);
            cover *= Mathf.Max(1f, overscan);
            rect.sizeDelta = new Vector2(
                spriteRect.width * cover,
                spriteRect.height * cover);
        }
    }
}
