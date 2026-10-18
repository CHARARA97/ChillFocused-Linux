# Platform Unity

`GameTimerProbe`、`GameProbe`、`FrameHook`、`GameHookInstaller`、`JsonUtilityCodec`、`ConfigBridge`。

Harmony/Unity/BepInEx 适配层；真实游戏验证优先于纯单测。

`GameTimerSource` 把静态 `GameTimerProbe` 适配成 `ITimerSource`；Harmony 安装仍在 `GameTimerProbe`。

`GameTimerController` 通过反射调用游戏的 Pomodoro/Countup 服务；优先复用 `GameTimerProbe` 缓存实例。

官方调用顺序见 [docs/game-timer-api.md](../../../docs/game-timer-api.md)。
