# 原版 SettingUI 注入

实现参考 ChillClock 的 `SettingsPageInjector`：

1. 找到 `SettingUI`；
2. 克隆 `_creditsInteractableUI`，追加一个独立页签；
3. 克隆 `_generalParent` 作为新页面；
4. 清空克隆页的 ScrollView；
5. 用原版行模板填充 `ChillFocus` 内容；
6. 原版 Tab 点击时隐藏注入页，注入 Tab 点击时隐藏原版页面并显示注入页。

原版 Credits 和原有六个页签不修改、不删除。

代码：

- `Core/UI/Unity/Native/SettingsPageInjector.cs`

验证：

- 设置页显示 `常规/图形/音频/账号/公告/制作人员/ChillFocus`；
- 点击 `制作人员` 仍能打开原版制作人员页；
- 点击 `ChillFocus` 显示注入页；
- 切回原版 Tab 正常隐藏注入页。
