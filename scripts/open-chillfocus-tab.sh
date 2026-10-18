#!/usr/bin/env bash
# Compatibility wrapper; use scripts/dev.sh tab (or game/open-chillfocus.sh).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
"$here/game/open-chillfocus.sh" "$@"
