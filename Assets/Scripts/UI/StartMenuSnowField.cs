using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 主菜单轻雪：三层预分配雪花共用一张软圆贴图，单 Graphic 单 Draw Call。
    /// 运行时按 unscaledDeltaTime 推进；编辑器预览可重置到固定时间点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartMenuSnowField : MaskableGraphic
    {
        private const int FarCount = 200;
        private const int MidCount = 125;
        private const int NearCount = 25;
        private const int TotalCount = FarCount + MidCount + NearCount;
        private const int DefaultSeed = 20261128;
        private const float FallbackWidth = 1920f;
        private const float FallbackHeight = 1080f;
        private const float WrapMargin = 48f;
        private const float PreviewStep = 1f / 60f;
        private const int SoftDotSize = 64;

        private struct Flake
        {
            public Vector2 position;
            public float size;
            public float alpha;
            public float fallSpeed;
            public float swayAmplitude;
            public float swayFrequency;
            public float phase;
            public float parallax;
            public float windFactor;
            public int layer;
        }

        private static readonly Color32 SnowColor = new Color32(255, 255, 255, 255);

        [SerializeField] private int seed = DefaultSeed;
        [SerializeField] private float parallaxSmoothTime = 0.12f;

        private readonly Flake[] flakes = new Flake[TotalCount];
        private readonly UIVertex[] quad = new UIVertex[4];
        private Texture2D softDot;
        private System.Random wrapRandom;
        private Vector2 parallaxOffset;
        private Vector2 parallaxVelocity;
        private Vector2 lastSize;
        private float fieldTime;
        private bool initialized;
        private bool previewMode;

        public int ParticleCount => TotalCount;
        public int FarParticleCount => FarCount;
        public int MidParticleCount => MidCount;
        public int NearParticleCount => NearCount;

        public override Texture mainTexture
        {
            get
            {
                EnsureSoftDot();
                return softDot;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            color = Color.white;
            EnsureSoftDot();
            if (!initialized)
            {
                ResetToInitial();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            EnsureSoftDot();
            if (!initialized)
            {
                ResetToInitial();
            }

            SetVerticesDirty();
        }

        protected override void OnDestroy()
        {
            if (softDot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(softDot);
                }
                else
                {
                    DestroyImmediate(softDot);
                }

                softDot = null;
            }

            base.OnDestroy();
        }

        public void Configure(float smoothTime)
        {
            parallaxSmoothTime = Mathf.Max(0.01f, smoothTime);
            if (!initialized)
            {
                ResetToInitial();
            }
        }

        /// <summary>编辑器预览：重置固定种子状态后推进到指定秒数。</summary>
        public void SetPreviewTime(float seconds)
        {
            previewMode = true;
            ResetToInitial();

            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0.0001f)
            {
                float step = Mathf.Min(PreviewStep, remaining);
                Advance(step);
                remaining -= step;
            }

            SetVerticesDirty();
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            if (!initialized)
            {
                ResetToInitial();
            }

            Vector2 size = GetFieldSize();
            if (size != lastSize)
            {
                ResetToInitial();
            }

            UpdateParallax(deltaTime);
            Advance(deltaTime);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!initialized)
            {
                ResetToInitial();
            }

            Rect rect = rectTransform.rect;
            Vector2 size = GetFieldSize();
            Vector2 center = new Vector2(
                rect.xMin + size.x * 0.5f,
                rect.yMin + size.y * 0.5f);

            for (int index = 0; index < flakes.Length; index++)
            {
                Flake flake = flakes[index];
                float sway = Mathf.Sin(
                    fieldTime * flake.swayFrequency + flake.phase) *
                    flake.swayAmplitude;
                Vector2 position = flake.position + center;
                position.x += sway + parallaxOffset.x * flake.parallax;
                position.y += parallaxOffset.y * flake.parallax;

                float alpha = flake.alpha;
                if (flake.layer == 2)
                {
                    alpha *= GetNearProtection(position - center, size);
                }
                else if (flake.layer == 1)
                {
                    alpha *= GetMidProtection(position - center, size);
                }

                if (alpha <= 0.002f)
                {
                    continue;
                }

                float half = flake.size * 0.5f;
                Color32 color32 = new Color32(
                    SnowColor.r,
                    SnowColor.g,
                    SnowColor.b,
                    (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));

                quad[0] = MakeVertex(position + new Vector2(-half, -half), 0f, 0f, color32);
                quad[1] = MakeVertex(position + new Vector2(-half, half), 0f, 1f, color32);
                quad[2] = MakeVertex(position + new Vector2(half, half), 1f, 1f, color32);
                quad[3] = MakeVertex(position + new Vector2(half, -half), 1f, 0f, color32);
                vh.AddUIVertexQuad(quad);
            }
        }

        private static UIVertex MakeVertex(
            Vector2 position,
            float u,
            float v,
            Color32 color32)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = new Vector3(position.x, position.y, 0f);
            vertex.uv0 = new Vector4(u, v, 0f, 0f);
            vertex.color = color32;
            return vertex;
        }

        private void ResetToInitial()
        {
            fieldTime = 0f;
            parallaxOffset = Vector2.zero;
            parallaxVelocity = Vector2.zero;
            wrapRandom = new System.Random(seed);
            Vector2 size = GetFieldSize();
            lastSize = size;

            for (int index = 0; index < flakes.Length; index++)
            {
                int layer = index < FarCount
                    ? 0
                    : index < FarCount + MidCount ? 1 : 2;
                flakes[index] = CreateFlake(layer, size);
            }

            initialized = true;
            SetVerticesDirty();
        }

        private Flake CreateFlake(int layer, Vector2 size)
        {
            System.Random random = wrapRandom;
            Flake flake = new Flake
            {
                layer = layer,
                phase = Range(random, 0f, Mathf.PI * 2f),
                position = CreateSpawnPosition(layer, size, random)
            };

            if (layer == 0)
            {
                flake.size = Range(random, 1.5f, 2.8f);
                flake.alpha = Range(random, 0.24f, 0.38f);
                flake.fallSpeed = Range(random, 12f, 20f);
                flake.swayAmplitude = Range(random, 1f, 3f);
                flake.swayFrequency = Range(random, 0.35f, 0.75f);
                flake.parallax = 3f;
                flake.windFactor = 0.55f;
            }
            else if (layer == 1)
            {
                flake.size = Range(random, 3.2f, 5.5f);
                flake.alpha = Range(random, 0.38f, 0.55f);
                flake.fallSpeed = Range(random, 25f, 40f);
                flake.swayAmplitude = Range(random, 2f, 5f);
                flake.swayFrequency = Range(random, 0.30f, 0.65f);
                flake.parallax = 7f;
                flake.windFactor = 0.82f;
            }
            else
            {
                flake.size = Range(random, 7f, 11f);
                flake.alpha = Range(random, 0.45f, 0.62f);
                flake.fallSpeed = Range(random, 45f, 65f);
                flake.swayAmplitude = Range(random, 4f, 9f);
                flake.swayFrequency = Range(random, 0.22f, 0.55f);
                flake.parallax = 12f;
                flake.windFactor = 1.15f;
            }

            return flake;
        }

        private Vector2 CreateSpawnPosition(
            int layer,
            Vector2 size,
            System.Random random)
        {
            float halfWidth = size.x * 0.5f + WrapMargin;
            float halfHeight = size.y * 0.5f + WrapMargin;

            if (layer == 2)
            {
                int edge = random.Next(4);
                float offset = Range(random, 0.38f, 0.48f);
                if (edge <= 1)
                {
                    float x = (edge == 0 ? -1f : 1f) * size.x * offset;
                    return new Vector2(x, Range(random, -halfHeight, halfHeight));
                }

                float y = (edge == 2 ? -1f : 1f) * size.y * offset;
                return new Vector2(Range(random, -halfWidth, halfWidth), y);
            }

            return new Vector2(
                Range(random, -halfWidth, halfWidth),
                Range(random, -halfHeight, halfHeight));
        }

        private void Advance(float deltaTime)
        {
            fieldTime += deltaTime;
            float gust = 1f +
                Mathf.Sin(fieldTime * 0.23f) * 0.32f +
                Mathf.Sin(fieldTime * 0.71f + 1.3f) * 0.12f;
            Vector2 size = GetFieldSize();
            float halfWidth = size.x * 0.5f + WrapMargin;
            float halfHeight = size.y * 0.5f + WrapMargin;

            for (int index = 0; index < flakes.Length; index++)
            {
                Flake flake = flakes[index];
                flake.position.x += (4f + flake.windFactor * 5f) * gust * deltaTime;
                flake.position.y -= flake.fallSpeed * deltaTime;

                if (flake.position.y < -halfHeight)
                {
                    flake.position.y = halfHeight;
                    flake.position.x = Range(wrapRandom, -halfWidth, halfWidth);
                }
                else if (flake.position.y > halfHeight)
                {
                    flake.position.y = -halfHeight;
                }

                if (flake.position.x > halfWidth)
                {
                    flake.position.x = -halfWidth;
                    flake.position.y = Range(wrapRandom, -halfHeight, halfHeight);
                }
                else if (flake.position.x < -halfWidth)
                {
                    flake.position.x = halfWidth;
                    flake.position.y = Range(wrapRandom, -halfHeight, halfHeight);
                }

                flakes[index] = flake;
            }
        }

        private void UpdateParallax(float deltaTime)
        {
            if (previewMode)
            {
                parallaxOffset = Vector2.zero;
                return;
            }

            Vector2 normalized = new Vector2(
                Screen.width > 0 ? Input.mousePosition.x / Screen.width * 2f - 1f : 0f,
                Screen.height > 0 ? Input.mousePosition.y / Screen.height * 2f - 1f : 0f);
            normalized.x = Mathf.Clamp(normalized.x, -1f, 1f);
            normalized.y = Mathf.Clamp(normalized.y, -1f, 1f);
            Vector2 target = new Vector2(-normalized.x, -normalized.y);
            parallaxOffset = Vector2.SmoothDamp(
                parallaxOffset,
                target,
                ref parallaxVelocity,
                parallaxSmoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        private Vector2 GetFieldSize()
        {
            Rect rect = rectTransform.rect;
            Vector2 size = rect.size;
            if (size.x >= 1f && size.y >= 1f)
            {
                return size;
            }

            return new Vector2(FallbackWidth, FallbackHeight);
        }

        private static float GetNearProtection(Vector2 position, Vector2 size)
        {
            float u = (position.x + size.x * 0.5f) / Mathf.Max(1f, size.x);
            float v = (position.y + size.y * 0.5f) / Mathf.Max(1f, size.y);
            float factor = 1f;
            if (u < 0.46f)
            {
                factor *= 0.55f;
                if (v > 0.22f)
                {
                    factor *= 0.55f;
                }
            }

            return factor;
        }

        private static float GetMidProtection(Vector2 position, Vector2 size)
        {
            float u = (position.x + size.x * 0.5f) / Mathf.Max(1f, size.x);
            float v = (position.y + size.y * 0.5f) / Mathf.Max(1f, size.y);
            return u < 0.46f && v > 0.22f ? 0.85f : 1f;
        }

        private void EnsureSoftDot()
        {
            if (softDot != null)
            {
                return;
            }

            softDot = new Texture2D(
                SoftDotSize,
                SoftDotSize,
                TextureFormat.RGBA32,
                false)
            {
                name = "StartMenuSnowSoftDot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color core = new Color(0.949f, 0.980f, 1f, 1f);
            Color edge = new Color(0.525f, 0.663f, 0.769f, 1f);
            Color[] pixels = new Color[SoftDotSize * SoftDotSize];
            for (int y = 0; y < SoftDotSize; y++)
            {
                float ny = (y + 0.5f) / SoftDotSize * 2f - 1f;
                for (int x = 0; x < SoftDotSize; x++)
                {
                    float nx = (x + 0.5f) / SoftDotSize * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    int index = y * SoftDotSize + x;
                    if (radius >= 1f)
                    {
                        pixels[index] = Color.clear;
                        continue;
                    }

                    float alpha = Mathf.Pow(1f - radius, 1.45f);
                    float colorT = Mathf.Clamp01(1f - radius * 1.1f);
                    Color color = Color.Lerp(edge, core, colorT);
                    color.a = alpha;
                    pixels[index] = color;
                }
            }

            softDot.SetPixels(pixels);
            softDot.Apply(false, true);
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

#if UNITY_EDITOR
        public float EditorFieldTime => fieldTime;
#endif
    }
}
