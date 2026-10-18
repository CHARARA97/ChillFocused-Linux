#!/usr/bin/env bash
# Compatibility wrapper; use scripts/dev.sh verify.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
"$here/ui/verify.sh" "$@"
