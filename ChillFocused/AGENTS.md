# ChillFocused C# 插件

入口 `Plugin.cs`；实现按 [Core/AGENTS.md](Core/AGENTS.md) 分层：

- [Core/Domain](Core/Domain/AGENTS.md) — 纯策略/数据。
- [Core/Sync](Core/Sync/AGENTS.md) — 同步、规则、心跳、HTTP。
- [Core/Status](Core/Status/AGENTS.md) — 状态快照/轮询。
- [Core/Ports](Core/Ports/AGENTS.md) — 窄接口。
- [Core/UI](Core/UI/AGENTS.md) — UI 模型与原生注入。
- [Core/Runtime](Core/Runtime/AGENTS.md) — 运行时/入口组件。
- [Core/Config](Core/Config/AGENTS.md)、[Core/Support](Core/Support/AGENTS.md)、[Core/Platform](Core/Platform/AGENTS.md)。

测试：`ChillFocused.Tests` 自动编译纯目录；`UI/Unity`、`Runtime`、`Platform/Unity` 是 Unity/BepInEx 适配层。

当前 UI 唯一入口是游戏 SettingUI 的 `ChillFocus` Tab；没有独立自绘面板/HUD/toast。F7 打开 SettingUI，F8 直接打开注入页。

后端只认 HTTP；不要在插件里复制冻结/规则决策。
