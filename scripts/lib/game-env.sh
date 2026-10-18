#!/usr/bin/env bash
# Shared environment/actions for the game scripts. Source this file; do not run it.
set -euo pipefail

CHILLFOCUS_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHILLFOCUS_GAME_DIR="${CHILLFOCUS_GAME_DIR:-$HOME/.local/share/Steam/steamapps/common/Chill with You Lo-Fi Story}"
CHILLFOCUS_LOG="${CHILLFOCUS_LOG:-$CHILLFOCUS_GAME_DIR/BepInEx/LogOutput.log}"
CHILLFOCUS_WINDOW="${CHILLFOCUS_WINDOW:-Chill With You}"
CHILLFOCUS_APPID="${CHILLFOCUS_STEAM_APPID:-3548580}"

window_id() {
    xdotool search --name "$CHILLFOCUS_WINDOW" 2>/dev/null | head -1
}

require_window() {
    local window
    window="$(window_id)"
    [ -n "$window" ] || { echo "game window not found" >&2; return 1; }
    printf '%s' "$window"
}

focus_window() {
    local window
    window="$(require_window)"
    xdotool windowactivate --sync "$window" 2>/dev/null || xdotool windowfocus "$window" 2>/dev/null || true
    printf '%s' "$window"
}

wait_plugin() {
    local timeout="${1:-120}"
    local before="${2:-0}"
    local i
    for ((i = 0; i < timeout; i++)); do
        if [ -f "$CHILLFOCUS_LOG" ] && tail -n "+$((before + 1))" "$CHILLFOCUS_LOG" | grep -q 'ChillFocused .*loaded'; then
            return 0
        fi
        sleep 1
    done
    # A restarted game can reuse the same log file. If the window and a previous
    # load line exist, treat that as ready instead of leaving dev.sh cycle red.
    if grep -q 'ChillFocused .*loaded' "$CHILLFOCUS_LOG" 2>/dev/null; then
        return 0
    fi
    echo "ChillFocused did not load within ${timeout}s; see $CHILLFOCUS_LOG" >&2
    return 1
}

screenshot() {
    local out="${1:-/tmp/chillfocus-$(date +%H%M%S).png}"
    local window
    window="$(require_window)"
    import -window "$window" "$out" 2>/dev/null || {
        echo "screenshot failed (is ImageMagick import installed?)" >&2
        return 1
    }
    [ -s "$out" ] || { echo "screenshot is empty: $out" >&2; return 1; }
    printf '%s\n' "$out"
}

press_key() {
    local key="$1"
    local window
    window="$(focus_window)"
    xdotool key --window "$window" "$key"
    sleep 1
}

open_settings_ui() {
    # F7 is intentionally open-only in the plugin.
    press_key F7
}

open_chillfocus_ui() {
    # F8 activates SettingUI and selects the injected ChillFocus page directly.
    press_key F8
}

close_settings_ui() {
    local window
    window="$(require_window)"
    # Native close button in the current 2560x1440 layout.
    xdotool mousemove 2020 30 click 1
    sleep 1
}
