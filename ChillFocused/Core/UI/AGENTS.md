# UI

- [Model/](Model/AGENTS.md) — 纯文本/HUD 模型，进入测试工程。
- [Unity/](Unity/AGENTS.md) — Unity 适配与原生 SettingUI 注入。

`Unity/` 当前只保留原生注入：

- `SettingsPageInjector` — 页面编排
- `MainPageView` / `ProcessPageView` — 页面布局
- `ChillFocusTabHost` — 原生 Tab/Page 生命周期
- `ChillFocusCopy` — 用户可见文案
- `TimerControlGuard` / `TimerControlNames` / `TimerButtonLookup` — 计时器按钮的隐藏、命名与查找

旧 `OverlayHost`、`SettingsPanel`、`AppPicker`、`AdvancedPane`、`HudRenderer` 已移除。
