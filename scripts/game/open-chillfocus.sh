#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=/dev/null
source "$here/../lib/game-env.sh"
open_chillfocus_ui
echo "ChillFocus page opened"
