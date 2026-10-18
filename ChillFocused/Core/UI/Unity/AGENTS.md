# UI Unity

只保留原生 SettingUI 注入，按职责分层：

```text
SettingUiReflection      游戏字段/Activate 的唯一反射边界
NativePageCloner         原生 Tab/Page 克隆与 ScrollRect 配置
ChillFocusTabHost        克隆后的 Tab/Page 生命周期、Show/Hide
NativeTabHook            原版 Tab 点击恢复原页面
NativeInteractable       原版 InteractableUI 状态桥
NativePageLayout         行宽/图标尺寸/分隔线常量
NativeRowFactory         原生控件：行/分隔线/图标
NativeActionButton       应用行/操作按钮：底板、文字、右对齐、点击
NativeToggleRow          On/Off Toggle 行状态
AppRowView               单个应用行绑定
MainPageView             主页面布局
ProcessPageView          进程页布局
ProcessListModel         进程请求
BlockedAppsModel         已暂停名单读写
ProcessIconView          名称 -> Sprite
ProcessIconCache         base64 PNG 解码
ChillFocusPageActions    视图可调用的业务动作
ChillFocusCopy           用户可见文案
SettingsPageInjector     仅连接 Host + 选择页面
TimerControlGuard        计时器锁门面：按计划隐藏/还原按钮
TimerControlNames        计时器按钮对象名（游戏改版只改这里）
TimerButtonLookup        按名字找活动对象 + 快照缓存 + 诊断候选
```

修改原则：

- 反射只在 `SettingUiReflection`；
- 克隆只在 `NativePageCloner`；
- 按钮视觉状态只走 `NativeInteractable`，不要手改 Image/tween；
- 行布局只在 `AppRowView` / `NativeActionButton`；
- 文案只在 `ChillFocusCopy`；
- 主控制器不要重新加入数据/控件/反射逻辑；
- 计时器按钮的查找/命名/隐藏分别只在 `TimerButtonLookup` / `TimerControlNames` / `TimerControlGuard`。

旧 IMGUI 面板、HUD、toast、SettingsPanel/AppPicker/AdvancedPane 已移除。
