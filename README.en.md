# ChillFocused

[简体中文](README.md) | [English](README.en.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)]((LICENSE)
[![Platform: Linux / Proton](https://img.shields.io/badge/Platform-Linux%20%2F%20Proton-blue.svg)](#requirements)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.x%20(not%206.0)-green.svg)](https://github.com/BepInEx/BepInEx)

A BepInEx plugin for *[Chill with You : Lo-Fi Story](https://store.steampowered.com/app/3548580/)*:
**when the game's pomodoro or count-up timer runs, the apps on your blocklist are suspended
(frozen) -- and resumed exactly as they were when the timer stops.**

The game itself is untouched: nothing is redrawn, rescaled or patched visually. A suspended
process uses no CPU and can be resumed at any moment.

<img src="packaging/thunderstore/icon.png" alt="ChillFocused" width="128">

---

## What it solves

Focus software has to be able to focus.

- You list the apps that always pull you away (browser, chat, another game);
- when the game's work session starts, they are **suspended** (`SIGSTOP` / cgroup freezer --
  not closed);
- when the timer pauses, ends, or you hand out a pass, they **come back as they were**,
  unsaved work included;
- and if something does go wrong, one command resumes everything.

**Why freeze instead of minimizing or killing**: minimizing does not stop you from clicking it
back open, and killing loses whatever was unsaved. Suspending is the only option that is
immediately unusable *and* lossless -- and it is reversible, because every failure path
(plugin crash, backend killed, machine reboot) resumes instead of leaving things stuck.

## Demo

> TODO(author): add two screenshots or a 10-second GIF: (1) the in-game HUD showing
> "active · work session / 3 frozen"; (2) the F7 panel's app list. Put them under
> `docs/images/` and replace this section with the image references.

## Features

| Feature | Notes |
|---|---|
| Follows the game's timer | Freezes during a pomodoro **work** phase, resumes during **breaks**; same for count-up |
| Process matching | By process name (`*`, `?` supported) or by a **launch-argument substring**; pick from the running processes in the panel |
| Protect list | 450+ built-in rails (compositor, terminals, IME, wine, the game itself...), additions only |
| Three UI languages | Follows the game's setting: 简体中文 / English / 日本語 (every string overridable) |
| Panel + HUD | `F9` cycles three HUD modes, `F7` opens the settings panel, `F8` scans immediately |
| Layout you can edit | The panel layout is a plain text file; save it and it applies within a second, **no game restart** |
| Rehearsal mode | "Record only, freeze nothing" -- see who would be hit before arming it |
| Crash safety | If the plugin stops sending heartbeats, the backend's lease expires and resumes everything; `--thaw-all` covers a killed backend |

## Requirements

| Platform | Status |
|---|---|
| **Linux + Steam / Proton** | ✅ **Supported** -- this is what it was written for; the host-side backend uses `/proc`, cgroup v2 and `SIGSTOP` |
| Windows | ⚠️ The plugin loads, but there is **no Windows backend**, so nothing happens |
| macOS | ❌ Untested |

Also required:

- **BepInEx 5.x** (`BepInEx_win_x64_5.4.23.5.zip`; **not 6.0** -- this plugin targets the 5.x API)
- The **Focused** backend installed on the host (see below)

## Installation

### 1. Install BepInEx

Download [BepInEx 5.4.23.5 (win x64)](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip)
and extract it into the **game root** (the folder holding `Chill With You.exe`), then start the
game once so it creates `BepInEx/`.

### 2. Drop in one DLL

Take `ChillFocused.dll` from [Releases]((releases) and put it in `BepInEx/plugins/`:

```
Chill with You Lo-Fi Story/
├── Chill With You.exe
├── winhttp.dll                     ← from BepInEx
└── BepInEx/
    ├── core/                       ← from BepInEx
    ├── config/
    │   └── com.chillfocused.plugin.cfg     ← written on first run
    └── plugins/
        └── ChillFocused.dll        ← this is the whole mod
```

> **⚠️ Never keep two `ChillFocused*.dll` files** -- not the pre-rename `ChillFocus.dll`, not a
> copy left in a `plugins/ChillFocused/` subfolder. BepInEx loads **all** of them, so the same
> feature runs twice: two overlays, two poll loops. The plugin scans for this at start-up and
> **warns in the log**.

### 3. The winhttp override (the step everyone misses)

UnityDoorstop needs `winhttp.dll` to take precedence over the built-in one, and Proton does not
do that by default. **Without it BepInEx never loads**: the game runs normally and the plugin
does nothing at all.

Persistent, one command:

```bash
protontricks -c 'wine reg add "HKCU\Software\Wine\DllOverrides" /v winhttp /d "native,builtin" /f' 3548580
```

Or via the GUI: `protontricks --gui` → *Chill with You : Lo-Fi Story* → Select default wineprefix →
Run winecfg → Libraries → add `winhttp` → set **Native then Builtin** → Apply.

Or per launch, as a Steam launch option:

```
WINEDLLOVERRIDES="winhttp.dll=n,b" %command%
```

### 4. Install the backend

The backend is what actually suspends anything:

```bash
# Arch
yay -S focused && systemctl --user enable --now focused
# other distributions
curl -fsSL <release-url>/install.sh | sh
```

### 5. Check it

```bash
focused-doctor        # or scripts/doctor-focused.sh
```

It walks the whole chain -- backend reachable, game directory, BepInEx, the winhttp override,
whether the DLL is installed (and how many copies), whether the log shows it loading, and whether
the plugin and backend agree on address/token -- and prints the exact command for every ✗.

After starting the game, `BepInEx/LogOutput.log` should contain:

```
[Info   :ChillFocused] ChillFocused 0.1.0 loaded from .../BepInEx/plugins/ChillFocused.dll; backend at http://127.0.0.1:8766.
```

## Hotkeys

| Key | Action |
|---|---|
| `F9` | Cycle the HUD: one line → detailed → off |
| `F7` | Settings panel (apps to freeze / protect list / behaviour / layout / diagnostics) |
| `F8` | Scan immediately |

## Uninstalling

1. Delete `BepInEx/plugins/ChillFocused.dll`;
2. delete `BepInEx/config/com.chillfocused.*.cfg` if you also want the lists and panel layout gone;
3. and if you no longer need the backend: `systemctl --user disable --now focused`.

Leaving BepInEx itself in place does not affect other mods.

## Configuration

`BepInEx/config/com.chillfocused.plugin.cfg` (generated on first run; the panel edits the same
entries):

| Key | Default | Meaning |
|---|---|---|
| `FocusedUrl` | `http://127.0.0.1:8766` | Backend address; must be loopback |
| `FocusedToken` | empty | Sent as `X-Focused-Token` when it matches the backend's `http.token` |
| `Enabled` | `true` | Master switch; off means nothing is ever frozen |
| `AlwaysOn` | `false` | `true` ignores the game's timer and stays active while the game runs |
| `DryRun` | `false` | Record only, freeze nothing |
| `TimerFreshSeconds` | `25` | How long the timer reading stays believable |
| `SessionLeaseSeconds` | `30` | Heartbeat lease; after it lapses the backend resumes everything |
| `ProcessNames` | empty | Names to freeze, `*`/`?` supported, `;` separated |
| `CmdlineSubstrings` | empty | Match by launch arguments |
| `ExtraProtectedNames` / `ExtraProtectedCmdlineSubstrings` | empty | Extra protection (additions only) |

Interface text and layout:

- `BepInEx/config/com.chillfocused.text.cfg` -- one key per string; leave a value empty to use the
  built-in wording (**which follows the game's language**), fill it in to pin your own.
- `BepInEx/config/com.chillfocused.panel.cfg` -- panel size, font sizes, colours, list height;
  applies within a second of saving.

> Neither file is part of the release: they are generated on first run, and deleting them returns
> the built-in defaults.

## Troubleshooting

**Nothing appears in game?** In order: ① is there a `ChillFocused` line in
`BepInEx/LogOutput.log`; ② did the plugin load at all; ③ did you do the winhttp step.
`focused-doctor` checks all three.

**The HUD says "Focused not connected"?** The backend is not running, or the address/token
disagree. `systemctl --user status focused`, then compare both sides' address and `http.token`.

**The HUD says "idle · starts with the game's timer"?** That is healthy: the game's timer has not
started yet.

**An app is still frozen?**

```bash
focusedd --frozen      # what is suspended
focusedd --thaw-all    # resume everything now
kill -USR1 $(pgrep -f focusedd)   # same thing, via signal
```

**The log says "another copy of this plugin is installed"?** Delete the extra DLL it lists --
BepInEx loads every DLL under `plugins/`, subdirectories included, so two copies run twice.

## Compatibility with other mods

- This plugin **does not touch game assets**: it reads the game's own timer services and reports
  state to a host-side backend over HTTP.
- So it barely overlaps with mods that change audio or assets (ChillPatcher), and it can coexist
  with mods that take over window behaviour (ChillClock minimizes windows). The two do not
  conflict, but you would be minimized *and* suspended; pick one.
- If you run several mods that read the game's timer, check each one's own toggles.

## License and third-party components

This plugin's source is MIT (see the repository's `LICENSE`).

Where the boundaries are:

| Component | License | Do we ship it? |
|---|---|---|
| [BepInEx](https://github.com/BepInEx/BepInEx) 5.x | LGPL-2.1 | ❌ No -- users install it themselves; the plugin references its assemblies at compile time (`Private=false`) and uses the installed copy at runtime |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) (`0Harmony.dll`) | MIT | ❌ No -- it arrives with BepInEx |
| The game's own assemblies (`Assembly-CSharp.dll`, UnityEngine) | the game's EULA | ❌ No -- referenced at compile time to hook the game; the release contains only this plugin's own DLL |
| Game assets (audio, models, textures, fonts) | the game's EULA | ❌ None at all |

> In short: MIT; use it, change it, redistribute it; keep the copyright notice and the license
> text, and accept that it comes with no warranty. This plugin contains no game files and does not
> modify game assets: it only reads the game's own timer and language services and reports
> "is this a work session" to a host-side backend over loopback HTTP.

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx) and HarmonyX -- the plugin platform
- [ChillClock](https://github.com/anyukari/ChillClock) -- this project **adopted two of its
  findings**: resolving the game's services from its own DI container
  (`ProjectLifetimeScope.Resolve<T>()`) rather than relying only on method-body patching, and
  following the game's `LanguageSupplier` for interface text. Its settings-page injection
  technique in turn credits [iGPU Savior](https://github.com/Small-tailqwq/iGPUSaviorMod).
- [awesome-chillwithyou](https://github.com/clsty/awesome-chillwithyou) -- the mod index for this game

> For learning and personal use; please support the game.
