# 提交到 awesome-chillwithyou（PR 条目草稿）

这个列表是《放松时光：与你共享 Lo-Fi 故事》Mod 的实际入口，条目按**名称大致字母序**排列，
模板固定为：仓库（带 star 徽章）/ 简介 / 许可 / 本地化 / **系统支持（Windows、Linux、macOS 三档）** / 特性列表。

- 仓库：<https://github.com/clsty/awesome-chillwithyou>
- 现有条目顺序：`ChillPatcher` → `Chill AI Mod` → `Chill Env Sync` → `Chill Music Information Sync`
  → `Chill With Anyone` → `iGPU Savior`
- **我们的插入位置**：`Chill With Anyone` 之后、`iGPU Savior` 之前（`ChillFocused` 按字母序在
  `Chill With Anyone` 与 `iGPU` 之间）

> PR 前请把下面两处占位替换掉：`<owner>` → 你的 GitHub 用户名，`v0.1.0`（如果发布时改了版本）。

---

## 要粘贴的条目（照抄，含空行）

```markdown
## ChillFocused（专注冻结）

- **仓库**：[![<owner>/chillfocus](https://img.shields.io/github/stars/<owner>/chillfocus?label=<owner>%2Fchillfocus&style=flat-square)](https://github.com/<owner>/chillfocus)
- **简介**：把黑名单里的应用在专注期间**挂起（冻结）**而不是关闭，计时结束原样恢复。由游戏内的番茄钟/正计时驱动；包含三个部分：游戏插件（本条目）、宿主侧后端应用 Focused、以及可选的浏览器扩展。
- **许可**：MIT
- **本地化**：界面三语言（简体中文 / English / 日本語），跟随游戏内的语言设置。文档：中、英。
- **系统支持**：Windows（不支持 —— 后端依赖 Linux 的 `/proc`、cgroup v2、`SIGSTOP`，只有插件能加载）；Linux（**支持**，Steam / Proton 实测）；MacOS（未知）
- **特性列举**：
  - 游戏计时器一响即挂起名单内的应用（`SIGSTOP` / cgroup freezer），休息阶段自动恢复，**不丢未保存内容**
  - 面板里从运行中的进程点选名单，支持通配符与按启动参数匹配
  - 内置 450+ 条保护名单（合成器、终端、输入法、wine、游戏本体…），用户只能追加
  - 面板布局是纯文本文件，改完保存即生效，无需重启游戏
  - 「只记录，不冻结」演练模式：先确认会命中谁
  - 崩溃兜底：插件停止心跳 → 后端租约到期 → 自动全部恢复；后端被杀也有 `--thaw-all` 与 `systemctl --user` 钩子
  - 自带 `focused-doctor` 自检：逐项检查 BepInEx、winhttp override、重复 DLL、后端连通性与令牌
```

## PR 步骤

```bash
# 1. fork 并克隆
gh repo fork clsty/awesome-chillwithyou --clone && cd awesome-chillwithyou

# 2. 在 README.md 中找到 "## Chill With Anyone" 一节，把上面的条目插在它后面
#    （即 "## iGPU Savior" 之前），保持前后各一个空行

# 3. 也可以看一眼英文版 README.en.md 的对应位置，若结构一致就一并补上英文条目
git checkout -b add-chillfocused

git add README.md README.en.md
git commit -m "Add ChillFocused"
gh pr create --title "Add ChillFocused" --body "新增条目：ChillFocused（专注冻结）。
专注期间把黑名单应用挂起而不是关闭，由游戏内计时驱动；Linux/Proton 实测支持。"
```

## 提交前自检

- [ ] 仓库公开、有 `LICENSE`，且 Release 里有可下载的资产（列表方会看这些）
- [ ] README 里写清了 BepInEx 版本（5.x，不要 6.0）与 winhttp override 步骤
- [ ] **系统支持一栏诚实**：Windows 标"不支持"而不是"未知"——后端没有 Windows 实现，
      标支持会把用户引到死路上；这份诚实反而是我们的差异点（列表里多数 Mod 是 Windows 支持 / Linux 未知）
- [ ] 简介里说明"需要宿主侧后端"，避免别人只装了插件以为就能用
- [ ] 徽章里的 star 数会自动更新；如果仓库还没发布过 Release，徽章也能显示，只是数值为 0

## 相关：其他两个组件的露出

| 组件 | 渠道 |
|---|---|
| 后端 Focused | AUR（`focused` / `focused-git`）+ PyPI（可选）+ GitHub Release 的 wheel 与 `install.sh`；不在这个列表里（它不是游戏 Mod） |
| 浏览器扩展 | Chrome Web Store（主）+ Release zip 加载已解压目录 |
