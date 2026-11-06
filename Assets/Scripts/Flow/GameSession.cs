using UnityEngine.SceneManagement;

namespace ProjectArtOfAll.Flow
{
    /// <summary>开局模式：主页面选一次，随后跨场景读取。</summary>
    public enum GameMode
    {
        /// <summary>正常模式：准备房间里凑齐四个座位（1 名真人 + 若干 CPU / 远程玩家）。</summary>
        Normal = 0,

        /// <summary>无 CPU 测试模式：四个座位都由同一个玩家手动操作，用来排查 AI 与验证规则。</summary>
        NoCpuTest = 1
    }

    /// <summary>
    /// 一局的会话上下文：主页面选了模式之后写在这里，准备房间、选地图页与对局场景依次读取。
    /// 只存"这一局怎么开"，不存任何对局状态（对局状态只在内核里）。
    /// </summary>
    public static class GameSession
    {
        public const string MatchSceneName = "MainGame";

        /// <summary>主页面场景名（Build Settings 第 0 个）。</summary>
        public const string MenuSceneName = "StartMenu";

        /// <summary>准备房间场景名（Build Settings 里叫 "ready room"）。</summary>
        public const string LobbySceneName = "ready room";

        /// <summary>当前模式，默认正常模式。</summary>
        public static GameMode Mode { get; private set; } = GameMode.Normal;

        /// <summary>
        /// 真人席位。正常模式由准备房间决定（房主坐 0 号席）；
        /// 无 CPU 测试模式返回 -1，表示"轮到谁就是谁，四个座位都手动"。
        /// </summary>
        public static int HumanSeat => Mode == GameMode.NoCpuTest
            ? -1
            : LobbyState.LocalHumanSeat;

        /// <summary>选地图页写入、对局场景读取的棋盘 id。</summary>
        public static string SelectedMapId
        {
            get => MapCatalog.SelectedId;
            set => MapCatalog.SelectedId = value;
        }

        /// <summary>
        /// 主页面点模式卡：记住模式并进入准备房间场景。正常模式先把房间重置成
        /// "房主自己 + 三个空席位"；无 CPU 测试模式不使用房间，直接显示选地图页。
        /// </summary>
        public static void ChooseMode(GameMode mode)
        {
            Mode = mode;
            if (mode == GameMode.Normal)
            {
                LobbyState.Reset();
            }

            SceneManager.LoadScene(LobbySceneName);
        }

        /// <summary>选地图页点「开始对局」：用当前选中的棋盘加载对局场景。</summary>
        public static void StartMatch()
        {
            SceneManager.LoadScene(MatchSceneName);
        }

        /// <summary>局内暂停菜单的「回主页面」：清掉本局模式，回到主页面。</summary>
        public static void BackToMenu()
        {
            Mode = GameMode.Normal;
            LobbyState.Reset();
            MapCatalog.Reset();
            SceneManager.LoadScene(MenuSceneName);
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
            LobbyState.Reset();
            MapCatalog.Reset();
        }
    }
}
