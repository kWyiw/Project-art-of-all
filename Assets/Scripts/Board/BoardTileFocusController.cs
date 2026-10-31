using ProjectArtOfAll.Core.Board;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectArtOfAll.Board
{
    [AddComponentMenu("Project Art of All/UI/Board Tile Focus")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BoardTileFocusController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera boardCamera;
        [SerializeField] private Canvas canvas;
        [SerializeField] private GameObject focusPanel;
        [SerializeField] private RectTransform focusRect;
        [SerializeField] private Text focusTitle;
        [SerializeField] private Text focusDetails;

        [Header("Interaction")]
        [SerializeField, Min(1f)] private float rayDistance = 500f;
        [SerializeField] private Vector2 pointerOffset = new Vector2(24f, -24f);

        private Font runtimeFont;
        private BoardTileView selectedTile;

        private void Awake()
        {
            if (boardCamera == null)
            {
                boardCamera = Camera.main;
            }

            runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Microsoft YaHei UI",
                    "Microsoft YaHei",
                    "PingFang SC",
                    "Noto Sans CJK SC",
                    "Arial"
                },
                22);

            ApplyFont(transform);
            HideFocus();
        }

        private void Update()
        {
            if (boardCamera == null || focusPanel == null)
            {
                return;
            }

            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                HideFocus();
                return;
            }

            Ray ray = boardCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                rayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            BoardTileView tile = null;
            foreach (RaycastHit hit in hits)
            {
                tile = hit.collider.GetComponent<BoardTileView>();
                if (tile != null)
                {
                    break;
                }
            }

            if (tile != null)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    selectedTile = tile;
                }

                ShowFocus(tile);
                PositionFocusPanel();
                return;
            }

            if (selectedTile != null)
            {
                ShowFocus(selectedTile);
                return;
            }

            HideFocus();
        }

        private void ShowFocus(BoardTileView tile)
        {
            focusPanel.SetActive(true);
            focusTitle.text =
                $"{GetTileName(tile.TileType)}  ·  R{tile.Row:00} C{tile.Column:00}";
            focusDetails.text = GetTileDescription(tile);
        }

        private void HideFocus()
        {
            if (focusPanel != null)
            {
                focusPanel.SetActive(false);
            }
        }

#if UNITY_EDITOR
        public void ShowTilePreview(BoardTileView tile)
        {
            if (tile == null)
            {
                selectedTile = null;
                HideFocus();
                return;
            }

            selectedTile = tile;
            ShowFocus(tile);
        }
#endif

        private void PositionFocusPanel()
        {
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 panelSize = focusRect.rect.size * scale;
            float x = Mathf.Clamp(
                Input.mousePosition.x + pointerOffset.x,
                12f,
                Mathf.Max(12f, Screen.width - panelSize.x - 12f));
            float y = Mathf.Clamp(
                Input.mousePosition.y + pointerOffset.y,
                panelSize.y + 12f,
                Screen.height - 12f);
            focusRect.position = new Vector3(x, y, 0f);
        }

        private static string GetTileName(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.Home:
                    return "Home 地块";
                case TileType.Shop:
                    return "商店地块";
                case TileType.Reward:
                    return "奖励地块";
                case TileType.Monster:
                    return "怪物地块";
                case TileType.Boss:
                    return "Boss 地块";
                case TileType.Warp:
                    return "传送地块";
                case TileType.Event:
                    return "事件地块";
                default:
                    return "普通地块";
            }
        }

        private static string GetTileDescription(BoardTileView tile)
        {
            switch (tile.TileType)
            {
                case TileType.Home:
                    return
                        $"玩家 {tile.OwnerPlayerId + 1} 的出生与升级地点。" +
                        "\n满足 Norma 后可在 Home 升级。";
                case TileType.Shop:
                    return "停留后打开商店。\n可购买、刷新和出售珍宝。";
                case TileType.Reward:
                    return "停留后获得奖励。\nM3 将接入二选一珍宝。";
                case TileType.Monster:
                    return "停留后遭遇怪物。\n胜利可获得碎片与胜场。";
                case TileType.Boss:
                    return "高阶战斗地块。\n由首名达到 4 级的玩家触发演化。";
                case TileType.Warp:
                    return
                        "抵达后传送到同组其他传送地块。\n" +
                        "途径不触发传送，传送不消耗移动步数。";
                case TileType.Event:
                    return "停留后触发随机事件。\n具体事件内容在 M3 接入。";
                default:
                    return "基础地块，没有附加效果。";
            }
        }

        private void ApplyFont(Transform root)
        {
            if (runtimeFont == null)
            {
                return;
            }

            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                text.font = runtimeFont;
            }
        }
    }
}
