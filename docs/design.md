# ChillFocus 设计文档


> **状态说明（合并后）**：本文档写作时的宿主侧执行者是 `chillfocusd` 守护进程。
> 它已经**并入 Focused 应用**（`focused/`，`~/.local/bin/focusedd`，端口 8766），
> 那一层已经删除；下文的"守护进程"就是今天的 **Focused**。
> 当前架构见 [README](../README.md)，应用细节见 [focused.md](../../focused/docs/focused.md)。
> 为《Chill with You : Lo-Fi Story》(Steam AppID `3548580`) 添加**运行时应用黑名单**：
> 游戏处于专注模式时，检测到黑名单中的进程名/PID 就自动结束该进程。

---

## 1. 目标与非目标

### 目标
| # | 目标 | 可验收标准 |
|---|---|---|
| G1 | 在游戏运行时，按**进程名**（可含 glob）冻结目标应用 ¹ | 启动 Firefox 后 3 秒内被挂起 |
| G2 | 支持按**cmdline 子串**拦截（应对改名/包装脚本启动的应用） | `--profile distractor` 可被单独拦截 |
| G3 | 支持按**PID**精确冻结，且不受 PID 复用影响 | 目标退出后 PID 被复用也不会误伤（解冻前逐 PID 校验 starttime） |
| G4 | 专注模式可在游戏内开/关，且有可见反馈 | 热键切换 + HUD 显示冻结计数 |

> ¹ **动作在 2026-10-03 从「关闭」改为「冻结」**：判定的目标集合没变，变的是对命中目标做的事。
> 决策记录见 §12d。
| G5 | 绝不误杀游戏、Steam、Wine 基础设施、桌面会话、本 mod 自身 | 见 §7 安全模型与测试用例 |
| G6 | 每次动作可审计 | `audit.jsonl` 逐条记录，含 dry-run |
| G7 | 首次安装后**默认不杀任何东西** | `enabled = false` 出厂默认 |

### 非目标（本原型范围外）
- 不做「阻止启动」的主动防御（eBPF / fanotify / cgroup 冻结）；本原型是**反应式终止**。
- 不修改游戏存档、不注入游戏 UI 树（HUD 用 IMGUI 叠加层，零游戏内部依赖）。
- 不做跨用户/特权操作，全部在用户自己的进程范围内。
- 不做 Windows 原生支持（架构天然可移植，但本原型只验证 Linux/Proton）。

---

## 2. 硬约束：为什么必须是双组件

游戏是 **Unity 2022.3.62f2 / Mono 后端**的 Windows 程序，在 Arch 上经 **Proton** 运行。
BepInEx 5 插件（`ChillFocused.dll`）因此运行在 **Wine 进程世界**里。这带来一个无法绕过的约束：

> **Wine 的进程表只包含同一 prefix 内的 Wine 进程。**
> 插件里的 `Process.GetProcesses()` 看不到宿主机的 Firefox、Chromium、原生游戏；
> `Process.Kill()` 自然也无从下手。

从 Wine 内部反向调用宿主命令也不可靠：

| 路径 | 状态 | 依据 |
|---|---|---|
| `cmd /c` 执行 Linux 程序 | **未实现**（仍是功能请求） | Wine Bug 54100 |
| `start /unix ...` | **已损坏** | Wine Bug 56471 |
| 直接 `CreateProcess` 一个 ELF | 历史上被移除 | Wine Bug 34730 |

**推论（本设计的地基）**：真正「看得见并杀得掉」宿主进程的代码，必须跑在**宿主 Linux 侧**。
插件只能做「配置 + 状态 + 反馈」。

---

## 3. 总体架构

```
┌─ Proton prefix（游戏进程世界）──────────────┐        ┌─ 宿主 Linux ────────────────────────┐
│                                             │        │                                     │
│  Chill With You.exe                         │        │   chillfocusd  (Python 3, stdlib)   │
│   └─ BepInEx 5.4.23.5                        │        │    ├─ ProcFS   读 /proc             │
│       └─ ChillFocused.dll          127.0.0.1   │        │    ├─ RuleSet  判定（纯函数）        │
│           ├─ Config (BepInEx ini) ◄─────────►│ HTTP   │    ├─ Killer   SIGTERM → SIGKILL     │
│           ├─ FocusRunner (Unity)  :8765      │ JSON   │    ├─ Engine   扫描循环 + 限流        │
│           │   ├─ 热键 F9/F7                  │        │    ├─ Server   HTTP API               │
│           │   ├─ IMGUI HUD / Toast            │        │    └─ Audit    JSONL 审计            │
│           │   └─ 后台轮询线程                  │        │                                     │
│           └─ RuleSync / 探针                │        │   由 launch wrapper / systemd 拉起     │
└─────────────────────────────────────────────┘        └─────────────────────────────────────┘
                     ▲                                                        │
                     │  Z: → /   （文件兜底：config.json / audit.jsonl）        ▼
                     └────────────────────────────────────────────  SIGTERM/SIGKILL → 目标进程
```

### 为什么用 **TCP 回环** 而不是文件作为主通道
Wine 在 Linux 上直接复用宿主网络栈，`127.0.0.1` 对双方是同一个回环。
这让**双向**通信成为可能：插件不仅能下发规则，还能拉取「刚刚冻结了谁」的事件流来做 HUD 提示与统计。
文件通道（`Z:\` → `/`）保留为兜底与离线配置途径。

### 为什么守护进程独立于游戏生命周期
若守护进程是游戏的子进程，游戏崩溃/被强杀时可能留下孤儿，或反过来在游戏退出后继续杀进程。
`scripts/launch-chillfocus.sh` 用 `setsid` + 父进程看护让守护进程**随游戏退出而退出**；
`packaging/systemd/chillfocusd.service` 提供常驻形态给「不开游戏也想保持专注」的用户。

---

## 4. 组件与模块职责

### 4.1 宿主侧 `focused/`

| 模块 | 职责 | 是否含 I/O | 可单测 |
|---|---|---|---|
| `procfs.py` | 读 `/proc`：comm / cmdline / status(Uid) / stat(starttime) / exe；快照 | 是（`root` 可注入） | ✅ 合成 /proc 树 |
| `rules.py` | **纯函数**判定：`evaluate(proc, rules, ctx) -> Decision` | **否** | ✅ |
| `killer.py` | 两阶段终止；信号发送/时钟/睡眠全部依赖注入 | 是（可注入） | ✅ 假实现 |
| `events.py` | 环形事件缓冲 + 单调 `seq`，供插件增量拉取 | 否 | ✅ |
| `config.py` | JSON 配置加载/校验/保存；XDG 路径解析 | 是 | ✅ tmpdir |
| `audit.py` | JSONL 追加审计日志 | 是 | ✅ tmpdir |
| `cgroupfs.py` | **纯函数**：PID → cgroup → unit，以及"能不能冻、冻整单元还是只冻进程树"（H3/H4/H6） | **否** | ✅ |
| `freezer.py` | 冻结/解冻本体：`SIGSTOP` 进程树、cgroup freezer 整单元、落盘与孤儿解冻、PID 复用防护 | 是（信号/时钟/`systemctl` 全部可注入） | ✅ 假实现 + 真进程 |
| `focused.py` | 把执行权交给外部 Focused 应用的客户端（推规则/推状态/回读冻结） | 是（HTTP） | ✅ 桩服务器 |
| `urlfilter.py` | **纯函数**：URL 黑名单匹配（与浏览器扩展 JS 同语义） | **否** | ✅ |
| `engine.py` | 扫描循环、限流、统计、规则热替换、三条解冻规则、委派 | 是 | ✅ 端到端 |
| `server.py` | `ThreadingHTTPServer`，绑 `127.0.0.1`，JSON API | 是 | ✅ 真实端口 |
| `chillfocusd.py` | CLI 入口、信号处理、`--once`/`--dry-run`/`--status` | — | 手工 |

**设计原则**：`rules.py` 是唯一决定「谁该死」的地方，且是纯函数、无 I/O、无时间依赖 —— 所有安全性判定都收敛到这一个可穷举测试的模块。

### 4.2 游戏侧 `plugin/`

**一条结构性规则**：**没有 Unity 依赖的逻辑单独成一个文件，并整份编进测试工程**
（`ChillFocused.Tests` 直接编译这些 `.cs`，且不引用 `UnityEngine` / BepInEx）。
于是「纯逻辑里混进 Unity」不是评审问题而是**编译错误**，`TextFit` / `Toasts` / `HudState`
这一层因此全都有单测（当前 334 项 xUnit）。

| 分组 | 文件 | 职责 |
|---|---|---|
| 入口 | `Plugin.cs` | `BaseUnityPlugin`；`Config.Bind` 全部配置项；延后创建 overlay 对象 |
| 运行 | `Core/FocusRunner.cs` | `MonoBehaviour`：`Update()` 热键/轮询/推送/行缓存，`OnGUI()` 组装下面三个绘制部件 |
| | `Core/HeadlessRunner.cs` | 不依赖 Unity 的后台同步线程；每 30 秒无条件重发规则，作为重启兜底 |
| | `Core/RuleSync.cs` | 覆盖层何时必须重发规则（首次、规则变化、失败退避、**守护进程 pid 变了**） |
| 传输 | `Core/DaemonClient.cs` | 后台线程 + 结果队列，供 `Update()` 在主线程消费 |
| | `Core/HttpTransport.cs` | `TcpClient` 手写 HTTP/1.1（依赖比 `HttpClient` 小，已由 prefix 内的探针实证） |
| | `Core/DaemonProtocol.cs` | `[Serializable]` DTO（`JsonUtility` 友好：具名字段 + 并行基础类型数组） |
| 配置 | `Core/ConfigBridge.cs` | 面板对 BepInEx 配置项的读写（写即落盘，UI 与文件只有一条编辑路径） |
| | `Core/ConfigFile.cs` | 只在文件不存在时创建；`Insert` / `Missing` / `SectionName` 保留用户已有内容 |
| 纯逻辑 | `Core/RuleText.cs` | 解析 `a;b;c`、名字归一化、15 字符 comm 截断匹配 |
| | `Core/TextFit.cs` | 按宽度截断文本（测量函数用委托注入，测试里传假实现） |
| | `Core/OverlayText.cs` | HUD 行标记编解码（`!` 前缀 = 警告样式） |
| | `Core/Toasts.cs` | 消息队列：上限、过期、淡出透明度（时钟是参数） |
| | `Core/HudState.cs` | 状态文案与「最近冻结」拼接（文案查找委托注入） |
| | `Core/OverlayLayout.cs` | 面板角落定位 |
| | `Core/Hotkeys.cs` | 按键边沿、HUD 模式循环、模式提示 |
| | `Core/PanelStrings.cs` | 面板文案回落与 `{0}` 替换 |
| | `Core/AppFilter.cs` | picker 的过滤谓词 |
| | `Core/FocusSettings.cs` | 设置快照 + 规则 payload 映射 |
| | `Core/HudMode.cs`、`Core/TimerState.cs` | HUD 三档、探针状态快照 |
| 界面 | `Core/HudRenderer.cs` | overlay 的样式与绘制（`Derive` 兜住 `new GUIStyle(null)`） |
| | `Core/UiTheme.cs`、`Core/HudFont.cs` | 九宫格纹理主题、中文字体解析 |
| | `Core/SettingsPanel.cs` | 设置窗口的框、状态行、视图切换、拖动 |
| | `Core/PanelStyles.cs` | 窗口样式与自绘复选框（Unity 的 toggle 无法改样式） |
| | `Core/AppPicker.cs` | 屏蔽名单 + 运行中列表：拉取、过滤、模式、滚动 |
| | `Core/AdvancedPane.cs` | 高级页；回调经 `IAdvancedHooks` 按需读取 |
| | `Core/PanelSpec.cs`、`Core/PanelSpecFile.cs`、`Core/PanelText.cs` | 热重载的布局与文案（模板即唯一事实来源） |
| 探针 | `Core/GameTimerProbe.cs` | Harmony 读游戏计时器（权威，带 TTL） |
| | `Core/GameProbe.cs`、`Core/FrameHook.cs` | API 清点、不依赖 GameObject 的帧回调 |

**主线程纪律**：网络全部在后台线程；结果放进 `lock` 保护的队列，只在 `Update()`（主线程）里消费。
Unity API 绝不在后台线程调用。

**零游戏内部依赖**：插件不引用 `Assembly-CSharp.dll`。HUD 走 IMGUI，热键走 BepInEx 的
`KeyboardShortcut.IsDown()`，游戏状态经反射读取。这样游戏更新不会让 mod 失效
（反射探针见 §12b/§12c）。

---

## 5. 通信协议

基址 `http://127.0.0.1:8765`，全部 JSON，全部带 `"ok"` 字段。

| 方法 | 路径 | 请求 | 响应 |
|---|---|---|---|
| GET | `/api/v1/health` | — | `{ok, version, pid, uid}` |
| GET | `/api/v1/status` | — | `{ok, enabled, dry_run, rules{names,...}, stats{...}, next_seq, daemon{...}}` |
| GET | `/api/v1/events?since=N&limit=M` | — | `{ok, events:[{seq,ts,kind,pid,name,rule,detail}], next_seq}` |
| POST | `/api/v1/rules` | `{names[], cmdline_substrings[], pids[], protect_names[], protect_cmdline_substrings[]}` | `{ok, applied{...}}` |
| POST | `/api/v1/enabled` | `{enabled:bool}` | `{ok, enabled}` |
| POST | `/api/v1/dry-run` | `{dry_run:bool}` | `{ok, dry_run}` |
| POST | `/api/v1/scan` | — | `{ok, outcomes:[...]}` |
| POST | `/api/v1/reload` | — | `{ok}` 重新读配置文件 |
| POST | `/api/v1/shutdown` | — | `{ok}` 优雅退出 |

> **为什么 `/status` 里既有嵌套块又有平铺字段**：Unity 的 `JsonUtility` 在这个项目里
> **不绑嵌套对象**（也不绑自定义类数组），插件读 `game_gate.enabled` / `daemon.pid` 只会拿到
> `null`——设置面板的「始终生效」因此长期显示错误且点不动。所以守护进程把插件需要的值在
> **顶层再平铺一份**（`gate_enabled` / `gate_satisfied` / `daemon_pid` / `daemon_version` /
> `recent_names`），嵌套块保留给别的客户端与人类阅读。契约测试会拒绝插件 DTO 里出现任何
> 非基元字段（`test_the_status_dto_stays_flat`），这类 bug 不会再悄悄回来。

**安全**：
- 只绑定 `127.0.0.1`（不监听 `0.0.0.0`）。
- 可选共享令牌：配置里设了 `http.token` 就必须带 `X-Focused-Token` 头，用 `hmac.compare_digest` 比较。
- 请求体上限 64 KiB，`Content-Type` 必须是 `application/json`。
- 未知路径返回 404，错误一律返回 `{ok:false, error:"..."}`，不回显内部堆栈。

---

## 6. 判定优先级（fail-closed）

`rules.evaluate()` **严格按顺序**求值，第一个命中者胜出。保护规则**先于**黑名单规则：

| 序 | 条件 | 结果 | 理由 |
|---|---|---|---|
| 1 | `pid <= 1` | PROTECT | PID 1 / swapper 绝不可动 |
| 2 | 内核线程（`/proc/pid/cmdline` 为空） | PROTECT | 内核线程不可由用户信号终止 |
| 3 | `pid ∈ {self, ppid} ∪ hard_protect_pids` | PROTECT | 守护进程自身与父进程（常为游戏启动脚本） |
| 4 | `uid != self_uid` | PROTECT | 不是我们的进程，`kill` 也会 EPERM |
| 5 | 命中 `protect.names`（glob） | PROTECT | 用户/默认白名单 |
| 6 | 命中 `protect.cmdline_substrings` | PROTECT | 同上，覆盖改名场景 |
| 7 | `protect_own_process_group` 且 `pgid == self_pgid` | PROTECT | 同进程组通常是同一次启动的链条 |
| 8 | 命中 `blacklist.names` (glob) / `cmdline_substrings` / `pids`(带 guard) | **MATCH** | 唯一会杀人的分支 |
| 9 | 其余 | SKIP | — |

**为什么保护优先而不是黑名单优先**：黑名单是用户可编辑的、容易写错的；保护名单是兜底的。
写错成 `names = ["*"]` 时，正确行为是「什么都不杀」（保护优先），而不是「杀掉整台机器」。
这一条在 `tests/test_rules.py` 里有专门用例。

### 名字匹配的三个细节

1. **大小写不敏感**：`Firefox` / `firefox` 视为同一规则。
2. **同时匹配三个候选**：`/proc/pid/comm`（内核，≤15 字符）、`/proc/pid/exe` 的 basename、`argv[0]` 的 basename。任一命中即算命中。
3. **内核截断补偿**：内核把 `comm` 硬截断到 **15 字符**。
   规则 `chromium-browser`（16 字符）永远无法与 comm `chromium-browse` 用精确比较匹配上。
   因此：**当规则不含通配符、且候选值恰好是 15 字符时，额外接受「规则以候选值为前缀」**。
   这是 Linux 进程黑名单类工具最经典的误判来源，必须显式处理并测试。

---

## 7. 安全模型

### 7.1 硬保护（不可被配置覆盖）
- PID 1、内核线程、守护进程自身、其父进程、`uid` 不匹配者。
- 这五条写死在代码里，不读配置，无法被 ini/JSON 关闭。

### 7.2 默认保护名单（可增不可减的语义）
出厂默认包含：`wineserver` `wine` `wine64` `wine-preloader` `services.exe` `explorer.exe`
`plugplay.exe` `winedevice.exe` `conhost.exe` `steam` `steamwebhelper` `steamlaunch`
`pressure-vessel` `reaper` `pv-bwrap` `gamescope` `mangohud` `gamemoderun`
`systemd` `systemd-logind` `dbus-daemon` `dbus-broker` `pipewire` `pipewire-pulse` `wireplumber`
`pulseaudio` `Xwayland` `Xorg` `gnome-shell` `kwin_wayland` `kwin_x11` `plasmashell`
`plasma-desktop` `xfwm4` `mutter` `sway` `Hyprland` `fcitx5` `ibus-daemon`
`xdg-desktop-portal*` `polkit*` `ssh-agent` `gpg-agent` `systemd-journald` `systemd-udevd`

理由：杀掉 `wineserver` 会连带干掉游戏；杀掉合成器会让人无法操作桌面；
杀掉 `systemd --user` 会终止整个用户会话。

**配置校验**：如果用户黑名单里的名字会被保护名单遮蔽（两者有交集），启动时打 `WARNING`
列出这些「永远不会生效」的规则 —— 静默失效比报错更危险。

### 7.3 PID 复用防护
`/proc/pid/stat` 第 22 字段 `starttime`（自开机起的时钟滴答数）唯一标识「某个 PID 的这一次生命」。
- 用户配置 PID 规则时，插件/命令行记录当时的 `starttime` 作为 guard。
- 判定阶段比对 guard：不一致 → 视为**不同进程**，不匹配。
- 执行阶段**发送信号前再核对一次**（TOCTOU 窗口）：不一致 → 放弃，记 `guard-mismatch`。

### 7.4 绝不使用 `killpg`
守护进程与游戏常常同属一个进程组（由同一个 wrapper 脚本启动）。
`os.killpg()` 会把游戏一起杀掉。全代码库禁用 `killpg`/`kill(-pid)`，只对**单个 PID** 发信号。

### 7.5 两阶段终止
```
SIGTERM ──(grace_seconds, 默认 3s)──► 仍在 → SIGKILL ──(1s)──► 仍在 → 记 killed_stubborn
```
先 SIGTERM 是为了让浏览器/编辑器有机会提示保存。SIGKILL 只在超时后使用。

### 7.6 限流与自愈
- `max_kills_per_minute`（默认 30）：超过则暂停执行 60 秒并记 `rate-limited` 事件。
  防止「有守护进程不断重启目标应用」导致的杀-重启风暴。
- 同一进程不会被重复发信号：扫描期间用 `_in_flight` 集合去重。
- 引擎的 `scan_once()` 用非阻塞锁保护，绝不并发扫描。

### 7.7 出厂安全默认
| 项 | 默认 | 说明 |
|---|---|---|
| `enabled` | **false** | 装完什么都不杀，必须先显式开启 |
| `dry_run` | false | 但 `--once --dry-run` 可随时预演 |
| `blacklist.names` | `[]` | 出厂不预设任何击杀目标 |
| `http.host` | `127.0.0.1` | 不对外暴露 |

---

## 8. 配置模型

```jsonc
{
  "enabled": false,              // 总开关（出厂关）
  "dry_run": false,              // 只记录不执行
  "scan_interval": 1.0,          // 秒，下限 0.2
  "grace_seconds": 3.0,          // SIGTERM → SIGKILL 等待
  "max_kills_per_minute": 30,
  "protect_own_process_group": true,
  "log_level": "info",
  "audit_log": "~/.local/share/chillfocus/audit.jsonl",
  "http":  { "host": "127.0.0.1", "port": 8765, "token": "" },
  "blacklist": {
    "names": ["firefox", "chromium*", "discord"],
    "cmdline_substrings": [],
    "pids": [], "pid_guards": {}
  },
  "protect": {
    "names": ["<默认名单>"],
    "cmdline_substrings": []
  }
}
```

保护名单还有一份**独立文本文件**：`~/.config/chillfocus/protect_names.txt`（每行一个名字，
`#` 为注释，超过 15 字符按 comm 上限截断）。它与内置名单、与 JSON 里的 `protect.names`
**三者取并集，只增不减**；文件只在守护进程启动时读一次，所以改完要重启才生效。
这也是唯一一处「不需要编辑 JSON 就能放宽保护」的入口。

配置文件路径解析顺序：`--config` → `$CHILLFOCUS_CONFIG` → `$XDG_CONFIG_HOME/chillfocus/config.json`
→ `~/.config/chillfocus/config.json`。审计日志与运行数据在 `$XDG_DATA_HOME/chillfocus/`。

**双配置源**：插件用 BepInEx 的 ini（`BepInEx/config/com.chillfocused.plugin.cfg`）作为用户界面，
启动时通过 `POST /api/v1/rules` 推给守护进程；守护进程也支持独立 JSON 配置，用于「不开游戏」的纯宿主侧模式。
两条来源不叠加 —— **后写入者整体替换规则集**，避免用户搞不清哪条生效。

---

## 9. 故障模式与降级

| 故障 | 行为 |
|---|---|
| 守护进程未运行 | 插件 HUD 显示「chillfocusd 未连接」，游戏正常运行，不做任何冻结 |
| 插件未加载 | 守护进程按自己的 JSON 配置独立工作 |
| HTTP 超时/畸形响应 | 客户端 1 秒超时，丢弃本次，下一轮重试；绝不阻塞游戏主线程 |
| `/proc` 读取竞态（进程消失） | 每次读取独立 `try`，返回 `None` 并跳过，不中断整轮扫描 |
| 目标进程拒绝 SIGTERM | 超时后 SIGKILL；仍存活则记 `killed_stubborn` 并继续 |
| 目标进程属别人 | `PermissionError` → 记 `denied`，不重试 |
| 配置文件损坏 | 启动失败并打印具体字段错误；**绝不静默用默认值继续**（否则可能以为在保护实际没保护） |
| 杀进程风暴 | 限流触发，暂停 60 秒并记事件 |

---

## 10. 测试策略

| 层 | 手段 | 破坏性 |
|---|---|---|
| `rules` 判定 | 纯函数穷举：保护优先、glob、15 字符截断、PID guard、uid、pid≤1、内核线程 | 无 |
| `procfs` | 在 tmpdir 合成假的 `/proc` 树（含带空格/括号的 comm、NUL 分隔 cmdline、缺失文件） | 无 |
| `killer` | 注入假 `signal_sender`/假时钟/假 procfs，覆盖 terminated/killed/denied/gone/guard-mismatch/dry-run | 无 |
| `config` | tmpdir、非法值、遮蔽规则警告 | 无 |
| `server` | 真实绑定 `127.0.0.1:0` 临时端口，打全部端点 + 鉴权 + 超大 body | 无 |
| 插件纯逻辑 | 同一份源码编进 `ChillFocused.Tests`：规则文本、配置插入、文案回落、picker 过滤、按键边沿、状态文案、坐标换算、消息过期、重推策略 | 无 |
| 插件界面（IMGUI） | 无法单测，靠编译 + 截图确认；大文件搬家时用「基线快照 + 逐行/字面量比对」核对（见 `docs/lessons.md`） | 无 |
| 跨语言契约 | `test_protocol_contract.py` 解析 C# DTO 源码：每个字段都必须出现在真实响应里，且**每个响应 DTO** 只允许标量与基础类型数组（JsonUtility 能绑的形状；请求 DTO 不受此限） | 无 |
| 集成（不杀） | 启动真 `chillfocusd` 子进程打 HTTP：重启后规则是否重推、旧事件游标会不会压住新进程、`protect_names.txt` 是否真的保护 | 无 |
| 集成（真杀） | 用**含唯一随机标记的哑进程**验证 dry-run 不杀、真杀能杀、受保护进程存活 | **有**，用 `CHILLFOCUS_ALLOW_KILL_TESTS=1` 门控，默认跳过 |

**破坏性测试的门控是刻意的**：默认 `unittest discover` 永远不会终止任何进程；
只有显式设置环境变量才会跑真杀用例，且规则里只有随机标记，不可能命中真实应用。

---

## 11. 部署形态

| 形态 | 组成 | 适用 |
|---|---|---|
| A. 游戏内（默认） | BepInEx + 插件 + launch wrapper 拉起守护进程 | 只想玩游戏时专注 |
| B. 常驻 | systemd user unit + JSON 配置，不开游戏也生效 | 全天专注 |
| C. 纯宿主 | 只用 `chillfocusd --once/--dry-run` 或常驻，不装 BepInEx | 不想动游戏目录 |

---

## 12. 关键决策记录（ADR）

| # | 决策 | 备选 | 理由 |
|---|---|---|---|
| D1 | 双组件（宿主守护进程 + 游戏插件） | 单插件 | Wine 进程视图隔离，单插件物理上做不到（§2） |
| D2 | HTTP/回环为主通道 | 纯文件 / Unix socket | 需双向（事件回流做 HUD）；Wine 不能访问宿主 Unix socket；回环天然可用 |
| D3 | 保护优先于黑名单（fail-closed） | 黑名单优先 | 用户配置写错时行为必须是「不杀」而非「乱杀」 |
| D4 | 守护进程用 Python 3 标准库，零第三方依赖 | psutil / Rust / Go | 免 pip 安装、Arch 自带 python；`/proc` 直读足够且可控 |
| D5 | 默认 `enabled=false` | 装完即生效 | 一个能杀进程的工具，出厂必须是惰性的 |
| D6 | 插件零 `Assembly-CSharp` 依赖 | 反射游戏类型做 UI | 游戏更新不破坏 mod；IMGUI HUD 足够表达状态 |
| D7 | `HttpWebRequest` 而非 `HttpClient` | `HttpClient` / `UnityWebRequest` | Unity Mono 下 `HttpWebRequest` 最稳；且不依赖主线程 pump |
| D8 | `JsonUtility` 而非第三方 JSON | Newtonsoft | Unity 内置、无额外 DLL 分发；代价是 DTO 必须是扁平具名字段 |
| D9 | 禁用 `killpg` | `killpg` 一次收一整棵树 | 守护进程与游戏同组，会误杀游戏（§7.4） |
| D10 | 破坏性测试环境变量门控 | 始终运行 | 测试代码本身也不该有权随便杀进程 |

---

## 12b. 追加发现（实测，2026-10-02）

实现并实机验证后才拿到的结论，推翻了本设计文档在 §4.2 处的一个隐含前提。

**游戏会在 BepInEx 插件加载完成后销毁插件的 GameObject。** 实测日志：

```
[Info   :ChillFocus] ChillFocus 0.1.0 loaded. ...
[Message:   BepInEx] Chainloader startup complete
[Info   :ChillFocus] runner destroyed; ...
[Info   :ChillFocus] plugin OnDestroy: the game is destroying the plugin object (never ticked)
```

`Awake` 与 `OnDestroy` 会被派发，但 **`Update`、`OnGUI`、`Start`、协程、
`Application.onBeforeRender`、`OnApplicationFocus`，以及 Harmony 补丁到
`Canvas.SendWillRenderCanvases` 的每帧回调，全部一次都不触发**——对象在第一次 `Start` 之前就被销毁。

### 影响与对应决策

| 原计划 | 实际做法 |
|---|---|
| 插件用 MonoBehaviour 的 `Update` 轮询守护进程（§4.2） | **改为普通后台线程**（`Core/HeadlessRunner.cs`），完全不依赖 Unity |
| 用 `JsonUtility` 序列化规则 | **手写 JSON**（`RuleJson`），既避开跨线程问题，也让传输层可单测 |
| 用 `HttpWebRequest` 通信 | **`TcpClient` 手写 HTTP**（`HttpTransport`），依赖更小，且已由探针在 prefix 内实证 |
| 游戏内 HUD + 热键作为主要交互 | **降级为保留代码**；控制入口改为配置文件（按 mtime 显式重载，因为 BepInEx 自带的文件监听在 Proton 下也不触发）——⚠️ 这一行已被 §12c 推翻：延后创建的对象能活下来，面板与热键因此可用 |

### 为什么社区 mod 看起来「能用」

[awesome-chillwithyou](https://github.com/clsty/awesome-chillwithyou) 中几个主要 mod
（ChillPatcher、RealTimeWeatherMod、iGPUSaviorMod）的系统支持均标注为 **Linux（未知）**。
查其源码，`ChillEnvPlugin.Awake()` 用的是 `new GameObject(...)` + `HideFlags.HideAndDontSave` +
`DontDestroyOnLoad` + `AddComponent` —— 与本项目最初尝试的写法一致，因此同样会失效。
「未知」是准确的：它们从未在 Proton 下被验证过。

### 新增的验证手段

`scripts/probe-wine-network.sh`：把插件**自己的传输层源码**编进一个 net472 控制台程序，用
`protontricks` 在 prefix 内运行。它同时测原始 TCP 连接、`HttpWebRequest` 和 `HttpTransport`，
用于把「Wine 网络问题」与「客户端代码问题」分开。实测三者均返回 200。

---

## 12c. 追加发现（二次修正）

上一节「游戏销毁插件对象，因此面板不可能」的结论**下得太早**：那是从「收不到回调」推出的，
而每个未触发的回调都有各自独立的解释。实际情况是**时序**问题——在 chainloader 那个窗口里创建的
对象拿不到帧回调，而**延后创建的对象可以活下来**。

解法是一个不依赖 GameObject 的主线程入口：捕获 Unity 的 `SynchronizationContext`，
由后台线程每 2 秒投递一次「确保 overlay 对象存在」。Unity 每帧排空该队列，于是对象在游戏
完全启动之后才被创建，`Start`/`Update`/`OnGUI` 全部正常，面板与热键随之可用。

（注意：不能在回调内部重新投递自己——Unity 会一直排空队列，自重投会在同一帧内无限循环卡死游戏。
所以由后台线程按固定节奏投递。）

### 创作模式门控

需求是「只在游戏计时器运行时拦截」。Unity 侧读取运行时状态的路走不通（Harmony 目标未被调用），
于是改从**游戏自己的存档**读取——Easy Save 3 的文件是 GZip 压缩的明文 JSON，守护进程在宿主侧
就能解析，完全不依赖插件。判据与四种子情形的时区推理见 README。

这也修正了 §2 的一个前提：插件并非「只能做配置与反馈」，它现在能提供面板；但**判定与执行仍然
留在守护进程**，因为那部分必须在游戏未运行、插件被销毁时也能工作。

### 即时门控（第三次修正）

存档轮询的延迟是结构性的：游戏每 6~15 秒才写一次盘。要即时，就必须在游戏进程内读状态。

最初判断「插件无法读取游戏状态」，依据是多个 Harmony 补丁安装后从未触发；实际原因是**补丁目标选择错误**：
补丁挂在 `Bulbul.CurrentDateAndTimeUI.UpdateDateAndTime` 上，而这个方法根本没被调用。
把游戏自己的类型清单 dump 出来之后（`GameProbe.DumpApi`，从已验证会触发的主线程泵里执行），
`Bulbul.PomodoroService` / `CountupTimerService` / `TimerCoreService` 这些服务及其
`IsTimerRunning` / `IsCurrentWorking` / 显式的 Start/Pause/OnTimerEnd 全部现形。

结论：**不要猜方法名，先把 API 清点出来再选目标。** 那次清点（317 行）一次性解决了这个问题。

于是形成两级门控：插件推送（当帧，权威，带 TTL）优先，存档轮询兜底。
这样既拿到了即时性，又保住了「守护进程不依赖游戏或插件存活」这条原则。

---

## 12d. 决策：从「关闭」改为「冻结」（2026-10-03）

**背景**：关闭进程解决了"分心"，代价是丢掉上下文——浏览器的标签页、没保存的草稿、下载进度。
而用户真正想要的是"专注期间别打扰我，结束后还给我"。

**决策**：ChillFocus **只冻结**命中黑名单的应用，不再关闭它们。`freeze.enabled` 出厂 `true`，
界面上不再提供"关闭"这个选项（连措辞都改：界面/文案统一说「冻结」「恢复」）。

**理由**：

1. **可逆**：`SIGSTOP`/cgroup freezer 挂起应用，计时结束、关掉游戏、守护进程崩溃后都能恢复；
   关闭是不可逆的，且有丢数据的风险（原先的免责声明就是在说这件事）。
2. **护栏更多**：冻结只挂起 CPU 调度，进程、窗口、内存、句柄都还在，被冻结的应用不会被"杀死到一半"。
3. **风险明确且可兜底**：冻结唯一的失败模式是"忘了恢复"，而我们为此做了四层兜底——
   退出自动解冻、崩溃后 `ExecStopPost` 解冻、启动孤儿解冻、`--thaw-all`/`SIGUSR1`/面板一键恢复。
4. **粒度更好**：能整单元冻结（`systemctl --user freeze`），连应用事后 fork 出的子进程一起管住。

**代价与保留**：

- 冻结**不释放内存**，被冻结的应用仍占着内存/端口；极端情况下会被它自己的 watchdog 判死。
- 终止代码（`killer.py`）**保留但不使用**：它是"冻结关掉时"的调试路径，也有完整测试。
  `freeze.enabled=false` 意味着"既不解冻也不关闭"，启动时会记一条警告。
- 跨语言契约不变：`/status` 新增的仍是**扁平基础类型**字段（`frozen_count` / `frozen_names[i]` …）。

**边界**：浏览器的事归浏览器扩展——它有自己的黑名单（`extra_patterns`），也接受守护进程
`urls.patterns` 下发的名单，两者取并集；**游戏插件不参与浏览器黑名单**（KISS：插件只管游戏里的进程）。
接口见 `docs/focused-protocol.md`。

---

## 13. 已知局限
- **反应式**：进程仍会短暂启动并抢焦点，之后才被杀。真要「启动即阻止」需 cgroup/`fanotify`，见 §14 第 3 条。
- **`comm` 截断**已补偿，但若规则既含通配符又超过 15 字符，截断补偿不生效（通配符与截断语义无法同时精确推理）—— 此时建议改用 `cmdline_substrings`。
- **同名进程不可区分**：同名同 cmdline 的两个进程无法只杀其一，除非用 PID 规则。
- 守护进程被强杀时不会清理自己启动的定时器（无副作用，可接受）。
- Windows 侧未验证。

## 14. 后续路线
1. ~~接游戏番茄钟/自习计时~~ **已完成，但没有走这条路**：不引用 `Assembly-CSharp.dll`，改用反射探针
   （`GameTimerProbe`）+ 存档新鲜度双路判定，见 §12b/§12c。
2. 反向能力：把「被拦截次数」做成游戏内统计数据/成就。
3. ~~「冻结代替关闭」~~ **已完成**：专注期间挂起（`SIGSTOP` 进程树 / systemd cgroup freezer 整单元），
   退出创作模式、误关游戏、守护进程崩溃（`ExecStopPost`）都会自动解冻。设计与验收记录见
   [freeze-mode.md](../../focused/docs/freeze-mode.md)。
4. ~~把执行权交给外部应用~~ **已完成**：不仅定了协议，还实现了 Focused 应用本身
   （`focused/`：会话 + 查找 + 冻结 + 看板 + systemd unit）。`delegate.enabled` 打开后 ChillFocus
   只推规则与专注状态、回读对方冻结了什么，不再自己动手；协议见
   [protocol.md](protocol.md)，应用文档见 [focused.md](../../focused/docs/focused.md)。
5. ~~浏览器拦截~~ **已实现 Chrome/Vivaldi 扩展**（`focused-extension/`）：专注模式下按 URL 黑名单关闭标签页。
6. 相关的另一条路是用 `fanotify` 拦截可执行文件打开。
7. 更聪明的规则：按窗口标题/焦点进程拦截（需要 X11/Wayland 侧信息，X11 下可用 `_NET_ACTIVE_WINDOW`）。
8. Thunderstore 正式发布（`packaging/thunderstore/`）。
