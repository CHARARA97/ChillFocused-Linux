# 原版 SettingUI 实测数据

运行时通过 `SettingUI` 实例反射读取，2026-10-18：

```text
ActiveColor    = RGBA(255,255,255,255)
DeactiveColor  = RGBA(110,110,147,255)   #6E6E93
DisabledAlpha  = 0.3

CanvasGroup UI_FacilitySetting.alpha = 0   # 关闭状态
_rectTransform.rect = 1210 x 891
_rectTransform.anchoredPosition = (0, 86.4)
_fromPosY = 86.4
_toPosY   = 94.4
```

## 动画

```text
Activate:
  DOAnchorPosY(toY, 0.2s)
  CanvasGroup.DOFade(1, 0.2s)

Deactivate:
  DOAnchorPosY(fromY, 0.2s)
  CanvasGroup.DOFade(0, 0.2s)
  OnComplete -> SetActive(false)
```

即 0.2s、轻微上移 8px、OutQuad 默认缓动。没有全屏遮罩。

## 精灵与文字

```text
BackGroundImage : t_ui_back, 1210 x 906, type=Tiled, color=white
BackLine        : t_ui_back_bar, 1210 x 4
Tab base        : t_ui_settings_tab, height 52, alpha 0.659
Tab active      : t_ui_settings_tab_active, alpha 1
Title           : 36
Tab label       : 24
Section title   : 42
Row title       : 26
```

Tab 位置（原版 6 个 Tab）示例：

```text
General  (100.0, -52)
Graphic  (307.2, -52)
Audio    (514.4, -52)
...
```

## 插件对齐

原版参数已用于 `SettingsPageInjector` 的克隆页和行模板：

```text
1210 x 906 panel
fromY - 8px -> home
0.2s OutQuad fade + slide
selected tab: #2C2946 + active sprite
cyan #07ACC0 / magenta #C10A90 border
DeactiveColor #6E6E93
no full-screen dimmer
```

## 行按钮

原版 OnButton/OffButton 实测（实机截图像素采样）：

```text
填充      #2C2946 (44,41,70)，圆角，自带渐变
尺寸      184 x 38（应用行与信息行一致）
Using     叠加青->洋红渐变描边        ActivateUseUI
普通      仅填充，无描边              DeactivateUseUI
悬停      由 InteractableUI 自己 tween，不要手改
```

插件只克隆这个按钮，状态交给 `NativeInteractable`，**不替换 sprite/color**：换成白色
sprite 会丢掉圆角与渐变，按钮变成纯色矩形。

Using 高亮留给“进入子页面/做选择”的按钮（`选择要暂停的应用`）；一次性动作
（返回、刷新、暂停、恢复）用普通态，别让高亮失去含义。

## 原版组件层级

```text
UI_FacilitySetting
├─ BackGroundImage       Image  t_ui_back        1210×906  Tiled
├─ BackLine              Image  t_ui_back_bar    1210×4    Sliced
├─ SelectSettingButtons
│  ├─ SettingTabButton General
│  │  ├─ MouseOverImage  Image  t_ui_settings_tab_select  alpha 0
│  │  ├─ ActiveImage     Image  t_ui_settings_tab_active   alpha 1
│  │  └─ Text (TMP)       24px, white
│  └─ ...
├─ General
│  ├─ Title              42px
│  ├─ BackLine
│  └─ ScrollView
```

Tab 不是普通 Button，而是：

```text
InteractableUI + Button
  BaseImage
  MouseOverImage
  ActiveImage
```

因此自绘 `Image + Button` 不可能完全复刻悬停/选中效果；正确实现应复制
`SettingTabButton General` 的 GameObject 和 `InteractableUI`，再挂自己的点击逻辑。
