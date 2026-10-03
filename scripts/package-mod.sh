#!/usr/bin/env bash
#
# Build the mod and lay out exactly what a release ships: one DLL.
#
# Users of this game are used to "download a DLL, drop it in BepInEx/plugins".
# A zip with a folder structure inside is one more chance to unpack it one level
# wrong, so the release artifact is a single file plus its checksum.
#
# Usage:
#   scripts/package-mod.sh [--offline]
#
# Output:
#   dist/mod/ChillFocused.dll
#   dist/mod/ChillFocused.dll.sha256
#
# The version comes from <Version> in plugin/ChillFocused/ChillFocused.csproj.
# The git commit is folded into the assembly as AssemblyInformationalVersion, and
# the plugin logs it -- so "which build is this?" is answerable from the DLL alone.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
PROJECT="$ROOT/ChillFocused/ChillFocused.csproj"
OUT_DIR="$ROOT/dist/mod"

OFFLINE=0
while [ "$#" -gt 0 ]; do
    case "$1" in
        --offline) OFFLINE=1; shift ;;
        -h|--help) sed -n '2,19p' "$0"; exit 0 ;;
        *) echo "unknown argument: $1" >&2; exit 2 ;;
    esac
done

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT/.build/dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-$ROOT/.build/nuget}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

say() { printf '[package] %s\n' "$*"; }

# The version is the project file's, which is also what VersionConsistencyTests
# holds the plugin's own constant against.
version="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$PROJECT" | head -1)"
if [ -z "$version" ]; then
    echo "could not read <Version> from $PROJECT" >&2
    exit 1
fi

revision="$(git -C "$ROOT" rev-parse --short=7 HEAD 2>/dev/null || true)"
say "version $version${revision:+, commit $revision}"

restore_args=()
build_args=()
if [ "$OFFLINE" -eq 1 ]; then
    # Reuse what is already in the local package cache: no network round trip.
    # Only useful on a machine that has built this before.
    say "offline: restoring from $NUGET_PACKAGES"
    dotnet restore "$PROJECT" --source "$NUGET_PACKAGES" >/dev/null
    build_args+=(--no-restore)
fi
if [ -n "$revision" ]; then
    build_args+=("-p:SourceRevisionId=$revision")
fi

say "building (Release)"
dotnet build "$PROJECT" -c Release "${build_args[@]}" -v minimal --nologo

dll="$ROOT/ChillFocused/bin/Release/net472/ChillFocused.dll"
if [ ! -f "$dll" ]; then
    echo "build produced no DLL at $dll" >&2
    exit 1
fi

mkdir -p "$OUT_DIR"
rm -f "$OUT_DIR"/*.dll "$OUT_DIR"/*.sha256
cp -f "$dll" "$OUT_DIR/ChillFocused.dll"

# A release should be verifiable: the installer can check this, and so can a user.
( cd "$OUT_DIR" && sha256sum ChillFocused.dll > ChillFocused.dll.sha256 )

say "artifact:"
ls -1 "$OUT_DIR" | sed 's/^/  /'
say "size: $(stat -c '%s' "$OUT_DIR/ChillFocused.dll") bytes"

# What the DLL says about itself, so a mismatch is obvious here rather than in a
# bug report three days later.
if command -v strings >/dev/null 2>&1; then
    strings -a "$OUT_DIR/ChillFocused.dll" \
        | grep -m1 -E "^${version}([+][0-9a-f]+)?$" \
        | sed 's/^/[package] DLL reports version /' || true
fi
