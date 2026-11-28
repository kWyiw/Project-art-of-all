using ProjectArtOfAll.Core.Board;
using UnityEngine;

namespace ProjectArtOfAll.Board
{
    public sealed class BoardTileView : MonoBehaviour
    {
        [SerializeField] private int row;
        [SerializeField] private int column;
        [SerializeField] private TileType tileType;
        [SerializeField] private int ownerPlayerId = -1;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor = Color.white;
        private bool bossPresentation;
        private bool capturedBaseColor;

        public int Row => row;
        public int Column => column;
        public TileType TileType => tileType;
        public int OwnerPlayerId => ownerPlayerId;

        private void Awake()
        {
            EnsureRenderer();
        }

        public void Configure(
            int rowIndex,
            int columnIndex,
            TileType type,
            int ownerId = -1)
        {
            row = rowIndex;
            column = columnIndex;
            tileType = type;
            ownerPlayerId = ownerId;
        }

        /// <summary>
        /// Boss is a phase presentation, not a separate runtime tile type. The shared
        /// material stays untouched; each renderer receives a phase tint via a property block.
        /// </summary>
        public void SetBossPresentation(bool boss)
        {
            EnsureRenderer();
            if (meshRenderer == null || bossPresentation == boss)
            {
                return;
            }

            if (!capturedBaseColor)
            {
                baseColor = meshRenderer.sharedMaterial != null
                    ? meshRenderer.sharedMaterial.color
                    : Color.white;
                capturedBaseColor = true;
            }

            bossPresentation = boss;
            if (!boss)
            {
                meshRenderer.SetPropertyBlock(null);
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            Color tint = new Color(
                Mathf.Min(1f, baseColor.r * 1.25f + 0.12f),
                baseColor.g * 0.70f,
                baseColor.b * 0.58f,
                baseColor.a);
            propertyBlock.SetColor("_Color", tint);
            propertyBlock.SetColor("_BaseColor", tint);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void EnsureRenderer()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
        }
    }
}
