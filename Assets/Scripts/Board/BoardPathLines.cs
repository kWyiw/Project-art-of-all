using System.Collections.Generic;
using ProjectArtOfAll.Core.Board;
using UnityEngine;

namespace ProjectArtOfAll.Board
{
    /// <summary>
    /// M4 地图渲染：把内核的邻接关系画成可见的路径线。
    /// 只在相邻（正交相接）的瓦片之间连线，线贴在瓦片顶面之上，不参与任何规则判定。
    /// </summary>
    public static class BoardPathLines
    {
        private const float LineLift = 0.055f;
        private const float LineWidth = 0.075f;
        private static readonly Color LineColor = new Color(0.98f, 0.80f, 0.42f, 0.55f);

        public static GameObject Build(
            Transform parent,
            MapGraph graph,
            IReadOnlyDictionary<int, BoardTileView> tiles)
        {
            GameObject rootObject = new GameObject("BoardPathLines");
            rootObject.transform.SetParent(parent, false);

            if (graph == null || tiles == null)
            {
                return rootObject;
            }

            Material material = CreateLineMaterial();
            HashSet<long> drawn = new HashSet<long>();

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                MapNode node = graph.Nodes[i];
                if (!tiles.TryGetValue(node.Id, out BoardTileView from) || from == null)
                {
                    continue;
                }

                for (int j = 0; j < node.OutEdges.Count; j++)
                {
                    int otherId = node.OutEdges[j];
                    if (!tiles.TryGetValue(otherId, out BoardTileView to) || to == null)
                    {
                        continue;
                    }

                    long key = node.Id < otherId
                        ? ((long)node.Id << 32) | (uint)otherId
                        : ((long)otherId << 32) | (uint)node.Id;
                    if (!drawn.Add(key))
                    {
                        continue;
                    }

                    CreateLine(rootObject.transform, from, to, material);
                }
            }

            return rootObject;
        }

        private static void CreateLine(
            Transform parent,
            BoardTileView from,
            BoardTileView to,
            Material material)
        {
            GameObject lineObject = new GameObject($"Path {from.Row:D2}{from.Column:D2}-{to.Row:D2}{to.Column:D2}");
            lineObject.transform.SetParent(parent, false);

            // 线是贴在地上的"扁带"，所以要把四边形摆到水平面里：
            // TransformZ 对齐时四边形位于物体的局部 XY 平面，绕 X 轴 -90° 后法线朝上（世界 +Y）。
            // 之前没有这一步，线是竖着的，从上往下看只剩一像素的斜边——屏幕上的"多余横线"就是它。
            lineObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, TopCenter(from));
            line.SetPosition(1, TopCenter(to));
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.numCapVertices = 4;
            line.alignment = LineAlignment.TransformZ;
            line.textureMode = LineTextureMode.Stretch;
            line.material = material;
            line.startColor = LineColor;
            line.endColor = LineColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private static Vector3 TopCenter(BoardTileView tile)
        {
            Vector3 position = tile.transform.position;
            position.y += tile.transform.lossyScale.y * 0.5f + LineLift;
            return position;
        }

        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            Material material = new Material(shader) { name = "BoardPathLine" };
            material.renderQueue = 3000;
            return material;
        }
    }
}
