# ChillFocused

[简体中文](README.md) | [English](README.en.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Linux / Proton](https://img.shields.io/badge/Platform-Linux%20%2F%20Proton-blue.svg)](#系统要求)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.x%20(not%206.0)-green.svg)](https://github.com/BepInEx/BepInEx)

《[放松时光：与你共享 Lo-Fi 故事](https://store.steampowered.com/app/3548580/)》的 BepInEx 插件。
游戏创作模式运行期间，挂起（冻结）屏蔽名单中列出的进程；创作模式结束后恢复。

<img src="packaging/thunderstore/icon.png" alt="ChillFocused" width="128">

---

## 功能

| 功能 | 说明 |
|---|---|
| 跟随游戏计时 | 番茄钟在专注阶段冻结、休息阶段恢复；正向计时同理 |
| 进程匹配 | 按进程名（支持 `*`、`?` 通配符）或启动参数子串匹配；可在面板中从运行中的进程选择 |
| 保护名单 | 内置 446 条保护项（桌面合成器、终端、输入法、Wine、游戏本体等），仅可追加 |
| 界面语言 | 简体中文 / English / 日本語，默认跟随游戏的语言设置；每条文案均可覆盖 |
| 状态面板 | `F9` 循环切换三种形态（单行、详细、关闭），`F7` 打开设置面板，`F8` 立即扫描 |
| 可调布局 | 面板布局保存为纯文本文件，修改后 1 秒内生效，无需重启游戏 |
| 只记录，不冻结 | 仅记录将被命中的进程，不执行冻结（`DryRun`） |
| 故障恢复 | 插件停止心跳后，后端租约到期并恢复全部进程；后端被终止时可用 `--thaw-all` 恢复 |

## 系统要求

| 平台 | 状态 | 说明 |
|---|---|---|
| Linux + Steam / Proton | 支持 | 宿主侧后端依赖 Linux 的 `/proc`、cgroup v2 与 `SIGSTOP` |
| Windows | 不支持 | 插件可正常加载，但后端没有 Windows 实现，因此不会生效 |
| macOS | 未测试 | — |

依赖：

- BepInEx 5.x（`BepInEx_win_x64_5.4.23.5.zip`）。不支持 BepInEx 6.0：本插件按 5.x 的 API 编写。
- 宿主侧 Focused 后端，见「安装」第 4 步。

## 安装

### 1. 安装 BepInEx

下载 [BepInEx 5.4.23.5 (win x64)](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip)，
解压到游戏根目录（包含 `Chill With You.exe` 的目录），启动游戏一次以生成 `BepInEx/` 目录。

### 2. 安装插件

从 [Releases](https://github.com/CHARARA97/ChillFocused-Linux/releases) 下载 `ChillFocused.dll`，放入 `BepInEx/plugins/`：

```
Chill with You Lo-Fi Story/
├── Chill With You.exe
├── winhttp.dll                     ← 来自 BepInEx
└── BepInEx/
    ├── core/                       ← 来自 BepInEx
    ├── config/
    │   └── com.chillfocused.plugin.cfg     ← 首次运行自动生成
    └── plugins/
        └── ChillFocused.dll
```

**注意**：`BepInEx/plugins/` 下只应存在一个 `ChillFocused*.dll`；不要同时安装两个副本。
旧版本的 `ChillFocus.dll`，以及 `plugins/ChillFocused/` 子目录中的副本，都必须删除。
BepInEx 会加载该目录下的全部 DLL（包括子目录），重复安装会使同一功能执行两次。
插件启动时会检测该情况并在日志中记录警告。

### 3. 配置 winhttp 覆盖

UnityDoorstop 要求 `winhttp.dll` 优先于系统内置版本，Proton 默认不提供该覆盖。
缺少该覆盖时 BepInEx 不会加载：游戏可正常运行，但插件不会生效。

命令行配置：

```bash
protontricks -c 'wine reg add "HKCU\Software\Wine\DllOverrides" /v winhttp /d "native,builtin" /f' 3548580
```

图形界面配置：`protontricks --gui` → 选择《Chill with You : Lo-Fi Story》 →
Select default wineprefix → Run winecfg → Libraries → 新增 `winhttp` →
设为 **Native then Builtin** → Apply。

也可仅对单次启动生效（Steam 启动选项）：

```
WINEDLLOVERRIDES="winhttp.dll=n,b" %command%
```

### 4. 安装后端

```bash
curl -fsSL https://github.com/CHARARA97/Focused/releases/latest/download/install.sh | sh
systemctl --user enable --now focused
```

Arch Linux 也可以从后端仓库本地构建 AUR 包（AUR 目前关闭新用户注册，暂未提交）：
`git clone https://github.com/CHARARA97/Focused && cd Focused/packaging/aur/focused && updpkgsums && makepkg -si`。

### 5. 自检

```bash
focused-doctor        # 等价于 scripts/doctor-focused.sh
```

自检项目：后端可达性、游戏目录、BepInEx、winhttp 覆盖、插件 DLL 与副本数量、
日志中的加载记录、插件与后端的地址及令牌一致性。

游戏启动后，`BepInEx/LogOutput.log` 中应出现：

```
[Info   :ChillFocused] ChillFocused 0.1.0 loaded from .../BepInEx/plugins/ChillFocused.dll; backend at http://127.0.0.1:8766.
```

## 热键

| 按键 | 功能 |
|---|---|
| `F9` | 切换 HUD 形态：单行 → 详细 → 关闭 |
| `F7` | 打开设置面板（要屏蔽的应用、保护名单、行为、布局、诊断） |
| `F8` | 立即执行一次扫描 |

## 卸载

1. 删除 `BepInEx/plugins/ChillFocused.dll`。
2. 如需同时清除配置，删除 `BepInEx/config/com.chillfocused.*.cfg`。
3. 如不再需要后端：`systemctl --user disable --now focused`。

保留 BepInEx 本身不影响其他插件。

## 配置

配置文件为 `BepInEx/config/com.chillfocused.plugin.cfg`，首次运行时生成：

| 键 | 默认值 | 说明 |
|---|---|---|
| `FocusedUrl` | `http://127.0.0.1:8766` | 后端地址，必须为回环地址 |
| `FocusedToken` | 空 | 与后端 `http.token` 一致时，请求携带 `X-Focused-Token` |
| `Enabled` | `true` | 总开关，关闭后不执行任何冻结 |
| `AlwaysOn` | `false` | 为 `true` 时忽略游戏计时器，游戏运行期间持续生效 |
| `DryRun` | `false` | 只记录，不冻结 |
| `TimerFreshSeconds` | `25` | 计时器读数的有效期，超时视为本次创作已结束 |
| `SessionLeaseSeconds` | `30` | 心跳租约；插件停止心跳后，后端等待此时长并恢复全部进程 |
| `ProcessNames` | 空 | 屏蔽名单中的进程名，支持 `*`、`?`，以 `;` 分隔 |
| `CmdlineSubstrings` | 空 | 按启动参数匹配的子串 |
| `ExtraProtectedNames` / `ExtraProtectedCmdlineSubstrings` | 空 | 追加保护项（仅可追加） |

界面文案与布局：

- `BepInEx/config/com.chillfocused.text.cfg`：每条文案对应一个键。值为空时使用内置文案（跟随游戏语言）；
  填写后固定为指定措辞。
- `BepInEx/config/com.chillfocused.panel.cfg`：面板尺寸、字号、配色、列表高度等，保存后 1 秒内生效。

两个文件均非发布内容：首次运行自动生成，删除后恢复内置默认值。

## 故障排查

**游戏内无任何显示。** 依次检查：① `BepInEx/LogOutput.log` 中是否存在 `ChillFocused` 记录；
② 是否记录了 runner 加载（如 `Loaded 1 plugin`）；③ 是否已配置 winhttp 覆盖。
`focused-doctor` 会完成以上检查。

**HUD 显示「未生效 · Focused 未连接」。** 后端未运行，或地址、令牌不一致：
执行 `systemctl --user status focused`，并核对两侧的地址与 `http.token`。

**HUD 显示「待机 · 开始游戏计时后自动生效」。** 状态正常，游戏计时尚未开始。

**有进程未恢复。**

```bash
focusedd --frozen      # 列出当前被冻结的进程
focusedd --thaw-all    # 立即恢复全部进程
kill -USR1 $(pgrep -f focusedd)   # 等效操作，通过信号触发
```

**日志出现 `another copy of this plugin is installed`。** 按日志列出的路径删除多余 DLL：
BepInEx 会加载 `plugins/` 下所有 DLL（包括子目录）。

## 与其他 Mod 的关系

- 本插件不修改游戏资源：仅读取游戏自身的计时服务，并通过回环 HTTP 将状态提交给宿主侧后端。
- 与修改音频或资源的 Mod（如 ChillPatcher）冲突面很小。
- 与接管窗口行为的 Mod（如 ChillClock 会最小化窗口）可能同时生效：两者不冲突，
  但窗口会同时被最小化并挂起，建议只启用其中之一。
- 若同时安装多个读取游戏计时的 Mod，请查阅各自的开关说明。

## 术语对照

用户可见文本只使用右列的说法；左列为实现或历史用词，不再出现。

| 界面用词 | 对应实现 | 说明 |
|---|---|---|
| 屏蔽名单 / 屏蔽模式 | blacklist | 描述结果，不描述机制 |
| 保护名单 / 保护模式 | protect list | 保护项仅可追加 |
| 只记录，不冻结 | dry_run | 仅记录命中的进程，不挂起 |
| 已冻结 N 个 | frozen_count | 实际执行的操作是冻结 |
| 未生效 · 原因 | — | 未生效状态统一为一种表述 |
| 按启动参数匹配 | cmdline_substrings | — |
| 后台执行器 | Focused 后端进程 | 避免使用「守护进程」 |
| 创作模式 | 游戏的工作时段（番茄钟专注阶段 / 正向计时） | 与游戏内用词一致 |

以下用词已弃用，由 `tests/test_wording.py` 校验：`黑名单模式`、`白名单模式`、`黑名单`、`白名单`、
`演练模式`、`冻结模式`、`守护进程`、`命令行子串`。

## 许可与第三方组件

本插件源码采用 MIT 许可，见仓库根目录 `LICENSE`。

本插件不分发以下组件：

| 组件 | 许可 | 说明 |
|---|---|---|
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.x | LGPL-2.1 | 由用户自行安装；本插件在编译期引用其程序集（`Private=false`），运行期使用用户安装的副本 |
| [HarmonyX](https://github.com/BepInEx/HarmonyX)（`0Harmony.dll`） | MIT | 随 BepInEx 一同安装 |
| 游戏程序集（`Assembly-CSharp.dll`、UnityEngine） | 游戏 EULA | 仅在编译期引用，用于接入游戏接口；发行包中只包含本插件自身的 DLL |
| 游戏资源（音频、模型、贴图、字体） | 游戏 EULA | 不包含 |

本插件不包含任何游戏文件，不修改游戏资源，仅读取游戏自身的计时与语言服务，
并通过回环 HTTP 提交状态。

## 致谢

- [BepInEx](https://github.com/BepInEx/BepInEx) 与 HarmonyX：插件平台。
- [ChillClock](https://github.com/anyukari/ChillClock)：本项目采用其两项做法 ——
  通过游戏自身的 DI 容器解析服务（`ProjectLifetimeScope.Resolve<T>()`），而非仅依赖方法体注入；
  以及通过 `LanguageSupplier` 获取界面语言。其设置页注入方法参考
  [iGPU Savior](https://github.com/Small-tailqwq/iGPUSaviorMod)。
- [awesome-chillwithyou](https://github.com/clsty/awesome-chillwithyou)：本游戏 Mod 索引。

本插件仅供学习与交流使用，请支持正版游戏。
