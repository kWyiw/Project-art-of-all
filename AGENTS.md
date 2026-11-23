# AGENTS.md — 交接给下一个代理

给接手这个仓库的代理（Codex / 其它）。读完这一页就该能开工，细节往 `规则书/` 里翻。

## 0. 三十秒版本

- 这是一个 **Unity 2022.3.62f2c1 + Built-in 管线** 的桌面游戏原型：外圈是「大富翁式」棋盘（掷骰、逆时针走、地块结算、商店/奖励/事件/陷阱），棋盘下面常驻一条**双栏卡槽带**（背包栏 + 战斗栏，拖拽摆位），走到怪物格就进**自动战斗**（按冷却自动施放，战斗回放有血条与跳字）。
- 玩法与数值的唯一权威是 **`规则书/` 里的八份文档** 和 **`卡牌设计稿/第一套卡牌.txt`**。代码是文档的实现；改行为先改文档，再改代码。
- 内核（`Assets/Game/Core/`）**不依赖 UnityEngine**（asmdef 里 `noEngineReferences: true`），可以用纯 `dotnet` 编译、跑测试、跑模拟。UI 与场景在 `Assets/Scripts/`。
- 所有验证都能在命令行完成，见第 3 节。**没有跑过验证就不要说「改好了」。**

## 1. 铁律（踩过坑换来的）

1. **先读文档再动代码。** 规则书按编号写死规则（`R-MOVE-009`、`CJ-5.11`、`UI-2.5.5`…），代码注释与测试消息里引用的就是这些编号。改规则要同步改文档与断言。
2. **验证三连必须全绿**（第 3 节）：两个编译检查 0 警告 0 错误 + `Game.Core checks passed.`。碰到场景/序列化字段/Editor 脚本，再加跑真实 Unity 批处理编译。
3. **不许编造。** 这个仓库以前发生过「声称做了、其实没做」的事故。只报你真正跑过、真正看过（截图/日志）的结果；失败就贴失败原文。
4. **新 `.cs` / 新资产要手写 `.meta`**，Unity 里生成的 GUID 必须自造，约定是
   `guid = md5("paoa.cs." + 相对仓库根的路径)`，例如 `Assets/Editor/EventPreview.cs` →
   `md5("paoa.cs.Assets/Editor/EventPreview.cs")`。用 python 算：
   `python -c "import hashlib;print(hashlib.md5(b'paoa.cs.Assets/Editor/X.cs').hexdigest())"`。
5. **不要手改 `Assets/Scenes/*.unity` 的大块 YAML。** 场景里的 UI 是「烤」进去的真实组件，重建走第 5 节的烘焙菜单；序列化字段新增后要配**自愈**逻辑（见 6.3），不要靠改场景。
6. **提交保持干净。** 见第 7 节。

## 2. 目录地图

```
Assets/Game/Core/          内核（无 UnityEngine 依赖，唯一有 asmdef 的目录）
  Board/                   地图图、走位求解（MovementSolver）、地块
  Cards/                   卡表（CardDatabase）、定义、效果、光环、文案、融合
  Combat/                  自动战斗模拟、效果结算、光环结算、战斗事件
  Match/                   对局主控（MatchController）、玩家状态、CPU、事件格、陷阱、棋盘层经济
  Commands/ Events/ Rng/ Util/
Assets/Scripts/            Unity 层
  Board/                   棋盘视图与 HUD（BoardGameplayController 是局内总控）
  UI/                      StartMenu / 战斗模拟页（CombatLabView）
  Flow/ View/ Player/ Environment/
Assets/Editor/             烘焙工具、预览脚本、回归探针
Assets/Scenes/             StartMenu / Map1 / Map2 / MainGame / ready room
Tests/Game.Core.Checks/    内核断言（`dotnet run`，3000+ 行，唯一的测试入口）
Tools/CompileChecks/       两个编译检查工程（第 3 节）
Tools/MatchSim|BalanceSim/ 无头对局 / 数值模拟
规则书/                    八份权威文档（见下）
卡牌设计稿/第一套卡牌.txt   卡牌的权威文案与数值
_tmp/                      本地草稿（已 gitignore）：日志、预览 PNG、临时探针
```

规则书清单：
`大富翁层规则书.md`（棋盘/回合/事件/陷阱）、`战斗规则书.md`、`卡牌构筑规则书.md`（背包/战斗栏/融合）、
`战斗与卡牌系统规格书.md`、`棋盘层双栏卡槽UI与拖拽交互规格.md`（卡槽带与所有 HUD 弹窗）、
`卡牌实现核对表.md`（逐张对照设计稿）、`Unity技术方案与开发计划.md`（实现记录，按条追加）、
`本地工具与验证.md`（验证与预览脚本）。

## 3. 验证回路

```bash
# 快速回路（秒级，不需要 Unity 在跑）
dotnet build Tools/CompileChecks/UICompileCheck.csproj -c Release
dotnet build Tools/CompileChecks/EditorCompileCheck.csproj -c Release
dotnet run --project Tests/Game.Core.Checks -c Release     # 以 Game.Core checks passed. 结尾

# 真实 Unity 编译（约 1 分钟；编辑器必须关着）
"/d/unity hub/2022.3.62f2c1/Editor/Unity.exe" -batchmode -quit -projectPath . -logFile _tmp/compile_log.txt
#   成功：退出码 0 + 日志 Exiting batchmode successfully now!；失败搜日志里的 error CS
```

- 编译检查工程的 Unity 安装目录默认是本机路径，换机器用 `-p:UnityInstallRoot="..."` 覆盖。
- 快速回路依赖 `Library/ScriptAssemblies/UnityEngine.UI.dll`（Unity 生成），所以**项目至少要先用 Unity 打开过一次**。
- 离线预览 UI（看不见画面时）：`CardTooltipPreview` / `CombatMeterPreview` / `ModalLayoutPreview` / `HomeStopPreview` / `EncounterPreview` / `EventPreview`，用法见 `规则书/本地工具与验证.md`。
- 回归探针：`TooltipHostProbe.Run`、`CombatLabLibraryProbe.Run`（`-executeMethod`）。

## 4. 架构要点

- **内核事件驱动**：`MatchController.Submit(ICommand)` → 状态变化 → 发 `*Event`（`Assets/Game/Core/Events`、`Match/BoardEvents.cs` 等）。UI 只读状态、只听事件；`CombatPlaybackView` 回放 `BattleSnapshot` 的战斗事件。
- **阶段机**：`MatchPhase` = `AwaitRoll / AwaitMove / AwaitNorma / AwaitReward / AwaitShop / AwaitEvent / Finished`。任何新阶段都要同时改：CPU（`CpuBrain`、`CpuMatchRunner`、`SimpleMatchAutoplayer`）、`BoardGameplayController` 的 `ResolveDeferredCorePhase`、HUD 面板、`HandleMove` 末尾的阶段判断。
- **卡牌**：`CardDatabase` 里 35 张基础牌 + 33 张「+」升级版（`All` 只有基础牌，`AllDefinitions` 含升级版）。同名牌再获得一次会**原地融合**升级（`MatchController.AcquireOrUpgrade`）。玩家文案来自 `CardDesignText`（逐字设计稿原文），卡面本身**不写效果**，效果只在悬停窗里显示。
- **卡槽带**：`CardBandController`（拖拽/列位/高度）+ `CardRowRenderer`（单张牌绘制）+ `CardTooltip`（悬停窗）+ `BoardHudController`（弹窗与 HUD 总装）。
- **场景化 UI**：`Map1`/`Map2`/`StartMenu` 里的 HUD 都是真实场景组件（不是运行时全建），运行时只绑回调与刷数据。

## 5. 烘焙与场景

- `Tools/Project Art of All/Rebake Gameplay UI` / `Validate Gameplay UI`（`Assets/Editor/BakeGameplayUI.cs`）
- `Tools/Project Art of All/Rebake Start Menu UI` / `Validate Start Menu UI`（`BakeStartMenuUI.cs`）
- 改 HUD 结构后跑一次烘焙 + 校验，再提交 `.unity` 改动。**场景能用自愈解决的，优先自愈**（见 6.3），因为手改 YAML 太脆。
- 编辑器菜单、预览、探针都可以用 `-executeMethod <类>.<方法> -batchmode -quit` 批处理调用。

## 6. 已知坑（都真踩过）

1. **PowerShell 5.1**：`&&` 不可用（用 `;` 与 `$LASTEXITCODE`）；heredoc `<<` 是保留字符（脚本先用写文件工具落盘再 `python x.py`）；git 推送时 stderr 会被当成错误，只看 `...  main -> main` 那一行判断成功。
2. **Unity 批处理**：编辑器开着会直接失败（`another Unity instance is running`）；强杀进程后 `Temp/UnityLockfile` 可能残留，删掉再跑；Unity 本体路径是 `D:/unity hub/2022.3.62f2c1/Editor/Unity.exe`。
3. **场景里烤着的旧 UI 缺新字段**：新增 `[SerializeField]` 后，旧场景反序列化出来是 null，旧代码又只判「面板对象存不存在」就跳过重建 → 新控件永远不出现。修法是**自愈**：`EnsureOverlayViews()` 里发现「面板在、但新字段为 null」就销毁重建（参考 `BoardHudController` 的遭遇战面板）。同理，烤进场景的列表（战斗模拟页的卡牌库）要用「按数据源比对、对不上就重建」的方式兜底（`CombatLabView.BindCardLibrary`）。
4. **悬停窗宿主画布口径要一致**：`CardTooltipHost.Register` 记录的是画布根（`CanvasRect`），所有调用点也必须传画布根，否则会互相销毁导致悬停窗再也不出现。
5. **`CanvasGroup.alpha = 0` vs `SetActive(false)`**：需要 `Update()` 继续跑的视图（地块悬停说明）不能用 `SetActive(false)` 隐藏。
6. **骰面修正会被夹**：`EffectAmount.Roll` 把每颗骰子的修正夹在 `[1, 面数]`。要表达「结算数值 +N」（自噬之蛇 +100）必须用 `AuraStat.ValueBonus`（结算后平加），不能用骰面修正。
7. **背包容量**：10c 是**背包这一排**的容量；判断装不装得下必须用 `LoadoutRules.FitsInBag(player.BagCardWidth, size)`，不要用「全部拥有宽度」`OwnedCardWidth`。
8. **`effect` 里没有目标时不会编译过**：`CardDefinition` 构造器会拒绝「既无 effect 也无 aura/passive」的牌。
9. **UI 用 legacy uGUI `Text`**（不是 TMP）；非交互件 `raycastTarget = false`；`Assets/Editor` 是独立程序集，看不到 `Assets/Scripts` 的 `internal` 成员（预览脚本里要自己搭 `Text`）。
10. **Unity 批处理会改写** `Packages/packages-lock.json` / `ProjectSettings/ProjectVersion.txt` / `PackageManagerSettings.asset`：提交前 `git checkout --` 掉，别带进 commit。

## 7. 提交与推送

```bash
git add -A Assets Tests Tools 规则书 .gitignore
git checkout -- Packages/packages-lock.json ProjectSettings/ProjectVersion.txt ProjectSettings/PackageManagerSettings.asset
git -c user.name="agent" -c user.email="agent@local" commit -q -m "feat(scope): ..."
git -c http.schannelCheckRevoke=false -c http.sslVerify=false push origin main
```

- 仓库主人也会往 `main` 上推：push 被拒就 `git rebase origin/main`，**rebase 之后要重跑验证三连**再推。
- 提交信息用英文、`feat/fix/chore/docs(scope): ...` 风格；一次提交只做一件事。
- **别用 `git commit -am`**（会把 Unity 改写的项目设置带进去，也不会带上新文件的 `.meta`）。
- `_tmp/` 已忽略，日志与预览图不用提交。

## 8. 当前状态（HEAD = `49aacf5`）

已具备：棋盘与四家回合、逆行走向与力直格、自家停步选择、路过/停住他人都能选开战或避开、岔路高亮预测落点、商店/奖励/事件（探险委托 + 神秘饮料）/陷阱（铁丝网 + 路障）、35 张卡表按设计稿重做 + 融合升级、棋盘层成长、战斗回放血条与状态数字条、卡槽带拖拽与悬停窗、局内弹窗让位卡槽带、CPU 四家自动对局、无头模拟工具。

已知缺口 / 可继续做的：

- 联机**完全没做**（方案已经讨论过：主机权威 + Relay 最省事；内核可脱离 Unity 跑服务端；`ComputeHash` 可做校验）。
- 「牌组无人机」卡组一览浮窗没做（早期调研里的 ②，只有悬停卡局内已能用）。
- 音量/设置、存档、结算画面等主菜单周边仍是原型。
- `README.md` 是早期的 GBK 编码设计稿，**别用 UTF-8 直接读**（会乱码）；玩法以 `规则书/` 为准。
- 设计稿里以「（」开头的整行是写给实现者的解释，`CardDesignText.Lines` 已把它们滤掉；核对表用的是 `RawLines`。
- 陷阱数值（铁丝网 2/3 回合、路障 3/4 次）与神秘饮料的碎片价是实现里定的，设计稿没有，改之前先问。

## 9. 和这个仓库的主人怎么协作

- 用中文回复；他给截图指出问题时，先定位到具体控件/文件再动手。
- 交付时给：改了哪些文件、行为怎么变、跑了哪些验证（附关键输出行）、提交号与是否已推送。
- 涉及版式改动的，尽量附一张离线渲染的 PNG（第 3 节），他没空开编辑器时这最有用。
