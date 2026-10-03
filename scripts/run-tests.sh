#!/usr/bin/env bash
#
# Run the mod's own tests.
#
#   scripts/run-tests.sh [--offline]
#
# Two halves, both needed:
#   * the C# test project, which compiles the Unity-free half of the plugin and
#     needs neither the game nor BepInEx;
#   * the installer tests, which run scripts/install-mod.sh against a throwaway
#     game directory -- a single-DLL release lives or dies by that script.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OFFLINE=0

while [ "$#" -gt 0 ]; do
    case "$1" in
        --offline) OFFLINE=1; shift ;;
        -h|--help) sed -n '2,12p' "$0"; exit 0 ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
done

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT/.build/dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$ROOT/.build/nuget}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "==> plugin tests (net8.0, no game required)"
tests="$ROOT/ChillFocused.Tests/ChillFocused.Tests.csproj"
restore_args=()
if [ "$OFFLINE" -eq 1 ]; then
    restore_args=(--source "$NUGET_PACKAGES")
fi
dotnet restore "$tests" "${restore_args[@]}" >/dev/null
dotnet test "$tests" --no-restore -v minimal --nologo

echo
echo "==> installer tests (throwaway game directory, nothing real is touched)"
( cd "$ROOT" && PYTHONPATH="$ROOT" python3 -m unittest discover -s tests -t . -v )

echo
echo "all tests passed"
