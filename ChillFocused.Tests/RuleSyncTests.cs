using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class RuleSyncTests
    {
        [Fact]
        public void The_first_rule_set_goes_out_as_soon_as_there_is_a_connection()
        {
            var sync = new RuleSync();

            Assert.True(sync.Due(0f));
        }

        [Fact]
        public void Nothing_is_sent_again_once_the_executor_accepted_it()
        {
            var sync = new RuleSync();

            sync.Sent();

            Assert.False(sync.Due(0f));
            Assert.False(sync.Due(1000f));
        }

        [Fact]
        public void A_failed_push_waits_out_the_backoff()
        {
            var sync = new RuleSync();

            sync.Failed(10f);

            Assert.False(sync.Due(10f));
            Assert.False(sync.Due(10f + RuleSync.RetrySeconds - 0.1f));
            Assert.True(sync.Due(10f + RuleSync.RetrySeconds));
        }

        [Fact]
        public void A_settings_change_sends_immediately_even_during_a_backoff()
        {
            var sync = new RuleSync();
            sync.Sent();
            sync.Failed(10f);

            sync.SettingsChanged();

            Assert.True(sync.Due(10f));
        }

        [Fact]
        public void The_first_pid_we_see_does_not_arm_a_push()
        {
            // The initial rule set is already pending; seeing a pid is not a restart.
            var sync = new RuleSync();
            sync.Sent();

            sync.ObservedPid(4242);

            Assert.False(sync.Due(0f));
        }

        [Fact]
        public void The_same_pid_never_arms_a_push()
        {
            var sync = new RuleSync();
            sync.Sent();
            sync.ObservedPid(4242);

            sync.ObservedPid(4242);
            sync.ObservedPid(4242);

            Assert.False(sync.Due(1000f));
        }

        [Fact]
        public void A_different_pid_means_the_executor_restarted_and_the_rules_go_again()
        {
            var sync = new RuleSync();
            sync.Sent();
            sync.ObservedPid(4242);

            sync.ObservedPid(5150);

            Assert.True(sync.Due(0f));
        }

        [Fact]
        public void A_restart_cancels_a_backoff()
        {
            // A push failed against the old process; its replacement is up, so waiting
            // out the retry window would leave the blacklist unenforced for no reason.
            var sync = new RuleSync();
            sync.Sent();
            sync.ObservedPid(4242);
            sync.Failed(10f);

            sync.ObservedPid(5150);

            Assert.True(sync.Due(10f));
        }

        [Fact]
        public void A_pid_we_cannot_read_neither_arms_a_push_nor_forgets_the_last_one()
        {
            var sync = new RuleSync();
            sync.Sent();
            sync.ObservedPid(4242);

            sync.ObservedPid(0);

            Assert.False(sync.Due(0f), "an unreadable pid must not fake a restart");

            // The remembered pid still works afterwards.
            sync.ObservedPid(5150);
            Assert.True(sync.Due(0f));
        }

        [Fact]
        public void A_restart_while_the_first_push_is_still_pending_changes_nothing()
        {
            var sync = new RuleSync();

            sync.ObservedPid(4242);
            sync.ObservedPid(5150);

            Assert.True(sync.Due(0f));
        }
    }
}
