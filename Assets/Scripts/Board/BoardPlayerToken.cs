using UnityEngine;

namespace ProjectArtOfAll.Board
{
    [DisallowMultipleComponent]
    public sealed class BoardPlayerToken : MonoBehaviour
    {
        [SerializeField] private int playerId;
        [SerializeField] private int currentRow;
        [SerializeField] private int currentColumn;

        public int PlayerId => playerId;
        public int CurrentRow => currentRow;
        public int CurrentColumn => currentColumn;

        public void Configure(int id, int row, int column)
        {
            playerId = id;
            SetBoardCell(row, column);
        }

        public void SetBoardCell(int row, int column)
        {
            currentRow = row;
            currentColumn = column;
        }
    }
}
