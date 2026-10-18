using System.Text;

namespace ChillFocused.Core
{
    /// <summary>
    /// One flat, loggable view of the plugin's runtime state.
    /// </summary>
    /// <remarks>
    /// The values were previously spread across HUD fields, HeadlessRunner counters,
    /// RulePublisher state and FocusRunner diagnostics. F6 prints this snapshot so a
    /// bug report can be answered from one line instead of five screenshots.
    /// No token or process command line is included.
    /// </remarks>
    public sealed class PluginDebugSnapshot
    {
        public bool Connected;
        public string Version = "?";
        public bool Degraded;
        public bool SessionActive;
        public bool GateSatisfied;
        public bool DryRun;
        public bool TimerKnown;
        public TimerMode TimerMode;
        public bool TimerWorking;
        public bool TimerRunning;
        public bool TimerResting;
        public string TimerPhase = string.Empty;
        public string TimerSource = string.Empty;
        public int FrozenCount;
        public int FrozenTotal;
        public string LastWarning = string.Empty;

        public bool SyncConnected;
        public bool RulesDue;
        public int BackendPid;
        public int SyncCycles;
        public int SyncPushes;
        public float LastHeartbeatAt;
        public double LastSyncCycleMs;
        public double MaxSyncCycleMs;
        public string SyncLastError = string.Empty;
        public string RuleLastError = string.Empty;

        public int PollEventCursor;
        public float PollNextAt;
        public int UpdateFrames;
        public int GuiFrames;
        public int PollCount;
        public double LastPollMs;
        public double MaxPollMs;
        public double LastDrawMs;
        public double MaxDrawMs;

        public string ToLine()
        {
            var b = new StringBuilder();
            Append(b, "connected", Connected ? "true" : "false");
            Append(b, "version", Version);
            Append(b, "degraded", Degraded ? "true" : "false");
            Append(b, "session", SessionActive ? "true" : "false");
            Append(b, "gate", GateSatisfied ? "true" : "false");
            Append(b, "dry_run", DryRun ? "true" : "false");
            Append(b, "timer_known", TimerKnown ? "true" : "false");
            Append(b, "timer_mode", TimerMode.ToString());
            Append(b, "timer_working", TimerWorking ? "true" : "false");
            Append(b, "timer_running", TimerRunning ? "true" : "false");
            Append(b, "timer_resting", TimerResting ? "true" : "false");
            Append(b, "timer_phase", string.IsNullOrEmpty(TimerPhase) ? "-" : TimerPhase);
            Append(b, "timer_source", string.IsNullOrEmpty(TimerSource) ? "-" : TimerSource);
            Append(b, "frozen", FrozenCount.ToString());
            Append(b, "frozen_total", FrozenTotal.ToString());
            Append(b, "sync_connected", SyncConnected ? "true" : "false");
            Append(b, "rules_due", RulesDue ? "true" : "false");
            Append(b, "backend_pid", BackendPid.ToString());
            Append(b, "sync_cycles", SyncCycles.ToString());
            Append(b, "sync_pushes", SyncPushes.ToString());
            Append(b, "last_heartbeat", LastHeartbeatAt.ToString("0.0"));
            Append(b, "sync_cycle_ms", LastSyncCycleMs.ToString("0.0"));
            Append(b, "sync_cycle_max_ms", MaxSyncCycleMs.ToString("0.0"));
            Append(b, "event_cursor", PollEventCursor.ToString());
            Append(b, "next_poll", PollNextAt.ToString("0.0"));
            Append(b, "poll_count", PollCount.ToString());
            Append(b, "poll_ms", LastPollMs.ToString("0.0"));
            Append(b, "poll_max_ms", MaxPollMs.ToString("0.0"));
            Append(b, "frames", UpdateFrames.ToString());
            Append(b, "gui_frames", GuiFrames.ToString());
            Append(b, "draw_ms", LastDrawMs.ToString("0.0"));
            Append(b, "draw_max_ms", MaxDrawMs.ToString("0.0"));
            if (!string.IsNullOrEmpty(SyncLastError))
            {
                Append(b, "sync_error", OneLine(SyncLastError));
            }

            if (!string.IsNullOrEmpty(RuleLastError))
            {
                Append(b, "rule_error", OneLine(RuleLastError));
            }

            if (!string.IsNullOrEmpty(LastWarning))
            {
                Append(b, "warning", OneLine(LastWarning));
            }

            return b.ToString();
        }

        private static void Append(StringBuilder b, string key, string value)
        {
            if (b.Length > 0)
            {
                b.Append(' ');
            }

            b.Append(key).Append('=').Append(value ?? string.Empty);
        }

        private static string OneLine(string value)
        {
            return (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }

    /// <summary>The sync-thread contribution to <see cref="PluginDebugSnapshot"/>.</summary>
    public struct SyncDebugSnapshot
    {
        public bool Connected;
        public bool RulesDue;
        public int BackendPid;
        public int Cycles;
        public int Pushes;
        public float LastHeartbeatAt;
        public double LastCycleMs;
        public double MaxCycleMs;
        public string LastError;
        public string RuleLastError;
    }
}
