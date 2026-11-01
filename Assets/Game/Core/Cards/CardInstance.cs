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
        public CardDefinition Definition { get; }

        /// <summary>铜锣烧 / 黄金战车: value grows during a run.</summary>
        public int ValueBonus { get; set; }

        /// <summary>幸运饼干 / 淡水: permanent per-die modifier (clamped by the resolver).</summary>
        public int PermanentDiceModifier { get; set; }

        /// <summary>粉碎机: permanently adds whole dice to this card.</summary>
        public int PermanentDiceCountBonus { get; set; }

        public int Value => Math.Max(0, Definition.Value + ValueBonus);

        public void ModifyValue(int delta)
        {
            ValueBonus = Math.Max(0, ValueBonus + delta);
        }
    }
}
