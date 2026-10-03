# ChillFocused

[简体中文](README.md) | [English](README.en.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Linux / Proton](https://img.shields.io/badge/Platform-Linux%20%2F%20Proton-blue.svg)](#系统要求)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.x%20(not%206.0)-green.svg)](https://github.com/BepInEx/BepInEx)

一个用于《[放松时光：与你共享 Lo-Fi 故事](https://store.steampowered.com/app/3548580/)》的 BepInEx 插件：
**开启创作模式时冻结黑名单中的进程，创作模式结束后恢复进程。**

<img src="packaging/thunderstore/icon.png" alt="ChillFocused" width="128">

---

## 功能

| 功能 | 说明 |
|---|---|
| 跟随游戏计时 | 番茄钟的**专注阶段**冻结、**休息阶段自动恢复**；正计时同理 |
| 进程名单 | 按进程名（支持 `*` `?`）或**启动参数子串**匹配；面板里从运行中的进程点选 |
| 保护名单 | 内置 450+ 条护栏（合成器、终端、输入法、wine、游戏本体…），只增不减 |
| 三语言界面 | 跟随游戏语言：简体中文 / English / 日本語（文案可覆盖） |
| 面板 HUD | `F9` 切换三种形态（一行 / 详细 / 关闭），`F7` 设置面板，`F8` 立即扫描 |
| 可调界面 | 面板布局是纯文本文件，改完保存 1 秒生效，**不用重启游戏** |
| 演练模式 | 「只记录，不冻结」：先看清会命中谁再启用 |
| 崩溃兜底 | 插件停止心跳 → 后端租约到期 → 自动全部恢复；后端被杀也有 `--thaw-all` |

## 系统要求

| 平台 | 状态 |
|---|---|
| **Linux + Steam / Proton** | ✅ **支持**（本项目就是为它写的，宿主侧后端只用 Linux 的 `/proc`、cgroup v2、`SIGSTOP`） |
| Windows | ⚠️ 插件本身能加载，但**后端没有 Windows 实现**，因此不生效 |
| macOS | ❌ 未测试 |

还需要：

- **BepInEx 5.x**（`BepInEx_win_x64_5.4.23.5.zip`；**不要用 6.0**，本插件按 5.x 的 API 写）
- 宿主侧安装 **Focused** 后端（见下）

## 安装

### 1. 装 BepInEx

下载 [BepInEx 5.4.23.5 (win x64)](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip)，
解压到**游戏根目录**（就是有 `Chill With You.exe` 的那层），然后**先启动一次游戏**，
让它生成 `BepInEx/` 目录。

### 2. 装载 DLL

从 [Releases](https://github.com/CHARARA97/ChillFocused-Linux/releases) 下载 `ChillFocused.dll`，放进 `BepInEx/plugins/`：

```
Chill with You Lo-Fi Story/
├── Chill With You.exe
├── winhttp.dll                     ← 来自 BepInEx
└── BepInEx/
    ├── core/                       ← 来自 BepInEx
    ├── config/
    │   └── com.chillfocused.plugin.cfg     ← 首次运行自动生成
    └── plugins/
        └── ChillFocused.dll        ← 将dll装至此处
```

> **⚠️ 不要同时装载两个 `ChillFocused*.dll`**（包括改名前的 `ChillFocus.dll`、或
> `plugins/ChillFocused/` 子目录里的旧副本）。BepInEx 会把它们**都**加载，于是同一套功能跑两遍：
> 两个面板、两次轮询。插件启动时会自己扫描并**在日志里警告**重复安装。

### 3. 设定 winhttp

UnityDoorstop 要让 `winhttp.dll` 优先于内置版本，Proton 默认不这么做。
**不设这一步，BepInEx 根本不会加载**，游戏看起来一切正常，插件毫无反应。

Shell命令：

```bash
protontricks -c 'wine reg add "HKCU\Software\Wine\DllOverrides" /v winhttp /d "native,builtin" /f' 3548580
```

或者：`protontricks --gui` → 选《Chill with You : Lo-Fi Story》→ Select default wineprefix →
Run winecfg → Libraries → 新增 `winhttp` → 改成 **Native then Builtin** → Apply。

也可以只在启动时生效（Steam 启动选项）：

```
WINEDLLOVERRIDES="winhttp.dll=n,b" %command%
```

### 4. 后端安装

```bash
# Arch
yay -S focused && systemctl --user enable --now focused
# 其他发行版
curl -fsSL <release-url>/install.sh | sh
```

### 5. 自检

```bash
focused-doctor        # 或 scripts/doctor-focused.sh
```

它会逐项检查：后端、游戏目录、BepInEx、winhttp、DLL、日志里加载记录、插件与后端的地址/令牌是否一致

启动游戏后，`BepInEx/LogOutput.log` 里应出现：

```
[Info   :ChillFocused] ChillFocused 0.1.0 loaded from .../BepInEx/plugins/ChillFocused.dll; backend at http://127.0.0.1:8766.
```

## 热键

| 键 | 作用 |
|---|---|
| `F9` | 切换 HUD：单行 → 详细 → 关闭 |
| `F7` | 设置面板（要冻结的应用 / 保护名单 / 行为 / 布局 / 诊断） |
| `F8` | 立即扫描 |

## 卸载

1. 删除 `BepInEx/plugins/ChillFocused.dll`；
2. 插件配置 `BepInEx/config/com.chillfocused.*.cfg`（含名单与面板布局）；
3. 后端（Focused）：`systemctl --user disable --now focused`。

## 配置

`BepInEx/config/com.chillfocused.plugin.cfg`（首次运行生成）：

| 键 | 默认 | 说明 |
|---|---|---|
| `FocusedUrl` | `http://127.0.0.1:8766` | 后端地址，必须是回环地址 |
| `FocusedToken` | 空 | 与后端 `http.token` 一致时发送 `X-Focused-Token` |
| `Enabled` | `true` | 总开关，关掉则永不冻结 |
| `AlwaysOn` | `false` | `true` = 不看游戏计时器，游戏跑着就一直生效 |
| `DryRun` | `false` | 只记录，不冻结（先看会命中谁） |
| `TimerFreshSeconds` | `25` | 计时器读数多久算新鲜，超时视为这次创作结束 |
| `SessionLeaseSeconds` | `30` | 心跳租约；插件消失后后端等这么久就全部恢复 |
| `ProcessNames` | 空 | 要冻结的进程名，支持 `*` `?`，`;` 分隔 |
| `CmdlineSubstrings` | 空 | 按启动参数匹配 |
| `ExtraProtectedNames` / `ExtraProtectedCmdlineSubstrings` | 空 | 追加保护（只增不减） |

界面文案与布局：

- `BepInEx/config/com.chillfocused.text.cfg` —— 每条文案的键，留空 = 用内置文案（**跟随游戏语言**）；
  填上值 = 固定成你的措辞。
- `BepInEx/config/com.chillfocused.panel.cfg` —— 面板尺寸、字号、配色、列表高度等，保存后 1 秒生效。

> 这两个文件**不是**发布物：首次运行自动生成，删掉就回到内置默认值。

## 常见问题

**游戏里什么都没出现？** 按顺序看：① `BepInEx/LogOutput.log` 里有没有 `ChillFocused`；
② 有没有 runner 加载记录（`Loaded 1 plugin` 之类）；③ winhttp 那一步做了没有。`focused-doctor` 会把这三步查完。

**HUD 显示「未生效 · Focused 未连接」？** 后端没在跑，或端口/令牌不一致：
`systemctl --user status focused`，再对一下两边的地址与 `http.token`。

**HUD 显示「待机 · 开始游戏计时后自动生效」？** 一切正常，只是游戏计时还没开始。

**应用被冻住没收回来？**

```bash
focusedd --frozen      # 看是谁
focusedd --thaw-all    # 立刻全部恢复
kill -USR1 $(pgrep -f focusedd)   # 同样的效果，走信号
```

**日志里出现 "another copy of this plugin is installed"？**
按它列出的路径删掉多余的 DLL —— BepInEx 会加载 `plugins/` 下所有 DLL（含子目录），两份 = 跑两遍。

## 与其他 Mod 的关系

- 本插件**不改游戏资源**：只读游戏自己的计时服务，把状态用 HTTP 交给宿主侧后端。
- 因此与改音频/资源的 Mod（如 ChillPatcher）冲突面很小；与同样接管窗口行为的 Mod
  （如 ChillClock 会最小化窗口）**可能**同时生效 —— 两者不冲突，但你会同时被最小化 + 被挂起，按需二选一。
- 若你同时装了多个"读游戏计时"的 Mod，注意它们各自的开关注释见各自文档。

## 许可与第三方组件

本插件源码：MIT（见仓库根目录 `LICENSE`）。

关于它依赖/引用的东西，界限是这样的：

| 组件 | 许可 | 我们是否分发它 |
|---|---|---|
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.x | LGPL-2.1 | ❌ 不分发 —— 由用户自己安装；本插件在编译期引用它的程序集（`Private=false`），运行期用用户装好的那份 |
| [HarmonyX](https://github.com/BepInEx/HarmonyX)（`0Harmony.dll`） | MIT | ❌ 不分发 —— 随 BepInEx 一起安装 |
| 游戏自身的程序集（`Assembly-CSharp.dll` / UnityEngine） | 游戏 EULA | ❌ 不分发 —— 编译期引用，用于把插件接上游戏的钩子；Release 里只有本插件自己的 DLL |
| 游戏资源（音频、模型、贴图、字体） | 游戏 EULA | ❌ 一概不含 |

> 简单说：MIT，自由使用/修改/分发，保留版权声明与许可文本，风险自负。
> 本插件不含任何游戏文件，也不修改游戏资源；它只读游戏自己的计时与语言服务，
> 并把"现在算不算专注"通过回环 HTTP 交给宿主侧后端。

## 致谢

- [BepInEx](https://github.com/BepInEx/BepInEx) 与 HarmonyX —— 插件底座
- [ChillClock](https://github.com/anyukari/ChillClock) —— 本项目**借鉴了它的两条经验**：
  用游戏自己的 DI 容器解析服务（`ProjectLifetimeScope.Resolve<T>()`）而不是只靠方法体注入；
  以及跟随游戏语言供应商（`LanguageSupplier`）显示界面文案。
  它的设置页注入思路又引用自 [iGPU Savior](https://github.com/Small-tailqwq/iGPUSaviorMod)。
- [awesome-chillwithyou](https://github.com/clsty/awesome-chillwithyou) —— 本游戏 Mod 的索引

> 本插件仅供学习交流，请支持正版游戏。
