# 游戏插件脚本

统一入口：`scripts/dev.sh <command>`。

```text
build      构建测试与插件
install    build + install-bepinex
start      启动游戏（已运行则等待插件）
stop       停止游戏
restart    stop + start
open       打开游戏 SettingUI（F7，仅打开）
tab        直接打开 ChillFocus 注入页（F8）
close      关闭 SettingUI
screenshot 截图当前游戏窗口
logs [pat] 过滤 BepInEx 日志
verify     打开页面、滚动、管理进程、返回并截图
cycle      install + restart + verify
```

分层：

- `scripts/lib/game-env.sh` — 窗口/日志/按键/截图等公共函数
- `scripts/game/*` — 启动、停止、构建安装、打开/关闭页面、截图、日志
- `scripts/ui/verify.sh` — 只组合页面验证
- `scripts/dev.sh` — 总入口

兼容入口 `open-settings.sh`、`open-chillfocus-tab.sh`、`close-settings.sh`、`verify-injected-ui.sh`、`ui-check.sh` 只转发到新脚本。

旧的低层脚本 `game-window.sh`、`game-screenshot.sh`、`restart-game.sh` 保留给已有调用，不再作为推荐入口。
