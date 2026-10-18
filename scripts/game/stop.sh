#!/usr/bin/env bash
set -euo pipefail
pids="$(pgrep -f 'Chill With You[.]exe$' 2>/dev/null || true)"
if [ -z "$pids" ]; then
    echo "game is not running"
    exit 0
fi
echo "closing game: $pids"
for pid in $pids; do kill "$pid" 2>/dev/null || true; done
for _ in {1..20}; do
    pgrep -f 'Chill With You[.]exe$' >/dev/null 2>&1 || { echo "game stopped"; exit 0; }
    sleep 0.5
done
echo "game did not stop" >&2
exit 1
