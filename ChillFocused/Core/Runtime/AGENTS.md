# Runtime

`FocusRunner`（Unity 帧/状态协调）、`PluginConfig`、`PluginActions`、`HotkeyController`。

BepInEx/Unity 适配层，不进入测试工程。不要在这里塞同步/规则实现。

`FocusRunnerWiring` 按 `Services/Panel/Hotkeys/Actions` 分组，避免初始化处平铺十几个字段。

`FocusRunner.Tick` 每帧把 `(LockTimerControls 开关, Status.Timer)` 交给 `TimerControlGuard`；
判定逻辑在 `Core/Domain/TimerControlPlan`，这里只做调用。
