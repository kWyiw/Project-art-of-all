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
        AwaitEvent = 6,

        /// <summary>未知小径选项已付款，等待选择指定传送地块。</summary>
        AwaitEventTarget = 7
    }
}
