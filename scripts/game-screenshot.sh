#!/usr/bin/env bash
# Wait a fixed grace period, optionally open the ChillFocused panel, screenshot it.
#
# Usage: scripts/game-screenshot.sh [--wait SECONDS] [--open-ui] [--key KEY]
#                                   [--out FILE] [--no-notify]
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WAIT=10
OPEN_UI=0
KEY=""
OUT=""
NOTIFY=1
while [ "$#" -gt 0 ]; do
    case "$1" in
        --wait) WAIT="$2"; shift 2 ;;
        --open-ui) OPEN_UI=1; shift ;;
        --key) KEY="$2"; shift 2 ;;
        --out) OUT="$2"; shift 2 ;;
        --no-notify) NOTIFY=0; shift ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
done

read -r WINDOW < <("$HERE/game-window.sh" 120 | sed -n 's/^WINDOW=//p')
[ -n "$WINDOW" ] || { echo "no game window" >&2; exit 1; }
echo "waiting ${WAIT}s for the game to settle"
sleep "$WAIT"

if [ "$OPEN_UI" -eq 1 ] || [ -n "$KEY" ]; then
    xdotool windowactivate --sync "$WINDOW" 2>/dev/null || xdotool windowfocus "$WINDOW" 2>/dev/null || true
    if [ -n "$KEY" ]; then
        xdotool key --window "$WINDOW" "$KEY"
    else
        xdotool key --window "$WINDOW" F7
    fi
    sleep 2
fi

if [ -z "$OUT" ]; then
    OUT="/tmp/chillfocus-$(date +%H%M%S).png"
fi
import -window "$WINDOW" "$OUT" 2>/dev/null || grim -g "$(xdotool getwindowgeometry --shell "$WINDOW" | \
    awk -F= '/^X=/{x=$2}/^Y=/{y=$2}/^WIDTH=/{w=$2}/^HEIGHT=/{h=$2}END{print x","y" "w"x"h}')" "$OUT"
[ -s "$OUT" ] || { echo "screenshot failed" >&2; exit 1; }
echo "SCREENSHOT=$OUT"
if [ "$NOTIFY" -eq 1 ] && command -v notify-send >/dev/null 2>&1; then
    notify-send "ChillFocused UI" "截图已保存：$OUT" || true
fi
