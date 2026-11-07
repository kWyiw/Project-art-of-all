using UnityEngine;

namespace ProjectArtOfAll.Flow
{
    /// <summary>
    /// Each physical map scene owns one baked board. This component makes opening
    /// Map1 or Map2 directly in the editor behave exactly like entering it through
    /// the MainGame dimension transit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private string mapId = MapCatalog.ClassicId;

        public string MapId => mapId;

        private void Awake()
        {
            if (MapCatalog.Get(mapId) == null)
            {
                Debug.LogError(string.Concat(
                    "[Map] Invalid map id on scene '",
                    gameObject.scene.name,
                    "': ",
                    mapId));
                return;
            }

            GameSession.SelectedMapId = mapId;
        }

        /// <summary>Editor scene baker only.</summary>
        public void Configure(string id)
        {
            mapId = id;
        }
    }
}
