# Status

`FocusStatus`（单所有者快照）、`FocusStatusMapper`（wire → 状态）、`StatusPoller`（只读 /focus + /events）。

`IStatusFeed` 定义在 `StatusPoller.cs`；`FocusedClient` 实现它。UI 不自己算轮询周期。

`StatusPoller` 记录请求次数与 last/max 耗时；F6 的 `PluginDebugSnapshot` 会打印。
