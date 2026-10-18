#!/usr/bin/env bash
#
# Self-check for an installed ChillFocused: says which link in the chain is
# broken and what to type to fix it.
#
# The mod fails quietly by nature.  If BepInEx never loads it, the game simply
# starts and nothing appears; if the backend is down, the overlay says "Focused
# 未连接" and nothing is suspended; if the two sides disagree about the port or the
# token, they look connected but nothing works.  This script walks the whole chain
# and reports the first thing that is actually wrong.
#
# Usage:
#   scripts/doctor-focused.sh [--game-dir DIR]
#
# Exit code: 0 when the chain looks healthy, 1 when something is broken.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GAME_DIR="${CHILLFOCUS_GAME_DIR:-}"
PREFIX="${CHILLFOCUS_PFX:-$HOME/.local/share/Steam/steamapps/compatdata/3548580/pfx}"
FOCUSED_CONFIG="${XDG_CONFIG_HOME:-$HOME/.config}/focused/config.json"
APP_ID=3548580

while [ "$#" -gt 0 ]; do
    case "$1" in
        --game-dir) GAME_DIR="$2"; shift 2 ;;
        -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
done

OK=0
WARN=0
BAD=0

ok()   { printf '  \033[32m✓\033[0m %s\n' "$1"; OK=$((OK + 1)); }
warn() { printf '  \033[33m!\033[0m %s\n' "$1"; WARN=$((WARN + 1)); }
bad()  { printf '  \033[31m✗\033[0m %s\n' "$1"; BAD=$((BAD + 1)); }
fix()  { printf '      %s\n' "$1"; }

# --- progress ---------------------------------------------------------------
#
# The check walks a fixed list of sections, so the bar counts sections: it opens at
# 0% and closes at 100%, one line per section.  A bar that rewrote itself on one
# line would be erased by the section's own output, and a bar is worthless in a log
# file -- the section name is on every line, so a redirected report stays readable.

TOTAL_STEPS=6
STEP=0
BAR_WIDTH=24

# fill <count> <char> -- repeated character, without tr: tr maps one byte per
# input character, so a multi-byte glyph such as █ would come out as mojibake.
fill() {
    printf '%*s' "$1" '' | sed "s/ /$2/g"
}

bar_line() {
    local percent="$1" label="$2" filled empty

    # A bar belongs on a terminal.  In a pipe or a log file it is noise, so the
    # plain label is printed instead and a saved report stays readable.
    if [ ! -t 1 ]; then
        printf '\n%s\n' "${label%"${label##*[! ]}"}"
        return
    fi

    filled=$(( percent * BAR_WIDTH / 100 ))
    empty=$(( BAR_WIDTH - filled ))
    printf '\n\033[36m[%s%s]\033[0m %3d%%  %s\n' \
        "$(fill "$filled" '█')" "$(fill "$empty" '░')" "$percent" "$label"
}

step() {
    bar_line $(( STEP * 100 / TOTAL_STEPS )) "$(printf '%-40s %d/%d' "$1" "$STEP" "$TOTAL_STEPS")"
    STEP=$(( STEP + 1 ))
}

# --- 1. the backend ---------------------------------------------------------
bar_line 0 "$(printf '%-40s %d/%d' "自检开始 / self-check" 0 "$TOTAL_STEPS")"
step "1. 后端 / backend"

FOCUSED_URL="http://127.0.0.1:8766"
BACKEND_JSON="$(curl -fsS -m 2 "$FOCUSED_URL/api/v1/focus" 2>/dev/null || true)"
if [ -n "$BACKEND_JSON" ]; then
    version="$(printf '%s' "$BACKEND_JSON" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("version","?"))' 2>/dev/null || echo '?')"
    ok "Focused is answering on $FOCUSED_URL (version $version)"
else
    bad "Focused is not answering on $FOCUSED_URL"
    fix "systemctl --user status focused"
    fix "systemctl --user enable --now focused"
fi

# --- 2. the game ------------------------------------------------------------
step "2. 游戏目录 / game directory"

if [ -z "$GAME_DIR" ]; then
    for vdf in \
        "$HOME/.local/share/Steam/steamapps/libraryfolders.vdf" \
        "$HOME/.steam/steam/steamapps/libraryfolders.vdf" \
        "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/libraryfolders.vdf"
    do
        [ -f "$vdf" ] || continue
        while IFS= read -r library; do
            [ -n "$library" ] || continue
            candidate="$library/steamapps/common/Chill with You Lo-Fi Story"
            if [ -f "$candidate/Chill With You_Data/Managed/Assembly-CSharp.dll" ]; then
                GAME_DIR="$candidate"
                break
            fi
        done < <(sed -n 's/.*"path"[[:space:]]*"\([^"]*\)".*/\1/p' "$vdf")
        [ -n "$GAME_DIR" ] && break
    done
fi
if [ -z "$GAME_DIR" ]; then
    GAME_DIR="$HOME/.local/share/Steam/steamapps/common/Chill with You Lo-Fi Story"
fi

if [ -f "$GAME_DIR/Chill With You_Data/Managed/Assembly-CSharp.dll" ]; then
    ok "game found: $GAME_DIR"
else
    bad "game not found (expected the folder with 'Chill With You.exe')"
    fix "scripts/doctor-focused.sh --game-dir '/path/to/Chill with You Lo-Fi Story'"
    GAME_DIR=""
fi

PLUGINS="$GAME_DIR/BepInEx/plugins"

# --- 3. BepInEx -------------------------------------------------------------
step "3. BepInEx"

if [ -n "$GAME_DIR" ] && [ -f "$GAME_DIR/winhttp.dll" ] && [ -d "$GAME_DIR/BepInEx/core" ]; then
    ok "BepInEx is installed in the game"
else
    bad "BepInEx is missing from the game"
    fix "scripts/install-bepinex.sh   (downloads BepInEx 5.4.23.5 and verifies its sha256)"
fi

# The override is what makes UnityDoorstop load at all: without it the game runs
# normally and BepInEx is simply never asked to do anything.
if [ -f "$PREFIX/user.reg" ]; then
    if python3 - "$PREFIX/user.reg" <<'PY'
import re, sys
text = open(sys.argv[1], encoding="utf-8", errors="replace").read()
m = re.search(r'\[Software\\\\Wine\\\\DllOverrides\](.*?)(?=\n\[|\Z)', text, re.S)
sys.exit(0 if m and re.search(r'"winhttp"\s*=', m.group(1), re.I) else 1)
PY
    then
        ok "the winhttp DLL override is set in $PREFIX"
    else
        bad "the winhttp DLL override is NOT set (BepInEx will never load)"
        fix "protontricks -c 'wine reg add \"HKCU\\Software\\Wine\\DllOverrides\" /v winhttp /d \"native,builtin\" /f' $APP_ID"
        fix "or set the Steam launch option: WINEDLLOVERRIDES=\"winhttp.dll=n,b\" %command%"
    fi
else
    warn "no Proton prefix at $PREFIX (game never started, or a different prefix)"
    fix "start the game once, then run this again"
fi

# The Steam launch option is the first link in the chain, and the most silent one:
# if it names a program that is no longer installed, Steam starts nothing, reports
# nothing, and the game simply never appears.
# ~/.steam/steam is usually a symlink to ~/.local/share/Steam, so the same file
# shows up twice: keep the canonical paths only.
launch_vdfs=()
for vdf in "$HOME/.steam/steam/userdata"/*/config/localconfig.vdf \
           "$HOME/.local/share/Steam/userdata"/*/config/localconfig.vdf; do
    [ -f "$vdf" ] || continue
    real="$(readlink -f "$vdf")"
    seen=0
    for known in "${launch_vdfs[@]:-}"; do
        [ "$known" = "$real" ] && seen=1
    done
    [ "$seen" -eq 1 ] || launch_vdfs+=("$real")
done

for vdf in "${launch_vdfs[@]:-}"; do
    [ -n "$vdf" ] && [ -f "$vdf" ] || continue
    launch_option="$(python3 - "$vdf" "$APP_ID" <<'PY'
import re, sys
text = open(sys.argv[1], encoding="utf-8", errors="replace").read()
app = sys.argv[2]
block = re.search(r'"%s"\s*\{(.*?)\n\t{4}\}' % app, text, re.S)
if not block:
    block = re.search(r'"%s"\s*\{(.{0,4000}?)\n\s*\}' % app, text, re.S)
if block:
    found = re.search(r'"LaunchOptions"\s*"([^"]*)"', block.group(1))
    print(found.group(1) if found else "")
PY
)"
    [ -n "$launch_option" ] || continue
    case "$launch_option" in
        %command%*) continue ;;
    esac
    launch_checked=1
    first_word="${launch_option%% *}"
    # The shell expands a leading ~, and Steam runs the option through /bin/sh.
    case "$first_word" in
        "~/"*) program="$HOME/${first_word#\~/}" ;;
        *)     program="$first_word" ;;
    esac
    if [ -x "$program" ]; then
        ok "Steam launch option runs $first_word"
    else
        bad "the Steam launch option names a program that is not installed: $first_word"
        fix "Steam -> this game -> Properties -> Launch Options: use /usr/bin/focused-launch %command%"
        fix "or remove the wrapper entirely: %command%"
    fi
done

# --- 4. the mod -------------------------------------------------------------
step "4. 插件 / the mod"

# Two copies mean the mod runs twice: BepInEx loads every DLL under plugins/,
# subdirectories included.
mapfile -t COPIES < <(find "$PLUGINS" -maxdepth 2 -iname 'ChillFocus*.dll' 2>/dev/null | sort)
if [ -n "$GAME_DIR" ] && [ -f "$PLUGINS/ChillFocused.dll" ]; then
    sha="$(sha256sum "$PLUGINS/ChillFocused.dll" | cut -d' ' -f1)"
    ok "installed: $PLUGINS/ChillFocused.dll"
    fix "sha256 $sha"
elif [ "${#COPIES[@]}" -gt 0 ]; then
    # Older layout.  It loads, so this is a tidiness problem with a real failure
    # mode: as soon as a second copy appears the mod runs twice.
    warn "installed in the older layout: ${COPIES[0]}"
    fix "scripts/install-bepinex.sh   (moves it to $PLUGINS/ChillFocused.dll and cleans up)"
else
    bad "ChillFocused.dll is not installed"
    fix "scripts/install-bepinex.sh"
fi

if [ "${#COPIES[@]}" -gt 1 ]; then
    bad "more than one copy of the mod is installed (it would run twice)"
    for copy in "${COPIES[@]}"; do
        fix "remove: $copy"
    done
elif [ "${#COPIES[@]}" -eq 1 ] && [ -f "$PLUGINS/ChillFocused.dll" ]; then
    ok "exactly one copy of the mod is installed"
elif [ "${#COPIES[@]}" -eq 1 ]; then
    warn "one copy, in the older layout (see above)"
fi

# --- 5. the log -------------------------------------------------------------
step "5. 日志 / the log file"

LOG="$GAME_DIR/BepInEx/LogOutput.log"
if [ -n "$GAME_DIR" ] && [ -f "$LOG" ]; then
    if grep -q 'ChillFocused' "$LOG"; then
        line="$(grep -m1 'ChillFocused .* loaded' "$LOG" | cut -c1-150)"
        if [ -n "$line" ]; then
            ok "the mod loaded: $line"
        else
            ok "the log mentions ChillFocused"
        fi
    else
        bad "the log has no ChillFocused entry: BepInEx is loading, the mod is not"
        fix "check the two 'another copy' / winhttp items above"
        fix "tail -50 '$LOG'"
    fi
    if grep -q 'another copy of this plugin is installed' "$LOG"; then
        warn "the log warns about a duplicate install (see the remove: lines above)"
        fix "grep -A3 'another copy' '$LOG'"
    fi
else
    warn "no log yet at $LOG"
    fix "start the game once, then run this again"
fi

# --- 6. plugin <-> backend agreement ---------------------------------------
step "6. 两端是否对得上 / plugin and backend agreement"

CONFIG="$GAME_DIR/BepInEx/config/com.chillfocused.plugin.cfg"
PLUGIN_URL=""
PLUGIN_TOKEN=""
if [ -f "$CONFIG" ]; then
    PLUGIN_URL="$(sed -n 's/^FocusedUrl[[:space:]]*=[[:space:]]*//p' "$CONFIG" | head -1 | tr -d '\r')"
    PLUGIN_TOKEN="$(sed -n 's/^FocusedToken[[:space:]]*=[[:space:]]*//p' "$CONFIG" | head -1 | tr -d '\r')"
fi

if [ -n "$PLUGIN_URL" ]; then
    ok "the plugin points at $PLUGIN_URL"
    case "$PLUGIN_URL" in
        *:8765*) bad "that is the retired daemon port; Focused listens on 8766"
                 fix "edit $CONFIG: FocusedUrl = http://127.0.0.1:8766" ;;
    esac
else
    warn "the plugin has no configuration file yet"
    fix "start the game once so BepInEx writes $CONFIG"
fi

BACKEND_TOKEN=""
if [ -f "$FOCUSED_CONFIG" ]; then
    BACKEND_TOKEN="$(python3 -c 'import json,sys; print((json.load(open(sys.argv[1])).get("http") or {}).get("token",""))' "$FOCUSED_CONFIG" 2>/dev/null || echo '')"
    ok "Focused config: $FOCUSED_CONFIG"
else
    warn "Focused has no config yet at $FOCUSED_CONFIG (it is written on first run)"
fi

if [ -n "$PLUGIN_TOKEN" ] || [ -n "$BACKEND_TOKEN" ]; then
    if [ "$PLUGIN_TOKEN" = "$BACKEND_TOKEN" ]; then
        ok "the tokens match"
    else
        bad "the tokens differ: the plugin will be refused with 401"
        fix "put the same value in FocusedToken (plugin cfg) and http.token (Focused config)"
    fi
else
    ok "no token configured on either side (loopback only)"
fi

# --- 7. the configuration's own paths ---------------------------------------
#
# A configuration copied from another machine points at a home directory that
# does not exist here.  The daemon then cannot read or write its freeze state,
# and a suspension it cannot undo becomes permanent -- so the paths are checked
# rather than assumed.
if [ -f "$FOCUSED_CONFIG" ]; then
    entries="$(python3 -c 'import json,sys
try:
    data = json.load(open(sys.argv[1], encoding="utf-8"))
except Exception:
    raise SystemExit(0)
freeze = data.get("freeze") if isinstance(data.get("freeze"), dict) else {}
for label, value in (("audit_log", data.get("audit_log")),
                     ("freeze.state_path", freeze.get("state_path")),
                     ("plugins_path", data.get("plugins_path")),
                     ("registry_path", data.get("registry_path"))):
    if value:
        print("%s=%s" % (label, value))' "$FOCUSED_CONFIG" 2>/dev/null || echo '')"

    while IFS='=' read -r label value; do
        [ -n "$value" ] || continue
        case "$value" in
            "$HOME"/*) ;;
            *)  bad "the configuration points $label outside this account: $value"
                fix "rewrite it for $HOME, or delete the config and let Focused write a new one"
                continue ;;
        esac
        directory="$(dirname "$value")"
        if [ -d "$directory" ] && [ -w "$directory" ]; then
            ok "configuration path $label is inside this account and writable"
        elif [ -d "$directory" ]; then
            bad "the configuration points $label at a directory this account cannot write: $directory"
            fix "fix the permissions, or point $label somewhere else"
        else
            warn "the directory for $label does not exist yet: $directory"
            fix "Focused creates it on first use"
        fi
    done <<<"$entries"

    # The other half of the same hazard: a state file still full of suspensions.
    state_path="$(python3 -c 'import json,sys
try:
    data = json.load(open(sys.argv[1], encoding="utf-8"))
except Exception:
    raise SystemExit(0)
print((data.get("freeze") or {}).get("state_path", ""))' "$FOCUSED_CONFIG" 2>/dev/null || echo '')"

    if [ -n "$state_path" ] && [ -f "$state_path" ]; then
        left="$(python3 -c 'import json,sys
try:
    data = json.load(open(sys.argv[1], encoding="utf-8"))
except Exception:
    print(0); raise SystemExit(0)
print(len(data.get("frozen") or []))' "$state_path" 2>/dev/null || echo 0)"
        if [ "${left:-0}" -gt 0 ]; then
            warn "the freeze state file still records $left suspension(s): $state_path"
            fix "with the service stopped, clear them with: focusedd --thaw-all"
        else
            ok "the freeze state file is empty"
        fi
    fi
fi

# --- 8. the engine is actually running ---------------------------------------
#
# The status API now carries the same Health object the UI reads.  Ask it first:
# a scan loop that raises on every tick is degraded even while /focus answers.
status_json=""
if [ -n "${BACKEND_TOKEN:-}" ]; then
    status_json="$(curl -fsS -m 2 -H "X-Focused-Token: $BACKEND_TOKEN"         "$FOCUSED_URL/api/v1/status" 2>/dev/null || true)"
else
    status_json="$(curl -fsS -m 2 "$FOCUSED_URL/api/v1/status" 2>/dev/null || true)"
fi

health_line=""
if [ -n "$status_json" ]; then
    health_line="$(printf '%s' "$status_json" | python3 -c 'import json,sys
d = json.load(sys.stdin)
h = d.get("health") or {}
print("%d\t%d\t%.1f\t%s" % (
    1 if h.get("degraded") else 0,
    int(h.get("scan_failures") or 0),
    float((d.get("scan_ms") or {}).get("p95") or 0.0),
    (h.get("last_scan_error") or "").replace("\t", " ")))' 2>/dev/null || true)"
fi

if [ -n "$health_line" ]; then
    IFS=$'\t' read -r degraded failures p95 scan_error <<< "$health_line"
    if [ "${degraded:-0}" = "1" ]; then
        bad "the scan loop is degraded (${failures:-0} failure(s)): nothing is being enforced"
        [ -n "${scan_error:-}" ] && fix "last scan error: $scan_error"
        fix "read the reason: journalctl --user -u focused -n 50 --no-pager"
    elif [ "${failures:-0}" -gt 0 ]; then
        warn "the scan loop has ${failures} recent failure(s): enforcement may be intermittent"
        [ -n "${scan_error:-}" ] && fix "last scan error: $scan_error"
    else
        ok "the scan loop is healthy (no failures, p95 ${p95:-0} ms)"
    fi
    if [ "${p95:-0}" != "0.0" ] && [ "${p95%.*}" -gt 50 ] 2>/dev/null; then
        warn "scan p95 is ${p95} ms; the 1s loop has little headroom"
    fi
elif command -v journalctl >/dev/null 2>&1; then
    # Older servers do not expose /status health; keep the log-based fallback.
    scan_failures="$(journalctl --user -u focused --since '60 seconds ago' --no-pager 2>/dev/null | grep -c 'scan failed' || true)"
    case "${scan_failures:-0}" in
        ''|0) ok "the scan loop has not failed in the last minute" ;;
        *)    bad "the scan loop failed ${scan_failures} time(s) in the last minute: nothing is being enforced"
              fix "read the reason: journalctl --user -u focused -n 50 --no-pager" ;;
    esac
fi

# --- 9. the installed modules agree with each other ---------------------------
#
# A new engine.py paired with an older cgroupfs.py raises on every scan (a keyword
# argument the other side does not accept) and the install looks fine in pacman.
# Comparing the interfaces against each other catches a half-finished install
# without needing the source tree, which a packaged install does not have.
module_state="$(python3 - <<'PY' 2>/dev/null
import inspect
try:
    from focused.core import cgroupfs
    from focused.core.freezer import Freezer
    from focused import engine
except Exception:
    print("absent")
    raise SystemExit(0)

# The cgroup planner is called by the freeze service, not by engine.py.
# Keep the old engine.py fallback for an older packaged layout.
try:
    from focused.app import freeze as freeze_service
    caller_source = inspect.getsource(freeze_service)
except Exception:
    caller_source = inspect.getsource(engine)

accepts = "allowed_pids" in inspect.signature(cgroupfs.plan_for).parameters
calls = "allowed_pids" in caller_source
counts = hasattr(Freezer, "frozen_count")
uses = "frozen_count()" in inspect.getsource(engine) or "frozen_count()" in caller_source
print("ok" if (accepts == calls and counts == uses) else "mixed")
PY
)"
case "${module_state:-absent}" in
    ok)     ok "the installed modules agree with each other" ;;
    mixed)  bad "the installed modules do not agree: one side is newer than the other (a half-finished install)"
            fix "reinstall one consistent set, then restart the service" ;;
    *)      warn "the Focused python package could not be imported for a consistency check" ;;
esac

# --- summary ----------------------------------------------------------------
bar_line 100 "$(printf '%-40s %d/%d' "自检完成 / self-check complete" "$TOTAL_STEPS" "$TOTAL_STEPS")"
printf '\n%s\n' "小结 / summary: $OK ok, $WARN warning(s), $BAD problem(s)"
if [ "$BAD" -eq 0 ]; then
    printf '%s\n' "链路看起来是通的。打开 http://127.0.0.1:8766/ ，开始一轮专注，看「概览」里出现已冻结的应用即可。"
    exit 0
fi

printf '%s\n' "先修上面标 ✗ 的项，再跑一次本脚本。"
exit 1
