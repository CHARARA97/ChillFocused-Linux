# Support

`RuntimeHost`（组合根 + `RuntimeHostContext`）、`LogEvent`（统一日志格式）、`PerfCounter`（耗时计数）、`RunnerHost`（runner 生命周期）、`SyncHost`（sync 组装）、`SettingsRefresher`、`FrameDriver`、`MainThreadPump`、`PluginInstanceLock`、`PluginInstallCheck`、`PluginInfo`。

小型运行时支撑；无 Unity 依赖的进入测试工程。单实例锁必须保持进程级生命周期。
