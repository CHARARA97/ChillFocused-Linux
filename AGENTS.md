# chillfocus — 游戏插件

《Chill with You: Lo-Fi Story》的 BepInEx 5 插件；只通过 loopback HTTP 与后端 `focused` 通信。

- 源码：[ChillFocused/AGENTS.md](ChillFocused/AGENTS.md)
- C# 测试：`ChillFocused.Tests/`
- 文档：[docs/AGENTS.md](docs/AGENTS.md)
- 脚本：[scripts/AGENTS.md](scripts/AGENTS.md)
- 打包：[packaging/AGENTS.md](packaging/AGENTS.md)

## 先看哪里

- 改 UI 注入/原生页面：[Core/UI/Unity/AGENTS.md](ChillFocused/Core/UI/Unity/AGENTS.md)
- 改同步/计时：[Core/Sync/AGENTS.md](ChillFocused/Core/Sync/AGENTS.md)、[Core/Session/AGENTS.md](ChillFocused/Core/Session/AGENTS.md)
- 改状态/轮询：[Core/Status/AGENTS.md](ChillFocused/Core/Status/AGENTS.md)
- 改运行时/配置：[Core/Runtime/AGENTS.md](ChillFocused/Core/Runtime/AGENTS.md)、[Core/Config/AGENTS.md](ChillFocused/Core/Config/AGENTS.md)

## UI 入口

当前只有原生 SettingUI 注入，不再有独立自绘面板、HUD、toast 或旧 IMGUI 设置页。

```text
F7 → 打开游戏 SettingUI
F8 → 直接进入 ChillFocus 注入页
```

实现路径：

```text
SettingsPageInjector
├─ ChillFocusTabHost
│  ├─ SettingUiReflection
│  ├─ NativePageCloner
│  ├─ NativeTabHook
│  └─ NativeInteractable
├─ MainPageView / ProcessPageView
│  ├─ AppRowView
│  ├─ NativeToggleRow
│  └─ NativeActionButton
├─ NativeRowFactory
├─ ChillFocusPageActions
├─ BlockedAppsModel
├─ ProcessListModel
├─ ProcessIconView / ProcessIconCache
└─ ChillFocusCopy
```

原则：主控制器只做页面编排；反射、克隆、按钮、图标、文案、数据分别下沉。

## 会话锁（可选功能）

`专注中隐藏计时按钮`：番茄钟工作/休息期间藏掉 暂停、跳过、停止；正向计时只藏暂停，
**保留停止**（那是退出该模式的唯一方式）。设计、取证与验证矩阵见
[docs/session-locks.md](docs/session-locks.md)。

```text
TimerControlPlan   Core/Domain      开关 + TimerState → 藏哪几个（纯逻辑，可单测）
TimerControlGuard  Core/UI/Unity    门面：查找 → 压 CanvasGroup → 0.25s 重放 → 还原
TimerControlNames  Core/UI/Unity    按钮对象名（游戏改版只改这里）
TimerButtonLookup  Core/UI/Unity    按名字找活动对象 + 快照缓存 + 失败诊断
```

判定只认“某个阶段真的在计时”（`Running && (Working || Resting)`）：番茄钟跑完后游戏
仍保留 `IsTimerRunning=true`，只看它会导致锁不释放、玩家无法开始下一轮。
开关当前是**本地配置**（`LockTimerControls`），会话中可被关掉，属自律而非防作弊。

## 同步所有权

- `HeadlessRunner` 是 rules 与 session heartbeat 的唯一发送者；`RuleSync` 只在这里使用。
- `FocusRunner` 只读 `/focus` 状态并驱动注入页；不发送 rules/heartbeat。
- `GameClock` 是计时器与 sync 线程共用的单调时钟。
- `TimerState.Mode` + `SessionGate` 区分 Pomodoro / Countup；未知模式 fail-closed。
- `FocusedUrl` 必须是 loopback；`X-Focused-Token` 是唯一鉴权头。

## 常用命令

```bash
cd chillfocus
scripts/dev.sh help
scripts/dev.sh install     # 构建 + 安装
scripts/dev.sh restart     # 重启游戏
scripts/dev.sh tab         # 打开 ChillFocus 注入页（F8）
scripts/dev.sh verify      # 打开/滚动/进程页/返回 + 截图
scripts/dev.sh cycle       # install + restart + verify
scripts/dev.sh logs        # 过滤 BepInEx 日志
```

后端契约：[`../focused/docs/focused-protocol.md`](../focused/docs/focused-protocol.md)。没有后端运行时，插件应显示未连接，不本地冻结。

## 原生 UI 细节

- `ChillFocus` 页是追加 Tab，不占用 Credits，不删除原版入口。
- 原版 Tab、页面、按钮状态由游戏 `InteractableUI` 控制；不要手动改 Base/MouseOver/Using Image。
- 当前页面：主页（状态 / 专注设置 / 已暂停应用）和进程页（返回/刷新/运行中应用）。
  「专注设置」下有三行开关：Focused 控制游戏计时、游戏计时器控制专注、专注中隐藏计时按钮。
- 暂停名单和“全部解冻”由后端负责；插件只发送规则/租约。
- 应用行宽度、按钮位置、颜色集中在 `NativePageLayout` / `NativeActionButton`。
