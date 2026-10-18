#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=/dev/null
source "$here/../lib/game-env.sh"

# The caller may be using this as the whole smoke test; make it idempotent.
"$here/../game/start.sh" >/dev/null

open_settings_ui
open_chillfocus_ui
sleep 1
out_main="/tmp/chillfocus-main.png"
out_bottom="/tmp/chillfocus-main-bottom.png"
out_process="/tmp/chillfocus-process.png"
out_back="/tmp/chillfocus-back.png"
screenshot "$out_main" >/dev/null

# Scroll to the blocked-app/manage area.
xdotool mousemove 900 700 click 5 click 5 click 5 click 5
sleep 1
screenshot "$out_bottom" >/dev/null

# Manage processes button (right-hand native action button).
xdotool mousemove 1460 720 click 1
sleep 2
screenshot "$out_process" >/dev/null

# Back button on the process page.
xdotool mousemove 1460 220 click 1
sleep 2
screenshot "$out_back" >/dev/null

printf 'main:    %s\nbottom:  %s\nprocess: %s\nback:    %s\n' \
    "$out_main" "$out_bottom" "$out_process" "$out_back"
