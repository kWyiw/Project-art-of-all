# Unity 技术方案与开发计划

> 本文档定位：**技术决策 + 落地计划**，可直接作为立项文档 / 外包沟通依据 / 个人开发排期表。
> 版本：v1.0

---

## 0. 结论先行（TL;DR）

这个项目的难度**不在大富翁，也不在美术，而在"确定性"**。

它同时具备三种互相冲突的特性：

| 特性 | 带来的技术要求 |
|---|---|
| 回合制、可暂停 | 逻辑必须能随时序列化/恢复（断线重连、存档） |
| 实时 CD 自动战斗 | 战斗必须是**固定步长、无浮点、可复现**的确定性模拟 |
| 4 人联机 + CPU 托管 | 必须有一份**服务端权威**的规则实现，且和客户端是同一份代码 |

所以整套方案的骨架只有一句话：

> **把游戏规则写成一份纯 C#（不引用任何 UnityEngine）的确定性内核，让"服务器 / 客户端 / CPU AI / 平衡测试工具"都跑这一份内核；Unity 只负责把内核吐出的"事件流"演出来。**

如果这句话做到了，后面所有模块都是线性工作量；如果没做到（把规则写在 MonoBehaviour 里），到联机阶段基本等于重写。

**推荐路径**：Unity 6 LTS + 纯 C# 内核 + FishNet（主机权威）+ uGUI + ScriptableObject 数据驱动。
**团队 3 人（1 程序 / 1 美术 / 1 策划兼程序）约 10 个月到 EA；单人全栈约 14～18 个月。**

> **项目形态（v1.7 定调，优先级据此排）**：这是**4 人派对游戏**。
> 阶段 A 先做**单机**：1 真人 + 3 CPU。阶段 B 再做**联机**：4 真人。
> **两种模式规则完全一致**——同一份内核、同一套命令接口、同一张地图、同一个卡池、同样的随机分布。差别只有两点：那 3 个席位由谁填，以及命令要不要过网络。
> 由此推出的三条优先级：①**M5（CPU AI）不是可选项，是阶段 A 的必需品**，可玩 Demo 的关键路径是 `M1 → M2 → M4 → M5`；②M6（联机）整块后置，且**后做不会变贵**——单机已经在跑主机权威内核，M6 只加传输层；③任何"为了单机方便而分叉规则"的念头都要立刻掐掉，那是这个架构唯一会翻车的地方。

---

## 1. 设计输入

本方案的上游是五份文档，**数值与手感一律以它们为唯一权威**：

- `大富翁层规则书.md`：棋盘层流程、经济、升级、胜负、回合结构
- `战斗规则书.md`：一场战斗内部的全部裁决
- `卡牌构筑规则书.md`：卡牌、卡槽、获得与失去、构筑合法性
- `棋盘层双栏卡槽UI与拖拽交互规格.md`：卡槽与拖拽的交互规格
- `战斗与卡牌系统规格书.md`：Schema、事件总线、验收步骤

本文只谈**架构与排期**。任何数值（商店价格、抢夺上限、升级曲线、卡槽宽度）都不在这份文档里再写一遍——写两遍就会变成两份互相打架的数字，那正是这类文档失效的最快方式。编码时直接引用规则编号（`R-*` / `BT-*` / `CJ-*` / `UI-*`）。

---

## 2. 技术选型

| 层 | 选型 | 理由 / 备选 |
|---|---|---|
| 引擎 | **Unity 6 LTS（6000.x）**，2D 模板 + URP 2D | 全新项目首选；若团队吃老教程，退 **2022.3 LTS**（资源兼容最好）。两者 API 差异对 2D 项目很小 |
| 逻辑内核 | 纯 C#（.NET Standard 2.1），**零 UnityEngine 引用** | 核心决策，见 §3 |
| UI | **uGUI + TextMeshPro**（局内 HUD、卡牌拖拽）+ **UI Toolkit**（主菜单/设置/房间） | 卡牌拖拽、复杂布局、粒子叠加，uGUI 生态成熟；纯列表式界面用 UI Toolkit 更快。不要全押 UI Toolkit，卡牌拖拽的迭代成本高 |
| 网络 | **FishNet**（开源、MIT） | 备选 Mirror（教程多、性能略弱）、NGO（官方但功能与文档滞后）。FishNet 支持 host/client 与 dedicated server 同构，正好匹配"主机权威"策略 |
| 传输 | Tugboat（UDP）/ 后期可换 Steamworks Datagram | 4 人小包，UDP 足够 |
| 序列化 | **MemoryPack**（源生成、快、无 GC） | 备选 MessagePack-CSharp、protobuf-net。战斗事件流每秒几十条，MemoryPack 的零分配很重要 |
| 确定随机 | **PCG32 / XorShift128+**（自实现，~40 行） | 不用 `UnityEngine.Random`，不用 `System.Random`（跨运行时版本不保证） |
| 数学 | **整型 + Q16.16 定点**（仅在必须小数时） | 骰子、碎片、CD 全是整数，能不用定点就不用 |
| 数据配置 | ScriptableObject（内容） + CSV/GoogleSheet 导出（数值） | 数值走表，策划能改；内容走 SO，程序能改 |
| 存档 | 客户端：JSON/PlayerPrefs；对局：服务端快照 | 对局存档 = 内核状态快照，见 §8 |
| 资源 | Addressables | 地图、角色、卡面按需加载 |
| 动画 | DOTween + 自己写的 `AnimationQueue` | 表现层通过队列串行播放，见 §3.3 |
| 音频 | Unity Audio Mixer（够用）；后期上 FMOD | |
| 本地化 | Unity Localization（第一天就接） | 中英双语，后期加语言零成本 |
| 测试 | NUnit（Unity Test Framework）+ 独立 `dotnet test`（跑纯 C# 内核） | 内核测试不启动 Unity，秒级反馈 |
| CI | GitHub Actions + GameCI + `dotnet test` | 每次提交跑内核单测 + 确定性回归 |
| 版本 | Git + Git LFS（美术资源） | Unity 项目必须配 `.gitattributes` 锁资产合并 |

### 关于"用不用 Steamworks / 联机后端"

- **第一版：Steam 好友局即可**。用 Steam Lobby 做房间列表 + Steam P2P（FishNet 的 Steam transport 或 Facepunch.Steamworks）。零服务器成本，4 人回合制对延迟极宽容。
- **Dedicated Server（Linux headless）不是第一阶段目标**，但架构要留好：内核是纯 C#，把 `Game.Core` 挂到一个 `dotnet` 控制台程序里就是一个服务器，工作量约 1 周。等有排位/反作弊需求再上。
- **账号/战绩/云存档**：不要自研。用 Steam 成就 + 一个极简 REST（或 Supabase / Nakama）即可。

---

## 3. 分层架构（这是整份方案的核心）

```
┌──────────────────────────────────────────────────────────────┐
│  Client (Unity)                                              │
│  ┌────────────┐  ┌──────────────┐  ┌──────────┐  ┌────────┐  │
│  │ Networking │→ │ Presentation │  │   UI     │  │ Input  │  │
│  │  (FishNet) │  │ (动画/特效)  │  │ (uGUI)   │  │        │  │
│  └─────┬──────┘  └──────▲───────┘  └──────────┘  └────────┘  │
└────────┼─────────────────┼───────────────────────────────────┘
         │ Command(输入)    │ GameEvent[] (唯一输出通道)
┌────────▼─────────────────┴───────────────────────────────────┐
│  Game.Core  ← 纯 C#，确定性，无 Unity API                     │
│  ┌──────────┬──────────┬──────────┬──────────┬─────────────┐  │
│  │ Board/   │ Cards/   │ Combat   │ Economy/ │  Match      │  │
│  │ Movement │ Effects  │ (fixed   │ Leveling │  StateMachine│ │
│  │ (Graph)  │ (SO+DSL) │  tick)   │          │             │  │
│  ├──────────┴──────────┴──────────┴──────────┴─────────────┤  │
│  │ DeterministicRng · Clock · EventLog · StateSnapshot      │  │
│  └──────────────────────────────────────────────────────────┘  │
└───────┬──────────────────────────────┬───────────────────────┘
        │ 同一份 DLL/源码               │
┌───────▼────────┐            ┌────────▼─────────┐
│ CPU AI Agent   │            │ Dedicated Server │
│ (行为树/效用)  │            │ (headless Linux) │
└────────────────┘            └──────────────────┘

         ┌──────────────────────────────┐
         │ tools/BalanceSim (命令行)    │  ← 复用 Core，跑 10 万局不出图
         └──────────────────────────────┘
```

### 3.1 为什么"内核必须纯 C#"

四个刚性理由，任何一个都足以成立：

1. **CPU AI 要复用规则**。AI 必须能"虚拟走一步看看结果"，这要求规则可以被实例化、克隆、快速试算。写在 MonoBehaviour 里做不到。
2. **战斗回放要确定**。客户端不模拟战斗，只回放服务器的事件流；若规则在两处分别实现，永远对不齐。
3. **平衡测试要离线跑**。`dotnet run --project tools/BalanceSim -- 100000` 一晚跑十万局，输出胜率/平均时长/经济曲线。这是小型团队做数值平衡唯一的现实手段。
4. **测试要秒级**。纯 C# 单测不需要起 Unity（省 30 秒/次 × 一天 100 次）。

**落地方式（关键工程细节）**：源码放在 `Assets/Game/Core/`（这样 Unity 直接编译），同时在仓库根建一个 `Game.Core.csproj`，用 `<Compile Include="Assets/Game/Core/**/*.cs" />` 把同一批文件纳入 dotnet 编译。**一份源码，两个编译器**，避免出现"Unity 里能跑、服务器编不过"。

### 3.2 三条必须写进代码规范的铁律

| 铁律 | 违反后果 | 检查手段 |
|---|---|---|
| Core 里禁止 `using UnityEngine` | 服务器编不过 / 单测起不来 | Roslyn 分析器或 asmdef 引用隔离 + CI grep |
| Core 里禁止 `float` / `double`（用于玩法数值） | 联机不同步、回放不一致 | 同上；CD 用 int tick，概率用整数百分比 |
| Core 里禁止 `Dictionary` 遍历顺序影响结果 | 结果随机漂移 | Code Review + 确定性哈希测试 |

### 3.3 "事件流"是逻辑与表现之间唯一的桥

所有玩法逻辑的产出只有一个：`List<GameEvent>`。表现层按顺序消费、播动画、播音效。

```csharp
// Core 产出（示意）
public abstract record GameEvent { public int Tick; public int Seq; }

public sealed record DiceRolled(int PlayerId, int Value, int Tick) : GameEvent;
public sealed record PlayerMoved(int PlayerId, int FromNode, int ToNode, int StepIndex) : GameEvent;
public sealed record CardBought(int PlayerId, int CardUid, int ShardCost) : GameEvent;
public sealed record CombatStarted(int AttackerId, int DefenderId) : GameEvent;
public sealed record CardCast(int PlayerId, int SlotIndex, int CardUid) : GameEvent;
public sealed record DamageDealt(int TargetId, int Amount, int ShieldAbsorbed) : GameEvent;
public sealed record CombatEnded(int WinnerId, CombatResult Result) : GameEvent;
public sealed record LeveledUp(int PlayerId, int NewLevel) : GameEvent;
```

客户端消费：

```csharp
// 表现层：严格串行，动画结束才放下一个事件
foreach (var e in events)
{
    var anim = _presenter.Create(e);        // 每个事件映射一个 Tween/Awaitable
    await anim.PlayAsync(destroyCancellationToken);
}
```

**收益**：

- 战斗只需要服务器把事件流发过来，客户端 `await` 着播，**零预测、零回滚、永不同步**（因为战斗期间玩家没有任何输入，这是本项目最大的幸运）。
- 单机模式下同一套代码 → 联机 = 把事件流的来源从"本地内核"换成"网络"。
- 想看回放/做 Bug 复现，只要存下 `seed + 命令流`，重放即可。

### 3.4 权威归属：主机权威（Host Authority），不是 P2P 锁步

| 方案 | 结论 |
|---|---|
| 锁步（RTS 式） | ❌ 需要严格同版本 + 全程模拟，断线重连极难 |
| 全客户端各自模拟 | ❌ 作弊、不同步 |
| **主机权威（一个玩家客户端跑完整 Core）** | ✅ 推荐。4 人好友局，作弊动机低；断线重连只要快照 + 事件补偿 |
| 专用服务器权威 | ✅ 后期做排位时切换，代码路径完全一致（Core 不变，只换宿主） |

主机权威的具体做法：`MatchHost` 是一个普通 C# 类，持有 `GameState` + `IRemoteTransport`。它既可以是 `NetManager.IsServer`，也可以是单机模式下的 `LocalTransport`（直接同步调用）。**单机和联机走同一条代码路径**，不要写两套。

---

## 4. 领域模型（Core 的数据结构）

```csharp
// ── 地图：有向图，不是二维格子 ──────────────────────────
public enum TileType { Start, Home, Reward, Monster, Shop, Teleport, Event, Empty }

public sealed class MapNode
{
    public int Id;
    public TileType Type;
    public int OwnerPlayerId;   // 仅 Home 有效，-1 表示无
    public Vector2Int LayoutPos; // 纯表现用（美术摆位），不参与逻辑
    public List<int> OutEdges;   // 出边（岔路口 = Count > 1）
    public int InEdgeFrom;       // 通常是单入边，传送/汇合点可能多入
}

public sealed class MapGraph
{
    public MapNode[] Nodes;
    public int[] TeleportGroup;              // 同类下标可互传
    public int StepsBetween(int a, int b);   // 用于 AI 估价：最短路
}

// ── 玩家（注意：这里没有 HP。棋盘层不存在生命值，见 战斗规则书 BT-4.7）────
public sealed class PlayerState
{
    public int Id;                 // 0..3
    public string Name;
    public int Level;              // 1..6（战斗内生命上限由 Level 查表得到）
    public int Shard;
    public int WinCount;           // 胜场数（升级另一条轨道）
    public UpgradePath Path;       // Shard 或 WinCount
    public int CurrentNode;
    public int HomeNode;

    public CardInstance[] Backpack = new CardInstance[10];   // 背包 10 格
    public CardInstance[] BattleSlots;                        // 长度随等级 4/6/8/10
    public List<Trap> Traps;      // 自己布下的陷阱
    public int PvpImmuneUntilTurn;
    // ⚠ 不要在这里加 _hp / _shield。护盾只活在战斗内（BT-4.5），
    //    这里一旦有血量字段，"残血补给""复活药"这类系统就会偷偷长回来。
}

// ── 卡牌 ────────────────────────────────────────────────
public sealed class CardDefinition      // 来自 ScriptableObject
{
    public string Id;
    public int SlotCost;                // 1c / 2c
    public int CooldownTicks;           // 0 = 无 CD（理财卡）
    public CardCategory Category;       // 攻击/技能/能力/理财/消耗品/陷阱
    public CardScope Scope;             // Combat / Passive / Sell / OnStop ...
    public int BaseSellPrice;
    public List<EffectData> Effects;    // 组成式效果
    public string TextZh, TextEn;
    public string ArtKey;
}

public sealed class CardInstance
{
    public int Uid;                     // 全局唯一，用于事件引用
    public string DefId;
    public int SlotIndex;               // 在哪个栏位的第几格
    public bool IsBattleSlot;
    public List<CardModifier> RuntimeModifiers; // "好运"这类动态修正
}
```

### 4.1 关于 `1c / 2c` 的格位系统

卡牌占 1～2 格，背包 10 格、战斗栏 4～10 格。两种实现：

| 方案 | 优点 | 缺点 | 结论 |
|---|---|---|---|
| A. 固定槽位数组（每格放一张 1c；2c 卡占 `SlotIndex` 与 `SlotIndex+1` 两个） | 简单，拖拽直观，相邻关系好算 | 2c 卡只能从偶数格起放（需要偏移规则） | ✅ 推荐（MVP） |
| B. 真实二维网格（6×4 拼图） | 空间玩法深度高 | 拖拽、旋转、寻路、UI 工作量 ×3 | 后期可升级 |

**用 A 起步**，把 `SlotCost` 保留在数据里，将来换 B 只改布局层。

### 4.2 相邻修正（"好运：右侧相邻攻击牌 +2 点数"）的实现

这是卡牌系统里最容易写成一团乱麻的地方。正确的做法是**两阶段结算**：

```
阶段 1（静态重算 static recompute，在布局变化/战斗开始时执行一次）：
    for each card in battleSlots:
        modifiers[card] = 所有 aura 来源的修正之和
    → 结果缓存进 CardInstance.RuntimeModifiers，带 dirty 标记

阶段 2（运行时，卡牌触发时）：
    finalValue = DieRoll(base) + sum(modifiers)   // 先投骰，再加修正
    → 严格按 (tick, slotIndex, uid) 排序避免顺序不确定
```

要点：

- **投骰与修正的先后必须写进卡牌文本规则**（"造成 1D6 伤害"配"好运 +2"，是 1D6+2 还是 (1+2)D6？文档没写，建议 **先骰后加**，并在卡面写清）。
- `aura` 卡（好运）自身**不消耗 CD**，是常驻被动；把它标成 `Scope = BattleSlotPassive`。
- 布局一变就 `dirty = true`，在下一个 tick 边界统一重算，**不要在事件回调里就地改数值**。

---

## 5. 卡牌效果系统：三种方案怎么选

这是项目的**第二个关键决策**（第一个是纯 C# 内核）。做错了，每加 10 张卡就要改一次核心代码，最后卡牌数量会停在 30 张。

| 方案 | 加一张新卡的成本 | 上限 | 适用 |
|---|---|---|---|
| ① 每种卡一个 C# 类 | 改代码 + 重新编译 | 30～50 张后失控 | ❌ |
| ② **ScriptableObject 组成式**（原子效果 + 参数） | 拖拽配置，0 行代码 | 100～150 张 | ✅ **推荐** |
| ③ 脚本 DSL（Lua/自定义表达式） | 写一行脚本 | 上限极高，但需自建运行时/沙箱/调试器，投入 3～4 周 | 后期再说 |

**推荐的 ② 的具体形态**：

```csharp
// 原子效果（枚举 + 参数），ScriptableObject 里拖拽组合
public enum EffectOp {
    DealDamage, GainShield, ApplyPoison, ApplyBurn, Heal, GainShard,
    GainDiceModifier, RepeatNext, TeleportToHome, PlaceTrap, StealShard, DrawTempCard
}

public sealed class EffectData
{
    public EffectOp Op;
    public DiceExpr Amount;     // 1D6+2 / 固定值 / 对手碎片的 20%
    public EffectTarget Target; // Self / Enemy / AdjacentCard
    public int Chance;          // 0..100 整数百分比
}

// 六个示例卡在数据里的样子（无需一行新代码）：
// 打击   : 1c, CD180, [DealDamage 1D6]                    Target=Enemy
// 防御   : 1c, CD180, [GainShield 1D6]                    Target=Self
// 剧毒   : 2c, CD180, [ApplyPoison 1D4]                   Target=Enemy
// 好运   : 1c, CD0,   [GainDiceModifier +2]               Target=AdjacentRight(AttackOnly)
// 草场   : 2c, CD0,   [GainShard 1]  触发时机=OnActionEnd  Scope=Passive, 背包也生效
// 车票   : 1c, CD0,   [TeleportToHome] 触发时机=OnSell     Scope=Sell
// 敲诈   : 1c, CD0,   [PlaceTrap gold=5] 触发时机=OnSell   Scope=Sell
```

**触发时机（Trigger）枚举是这套系统的关键**，先把文档里所有"出售时/行动后/停在/战斗开始时/受击时"列出来：

```
OnBattleStart / OnBattleEnd / OnCast(CD到期) / OnHit / OnHurt /
OnSell / OnBuy / OnStopAtTile / OnPassTile / OnActionEnd / OnTurnStart /
OnLevelUp / OnPvpWin / OnPvpLose / OnTrapTriggered
```

**扩展性兜底**：留一个 `IEffectHandler` 接口，只有确实无法用原子效果表达的卡（比如"复制对手血量最低的卡"）才写 C# 类。比例控制在 10% 以内。

**编辑器收益**：写一个卡牌编辑器窗口（列表 + 搜索 + 数值校验 + 一键生成 `CardDefinition` 资产 + 自动导出对照表），投入约 3 天，之后每张卡 3 分钟配完。这是投入产出比最高的工具。

---

## 6. 回合流程：权威状态机

文档第三节的流程是**线性描述**，但"行走中遇到岔路/玩家/home 要暂停等待选择"意味着**移动是可中断、可恢复的异步过程**。必须用显式状态机表达，否则联机 + 断线会崩。

```
Idle
 └→ TurnStart          （全局发碎片：每 3 轮全员 +5，见 `大富翁层规则书.md` §2.6）
     └→ FreeAction     （自由操作：买卡/卖卡/调布局）   ← 有回合计时 90s，超时自动跳过
         └→ Rolling    （掷骰，RNG 从 (seed, turn, playerId) 派生）
             └→ Moving ←────────────────────┐
                 ├─ 到达岔路口 → AwaitChoice(Fork)   ──┐
                 ├─ 路过其他玩家 → AwaitChoice(PvP)  ──┤→ 玩家选择后回 Moving
                 ├─ 路过自己 home → AwaitChoice(Stop) ─┘
                 ├─ 步数耗尽 → TileResolve
                 └─ 传送到其他传送格 → 从新节点继续计步
                     └→ TileResolve  （按地块类型分发：奖励/怪物/商店/事件）
                         ├─ 商店 → ShopUI（同一 FreeAction 规则）
                         ├─ 怪物/玩家 → Combat
                         └─ 到达 home 且满足升级条件 → LevelUp
                             └→ TurnEnd（OnActionEnd 结算：草场等理财卡发钱）
                                 └→ 下一玩家 / 全局计时 → TurnStart
```

**必须实现的三个"防卡死"机制**（联机游戏 80% 的差评来自这里）：

1. **每个 AwaitChoice 有超时**（默认 25 秒），超时取默认项（岔路走第一条边 / PvP 选择继续走 / home 选择不停）。
2. **断线玩家自动托管**：AI 接管，重连后归还。（直接复用 §9 的 CPU AI，一份代码两种用途）
3. **完整的 `TurnLog`**：记录每个状态迁移，出 Bug 时能一键导出对局 JSON 复现。

> 实现建议：状态机不要用第三方库，就用一个 `MatchPhase` 枚举 + `MatchController.Tick()` 的 switch，每个阶段自己决定何时迁移。150 行以内，比任何框架都好调试。

---

## 7. 战斗模块（确定性实时模拟）

这是全场唯一"实时"的部分，也最容易出错。

### 7.1 规则

- **固定步长 60 tick/s**，逻辑里只有 `int tick`，没有任何 `Time.deltaTime`。
- 每个战斗卡牌槽位维护 `ReadyAtTick`。`tick == ReadyAtTick` 时释放，释放后 `ReadyAtTick += CooldownTicks`。
- 释放顺序：同一 tick 内按 `slotIndex` 升序，再按 `uid` 升序（消除不确定性）。
- 目标选择：默认"对方"（单位对单位）；若将来做多单位，按 `最低血量 → slotIndex` 确定。
- 超时惩罚：1200 tick（20 秒）起，每 6 tick 双方各受 `(k+9)/10` 伤害（走普通 absorb 路径，会被护盾吸收）；3600 tick（60 秒）强制平局。
- 胜负：任一方 HP ≤ 0 → 结束；同 tick 双方都 ≤ 0 → 平局。

### 7.2 架构

```csharp
public sealed class CombatSim
{
    private CombatState _state;
    private readonly List<GameEvent> _events = new();

    public bool IsFinished { get; private set; }

    /// 由外部按 60Hz 驱动；单机模式跟 Time，联机模式由主机驱动后发事件
    public void Tick()
    {
        _state.Tick++;
        ProcessTimedEffects();     // 中毒/灼烧/再生/超时惩罚（惩罚走普通 absorb，会被护盾吸收）
        ResolveReadyCards();       // 扫描 ReadyAtTick == Tick
        CheckVictory();
    }

    public CombatState Clone();                    // 给 AI 试算用（关键！）
    public ulong ComputeHash();                    // 确定性校验
}
```

**`Clone()` 是给 AI 用的**：AI 在"买这张卡还是那张卡"时，会克隆自己的状态跑一遍虚拟战斗看胜率。没有纯 C# 内核就做不了这个，AI 会非常蠢。

### 7.3 表现与逻辑的同步

战斗期间玩家 **0 输入**（这是本设计最幸运的地方），所以：

```
主机：跑 CombatSim（60Hz）→ 攒事件 → 每 100ms 打包发一批（带 tick 戳）
客户端：收包 → 入队 → AnimationQueue 串行播放 → 与逻辑进度差保持在 300ms 内
```

**不需要预测、不需要回滚、不需要状态同步**，只需要事件流。这是我把"确定性内核"列为第一优先级的最大回报。

### 7.4 战斗的"可观看性"

自动战斗如果只是两根血条在掉，会很无聊。建议表现层加：

- 卡牌图标从槽位飞向目标（0.15s）+ 命中顿帧（0.05s）+ 屏幕轻微震动。
- 伤害数字按"1D6=4(+2)"的形式显示骰子过程，让玩家看懂"我的构筑为什么弱"。
- 战斗结束时给出贡献统计（哪张卡打了多少伤害）——这是构筑类游戏留存的关键反馈。

> 贡献统计顺便就是**平衡工具**：它能直接告诉你"打击"占了 40% 的输出，该削了。

---

## 8. 存档 / 断线重连 / 回放（一套东西）

三者本质相同：**恢复一个 `GameState`，然后重放一段事件**。

```
存档文件 = {
  version, seed,
  stateSnapshot: 完整 GameState 的 MemoryPack 二进制（通常 < 20KB）,
  commandLog: [ {turn, playerId, command} ... ]   // 可选，用于回放/校验
}
```

- **本地存档**：每个玩家回合结束自动存一次（写临时文件再原子重命名，防断电毁档）。
- **断线重连**：主机把快照 + 最近 200 条事件发给重连者，客户端先 `Load(snapshot)`（不播动画），再从"当前状态"继续播。
- **回放**：`seed + commandLog` 重跑内核，配一个"回放播放器"（复用 AnimationQueue）。开发期配合确定性哈希，能定位 99% 的规则 Bug。
- **版本兼容**：快照里写 `rulesVersion`，跨版本直接判"存档不兼容"比"试图迁移"安全得多。

---

## 9. CPU AI

分三层，逐层加成本：

| 层 | 做法 | 投入 | 效果 |
|---|---|---|---|
| L1 规则驱动（MVP 必做） | 固定策略：有钱就买、血少就卖、看到比自己弱的人就打 | 3 天 | 能陪玩，但蠢 |
| L2 效用评估 | 对每个可选动作算分：`Δ(血量预计) + Δ(碎片) + Δ(升级距离)`，用 `CombatSim.Clone()` 做 20 次未来战斗采样 | 1.5 周 | 明显的"感觉在思考" |
| L3 难度分档 | 用 L2 的评分加噪声/限制信息量造出"简单/普通/困难" | 3 天 | 新手体验 |

**实现要点**：

- AI 必须是 Core 的一个"客户端"，只能通过 `IMatchCommandSink` 发命令，**不能直接改 GameState**（否则必然写出作弊 AI）。这条在单机模式下同样成立——单机跑的就是主机权威内核，AI 与真人走同一条命令入口（`R-CPU-001`）。
- **信息集必须与真人相等**（`R-CPU-002`）：AI 只能读到公共状态 + 自己的卡槽，**不能读对手背包里的具体卡牌与效果数值**。日志里要能断言这一点，否则难度分档就是假的（"困难"只是"偷看"）。
- 加一个 `AiThinkingDelay`（0.8～1.5s 随机），否则 AI 秒回会让人怀疑自己在和脚本玩；但**决策本身必须在 300ms 内算完**（联机托管 100ms，`R-CPU-003`）。
- 三个 CPU 应该长得不一样：同一份内核 + 同样的信息集，只把评分权重随机化（激进/保守/理财/狩猎），人格存进对局数据。否则四人局会变成"三个同一个对手"。
- **反直觉但重要**：AI 是平衡测试的副产品。L2 做出来之后，`BalanceSim` 跑 10 万局就能出"哪条升级路线胜率高 20%"这种报告。

> **排期影响（v1.7）**：既然是"先做单机"，M5（CPU）从"可选优化"变成**阶段 A 的必需项**，可玩 Demo 的关键路径是 `M1 → M2 → M4 → M5`，M6（联机）整个后置。好消息是 M6 的成本不会因此上升——单机和联机跑的是同一份内核与同一条命令路径，M6 只增加传输层。

---

## 10. 联机模块

| 功能 | 方案 | 备注 |
|---|---|---|
| 房间 | Steam Lobby（好友局）/ IP 直连（调试） | 房间列表 + 邀请链接 |
| 人数 | **固定 4 人**（1 真人 + 3 CPU，或 4 真人） | v1 的平衡与 UI 只针对 4 人局；引擎里 `playerCount` 仍参数化，方便用 2 人局做快速调试，但不承诺 2 人平衡 |
| 权威 | 主机权威（§3.4） | 房主退出 → 转移到"等级最高/在线最早"的玩家 |
| 断线 | 60s 重连窗口 + AI 托管 | |
| 观战 | 后期；事件流天然支持 | |
| 延迟容忍 | 回合制，命令走可靠通道；战斗事件走可靠有序通道 | 不需要任何插值/预测网络代码 |

**联机的真正工作量不在同步，在"异常路径"**：房主拔线、玩家超时、同时触发、中途加入、版本不一致。建议列一张 30 条的"异常用例清单"，逐条写测试——这是联机质量的全部。

---

## 11. UI 实现方案

按文档第四节：

```
┌──────────────────────────────────────────────────────────────┐
│ [P1 头像 等级 碎片 升级方式]              [P4 头像 ...]      │  ← 四角玩家信息（独立 Canvas，不随地图缩放）
│                                                              │
│                    ┌────────────────────────┐                │
│                    │                        │                │
│      [P2]          │      地图视口           │      [P3]      │  ← 正交相机，聚焦自己，可拖动/缩放
│                    │   （节点图 + 角色）     │                │
│                    │                        │                │
│                    └────────────────────────┘                │
│  ┌────────────────────────────────────────────────────────┐  │
│  │ 战 斗 栏：[ ][ ][ ][ ]      4/6/8/10 格                 │  │  ← 下方两列车队
│  │ 背 包 栏：[ ][ ][ ][ ][ ][ ][ ][ ][ ][ ]  10 格          │  │
│  └────────────────────────────────────────────────────────┘  │
│  [掷骰子]  [回合 12/90s]                                     │
└──────────────────────────────────────────────────────────────┘
事件（战斗/商店/奖励）→ 覆盖"卡组以上"区域，半透明遮罩 + 悬浮窗
```

**关键技术点**：

1. **地图不随 HUD 缩放**：地图用独立 `Camera`（正交，`LayerMask` 隔离），HUD 用 `Screen Space - Overlay` Canvas。聚焦自己 = 相机平滑跟随 + 边缘小箭头指向其他玩家。
2. **卡牌拖拽**：`IBeginDragHandler/IDragHandler/IEndDragHandler` + 一个"卡牌幽灵层"（DragLayer，最高 Sort Order）。落点校验在 `IEndDragHandler` 里做，非法落点播回弹动画。
3. **布局缓存**：每次拖拽重算所有槽位的目标位置时用 `LayoutGroup` 会有性能抖动，建议**自己算目标位置 + DOTween 移动**，不用 LayoutGroup。
4. **大量卡牌实例**：池化 `CardView`（20 个足够），不要 Instantiate/Destroy。
5. **事件悬浮窗**：做成一个统一的 `EventPopupController`，内部用 `switch (eventType)` 加载不同 Prefab（商店/奖励/战斗/升级），避免 5 套弹窗代码。

**关于 uGUI vs UI Toolkit 的最终建议**：局内 HUD 全部 uGUI；主菜单、设置、房间列表、卡牌图鉴用 UI Toolkit（`UIDocument` + USS，写起来像网页）。两者可以共存在同一个场景。

---

## 12. 目录结构与 Assembly Definition

```
DiceBazaar/
├─ Assets/
│  ├─ Game/
│  │  ├─ Core/                    # asmdef: Game.Core  ← 禁止引用 UnityEngine
│  │  │  ├─ Board/                # MapGraph, MovementSolver
│  │  │  ├─ Cards/                # CardDefinition, EffectResolver, AuraCalculator
│  │  │  ├─ Combat/               # CombatSim, CombatState, StatusEffects
│  │  │  ├─ Economy/              # ShardLedger, ShopService
│  │  │  ├─ Progression/          # LevelTable, UpgradeService
│  │  │  ├─ Match/                # MatchController (状态机), TurnLog
│  │  │  ├─ Rng/                  # Pcg32, DiceExpr
│  │  │  ├─ Events/               # GameEvent 定义
│  │  │  └─ Snapshot/             # 序列化
│  │  ├─ Protocol/                # asmdef: Game.Protocol (网络消息 + MemoryPack)
│  │  ├─ Client/
│  │  │  ├─ Net/                  # asmdef: Game.Client.Net (FishNet)
│  │  │  ├─ Presentation/         # asmdef: Game.Client.Presentation (动画队列/表现)
│  │  │  ├─ UI/                   # asmdef: Game.Client.UI (uGUI)
│  │  │  └─ Flow/                 # asmdef: Game.Client.Flow (场景切换/主菜单)
│  │  └─ Ai/                      # asmdef: Game.Ai（Core 的消费者）
│  ├─ Content/                    # ScriptableObject 资产 + Addressables
│  │  ├─ Cards/  Maps/  Characters/  Balance/
│  ├─ Art/  Audio/  Prefabs/  Scenes/
│  └─ Tests/                      # asmdef: Game.Tests（引用 nunit）
├─ Server/                        # dotnet 控制台/ASP.NET（引用 Game.Core）
├─ tools/
│  ├─ BalanceSim/                 # dotnet run -- 100000
│  └─ CardEditor/                 # Unity Editor 窗口
├─ Game.Core.csproj               # <Compile Include="Assets/Game/Core/**/*.cs"/>
└─ docs/DesignDecisions.md
```

**为什么要折腾 asmdef**：Core 独立成程序集后，"有没有引用 UnityEngine"是**编译器级**的强制，而不是靠自觉。这是整个方案里最便宜、最有效的一条投资。

---

## 13. 里程碑计划（3 人团队 / 约 40 周）

> 每一阶段都以"**可运行的构建产物**"为验收，不接受"代码写完了但跑不起来"。

### M0 · 预研与骨架（2 周）
- 建仓、Unity 版本锁定、`.gitattributes`、asmdef 分离、CI（`dotnet test` + `Unity -batchmode -runTests`）。
- 通读五份规则书，把编码前仍待拍板的点写进 `DesignDecisions.md`（数值口径以规则书为准，不另立一份）。
- **验收**：Core 能在 `dotnet test` 里跑通一个"掷 1000 次 1D6，分布误差 < 1%"的测试。

### M1 · 行动内核原型（4 周）："能走了"
- MapGraph 数据结构 + 编辑器摆图工具（GraphView 或简单的手写编辑器窗口）。
- 骰子、移动、岔路暂停、传送、home 停留、经过其他玩家。
- 纯命令行/文字界面的对局（不用美术）。
- **验收**：4 个 AI 在自己的回合里走完 200 回合不卡死、不越界、状态哈希每次一致。

### M2 · 卡牌 + 战斗内核（6 周）："能打了"
- CardDefinition / EffectOp / 效果解析器 / aura 重算。
- CombatSim 60Hz、CD、中毒、护盾、20 秒超时惩罚 / 60 秒平局。
- 6 张示例卡（打击/防御/剧毒/好运/草场/车票/敲诈）全部数据化配置。
- **验收**：两个 AI 卡组对战 10 万局，平均时长 12～20s，胜负比 45%～55%（说明数值没有一边倒）；同一 seed 两次跑出的事件流字节级一致。

### M3 · 经济 + 升级 + 胜负闭环（4 周）："能赢能输"
- 商店、碎片经济、全局发碎片、PVP 抢夺、双轨升级、6 级胜利判定。
- **验收**：一整局 4 AI 对战能跑到有胜者，输出统计报表（平均局时长、碎片曲线、升级路径胜率）。

### M4 · 表现层与 UI（6 周）："像个游戏了"
- AnimationQueue + 全部 GameEvent 的表现映射。
- 地图渲染（节点 + 路径 + 角色棋子 + 相机跟随）。
- HUD：四角玩家栏、掷骰、事件悬浮窗。
- **棋盘层常驻双排卡槽带**（上排背包 10c / 下排战斗栏 4~10c）+ 自由拖拽 + 插入式重排 + **拖进专用出售区松手即卖**（无确认无撤销，拖出区外回弹）+ 悬停 tooltip + 右固定详情面板。**规格见 `棋盘层双栏卡槽UI与拖拽交互规格.md`**；这是本阶段最容易超时的一块，建议**第 1 周就做只有 4 张占位牌的可拖拽原型**并找真人试手感（风险登记册第 10 条的落地手段）。
- 主菜单 / 设置 / 暂停。
- **验收**：一个没看过文档的人能自己玩完一整局；且**能只靠拖拽把一张牌从背包换到战斗栏、误卖后撤销、并看到相邻加成从"未生效"变"生效"**。

### M5 · CPU 对手与平衡（4 周）："能一个人玩了"
- L1 → L2 效用 AI、四种人格权重、难度分档、思考延迟（0.8~1.5s 表现延迟，但决策 300ms 内）。
- **信息集断言**：AI 只能读公共状态 + 自己的卡槽（`R-CPU-002`），要有单测证明它看不到对手背包。
- BalanceSim 出第一份平衡报告，按报告调数值（这 4 周里至少 1 周纯调数值）。
- **验收**：3 个 CPU 在同一局里表现出可区分的行为差异；AI 普通难度对新手胜率 ~50%，困难 ~75%；500 局全部自然结束、中位圈数 8~10。**此时可以开始做 Demo 给朋友玩。**

### M6 · 联机（6 周）："能一起玩了"
- FishNet 接入、主机权威、房间、Steam Lobby、重连、超时托管、异常用例 30 条。
- **注意**：M6 的前提是"单机已经在跑主机权威内核 + 命令流"，所以这 6 周不包含任何规则改造。如果此时发现规则里有"单机特供分支"，M6 立刻膨胀一倍。
- **验收**：4 人跨网络连续 20 局，无不同步、无卡死、断线 30s 内可恢复；同一 seed 下"单机 1+3CPU"与"联机 4 真人喂同一命令流"产生完全相同的事件流哈希。

### M7 · 内容量产 + 打磨（8 周+）
- 卡牌从 7 张扩到 40～60 张（数据配置，每张 3 分钟）。
- 2～3 张地图、可选角色、事件地块。
- 美术替换占位图、音效音乐、胜利动画、本地化、教程关。
- **验收**：Steam Playtest / Demo 可发布。

### 关键路径与压缩建议

- **关键路径**：M1（行动）→ M2（战斗）→ M4（表现）→ M5（CPU）。**到"可玩单机 Demo"为止只有这四步**（约 20 周，3 人配置）。M6（联机）是阶段 B 的第一站，不在阶段 A 的关键路径上。卡牌数量、美术、事件地块都是并行可加的尾巴。
- **想更快看到 demo**：M1+M2 用纯文字先跑通（控制台就能玩），直接跳 M4 做"单机可玩" → 约 16 周出可玩 Demo，拿反馈再决定 M6 的规格。
- **单人全栈**：上面每阶段 ×1.6～1.8，总量 14～18 个月。**建议单人版先砍联机 + 砍美术自制（用商用素材包），把时间全砸在 M1/M2/M4/M5。**

### 团队配置对照

| 配置 | 人 | 到 EA 周期 | 说明 |
|---|---|---|---|
| 最小可行 | 1 程序 + 1 美术（外包） | 12～14 个月 | 策划由程序兼，数值靠 BalanceSim 硬调 |
| 推荐 | 1 主程 + 1 玩法程序 + 1 美术 | 9～10 个月 | 本文档基准 |
| 加速 | +1 内容策划（专做卡牌/数值） | 7～8 个月 | 卡牌数量是这款游戏的内容护城河 |

---

## 14. 风险登记册

| # | 风险 | 概率 | 影响 | 缓解措施 |
|---|---|---|---|---|
| 1 | 规则写进 MonoBehaviour，联机阶段重写 | 高 | 致命 | §3 纯 C# 内核 + asmdef 强制隔离，第 1 周就立 |
| 2 | 战斗静默不同步（浮点/遍历顺序） | 中 | 致命 | 禁用 float、事件确定排序、每局比对状态哈希、CI 回归 |
| 3 | 卡牌数量上不去（每张卡改代码） | 高 | 高 | 组成式效果系统 + 卡牌编辑器窗口（M2 就必须做） |
| 4 | 数值失衡（某条升级路线/某张卡无敌） | 高 | 高 | BalanceSim 十万局离线跑 + 战斗贡献统计 |
| 5 | 联机异常路径没覆盖，玩家卡死 | 中 | 高 | 30 条异常用例清单 + 全员超时兜底 + AI 托管 |
| 6 | 单局太长（回合数过多导致疲） | 中 | 中 | 全局发碎片 + 强制 6 级结束 + 可配"短局"模式；用 BalanceSim 盯平均局时长 |
| 7 | 大富翁"运气感"压过"构筑感" | 中 | 中 | 让卡牌能改骰子（+1 步/重掷/转向）、让玩家有"选择方向"的权力；这既是玩法也是卖点 |
| 8 | Scope 膨胀（先做联机再做内容） | 高 | 中 | 锁定"单机 40 张卡 + 2 地图"为 EA 内容底线，联机在 M6 前不碰 |
| 9 | 美术成本超支 | 中 | 中 | 卡面用"图标 + 程序化边框"而不是全手绘插画；地图用 Tile 模块化 |
| 10 | UI 卡牌拖拽体验差（手感问题） | 中 | 中 | 尽早做 M4 的可拖拽原型，找真人测试，别做完再调 |

---

## 15. 性能与平台目标

**性能目标（这游戏不应该有性能问题，但要有底线）**

- 60 FPS / 1080p，Steam Deck 上 40 FPS+。
- 战斗期间 GC Alloc = 0（事件用 struct/池化 List，MemoryPack 零分配）。
- 单局内存 < 300 MB。
- 载入地图 < 2s（Addressables 预加载 + 地图 shader 预热）。

**平台**
- 首发：Windows（Steam），Steam Deck 验证（手柄 + 触屏都要能用）。
- 架构上不锁定平台；触屏支持在 M4 顺手做（uGUI 一套代码）。

---

## 16. 本周就能开始的 10 件事（Checklist）

- [ ] 1. 建 Git 仓库 + Unity 6 LTS 项目 + `.gitattributes` + LFS。
- [ ] 2. 按 §12 建目录与 asmdef，`Game.Core` 里放一个 `Dice.cs` 验证没有 UnityEngine 引用。
- [ ] 3. 写 `Game.Core.csproj`，跑通 `dotnet test`。
- [ ] 4. 实现 `Pcg32`，写分布测试（1D6 × 100k，误差 < 1%）。
- [ ] 5. 用纸笔画出第一张地图：18～24 个节点、2 个岔路、2 组传送、4 个 home，拍照存档。
- [ ] 6. 通读 §1 列出的五份规则书，把编码前仍需拍板的点记下来（数值已全部在规则书里，不要另起一份口径）。
- [ ] 7. 定义 `GameEvent` 列表（20 条以内），和美术/自己约定"每个事件配一个表现"。
- [ ] 8. 实现 `CombatSim.Tick()` 的最小版：2 张卡（打击/防御）+ 血条，纯控制台输出战斗日志。
- [ ] 9. 把 7 张示例卡写成 `CardDefinition` 的 SO 资产（哪怕字段先空一半）。
- [ ] 10. 建 `BalanceSim` 空项目，占位 `dotnet run` 能打印"还没实现"。

> 第 8 条是整份计划的心理锚点：**在纯控制台里看到"打击 1D6=4，敌人 HP 20→16"的那一刻，说明架构方向是对的**；此时再谈美术才有意义。

---

## 17. 玩法钩子对技术的含义

看设计，这款游戏的"钩子"是：

> **大富翁的运气外壳 + 卡槽构筑内核**，用"骰子"把两者缝在一起（战斗伤害是 1D6，移动也是骰子，卡牌还能改骰）。

这个定位对技术上意味着：

1. **骰子必须"可视化"**：每次投骰要有明确的骰子动画和点数展示，这是玩家能理解"构筑为什么有效"的唯一窗口。
2. **卡牌必须能作用于骰子**：这是本作最独特的空间，建议专门做一类"骰子修正卡"（重投、+1、转向、双倍、锁定点数），它同时服务移动和战斗，是设计上的高杠杆点。
3. **单局时长必须可控**：同类游戏（大富翁类）死于"3 小时一局"。建议目标 **25～40 分钟/局**，用 BalanceSim 当作硬指标盯住。

---

## 附录 A：卡牌数据表模板（CSV）

> 下面几行只演示**字段怎么填**，不是真实卡表。真实 32 张的数值以 `战斗与卡牌系统规格书.md` §9 与 `卡牌构筑规则书.md` 第 11 章为准。

| id | name_zh | cost | cd_sec | category | scope | trigger | op1 | amount1 | target1 | op2 | amount2 | sell_price | text_zh |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| strike | 打击 | 1 | 3 | Attack | BattleSlot | OnCast | DealDamage | 1D6 | Enemy | | | 2 | 造成 1D6 伤害 |
| defend | 防御 | 1 | 3 | Skill | BattleSlot | OnCast | GainShield | 1D6 | Self | | | 2 | 获得 1D6 护盾 |
| poison | 剧毒 | 2 | 3 | Skill | BattleSlot | OnCast | ApplyPoison | 1D4 | Enemy | | | 4 | 造成 1D4 中毒 |
| luck | 好运 | 1 | 0 | Ability | BattleSlot | Passive | GainDiceModifier | 2 | AdjacentRightAtk | | | 3 | 右侧相邻攻击牌 +2 |
| pasture | 草场 | 1 | 0 | Finance | Passive | OnActionEnd | GainShard | 1 | Self | | | 5 | 每次行动后 +1 碎片 |
| ticket | 车票 | 1 | 0 | Consumable | Sell | OnSell | TeleportToHome | 0 | Self | | | 1 | 出售时立即回 home |
| extort | 敲诈 | 1 | 0 | Trap | Sell | OnSell | PlaceTrap | 5 | SelfTile | | | 1 | 留下陷阱，他人踩到付 5 碎片 |

## 附录 B：核心测试清单（M2 结束前必须有）

| 测试 | 断言 |
|---|---|
| RNG 分布 | 1D6 × 100k，每面 16.67% ± 1% |
| 确定性 | 同 seed 同命令流 → 事件流 SHA256 相同 |
| 战斗收敛 | 任意合法卡组对战 10k 局全部在 60s 内结束（硬平局兜底） |
| 超时惩罚 | 纯盾 vs 纯输出：20s 后惩罚被护盾吸收，由有效生命（生命+护盾）决定谁先倒下，约 27~30s 分出胜负；两个同血池纯盾 build → 60s 平局 |
| 平局 | 构造双方同 tick 归零 / 拖满 3600 tick → 均判平局且双方资源不变 |
| CD 正确性 | 3s 卡在 180/360/540 tick 释放 |
| 相邻修正 | 好运右侧是打击 → 伤害 = 投骰 + 2；左侧是打击 → 无修正 |
| 槽位溢出 | 2c 卡放在第 10 格必须被拒绝 |
| 移动边界 | 图上前进 200 步永不 null/越界 |
| 存档往返 | Save → Load → 状态哈希一致 |
| 断线重连 | 任意时刻快照恢复后 20 步内与主机哈希一致 |
| 升级 | 碎片/胜场双轨都能升到 6 级并触发胜利 |
| 血量作用域 | `PlayerState` 里不存在 HP 字段；战斗结束返回的结果对象里也不含护盾 |
