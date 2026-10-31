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

        public int Row => row;
        public int Column => column;
        public TileType TileType => tileType;
        public int OwnerPlayerId => ownerPlayerId;

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
    }
}
