# ChillFocused

[简体中文](README.md) | [English](README.en.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Linux / Proton](https://img.shields.io/badge/Platform-Linux%20%2F%20Proton-blue.svg)](#requirements)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.x%20(not%206.0)-green.svg)](https://github.com/BepInEx/BepInEx)

A BepInEx plugin for *[Chill with You : Lo-Fi Story](https://store.steampowered.com/app/3548580/)*.
It suspends the processes listed in the block list while the game's work session is running,
and resumes them when the session ends.

<img src="packaging/thunderstore/icon.png" alt="ChillFocused" width="128">

---

## Features

| Feature | Description |
|---|---|
| Follows the game timer | Frozen during a pomodoro work phase, resumed during a break; the same for count-up |
| Process matching | By process name (`*`, `?` wildcards) or by a launch-argument substring; selected from the running processes in the panel |
| Protect list | 446 built-in entries (compositor, terminals, input methods, Wine, the game itself); additions only |
| Interface language | 简体中文 / English / 日本語, following the game's language setting by default; every string can be overridden |
| Status panel | `F9` cycles three HUD modes (one line, detailed, off); `F7` opens the settings panel; `F8` scans immediately |
| Editable layout | The panel layout is a plain text file; changes apply within one second, without restarting the game |
| Record only, freeze nothing | Records the processes that would be matched, without suspending them (`DryRun`) |
| Recovery | When the plugin stops sending heartbeats the backend's lease expires and resumes every process; `--thaw-all` covers a terminated backend |

## Requirements

| Platform | Status | Notes |
|---|---|---|
| Linux + Steam / Proton | Supported | The host-side backend requires Linux `/proc`, cgroup v2 and `SIGSTOP` |
| Windows | Not supported | The plugin loads, but there is no Windows backend, so nothing is enforced |
| macOS | Untested | — |

Dependencies:

- BepInEx 5.x (`BepInEx_win_x64_5.4.23.5.zip`). BepInEx 6.0 is not supported: this plugin targets the 5.x API.
- The host-side Focused backend; see step 4 under Installation.

## Installation

### 1. Install BepInEx

Download [BepInEx 5.4.23.5 (win x64)](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip)
and extract it into the game root (the directory containing `Chill With You.exe`), then start the game
once so that the `BepInEx/` directory is created.

### 2. Install the plugin

Download `ChillFocused.dll` from [Releases](https://github.com/CHARARA97/ChillFocused-Linux/releases)
and place it in `BepInEx/plugins/`:

```
Chill with You Lo-Fi Story/
├── Chill With You.exe
├── winhttp.dll                     ← from BepInEx
└── BepInEx/
    ├── core/                       ← from BepInEx
    ├── config/
    │   └── com.chillfocused.plugin.cfg     ← created on first run
    └── plugins/
        └── ChillFocused.dll
```

**Note**: exactly one `ChillFocused*.dll` may be present under `BepInEx/plugins/`; do not install two
copies. The pre-rename `ChillFocus.dll`, and any copy inside a `plugins/ChillFocused/` subdirectory,
must be removed. BepInEx loads every DLL under that directory (subdirectories included), so a duplicate
makes the same feature run twice. The plugin checks for this at start-up and logs a warning.

### 3. Configure the winhttp override

UnityDoorstop requires `winhttp.dll` to take precedence over the built-in version, which Proton does not
do by default. Without the override BepInEx does not load: the game starts normally and the plugin has
no effect.

From the command line:

```bash
protontricks -c 'wine reg add "HKCU\Software\Wine\DllOverrides" /v winhttp /d "native,builtin" /f' 3548580
```

Graphical equivalent: `protontricks --gui` → select *Chill with You : Lo-Fi Story* →
Select default wineprefix → Run winecfg → Libraries → add `winhttp` →
set it to **Native then Builtin** → Apply.

For a single launch only (Steam launch options):

```
WINEDLLOVERRIDES="winhttp.dll=n,b" %command%
```

### 4. Install the backend

```bash
# Arch Linux
yay -S focused && systemctl --user enable --now focused
# other distributions
curl -fsSL <release-url>/install.sh | sh
```

### 5. Verify

```bash
focused-doctor        # equivalent to scripts/doctor-focused.sh
```

It checks: backend reachability, the game directory, BepInEx, the winhttp override, the plugin DLL and
the number of copies, the load record in the log, and the address and token on both sides.

After the game starts, `BepInEx/LogOutput.log` should contain:

```
[Info   :ChillFocused] ChillFocused 0.1.0 loaded from .../BepInEx/plugins/ChillFocused.dll; backend at http://127.0.0.1:8766.
```

## Hotkeys

| Key | Action |
|---|---|
| `F9` | Cycle the HUD: one line → detailed → off |
| `F7` | Open the settings panel (apps to block, protect list, behaviour, layout, diagnostics) |
| `F8` | Run one scan immediately |

## Uninstalling

1. Delete `BepInEx/plugins/ChillFocused.dll`.
2. To remove the configuration as well, delete `BepInEx/config/com.chillfocused.*.cfg`.
3. If the backend is no longer needed: `systemctl --user disable --now focused`.

BepInEx itself can stay installed; it does not affect other plugins.

## Configuration

The configuration file is `BepInEx/config/com.chillfocused.plugin.cfg`, created on first run:

| Key | Default | Description |
|---|---|---|
| `FocusedUrl` | `http://127.0.0.1:8766` | Backend address; must be a loopback address |
| `FocusedToken` | empty | Sent as `X-Focused-Token` when it matches the backend's `http.token` |
| `Enabled` | `true` | Master switch; when off, nothing is frozen |
| `AlwaysOn` | `false` | When `true`, the game timer is ignored and the plugin stays active while the game runs |
| `DryRun` | `false` | Record only, freeze nothing |
| `TimerFreshSeconds` | `25` | Validity of the timer reading; beyond it the session is considered over |
| `SessionLeaseSeconds` | `30` | Heartbeat lease; after the plugin stops sending heartbeats the backend waits this long, then resumes every process |
| `ProcessNames` | empty | Process names in the block list, `*` and `?` supported, separated by `;` |
| `CmdlineSubstrings` | empty | Substrings matched against launch arguments |
| `ExtraProtectedNames` / `ExtraProtectedCmdlineSubstrings` | empty | Additional protect entries (additions only) |

Interface text and layout:

- `BepInEx/config/com.chillfocused.text.cfg`: one key per string. An empty value uses the built-in
  wording (which follows the game's language); a value pins that string to the given text.
- `BepInEx/config/com.chillfocused.panel.cfg`: panel size, font sizes, colours, list height; changes
  apply within one second of saving.

Neither file is part of the release: both are created on first run, and deleting them restores the
built-in defaults.

## Troubleshooting

**Nothing appears in game.** Check, in order: ① whether `BepInEx/LogOutput.log` contains a
`ChillFocused` record; ② whether a runner load was recorded (for example `Loaded 1 plugin`);
③ whether the winhttp override is configured. `focused-doctor` performs all of these checks.

**The HUD shows "inactive · Focused not connected".** The backend is not running, or the address and
token do not match: run `systemctl --user status focused`, then compare the address and `http.token`
on both sides.

**The HUD shows "idle · starts with the game's timer".** This is the normal state; the game's timer
has not started yet.

**A process was not resumed.**

```bash
focusedd --frozen      # list the processes currently frozen
focusedd --thaw-all    # resume all of them immediately
kill -USR1 $(pgrep -f focusedd)   # equivalent, triggered by signal
```

**The log contains `another copy of this plugin is installed`.** Delete the extra DLLs at the paths
the log lists: BepInEx loads every DLL under `plugins/`, subdirectories included.

## Compatibility with other mods

- This plugin does not modify game assets: it only reads the game's own timer services and reports
  state to the host-side backend over loopback HTTP.
- Overlap with mods that change audio or assets (such as ChillPatcher) is minimal.
- Mods that take over window behaviour (such as ChillClock, which minimizes the window) may be active
  at the same time: the two do not conflict, but the window is then both minimized and suspended, so
  enabling only one of them is recommended.
- If several mods that read the game's timer are installed, consult each one's own switches.

## Vocabulary

User-visible text uses only the right-hand column; the left is implementation detail or history and no
longer appears.

| Interface wording | Implementation | Notes |
|---|---|---|
| block list / block mode | blacklist | Describes the result, not the mechanism |
| protect list / protect mode | protect list | Entries can only be added |
| record only, freeze nothing | dry_run | Records matches without suspending them |
| N frozen | frozen_count | Suspending is what actually happens |
| inactive · reason | — | One wording for every "not active" state |
| match by launch arguments | cmdline_substrings | — |
| the Focused backend | the host-side process | Avoids the word "daemon" |
| work session | the game's work period (pomodoro work phase, count-up) | Matches the words the game uses |

The following wording is retired and verified by `tests/test_wording.py`: `黑名单模式`, `白名单模式`,
`黑名单`, `白名单`, `演练模式`, `冻结模式`, `守护进程`, `命令行子串`, "rehearsal mode", "whitelist",
"daemon".

## License and third-party components

The plugin source is MIT licensed; see `LICENSE` in the repository root.

This plugin does not distribute the following components:

| Component | License | Notes |
|---|---|---|
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.x | LGPL-2.1 | Installed by the user; this plugin references its assemblies at compile time (`Private=false`) and uses the installed copy at run time |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) (`0Harmony.dll`) | MIT | Installed together with BepInEx |
| Game assemblies (`Assembly-CSharp.dll`, UnityEngine) | the game's EULA | Referenced at compile time only, to attach to the game's interfaces; the release contains this plugin's own DLL only |
| Game assets (audio, models, textures, fonts) | the game's EULA | Not included |

This plugin contains no game files and does not modify game assets. It only reads the game's own timer
and language services and reports state over loopback HTTP.

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx) and HarmonyX: the plugin platform.
- [ChillClock](https://github.com/anyukari/ChillClock): this project adopts two of its approaches --
  resolving the game's services through its own DI container (`ProjectLifetimeScope.Resolve<T>()`)
  instead of relying on method-body patching alone, and reading the interface language through
  `LanguageSupplier`. Its settings-page injection method in turn references
  [iGPU Savior](https://github.com/Small-tailqwq/iGPUSaviorMod).
- [awesome-chillwithyou](https://github.com/clsty/awesome-chillwithyou): the mod index for this game.

This plugin is provided for learning and personal use; please support the game.
