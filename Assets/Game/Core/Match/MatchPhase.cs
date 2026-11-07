namespace ProjectArtOfAll.Core.Match
{
    public enum MatchPhase
    {
        AwaitRoll = 0,
        AwaitMove = 1,
        AwaitNorma = 2,
        AwaitReward = 3,
        AwaitShop = 4,
        Finished = 5,

        /// <summary>停在事件格，等玩家在事件面板上挑一个选项（R-EVT-001）。</summary>
        AwaitEvent = 6
    }
}
