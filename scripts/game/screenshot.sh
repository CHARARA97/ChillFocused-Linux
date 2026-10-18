#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=/dev/null
source "$here/../lib/game-env.sh"
screenshot "${1:-/tmp/chillfocus-$(date +%H%M%S).png}"
