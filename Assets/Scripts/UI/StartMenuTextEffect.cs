using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// Legacy uGUI Text 的阴影、描边与逐字竖向渐变。
    /// 三者放在同一个 BaseMeshEffect 里，避免 Outline/Shadow 和渐变互相覆盖顶点颜色。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class StartMenuTextEffect : BaseMeshEffect
    {
        private static readonly Vector2[] OutlineDirections =
        {
            new Vector2(0f, 1f),
            new Vector2(0.7071068f, 0.7071068f),
            new Vector2(1f, 0f),
            new Vector2(0.7071068f, -0.7071068f),
            new Vector2(0f, -1f),
            new Vector2(-0.7071068f, -0.7071068f),
            new Vector2(-1f, 0f),
            new Vector2(-0.7071068f, 0.7071068f)
        };

        private static readonly Vector2[] ShadowDirections =
        {
            new Vector2(0f, 1f),
            new Vector2(0.7071068f, 0.7071068f),
            new Vector2(1f, 0f),
            new Vector2(0.7071068f, -0.7071068f),
            new Vector2(0f, -1f),
            new Vector2(-0.7071068f, -0.7071068f),
            new Vector2(-1f, 0f),
            new Vector2(-0.7071068f, 0.7071068f)
        };

        [SerializeField] private Color fillBottom = Color.white;
        [SerializeField] private Color fillTop = Color.white;
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Range(0f, 8f)] private float outlineWidth = 3f;
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.19f);
        [SerializeField] private Vector2 shadowOffset = new Vector2(3f, -4f);
        [SerializeField, Range(0f, 8f)] private float shadowSoftness = 2.5f;
        [SerializeField] private bool drawShadow = true;
        [SerializeField] private bool drawOutline = true;

        private readonly List<UIVertex> baseVertices = new List<UIVertex>();
        private readonly UIVertex[] quad = new UIVertex[4];

        public void Configure(
            Color bottom,
            Color top,
            Color outline,
            float strokeWidth,
            Color shadow,
            Vector2 offset,
            float softness)
        {
            fillBottom = bottom;
            fillTop = top;
            outlineColor = outline;
            outlineWidth = Mathf.Max(0f, strokeWidth);
            shadowColor = shadow;
            shadowOffset = offset;
            shadowSoftness = Mathf.Max(0f, softness);
            MarkDirty();
        }

        public void SetFillColors(Color bottom, Color top)
        {
            if (fillBottom == bottom && fillTop == top)
            {
                return;
            }

            fillBottom = bottom;
            fillTop = top;
            MarkDirty();
        }

        private void MarkDirty()
        {
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
            {
                return;
            }

            if (graphic == null)
            {
                return;
            }

            baseVertices.Clear();
            for (int index = 0; index < vh.currentVertCount; index++)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vh.PopulateUIVertex(ref vertex, index);
                baseVertices.Add(vertex);
            }

            vh.Clear();
            int quadCount = baseVertices.Count / 4;

            if (drawShadow)
            {
                for (int index = 0; index < quadCount; index++)
                {
                    AddShadowQuads(vh, index * 4);
                }
            }

            if (drawOutline && outlineWidth > 0.01f)
            {
                for (int index = 0; index < quadCount; index++)
                {
                    AddOutlineQuads(vh, index * 4);
                }
            }

            for (int index = 0; index < quadCount; index++)
            {
                AddFillQuad(vh, index * 4);
            }
        }

        private void AddShadowQuad(
            VertexHelper vh,
            int start,
            Vector2 offset,
            Color colorValue)
        {
            Color32 color = colorValue;
            for (int index = 0; index < 4; index++)
            {
                quad[index] = baseVertices[start + index];
                quad[index].position += new Vector3(offset.x, offset.y, 0f);
                quad[index].color = color;
            }

            vh.AddUIVertexQuad(quad);
        }

        private void AddShadowQuads(VertexHelper vh, int start)
        {
            AddShadowQuad(vh, start, shadowOffset, shadowColor);
            if (shadowSoftness <= 0.01f)
            {
                return;
            }

            Color softColor = shadowColor;
            softColor.a *= 0.18f;
            Color farColor = shadowColor;
            farColor.a *= 0.06f;
            for (int index = 0; index < ShadowDirections.Length; index++)
            {
                Vector2 direction = ShadowDirections[index];
                AddShadowQuad(
                    vh,
                    start,
                    shadowOffset + direction * shadowSoftness,
                    softColor);
                AddShadowQuad(
                    vh,
                    start,
                    shadowOffset + direction * shadowSoftness * 2f,
                    farColor);
            }
        }

        private void AddOutlineQuads(VertexHelper vh, int start)
        {
            Color32 color = outlineColor;
            for (int direction = 0; direction < OutlineDirections.Length; direction++)
            {
                Vector2 offset = OutlineDirections[direction] * outlineWidth;
                for (int index = 0; index < 4; index++)
                {
                    quad[index] = baseVertices[start + index];
                    quad[index].position += new Vector3(offset.x, offset.y, 0f);
                    quad[index].color = color;
                }

                vh.AddUIVertexQuad(quad);
            }
        }

        private void AddFillQuad(VertexHelper vh, int start)
        {
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int index = 0; index < 4; index++)
            {
                float y = baseVertices[start + index].position.y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            float span = Mathf.Max(0.0001f, maxY - minY);
            for (int index = 0; index < 4; index++)
            {
                quad[index] = baseVertices[start + index];
                float t = Mathf.Clamp01((quad[index].position.y - minY) / span);
                quad[index].color = Color.Lerp(fillBottom, fillTop, t);
            }

            vh.AddUIVertexQuad(quad);
        }

        protected override void OnValidate()
        {
            MarkDirty();
        }
    }
}
