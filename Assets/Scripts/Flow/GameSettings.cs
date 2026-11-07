using System;
using ProjectArtOfAll.Core.Match;
using UnityEngine;

namespace ProjectArtOfAll.Flow
{
    /// <summary>
    /// 玩家设置（只影响表现，不改任何规则数值）。用 PlayerPrefs 持久化，主菜单与暂停菜单共用同一份。
    /// 之所以做成静态类：设置被多个场景读写，而 Unity 的跨场景引用要么走 Resources 要么走 DontDestroyOnLoad，
    /// 对一个只有四个标量的东西来说都不值得。
    /// </summary>
    public static class GameSettings
    {
        public const float MinimumCardScale = 0.75f;
        public const float MaximumCardScale = 1.25f;

        private const string CardScaleKey = "paoa.settings.cardScale";
        private const string CameraFollowKey = "paoa.settings.cameraFollow";
        private const string AutoQualityKey = "paoa.settings.autoQuality";
        private const string CpuDifficultyKey = "paoa.settings.cpuDifficulty";

        private static bool loaded;
        private static float cardScale = 1f;
        private static bool cameraFollow = true;
        private static bool autoQuality = true;
        private static int cpuDifficulty = 1;

        /// <summary>任何一项设置变化后触发（参数为变化项的名字，便于日志）。</summary>
        public static event Action<string> Changed;

        /// <summary>常驻卡槽的额外缩放（乘在自动缩放上），0.75~1.25。</summary>
        public static float CardScale
        {
            get
            {
                EnsureLoaded();
                return cardScale;
            }

            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp(value, MinimumCardScale, MaximumCardScale);
                if (Mathf.Approximately(clamped, cardScale))
                {
                    return;
                }

                cardScale = clamped;
                PlayerPrefs.SetFloat(CardScaleKey, cardScale);
                PlayerPrefs.Save();
                Changed?.Invoke(nameof(CardScale));
            }
        }

        /// <summary>镜头是否跟随当前行动的棋子。</summary>
        public static bool CameraFollow
        {
            get
            {
                EnsureLoaded();
                return cameraFollow;
            }

            set
            {
                EnsureLoaded();
                if (cameraFollow == value)
                {
                    return;
                }

                cameraFollow = value;
                PlayerPrefs.SetInt(CameraFollowKey, cameraFollow ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke(nameof(CameraFollow));
            }
        }

        /// <summary>掉帧时自动降低卡槽尺寸。关掉后画质就固定不动。</summary>
        public static bool AutoQuality
        {
            get
            {
                EnsureLoaded();
                return autoQuality;
            }

            set
            {
                EnsureLoaded();
                if (autoQuality == value)
                {
                    return;
                }

                autoQuality = value;
                PlayerPrefs.SetInt(AutoQualityKey, autoQuality ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke(nameof(AutoQuality));
            }
        }

        /// <summary>
        /// CPU 难度：0 新手 / 1 普通 / 2 困难。只改 AI 的决策参数，不动任何规则数值。
        /// </summary>
        public static int CpuDifficultyLevel
        {
            get
            {
                EnsureLoaded();
                return cpuDifficulty;
            }

            set
            {
                EnsureLoaded();
                int clamped = Mathf.Clamp(value, 0, 2);
                if (clamped == cpuDifficulty)
                {
                    return;
                }

                cpuDifficulty = clamped;
                PlayerPrefs.SetInt(CpuDifficultyKey, cpuDifficulty);
                PlayerPrefs.Save();
                Changed?.Invoke(nameof(CpuDifficultyLevel));
            }
        }

        /// <summary>难度按钮上的中文标签。</summary>
        public static string CpuDifficultyLabel
        {
            get
            {
                switch (CpuDifficultyLevel)
                {
                    case 0:
                        return "新手";
                    case 2:
                        return "困难";
                    default:
                        return "普通";
                }
            }
        }

        /// <summary>内核 AI 用的难度分档（R-CPU-005）。</summary>
        public static CpuDifficulty CpuDifficultyTier
        {
            get { return (CpuDifficulty)CpuDifficultyLevel; }
        }

        public static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            cardScale = Mathf.Clamp(
                PlayerPrefs.GetFloat(CardScaleKey, 1f),
                MinimumCardScale,
                MaximumCardScale);
            cameraFollow = PlayerPrefs.GetInt(CameraFollowKey, 1) != 0;
            autoQuality = PlayerPrefs.GetInt(AutoQualityKey, 1) != 0;
            cpuDifficulty = Mathf.Clamp(PlayerPrefs.GetInt(CpuDifficultyKey, 1), 0, 2);
        }

        /// <summary>测试/重开用：把设置恢复默认并落盘。</summary>
        public static void ResetToDefaults()
        {
            loaded = true;
            cardScale = 1f;
            cameraFollow = true;
            autoQuality = true;
            cpuDifficulty = 1;
            PlayerPrefs.SetFloat(CardScaleKey, cardScale);
            PlayerPrefs.SetInt(CameraFollowKey, 1);
            PlayerPrefs.SetInt(AutoQualityKey, 1);
            PlayerPrefs.SetInt(CpuDifficultyKey, cpuDifficulty);
            PlayerPrefs.Save();
            Changed?.Invoke("Reset");
        }
    }
}
