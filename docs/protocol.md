# ChillFocused ⇄ Focused ⇄ 浏览器扩展 协议

这份文档定义三端之间**唯一的**通信契约：

| 角色 | 现在在哪 | 负责什么 |
|---|---|---|
| **ChillFocused 插件**（BepInEx mod） | 游戏进程内 | 读游戏计时、自己判定会话、把规则与心跳推给 Focused、显示面板/HUD |
| **Focused**（后端应用） | ✅ 已实现，见 `focused/` 与 [focused.md](../../focused/docs/focused.md) | **本体**：查找 + 冻结、会话与租约、看板、插件平台；两个前端都直连它 |
| **Chrome / Vivaldi 扩展** | 浏览器内 | 专注模式下按 URL 黑名单**关闭标签页** |

所有调用都是 **loopback HTTP + JSON**，端口可配置，不需要 root，不需要 D-Bus。

> **实现状态**：Focused 已经按这份契约实现（`focused/focused/server.py`），ChillFocus 的委派客户端
> 用真实 HTTP 与它对接，端到端测试跑的是**两个真实进程 + 一个真被冻结的目标进程**
> （`focused/tests/test_delegation_e2e.py`）；浏览器扩展则用真实响应形状的 fixture 验证解析
> （`focused-extension/test/daemon-contract.test.mjs`）。

---

## 1. 浏览器侧：`GET /api/v1/focus`

由**守护进程或 Focused**提供，浏览器扩展每 `poll_seconds`（默认 5 秒）拉一次。

```http
GET <base>/api/v1/focus
X-Focused-Token: <token>        # 仅在配置了 token 时发送
```

```jsonc
200 OK
{
  "ok": true,
  "active": true,                 // 专注模式是否正在运行
  "since": 1791012345.6,          // 本次专注开始的 epoch 秒（0 = 未知）
  "pause_until": 0,               // > now 时表示"临时放行"，浏览器必须停手
  "paused": false,                // pause_until > now 的冗余表达，便于 UI 显示
  "url_patterns": ["reddit.com", "*://*.twitter.com/*"],
  "delegate": false,              // true = 执行权已交给 Focused
  "delegate_ok": false,           // 与 Focused 的通信是否正常
  "version": "0.1.0"
}
```

> **浏览器黑名单归客户端所有**：扩展的设置页里有自己的 `extra_patterns`，与这里下发的
> `url_patterns` **取并集**（服务器在前、本地在后，大小写不敏感去重），白名单（`whitelist`）
> 优先于两者。因此「仅在浏览器中维护名单」与「由 Focused 统一下发」两种用法均可工作；
> 游戏插件不参与这份名单。

**匹配规则**（两侧实现必须一致；Python 见 `focused/focused/core/urlfilter.py`，JS 见
`focused-extension/src/logic.js`）：

1. 大小写不敏感；
2. 含 `*` 的模式 → 对**整个 URL** 做 glob，`*` 匹配任意字符（含 `/` 与 `:`）；
3. 不含 `*` 的模式 → **子串**匹配（人们写的就是 `reddit.com`）；
4. 空列表不匹配任何东西。

**客户端的强制要求**（安全相关）：

- `ok != true`、非 200、超时、JSON 坏掉、网络不可达 → **一律视为未激活**，绝不关标签页；
- 只处理 `http://` / `https://`；`chrome://`、`file://`、`about:` 一律不碰；
- `pause_until > now` → 什么都不做（这是"我就看一眼"的放行机制）。

**临时放行**：

```http
POST /api/v1/focus/pause      {"seconds": 300}
→ 200 {"ok": true, "pause_until": 1791012645.6}
```

---

## 2. 委派：ChillFocus 把执行权交给 Focused

开关（`config.json`，默认关闭）：

```jsonc
"delegate": {
  "enabled": false,
  "base_url": "http://127.0.0.1:8766",   // 必须是回环地址（配置校验会拒绝其它主机）
  "token": "",
  "timeout_ms": 1500,
  "freeze": true                          // 让 Focused 冻结而不是关闭
}
```

开启后，守护进程**不再自己动手**（不冻结、不关闭；`stats.delegated` 记录它看见了但让出去的次数），
只做三件事：

### 2.1 推规则

```http
PUT /api/v1/focus/rules
{"names": ["firefox"], "cmdline_substrings": [], "protect_names": ["systemd", ...],
 "protect_cmdline_substrings": [], "freeze": true}
→ 200 {"ok": true}
```

在**规则变化时**推送（插件推新规则、配置热重载、进程启动时各一次）。

> `protect_*` 是**并集**语义：Focused 必须把它与自己的内置护栏合并，**绝不能**因为收到一份空列表
> 就丢掉"不要冻结桌面会话/终端/合成器"这些底线。内置清单见 `focused/focused/core/protect.py` 与
> 文档 §4.1 的硬护栏。

### 2.2 推专注状态

```http
POST /api/v1/focus/state
{"active": true, "dry_run": false, "source": "game-timer"}
→ 200 {"ok": true}
```

**状态变化时**推送。`active=false` 是"立刻把冻住的都解冻"的信号（就等于"退出创作模式"）。

### 2.3 回读冻结结果

```http
GET /api/v1/focus/frozen
→ 200 {"ok": true,
       "frozen": [{"name": "firefox", "pid": 4242, "mode": "unit",
                   "unit": "app-firefox-1.scope", "since": 1791012347.1,
                   "reason": "blacklist.name:firefox"}]}
```

守护进程每一轮扫描都回读一次，并镜像进自己的 `/status`（`delegate_frozen_count` /
`delegate_frozen_names`），于是**游戏内面板仍然显示真相**，即使执行的是 Focused。
`ok=false` 或不可达 → `delegate_ok=false`，守护进程照常运行（绝不因为 Focused 挂了而卡住）。

### 2.4 交接语义（必须遵守）

| 事件 | 要求 |
|---|---|
| 打开委派 | ChillFocus **先解冻自己冻过的一切**（避免两个引擎同时持有同一个进程）；然后 Focused 接管 |
| 关闭委派 | ChillFocus 推送 `{"active": false}`，然后自己重新接管 |
| Focused 崩溃 | 它**必须**在下次启动时解冻自己记录的所有目标（对照 `docs/freeze-mode.md` F1/F2） |
| Focused 被 SIGKILL | 建议用 systemd 的 `ExecStopPost=` 跑一次自己的 `--thaw-all`（对照 F3） |

---

## 3. 冻结实现的最低要求（给 Focused 的清单）

早期前端实现遇到过以下问题，Focused 实现时可直接规避：

1. **落盘再动手**：冻结前先把"我冻了谁"写进状态文件；崩溃后靠它解冻（`frozen.json`）。
2. **PID 复用防护**：解冻前必须核对 `starttime`（`/proc/<pid>/stat` 第 22 字段）。PID 可能已经被
   别的进程复用，误发 `SIGCONT` 等于对陌生人发信号。**每个**子进程都要核对，不只是根进程。
3. **整单元优先**：能用 cgroup freezer 就用（`systemctl --user freeze <unit>`，或直接写
   `/sys/fs/cgroup/<path>/cgroup.freeze`）；它连"冻结之后新 fork 的子进程"一起冻住，这对浏览器很关键。
   判定"这个 unit 是否专属单个应用"见 `focused/focused/core/cgroupfs.py`（保守：只接受 `app.slice/app-*.scope`）。
4. **确认生效**：冻结是异步的，且**被 cgroup 冻结的进程在 `/proc/<pid>/status` 里仍显示 `S`**；
   唯一可靠的确认是 `cgroup.events` 里的 `frozen 1`。
5. **绝不冻结自己所在的 cgroup**：那会冻住执行者，没人再解冻。降级为"只冻进程树"。
6. **保护名单优先**：被保护的进程既不冻结也不关闭。
7. **绝不按进程组发信号**（`killpg`）：解冻按 PID + starttime 或按 cgroup。

---

## 4. 错误处理与兼容性

- 所有端点：成功 `{"ok": true, ...}`；失败时返回非 200 且 `{"ok": false, "error": "..."}`。
- 鉴权：可选共享 token，经 `X-Focused-Token` 头传递（与插件、扩展一致）。
- 未知字段必须被忽略（前后端可以各自升级）；**已知字段的语义不能变**。
- 版本兼容：请求里带 `version`（ChillFocus 侧）；Focused 不认识的版本应回 `ok=false` 并说明，
  而不是猜。

---

## 5. 现状（2026-10-03）

| 能力 | 状态 |
|---|---|
| Focused 的 `GET /api/v1/focus` + `POST /api/v1/focus/pause` | ✅ 已实现并测试（`focused/tests/test_focused_protocol.py`） |
| 跨语言契约（插件 DTO 字段 ⊆ Focused 实际响应） | ✅ 自动校验（`focused/tests/test_plugin_contract.py`） |
| 浏览器扩展 | ✅ 已实现（`focused-extension/`，32 项 node 测试），端点可指向守护进程或 Focused |
| **Focused 应用本身** | ✅ 已实现：`focused/`（引擎 + 会话 + 看板 + 协议 + systemd unit），端到端测试见 `focused/tests/test_delegation_e2e.py` |
