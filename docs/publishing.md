# 首次发布指南（GitHub + 游戏插件）

> **发行形态是三个独立仓库**（`focused` / `chillfocused` / `focused-extension`）。
> 仓库怎么分、内容映射、三个仓库各自的首次发布命令、以及发布后的同步流程，
> 见 **[repo-split.md](repo-split.md)**。本文讲的是与平台有关的规则与红线，三个仓库通用。

> 面向"**第一次在 GitHub 发东西、第一次发游戏 Mod**"。每一步都给可复制的命令，以及
> "平台会因此拒绝你"的具体红线（红线部分来自平台官方规则，文末列了出处）。
>
> 本仓库已经按这份清单预置好了绝大部分东西；下文标 ✅ 的是**已经就绪**，标 ⬜ 的是**要你决定或动手**的。

---

## 0. 先做一次仓库体检（我已实测）

| 项 | 结果 |
|---|---|
| 会被提交的文件 | **142 个**，最大的文件 64 KB（`focused/focused/engine.py`）→ 仓库体积只有几 MB |
| 敏感内容 | ✅ 无 token/密钥；✅ 已无作者机器路径（发现 1 处并已改成 `/home/user/...`） |
| **绝不能提交的东西** | ✅ `.gitignore` 已排除 `.backup/`（里面是**你本机的真实配置**：`~/.config/chillfocus/config.json`、游戏的 `com.chillfocused.*.cfg`）、`.build/`、`dist/`、`third_party/`、`.tmp/`、`obj/`、`bin/` |
| 第三方二进制 | ✅ 不提交 BepInEx 的 zip 与游戏 DLL（`dist/`、`third_party/` 都在忽略列表里） |

体检命令（随时可自查）：

```bash
git init -q && git add -An . | wc -l          # 看会被提交的文件数
git status --short | head                     # 看有没有意外文件
grep -rIl "$HOME" --exclude-dir=.git . | head  # 看有没有泄露本机路径
```

---

## 1. GitHub 首次发布

### 1.1 一次性准备

```bash
# 身份（会写进每个 commit，公开可见）
git config --global user.name  "你的名字"
git config --global user.email "你的邮箱"     # 建议用 GitHub 的 noreply 邮箱防垃圾邮件

# 认证：SSH 或 gh CLI 二选一
#  A) gh CLI（推荐，后面发 Release 也要用它）
sudo pacman -S github-cli && gh auth login     # 选 GitHub.com → SSH → 浏览器登录
#  B) 纯 SSH
ssh-keygen -t ed25519 -C "你的邮箱"
cat ~/.ssh/id_ed25519.pub                     # 贴到 GitHub → Settings → SSH keys
```

### 1.2 建仓库并首次推送

**先在 GitHub 网页上建空仓库**（不要勾 "Add README"、"Add .gitignore"、**不要选 License**，
否则首次 `git push` 会冲突）。建议：

- 名字：`chillfocus`
- 描述：`Suspend distracting apps while you focus — backend + game plugin + browser extension for Chill with You : Lo-Fi Story`
- 可见性：**Public**（私有仓库别人装不了，也不该收 AUR/列表收录）
- Topics：`bepinex` `unity-mod` `linux` `proton` `focus` `pomodoro` `steam`

```bash
cd /path/to/chillfocus
git init -b main
git add .
git status --short | head -20          # 最后确认一遍没有私密文件
git commit -m "Initial release: Focused backend, ChillFocused game plugin, browser extension"
git remote add origin git@github.com:<owner>/chillfocus.git
git push -u origin main
```

> **首次提交的粒度**：一次大提交是可以接受的（"Initial release"），但从第二次起请一功能一提交，
> 别人 review issue 时能看懂历史。**不要**把 `.backup/` 里的阶段快照塞进来。

### 1.3 打标签 + 发 Release（插件就是这么发出去的）

```bash
# 1) 先确保版本号唯一来源正确：plugin/ChillFocused/ChillFocused.csproj 的 <Version>
#    测试会替你守住它与源码常量一致（VersionConsistencyTests）
scripts/package-mod.sh --offline            # 产出 dist/mod/ChillFocused.dll + .sha256

# 2) 打 tag（每个仓库各自用自己的 vX.Y.Z，不再需要组件前缀）
git tag -a v0.1.0 -m "ChillFocused 0.1.0"
git push origin v0.1.0

# 3) 建 Release：资产就是那一（两）个文件
gh release create v0.1.0 \
   dist/mod/ChillFocused.dll dist/mod/ChillFocused.dll.sha256 \
   --title "ChillFocused 0.1.0" \
   --notes-file plugin/ChillFocused/CHANGELOG.md      # 没有就先 --generate-notes
```

Release 说明建议包含：**这版做了什么 / 需要什么（BepInEx 5.4.23.5、Linux+Proton）/ 怎么装（三步）/
校验和 / 已知问题**。别人 90% 的 issue 都能被这三行挡掉。

### 1.4 仓库设置（点几下，很值）

| 设置 | 位置 | 为什么 |
|---|---|---|
| Description + Topics | 仓库首页右侧 ⚙ | 被人搜到的唯一机会 |
| Actions 权限 | Settings → Actions → General | 若要 CI 发 Release，把 `Workflow permissions` 设为 **Read and write** |
| Issues 开启 | Settings → Features | 收 bug 报告（模板见 §5） |
| 分支保护（可选） | Settings → Branches | 单人项目可跳过 |
| Releases 页面 | 自动 | 用户下载入口，README 里链接指向它 |

### 1.5 许可证 ⬜（发布前必须定）

- 现在仓库**没有 `LICENSE`**，GitHub 会显示 "No license"，别人默认**不能**合法复用你的代码。
- 我们的推荐：**MIT**（最省事、生态最常见；ChillClock 也是 MIT，ChillPatcher 是 GPLv3）。
- 加文件：仓库根目录放 `LICENSE`（GitHub 网页 → Add file → Create new file → 输入 `LICENSE` 会自动给模板）。
- ⚠️ AUR 的**包源码**（`PKGBUILD` 等）按 Arch 规范要 **0BSD**，与上游许可无关，两者别混。

---

## 2. 发游戏插件：平台规则与红线

### 2.1 渠道现状（针对这个游戏，都已实测）

| 平台 | 能不能用 | 说明 |
|---|---|---|
| **GitHub Releases** | ✅ 主渠道 | 你已经在用；玩家习惯"下载一个 DLL 丢进 `BepInEx/plugins/`" |
| **awesome-chillwithyou** | ✅ 建议 | 这个游戏的 Mod 索引；草稿见 [awesome-entry.md](awesome-entry.md)，PR 即可 |
| **Nexus Mods** | ⚠️ 可用但有额外要求 | Windows 玩家生态；本插件实际只服务 Linux（后端无 Windows 实现）→ 收录价值低，且见 §2.2 的联网与 AI 条款 |
| **Thunderstore / r2modman** | ❌ 暂不可用 | 该游戏**没有社区**（实测），且清单无法携带"宿主侧后端"这个依赖 |
| **Steam 创意工坊** | ❌ 不适用 | BepInEx 是文件注入，不是游戏提供的 Mod 接口 |

### 2.2 Nexus Mods 的硬性条款（官方 File Submission Guidelines，我逐条比对过）

| 条款 | 对我们的影响 |
|---|---|
| **不得包含受版权保护的内容（含游戏文件、图像）** | ✅ 我们不分发任何游戏文件；插件只编译期引用游戏程序集（`Private=false`） |
| **文件必须可用、不得是占位；描述必须真实，不得夸大** | ✅ 但要避免写"提升性能/FPS"这类无法证明的话 |
| **联网发送/接收信息的文件原则上禁止，"自动更新"不算正当理由；若功能必需需先联系 staff 并提供源代码** | ⚠️ **我们的插件会连 `127.0.0.1`**（回环，不是互联网）——这是它**功能必需**的部分。若要在 Nexus 发布，必须在描述里明确写："只与本机回环地址通信，无遥测、无自动更新、无外部服务器"，并说明后端是另一个开源程序 |
| **生成式 AI 使用必须打标签**：`AI-Generated Content`（AI 生成代码/界面/语音…）、`AI Media`（宣传图/描述）、`AI Assisted`（人类主导、AI 辅助） | ⚠️ **必须诚实申报**。这个项目的代码有大量 AI 参与；`AI Assisted` 标签要求你能解释代码功能、有开发历史证据（我们恰好有：git 历史、测试、文档）→ 按实际填写；**若被标记为 AI-Generated 而你没有相应证据，审核会改标签** |
| 不得用文件/图片骚扰第三方；不得广告其他站点 | ✅ |
| 分类与标签必须准确 | 发布时选"Utilities / Gameplay"之类，别乱挂 |
| 仅 PC 平台；不得含破解主机内容 | ✅ |

> **我的建议**：先发 GitHub + awesome 列表。Nexus 等到"有 Windows 用户来问"再考虑 —— 那时你要么补一个 Windows 后端，
> 要么在描述里写清"仅 Linux"，并且**把 AI 申报与回环通信说明准备好**。

### 2.3 一个 Mod 的"完整交付物"清单

| 项 | 状态 |
|---|---|
| 一个 DLL（Release 资产）+ sha256 | ✅ `dist/mod/` |
| README（安装 / 卸载 / 配置 / FAQ / 系统支持 / 兼容性） | ✅ 中英双语，12 节 |
| 许可证 + 第三方组件说明 | ✅ 中英 README 里已有表格；⬜ 根目录 `LICENSE` 待你定 |
| 版本号唯一来源 + changelog | ✅ 版本有测试守护；⬜ `CHANGELOG.md` 还没建（Release notes 可先 `--generate-notes`） |
| 自检命令（用户报障时先跑它） | ✅ `focused-doctor` |
| 卸载路径 | ✅ README 已写 |
| 崩溃兜底说明（会让审核放心） | ✅ README/后端文档都写了租约 + `--thaw-all` |
| 截图/GIF | ⬜ 需要你在游戏里截 1–2 张（README 里留了占位） |

---

## 3. 法务与许可要点（首次发布最容易踩的）

1. **不要打包 BepInEx**：它是 **LGPL-2.1**，要求你给出许可与来源声明。我们的做法是"用户自己装、我们只引用"，
   并在 README 里写明依赖与许可（LGPL §6 要求的 notice 就是这个）。HarmonyX 是 MIT，随 BepInEx 来。
2. **不要打包游戏 DLL 或游戏资源**：Release 里只有你自己的 DLL；`Assembly-CSharp.dll` 只在编译期用。
3. **不要绕过 DRM、不要做联机作弊**：本插件只操作**用户自己机器上、自己拥有的进程**，且只在单机游戏里被触发。
4. **插件会在用户机器上挂起其他进程** —— 这是它最大的"信任问题"。发布时要主动讲清：
   只对**当前用户**的进程、只 `SIGSTOP`/`SIGCONT`（从不 kill）、有 446 条保护名单、
   任何异常路径都会解冻、后端 HTTP 只绑回环且可设令牌。**把它写在 README 第一屏**（我们已在功能表与"为什么冻结"里写了）。
5. **MIT 只覆盖你的代码**；游戏与 BepInEx 的许可各归各的，README 里的表格就是给审核和用户看的。

---

## 4. 首次发布的推荐顺序

```
① 定许可证（MIT）→ 三个仓库各放 LICENSE（生成器已放好 MIT 占位，改版权人即可）
② 生成三个仓库：scripts/split-repos.sh --owner <你的账号> --verify
③ 先发后端 focused（插件与 doctor 都指向它）→ 再发 chillfocused → 最后发扩展
   每个仓库：git init → 首次提交 → push → tag v0.1.0 → gh release create
   —— 逐条命令见 docs/repo-split.md §3
④ 每个仓库的 Description/Topics/Actions 权限（§1.4）
⑤ 提 awesome-chillwithyou 的 PR（草稿已备好，指向 chillfocused 仓库）
⑥ 视反响决定是否上 Nexus/Thunderstore，以及是否做 Windows 后端
⑦ 后端另发 AUR（focused / focused-git，PKGBUILD 已生成在 packaging/aur/）
```

---

## 5. 发布之后（维护预期）

- **Issue 模板**（10 分钟，回报很高）：`.github/ISSUE_TEMPLATE/bug_report.md` 里直接要求附上
  ① `focused-doctor` 输出 ② `BepInEx/LogOutput.log` 里 `ChillFocused ... loaded` 那行 ③ 系统与 Proton 版本。
  这三个信息能覆盖绝大多数问题。
- **版本节奏**：`plugin-vX.Y.Z` / `focused-vX.Y.Z` / `ext-vX.Y.Z` 分开打 tag；改 `csproj <Version>` 时
  `VersionConsistencyTests` 会逼你同步源码常量。
- **回复 issue 的姿态**：先问"跑没跑 doctor"，再让对方附日志；不要凭猜测改代码。
- **安全策略（可选）**：加 `SECURITY.md`，说明"后端只绑回环、请勿暴露到公网、Token 配置项存在"。
- **别删历史**：即使你后悔某个命名，也用新 tag/新 Release 纠正，别 force-push 已发布的分支。

---

## 6. 出处

- [GitHub Docs: Quickstart for repositories](https://docs.github.com/en/repositories/creating-and-managing-repositories/quickstart-for-repositories)（建库、首次推送、Releases）
- [Nexus Mods: File Submission Guidelines](https://help.nexusmods.com/article/28-file-submission-guidelines)（版权、联网、AI 标签、真实性）
- [Thunderstore: Creating a Package](https://wiki.thunderstore.io/mods/creating-a-package)（`icon.png` 256×256、manifest、zip 根目录）
- [BepInEx LICENSE（LGPL-2.1）](https://github.com/BepInEx/BepInEx/blob/master/LICENSE)
- [AUR submission guidelines](https://wiki.archlinux.org/title/AUR_submission_guidelines) 与
  [Arch package guidelines（包源码用 0BSD）](https://manual.archlinux.page/package-guidelines/)
