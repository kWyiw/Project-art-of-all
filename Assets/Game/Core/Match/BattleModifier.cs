using System;

namespace ProjectArtOfAll.Core.Match
{
    /// <summary>
    /// 事件给的临时战斗增益（R-EVT-003）。一场战斗结算完就少一次，
    /// 归零即失效；多个增益同时存在时按字段相加。
    /// </summary>
    public readonly struct BattleModifier
    {
        public BattleModifier(
            int shieldPercent,
            int damageBonus,
            int blockBonus,
            int battlesRemaining)
        {
            if (shieldPercent < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(shieldPercent));
            }

            if (damageBonus < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damageBonus));
            }

            if (blockBonus < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockBonus));
            }

            if (battlesRemaining < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(battlesRemaining));
            }

            ShieldPercent = shieldPercent;
            DamageBonus = damageBonus;
            BlockBonus = blockBonus;
            BattlesRemaining = battlesRemaining;
        }

        /// <summary>战斗开始时额外获得的最大生命值百分比护盾。</summary>
        public int ShieldPercent { get; }

        /// <summary>本场战斗所有伤害 +N。</summary>
        public int DamageBonus { get; }

        /// <summary>本场战斗所有格挡 +N。</summary>
        public int BlockBonus { get; }

        /// <summary>还剩几场战斗。</summary>
        public int BattlesRemaining { get; }

        public bool IsExpired => BattlesRemaining <= 0;

        public BattleModifier WithRemaining(int battlesRemaining)
        {
            return new BattleModifier(
                ShieldPercent,
                DamageBonus,
                BlockBonus,
                battlesRemaining);
        }

        /// <summary>消耗一场战斗。</summary>
        public BattleModifier Consume()
        {
            return WithRemaining(BattlesRemaining - 1);
        }
    }
}
