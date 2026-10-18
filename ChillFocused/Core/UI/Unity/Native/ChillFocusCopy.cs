namespace ChillFocused.Core
{
    /// <summary>User-facing wording for the injected page. Keep it short and action-oriented.</summary>
    internal static class ChillFocusCopy
    {
        internal const string PageTitle = "ChillFocus";
        internal const string Status = "Focused";
        internal const string Version = "插件版本";
        internal const string PausedCount = "已暂停应用";
        internal const string Settings = "专注设置";
        internal const string Mirror = "由 Focused 控制游戏计时";
        internal const string FollowGameTimer = "由游戏计时器控制专注";
        internal const string LockTimerControls = "专注中隐藏计时按钮";
        internal const string Sync = "自动校准游戏计时";
        internal const string ManageApps = "选择要暂停的应用";
        internal const string PausedApps = "已暂停的应用";
        internal const string NoApps = "暂停名单为空";
        internal const string Open = "打开";
        internal const string Back = "返回";
        internal const string Refresh = "刷新列表";
        internal const string RunningApps = "正在运行的应用";
        internal const string Loading = "读取中…";
        internal const string Remove = "恢复";
        internal const string Pause = "暂停";
        internal const string Protected = "系统保护";
        internal const string CountSuffix = "  ×";

        internal static string Connection(bool connected)
        {
            return connected ? "已连接" : "未连接";
        }

        internal static string Frozen(int frozen, int total)
        {
            return frozen + " 个（上限 " + total + "）";
        }

        internal static string SyncValue(float seconds)
        {
            return seconds <= 0f ? "仅在开始/结束时" : "每 " + seconds.ToString("0.#") + " 秒";
        }

        internal static string AppSummary(ProcessItem item)
        {
            var suffix = item.count > 1 ? CountSuffix + item.count : string.Empty;
            if (item.@protected) suffix += "  " + Protected;
            return item.name + suffix;
        }
    }
}
