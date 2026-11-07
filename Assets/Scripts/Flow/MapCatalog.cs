using System;
using System.Collections.Generic;

namespace ProjectArtOfAll.Flow
{
    public enum BoardMapKind
    {
        /// <summary>The original 68-tile board baked into the match scene.</summary>
        Classic = 0,

        /// <summary>Sweet-tooth board: a diamond ring with a small loop on every side, 68 tiles.</summary>
        SweetRing = 1
    }

    /// <summary>One selectable board. Maps are data, so adding a board never touches the kernel.</summary>
    public sealed class BoardMapInfo
    {
        public BoardMapInfo(
            string id,
            string sceneName,
            string displayName,
            string subtitle,
            string description,
            BoardMapKind kind,
            int tileCount)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            SceneName = sceneName ?? throw new ArgumentNullException(nameof(sceneName));
            DisplayName = displayName ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Description = description ?? string.Empty;
            Kind = kind;
            TileCount = tileCount;
        }

        public string Id { get; }

        /// <summary>场景名。每张地图的实体内容只保留在自己的场景里。</summary>
        public string SceneName { get; }

        public string DisplayName { get; }

        public string Subtitle { get; }

        public string Description { get; }

        public BoardMapKind Kind { get; }

        public int TileCount { get; }

        /// <summary>True when the board's topology also needs its code-side definition.</summary>
        public bool IsGenerated => Kind != BoardMapKind.Classic;
    }

    /// <summary>The board list shown by the map-select page, plus the current choice.</summary>
    public static class MapCatalog
    {
        public const string ClassicId = "classic";
        public const string SweetRingId = "sweet-ring";

        private static readonly BoardMapInfo[] Maps =
        {
            new BoardMapInfo(
                ClassicId,
                "Map1",
                "经典广场",
                "12 × 12 棋盘 · 68 格",
                "四角各有一个家，8 个三岔口把棋盘切成环线；商店、奖励、怪物与传送门交错分布。",
                BoardMapKind.Classic,
                68),
            new BoardMapInfo(
                SweetRingId,
                "Map2",
                "甜点回廊",
                "手绘方格环带 · 36 格",
                "照着草图逐格复刻：11 × 9 的方格上摆出一圈环带，四个角各有一个家，左右两条侧环由三格拐角接回主环，紫色传送门两两相对。",
                BoardMapKind.SweetRing,
                36)
        };

        private static string selectedId = ClassicId;

        public static IReadOnlyList<BoardMapInfo> All => Maps;

        /// <summary>Id of the board the next match uses. Unknown ids fall back to the first board.</summary>
        public static string SelectedId
        {
            get => selectedId;
            set => selectedId = Get(value) != null ? value : Maps[0].Id;
        }

        public static BoardMapInfo Selected => Get(selectedId) ?? Maps[0];

        public static BoardMapInfo Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int index = 0; index < Maps.Length; index++)
            {
                if (string.Equals(Maps[index].Id, id, StringComparison.Ordinal))
                {
                    return Maps[index];
                }
            }

            return null;
        }

        public static void Reset()
        {
            selectedId = ClassicId;
        }
    }
}
