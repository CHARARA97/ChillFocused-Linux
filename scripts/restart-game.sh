#!/usr/bin/env bash
# Close the game safely and relaunch it through Steam.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_ID="${CHILLFOCUS_STEAM_APPID:-3548580}"
TIMEOUT="${1:-120}"

pids="$(pgrep -f 'Chill With You[.]exe$' 2>/dev/null || true)"
if [ -n "$pids" ]; then
    echo "closing game: $pids"
    for pid in $pids; do kill "$pid" 2>/dev/null || true; done
    sleep 4
fi

echo "launching Steam app $APP_ID"
steam -applaunch "$APP_ID" >/dev/null 2>&1 || true
"$HERE/game-window.sh" "$TIMEOUT"
