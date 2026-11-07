using System.Collections.Generic;
using UnityEngine;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// 岔路选择提示：在玩家能选的每个目标地块顶面盖一块同色发光薄片。
    /// 薄片颜色与屏幕上的方向箭头一一对应，让"哪个按钮会走到哪块地"一眼可见。
    /// 纯表现层：薄片没有碰撞体，也不参与任何规则判定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardForkHighlight : MonoBehaviour
    {
        private const int MaxPlates = 4;
        private const float PlateThickness = 0.06f;
        private const float PlateOverhang = 1.16f;
        private const float PlateLift = 0.03f;
        private const float PulseSpeed = 3.4f;
        private const float MinAlpha = 0.40f;
        private const float MaxAlpha = 0.88f;

        private readonly List<Transform> plateTransforms = new List<Transform>();
        private readonly List<MeshRenderer> plateRenderers = new List<MeshRenderer>();
        private readonly List<Color> plateColors = new List<Color>();

        private int visibleCount;

        public int VisibleCount => visibleCount;

        public static BoardForkHighlight Create(Transform parent)
        {
            GameObject rootObject = new GameObject("BoardForkHighlight");
            if (parent != null)
            {
                rootObject.transform.SetParent(parent, false);
            }

            BoardForkHighlight highlight =
                rootObject.AddComponent<BoardForkHighlight>();
            highlight.BuildPlates();
            return highlight;
        }

        /// <summary>
        /// 为每个可选目标地块点亮一块薄片，颜色与 <paramref name="colors"/> 一一对应。
        /// 传空则等同于 <see cref="Clear"/>。
        /// </summary>
        public void Show(
            IReadOnlyList<BoardTileView> targets,
            IReadOnlyList<Color> colors)
        {
            if (targets == null || targets.Count == 0)
            {
                Clear();
                return;
            }

            Vector3 parentScale = transform.lossyScale;
            int used = 0;
            int count = Mathf.Min(targets.Count, plateTransforms.Count);
            for (int index = 0; index < count; index++)
            {
                BoardTileView tile = targets[index];
                if (tile == null)
                {
                    continue;
                }

                Transform plate = plateTransforms[index];
                Vector3 tileScale = tile.transform.lossyScale;
                Vector3 tilePosition = tile.transform.position;
                plate.position = new Vector3(
                    tilePosition.x,
                    tilePosition.y + Mathf.Abs(tileScale.y) * 0.5f + PlateLift,
                    tilePosition.z);
                plate.rotation = Quaternion.identity;
                // 父节点可能整体旋转/缩放（生成地图的 FitScale），所以按父级 lossyScale
                // 反算 localScale，薄片在世界里始终等于地块尺寸的固定倍数。
                plate.localScale = new Vector3(
                    Mathf.Abs(tileScale.x) * PlateOverhang /
                    Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                    PlateThickness / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                    Mathf.Abs(tileScale.z) * PlateOverhang /
                    Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));

                Color color = colors != null && index < colors.Count
                    ? colors[index]
                    : Color.white;
                plateColors[index] = color;
                plate.gameObject.SetActive(true);
                used++;
            }

            visibleCount = used;
            Pulse(Time.unscaledTime);
        }

        public void Clear()
        {
            visibleCount = 0;
            for (int index = 0; index < plateTransforms.Count; index++)
            {
                Transform plate = plateTransforms[index];
                if (plate != null)
                {
                    plate.gameObject.SetActive(false);
                }
            }
        }

        private void BuildPlates()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            for (int index = 0; index < MaxPlates; index++)
            {
                GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "ForkHighlight" + index;
                Collider collider = plate.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                plate.transform.SetParent(transform, false);
                MeshRenderer renderer = plate.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = new Material(shader)
                    {
                        name = "BoardForkHighlight" + index,
                    };
                    renderer.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                plate.SetActive(false);
                plateTransforms.Add(plate.transform);
                plateRenderers.Add(renderer);
                plateColors.Add(Color.white);
            }
        }

        private void Update()
        {
            if (visibleCount <= 0)
            {
                return;
            }

            Pulse(Time.unscaledTime);
        }

        private void Pulse(float time)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(time * PulseSpeed);
            float alpha = Mathf.Lerp(MinAlpha, MaxAlpha, wave);
            for (int index = 0; index < visibleCount; index++)
            {
                MeshRenderer renderer = plateRenderers[index];
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    continue;
                }

                Color color = plateColors[index];
                color.a = alpha;
                renderer.sharedMaterial.color = color;
            }
        }

        private void OnDestroy()
        {
            for (int index = 0; index < plateRenderers.Count; index++)
            {
                MeshRenderer renderer = plateRenderers[index];
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    Destroy(renderer.sharedMaterial);
                }
            }

            plateRenderers.Clear();
            plateTransforms.Clear();
            plateColors.Clear();
        }
    }
}
