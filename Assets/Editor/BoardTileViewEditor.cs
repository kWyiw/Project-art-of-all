using ProjectArtOfAll.Board;
using UnityEditor;
using UnityEngine;

namespace ProjectArtOfAll.Editor
{
    [CustomEditor(typeof(BoardTileView))]
    internal sealed class BoardTileViewEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            EditorApplication.delayCall += PreviewSelectedTile;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Refresh M3 Tile Preview"))
            {
                PreviewSelectedTile();
            }
        }

        private void PreviewSelectedTile()
        {
            BoardTileView tile = target as BoardTileView;
            if (tile == null)
            {
                return;
            }

            BoardTileFocusController controller =
                Object.FindObjectOfType<BoardTileFocusController>();
            if (controller != null)
            {
                controller.ShowTilePreview(tile);
            }
        }
    }
}
