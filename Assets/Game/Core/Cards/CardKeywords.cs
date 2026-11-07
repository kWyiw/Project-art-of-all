using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectArtOfAll.Core.Cards
{
    /// <summary>
    /// 卡面关键词与配色。放在纯内核里，是为了让「哪句话里的哪个词该上什么色」只有一份定义，
    /// 并且能被 <c>Game.Core.Checks</c> 断言——UI 层只负责把富文本贴到 Text 上。
    /// </summary>
    public static class CardKeywords
    {
        public const string DamageColor = "#E8705F";
        public const string ShieldColor = "#E7C05C";
        public const string PoisonColor = "#83CF6B";
        public const string BurnColor = "#EF9152";
        public const string HealColor = "#8FDC9F";
        public const string RegenerationColor = "#6FE3B4";
        public const string CooldownColor = "#6EC6E6";
        public const string SlowColor = "#7EA7EC";
        public const string MultiplyColor = "#BE8CEB";
        public const string DiceColor = "#7FCBBA";
        public const string MutedColor = "#9C9A92";

        private static readonly Dictionary<string, string> Colors =
            new Dictionary<string, string>
            {
                { "生命再生", HealColor },
                { "再生", RegenerationColor },
                { "多重释放", MultiplyColor },
                { "额外施放", MultiplyColor },
                { "骰值", DiceColor },
                { "骰数", DiceColor },
                { "修正", DiceColor },
                { "剧毒", PoisonColor },
                { "中毒", PoisonColor },
                { "灼烧", BurnColor },
                { "燃烧", BurnColor },
                { "护盾获得", ShieldColor },
                { "护盾", ShieldColor },
                { "格挡", ShieldColor },
                { "治疗", HealColor },
                { "充能", CooldownColor },
                { "加速", CooldownColor },
                { "缓速", SlowColor },
                { "摧毁", MutedColor },
                { "销毁", MutedColor },
                { "价值", ShieldColor },
                { "伤害", DamageColor }
            };

        /// <summary>匹配顺序：长词优先，免得「生命再生」被「再生」抢先吃掉。</summary>
        private static readonly string[] Keywords = BuildKeywords();

        public static IReadOnlyList<string> All => Keywords;

        public static bool IsKeyword(string keyword)
        {
            return keyword != null && Colors.ContainsKey(keyword);
        }

        public static string ColorOf(string keyword)
        {
            string color;
            return keyword != null && Colors.TryGetValue(keyword, out color) ? color : null;
        }

        /// <summary>把一段卡面文案里的关键词包上颜色与粗体；单次扫描，不会把已插入的标签再替换一遍。</summary>
        public static string Colorize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length + 32);
            int index = 0;
            while (index < text.Length)
            {
                string matched = MatchAt(text, index);
                if (matched != null)
                {
                    builder.Append("<color=")
                        .Append(Colors[matched])
                        .Append("><b>")
                        .Append(matched)
                        .Append("</b></color>");
                    index += matched.Length;
                    continue;
                }

                builder.Append(text[index]);
                index++;
            }

            return builder.ToString();
        }

        public static string MatchAt(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
            {
                return null;
            }

            for (int order = 0; order < Keywords.Length; order++)
            {
                string keyword = Keywords[order];
                if (string.CompareOrdinal(text, index, keyword, 0, keyword.Length) == 0)
                {
                    return keyword;
                }
            }

            return null;
        }

        private static string[] BuildKeywords()
        {
            List<string> keys = new List<string>(Colors.Keys);
            keys.Sort((left, right) => right.Length != left.Length
                ? right.Length.CompareTo(left.Length)
                : string.CompareOrdinal(left, right));
            return keys.ToArray();
        }
    }
}
