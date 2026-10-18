# Ports

- `IOverlayActions` — 面板/进程选择器可调用的窄动作。
- `JsonCodec.cs` — `IJsonCodec`，由 `Platform/Unity/JsonUtilityCodec` 实现。
- `IFocusRunner` — `RuntimeHost` 需要的最小 runner 面；真实实现是 `Runtime/FocusRunner`。

接口保持狭小；不要在这里放业务实现。

- `IFocusedEndpoint` — `IStatusFeed` + `IOverlayActions` + `Pump`；`FocusRunner` 不再持有具体 client。

- `ITimerSource` / `NullTimerSource` — runner/sync 的计时状态端口；真实实现是 `Platform/Unity/GameTimerSource`。

- `ITimerController` — 写游戏计时器的端口（SetWorkMinutes/Start/Reset）。
- `ISessionReportGate` — 外部会话期间抑制 game-timer 心跳。
