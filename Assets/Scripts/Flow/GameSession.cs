using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Flow
{
    /// <summary>开局模式：主页面选一次，随后跨场景读取。</summary>
    public enum GameMode
    {
        /// <summary>正常模式：1 名真人 + 3 名 CPU（阶段 A 的目标形态）。</summary>
        Normal = 0,

        /// <summary>无 CPU 测试模式：四个座位都由同一个玩家手动操作，用来排查 AI 与验证规则。</summary>
        NoCpuTest = 1
    }

    /// <summary>
    /// 一局的会话上下文：主页面选了模式之后写在这里，对局场景启动时读。
    /// 只存"这一局怎么开"，不存任何对局状态（对局状态只在内核里）。
    /// </summary>
    public static class GameSession
    {
        public const string MatchSceneName = "MainGame";

        /// <summary>当前模式，默认正常模式。</summary>
        public static GameMode Mode { get; private set; } = GameMode.Normal;

        /// <summary>真人席位；无 CPU 测试模式下四个座位都算真人（返回 -1 表示"当前行动者"）。</summary>
        public static int HumanSeat => Mode == GameMode.Normal ? 0 : -1;

        /// <summary>主页面点「正常模式」/「无CPU测试模式」时调用：记住模式并加载对局场景。</summary>
        public static void StartNewGame(GameMode mode)
        {
            Mode = mode;
            SceneManager.LoadScene(MatchSceneName);
        }

        /// <summary>模式的中文名，HUD 与日志共用。</summary>
        public static string ModeLabel(GameMode mode)
        {
            return mode == GameMode.NoCpuTest ? "无CPU测试模式" : "正常模式";
        }

        public static string CurrentModeLabel => ModeLabel(Mode);

        /// <summary>编辑器里直接开对局场景时也能有确定行为：默认正常模式。</summary>
        public static void Reset()
        {
            Mode = GameMode.Normal;
        }
    }
}
