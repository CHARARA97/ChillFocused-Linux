# 它和 Focused 之间说什么

这份插件**不提供** HTTP 服务，它是 **Focused 后端的客户端**。它做三件事：

| 时机 | 请求 | 说明 |
|---|---|---|
| 启动后 / 规则变化 / 每 30 秒 | `PUT /api/v1/focus/rules` | 提交自己的名单与追加保护项。Focused 按来源分别保存并取**并集**，所以本插件永远覆盖不了别人的名单 |
| 每 2 秒 | `GET /api/v1/focus` | 读扁平状态：`active` / `delegated_source` / `frozen_count` / `frozen_names` / `version` / `pid` … |
| 同一循环 | `POST /api/v1/focus/state` | 交会话心跳：`{"active":true,"dry_run":false,"source":"game-timer","ttl_seconds":30}` |

**心跳里的 `ttl_seconds` 是安全关键**：插件停止续期（崩溃、被杀、被系统冻住）后，后端会把租约到期当成"专注结束"，
自动恢复全部被挂起的应用。没有它，插件一死用户的应用就永远挂着。

字段为什么必须是**扁平的标量或基础类型数组**：Unity 的 `JsonUtility` 只绑定 public 字段与基础数组，
嵌套对象会静默变成 `null`。后端因此专门保持 `/api/v1/focus` 扁平 —— 这个坑曾经让面板上的复选框卡住一整个版本。

完整契约（所有端点、鉴权、事件流、插件注册）见后端仓库：
**https://github.com/CHARARA97/Focused/blob/main/docs/focused-protocol.md**
