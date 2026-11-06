using System;
using System.Collections.Generic;
using ProjectArtOfAll.Board;
using ProjectArtOfAll.Core.Board;
using ProjectArtOfAll.Environment;
using ProjectArtOfAll.Flow;
using ProjectArtOfAll.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectArtOfAll.UI
{
    /// <summary>
    /// 准备房间 + 选地图页。场景里没有挂这个组件——它由 <see cref="Bootstrap"/> 在
    /// "ready room" 场景加载后自动创建，所以不需要改动场景文件。
    /// 正常模式：四个座位（房主自己 + CPU / 未来的远程玩家），坐满且真人准备后房主开始。
    /// 无 CPU 测试模式：跳过座位，直接显示选地图页。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyScreen : MonoBehaviour
    {
        private enum Page
        {
            Lobby = 0,
            MapSelect = 1
        }

        private const float ReferenceWidth = 1920f;
        private const float CardWidth = 404f;
        private const float CardHeight = 372f;
        private const float CardGap = 22f;
        private const float MapCardWidth = 520f;
        private const float MapCardHeight = 470f;
        private const float MapCardGap = 40f;

        private static readonly Color Scrim = new Color(0.018f, 0.028f, 0.032f, 0.90f);
        private static readonly Color Panel = new Color(0.05f, 0.08f, 0.09f, 0.98f);
        private static readonly Color CardColor = new Color(0.075f, 0.115f, 0.125f, 0.98f);
        private static readonly Color ButtonColor = new Color(0.15f, 0.21f, 0.23f, 1f);
        private static readonly Color ButtonHover = new Color(0.22f, 0.30f, 0.32f, 1f);
        private static readonly Color Brass = new Color(0.92f, 0.69f, 0.27f, 1f);
        private static readonly Color TextColor = new Color(0.94f, 0.93f, 0.88f, 1f);
        private static readonly Color MutedText = new Color(0.62f, 0.65f, 0.62f, 1f);
        private static readonly Color Accent = new Color(0.36f, 0.72f, 0.79f, 1f);
        private static readonly Color ReadyGreen = new Color(0.36f, 0.66f, 0.40f, 1f);
        private static readonly Color Danger = new Color(0.52f, 0.22f, 0.20f, 1f);

        private readonly List<GameObject> pageObjects = new List<GameObject>();

        private Font font;
        private RectTransform root;
        private RectTransform screenScrim;
        private Page page = Page.Lobby;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreate(scene);
        }

        private static void TryCreate(Scene scene)
        {
            if (!scene.IsValid() || scene.name != GameSession.LobbySceneName)
            {
                return;
            }

            if (FindObjectOfType<LobbyScreen>() != null)
            {
                return;
            }

            GameObject holder = new GameObject("LobbyScreen");
            holder.AddComponent<LobbyScreen>();
        }

        private void Awake()
        {
            font = BoardHudOverlayFactory.CreateFont();
            PrepareScene();
            EnsureEventSystem();
            BuildCanvas();

            page = GameSession.Mode == GameMode.NoCpuTest ? Page.MapSelect : Page.Lobby;
            LobbyState.Changed += OnLobbyChanged;
            Rebuild();
            Debug.Log(string.Concat(
                "[Lobby] screen ready mode=", GameSession.Mode.ToString(),
                " page=", page.ToString(),
                " map=", MapCatalog.SelectedId));
        }

        private void OnDestroy()
        {
            LobbyState.Changed -= OnLobbyChanged;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (page == Page.MapSelect && GameSession.Mode == GameMode.Normal)
            {
                page = Page.Lobby;
                Rebuild();
                return;
            }

            GameSession.BackToMenu();
        }

        private void OnLobbyChanged()
        {
            if (page == Page.Lobby)
            {
                Rebuild();
            }
        }

        /// <summary>准备房间是 3D 房间场景：停掉第一人称控制、解锁鼠标、关掉门的传送触发。</summary>
        private void PrepareScene()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ReadyRoomPlayerController walker = FindObjectOfType<ReadyRoomPlayerController>();
            if (walker != null)
            {
                walker.enabled = false;
            }

            RingDoorPortal[] portals = FindObjectsOfType<RingDoorPortal>();
            for (int index = 0; index < portals.Length; index++)
            {
                if (portals[index] != null)
                {
                    portals[index].enabled = false;
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject holder = new GameObject(
                "LobbyEventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            holder.transform.SetParent(null);
        }

        private void BuildCanvas()
        {
            GameObject canvasObject = new GameObject(
                "LobbyCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root = canvasObject.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            GameObject scrimObject = BoardHudOverlayFactory.CreateBar(
                root,
                "Scrim",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                Scrim);
            scrimObject.GetComponent<Image>().raycastTarget = true;
            screenScrim = scrimObject.GetComponent<RectTransform>();
        }

        private void Rebuild()
        {
            for (int index = 0; index < pageObjects.Count; index++)
            {
                if (pageObjects[index] != null)
                {
                    pageObjects[index].SetActive(false);
                    Destroy(pageObjects[index]);
                }
            }

            pageObjects.Clear();

            if (page == Page.Lobby)
            {
                BuildLobbyPage();
            }
            else
            {
                BuildMapPage();
            }
        }

        // ------------------------------------------------------------------ lobby

        private void BuildLobbyPage()
        {
            RectTransform holder = CreatePage("LobbyPage");

            Label(holder, "Title", "准备房间", 0f, 40f, ReferenceWidth, 52f,
                40, FontStyle.Bold, TextAnchor.MiddleCenter, Brass);

            Label(holder, "Subtitle",
                GameSession.CurrentModeLabel + " · 四席坐满、真人准备后房主开始对局",
                0f, 98f, ReferenceWidth, 26f,
                18, FontStyle.Normal, TextAnchor.MiddleCenter, MutedText);

            float rowWidth = 4f * CardWidth + 3f * CardGap;
            float startX = (ReferenceWidth - rowWidth) * 0.5f;
            for (int seat = 0; seat < LobbyState.SeatCount; seat++)
            {
                BuildSeatCard(holder, seat, startX + seat * (CardWidth + CardGap), 150f);
            }

            string footer;
            if (!LobbyState.AllSeatsFilled)
            {
                footer = "还有空席位：给每个空位加一个 CPU，或等待远程玩家加入（联机功能开发中）";
            }
            else if (!LobbyState.AllHumansReady)
            {
                footer = "所有座位都坐满了，等真人玩家点「准备」后房主就能开始";
            }
            else
            {
                footer = "四席就绪 —— 房主可以开始，接下来选择这一局的地图";
            }

            Label(holder, "Footer", footer, 0f, 548f, ReferenceWidth, 26f,
                17, FontStyle.Normal, TextAnchor.MiddleCenter, MutedText);

            Button start = MakeButton(
                holder,
                "StartButton",
                "开始游戏",
                (ReferenceWidth - 420f) * 0.5f,
                600f,
                420f,
                76f,
                OpenMapSelect,
                LobbyState.CanStart ? new Color(0.86f, 0.64f, 0.22f, 1f) : new Color(0.16f, 0.19f, 0.20f, 1f),
                26);
            start.interactable = LobbyState.CanStart;

            MakeButton(holder, "BackButton", "返回主页面", ReferenceWidth - 300f, 612f, 240f, 52f, GameSession.BackToMenu,
                ButtonColor, 18);

            Label(holder, "Hint",
                "CPU 席位的风格与难度可以逐个调整；把四个空位都留给 CPU 也能直接开始（未来联机时客机玩家会占据这些席位）",
                0f, 706f, ReferenceWidth, 24f,
                15, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.5f, 0.53f, 0.51f, 1f));
        }

        private void BuildSeatCard(RectTransform parent, int seatIndex, float x, float y)
        {
            LobbySeat seat = LobbyState.Get(seatIndex);
            Color accent = seat.Kind == LobbySeatKind.LocalHuman
                ? Brass
                : seat.Kind == LobbySeatKind.Cpu
                    ? Accent
                    : seat.Kind == LobbySeatKind.RemoteHuman
                        ? new Color(0.62f, 0.52f, 0.86f, 1f)
                        : new Color(0.30f, 0.33f, 0.34f, 1f);

            GameObject cardObject = BoardHudOverlayFactory.CreateBar(
                parent,
                "Seat" + seatIndex,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x, -(y + CardHeight)),
                new Vector2(x + CardWidth, -y),
                CardColor);
            RectTransform card = cardObject.GetComponent<RectTransform>();

            BoardHudOverlayFactory.CreateBar(
                card,
                "Accent",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -4f),
                Vector2.zero,
                accent);

            Label(card, "SeatIndex", seat.Title.ToUpperInvariant(), 22f, 18f, 200f, 22f,
                15, FontStyle.Bold, TextAnchor.MiddleLeft, accent);

            Label(card, "Occupant", seat.OccupantLabel, 22f, 44f, CardWidth - 44f, 34f,
                26, FontStyle.Bold, TextAnchor.MiddleLeft, TextColor);

            string detail;
            switch (seat.Kind)
            {
                case LobbySeatKind.LocalHuman:
                    detail = seatIndex == LobbyState.HostSeat
                        ? "本机玩家 · 房主（可以开始对局）"
                        : "本机玩家";
                    break;
                case LobbySeatKind.RemoteHuman:
                    detail = "等待远程玩家准备（联机功能开发中）";
                    break;
                case LobbySeatKind.Cpu:
                    detail = "由电脑操作 · 只能使用公开信息决策";
                    break;
                default:
                    detail = "空着——加一个 CPU 或等远程玩家";
                    break;
            }

            Label(card, "Detail", detail, 22f, 86f, CardWidth - 44f, 44f,
                15, FontStyle.Normal, TextAnchor.UpperLeft, MutedText);

            BoardHudOverlayFactory.CreateBar(
                card,
                "Divider",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(18f, -142f),
                new Vector2(-18f, -140f),
                new Color(1f, 1f, 1f, 0.06f));

            float buttonX = 22f;
            float buttonWidth = CardWidth - 44f;

            switch (seat.Kind)
            {
                case LobbySeatKind.LocalHuman:
                {
                    Button ready = MakeButton(
                        card,
                        "ReadyButton",
                        seat.Ready ? "已准备（点击取消）" : "准备（点击准备）",
                        buttonX,
                        156f,
                        buttonWidth,
                        52f,
                        () => LobbyState.ToggleReady(seatIndex),
                        seat.Ready ? ReadyGreen : ButtonColor,
                        19);
                    ready.interactable = true;

                    Label(card, "HostNote",
                        seatIndex == LobbyState.HostSeat ? "你是房主：开始游戏由你决定" : "等待房主开始",
                        buttonX,
                        222f,
                        buttonWidth,
                        46f,
                        15,
                        FontStyle.Normal,
                        TextAnchor.UpperLeft,
                        MutedText);
                    break;
                }

                case LobbySeatKind.Cpu:
                {
                    MakeButton(
                        card,
                        "ArchetypeButton",
                        "风格：" + LobbyState.ArchetypeLabel(seat.Archetype) + "    ▸",
                        buttonX,
                        156f,
                        buttonWidth,
                        48f,
                        () => LobbyState.CycleArchetype(seatIndex, 1),
                        ButtonColor,
                        18);

                    MakeButton(
                        card,
                        "DifficultyButton",
                        "难度：" + LobbyState.DifficultyLabel(seat.Difficulty) + "    ▸",
                        buttonX,
                        212f,
                        buttonWidth,
                        48f,
                        () => LobbyState.CycleDifficulty(seatIndex, 1),
                        ButtonColor,
                        18);

                    MakeButton(
                        card,
                        "RemoveButton",
                        "移除 CPU",
                        buttonX,
                        268f,
                        buttonWidth,
                        44f,
                        () => LobbyState.Remove(seatIndex),
                        Danger,
                        17);
                    break;
                }

                case LobbySeatKind.RemoteHuman:
                {
                    MakeButton(
                        card,
                        "RemoveButton",
                        "移出房间",
                        buttonX,
                        156f,
                        buttonWidth,
                        48f,
                        () => LobbyState.Remove(seatIndex),
                        ButtonColor,
                        18);
                    break;
                }

                default:
                {
                    Button add = MakeButton(
                        card,
                        "AddCpuButton",
                        "添加 CPU",
                        buttonX,
                        156f,
                        buttonWidth,
                        52f,
                        () => LobbyState.AddCpu(seatIndex),
                        new Color(0.24f, 0.34f, 0.36f, 1f),
                        19);
                    add.interactable = true;

                    Button invite = MakeButton(
                        card,
                        "InviteButton",
                        "邀请好友（联机开发中）",
                        buttonX,
                        216f,
                        buttonWidth,
                        44f,
                        null,
                        new Color(0.13f, 0.16f, 0.17f, 1f),
                        16);
                    invite.interactable = false;
                    break;
                }
            }

            string status;
            switch (seat.Kind)
            {
                case LobbySeatKind.LocalHuman:
                    status = seat.Ready ? "● 已准备" : "○ 未准备";
                    break;
                case LobbySeatKind.Cpu:
                    status = "● 准备完毕（CPU 无需手动准备）";
                    break;
                case LobbySeatKind.RemoteHuman:
                    status = seat.Ready ? "● 远程玩家已准备" : "○ 等待远程玩家准备";
                    break;
                default:
                    status = "○ 空席位";
                    break;
            }

            Label(card, "Status", status, 22f, 326f, CardWidth - 44f, 24f,
                15, FontStyle.Normal, TextAnchor.MiddleLeft,
                seat.Kind == LobbySeatKind.Empty ? MutedText : accent);
        }

        // -------------------------------------------------------------- map page

        private void BuildMapPage()
        {
            RectTransform holder = CreatePage("MapPage");

            Label(holder, "Title", "选择地图", 0f, 48f, ReferenceWidth, 52f,
                40, FontStyle.Bold, TextAnchor.MiddleCenter, Brass);

            Label(holder, "Subtitle",
                "两套棋盘规则完全一致，只有布局不同；选好后点「开始对局」。",
                0f, 108f, ReferenceWidth, 26f,
                18, FontStyle.Normal, TextAnchor.MiddleCenter, MutedText);

            IReadOnlyList<BoardMapInfo> maps = MapCatalog.All;
            float rowWidth = maps.Count * MapCardWidth + Mathf.Max(0, maps.Count - 1) * MapCardGap;
            float startX = (ReferenceWidth - rowWidth) * 0.5f;
            for (int index = 0; index < maps.Count; index++)
            {
                BuildMapCard(
                    holder,
                    maps[index],
                    startX + index * (MapCardWidth + MapCardGap),
                    170f);
            }

            MakeButton(holder, "StartMatchButton", "开始对局",
                (ReferenceWidth - 420f) * 0.5f, 700f, 420f, 76f,
                StartMatch,
                new Color(0.86f, 0.64f, 0.22f, 1f),
                26);

            string backLabel = GameSession.Mode == GameMode.Normal ? "返回房间" : "返回主页面";
            MakeButton(holder, "BackButton", backLabel, ReferenceWidth - 300f, 712f, 240f, 52f, () =>
            {
                if (GameSession.Mode == GameMode.Normal)
                {
                    page = Page.Lobby;
                    Rebuild();
                }
                else
                {
                    GameSession.BackToMenu();
                }
            }, ButtonColor, 18);

            Label(holder, "MapHint",
                "当前选择：" + MapCatalog.Selected.DisplayName
                + "（" + MapCatalog.Selected.TileCount + " 格）",
                0f, 792f, ReferenceWidth, 26f,
                16, FontStyle.Normal, TextAnchor.MiddleCenter, Accent);
        }

        private void BuildMapCard(RectTransform parent, BoardMapInfo map, float x, float y)
        {
            bool selected = MapCatalog.Get(map.Id) != null
                            && string.Equals(MapCatalog.SelectedId, map.Id, StringComparison.Ordinal);

            GameObject cardObject = BoardHudOverlayFactory.CreateBar(
                parent,
                "Map_" + map.Id,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x, -(y + MapCardHeight)),
                new Vector2(x + MapCardWidth, -y),
                selected ? new Color(0.11f, 0.16f, 0.15f, 1f) : CardColor);
            RectTransform card = cardObject.GetComponent<RectTransform>();

            // CreateBar 默认把 raycastTarget 关掉了，地图卡本身要点选，必须打开。
            Image cardImage = cardObject.GetComponent<Image>();
            cardImage.raycastTarget = true;

            Button button = cardObject.AddComponent<Button>();
            button.targetGraphic = cardImage;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                MapCatalog.SelectedId = map.Id;
                Rebuild();
            });

            BoardHudOverlayFactory.CreateBar(
                card,
                "Accent",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -4f),
                Vector2.zero,
                selected ? Brass : new Color(0.30f, 0.33f, 0.34f, 1f));

            Label(card, "Name", map.DisplayName, 26f, 22f, MapCardWidth - 52f, 40f,
                30, FontStyle.Bold, TextAnchor.MiddleLeft, selected ? Brass : TextColor);

            Label(card, "Subtitle", map.Subtitle, 26f, 66f, MapCardWidth - 52f, 26f,
                17, FontStyle.Normal, TextAnchor.MiddleLeft, Accent);

            Label(card, "Description", map.Description, 26f, 108f, MapCardWidth - 52f, 150f,
                18, FontStyle.Normal, TextAnchor.UpperLeft, TextColor).horizontalOverflow =
                HorizontalWrapMode.Wrap;

            Label(card, "TileCount", map.TileCount + " 格 · " + TileMixLabel(map.Kind),
                26f, 274f, MapCardWidth - 52f, 26f,
                16, FontStyle.Normal, TextAnchor.MiddleLeft, MutedText);

            BuildMapPreview(card, map.Kind, 26f, 314f, MapCardWidth - 52f, 96f);

            Label(card, "State", selected ? "● 已选择" : "○ 点击选择",
                26f, MapCardHeight - 42f, MapCardWidth - 52f, 24f,
                16, FontStyle.Bold, TextAnchor.MiddleLeft, selected ? Brass : MutedText);
        }

        /// <summary>
        /// 卡片下部的棋盘缩略图。用点阵直接画，不用旋转的细条：
        /// 细条一旦锚点/枢轴算错就会压到卡片外面，看着像画面中间莫名其妙的多余线条。
        /// </summary>
        private void BuildMapPreview(
            Transform parent,
            BoardMapKind kind,
            float x,
            float y,
            float width,
            float height)
        {
            GameObject preview = BoardHudOverlayFactory.CreateBar(
                parent,
                "Preview",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x, -(y + height)),
                new Vector2(x + width, -y),
                new Color(0.03f, 0.05f, 0.055f, 1f));
            RectTransform rect = preview.GetComponent<RectTransform>();

            const float pad = 10f;
            if (kind == BoardMapKind.SweetRing)
            {
                DrawRingPreview(rect, width, height, pad);
            }
            else
            {
                DrawClassicPreview(rect, width, height, pad);
            }
        }

        /// <summary>甜点回廊：按真实地块坐标画一圈彩色点阵（菱形环）。</summary>
        private static void DrawRingPreview(RectTransform rect, float width, float height, float pad)
        {
            IReadOnlyList<BoardMapCell> cells = BoardMapLibrary.SweetRing.Cells;
            int min = int.MaxValue;
            int max = int.MinValue;
            for (int index = 0; index < cells.Count; index++)
            {
                BoardMapCell cell = cells[index];
                min = Mathf.Min(min, Mathf.Min(cell.Row, cell.Column));
                max = Mathf.Max(max, Mathf.Max(cell.Row, cell.Column));
            }

            float span = Mathf.Max(1, max - min);
            // 棋盘在场景里整体旋转 45°，缩略图也转成菱形才对得上实际形状。
            float dot = Mathf.Clamp(Mathf.Min(width, height) / (span * 1.45f + 1.2f), 4f, 11f);
            float half = (width - pad * 2f) * 0.5f;
            for (int index = 0; index < cells.Count; index++)
            {
                BoardMapCell cell = cells[index];
                float u = (cell.Column - min) / span - 0.5f;
                float v = (cell.Row - min) / span - 0.5f;
                float x = pad + (width - pad * 2f) * 0.5f + (u - v) * half;
                float y = pad + (height - pad * 2f) * 0.5f + (u + v) * half;
                AddTileDot(rect, x, y, dot, TileDotColor(cell.Type));
            }
        }

        /// <summary>经典广场：12 × 12 的淡色格网（现场地块太多，缩略图只示意密度）。</summary>
        private static void DrawClassicPreview(RectTransform rect, float width, float height, float pad)
        {
            const int lattice = 12;
            float dot = Mathf.Clamp(Mathf.Min(width, height) / (lattice + 3f), 3f, 6f);
            float stepX = (width - pad * 2f) / (lattice - 1);
            float stepY = (height - pad * 2f) / (lattice - 1);
            Color faint = new Color(0.42f, 0.47f, 0.44f, 0.45f);
            Color edge = new Color(0.62f, 0.56f, 0.36f, 0.85f);
            for (int row = 0; row < lattice; row++)
            {
                for (int column = 0; column < lattice; column++)
                {
                    bool onEdge = row == 0 || column == 0 || row == lattice - 1 || column == lattice - 1;
                    AddTileDot(
                        rect,
                        pad + column * stepX,
                        pad + row * stepY,
                        dot,
                        onEdge ? edge : faint);
                }
            }
        }

        private static void AddTileDot(RectTransform parent, float x, float y, float size, Color color)
        {
            BoardHudOverlayFactory.CreateBar(
                parent,
                "Dot",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x - size * 0.5f, -(y + size * 0.5f)),
                new Vector2(x + size * 0.5f, -(y - size * 0.5f)),
                color);
        }

        private static Color TileDotColor(TileType type)
        {
            switch (type)
            {
                case TileType.Home:
                    return new Color(0.97f, 0.95f, 0.86f, 1f);
                case TileType.Reward:
                    return new Color(0.96f, 0.83f, 0.35f, 1f);
                case TileType.Monster:
                    return new Color(0.88f, 0.36f, 0.32f, 1f);
                case TileType.Shop:
                    return new Color(0.42f, 0.80f, 0.53f, 1f);
                case TileType.Warp:
                    return new Color(0.70f, 0.44f, 0.92f, 1f);
                case TileType.Event:
                    return new Color(0.42f, 0.68f, 0.94f, 1f);
                default:
                    return new Color(0.55f, 0.58f, 0.55f, 0.9f);
            }
        }

        private static string TileMixLabel(BoardMapKind kind)
        {
            if (kind != BoardMapKind.SweetRing)
            {
                return "奖励 10 · 怪物 12 · 商店 4 · 事件 14 · 传送 4 · 家 4 · 空地 20";
            }

            // 直接数生成棋盘的数据，改布局不用回来改文案。
            int rewards = 0;
            int monsters = 0;
            int shops = 0;
            int events = 0;
            int warps = 0;
            int homes = 0;
            IReadOnlyList<BoardMapCell> cells = BoardMapLibrary.SweetRing.Cells;
            for (int index = 0; index < cells.Count; index++)
            {
                switch (cells[index].Type)
                {
                    case TileType.Reward:
                        rewards++;
                        break;
                    case TileType.Monster:
                        monsters++;
                        break;
                    case TileType.Shop:
                        shops++;
                        break;
                    case TileType.Event:
                        events++;
                        break;
                    case TileType.Warp:
                        warps++;
                        break;
                    case TileType.Home:
                        homes++;
                        break;
                }
            }

            return string.Format(
                "奖励 {0} · 怪物 {1} · 商店 {2} · 事件 {3} · 传送 {4} · 家 {5}",
                rewards,
                monsters,
                shops,
                events,
                warps,
                homes);
        }

        /// <summary>准备房间的「开始游戏」只是进入选地图页，真正开局在 <see cref="StartMatch"/>。</summary>
        private void OpenMapSelect()
        {
            if (GameSession.Mode == GameMode.Normal && !LobbyState.CanStart)
            {
                return;
            }

            page = Page.MapSelect;
            Rebuild();
        }

        private void StartMatch()
        {
            if (GameSession.Mode == GameMode.Normal && !LobbyState.CanStart)
            {
                return;
            }

            Debug.Log(string.Concat("[Lobby] start match map=", MapCatalog.SelectedId));
            GameSession.StartMatch();
        }

        // --------------------------------------------------------------- helpers

        private RectTransform CreatePage(string name)
        {
            GameObject pageObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = pageObject.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            pageObjects.Add(pageObject);
            return rect;
        }

        private static GameObject Bar(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            Color color)
        {
            return BoardHudOverlayFactory.CreateBar(
                parent,
                name,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(x, -(y + height)),
                new Vector2(x + width, -y),
                color);
        }

        private Text Label(
            Transform parent,
            string name,
            string content,
            float x,
            float y,
            float width,
            float height,
            int size,
            FontStyle style,
            TextAnchor anchor,
            Color color)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);

            Text text = labelObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button MakeButton(
            Transform parent,
            string name,
            string content,
            float x,
            float y,
            float width,
            float height,
            Action onClick,
            Color color,
            int fontSize)
        {
            GameObject buttonObject = Bar(parent, name, x, y, width, height, color);
            Image image = buttonObject.GetComponent<Image>();
            image.raycastTarget = true;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.70f, 0.70f, 0.70f, 0.6f);
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(buttonObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = labelObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.97f, 0.95f, 0.90f, 1f);
            text.text = content;
            text.raycastTarget = false;

            return button;
        }
    }
}
