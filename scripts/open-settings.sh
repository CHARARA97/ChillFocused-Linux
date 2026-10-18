#!/usr/bin/env bash
# Compatibility wrapper; use scripts/dev.sh open (or game/open-ui.sh).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
"$here/game/open-ui.sh" "$@"
