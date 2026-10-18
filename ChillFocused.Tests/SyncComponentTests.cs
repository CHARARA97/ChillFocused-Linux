using System;
using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class RulePublisherTests
    {
        [Fact]
        public void Rules_are_sent_once_per_change_and_again_after_a_pid_change()
        {
            var bodies = new List<string>();
            var publisher = new RulePublisher(bodies.Add, _ => { }, _ => { });
            var settings = new FocusSettings { ProcessNames = new[] { "firefox" } };

            publisher.Publish(settings, 0f, 1);
            publisher.Publish(settings, 1f, 1);
            Assert.Single(bodies);

            settings = new FocusSettings { ProcessNames = new[] { "discord" } };
            publisher.Publish(settings, 2f, 1);
            Assert.Equal(2, bodies.Count);

            publisher.Publish(settings, 3f, 2);
            Assert.Equal(3, bodies.Count);
            Assert.Equal(3, publisher.PushCount);
        }

        [Fact]
        public void A_failed_push_retries_after_the_backoff()
        {
            var calls = 0;
            var publisher = new RulePublisher(
                _ => { calls++; throw new InvalidOperationException("down"); },
                _ => { }, _ => { });
            var settings = new FocusSettings();

            publisher.Publish(settings, 10f, null);
            publisher.Publish(settings, 11f, null);
            Assert.Equal(1, calls);

            publisher.Publish(settings, 10f + RuleSync.RetrySeconds, null);
            Assert.Equal(2, calls);
            Assert.Equal(0, publisher.PushCount);
        }
    }

    public class SessionReporterTests
    {
        [Fact]
        public void The_heartbeat_carries_active_dry_run_and_the_lease()
        {
            var bodies = new List<string>();
            var reporter = new SessionReporter(bodies.Add);
            var settings = new FocusSettings
            {
                DryRun = true,
                SessionLeaseSeconds = 30,
            };
            var timer = new TimerState
            {
                Known = true,
                Mode = TimerMode.Pomodoro,
                Working = true,
                Running = true,
                Phase = "Work",
                ReadAt = 100f,
                ChangedAt = 100f,
            };

            var active = reporter.Report(settings, timer, 110f);

            Assert.True(active);
            Assert.Contains("\"active\":true", bodies[0]);
            Assert.Contains("\"dry_run\":true", bodies[0]);
            Assert.Contains("\"ttl_seconds\":30", bodies[0]);
        }
    }

    public class FocusStatusProbeTests
    {
        [Fact]
        public void Parses_the_two_fields_the_sync_loop_needs()
        {
            var probe = new FocusStatusProbe(() => "{\"ok\":true,\"active\":true,\"pid\":4242}");

            var result = probe.Read();

            Assert.True(result.Active);
            Assert.Equal(4242, result.Pid);
            Assert.Contains("\"pid\":4242", result.Raw);
        }
    }
}
