#!/usr/bin/env bash
#
# Build the plugin and the pure-logic tests.
#
# dotnet writes to ~/.dotnet and ~/.nuget by default; inside a sandboxed or
# read-only home that fails, so both are redirected into .build/ here. That also
# keeps the repository self-contained: deleting .build/ undoes everything.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# --offline restores from the local package cache instead of nuget.org, which is
# what you want on a machine that has built this before and has no network.
OFFLINE=0
for arg in "$@"; do
    case "$arg" in
        --offline) OFFLINE=1 ;;
        -h|--help) sed -n '2,10p' "$0"; exit 0 ;;
        *) echo "unknown argument: $arg" >&2; exit 2 ;;
    esac
done
export DOTNET_CLI_HOME="$ROOT/.build/dotnet-home"
export NUGET_PACKAGES="$ROOT/.build/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "dotnet SDK not found. On Arch:  sudo pacman -S dotnet-sdk" >&2
    exit 1
fi

restore_args=()
if [ "$OFFLINE" -eq 1 ]; then
    restore_args=(--source "$NUGET_PACKAGES")
fi

echo "==> building ChillFocused.Tests (net8.0, no Unity required)"
dotnet restore "$ROOT/ChillFocused.Tests/ChillFocused.Tests.csproj" "${restore_args[@]}" >/dev/null
dotnet build "$ROOT/ChillFocused.Tests/ChillFocused.Tests.csproj" --no-restore -v minimal

echo
echo "==> building ChillFocus plugin (net472, against the real game assemblies)"
if dotnet restore "$ROOT/ChillFocused/ChillFocused.csproj" "${restore_args[@]}" >/dev/null \
   && dotnet build "$ROOT/ChillFocused/ChillFocused.csproj" --no-restore -v minimal; then
    echo
    echo "artifact: $ROOT/plugin/ChillFocused/bin/Debug/net472/ChillFocused.dll"
    echo "deploy:   scripts/install-bepinex.sh"
else
    echo
    echo "The plugin build needs the game installed and discoverable." >&2
    echo "Set CHILLFOCUS_GAME_DIR, or copy plugin/.env.props.example to plugin/.env.props." >&2
    exit 1
fi
