#!/usr/bin/env bash
# Single entry point for the ChillFocused development loop.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
cmd="${1:-help}"
shift || true
case "$cmd" in
    build)       "$here/build.sh" "$@" ;;
    install)     "$here/game/build-install.sh" "$@" ;;
    start)       "$here/game/start.sh" "$@" ;;
    stop)        "$here/game/stop.sh" "$@" ;;
    restart)     "$here/game/restart.sh" "$@" ;;
    open)        "$here/game/open-ui.sh" "$@" ;;
    tab)         "$here/game/open-chillfocus.sh" "$@" ;;
    close)       "$here/game/close-ui.sh" "$@" ;;
    screenshot)  "$here/game/screenshot.sh" "$@" ;;
    logs)        "$here/game/logs.sh" "$@" ;;
    verify)      "$here/ui/verify.sh" "$@" ;;
    cycle)       "$here/game/build-install.sh" && "$here/game/restart.sh" && "$here/ui/verify.sh" ;;
    help|*)      cat <<'TXT'
Usage: scripts/dev.sh <command> [args]

  build         build plugin + tests
  install       build and install into the game
  start         launch game if needed, wait for plugin load
  stop          stop game
  restart       stop + start
  open          open the game's SettingUI (F7)
  tab           open the injected ChillFocus page (F8)
  close         close SettingUI
  screenshot    capture the game window
  logs [pat]    tail filtered BepInEx log
  verify        open the page, scroll/manage/back, capture screenshots
  cycle         build + install + restart + verify
TXT
        ;;
esac
