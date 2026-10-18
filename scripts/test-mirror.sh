#!/usr/bin/env bash
#
# Drive the mirror end-to-end without touching the Focused UI:
#   launch the game if needed, start/stop Focused sessions through the API,
#   then assert the plugin logged the expected mirror transitions.
#
# Usage:
#   scripts/test-mirror.sh                 # use a running game, or launch it
#   scripts/test-mirror.sh --restart       # close + relaunch, then test
#   scripts/test-mirror.sh --install       # build/install before restarting
#   scripts/test-mirror.sh --launch-only   # just make sure the game is up
#
# This still restarts the game when the DLL changes; the point is that a whole
# scenario is one command and no manual Focused clicks.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
LOG="${CHILLFOCUS_LOG:-$HOME/.local/share/Steam/steamapps/common/Chill with You Lo-Fi Story/BepInEx/LogOutput.log}"
BASE="${FOCUSED_URL:-http://127.0.0.1:8766/api/v1}"
APP_ID="${CHILLFOCUS_STEAM_APPID:-3548580}"
WINDOW_NAME="${CHILLFOCUS_WINDOW:-Chill With You}"
RESTART=0
INSTALL=0
LAUNCH_ONLY=0
for arg in "$@"; do
    case "$arg" in
        --restart) RESTART=1 ;;
        --install) INSTALL=1 ;;
        --launch-only) LAUNCH_ONLY=1 ;;
        -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
        *) echo "unknown argument: $arg" >&2; exit 2 ;;
    esac
done

say() { printf '[mirror-test] %s\n' "$*"; }
fail() { printf '[mirror-test] FAIL: %s\n' "$*" >&2; exit 1; }

window_open() {
    command -v xdotool >/dev/null 2>&1 || return 1
    xdotool search --name "$WINDOW_NAME" >/dev/null 2>&1
}

wait_for_log() {
    local pattern="$1" timeout="${2:-90}" start="$3" i
    for ((i = 0; i < timeout; i++)); do
        if tail -n "+$((start + 1))" "$LOG" 2>/dev/null | grep -q "$pattern"; then
            return 0
        fi
        sleep 1
    done
    return 1
}

log_lines() { wc -l < "$LOG" 2>/dev/null || echo 0; }

start_http() {
    curl -fsS -X POST -H 'Content-Type: application/json' \
        -d '{"mode":"stopwatch"}' "$BASE/focus/session" >/dev/null
}

start_pomodoro() {
    curl -fsS -X POST -H 'Content-Type: application/json' \
        -d '{"mode":"pomodoro","work_minutes":1,"break_minutes":1,"cycles":2}' \
        "$BASE/focus/session" >/dev/null
}

stop_http() {
    curl -fsS -X POST -H 'Content-Type: application/json' \
        -d '{"stop":true}' "$BASE/focus/session" >/dev/null || true
}

wait_focus_idle() {
    local i
    for ((i = 0; i < 20; i++)); do
        if curl -fsS "$BASE/focus" 2>/dev/null | python3 -c \
            'import json,sys; d=json.load(sys.stdin); sys.exit(0 if not d.get("active") and not d.get("manual_session") else 1)'; then
            return 0
        fi
        sleep 0.5
    done
    return 1
}

restart_game() {
    local pids pid
    pids="$(pgrep -f 'Chill With You[.]exe$' 2>/dev/null || true)"
    if [ -n "$pids" ]; then
        say "closing the game process"
        for pid in $pids; do
            kill "$pid" 2>/dev/null || true
        done
        sleep 4
    elif window_open; then
        say "closing the game window"
        xdotool search --name "$WINDOW_NAME" windowclose 2>/dev/null || true
        sleep 4
    fi
    say "launching Steam app $APP_ID"
    steam -applaunch "$APP_ID" >/dev/null 2>&1 || true
}

if [ "$INSTALL" -eq 1 ]; then
    say "building and installing"
    "$HERE/build.sh"
    "$HERE/install-bepinex.sh"
fi

NEED_NEW_LOAD=0
if [ "$RESTART" -eq 1 ]; then
    restart_game
    NEED_NEW_LOAD=1
elif ! window_open; then
    say "game window not found; launching"
    steam -applaunch "$APP_ID" >/dev/null 2>&1 || true
    NEED_NEW_LOAD=1
fi

[ -f "$LOG" ] || fail "log not found: $LOG"
if [ "$NEED_NEW_LOAD" -eq 1 ]; then
    say "waiting for ChillFocused to load"
    before="$(log_lines)"
    if ! wait_for_log 'ChillFocused .*loaded' 120 "$before"; then
        fail "plugin did not load; see $LOG"
    fi
else
    if ! grep -q 'ChillFocused .*loaded' "$LOG"; then
        fail "running game has no ChillFocused load line; see $LOG"
    fi
fi
say "plugin loaded"

[ "$LAUNCH_ONLY" -eq 1 ] && { say "game is up"; exit 0; }

# Countup: start, observe, stop.
stop_http || true
wait_focus_idle || true
before="$(log_lines)"
say "starting countup"
start_http
if ! wait_for_log 'session\.mirror\.start mode=Countup' 15 "$before"; then
    fail "countup mirror did not start"
fi
say "countup mirror started"
sleep 2
stop_http
if ! wait_for_log 'session\.mirror\.stop action=Reset' 15 "$before"; then
    fail "countup mirror did not stop"
fi
wait_focus_idle || fail "Focused session did not become idle after countup"
say "countup stopped"

# Pomodoro after countup: this is the regression that used to be suppressed.
before="$(log_lines)"
say "starting pomodoro after countup"
start_pomodoro
if ! wait_for_log 'session\.mirror\.start mode=Pomodoro' 15 "$before"; then
    fail "second pomodoro mirror did not start"
fi
say "pomodoro mirror started"
sleep 2
stop_http
wait_focus_idle || fail "Focused session did not become idle after pomodoro"
say "pomodoro stopped"

if tail -n "+$((before + 1))" "$LOG" | grep -q 'session\.mirror\.sync\.failed'; then
    fail "periodic sync reported a failure"
fi

say "PASS: countup -> stop -> pomodoro -> stop"
