#!/usr/bin/env bash
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
"$here/../build.sh"
"$here/../install-bepinex.sh" --no-doctor
