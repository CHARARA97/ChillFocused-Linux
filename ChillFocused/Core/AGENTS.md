# ChillFocused Core

按层找文件：

- [Domain/](Domain/AGENTS.md) — 纯策略/数据，无 Unity/BepInEx。
- [Sync/](Sync/AGENTS.md) — 同步线程、规则、心跳、HTTP、DTO。
- [Session/](Session/AGENTS.md) — 外部 Focused 会话 → 游戏计时器的镜像。
- [Status/](Status/AGENTS.md) — UI/面板读的状态快照与轮询。
- [Ports/](Ports/AGENTS.md) — UI/JSON 窄接口。
- [UI/](UI/AGENTS.md) — 纯 UI 模型与 Unity 视图。
- [Runtime/](Runtime/AGENTS.md) — 插件运行时/入口组件。
- [Config/](Config/AGENTS.md) — 配置文件与 mtime 监听。
- [Support/](Support/AGENTS.md) — 帧、锁、安装检查、主线程泵。
- [Platform/](Platform/AGENTS.md) — 游戏适配与 Harmony/反射。

测试工程只自动编译：`Domain`、`Sync`、`Status`、`Session`、`Ports`、`Config`、`Support`、`UI/Model`、`Platform/Pure`。
`UI/Unity`、`Runtime`、`Platform/Unity` 是 Unity/BepInEx 适配层，不进入测试工程。
