namespace ProjectArtOfAll.Core.Events
{
    /// <summary>
    /// The only output channel from Core gameplay. Tick and Sequence are assigned by GameEventBus.
    /// </summary>
    public abstract class GameEvent
    {
        public long Tick { get; internal set; }
        public int Sequence { get; internal set; }
    }
}
