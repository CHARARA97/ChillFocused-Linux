#!/usr/bin/env bash
# Compatibility wrapper; use scripts/dev.sh close.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
"$here/game/close-ui.sh" "$@"
