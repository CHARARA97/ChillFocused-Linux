using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class FocusSettingsTests
    {
        [Fact]
        public void ToRulePayload_carries_the_blacklist_verbatim()
        {
            var settings = new FocusSettings
            {
                ProcessNames = new[] { "firefox", "discord" },
                CmdlineSubstrings = new[] { "--profile distractor" },
                ProtectedNames = new[] { "my-precious-app" }
            };

            var payload = settings.ToRulePayload();

            Assert.Equal(new[] { "firefox", "discord" }, payload.names);
            Assert.Equal(new[] { "--profile distractor" }, payload.cmdline_substrings);
            Assert.Equal(new[] { "my-precious-app" }, payload.protect_names);
        }

        [Fact]
        public void ToRulePayload_never_reports_null_arrays()
        {
            // JsonUtility renders a null array as []; relying on "absent" would be
            // fragile, so the payload always carries concrete arrays.
            var payload = new FocusSettings
            {
                ProcessNames = null,
                CmdlineSubstrings = null,
                ProtectedNames = null,
                ProtectedCmdlineSubstrings = null
            }.ToRulePayload();

            Assert.NotNull(payload.names);
            Assert.NotNull(payload.cmdline_substrings);
            Assert.NotNull(payload.pids);
            Assert.NotNull(payload.protect_names);
            Assert.NotNull(payload.protect_cmdline_substrings);
            Assert.Empty(payload.names);
            Assert.Empty(payload.protect_names);
        }

        [Fact]
        public void ToRulePayload_sends_no_pid_rules()
        {
            // The plugin has no UI for picking PIDs yet, and a PID rule without a
            // reuse guard is the most dangerous kind. Sending none is deliberate.
            Assert.Empty(new FocusSettings().ToRulePayload().pids);
        }

        [Fact]
        public void Validate_flags_a_catch_all_blacklist()
        {
            var settings = new FocusSettings { ProcessNames = new[] { "*" } };

            var problems = settings.Validate();

            Assert.Single(problems);
            Assert.Contains("catch-all", problems[0]);
        }

        [Fact]
        public void Validate_flags_a_catch_all_cmdline_rule()
        {
            var settings = new FocusSettings { CmdlineSubstrings = new[] { "**" } };

            Assert.Contains("catch-all", settings.Validate()[0]);
        }

        [Fact]
        public void Validate_accepts_a_normal_rule_set()
        {
            var settings = new FocusSettings
            {
                ProcessNames = new[] { "firefox", "chrom*" },
                CmdlineSubstrings = new[] { "--profile distractor" }
            };

            Assert.Empty(settings.Validate());
        }

        [Fact]
        public void Validate_flags_an_absurd_poll_interval()
        {
            var settings = new FocusSettings { PollIntervalSeconds = 0.01f };

            Assert.Contains("0.2s", settings.Validate()[0]);
        }

        [Fact]
        public void Validate_flags_a_tiny_timeout_and_an_empty_url()
        {
            Assert.Contains("100ms", new FocusSettings { RequestTimeoutMs = 10 }.Validate()[0]);
            Assert.Contains("FocusedUrl", new FocusSettings { FocusedUrl = "" }.Validate()[0]);

            var lease = new FocusSettings { SessionLeaseSeconds = 1 }.Validate();
            Assert.Contains("SessionLeaseSeconds", lease[lease.Length - 1]);
            Assert.Contains(
                "TimerFreshSeconds",
                new FocusSettings { TimerFreshSeconds = 1f }.Validate()[0]);
        }

        [Fact]
        public void RulesEqual_compares_only_the_rule_arrays()
        {
            var a = new FocusSettings { ProcessNames = new[] { "firefox" }, Hud = HudMode.Detailed };
            var b = new FocusSettings { ProcessNames = new[] { "firefox" }, Hud = HudMode.Off };
            var c = new FocusSettings { ProcessNames = new[] { "discord" } };

            Assert.True(a.RulesEqual(b));
            Assert.False(a.RulesEqual(c));
            Assert.False(a.RulesEqual(null));
        }

        [Fact]
        public void RulesEqual_is_order_sensitive_but_null_safe()
        {
            var a = new FocusSettings { ProcessNames = null };
            var b = new FocusSettings { ProcessNames = new string[0] };
            var c = new FocusSettings { ProcessNames = new[] { "a", "b" } };
            var d = new FocusSettings { ProcessNames = new[] { "b", "a" } };

            Assert.True(a.RulesEqual(b));
            Assert.False(c.RulesEqual(d));
        }

        [Fact]
        public void EndpointEquals_tracks_url_token_and_timeout()
        {
            var a = new FocusSettings();
            var b = new FocusSettings();

            Assert.True(a.EndpointEquals(b));

            b.FocusedUrl = "http://127.0.0.1:9000";
            Assert.False(a.EndpointEquals(b));

            b = new FocusSettings { FocusedToken = "secret" };
            Assert.False(a.EndpointEquals(b));

            b = new FocusSettings { RequestTimeoutMs = 3000 };
            Assert.False(a.EndpointEquals(b));
        }

        [Fact]
        public void EndpointEquals_ignores_unrelated_preferences()
        {
            var a = new FocusSettings();
            var b = new FocusSettings { Hud = HudMode.Off, HudCorner = "bottom-right" };

            Assert.True(a.EndpointEquals(b));
        }

        [Fact]
        public void Defaults_are_inert()
        {
            var settings = new FocusSettings();

            // Nothing armed, nothing on screen beyond the one-line status.
            Assert.Empty(settings.ProcessNames);
            Assert.Equal(HudMode.Minimal, settings.Hud);
            Assert.Equal("http://127.0.0.1:8766", settings.FocusedUrl);
        }
    }
}
