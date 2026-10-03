using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class ConfigFileTests
    {
        private const string Template =
            "# Demo\n" +
            "#\n" +
            "# A line of prose about the file as a whole.\n" +
            "\n" +
            "# --- alpha ---\n" +
            "# What alpha is for.\n" +
            "a.one = 1\n" +
            "a.two = 2\n" +
            "\n" +
            "# --- beta ---\n" +
            "# What beta is for.\n" +
            "b.one = 10\n";

        [Fact]
        public void Template_reports_keys_values_and_sections()
        {
            var entries = ConfigFile.Template(Template);

            Assert.Equal(3, entries.Count);
            Assert.Equal("a.one", entries[0].Key);
            Assert.Equal("1", entries[0].Value);
            Assert.Equal("alpha", entries[0].Section);
            Assert.Equal("beta", entries[2].Section);
        }

        [Fact]
        public void SectionName_only_recognises_banners()
        {
            Assert.Equal("alpha", ConfigFile.SectionName("# --- alpha ---"));
            Assert.Equal("位置", ConfigFile.SectionName("# --- 位置 ------------------"));
            Assert.Null(ConfigFile.SectionName("# What alpha is for."));
            Assert.Null(ConfigFile.SectionName("# ---"));
        }

        [Fact]
        public void Keys_ignores_comments_and_blank_lines()
        {
            // One real entry: the other two lines are commented out.
            var keys = ConfigFile.Keys("# a.one = 1\n\na.two = 2\n; b.one = 3\n");

            Assert.Single(keys);
            Assert.Contains("a.two", keys);
            Assert.DoesNotContain("a.one", keys);
            Assert.DoesNotContain("b.one", keys);
        }

        [Fact]
        public void Missing_lists_absent_keys_in_template_order()
        {
            var missing = ConfigFile.Missing("a.two = 99\n", Template);

            Assert.Equal(2, missing.Count);
            Assert.Equal("a.one", missing[0].Key);
            Assert.Equal("b.one", missing[1].Key);
        }

        [Fact]
        public void Insert_places_a_key_inside_its_own_section()
        {
            var result = ConfigFile.Insert("# --- alpha ---\na.one = 1\na.two = 2\n", Template);
            var lines = result.Split('\n');

            var inserted = System.Array.IndexOf(lines, "b.one = 10");
            Assert.True(inserted > 0, result);
            Assert.Equal("b.one = 10", lines[inserted]);
        }

        [Fact]
        public void Insert_keeps_existing_values_and_lines_untouched()
        {
            var result = ConfigFile.Insert("# my note\na.one = 42\n", Template);

            Assert.Contains("# my note", result);
            Assert.Contains("a.one = 42", result);
            Assert.DoesNotContain("a.one = 1", result);
        }

        [Fact]
        public void Insert_changes_nothing_when_every_key_is_present()
        {
            var complete = "a.one = 1\na.two = 2\nb.one = 10\n";

            Assert.Equal(complete, ConfigFile.Insert(complete, Template));
        }
    }
}
