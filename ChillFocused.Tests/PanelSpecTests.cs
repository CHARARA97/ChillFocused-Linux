using System;
using System.Collections.Generic;
using System.IO;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The panel layout file.
    /// </summary>
    /// <remarks>
    /// This file exists so that adjusting the panel does not cost a rebuild and a
    /// game restart. A malformed value must therefore never throw and never leave the
    /// panel in an unrenderable state -- it has to be clamped and reported.
    /// </remarks>
    public class PanelSpecTests
    {
        [Fact]
        public void Defaults_are_sane()
        {
            var spec = PanelSpec.Defaults();

            Assert.Equal(640f, spec.WindowWidth);
            Assert.Equal(700f, spec.WindowHeight);
            Assert.Equal(15, spec.BodyFontSize);
            Assert.False(spec.Previewing);
        }

        [Fact]
        public void Template_round_trips_to_the_defaults()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();
            spec.Apply(PanelSpec.Template(), problems);

            Assert.Empty(problems);
            Assert.Equal(640f, spec.WindowWidth);
            Assert.Equal(170f, spec.BlockedListMaxHeight);
            Assert.Equal(0, spec.PreviewApps);
        }

        [Fact]
        public void Values_are_applied()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();

            spec.Apply("font.body = 20\nlist.picker.max = 300\nwindow.height = 500", problems);

            Assert.Empty(problems);
            Assert.Equal(20, spec.BodyFontSize);
            Assert.Equal(300f, spec.PickerListMaxHeight);
            Assert.Equal(500f, spec.WindowHeight);
        }

        [Fact]
        public void Comments_and_blank_lines_are_ignored()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();

            spec.Apply("# a comment\n; another\n\n   \nfont.body = 21\n", problems);

            Assert.Empty(problems);
            Assert.Equal(21, spec.BodyFontSize);
        }

        [Fact]
        public void Whitespace_around_keys_and_values_does_not_matter()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();

            spec.Apply("   font.body    =    22   ", problems);

            Assert.Empty(problems);
            Assert.Equal(22, spec.BodyFontSize);
        }

        [Fact]
        public void Keys_are_case_insensitive()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();

            spec.Apply("FONT.BODY = 23", problems);

            Assert.Empty(problems);
            Assert.Equal(23, spec.BodyFontSize);
        }

        [Fact]
        public void An_unknown_key_is_reported_but_harmless()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();

            spec.Apply("font.banana = 12\nfont.body = 16", problems);

            Assert.Single(problems);
            Assert.Contains("font.banana", problems[0]);
            Assert.Equal(16, spec.BodyFontSize);
        }

        [Fact]
        public void A_line_without_an_equals_sign_is_reported()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();

            spec.Apply("font.body 16", problems);

            Assert.Single(problems);
            Assert.Contains("line 1", problems[0]);
        }

        [Fact]
        public void An_unparseable_number_is_reported()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();

            spec.Apply("font.body = big", problems);

            Assert.Single(problems);
            Assert.Equal(15, spec.BodyFontSize);
        }

        [Fact]
        public void Out_of_range_values_are_clamped_and_reported()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();

            spec.Apply("font.body = 400\nwindow.width = 10\nlist.picker.max = 99999", problems);
            spec.Clamp(problems);

            Assert.Equal(48, spec.BodyFontSize);
            Assert.Equal(320f, spec.WindowWidth);
            Assert.Equal(2000f, spec.PickerListMaxHeight);
            Assert.Equal(3, problems.Count);
        }

        [Fact]
        public void A_negative_padding_is_clamped_to_zero()
        {
            var problems = new List<string>();
            var spec = PanelSpec.Defaults();

            spec.Apply("pad.label.bottom = -10", problems);
            spec.Clamp(problems);

            Assert.Equal(0, spec.PadLabelBottom);
        }

        [Fact]
        public void Preview_mode_needs_a_non_zero_count()
        {
            var spec = PanelSpec.Defaults();
            Assert.False(spec.Previewing);

            spec.PreviewApps = 3;
            Assert.True(spec.Previewing);
        }

        [Fact]
        public void Preview_items_are_deterministic_and_marked()
        {
            var spec = PanelSpec.Defaults();
            spec.PreviewProcesses = 20;

            var first = spec.PreviewItems();
            var second = spec.PreviewItems();

            Assert.Equal(20, first.Length);
            Assert.Equal(first[0].name, second[0].name);
            Assert.Contains(first, item => item.@protected);
        }

        [Fact]
        public void Preview_apps_are_deterministic()
        {
            var spec = PanelSpec.Defaults();
            spec.PreviewApps = 30;

            var names = spec.PreviewNames();

            Assert.Equal(30, names.Length);
            Assert.All(names, name => Assert.False(string.IsNullOrEmpty(name)));
        }

        [Fact]
        public void Load_reads_a_file_and_falls_back_when_it_is_missing()
        {
            var path = Path.Combine(Path.GetTempPath(), "chillfocused-panelspec-" + Guid.NewGuid() + ".cfg");
            try
            {
                File.WriteAllText(path, "font.body = 19\n");
                var problems = new List<string>();

                var loaded = PanelSpecFile.Load(path, problems);
                Assert.Equal(19, loaded.BodyFontSize);
                Assert.Equal(path, loaded.Source);

                var missing = PanelSpecFile.Load(path + ".nope", problems);
                Assert.Equal(15, missing.BodyFontSize);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void EnsureExists_writes_the_template_once()
        {
            var path = Path.Combine(Path.GetTempPath(), "chillfocused-panelspec-" + Guid.NewGuid() + ".cfg");
            try
            {
                Assert.True(PanelSpecFile.EnsureExists(path));
                Assert.True(File.Exists(path));

                // Second call must not overwrite the user's edits.
                File.WriteAllText(path, "font.body = 30\n");
                Assert.False(PanelSpecFile.EnsureExists(path));
                Assert.Contains("30", File.ReadAllText(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void DefaultPath_sits_beside_the_plugin_config()
        {
            var path = PanelSpecFile.DefaultPath("/home/someone/BepInEx/config/com.chillfocused.plugin.cfg");

            Assert.Equal("/home/someone/BepInEx/config/" + PanelSpecFile.FileName, path);
        }

        [Fact]
        public void DefaultPath_copes_with_no_config_path()
        {
            Assert.Equal(PanelSpecFile.FileName, PanelSpecFile.DefaultPath(null));
        }

        [Fact]
        public void Stamp_reports_a_change()
        {
            var path = Path.Combine(Path.GetTempPath(), "chillfocused-panelspec-" + Guid.NewGuid() + ".cfg");
            try
            {
                File.WriteAllText(path, "font.body = 16\n");
                var before = PanelSpecFile.Stamp(path);

                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
                Assert.NotEqual(before, PanelSpecFile.Stamp(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
