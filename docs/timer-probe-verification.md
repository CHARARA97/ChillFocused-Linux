# Timer Probe 真机验证（C-05）

代码侧已完成：

- `ITimerSource` / `GameTimerSource` / `NullTimerSource`
- `TimerStateReader` 模式解析
- F6 `PluginDebugSnapshot` 增加：
  `timer_working`、`timer_running`、`timer_resting`、`timer_phase`、`timer_source`

但 Harmony/Proton 下的服务名和方法名只能真机验证。

## 准备

```bash
cd chillfocus
scripts/build.sh
scripts/install-bepinex.sh
```

启动：

- 游戏（Proton）
- 后端 `focused`
- 确认插件配置 `FocusedUrl=http://127.0.0.1:8766`

日志：

```text
<游戏目录>/BepInEx/LogOutput.log
```

快捷键：

```text
F6  重载 UI + 打印 PluginDebugSnapshot
F7  设置面板
F9  HUD 模式
```

## 最短验证

每个场景先按 F6，记录完整 `debug ...` 行。

| # | 场景 | 期望 |
|---|---|---|
| 1 | 启动未开始 | `timer_known=true`，`session=false` |
| 2 | Pomodoro Work | `timer_mode=Pomodoro`，`timer_phase=Work`，`timer_working=true`，`timer_running=true`，`session=true` |
| 3 | Pomodoro 暂停 | `timer_phase=Work`，`timer_running=false`，`session=false` |
| 4 | Pomodoro Break | `timer_phase=Break`，`session=false` |
| 5 | Pomodoro Complete | `timer_phase=Complete`，`session=false` |
| 6 | Countup 运行 | `timer_mode=Countup`，`timer_phase=-`，`timer_running=true`，`session=true` |
| 7 | Countup 暂停 | `timer_mode=Countup`，`timer_running=false`，`session=false` |

重点失败：

```text
timer_known=false
  → 服务未解析/Harmony 未命中

timer_phase=-
  → CurrentPomodoroType 未读到

countup run=n/a
  → IsTimerRunning/IsRunning/IsPlaying 都未命中
```

## 日志证据

需要保留：

```text
timer probe: hooked ...
timer probe: first ...
timer probe: state changed ...
event=plugin.sync.start
event=plugin.sync.connected
event=plugin.sync.rules
event=plugin.session.change
event=plugin.debug ...
```

## 后端/重启场景

| 场景 | 期望 |
|---|---|
| 停止 Focused 超过 lease | 插件不本地冻结；应用恢复 |
| 重启 Focused | 插件重连，规则重新推送 |
| 场景切换 | overlay 重建，F6 仍可用 |

## 回传内容

- 每个场景的 F6 debug 行；
- 对应时间段 LogOutput 片段；
- 游戏版本 / Proton 版本；
- 失败场景的后端 `active` 与应用冻结状态。

不需要回传 token 或存档。

## 验证记录（2026-10-18）

已用当前 DLL + 游戏内实际操作验证：

| 场景 | 实测结果 | 结论 |
|---|---|---|
| Pomodoro Work | `timer run=true`、`phase=Work`、`session=on` | ✅ |
| Pomodoro Pause | `timer run=false`、`state changed running=False`、`session=off` | ✅ |
| Pomodoro Break | `phase=Break`、`core rest=true`、`session=off` | ✅ |
| Pomodoro Complete | `phase=Complete`、`session=off` | ✅ |
| Countup Run | `CountupService`、`countup run=true work=true`、`session=on` | ✅ |
| Countup Pause | `countup run=true work=false` → `running=False`、`session=off` | ✅ |

验证中确认的游戏 API：

```text
PomodoroTimerService._isRunning       实际 play/pause 位
PomodoroService.IsTimerRunning        暂停后仍可能为 true
CountupService.IsCurrentWorking       实际 work/pause 位
CountupService.IsTimerRunning         暂停后仍可能为 true
TimerCoreService.IsTimerRunning       暂停后仍可能为 true
```

因此当前实现：

```text
Pomodoro 运行状态 = PomodoroTimerService._isRunning
Countup 运行状态  = CountupService.IsCurrentWorking（没有底层字段时）
```

仍未做的可选验证：

- 后端停止超过 lease 后，插件不本地冻结、应用恢复；
- 后端重启后，插件重连并重推 rules；
- 场景切换后 overlay 重建。
