using System.Collections.Generic;

namespace ProjectArtOfAll.Core.Cards
{
    /// <summary>
    /// 怪物 / 敌人用的预设卡组。
    /// </summary>
    /// <remarks>
    /// 开局不再给玩家发现成的卡牌（见 <c>MatchController.StartingShards</c>），
    /// 原先那几套「初始卡牌」就搬到这里，当成怪物和对手的配置：
    /// 拾荒者 = 原来的玩家初始套（撬棍 / 核废水 / 压缩饼干）；
    /// 打火队 = 原来的 1 级野怪套（撬棍 / 打火机 / 压缩饼干）；
    /// 拆车队 = 1 级野怪套 + 汽车门板（2 级起）。
    /// </remarks>
    public static class MonsterDecks
    {
        public const string ScavengerName = "拾荒者";
        public const string TorchGangName = "打火队";
        public const string WreckerName = "拆车队";

        public static readonly IReadOnlyList<string> ScavengerCards = new[]
        {
            "crowbar",
            "nuclear_water",
            "hardtack"
        };

        public static readonly IReadOnlyList<string> TorchGangCards = new[]
        {
            "crowbar",
            "lighter",
            "hardtack"
        };

        public static readonly IReadOnlyList<string> WreckerCards = new[]
        {
            "crowbar",
            "lighter",
            "hardtack",
            "car_door"
        };

        /// <summary>野怪按玩家等级换装：1 级打火队，2 级起改拆车队。</summary>
        public static IReadOnlyList<string> ForLevel(int level)
        {
            return level >= 2 ? WreckerCards : TorchGangCards;
        }

        /// <summary>名字只用于日志与调试，出场顺序与 <see cref="ForLevel"/> 一致。</summary>
        public static string NameForLevel(int level)
        {
            return level >= 2 ? WreckerName : TorchGangName;
        }
    }
}
