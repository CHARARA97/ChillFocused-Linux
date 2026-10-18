#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=/dev/null
source "$here/../lib/game-env.sh"
if window_id >/dev/null; then
    echo "game already running"
    if grep -q 'ChillFocused .*loaded' "$CHILLFOCUS_LOG" 2>/dev/null; then
        echo "game ready"
        exit 0
    fi
    wait_plugin 120 0
    exit 0
fi
before=0
[ -f "$CHILLFOCUS_LOG" ] && before="$(wc -l < "$CHILLFOCUS_LOG")"
echo "launching Steam app $CHILLFOCUS_APPID"
steam -applaunch "$CHILLFOCUS_APPID" >/dev/null 2>&1 || true
wait_plugin 120 "$before"
echo "game ready"
