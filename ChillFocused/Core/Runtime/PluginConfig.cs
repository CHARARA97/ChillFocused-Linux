using System;
using BepInEx.Configuration;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// The plugin's BepInEx configuration, in one place.
    /// </summary>
    /// <remarks>
    /// Binding was mixed into the entry point together with lifecycle, frame hooks
    /// and runner wiring. Keeping the ConfigEntry handles here means the rest of the
    /// plugin only sees <see cref="ReadSettings"/> or the entries it actually needs
    /// (hotkeys and panel switches).
    /// </remarks>
    internal sealed class PluginConfig
    {
        private const string SectionGeneral = "1. General";
        private const string SectionBlacklist = "2. Blacklist";
        private const string SectionProtect = "3. Protect";
        private const string SectionOverlay = "4. Overlay";
        private const string SectionHotkeys = "5. Hotkeys";
        private const string SectionSession = "6. Session";
        private const string SectionUi = "7. UI";

        internal ConfigEntry<bool> RunInBackground;
        internal ConfigEntry<string> FocusedUrl;
        internal ConfigEntry<string> FocusedToken;
        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<bool> AlwaysOn;
        internal ConfigEntry<bool> DryRun;
        internal ConfigEntry<bool> MirrorExternalSession;
        internal ConfigEntry<float> MirrorTimeSyncSeconds;
        internal ConfigEntry<bool> LockTimerControls;
        internal ConfigEntry<bool> NativeUi;
        internal ConfigEntry<float> TimerFreshSeconds;
        internal ConfigEntry<int> SessionLeaseSeconds;
        internal ConfigEntry<int> RequestTimeoutMs;
        internal ConfigEntry<float> PollInterval;
        internal ConfigEntry<string> ProcessNames;
        internal ConfigEntry<string> CmdlineSubstrings;
        internal ConfigEntry<string> ProtectedNames;
        internal ConfigEntry<string> ProtectedCmdlineSubstrings;
        internal ConfigEntry<HudMode> HudMode;
        internal ConfigEntry<string> HudCorner;
        internal ConfigEntry<float> ToastSeconds;
        internal ConfigEntry<KeyboardShortcut> ToggleHud;
        internal ConfigEntry<KeyboardShortcut> ToggleSettings;
        internal ConfigEntry<KeyboardShortcut> ReloadUi;

        internal PluginConfig(BepInEx.Configuration.ConfigFile config)
        {
            RunInBackground = config.Bind(
                SectionGeneral, "RunInBackground", true,
                "窗口失焦时仍保持插件运行（默认开启）。\n" +
                "某些 Wayland/XWayland 组合下合成器不发布 _NET_ACTIVE_WINDOW，Unity\n" +
                "无法判断自己是否获得焦点，于是把整个帧循环暂停：Update/OnGUI 一次都\n" +
                "不调用，表现为「没有面板、也不与 Focused 后端通信」，而且不报任何异常。\n" +
                "开启此项会设置 Application.runInBackground = true 来规避。\n" +
                "Keep the plugin ticking while the window is not focused. Some Wayland/\n" +
                "XWayland compositors publish no _NET_ACTIVE_WINDOW, so Unity cannot tell\n" +
                "whether it is focused and pauses the frame loop entirely: no Update, no\n" +
                "OnGUI, no exception. Sets Application.runInBackground = true.");

            FocusedUrl = config.Bind(
                SectionGeneral, "FocusedUrl", "http://127.0.0.1:8766",
                "Focused 应用的 HTTP 地址。必须是回环地址：该 API 可以挂起进程。\n" +
                "Base URL of the Focused application. Must be loopback: the API can\n" +
                "suspend processes. The game runs in a Wine prefix but shares the host\n" +
                "network stack, so 127.0.0.1 reaches Focused.");

            FocusedToken = config.Bind(
                SectionGeneral, "FocusedToken", string.Empty,
                "可选共享令牌，需与 Focused config.json 的 http.token 一致。\n" +
                "Optional shared secret. When set it must match 'http.token' in\n" +
                "Focused's config.json, and is sent as the X-Focused-Token header.");

            Enabled = config.Bind(
                SectionGeneral, "Enabled", true,
                "总开关。关闭后永远不会冻结任何应用（只在面板/热键里改也行）。\n" +
                "Master switch. When off, nothing is ever suspended.");

            AlwaysOn = config.Bind(
                SectionGeneral, "AlwaysOn", false,
                "true = 不看游戏计时器，游戏运行时一直生效；false = 跟随游戏计时器。\n" +
                "true = ignore the game's timer and stay active while the game runs;\n" +
                "false = follow the game's own work sessions.");

            DryRun = config.Bind(
                SectionGeneral, "DryRun", false,
                "只记录，不冻结：只记录会命中哪些进程，不真的挂起它们。\n" +
                "Rehearsal: record what would be suspended, suspend nothing.");

            MirrorExternalSession = config.Bind(
                SectionSession, "MirrorExternalSession", false,
                "Focused 中手动开始专注时，也在游戏内启动对应的计时器；\n" +
                "Focused 结束专注时，暂停或重置游戏内计时器。\n" +
                "Mirror a manual Focused session into the game's timer. When Focused\n" +
                "starts a session, the plugin applies its duration/cycles to the game\n" +
                "Pomodoro timer and starts it; when Focused ends, the game timer stops.");

            MirrorTimeSyncSeconds = config.Bind(
                SectionSession, "MirrorTimeSyncSeconds", 1f,
                "每多少秒用 Focused 的进度校正一次游戏计时器；0 表示只在开始/结束时校正。\n" +
                "How often to correct the game timer from Focused; 0 disables periodic sync.");

            LockTimerControls = config.Bind(
                SectionSession, "LockTimerControls", false,
                "专注中隐藏游戏计时器的暂停/跳过/停止按钮，防止中途打断计划。\n" +
                "番茄钟：暂停、跳过、停止三个按钮都藏起来（工作与休息阶段相同）。\n" +
                "正向计时：只藏暂停；停止按钮保留，否则无法退出正向计时模式。\n" +
                "Hide the game timer's pause/skip/stop controls during a session. Pomodoro\n" +
                "hides all three; countup keeps its stop button, which is the only way out\n" +
                "of that mode.");

            NativeUi = config.Bind(
                SectionUi, "NativeUi", true,
                "使用游戏原生 UGUI 面板；关闭后回退到旧 IMGUI 面板。\n" +
                "Use the native UGUI panel; false falls back to the legacy IMGUI panel.");

            TimerFreshSeconds = config.Bind(
                SectionGeneral, "TimerFreshSeconds", 25f,
                "游戏计时器读数多久算新鲜（秒）。超时视为这次创作结束。\n" +
                "How long the game's last timer reading stays believable. After that\n" +
                "the work session is considered over and everything is resumed.");

            SessionLeaseSeconds = config.Bind(
                SectionGeneral, "SessionLeaseSeconds", 30,
                "会话租约（秒）。插件会持续续期；一旦插件停止（崩溃/被杀/被冻），\n" +
                "Focused 会在租约到期后自动恢复所有被挂起的应用。\n" +
                "Session lease in seconds, renewed while a session runs. If this\n" +
                "plugin stops existing, Focused resumes everything when it lapses.");

            RequestTimeoutMs = config.Bind(
                SectionGeneral, "RequestTimeoutMs", 1500,
                "单次 HTTP 请求超时（毫秒）。请求在后台线程执行，不会卡住游戏。\n" +
                "Per-request timeout in milliseconds. Requests run on a background\n" +
                "thread, so this bounds latency, not frame rate.");

            PollInterval = config.Bind(
                SectionGeneral, "PollIntervalSeconds", 1.0f,
                "轮询 Focused 后端状态的间隔（秒），下限 0.2。\n" +
                "How often to poll Focused, in seconds. Floor of 0.2s; a faster\n" +
                "poll buys nothing because Focused's own scan interval is >= 0.2s.");

            ProcessNames = config.Bind(
                SectionBlacklist, "ProcessNames", string.Empty,
                "屏蔽名单里的进程名，用分号或换行分隔，支持 * 和 ? 通配符，大小写不敏感。\n" +
                "例：firefox; chromium*; discord; Slack\n" +
                "Process names to suspend, separated by ';' or newlines. Glob '*'\n" +
                "and '?' are supported; matching is case-insensitive. Both the kernel\n" +
                "task name (comm) and the executable basename are considered.\n" +
                "NOTE: names longer than 15 characters also match their kernel-\n" +
                "truncated form, e.g. 'chromium-browser' matches comm 'chromium-browse'.");

            CmdlineSubstrings = config.Bind(
                SectionBlacklist, "CmdlineSubstrings", string.Empty,
                "屏蔽名单里的启动参数子串（用于改名或由脚本启动的应用），分号/换行分隔。\n" +
                "例：--profile distractor; /opt/tools/distraction\n" +
                "Substrings matched against the target's full command line. Use this\n" +
                "when a process can be renamed, or when only one of several instances\n" +
                "should be blocked. Commas are NOT separators, so '--flag=a,b' is safe.");

            ProtectedNames = config.Bind(
                SectionProtect, "ExtraProtectedNames", string.Empty,
                "额外的保护名单进程名。保护名单只增不减：这里的内容会与 Focused 内置的\n" +
                "保护名单（wineserver、steam、桌面合成器、systemd 等）合并。\n" +
                "Extra protect names, separated by ';' or newlines. This list is\n" +
                "ADDITIVE: Focused keeps its built-in rails (wineserver, steam, the\n" +
                "compositor, systemd, ...) and unions them with these. Protection always\n" +
                "wins over the block list, so an entry here is never suspended.");

            ProtectedCmdlineSubstrings = config.Bind(
                SectionProtect, "ExtraProtectedCmdlineSubstrings", string.Empty,
                "额外的保护名单启动参数子串，同样只增不减。\n" +
                "Extra protect command-line substrings. Also additive.");

            HudMode = config.Bind(
                SectionOverlay, "HudMode", ChillFocused.Core.HudMode.Minimal,
                "游戏内状态面板显示多少内容。F9 循环切换。\n" +
                "  Minimal  = 一行：●/○ 状态 + 已冻结数量（默认）\n" +
                "  Detailed = 分组多行，含计时器/执行器/热键，排查时用\n" +
                "  Off      = 完全不显示\n" +
                "How much the in-game overlay shows. F9 cycles it. Minimal is one line:\n" +
                "a filled circle while the block list is being enforced, how many were\n" +
                "closed, and nothing else. Detailed adds the timer, executor and hotkey\n" +
                "rows for troubleshooting.");

            HudCorner = config.Bind(
                SectionOverlay, "HudCorner", "top-left",
                "面板位置：top-left / top-right / bottom-left / bottom-right。\n" +
                "Overlay corner: top-left, top-right, bottom-left or bottom-right.");

            ToastSeconds = config.Bind(
                SectionOverlay, "ToastSeconds", 4.0f,
                "冻结提示的显示时长（秒）。\n" +
                "How long an interception notice stays on screen, in seconds.");

            ToggleHud = config.Bind(
                SectionHotkeys, "ToggleHud", new KeyboardShortcut(KeyCode.F9),
                "显示/隐藏状态面板。\n" +
                "Show or hide the status overlay.");

            ToggleSettings = config.Bind(
                SectionHotkeys, "ToggleSettings", new KeyboardShortcut(KeyCode.F7),
                "打开/关闭游戏内设置面板。\n" +
                "Open or close the in-game settings window.");

            ReloadUi = config.Bind(
                SectionHotkeys, "ReloadUi", new KeyboardShortcut(KeyCode.F6),
                "重新加载面板布局、文案和样式，不需要重启游戏。\n" +
                "Reload the overlay layout, text and styles without restarting the game.");
        }

        /// <summary>A cheap snapshot of everything the runner cares about.</summary>
        internal string Fingerprint()
        {
            return string.Join(
                "|",
                RunInBackground.Value.ToString(),
                HudMode.Value.ToString(),
                HudCorner.Value,
                ToastSeconds.Value.ToString("R"),
                PollInterval.Value.ToString("R"),
                FocusedUrl.Value,
                FocusedToken.Value,
                RequestTimeoutMs.Value.ToString(),
                ProcessNames.Value,
                CmdlineSubstrings.Value,
                ProtectedNames.Value,
                ProtectedCmdlineSubstrings.Value,
                MirrorExternalSession.Value.ToString(),
                MirrorTimeSyncSeconds.Value.ToString("R"),
                NativeUi.Value.ToString());
        }

        internal FocusSettings ReadSettings()
        {
            return new FocusSettings
            {
                Hud = HudMode.Value,
                HudCorner = HudCorner.Value,
                ToastSeconds = ToastSeconds.Value,
                PollIntervalSeconds = PollInterval.Value,
                FocusedUrl = FocusSettings.IsLoopbackUrl(FocusedUrl.Value)
                    ? FocusedUrl.Value
                    : "http://127.0.0.1:8766",
                FocusedToken = FocusedToken.Value,
                RequestTimeoutMs = RequestTimeoutMs.Value,
                Enabled = Enabled.Value,
                AlwaysOn = AlwaysOn.Value,
                DryRun = DryRun.Value,
                MirrorExternalSession = MirrorExternalSession.Value,
                MirrorTimeSyncSeconds = MirrorTimeSyncSeconds.Value,
                NativeUi = NativeUi.Value,
                TimerFreshSeconds = TimerFreshSeconds.Value,
                SessionLeaseSeconds = SessionLeaseSeconds.Value,
                ProcessNames = RuleText.ToArray(ProcessNames.Value),
                CmdlineSubstrings = RuleText.ToArray(CmdlineSubstrings.Value),
                ProtectedNames = RuleText.ToArray(ProtectedNames.Value),
                ProtectedCmdlineSubstrings = RuleText.ToArray(ProtectedCmdlineSubstrings.Value),
            };
        }
    }
}
