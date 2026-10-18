# 游戏插件 Python 测试

只有发布脚本/安装器检查；C# 逻辑测试在 `../ChillFocused.Tests/`。

```bash
cd chillfocus
python3 -m pytest -q tests
```

- `test_release_scripts.py` — 发布 DLL、脚本语法、doctor。
- `test_installer_layout.py` — 旧布局清理/单 DLL 安装。

改脚本先看 `../scripts/`；测试不碰真实游戏目录。
