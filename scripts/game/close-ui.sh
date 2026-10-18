#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=/dev/null
source "$here/../lib/game-env.sh"
close_settings_ui
echo "SettingUI closed"
