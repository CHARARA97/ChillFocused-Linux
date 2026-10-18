#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
"$here/stop.sh" || true
sleep 1
"$here/start.sh"
