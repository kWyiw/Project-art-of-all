namespace ProjectArtOfAll.Core.Events
{
    public sealed class HealthRestoredEvent : GameEvent
    {
        public HealthRestoredEvent(int playerId, int amount, int actualAmount)
        {
            PlayerId = playerId;
            Amount = amount;
            ActualAmount = actualAmount;
        }

        public int PlayerId { get; }
        public int Amount { get; }
        public int ActualAmount { get; }
    }
}
