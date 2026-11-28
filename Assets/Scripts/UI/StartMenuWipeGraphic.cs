using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>固定位置的矩形擦除图元，用于悬停金线的自下而上显现。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StartMenuWipeGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 1f)] private float wipeStart;
        [SerializeField, Range(0f, 1f)] private float wipeEnd;

        public void SetWipe(float start, float end)
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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (wipeEnd <= wipeStart)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            float yMin = Mathf.Lerp(rect.yMin, rect.yMax, wipeStart);
            float yMax = Mathf.Lerp(rect.yMin, rect.yMax, wipeEnd);
            if (yMax <= yMin)
            {
                return;
            }

            AddVertex(vh, new Vector2(rect.xMin, yMin));
            AddVertex(vh, new Vector2(rect.xMin, yMax));
            AddVertex(vh, new Vector2(rect.xMax, yMax));
            AddVertex(vh, new Vector2(rect.xMax, yMin));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private void AddVertex(VertexHelper vh, Vector2 position)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vh.AddVert(vertex);
        }

        protected override void OnValidate()
        {
            SetVerticesDirty();
        }
    }
}
