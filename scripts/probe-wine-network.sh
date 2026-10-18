#!/usr/bin/env bash
#
# Build the Wine network probe and run it inside the game's Proton prefix.
#
# Usage:
#   scripts/probe-wine-network.sh [--url http://127.0.0.1:8765] [--appid 3548580]
#
# The probe is a plain net472 console program run through protontricks, so it
# uses the same networking path as the BepInEx plugin while depending on none of
# BepInEx, Unity or the plugin code. If the plugin cannot reach Focused, this
# says whether Wine networking or the plugin's HTTP client is at fault.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_CLI_HOME="$ROOT/.build/dotnet-home"
export NUGET_PACKAGES="$ROOT/.build/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

APPID=3548580
URL="http://127.0.0.1:8765"

while [ "$#" -gt 0 ]; do
    case "$1" in
        --url) URL="$2"; shift 2 ;;
        --appid) APPID="$2"; shift 2 ;;
        -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
done

OUT="$ROOT/.build/netprobe"
mkdir -p "$OUT"

echo "==> building probe (net472 console)"
dotnet build "$ROOT/tools/netprobe/netprobe.csproj" -c Release \
    -o "$OUT" -v quiet --nologo >/dev/null

EXE="$OUT/netprobe.exe"
[ -f "$EXE" ] || { echo "build produced no netprobe.exe" >&2; exit 1; }
echo "    $EXE"

# protontricks writes its own cache; keep it inside the tree so this works in
# sandboxes with a read-only home.
export XDG_CACHE_HOME="${XDG_CACHE_HOME:-$ROOT/.build/cache}"
mkdir -p "$XDG_CACHE_HOME"

# The prefix sees the host filesystem as Z:, and wine wants a Windows path.
WIN_EXE="Z:$(echo "$EXE" | tr '/' '\\')"

echo "==> running it inside prefix $APPID (this starts a short-lived wine process)"
echo
protontricks -c "wine \"$WIN_EXE\" \"$URL\"" "$APPID" 2>&1 \
    | grep -vE '^pressure-vessel-wrap|not sharing|ntsync:|^[0-9a-f]{4}:(err|fixme|warn)' \
    || true
