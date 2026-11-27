using System;

namespace ProjectArtOfAll.Core.Cards
{
    /// <summary>
    /// A card the player owns. Definition is shared and immutable; everything below is
    /// per-instance growth (永久成长 in R-ECO-010) and must be saved with the run.
    /// </summary>
    public sealed class CardInstance
    {
        public CardInstance(int uid, CardDefinition definition)
        {
            if (uid < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uid));
            }

            Uid = uid;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public int Uid { get; }

        /// <summary>这张牌当前的定义：融合升级后指向升级版（CJ-5.11）。</summary>
        public CardDefinition Definition { get; private set; }

        private int valueBonus;
        private int permanentDiceModifier;
        private int permanentDiceCountBonus;
        private int permanentHealingModifier;

        /// <summary>铜锣烧 / 黄金战车: value grows during a run, capped at 20.</summary>
        public int ValueBonus
        {
            get => valueBonus;
            set => valueBonus = Math.Max(-Definition.Value, Math.Min(20 - Definition.Value, value));
        }

        /// <summary>幸运饼干 / 淡水: permanent per-die modifier (clamped by the resolver).</summary>
        public int PermanentDiceModifier
        {
            get => permanentDiceModifier;
            set => permanentDiceModifier = Math.Max(-5, Math.Min(5, value));
        }

        /// <summary>粉碎机: permanently adds whole dice to this card.</summary>
        public int PermanentDiceCountBonus
        {
            get => permanentDiceCountBonus;
            set => permanentDiceCountBonus = Math.Max(-2, Math.Min(4, value));
        }

        /// <summary>Fresh water sales: only modifies dice that heal.</summary>
        public int PermanentHealingModifier
        {
            get => permanentHealingModifier;
            set => permanentHealingModifier = Math.Max(-5, Math.Min(5, value));
        }

        public int Value => Math.Max(0, Definition.Value + ValueBonus);

        public bool IsUpgraded => Definition.IsUpgraded;

        /// <summary>这张牌还能不能再升级（每张牌只能升级一次）。</summary>
        public bool CanUpgrade => Definition.CanUpgrade;

        public void ModifyValue(int delta)
        {
            ValueBonus += delta;
        }

        /// <summary>
        /// 融合升级：把这张牌换成它的升级版。已经升级过、或没有设计升级版时返回 false。
        /// 实例本身（Uid、栏位、永久成长）保持不变，所以升级不会移动牌的位置。
        /// </summary>
        internal bool TryUpgrade()
        {
            if (!CanUpgrade)
            {
                return false;
            }

            Definition = Definition.Upgraded;
            return true;
        }

        internal CardInstance CloneForRuntime(int dynamicValueBonus)
        {
            CardInstance clone = new CardInstance(Uid, Definition);
            clone.ValueBonus = ValueBonus + dynamicValueBonus;
            clone.PermanentDiceModifier = PermanentDiceModifier;
            clone.PermanentDiceCountBonus = PermanentDiceCountBonus;
            clone.PermanentHealingModifier = PermanentHealingModifier;
            return clone;
        }
    }
}
