# ChillFocused 游戏插件

《Chill with You: Lo-Fi Story》的 BepInEx 5 插件。读取游戏计时状态，向本机 **Focused** 后端提交规则和租约。

- 后端契约：[`../focused/docs/focused-protocol.md`](../focused/docs/focused-protocol.md)
- 构建/测试：`scripts/build.sh`；C# 测试在 `ChillFocused.Tests/`
- 安装：`scripts/install-bepinex.sh`
- 打包：`scripts/package-mod.sh`

后端不是本仓库的一部分；没有 `focused` 在运行时，插件只显示未连接。
