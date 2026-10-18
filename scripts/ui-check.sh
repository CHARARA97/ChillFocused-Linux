#!/usr/bin/env bash
# Compatibility wrapper for the old loop.
# Prefer: scripts/dev.sh cycle   or   scripts/dev.sh verify
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
INSTALL=0
OPEN=0
WAIT=10
for arg in "$@"; do
    case "$arg" in
        --install) INSTALL=1 ;;
        --open-ui) OPEN=1 ;;
        --wait) shift ;;
        *) ;;
    esac
done
[ "$INSTALL" -eq 1 ] && "$here/dev.sh" install
"$here/dev.sh" restart
[ "$OPEN" -eq 1 ] && "$here/dev.sh" open
"$here/dev.sh" screenshot
