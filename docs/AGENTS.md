# 游戏插件文档

- `design.md` — 游戏内会话/计时/UI 设计。
- `protocol.md` — 后端契约副本；canonical 在 `../../focused/docs/focused-protocol.md`。
- `publishing.md` — 发布流程。
- `known-issues.md` — 插件侧/跨项目问题入口。
- `architecture-backlog.md` — 架构审计待办、优先级、验收标准。
- `timer-probe-verification.md` — C-05 真机验证矩阵。
- `game-timer-api.md` — 游戏官方计时器 API、调用顺序与周期校正。
- `native-ui-probe.md` — 原版 SettingUI 的实测颜色、尺寸和动画参数。
- `session-locks.md` — 会话锁（隐藏计时器按钮 / 禁止关闭游戏）的设计与取证。
- `awesome-entry.md` — 社区条目草稿。

改后端协议时先改 `focused`，再同步这里的 `protocol.md`。
