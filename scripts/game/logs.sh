#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=/dev/null
source "$here/../lib/game-env.sh"
pattern="${1:-ChillFocused|settings-inject|ui-inject|error|warning}"
tail -n "${2:-80}" "$CHILLFOCUS_LOG" | grep -Ei "$pattern" || true
