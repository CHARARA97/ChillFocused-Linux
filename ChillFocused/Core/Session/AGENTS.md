# Session

`ExternalSessionMirror` 把 Focused 的手动会话镜像到游戏计时器。

- 只处理 `manual_session=true`；浏览器 delegated claim 暂不处理。
- 只通过 `ITimerController` 写游戏计时器。
- 通过 `ISessionReportGate` 抑制 game-timer 心跳，避免回环。
- `SessionPlan` 只使用整数分钟；countdown 映射为一轮 Pomodoro。
- 外部会话结束固定 `Reset` 游戏计时器；不提供 Pause 配置。
