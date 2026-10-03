using System;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// Tests for the hand-rolled JSON used by the Unity-independent sync thread.
    /// </summary>
    /// <remarks>
    /// JsonUtility cannot be used there (wrong thread, and it is unavailable
    /// outside Unity), so the payload is built by hand. That makes escaping the
    /// one thing that must not be wrong: a process name containing a quote would
    /// otherwise produce a malformed body and Focused would reject the whole
    /// rule set.
    /// </remarks>
    public class RuleJsonTests
    {
        [Fact]
        public void Empty_settings_produce_a_complete_payload()
        {
            var json = new FocusSettings().BuildRulesJson();

            Assert.Equal(
                "{\"names\":[],\"cmdline_substrings\":[],\"pids\":[],\"pid_guards\":{}," +
                "\"protect_names\":[],\"protect_cmdline_substrings\":[]}",
                json);
        }

        [Fact]
        public void Rules_are_serialised_in_order()
        {
            var settings = new FocusSettings
            {
                ProcessNames = new[] { "firefox", "discord" },
                CmdlineSubstrings = new[] { "--profile=distraction" },
                ProtectedNames = new[] { "my-app" },
                ProtectedCmdlineSubstrings = new[] { "keep-me" }
            };

            var json = settings.BuildRulesJson();

            Assert.Contains("\"names\":[\"firefox\",\"discord\"]", json);
            Assert.Contains("\"cmdline_substrings\":[\"--profile=distraction\"]", json);
            Assert.Contains("\"protect_names\":[\"my-app\"]", json);
            Assert.Contains("\"protect_cmdline_substrings\":[\"keep-me\"]", json);
        }

        [Fact]
        public void Null_arrays_serialise_as_empty_arrays()
        {
            var settings = new FocusSettings
            {
                ProcessNames = null,
                CmdlineSubstrings = null,
                ProtectedNames = null,
                ProtectedCmdlineSubstrings = null
            };

            var json = settings.BuildRulesJson();

            Assert.DoesNotContain("null]", json);
            Assert.Contains("\"names\":[]", json);
            Assert.Contains("\"protect_names\":[]", json);
        }

        [Theory]
        [InlineData("plain", "\"plain\"")]
        [InlineData("with \"quote\"", "\"with \\\"quote\\\"\"")]
        [InlineData("back\\slash", "\"back\\\\slash\"")]
        [InlineData("line\nbreak", "\"line\\nbreak\"")]
        [InlineData("tab\there", "\"tab\\there\"")]
        [InlineData("carriage\rreturn", "\"carriage\\rreturn\"")]
        public void Special_characters_are_escaped(string value, string expected)
        {
            var settings = new FocusSettings { ProcessNames = new[] { value } };

            var json = settings.BuildRulesJson();

            Assert.Contains("[" + expected + "]", json);
        }

        [Fact]
        public void Control_characters_become_unicode_escapes()
        {
            var settings = new FocusSettings { ProcessNames = new[] { "bell\u0007end" } };

            var json = settings.BuildRulesJson();

            Assert.Contains("\"bell\\u0007end\"", json);
        }

        [Fact]
        public void Non_ascii_is_passed_through_unescaped()
        {
            // The Focused reads UTF-8, so there is no need to escape these; doing
            // so would only make the audit log harder to read.
            var settings = new FocusSettings { ProcessNames = new[] { "浏览器" } };

            Assert.Contains("\"浏览器\"", settings.BuildRulesJson());
        }

        [Fact]
        public void A_quote_in_a_rule_cannot_escape_the_payload()
        {
            var settings = new FocusSettings { ProcessNames = new[] { "\"] ,\"pids\":[1] ,\"x\":[\"" } };

            var json = settings.BuildRulesJson();

            // The injected structure must stay inside a string literal.
            Assert.Contains("\\\"", json);
            Assert.Equal(1, CountOccurrences(json, "\"pids\""));
            Assert.Equal(1, CountOccurrences(json, "\"names\""));
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            var count = 0;
            var index = haystack.IndexOf(needle, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = haystack.IndexOf(needle, index + 1, StringComparison.Ordinal);
            }

            return count;
        }
    }

    /// <summary>
    /// Tests for the tiny boolean probe the sync thread uses instead of a real
    /// JSON parser. A miss must read as "unknown", never as "false".
    /// </summary>
    public class ReadBoolTests
    {
        [Fact]
        public void Reads_a_real_status_response()
        {
            const string status =
                "{\"ok\": true, \"enabled\": false, \"dry_run\": true, \"stats\": {\"scans\": 3}}";

            Assert.True(HeadlessRunner.ReadBool(status, "ok"));
            Assert.False(HeadlessRunner.ReadBool(status, "enabled"));
            Assert.True(HeadlessRunner.ReadBool(status, "dry_run"));
        }

        [Fact]
        public void Handles_the_compact_form()
        {
            Assert.True(HeadlessRunner.ReadBool("{\"ok\":true}", "ok"));
            Assert.False(HeadlessRunner.ReadBool("{\"enabled\":false}", "enabled"));
        }

        [Fact]
        public void Unknown_keys_read_as_null_not_false()
        {
            // "unknown" is important: the caller must not correct Focused
            // based on a field it failed to understand.
            Assert.Null(HeadlessRunner.ReadBool("{\"ok\": true}", "enabled"));
            Assert.Null(HeadlessRunner.ReadBool(string.Empty, "enabled"));
            Assert.Null(HeadlessRunner.ReadBool(null, "enabled"));
            Assert.Null(HeadlessRunner.ReadBool("{\"enabled\": null}", "enabled"));
            Assert.Null(HeadlessRunner.ReadBool("{\"enabled\": 1}", "enabled"));
        }

        [Fact]
        public void A_key_used_as_a_substring_does_not_confuse_it()
        {
            // "enabled" also appears inside "disabled"; the quoted needle stops
            // that from matching.
            Assert.Null(HeadlessRunner.ReadBool("{\"disabled\": true}", "enabled"));
            Assert.True(HeadlessRunner.ReadBool("{\"disabled\": true, \"enabled\": true}", "enabled"));
        }

        [Fact]
        public void First_matching_key_wins()
        {
            Assert.False(HeadlessRunner.ReadBool("{\"enabled\": false, \"enabled\": true}", "enabled"));
        }

    }
}
