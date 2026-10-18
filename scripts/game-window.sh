#!/usr/bin/env bash
# Wait for the game window and for ChillFocused to finish loading.
set -euo pipefail
WINDOW_NAME="${CHILLFOCUS_WINDOW:-Chill With You}"
LOG="${CHILLFOCUS_LOG:-$HOME/.local/share/Steam/steamapps/common/Chill with You Lo-Fi Story/BepInEx/LogOutput.log}"
TIMEOUT="${1:-120}"

for ((i = 0; i < TIMEOUT; i++)); do
    if xdotool search --name "$WINDOW_NAME" >/dev/null 2>&1; then
        WINDOW=$(xdotool search --name "$WINDOW_NAME" | head -1)
        if grep -q 'ChillFocused .*loaded' "$LOG" 2>/dev/null; then
            echo "WINDOW=$WINDOW"
            exit 0
        fi
    fi
    sleep 1
done

echo "game window/plugin did not become ready in ${TIMEOUT}s" >&2
exit 1
