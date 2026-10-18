# 会话锁：隐藏计时器控制 + 禁止关闭游戏

两个可选开关，都是"玩家不能自己中断计划"的自约束。本文只做设计与取证，不含实现。

```text
开关 A  LockTimerControls   隐藏番茄钟/正向计时的停止、暂停按钮
开关 B  BlockGameExit       会话期间不能关闭游戏
```

## 0. 共同的硬约束：两个世界

沿用 [design.md §2](design.md) 的结论，这里再补一次实测证据（本机真实进程树）：

```text
reaper SteamLaunch AppId=3548580
└─ srt-bwrap（Steam Linux Runtime 4 / pressure-vessel 沙箱）
   └─ proton waitforexitandrun（DW-Proton）
      └─ python3 proton
         └─ Chill With You.exe        ← 插件在这里，只能看见 wine 进程表
```

- 插件里 `Process.GetProcesses()` 只覆盖同一 prefix；宿主信号、Steam 的 Stop、systemd 全看不见。
- 插件里 `cmd /c` 跑 Linux 程序、`start /unix`、直接 `CreateProcess` 一个 ELF 都不可用（Wine Bug 54100 / 56471 / 34730）。
- **UI 与退出拦截必须做在 Wine 里（能拦消息、能 patch 托管方法）；进程存活只能由宿主看守。**

## 1. 功能 A：隐藏计时器停止/暂停按钮

### ChillClock 怎么做的

[`UI/FocusUiHider.cs`](https://github.com/anyukari/ChillClock/blob/main/UI/FocusUiHider.cs)：

| 位置 | 手法 |
|---|---|
| `:347 HideTimerPauseButtons()` | 按 **GameObject 精确名字**找：`PomodoroPlayOrPauseButton`、`CountupPlayOrPauseButton` |
| `:372 FindActiveByExactName` | 遍历 `Resources.FindObjectsOfTypeAll<Transform>()` 快照，只认 `activeInHierarchy` 的 |
| `:493 HideTarget` | 给对象加/取 `CanvasGroup`，**`alpha=0` + `interactable=false` + `blocksRaycasts=false`** |
| `:764 HiddenEntry` | 记下原始 alpha/interactable/blocksRaycasts，恢复时逐项还原 |
| `:18 ReapplyInterval = 0.25f` | **每 0.25s 重放一次**：游戏会在状态切换时把按钮重新打开 |
| `:215 HideStopAndSkip` | 另一档：`PomodoroResetButton`、`SkipButton`、`PomodoroNextButton`，并反射 `_resetButton`、`_skipPomodoroButton`、`_skipButtonObject` |
| `:57 Tick(...)` | 五个布尔变了就 `RestoreAll()` 重建；异常只记 warning，不影响游戏 |

三个值得照搬的判断：

1. **用 `CanvasGroup` 而不是 `SetActive(false)`**：节点还在，布局不塌，游戏的激活动作不会和我们抢状态；`blocksRaycasts=false` 才是真正的"点不到"。
2. **必须重放**：隐藏是"压住"而不是"删掉"，每次游戏改 UI 都要重压一遍。
3. **必须能还原**：记录原值，锁解除后逐项写回，不能留半截状态。

### 我们的设计

```text
Core/Domain/TimerControlPlan       纯函数：开关 + TimerState → 该藏哪几个
Core/UI/Unity/TimerControlGuard    门面：Tick(enabled, timer, log)，压 CanvasGroup / 重放 / 还原
Core/UI/Unity/TimerButtonLookup    按名字找活动对象（快照缓存 + 失败诊断）
Core/UI/Unity/TimerControlNames    对象名常量表（唯一的字符串来源）
```

- 找对象：先按精确名字，再回落到反射字段 `_pomodoroPlayOrPauseButtonImage` / `_countupPlayOrPauseButtonImage`（这两个符号已在本机 `Assembly-CSharp.dll` 里确认存在）。名字表一处定义，游戏改版只改这个文件。
- 重放节奏与 ChillClock 对齐（0.25s），但**只在锁生效期间跑**；锁解除立刻 `RestoreAll()`。
- 隐藏范围分两档：`pause`（暂停/继续）为必需，`reset/skip` 可选，由配置决定，避免第一版就动太多按钮。
- 失败即降级：找不到按钮只记一条日志，不阻断 Tick，也不假装成功。

### 实机验证（游戏 1.17.3，2026-10-19）

启动番茄钟后守卫打出的命中日志，这是三个按钮的真实对象名：

```text
timer-lock: hid pause=PomodoroPlayOrPauseButton
timer-lock: hid skip =PomodoroNextButton      ← ChillClock 用的 SkipButton 在本版本不存在
timer-lock: hid stop =PomodoroResetButton
```

| 场景 | 结果 |
|---|---|
| 番茄钟 创作中（锁开） | 三键全部隐藏 |
| 番茄钟 休息中（锁开） | 三键全部隐藏（`phase=Break` 且 `running=True`，锁不释放） |
| 番茄钟 休息中（锁关） | 三键立即恢复 ← 还原路径验证 |
| 正向计时 运行中（锁开） | 只藏暂停，**停止按钮保留**（唯一退出方式） |
| 计时结束（锁开） | 自动恢复 |

注意：守卫第一次尝试时按钮还没被游戏创建，会打一条 `no active object`；随后命中即静默，
所以**判断是否生效要看 `hid` 行**，不能只看有没有 `no active object`。

## 2. 功能 B：禁止关闭游戏

先把"关闭游戏"拆成具体路径，才能说清哪些拦得住。

| # | 玩家动作 | 实际路径 | 能拦吗 | 拦在哪 |
|---|---|---|---|---|
| 1 | 点窗口 X / Alt+F4 / 任务栏关闭 / 合成器关闭 | X11 `WM_DELETE_WINDOW` → Wine → 窗口的 `WM_CLOSE` / `WM_SYSCOMMAND+SC_CLOSE` | ✅ | Wine 里的窗口过程钩子 |
| 2 | 游戏内"退出游戏"菜单/图标 | 托管方法 `OnClickExitButton`、`OnClickButtonExitGameIcon`、`ExitGame0` | ✅ | Harmony prefix |
| 3 | 任何代码调 `Application.Quit()` | Unity 引擎调用 | ✅ | patch `Application.Quit` + `Application.wantsToQuit` 返回 false |
| 4 | Steam 客户端"退出" | Steam 退出会连带强杀游戏 | ⚠️ 只能**不让 Steam 退** | Wine 里收掉 Steam 托盘菜单/任务栏窗口（ChillClock 的做法） |
| 5 | Steam 库页面「停止」 | `reaper` 直接对进程树发信号 | ❌ | 拦不到，只能宿主侧补救 |
| 6 | `kill` / `pkill` / 系统监视器 / `winetop` | SIGTERM / SIGKILL | ❌（SIGKILL 无解） | 只能宿主侧补救 |
| 7 | 注销/重启/断电 | 会话结束 | ❌ | 不设计对抗 |

**结论：不存在"绝对禁止关闭"，只有"把可拦的都拦住 + 被强杀后能恢复"。** ChillClock 自己也是这个结论，它在
[`Core/SteamCloseGuard.cs`](https://github.com/anyukari/ChillClock/blob/main/Core/SteamCloseGuard.cs) 的注释里写得直白：
"Steam 一退出会把游戏进程强杀，那一步拦不住，所以只能不让它退"。

### 2.1 Wine 侧（进程内）

**窗口关闭**——照搬 ChillClock [`Core/CloseGuard.cs`](https://github.com/anyukari/ChillClock/blob/main/Core/CloseGuard.cs) 的形状：

```text
SetWindowLongPtr(hwnd, GWL_WNDPROC, 我们的委托)
  收到 WM_CLOSE            → 返回 0（否决）
  收到 WM_SYSCOMMAND+SC_CLOSE → 返回 0
  收到 WM_NC_DESTROY       → 清记录后原样转发
```

要点：

- 委托**创建后永不释放**（`SetWindowLongPtr` 存的是函数指针，窗口销毁顺序不可控）。
- 别人也可能挂钩同一窗口（小窗/画中画类 mod）：发现当前过程不是自己时**什么都别做**，保留记录继续转发，否则会把别人的链打断。
- 每 300ms 重新枚举可见窗口补钩（窗口可能在启动后才出现）。

**退出调用**——两条一起：

```text
Application.wantsToQuit += () => !locked;      // 取消退出（Unity 官方钩子）
Harmony prefix on Application.Quit             // 覆盖直接调用
Harmony prefix on 游戏自己的退出方法（按符号名扫描，找不到就跳过）
```

**可信放行（必须有）**：会话正常结束、Focused 让停、游戏自己走"结束通话"演出时，必须放行，否则会把游戏的收尾流程卡死。
ChillClock 的 [`Plugin.cs:839 ShouldBlockGameExit`](https://github.com/anyukari/ChillClock/blob/main/Plugin.cs) 就是这个思路：

```text
锁生效 = 总开关 && BlockGameExit && (专注中 || 番茄钟运行中) && 不是可信退出路径
```

我们还要再加一条**防困死**：Focused 不可达超过 N 秒 → 自动解除 B，避免后端崩了玩家永远退不出去。

### 2.2 宿主侧（进程外）

插件看不见宿主信号，所以只能由宿主侧看守。可做的三件事，按侵入性排序：

| 档 | 做法 | 能解决 | 代价 |
|---|---|---|---|
| 记录 | 会话期间轮询游戏进程/窗口；消失就 `POST` 一条 violation 给 Focused | 把"被强杀"变成可见事实，会话标记为中断 | 无 |
| 重开 | 检测到消失且锁仍有效 → `steam -applaunch 3548580` | 对玩家形成真实阻力 | 重开要 30s+，体验粗暴 |
| 抑制信号 | `ptrace` attach，`signal-delivery-stop` 时丢弃 SIGTERM/SIGINT/SIGHUP | 挡住 `kill`、Steam Stop 的第一阶段 | 复杂、与 Proton/反作弊不兼容、**SIGKILL 依然无解** |

[ptrace(2)](https://manpages.ubuntu.com/manpages/jammy/en/man2/ptrace.2.html) 明确：信号在交付前处于
signal-delivery-stop，可被 tracer 抑制——这是宿主侧唯一能"真的不让它死"的手段，但只对可捕获信号有效。
[Steam Reaper](https://github.com/sonic2kk/steamtinkerlaunch/wiki/Steam-Reaper) 是 Steam 的看护进程，Stop 由它转成对进程树的终止信号。

我的建议：**第一版只做"记录"，第二版再评估"重开"；`ptrace` 不进默认路径。**

### 2.3 两侧怎么协同

```text
        ┌─ Wine（插件）────────────────┐        ┌─ 宿主 Linux ──────────────┐
        │ WindowCloseGuard  拦 WM_CLOSE │        │ GameGuardWatcher          │
        │ QuitCallGuard     拦托管退出   │  锁状态 │  ├─ 轮询 GET /focus       │
        │ TimerControlGuard  藏按钮      │◄──────►│  ├─ 看进程/窗口是否还在   │
        └───────────────┬───────────────┘  同一份 │  └─ 违规 → POST violation │
                        │                   真相   └───────────────────────────┘
                        └──────────► Focused ◄──────────────┘
```

关键点：**两边都以 Focused 的会话状态为准**，不各存一份。宿主侧不读插件配置，只读 `GET /api/v1/focus`，
这与现有"暂停名单由后端负责"的划分一致；Focused 挂了就两边同时不锁（fail-open），符合
[AGENTS.md](../AGENTS.md) 的"没有后端运行时不要本地冻结"。

## 3. 架构落点

| 模块 | 层 | 职责 | Unity | 可单测 |
|---|---|---|---|---|
| `SessionLock` | `Core/Domain` | 两个锁的判定结果（`TimerControls` / `GameExit` + 原因） | 否 | ✅ |
| `SessionLockPolicy` | `Core/Domain` | 输入 `FocusStatus` + 本地 `TimerState` + 偏好 → `SessionLock`；含防困死与可信放行 | 否 | ✅ |
| `TimerControlPlan` | `Core/Domain` | 由开关 + `TimerState` 算出“该藏哪几个” | 否 | ✅ |
| `TimerControlNames` | `Core/UI/Unity` | 按钮对象名常量（游戏改版只改这里） | 否 | ✅（纯常量） |
| `TimerButtonLookup` | `Core/UI/Unity` | 按名字找活动对象、快照缓存、失败诊断 | 是 | ❌ 真机 |
| `TimerControlGuard` | `Core/UI/Unity` | 门面：`Tick(enabled, timer, log)` → 压 `CanvasGroup` / 0.25s 重放 / 还原 | 是 | ❌ 真机 |
| `WindowCloseGuard` | `Core/Platform/Unity` | `GWL_WNDPROC` 钩子与消息否决 | 是 | ❌ 真机 |
| `QuitCallGuard` | `Core/Platform/Unity` | Harmony：`Application.Quit`、游戏退出方法、`wantsToQuit` | 是 | ❌ 真机 |
| `ExitLockController` | `Core/Runtime` | 组合上面两者、维护放行窗口、供给 `FocusRunner.Tick` | 是 | ❌ 薄 |
| `GameGuardWatcher` | 宿主（`focused/` 或 `chillfocus/scripts/`） | 会话期看守进程/窗口、报违规、可选重开 | 否（Python） | ✅ 假 procfs |

UI 侧：主页「专注设置」下加两行 `NativeToggleRow`（文案进 `ChillFocusCopy`），复用现有行工厂，不新增控件类型。
**会话进行中这两行应显示为不可点**——锁一旦生效，玩家不能在游戏里把它关掉。

> **当前实现是本地开关**（`LockTimerControls`，BepInEx 配置）。会话进行中打开设置页把它关掉即可解除，
> 所以现阶段它是"自律工具"而不是"防作弊"。要做成防作弊需按 §4 的 B 方案把生效值交给 Focused 的会话状态。

## 4. 锁的来源（要先定的一件事）

| 方案 | 谁能改 | 防作弊 | 复杂度 |
|---|---|---|---|
| A 纯本地 BepInEx 配置 | 玩家随时 | ❌ 会话中关掉即失效 | 最低 |
| B 本地偏好 + 后端会话接管 | 会话前可改；会话中由 `GET /focus` 决定 | ✅ | 需要 `focused` 在会话里带两个布尔 |

推荐 B：开关是"下一次会话的偏好"，真正生效的值来自 Focused 的会话状态。协议改动很小——
`GET /api/v1/focus` 的响应里加 `"locks": {"timer_controls": true, "game_exit": true}`，与现有 `freeze` 语义并列；
协议 canonical 在 `focused/docs/focused-protocol.md`，改完同步 [protocol.md](protocol.md)。

## 5. 分阶段实施

| 阶段 | 内容 | 验收 |
|---|---|---|
| A1 | `TimerControlPlan` + 单测 | 未知模式/未运行/会话结束时判定正确 ✅ **已完成** |
| A2 | `TimerControlGuard` + `TimerControlNames` + `TimerButtonLookup` | 会话中按钮消失且点不到；结束时原样回来 ✅ **已完成**（番茄钟三键、休息段、正向计时只藏暂停，均已真机验证） |
| B1 | `WindowCloseGuard` + `QuitCallGuard`（含可信放行） | X、Alt+F4、游戏内退出都被拦；Focused 结束会话后能正常退 |
| B2 | `GameGuardWatcher` 只记录 | Steam「停止」后 Focused 收到 violation |
| C | 可选：reset/skip 档、重开、`ptrace` 抑制 | 逐项独立开关，默认关 |

## 6. 风险与开放问题

**风险**

1. 对象名随游戏版本漂移 → 找不到就 fail-open + 一条日志，不做假成功。
2. 窗口钩子与其他 mod 抢同一个窗口 → 按 ChillClock 的链式转发规则处理，不覆盖别人的过程。
3. 拦退出可能卡死游戏自己的收尾 → 必须有可信放行 + 后端失联自动解锁。
4. 玩家被彻底困住的风险 → 保留一个明确的逃逸方式（例如后端不可达 60s 自动解锁），并写进文档。
5. 游戏从 Steam 启动，重开必须走 `-applaunch`，不能用裸命令；否则存档/云同步行为不一致。

**待定**

1. 开关 A 的隐藏范围：只 `pause`，还是连 `reset`/`skip` 一起？
2. 被强杀后的行为：只记录、立刻重开、还是让玩家选？
3. 是否要连 Steam 客户端退出也拦（ChillClock 的 `SteamCloseGuard` 那套要处理托盘菜单 CEF 弹窗，成本不低）？
4. 宿主看守做成 `focused` 的一个子命令，还是 `chillfocus/scripts/` 的独立脚本？

## 7. 参考

- ChillClock：[`UI/FocusUiHider.cs`](https://github.com/anyukari/ChillClock/blob/main/UI/FocusUiHider.cs)、[`Core/CloseGuard.cs`](https://github.com/anyukari/ChillClock/blob/main/Core/CloseGuard.cs)、[`Core/SteamCloseGuard.cs`](https://github.com/anyukari/ChillClock/blob/main/Core/SteamCloseGuard.cs)、[`Plugin.cs`](https://github.com/anyukari/ChillClock/blob/main/Plugin.cs)
- Wine 开发者列表：X11 关闭请求如何变成 `WM_CLOSE`、能否否决 —— [wine-devel 讨论](https://marc.info/?l=wine-devel&m=111957739309614&w=3)
- ptrace 信号抑制：signal-delivery-stop —— [ptrace(2)](https://manpages.ubuntu.com/manpages/jammy/en/man2/ptrace.2.html)
- Steam 的看护进程与 Stop 语义 —— [Steam Reaper](https://github.com/sonic2kk/steamtinkerlaunch/wiki/Steam-Reaper)
- cgroup v2 冻结（宿主侧"暂停而非杀死"的备选）—— [cgroup-v2.rst](https://docs.kernel.org/admin-guide/cgroup-v2.html)
