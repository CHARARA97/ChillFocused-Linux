using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class PanelStringsTests
    {
        /// <summary>Run the body with a text table installed, then put the static back.</summary>
        private static void WithTable(PanelText table, System.Action body)
        {
            PanelStrings.Text = table;
            try
            {
                body();
            }
            finally
            {
                PanelStrings.Text = null;
            }
        }

        [Fact]
        public void Without_a_text_table_the_built_in_wording_is_used()
        {
            Assert.Equal("返回", PanelStrings.S("panel.back", "返回"));
        }

        [Fact]
        public void The_text_table_wins_over_the_built_in_wording()
        {
            var table = new PanelText();
            table.Apply("panel.back = Zuruck", new List<string>());

            WithTable(table, () =>
                Assert.Equal("Zuruck", PanelStrings.S("panel.back", "返回")));
        }

        [Fact]
        public void A_key_the_table_leaves_empty_keeps_the_built_in_wording()
        {
            var table = new PanelText();
            table.Apply("panel.back =", new List<string>());

            WithTable(table, () =>
                Assert.Equal("返回", PanelStrings.S("panel.back", "返回")));
        }

        [Fact]
        public void Sf_substitutes_its_arguments()
        {
            Assert.Equal("字体：Ari", PanelStrings.Sf("panel.font", "字体：{0}", "Ari"));
        }

        [Fact]
        public void Sf_returns_the_raw_wording_when_a_placeholder_was_edited_away()
        {
            Assert.Equal("字体 {0", PanelStrings.Sf("panel.font", "字体 {0", "Ari"));
        }

        [Fact]
        public void Notice_appends_the_affected_name()
        {
            Assert.Equal("已移除 firefox",
                         PanelStrings.Notice("toast.removed", "已移除", "firefox"));
        }

        [Fact]
        public void Notice_is_just_the_verb_without_a_name()
        {
            Assert.Equal("正在扫描", PanelStrings.Notice("toast.scanning", "正在扫描", string.Empty));
            Assert.Equal("正在扫描", PanelStrings.Notice("toast.scanning", "正在扫描", null));
        }

        [Fact]
        public void Override_prefers_a_configured_string()
        {
            Assert.Equal("My panel", PanelStrings.Override("My panel", "ChillFocused settings"));
        }

        [Fact]
        public void Override_falls_back_when_nothing_is_configured()
        {
            Assert.Equal("ChillFocused settings", PanelStrings.Override(null, "ChillFocused settings"));
            Assert.Equal("ChillFocused settings", PanelStrings.Override(string.Empty, "ChillFocused settings"));
        }
    }
}
