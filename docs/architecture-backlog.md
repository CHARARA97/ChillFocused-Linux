# ChillFocus 架构待办

> 来源：2026-10-18 的架构审计。  
> 当前基线：`ChillFocused.Tests` 312 passed，net472 构建 0 errors / 0 warnings。  
> 本文只记录待办和验收标准；完成一项后在这里更新状态，不要只改聊天记录。

## 进度快照

- ✅ **S-01**：Core 已按 Domain/Sync/Status/Ports/UI/Runtime/Config/Support/Platform 分层。
- ✅ **S-02 / T-04**：测试工程改为纯目录 glob，Unity/BepInEx 目录显式排除。
- ✅ **D-01 / D-03**：F6 输出 `PluginDebugSnapshot`（连接/会话/冻结/规则 due/后端 pid/心跳/错误）。
- ✅ **C-01**：`HeadlessRunner` 注入 transport/probe/rules/reporter；`PluginHost` 注入 `IFocusRunner` 工厂。
- ✅ **S-04（部分）**：`RuntimeHost` 由 `RunnerHost` + `SyncHost` 组成；`Plugin` 拆出 `PanelFiles`/`PluginEnvironmentReport`。
- ✅ **N-01**：`PluginHost` 正名为 `RuntimeHost`，承认它是运行时组合而非完整生命周期根。
- ✅ **C-03**：构造参数聚合为 `RuntimeHostContext`。
- ✅ **C-02**：`FocusRunner` 依赖 `IFocusedEndpoint`，不再持有具体 `FocusedClient`。
- ✅ **C-04**：`OverlayHost` 依赖 `IOverlayPanel`，`SettingsPanel` 显式实现。
- ✅ **C-06**：`FocusStatus` 改为复制式不可变快照，`FocusStatusMapper` 返回新对象。
- ✅ **D-02**：关键日志统一为 `event=plugin.*`，入口日志统一 `[ChillFocused]` 前缀。
- ✅ **D-04**：新增 `PerfCounter`，记录 poll/sync cycle/draw 的次数与 last/max 耗时。
- ✅ **N-02/S-04（部分）**：拆出 `SettingsRefresher`、`HudModelFactory`、`StatusPresenter`、`StatusCoordinator`；`FocusRunner` 646→493；`FocusRunnerWiring` 按 Services/Panel/Hotkeys/Actions 分组。
- ✅ **C-05**：`ITimerSource`/`GameTimerSource`/`TimerStateReader`；真机验证 Work/Pause/Break/Complete/Countup Run/Pause 全部通过。
- ✅ **T-01（部分）**：Host runner 生命周期 + Headless 启停测试；完整 sync transport 集成归 T-02。
- ✅ **X-01..X-07**：见第 0 节。

## 优先级图例

| 标记 | 含义 |
|---|---|
| P1 | 优先做，直接影响可读性、可测性或扩展成本 |
| P2 | 结构性问题，值得排期 |
| P3 | 清理/优化，不阻塞开发 |
| S / M / L | 小 / 中 / 大工作量 |
| 风险 | 改动可能破坏游戏内行为或测试的程度 |

---

## 0. 已处理（不再重复排期）

| ID | 内容 | 状态 |
|---|---|---|
| X-01 | `RuleJson` 从 `HeadlessRunner.cs` 移到独立文件 | ✅ |
| X-02 | `FocusStatus` 与 `FocusStateResponse` 的映射移到 `FocusStatusMapper` | ✅ |
| X-03 | `Plugin.cs` 的过期注释、未使用 using 清理 | ✅ |
| X-04 | HUD/面板/toast 从 `FocusRunner` 移到 `OverlayHost` | ✅ |
| X-05 | 热键边沿处理移到 `HotkeyController` | ✅ |
| X-06 | 配置监听/动作/帧调度拆为 `ConfigWatcher` / `PluginActions` / `FrameDriver` | ✅ |
| X-07 | FrameHook/GameProbe 安装拆为 `GameHookInstaller`，主线程队列拆为 `MainThreadPump` | ✅ |

---

## 1. 结构 / 可读性

| ID | 问题 | 影响 | 优先级 | 工作量 | 风险 | 验收 |
|---|---|---|---|---|---|---|
| S-01 | `Core/` 平铺 60+ 文件，domain/application/UI/adapter 只能靠文件名猜 | 高 | P1 | M | 低 | 目录分为 `Domain/`、`Sync/`、`Status/`、`UI/`、`Runtime/`、`Ports/`；先只移动文件、不改命名空间；构建和 293 项测试通过 |
| S-02 | 测试工程逐个列出纯逻辑文件，新增文件容易漏测 | 中 | P1 | M | 中 | 纯逻辑目录按 glob 编译；Unity/BepInEx 文件显式排除；新增纯类不需要手工改 csproj；测试仍全绿 |
| S-03 | `public` / `internal` 不一致，部分类没有真实外部 API 需求 | 中 | P2 | S | 低 | 逐类审计可见性；默认 `internal`；需要跨程序集的才 `public`，并在注释说明原因 |
| S-04 | `FocusRunner` 607 行、`Plugin` 486 行、`OverlayHost` 284 行，仍有多个变化原因 | 中 | P2 | M | 中 | `FocusRunner` 只保留 Unity 生命周期 + 状态调度；`Plugin` 只保留入口/启动；`OverlayHost` 的面板/布局可独立 |
| S-05 | 拆分后类注释可能过期，容易误导读者 | 低 | P3 | S | 低 | 每个被移动/改职责的类，顶部注释只描述当前职责；不再出现“hotkeys, polling, HUD and toasts”这类旧描述 |
| S-06 | 全项目仍是 `ChillFocused.Core` 单命名空间，目录不能表达边界 | 低 | P3 | M | 中 | `Domain` 不引用 `UI`/`Runtime`；如改命名空间，先确认 BepInEx/测试无影响；否则保持单命名空间但用目录表达 |

---

## 2. 命名

| ID | 问题 | 影响 | 优先级 | 工作量 | 风险 | 验收 |
|---|---|---|---|---|---|---|
| N-01 | `PluginHost` 实际只负责 FocusRunner/HeadlessRunner 运行时对象，名字像完整组合根 | 中 | P1 | S | 低 | 二选一：改名 `RuntimeHost`，或把剩余生命周期并入成为真正的 `PluginHost` |
| N-02 | `FocusRunner` 已不再“run focus”，更像 overlay/status controller | 中 | P2 | S/M | 低 | 改名 `FocusOverlayController`，或继续拆 `StatusCoordinator` + `ConfigRefreshCoordinator` |
| N-03 | `HeadlessRunner` 现在更接近 sync loop 的宿主 | 低 | P3 | S | 低 | 如改名，优先 `SyncRunner`/`SyncLoopHost`；不改也不影响功能 |
| N-04 | 部分 `public` 类名暗示对外契约，但实际只在插件内使用 | 中 | P2 | S | 低 | 与 S-03 一起处理；命名/可见性同时审计 |

---

## 3. 耦合 / 扩展

| ID | 问题 | 影响 | 优先级 | 工作量 | 风险 | 验收 |
|---|---|---|---|---|---|---|
| C-01 | `HeadlessRunner` 内部直接 `new FocusStatusProbe/RulePublisher/SessionReporter`，替换/扩展必须改循环 | 高 | P1 | M | 中 | 构造函数接受三个组件（或工厂）；测试可用 fake 替换；加新同步通道不需要改 `HeadlessRunner` 主体 |
| C-02 | `FocusRunner` 仍直接依赖具体 `FocusedClient`（`Pump`、`SetClient`、`BaseUrl`） | 高 | P2 | M | 中 | 拆出 `IClientPump`/`IClientEndpoint`；UI 只依赖 `IOverlayActions` + 泵接口；便于 mock 生命周期 |
| C-03 | `PluginHost` 构造函数有 8 个委托参数，装配成本高 | 中 | P2 | M | 中 | 用 `PluginRuntimeContext`/builder 聚合；构造参数降到可读范围 |
| C-04 | `OverlayHost` 直接持有具体 `SettingsPanel`，新增面板/替换视图成本高 | 中 | P2 | M | 中 | 面板通过 `IOverlayPanel`/工厂接入；HUD/面板可独立测试或替换 |
| C-05 | `GameTimerProbe` 硬编码服务名/反射方法，新增计时模式需要改探针 | 高 | P2 | L/M | 高 | 抽 `GameTimerSource`/`ITimerSource`；模式查询集中；必须真机验证 Harmony/Proton |
| C-06 | `FocusStatus` 是单所有者可变对象，不是真正的不可变快照 | 中 | P2 | M | 中 | 改为复制式更新；HUD/面板拿到的快照不会被后续写改动 |
| C-07 | `Plugin` 仍包含启动细节（panel 路径、诊断、生命周期） | 中 | P2 | M | 中 | 启动细节移到 `PluginBootstrap`；`Plugin` 只创建/启动/销毁 host |
| C-08 | 新增同步字段/状态需要同时改 DTO、Mapper、FocusStatus、HudModel | 中 | P3 | S/M | 低 | 提供一份“新增字段检查表”或生成式映射；至少保证测试覆盖契约 |

---

## 4. 调试 / 可观测

| ID | 问题 | 影响 | 优先级 | 工作量 | 风险 | 验收 |
|---|---|---|---|---|---|---|
| D-01 | 没有统一的 `PluginDebugSnapshot`，排查要同时看 HUD、日志、后端状态 | 高 | P1 | M | 低 | 快照含 connected/version/pid/session/frozen/poll/event cursor/sync cycle/push/last error/main-thread counters；F6 或 Detailed HUD 可打印 |
| D-02 | 日志前缀不统一（`[sync]`、`[panel]`、`[api]`、裸文本） | 中 | P2 | S | 低 | 统一为 `event=plugin.*` 或至少统一 `[ChillFocused]` 前缀；关键失败带 reason |
| D-03 | “为什么没发规则/没冻/没心跳”不可视化 | 高 | P2 | M | 中 | 调试快照包含：ruleDue、backendPidChanged、lastRuleError、lastHeartbeatAt、timerMode/unknown、lastPollError |
| D-04 | 缺少性能/频率计数，难以判断是否轮询过密 | 中 | P3 | S/M | 低 | 记录 poll/cycle/push 次数与最近耗时；只在 Detailed HUD 或 debug snapshot 展示 |
| D-05 | IMGUI 绘制无 profiling marker | 低 | P3 | S/M | 低 | 在 `OnGUI`/`HudState.BuildLines`/`SettingsPanel.Draw` 附近加轻量 Stopwatch 统计，默认关闭或仅 debug |

---

## 5. 测试 / 生命周期

| ID | 问题 | 影响 | 优先级 | 工作量 | 风险 | 验收 |
|---|---|---|---|---|---|---|
| T-01 | `PluginHost`、`FocusRunner`、`OverlayHost` 不在测试工程，生命周期只靠人工验证 | 高 | P1 | M/L | 中 | 给 host 注入 fake runner/sync/pump；覆盖创建、场景销毁后重建、重复 Ensure、启动失败 |
| T-02 | 没有完整 sync 线程 start/stop/重启集成测试 | 中 | P2 | M | 中 | fake loopback server 覆盖：启动→规则→心跳→后端 pid 变化→停止；不与真实游戏耦合 |
| T-03 | 没有 UI 预览/截图测试 | 低 | P3 | L | 中 | 可选：HudModel 快照测试已覆盖文本；IMGUI 不强行做像素测试，至少在测试里覆盖 ViewModel |
| T-04 | `ChillFocused.Tests` 的文件列表由手工维护 | 中 | P1 | M | 中 | 与 S-02 合并：纯目录 glob，Unity 目录显式排除；新增纯类自动进测试 |

---

## 建议执行顺序

1. **S-01 + S-02 + T-04**：先整理目录和测试编译边界，收益最大、行为风险最低。
2. **D-01 + D-03**：统一调试快照，后续所有排查都受益。
3. **C-01 + T-01**：Headless 注入化和 host 生命周期测试。
4. **N-01 + C-03**：PluginHost 正名/上下文聚合。
5. **C-02 + C-04 + C-06**：FocusRunner 客户端端口、面板端口、不可变状态。
6. **C-05**：GameTimerProbe 适配器，必须留真机验证窗口。
7. **D-02/D-04/D-05、S-03/S-05/S-06、N-02/N-03/N-04**：清理和体验项。

---

## 完成定义

一项任务只有同时满足以下条件才可标记完成：

- 代码在 net472 下构建通过；
- `ChillFocused.Tests` 全绿；
- 新行为有对应测试，或明确记录“只能在真机验证”及验证结果；
- `AGENTS.md` / 相关文档同步；
- 不把新职责重新塞回 `Plugin.cs` 或 `FocusRunner.cs`。
