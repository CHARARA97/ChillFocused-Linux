# 游戏计时器 API 对照

这份文档记录插件实际调用的游戏方法，以及游戏自身 UI 的官方调用路径。
用途是避免以后继续用“看起来能用”的反射拼接方法。

## 官方启动路径

### 正向计时 / Countup

游戏 UI 的调用链：

```text
TimerParentUI.ChangeCountupTimer()
  → 切换番茄钟/正向计时的父级 UI
  → 写入 PomodoroData.CurrentTimerMode = Countup

CountupService.StartCountup()
  → TimerCoreService.StartTimer()
  → CountupService.PlayCountupTimer()
  → CountupTimerService.PlayTimer()
```

`FacilityCountup.Setup` 只订阅 UI 按钮：

```text
OnClickButtonStartCountup
  → CountupService.StartCountup()
```

它不会自动切换 `TimerParentUI`，所以插件必须补上
`TimerParentUI.ChangeCountupTimer()`。

### 番茄钟 / Pomodoro

游戏 UI 的调用链：

```text
TimerParentUI.ChangePomodoroTimer()
  → 切换父级 UI
  → 写入 PomodoroData.CurrentTimerMode = Pomodoro

PomodoroService.SetWorkMinutes(...)
PomodoroService.SetBreakMinutes(...)
PomodoroService.SetLoopCount(...)
PomodoroService.StartPomodoro()
  → TimerCoreService.StartTimer()
  → PomodoroService.PlayPomodoroTimer(Work)
```

`PomodoroService.StartPomodoro()` 会把当前轮次重置为 1；
`SetLoopCount(...)` 设置的是预设轮数，不是当前轮次。

## 官方结束路径

```text
PomodoroService.ResetTimer()          // 无参数
CountupService.ResetTimer(true)       // 参数是用户操作标记
```

游戏自己的 `FacilityCountup` 重置按钮调用的是：

```text
CountupService.ResetTimer(true)
```

因此插件对 Countup 也传 `true`，而不是自己猜 `false`。

## 插件组合后的调用顺序

```text
启动 Countup:
  Reset 旧 Pomodoro
  TimerParentUI.ChangeCountupTimer()
  CountupService.StartCountup()

启动 Pomodoro:
  Reset 旧 Countup
  PomodoroService.SetWorkMinutes / SetBreakMinutes / SetLoopCount
  TimerParentUI.ChangePomodoroTimer()
  PomodoroService.StartPomodoro()

结束:
  Pomodoro: PomodoroService.ResetTimer()
  Countup:  CountupService.ResetTimer(true)
```

## 哪些不是官方单一接口

游戏没有：

```text
StartExternalSession(plan)
StopExternalSession()
```

这种统一入口。插件必须组合：

```text
TimerParentUI + 对应 Service 的 Start/Set/Reset
```

但每个原子动作都使用游戏官方方法；插件不做字段级状态拼接。

## 服务实例来源

优先顺序：

```text
1. GameTimerProbe 的 Harmony 实例缓存
2. ProjectLifetimeScope.Resolve<T>()（官方 DI）
3. FacilityPomodoro / FacilityCountup 的注入字段
```

`TimerParentUI` 通过 `FacilityPomodoro._timerParentUI` 捕获，
并有 `FindObjectOfType` 兜底。

## 时间校正接口

游戏公开了两个低层时钟 setter：

```text
CountupTimerService.SetElapsedTime(TimeSpan)
PomodoroTimerService.SetRemainingTime(TimeSpan)
```

它们不是游戏 UI 的常规路径；游戏 UI 的移动端恢复使用：

```text
CountupService.MoveAheadTimer(float seconds)
PomodoroService.MoveAheadTimer(float seconds)
```

`MoveAheadTimer` 只接受正数，适合追赶，不适合双向校正。插件使用两个 setter
做外部 → 游戏的绝对时间校正，默认每秒一次，配置项：

```ini
[6. Session]
MirrorTimeSyncSeconds = 1
```

`0` 表示禁用周期校正，只在开始/结束处理。校正对象：

```text
Countup  → SetElapsedTime(外部已进行时间)
Pomodoro → SetRemainingTime(外部剩余时间)
```

低层 setter 不触发高层完成事件，也不会主动改存档统计；UI 在下一帧读取新值。
