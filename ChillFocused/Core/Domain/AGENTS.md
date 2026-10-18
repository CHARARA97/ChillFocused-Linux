# Domain

纯策略与值对象：`FocusSettings`、`SessionGate`、`TimerMode`、`TimerState`、`RuleText`、`GameClock`。

不引用 UnityEngine/BepInEx。新增纯逻辑优先放这里，测试工程会自动编译。

`SessionPlan` / `SessionMirrorPolicy` / `ExternalSessionState` 是纯会话镜像逻辑，无 Unity/BepInEx。

`TimerControlPlan` 是“专注中该隐藏哪些计时按钮”的纯判定：番茄钟三键、正向计时只藏暂停。
