#!/usr/bin/env bash
#
# Install the ChillFocused mod into the game: BepInEx (if missing) plus one DLL.
#
# The release ships a single file -- BepInEx/plugins/ChillFocused.dll -- so this
# script's job is mostly to put that one file in the right place, to clean up the
# places it used to live, and to say what is left to do.
#
# This writes inside the Steam game directory, so it is explicit about every path
# it touches.  Run it with --dry-run first.
#
# Usage:
#   scripts/install-bepinex.sh [--game-dir DIR] [--dll PATH|URL] [--sha256 HEX]
#                              [--dry-run] [--no-doctor]
#
#   --dll PATH   install a downloaded ChillFocused.dll instead of the build output
#   --dll URL    download it first (use --sha256 to verify what arrives)
#
# Environment:
#   CHILLFOCUS_GAME_DIR   game directory, when it is not in a default Steam library

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"

GAME_DIR="${CHILLFOCUS_GAME_DIR:-}"
BEPINEX_ZIP="$ROOT/dist/BepInEx_win_x64_5.4.23.5.zip"
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip"
# The upstream release asset, pinned.  Verified before anything is extracted into
# the game directory, because that directory is the user's save-adjacent data.
BEPINEX_SHA256="82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4"
BUILT_DLL="$ROOT/ChillFocused/bin/Release/net472/ChillFocused.dll"
DEBUG_DLL="$ROOT/ChillFocused/bin/Debug/net472/ChillFocused.dll"
PLUGIN_DLL=""
PLUGIN_SHA256=""
DRY_RUN=0
RUN_DOCTOR=1

while [ "$#" -gt 0 ]; do
    case "$1" in
        --game-dir) GAME_DIR="$2"; shift 2 ;;
        --dll) PLUGIN_DLL="$2"; shift 2 ;;
        --sha256) PLUGIN_SHA256="$2"; shift 2 ;;
        --dry-run) DRY_RUN=1; shift ;;
        --no-doctor) RUN_DOCTOR=0; shift ;;
        -h|--help) sed -n '2,18p' "$0"; exit 0 ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
done

say()  { printf '[install] %s\n' "$*"; }
warn() { printf '[install] %s\n' "$*" >&2; }
run()  { if [ "$DRY_RUN" -eq 1 ]; then printf '  would run: %s\n' "$*"; else "$@"; fi; }

# --- 0. find the game -------------------------------------------------------
#
# The default library is the common case, but plenty of people keep Steam games on
# a second disk.  Read Steam's own library list instead of guessing one path.
find_game_dir() {
    local candidates=() library game vdf

    for vdf in \
        "$HOME/.local/share/Steam/steamapps/libraryfolders.vdf" \
        "$HOME/.steam/steam/steamapps/libraryfolders.vdf" \
        "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/libraryfolders.vdf"
    do
        [ -f "$vdf" ] || continue
        while IFS= read -r library; do
            [ -n "$library" ] || continue
            candidates+=("$library/steamapps/common/Chill with You Lo-Fi Story")
        done < <(sed -n 's/.*"path"[[:space:]]*"\([^"]*\)".*/\1/p' "$vdf")
    done

    candidates+=(
        "$HOME/.local/share/Steam/steamapps/common/Chill with You Lo-Fi Story"
        "$HOME/.steam/steam/steamapps/common/Chill with You Lo-Fi Story"
        "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Chill with You Lo-Fi Story"
    )

    for game in "${candidates[@]}"; do
        if [ -f "$game/Chill With You_Data/Managed/Assembly-CSharp.dll" ]; then
            printf '%s' "$game"
            return 0
        fi
    done

    return 1
}

if [ -z "$GAME_DIR" ]; then
    if ! GAME_DIR="$(find_game_dir)"; then
        warn "ERROR: could not find the game in any Steam library."
        warn "Pass --game-dir DIR, or set CHILLFOCUS_GAME_DIR."
        warn "Expected the folder containing 'Chill With You.exe'."
        exit 1
    fi
    say "found the game in a Steam library: $GAME_DIR"
fi

MANAGED="$GAME_DIR/Chill With You_Data/Managed"
if [ ! -f "$MANAGED/Assembly-CSharp.dll" ]; then
    warn "ERROR: $GAME_DIR does not look like Chill with You (missing Managed/Assembly-CSharp.dll)"
    warn "Pass --game-dir, or set CHILLFOCUS_GAME_DIR."
    exit 1
fi

PLUGINS="$GAME_DIR/BepInEx/plugins"
say "game directory : $GAME_DIR"
say "plugin folder  : $PLUGINS"

# --- 1. BepInEx ------------------------------------------------------------
if [ -f "$GAME_DIR/winhttp.dll" ] && [ -d "$GAME_DIR/BepInEx/core" ]; then
    say "BepInEx already present, leaving it alone"
else
    if [ ! -f "$BEPINEX_ZIP" ]; then
        say "downloading BepInEx 5.4.23.5..."
        run mkdir -p "$ROOT/dist"
        run curl -fsSL -o "$BEPINEX_ZIP" "$BEPINEX_URL"
    fi
    if [ "$DRY_RUN" -eq 0 ]; then
        actual_sha256="$(sha256sum "$BEPINEX_ZIP" | cut -d' ' -f1)"
        if [ "$actual_sha256" != "$BEPINEX_SHA256" ]; then
            warn "ERROR: checksum mismatch for $(basename "$BEPINEX_ZIP")"
            warn "  expected $BEPINEX_SHA256"
            warn "  actual   $actual_sha256"
            warn "Refusing to extract. Delete the file and run this script again."
            exit 1
        fi
        say "checksum ok: $(basename "$BEPINEX_ZIP")"
    fi
    say "extracting $(basename "$BEPINEX_ZIP")"
    if [ "$DRY_RUN" -eq 1 ]; then
        printf '  would unzip %s into %s\n' "$BEPINEX_ZIP" "$GAME_DIR"
    else
        python3 - "$BEPINEX_ZIP" "$GAME_DIR" <<'PY'
import sys, zipfile
zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])
PY
    fi
fi

# --- 2. the plugin: one DLL, no folder ------------------------------------
case "$PLUGIN_DLL" in
    http://*|https://*)
        target="$ROOT/dist/ChillFocused.dll"
        say "downloading $(basename "${PLUGIN_DLL%%\?*}")"
        run mkdir -p "$ROOT/dist"
        run curl -fsSL -o "$target" "$PLUGIN_DLL"
        PLUGIN_DLL="$target"
        ;;
esac

if [ -z "$PLUGIN_DLL" ]; then
    if [ -f "$BUILT_DLL" ]; then
        PLUGIN_DLL="$BUILT_DLL"
    elif [ -f "$DEBUG_DLL" ]; then
        PLUGIN_DLL="$DEBUG_DLL"
        say "note: using the Debug build; scripts/package-mod.sh makes a Release one"
    fi
fi

if [ -z "$PLUGIN_DLL" ] || [ ! -f "$PLUGIN_DLL" ]; then
    warn "ERROR: no ChillFocused.dll to install."
    warn "Either build it (scripts/build.sh), or pass one:"
    warn "  scripts/install-bepinex.sh --dll ~/Downloads/ChillFocused.dll"
    warn "  scripts/install-bepinex.sh --dll <release-url>/ChillFocused.dll --sha256 <hex>"
    exit 1
fi

if [ -n "$PLUGIN_SHA256" ] && [ "$DRY_RUN" -eq 0 ]; then
    actual="$(sha256sum "$PLUGIN_DLL" | cut -d' ' -f1)"
    if [ "$actual" != "$PLUGIN_SHA256" ]; then
        warn "ERROR: checksum mismatch for $(basename "$PLUGIN_DLL")"
        warn "  expected $PLUGIN_SHA256"
        warn "  actual   $actual"
        exit 1
    fi
    say "checksum ok: $(basename "$PLUGIN_DLL")"
fi

# Old layouts.  BepInEx loads every DLL under plugins/ (subdirectories included),
# so leaving one behind runs the whole mod twice with nothing in the log to say so.
STALE=(
    "$PLUGINS/ChillFocus"
    "$PLUGINS/ChillFocused"
    "$PLUGINS/ChillFocus.dll"
    "$PLUGINS/ChillFocused-old.dll"
)
for stale in "${STALE[@]}"; do
    if [ -e "$stale" ]; then
        say "removing the old location: $stale"
        run rm -rf "$stale"
    fi
done

run mkdir -p "$PLUGINS"
say "installing $(basename "$PLUGIN_DLL") -> $PLUGINS/ChillFocused.dll"
run cp -f "$PLUGIN_DLL" "$PLUGINS/ChillFocused.dll"
if [ "$DRY_RUN" -eq 0 ]; then
    say "sha256: $(sha256sum "$PLUGINS/ChillFocused.dll" | cut -d' ' -f1)"
fi

# --- 3. the Wine DLL override, which is the part everyone misses -----------
PREFIX="${CHILLFOCUS_PFX:-$HOME/.local/share/Steam/steamapps/compatdata/3548580/pfx}"

override_present() {
    [ -f "$PREFIX/user.reg" ] || return 1
    python3 - "$PREFIX/user.reg" <<'PY'
import re, sys
try:
    text = open(sys.argv[1], encoding="utf-8", errors="replace").read()
except OSError:
    sys.exit(1)
# Only the global section counts: a per-app override would not cover the game.
m = re.search(r'\[Software\\\\Wine\\\\DllOverrides\](.*?)(?=\n\[|\Z)', text, re.S)
sys.exit(0 if m and re.search(r'"winhttp"\s*=', m.group(1), re.I) else 1)
PY
}

if override_present; then
    say "winhttp DLL override already set in $PREFIX - nothing to do."
else
    say ""
    say "REMAINING STEP - BepInEx will not load without it."
    say ""
    say "UnityDoorstop needs winhttp.dll to be preferred over the built-in one, and"
    say "Proton does not do that by default.  Either option works:"
    say ""
    say "  Option A (persistent, recommended). One command, no GUI:"
    printf '      protontricks -c %s 3548580\n' "'wine reg add \"HKCU\\\\Software\\\\Wine\\\\DllOverrides\" /v winhttp /d \"native,builtin\" /f'"
    say ""
    say "    ...or through the GUI:"
    say "      protontricks --gui"
    say "      -> select 'Chill with You : Lo-Fi Story'"
    say "      -> Select default wineprefix -> Run winecfg"
    say "      -> Libraries -> New override for library: winhttp -> Add"
    say "      -> Edit it to 'Native then Builtin' -> Apply"
    say ""
    say "  Option B (per launch): set the Steam launch option to"
    printf '      WINEDLLOVERRIDES="winhttp.dll=n,b" %%command%%\n'
fi

# --- 4. the backend, which is what actually suspends anything --------------
say ""
say "The backend does the suspending.  If it is not installed yet:"
say "      the backend installer: https://github.com/OWNER/focused#installation"
say ""
say "Then start the game and look for 'ChillFocused' in"
say "      $GAME_DIR/BepInEx/LogOutput.log"

if [ "$RUN_DOCTOR" -eq 1 ] && [ "$DRY_RUN" -eq 0 ]; then
    doctor="$(command -v focused-doctor || true)"
    if [ -f "$doctor" ]; then
        say ""
        say "running the self-check..."
        say ""
        "$doctor" --game-dir "$GAME_DIR" || true
    fi
fi
