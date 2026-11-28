using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 左侧垂直、右侧斜切的梯形 UI 图元。
    /// 支持水平裁切显示，用固定的图形位置做从左到右的擦除动画。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TrapezoidGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        [SerializeField] private float rightSlant = 16f;
        [SerializeField, Range(0f, 1f)] private float wipeStart;
        [SerializeField, Range(0f, 1f)] private float wipeEnd = 1f;

        private readonly List<Vector2> polygon = new List<Vector2>(4);
        private readonly List<Vector2> clipped = new List<Vector2>(6);

        public float RightSlant
        {
            get => rightSlant;
            set
            {
                if (Mathf.Approximately(rightSlant, value))
                {
                    return;
                }

                rightSlant = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public void SetHorizontalWipe(float start, float end)
        {
            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            if (Mathf.Approximately(wipeStart, start) &&
                Mathf.Approximately(wipeEnd, end))
            {
                return;
            }

            wipeStart = start;
            wipeEnd = end;
            SetVerticesDirty();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    screenPoint,
                    eventCamera,
                    out Vector2 local))
            {
                return false;
            }

            Rect rect = GetPixelAdjustedRect();
            if (local.x < rect.xMin || local.x > rect.xMax ||
                local.y < rect.yMin || local.y > rect.yMax)
            {
                return false;
            }

            float slant = Mathf.Min(rightSlant, rect.width * 0.45f);
            float t = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            float maxX = Mathf.Lerp(rect.xMax - slant, rect.xMax, t);
            return local.x <= maxX;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float slant = Mathf.Min(rightSlant, rect.width * 0.45f);

            polygon.Clear();
            polygon.Add(new Vector2(rect.xMin, rect.yMin));
            polygon.Add(new Vector2(rect.xMin, rect.yMax));
            polygon.Add(new Vector2(rect.xMax, rect.yMax));
            polygon.Add(new Vector2(rect.xMax - slant, rect.yMin));

            float minX = Mathf.Lerp(rect.xMin, rect.xMax, wipeStart);
            float maxX = Mathf.Lerp(rect.xMin, rect.xMax, wipeEnd);
            ClipVertical(minX, true);
            ClipVertical(maxX, false);

            if (clipped.Count < 3)
            {
                return;
            }

            for (int index = 0; index < clipped.Count; index++)
            {
                AddVertex(vh, clipped[index]);
            }

            for (int index = 1; index < clipped.Count - 1; index++)
            {
                vh.AddTriangle(0, index, index + 1);
            }
        }

        private void ClipVertical(float x, bool keepGreater)
        {
            clipped.Clear();
            if (polygon.Count == 0)
            {
                return;
            }

            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 current = polygon[index];
                Vector2 next = polygon[(index + 1) % polygon.Count];
                bool currentInside = keepGreater ? current.x >= x : current.x <= x;
                bool nextInside = keepGreater ? next.x >= x : next.x <= x;

                if (currentInside)
                {
                    clipped.Add(current);
                }

                if (currentInside == nextInside)
                {
                    continue;
                }

                float denominator = next.x - current.x;
                if (Mathf.Abs(denominator) < 0.0001f)
                {
                    continue;
                }

                float t = (x - current.x) / denominator;
                clipped.Add(Vector2.Lerp(current, next, Mathf.Clamp01(t)));
            }

            polygon.Clear();
            polygon.AddRange(clipped);
        }

        private void AddVertex(VertexHelper vh, Vector2 position)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vh.AddVert(vertex);
        }

        private new void OnValidate()
        {
            SetVerticesDirty();
        }
    }
}
