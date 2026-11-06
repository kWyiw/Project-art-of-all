using System;
using System.Threading;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// 每局根种子的来源与派生。
    ///
    /// 一局里所有随机都由根种子决定，所以「同种子 + 同命令 = 同一局」是设计约束
    /// （回放、平衡模拟、信息集断言都靠它）。但这只要求种子相同可复现，
    /// 不要求每局都用同一个种子——根种子必须每局都不一样，否则骰子序列、
    /// 商店货、奖励选项、怪物牌组会在每一局里完全一样。
    ///
    /// 子流用 <see cref="Derive"/> 派生：不同用途各拿一条独立序列，
    /// 互相消耗不到对方，避免「买一次东西就把骰子序列整体错位」这种耦合。
    /// </summary>
    public static class MatchSeed
    {
        private const ulong FnvOffset = 0xCBF29CE484222325UL;
        private const ulong FnvPrime = 0x100000001B3UL;
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private const ulong Fallback = 0x2545F4914F6CDD1DUL;

        private static long counter;

        /// <summary>
        /// 取一个够随机的根种子：GUID（平台熵源）+ 高精度时间 + 进程内计数，
        /// 再用 SplitMix64 打散。任何一路变化都会得到完全不同的种子。
        /// 永不返回 0（PCG 的 0 状态退化）。
        /// </summary>
        public static ulong Create()
        {
            ulong mixed = FnvOffset;
            byte[] entropy = Guid.NewGuid().ToByteArray();
            for (int index = 0; index < entropy.Length; index++)
            {
                mixed ^= entropy[index];
                mixed *= FnvPrime;
            }

            mixed = Mix(mixed ^ (ulong)DateTime.UtcNow.Ticks);
            // Unity 侧有 ProjectArtOfAll.Environment 命名空间，这里必须写全名。
            mixed = Mix(mixed ^ (ulong)(uint)System.Environment.TickCount);
            mixed = Mix(mixed ^ (ulong)Interlocked.Increment(ref counter));
            return mixed == 0UL ? Fallback : mixed;
        }

        /// <summary>
        /// 从根种子派生指定编号的子流。同一个根 + 同一个编号永远得到同一个流。
        /// </summary>
        public static ulong Derive(ulong root, ulong streamId)
        {
            ulong mixed = Mix(root + (GoldenGamma * (streamId + 1UL)));
            return mixed == 0UL ? Fallback : mixed;
        }

        /// <summary>种子的稳定文本形态（日志里用，便于复现某一局）。</summary>
        public static string Describe(ulong seed)
        {
            return seed.ToString("X16");
        }

        private static ulong Mix(ulong value)
        {
            unchecked
            {
                value ^= value >> 30;
                value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27;
                value *= 0x94D049BB133111EBUL;
                value ^= value >> 31;
            }

            return value;
        }
    }
}
