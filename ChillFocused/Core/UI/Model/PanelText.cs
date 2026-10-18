using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ChillFocused.Core
{
    /// <summary>The languages the panels can speak.</summary>
    public enum GameLanguage
    {
        ChineseSimplified = 0,
        ChineseTraditional = 1,
        English = 2,
        Japanese = 3,
    }

    /// <summary>
    /// Every string the panels display, overridable from a local file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate file rather than more keys in the layout spec: there are dozens of
    /// strings, and the generated file lists all of them with the wording currently in
    /// use, so changing one is a matter of editing a value rather than discovering a
    /// key name.
    /// </para>
    /// <para>
    /// The defaults below carry Chinese, English and Japanese, and the running
    /// language follows the game's own setting (see GameLanguageProbe).  An empty or
    /// absent override means "use the built-in wording"; a value the user typed always
    /// wins, whatever language the game is set to.
    /// </para>
    /// </remarks>
    public sealed class PanelText
    {
        private readonly Dictionary<string, string> _overrides =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>One string, in every language the panels speak.</summary>
        private sealed class Entry
        {
            public string Chinese;
            public string English;
            public string Japanese;
        }

        /// <summary>
        /// The wording the panels ship with.  The key list is the interface: every
        /// string the panels can show has one, so the generated text file can list
        /// them all and a translator can find them.
        /// </summary>
        private static readonly Dictionary<string, Entry> Defaults =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase)
            {
            { "overlay.app_name_s", new Entry { Chinese = " 个应用名", English = " app name(s)", Japanese = " 件のアプリ" } },
            { "overlay.closed", new Entry { Chinese = "已冻结", English = "frozen", Japanese = "凍結中" } },
            { "overlay.connected_to_the_executor", new Entry { Chinese = "已连接到 Focused", English = "connected to Focused", Japanese = "Focused に接続しました" } },
            { "overlay.connected_v", new Entry { Chinese = "已连接 v", English = "connected v", Japanese = "接続済み v" } },
            { "overlay.enforcing", new Entry { Chinese = "生效中", English = "active", Japanese = "有効" } },
            { "overlay.enforcing_work_session", new Entry { Chinese = "生效中 · 创作模式", English = "active · work session", Japanese = "有効 · 作業セッション" } },
            { "overlay.executor", new Entry { Chinese = "Focused", English = "Focused", Japanese = "Focused" } },
            { "overlay.frozen", new Entry { Chinese = "已冻结", English = "frozen", Japanese = "凍結" } },
            { "overlay.hotkeys", new Entry { Chinese = "热键", English = "hotkeys", Japanese = "ホットキー" } },
            { "overlay.last_closed", new Entry { Chinese = "最近冻结", English = "recently frozen", Japanese = "最近の凍結" } },
            { "overlay.last_error", new Entry { Chinese = "最近错误", English = "last error", Japanese = "直近のエラー" } },
            { "overlay.no_signal_yet", new Entry { Chinese = "尚无信号", English = "no signal yet", Japanese = "信号なし" } },
            { "overlay.not_connected", new Entry { Chinese = "未连接", English = "not connected", Japanese = "未接続" } },
            { "overlay.not_enforcing_executor_not_connected", new Entry { Chinese = "未生效 · Focused 未连接", English = "inactive · Focused not connected", Japanese = "無効 · Focused に未接続" } },
            { "overlay.not_enforcing_start_the_game_s_timer", new Entry { Chinese = "未生效 · 开始游戏计时后自动生效", English = "inactive · starts with the game's timer", Japanese = "無効 · ゲームのタイマー開始で有効" } },
            { "overlay.overlay", new Entry { Chinese = "面板", English = "overlay", Japanese = "オーバーレイ" } },
            { "overlay.overlay_detailed", new Entry { Chinese = "面板：详细", English = "overlay: detailed", Japanese = "オーバーレイ: 詳細" } },
            { "overlay.overlay_one_line", new Entry { Chinese = "面板：一行", English = "overlay: one line", Japanese = "オーバーレイ: 1 行" } },
            { "overlay.panel_layout_reloaded", new Entry { Chinese = "面板布局已重新加载", English = "panel layout reloaded", Japanese = "パネルレイアウトを再読み込みしました" } },
            { "overlay.recording_only", new Entry { Chinese = "  [只记录]", English = "  [record only]", Japanese = "  [記録のみ]" } },
            { "overlay.rules", new Entry { Chinese = "规则", English = "rules", Japanese = "ルール" } },
            { "overlay.running", new Entry { Chinese = "运行中", English = "running", Japanese = "実行中" } },
            { "overlay.session_ended_blacklist_paused", new Entry { Chinese = "创作结束 · 已全部恢复", English = "session over · everything resumed", Japanese = "セッション終了 · すべて再開" } },
            { "overlay.settings", new Entry { Chinese = "设置", English = "settings", Japanese = "設定" } },
            { "overlay.stopped", new Entry { Chinese = "已停止", English = "stopped", Japanese = "停止" } },
            { "overlay.timer", new Entry { Chinese = "计时器", English = "timer", Japanese = "タイマー" } },
            { "overlay.work_session_blacklist_active", new Entry { Chinese = "开始创作 · 已冻结屏蔽名单应用", English = "session started · block-list apps frozen", Japanese = "セッション開始 · 対象アプリを凍結" } },
            { "panel.active_always_on", new Entry { Chinese = "生效中 · 不依赖计时器", English = "active · ignores the timer", Japanese = "有効 · タイマー無視" } },
            { "panel.active_work_session_running", new Entry { Chinese = "生效中 · 创作模式", English = "active · work session", Japanese = "有効 · 作業セッション" } },
            { "panel.add", new Entry { Chinese = "加入", English = "Add", Japanese = "追加" } },
            { "panel.address", new Entry { Chinese = "地址", English = "Address", Japanese = "アドレス" } },
            { "panel.advanced", new Entry { Chinese = "高级选项", English = "Advanced", Japanese = "詳細設定" } },
            { "panel.apps_to_block", new Entry { Chinese = "要屏蔽的应用", English = "Apps to freeze", Japanese = "凍結するアプリ" } },
            { "panel.asking_the_executor", new Entry { Chinese = "  正在向 Focused 查询…", English = "  asking Focused", Japanese = "  Focused に問い合わせ中" } },
            { "panel.back", new Entry { Chinese = "返回", English = "Back", Japanese = "戻る" } },
            { "panel.behaviour", new Entry { Chinese = "行为", English = "Behaviour", Japanese = "動作" } },
            { "panel.connected_v", new Entry { Chinese = "已连接 v", English = "connected v", Japanese = "接続済み v" } },
            { "panel.could_not_read_the_list", new Entry { Chinese = "无法读取列表：", English = "could not read the list:", Japanese = "リストを読み込めません:" } },
            { "panel.detailed_overlay", new Entry { Chinese = "详细状态面板", English = "Detailed status", Japanese = "詳細ステータス" } },
            { "panel.diagnostics_for_a_bug_report", new Entry { Chinese = "诊断信息（用于反馈问题）：", English = "diagnostics (for bug reports):", Japanese = "診断情報（不具合報告用）:" } },
            { "panel.display", new Entry { Chinese = "显示", English = "Display", Japanese = "表示" } },
            { "panel.do_not_depend_on_the_game_s_timer_always", new Entry { Chinese = "不依赖游戏计时器（始终生效）", English = "Ignore the game's timer (always active)", Japanese = "ゲームのタイマーを使わない（常に有効）" } },
            { "panel.executor", new Entry { Chinese = "Focused", English = "Focused", Japanese = "Focused" } },
            { "panel.filter", new Entry { Chinese = "过滤", English = "Filter", Japanese = "絞り込み" } },
            { "panel.font", new Entry { Chinese = "字体：{0}", English = "Font: {0}", Japanese = "フォント: {0}" } },
            { "panel.found_n", new Entry { Chinese = "已获取 {0} 项", English = "{0} entries", Japanese = "{0} 件" } },
            { "panel.freeze_off_warning", new Entry { Chinese = "注意：冻结已被配置文件关闭（freeze.enabled=false），应用不会被冻结。", English = "Note: freezing is off in the configuration (freeze.enabled=false); nothing will be suspended.", Japanese = "注意: 設定で凍結が無効です（freeze.enabled=false）。アプリは凍結されません。" } },
            { "panel.hint_block_mode", new Entry { Chinese = "屏蔽模式 —— 点中的进程会在创作期间被冻结；再点一次移出名单。", English = "Freeze mode - a selected process is frozen while the game's timer runs; click it again to remove it.", Japanese = "凍結モード — 選んだプロセスはゲームのタイマー動作中に凍結されます。もう一度押すと解除します。" } },
            { "panel.hint_protect_mode", new Entry { Chinese = "保护模式 —— 点中的进程永不被冻结。受保护的进程平时不出现在这个列表里，所以这里是取消保护的地方。", English = "Protect mode - a selected process is never frozen. Protected processes are hidden from the normal list, so this is where protection is lifted.", Japanese = "保護モード — 選んだプロセスは凍結されません。保護中のプロセスは通常の一覧に出ないため、ここで解除します。" } },
            { "panel.layout_file_hint", new Entry { Chinese = "改完保存即生效，无需重启。把预览数量设为大于 0 可用测试数据检查布局。", English = "Save to apply, no restart needed. Set the preview count above 0 to check the layout with test data.", Japanese = "保存すればすぐ反映されます（再起動は不要）。プレビュー数を 0 より大きくするとテストデータで確認できます。" } },
            { "panel.match_by_launch_arguments", new Entry { Chinese = "按启动参数匹配", English = "Match by launch arguments", Japanese = "起動引数で一致" } },
            { "panel.matches_the_whole_command_line_so_one_in", new Entry { Chinese = "匹配完整命令行，因此可以只冻结某个应用的其中一个实例。", English = "Matches the whole command line, so a single instance of an app can be frozen on its own.", Japanese = "コマンドライン全体と照合するため、同じアプリの 1 つのインスタンスだけを凍結できます。" } },
            { "panel.mode_block", new Entry { Chinese = "屏蔽模式", English = "Freeze mode", Japanese = "凍結モード" } },
            { "panel.mode_protect", new Entry { Chinese = "保护模式", English = "Protect mode", Japanese = "保護モード" } },
            { "panel.not_active_executor_offline", new Entry { Chinese = "未生效 · Focused 未连接", English = "inactive · Focused not connected", Japanese = "無効 · Focused に未接続" } },
            { "panel.not_connected", new Entry { Chinese = "未连接", English = "not connected", Japanese = "未接続" } },
            { "panel.not_fetched", new Entry { Chinese = "尚未获取", English = "not fetched yet", Japanese = "未取得" } },
            { "panel.nothing_is_blocked_yet_pick_one_from_the", new Entry { Chinese = "   尚未添加要屏蔽的应用；可从下方列表中选择。", English = "   no apps to block yet; select one from the list below.", Japanese = "   ブロックするアプリがまだありません。下の一覧から選択してください。" } },
            { "panel.nothing_matches", new Entry { Chinese = "  （没有匹配的进程）", English = "  (no matching process)", Japanese = "  （一致するプロセスなし）" } },
            { "panel.nothing_to_block_yet", new Entry { Chinese = "尚未添加任何应用。", English = "No apps added yet.", Japanese = "まだアプリが追加されていません。" } },
            { "panel.panel_layout_file", new Entry { Chinese = "面板布局文件", English = "Panel layout file", Japanese = "パネルレイアウトファイル" } },
            { "panel.preview_mode_nothing_was_changed", new Entry { Chinese = "预览模式 —— 未做任何修改", English = "Preview mode - nothing was changed", Japanese = "プレビューモード — 変更は保存されません" } },
            { "panel.preview_test_data_for_checking_the_layou", new Entry { Chinese = "预览模式 —— 正在用测试数据检查布局，不会保存任何修改", English = "Preview mode - checking the layout with test data; nothing is saved", Japanese = "プレビューモード — テストデータでレイアウトを確認中。保存されません" } },
            { "panel.protected", new Entry { Chinese = "受保护", English = "protected", Japanese = "保護中" } },
            { "panel.protected_names_hint", new Entry { Chinese = "灰色名称由系统保护，无法冻结。", English = "Greyed-out names are protected by the system and cannot be frozen.", Japanese = "灰色の名前はシステムが保護しており、凍結できません。" } },
            { "panel.record_only_do_not_close_anything", new Entry { Chinese = "只记录，不冻结任何进程", English = "Record only, freeze nothing", Japanese = "記録のみ（凍結しない）" } },
            { "panel.refresh", new Entry { Chinese = "刷新", English = "Refresh", Japanese = "更新" } },
            { "panel.reload_panel_config", new Entry { Chinese = "重新加载面板配置", English = "Reload panel config", Japanese = "パネル設定を再読み込み" } },
            { "panel.running_now_click_a_row_to_add_or_remove", new Entry { Chinese = "当前运行中 —— 点击一行即可加入/移除：", English = "Running now - click a row to add or remove:", Japanese = "実行中 — 行をクリックで追加/解除:" } },
            { "panel.scan_now", new Entry { Chinese = "立即扫描", English = "Scan now", Japanese = "今すぐスキャン" } },
            { "panel.status", new Entry { Chinese = "状态", English = "Status", Japanese = "状態" } },
            { "panel.thaw_now", new Entry { Chinese = "立即恢复全部", English = "Resume everything", Japanese = "すべて再開" } },
            { "panel.these_apply_immediately_and_are_written_", new Entry { Chinese = "以下设置立即生效，并会写入配置文件。", English = "These take effect immediately and are written to the configuration file.", Japanese = "以下の設定はすぐに反映され、設定ファイルに保存されます。" } },
            { "panel.these_are_closed_while_the_game_s_timer_", new Entry { Chinese = "游戏计时运行期间（番茄钟或正向计时）这些应用会被冻结，计时结束后自动恢复。", English = "These apps are frozen while the game's timer runs (pomodoro or count-up), and resumed when it stops.", Japanese = "ゲームのタイマー動作中（ポモドーロ / カウントアップ）はこれらのアプリを凍結し、終了時に再開します。" } },
            { "panel.waiting_start_the_game_s_timer", new Entry { Chinese = "待机 · 开始游戏计时后自动生效", English = "idle · starts with the game's timer", Japanese = "待機 · ゲームのタイマー開始で有効" } },
            { "toast.added", new Entry { Chinese = "已加入", English = "added", Japanese = "追加しました" } },
            { "toast.always_on", new Entry { Chinese = "始终生效", English = "always active", Japanese = "常に有効" } },
            { "toast.record_only", new Entry { Chinese = "只记录不冻结", English = "record only", Japanese = "記録のみ" } },
            { "toast.removed", new Entry { Chinese = "已移除", English = "removed", Japanese = "削除しました" } },
            { "toast.scanning", new Entry { Chinese = "正在扫描", English = "scanning", Japanese = "スキャン中" } },
            { "toast.thaw", new Entry { Chinese = "恢复", English = "resumed", Japanese = "再開" } },
            };

        /// <summary>Where this was read from, for display.</summary>
        public string Source = string.Empty;

        /// <summary>The number of keys the file may set.</summary>
        public int Count { get { return Defaults.Count; } }

        /// <summary>
        /// The language the panels currently speak, set from the game's own setting.
        /// </summary>
        /// <remarks>
        /// Static because the call sites are spread over the renderer and the runner
        /// and none of them owns the setting; the probe updates it, and an explicit
        /// text override still wins over whatever it says.
        /// </remarks>
        public static GameLanguage Language = GameLanguage.ChineseSimplified;

        public string Get(string key, string chinese)
        {
            string custom;
            if (_overrides.TryGetValue(key, out custom) && !string.IsNullOrEmpty(custom))
            {
                return custom;
            }

            Entry entry;
            if (Defaults.TryGetValue(key, out entry))
            {
                if (Language == GameLanguage.English && !string.IsNullOrEmpty(entry.English))
                {
                    return entry.English;
                }

                if (Language == GameLanguage.Japanese && !string.IsNullOrEmpty(entry.Japanese))
                {
                    return entry.Japanese;
                }

                // Chinese comes from the caller's own string: the panel layouts and
                // a few call sites carry deliberate spacing that predates this table,
                // and switching those silently would be a wording change nobody asked
                // for.  The table's Chinese wording is what the generated file shows.
                if (Language == GameLanguage.ChineseTraditional && !string.IsNullOrEmpty(entry.Chinese))
                {
                    return entry.Chinese;
                }
            }

            return chinese;
        }

        /// <summary>The built-in wording for a key, in the current language.</summary>
        public string BuiltIn(string key)
        {
            Entry entry;
            if (!Defaults.TryGetValue(key, out entry))
            {
                return string.Empty;
            }

            if (Language == GameLanguage.English)
            {
                return entry.English;
            }

            if (Language == GameLanguage.Japanese)
            {
                return entry.Japanese;
            }

            return entry.Chinese;
        }

        /// <summary>Every key the panels know about, sorted.</summary>
        public static string[] Keys()
        {
            var keys = new List<string>(Defaults.Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys.ToArray();
        }

        /// <summary>Parse the file over the defaults.</summary>
        public void Apply(string text, List<string> problems)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                {
                    continue;
                }

                var split = line.IndexOf('=');
                if (split <= 0)
                {
                    Note(problems, "第 " + (i + 1) + " 行应为「键 = 值」");
                    continue;
                }

                var key = line.Substring(0, split).Trim();
                if (!Defaults.ContainsKey(key))
                {
                    Note(problems, "第 " + (i + 1) + " 行：未知键 " + key);
                    continue;
                }

                _overrides[key] = line.Substring(split + 1).Trim();
            }
        }

        private static void Note(List<string> problems, string message)
        {
            if (problems != null && problems.Count < 40)
            {
                problems.Add(message);
            }
        }

        /// <summary>Load the file, reporting anything it got wrong.</summary>
        public static PanelText Load(string path, List<string> problems)
        {
            var text = new PanelText();
            text.Source = path ?? string.Empty;

            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    text.Apply(File.ReadAllText(path), problems);
                }
            }
            catch (Exception ex)
            {
                if (problems != null)
                {
                    problems.Add("读取失败: " + ex.Message);
                }
            }

            return text;
        }

        /// <summary>The file written on first run: every key, with its current wording.</summary>
        public string Template()
        {
            var text = new StringBuilder();
            text.AppendLine("# ChillFocused 面板文字");
            text.AppendLine("#");
            text.AppendLine("# 直接改右侧的值并保存，1 秒内生效，不用重启。");
            text.AppendLine("# 留空 = 使用内置文字。以 # 或 ; 开头的行为注释。");
            text.AppendLine("#");
            text.AppendLine("# 每行上方注释里是内置文案（跟随游戏语言）；把值填在等号右边即固定成你自己的措辞。");
            text.AppendLine("# The comment above each key is the built-in wording, which follows the");
            text.AppendLine("# game's language; fill in a value to pin your own wording.");
            text.AppendLine();

            // Values are written empty on purpose: an empty value means "use the
            // built-in wording", which keeps following the game's language.  The
            // built-in text is in the comment, so the file still reads as a list of
            // what each key currently shows.
            var keys = Keys();
            for (var i = 0; i < keys.Length; i++)
            {
                text.AppendLine("# " + BuiltIn(keys[i]));
                text.AppendLine(keys[i] + " =");
                text.AppendLine();
            }

            return text.ToString();
        }
    }
}
