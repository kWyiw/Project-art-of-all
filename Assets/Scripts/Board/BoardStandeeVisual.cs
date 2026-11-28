using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// Replaces player 1's block mesh with the authored acrylic standee while
    /// keeping the existing BoardPlayerToken root, scale animation and movement.
    /// </summary>
    public static class BoardStandeeVisual
    {
        private const int MainCharacterId = 0;
        public const float GameplayHeight = 3.375f;
        private const string VisualObjectName = "RedHoodStandee";
        private const string PrefabResourcePath =
            "Standees/RedHood/PF_RedHoodStandee";
        private static bool resourcesWarningLogged;

        public static void ApplyMainCharacter(
            IReadOnlyDictionary<int, BoardPlayerToken> tokensByPlayer,
            Camera boardCamera)
        {
            if (tokensByPlayer == null ||
                !tokensByPlayer.TryGetValue(MainCharacterId, out BoardPlayerToken token) ||
                token == null)
            {
                return;
            }

            Transform existing = token.transform.Find(VisualObjectName);
            if (existing != null)
            {
                ConfigureMaterialPresentation(existing.gameObject);
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                if (!resourcesWarningLogged)
                {
                    resourcesWarningLogged = true;
                    Debug.LogWarning(
                        "[Board] Red-hood standee prefab has not imported yet. Run " +
                        "Tools/Project Art of All/Build Red Hood Standee Assets.");
                }

                return;
            }

            Renderer blockRenderer = token.GetComponent<Renderer>();
            float standingSurfaceY = ResolveStandingSurfaceY(token, blockRenderer);
            if (blockRenderer != null)
            {
                blockRenderer.enabled = false;
            }

            Transform anchor = new GameObject(VisualObjectName).transform;
            anchor.SetParent(token.transform, false);
            Vector3 parentScale = token.transform.lossyScale;
            anchor.localScale = new Vector3(
                SafeInverse(parentScale.x),
                SafeInverse(parentScale.y),
                SafeInverse(parentScale.z));

            GameObject model = Object.Instantiate(prefab, anchor, false);
            model.name = "RedHoodStandee_Model";
            model.transform.localPosition = Vector3.zero;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 3)
            {
                Debug.LogError(
                    "[Board] Red-hood standee prefab has " +
                    renderers.Length.ToString() +
                    " renderers instead of three.");
                Object.Destroy(anchor.gameObject);
                if (blockRenderer != null)
                {
                    blockRenderer.enabled = true;
                }

                return;
            }

            ConfigureMaterialPresentation(model);
            Bounds bounds = CalculateBounds(model);
            if (bounds.size.y <= 0.0001f)
            {
                Debug.LogError("[Board] Red-hood standee has invalid render bounds.");
                Object.Destroy(anchor.gameObject);
                if (blockRenderer != null)
                {
                    blockRenderer.enabled = true;
                }

                return;
            }

            float uniformScale = GameplayHeight / bounds.size.y;
            model.transform.localScale *= uniformScale;
            bounds = CalculateBounds(model);

            if (boardCamera != null)
            {
                Vector3 direction = boardCamera.transform.position - anchor.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    anchor.rotation = Quaternion.LookRotation(direction.normalized);
                    bounds = CalculateBounds(model);
                }
            }

            model.transform.position +=
                Vector3.up * (standingSurfaceY - bounds.min.y);
            Debug.Log("[Board] Replaced player 1 block with red-hood acrylic standee.");
        }

        private static void ConfigureMaterialPresentation(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer != null &&
                    renderer.gameObject.name == "Acrylic_Panel")
                {
                    Material material = renderer.material;
                    material.renderQueue = (int)RenderQueue.Geometry + 1;
                    return;
                }
            }
        }

        private static float ResolveStandingSurfaceY(
            BoardPlayerToken token,
            Renderer blockRenderer)
        {
            BoardTileView[] tiles = Object.FindObjectsOfType<BoardTileView>();
            for (int index = 0; index < tiles.Length; index++)
            {
                BoardTileView tile = tiles[index];
                if (tile == null ||
                    tile.gameObject.scene != token.gameObject.scene ||
                    tile.Row != token.CurrentRow ||
                    tile.Column != token.CurrentColumn)
                {
                    continue;
                }

                Renderer tileRenderer = tile.GetComponent<Renderer>();
                if (tileRenderer != null)
                {
                    return tileRenderer.bounds.max.y;
                }
            }

            if (blockRenderer != null)
            {
                return blockRenderer.bounds.min.y -
                    BoardMapBuilder.TileGroundSurfaceHeight;
            }

            return token.transform.position.y - token.transform.lossyScale.y * 0.5f;
        }

        private static float SafeInverse(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }
    }
}
