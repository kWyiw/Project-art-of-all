using System;

namespace ProjectArtOfAll.Core.Cards
{
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
    }
}
