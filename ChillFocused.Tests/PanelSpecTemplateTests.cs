using System.Collections.Generic;
using System.Linq;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The template is the single source of truth for which keys exist and where they
    /// belong, so these checks guard the feature that adds new keys to a config file.
    /// </summary>
    public class PanelSpecTemplateTests
    {
        [Fact]
        public void Every_key_is_grouped_under_a_section()
        {
            var entries = ConfigFile.Template(PanelSpec.Template());

            Assert.NotEmpty(entries);
            foreach (var entry in entries)
            {
                Assert.False(string.IsNullOrEmpty(entry.Section), entry.Key + " has no section");
            }
        }

        [Fact]
        public void No_key_appears_twice()
        {
            var keys = ConfigFile.Template(PanelSpec.Template()).Select(e => e.Key).ToList();

            Assert.Equal(keys.Count, keys.Distinct().Count());
        }

        [Fact]
        public void Inserting_into_an_empty_file_leaves_nothing_missing()
        {
            var template = PanelSpec.Template();

            var filled = ConfigFile.Insert("# ChillFocused 面板布局\n", template);

            Assert.Empty(ConfigFile.Missing(filled, template));
        }

        [Fact]
        public void A_key_lands_after_its_own_section_banner()
        {
            var template = PanelSpec.Template();
            var filled = ConfigFile.Insert("# ChillFocused\n", template);
            var lines = filled.Split('\n').ToList();

            foreach (var entry in ConfigFile.Template(template))
            {
                var keyLine = lines.FindIndex(l => l.TrimStart().StartsWith(entry.Key + " ="));
                var bannerLine = lines.FindIndex(l =>
                    ConfigFile.SectionName(l.Trim()) == entry.Section);

                Assert.True(keyLine > bannerLine,
                            entry.Key + " was inserted before its section '" + entry.Section + "'");
            }
        }

        [Fact]
        public void Inserting_twice_changes_nothing_the_second_time()
        {
            var template = PanelSpec.Template();
            var once = ConfigFile.Insert("# ChillFocused\n", template);

            Assert.Equal(once, ConfigFile.Insert(once, template));
        }
    }
}
