using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The plugin folder is shared, and BepInEx loads everything in it.  Two copies
    /// of this plugin mean two overlays and two poll loops with nothing in the log
    /// to explain it, so the install is checked at start-up.
    /// </summary>
    public class PluginInstallCheckTests
    {
        private const string Self = "/game/BepInEx/plugins/ChillFocused.dll";

        [Fact]
        public void A_clean_install_has_nothing_to_report()
        {
            var found = PluginInstallCheck.ForeignCopies(
                new[] { Self, "/game/BepInEx/plugins/ChillClock.dll", "/game/BepInEx/plugins/Foo.dll" },
                Self);

            Assert.Empty(found);
            Assert.Null(PluginInstallCheck.Warning(found, Self));
        }

        [Fact]
        public void The_old_subdirectory_layout_is_flagged()
        {
            // What an upgrade actually looks like: the flat file was added and the
            // previous folder was forgotten.
            var found = PluginInstallCheck.ForeignCopies(
                new[] { Self, "/game/BepInEx/plugins/ChillFocused/ChillFocused.dll" },
                Self);

            Assert.Single(found);
            Assert.Contains("ChillFocused/ChillFocused.dll", found[0]);
            Assert.Contains("plugins/ChillFocused/ChillFocused.dll", PluginInstallCheck.Warning(found, Self));
        }

        [Fact]
        public void The_retired_name_is_flagged_too()
        {
            var found = PluginInstallCheck.ForeignCopies(
                new[] { Self, "/game/BepInEx/plugins/ChillFocus/ChillFocus.dll" },
                Self);

            Assert.Single(found);
        }

        [Fact]
        public void A_second_copy_in_the_same_folder_is_flagged()
        {
            var found = PluginInstallCheck.ForeignCopies(
                new[] { Self, "/game/BepInEx/plugins/ChillFocused-old.dll" },
                Self);

            Assert.Single(found);
        }

        [Fact]
        public void A_name_that_merely_starts_the_same_is_not_flagged()
        {
            // "ChillFocusable" is somebody else's plugin, and telling the user to
            // delete it would be worse than saying nothing.
            var found = PluginInstallCheck.ForeignCopies(
                new[] { Self, "/game/BepInEx/plugins/ChillFocusable.dll",
                        "/game/BepInEx/plugins/ChillFocusedness.dll" },
                Self);

            Assert.Empty(found);
        }

        [Fact]
        public void Case_differences_are_two_files_on_linux()
        {
            var found = PluginInstallCheck.ForeignCopies(
                new[] { Self, "/game/BepInEx/plugins/chillfocused.dll" },
                Self);

            Assert.Single(found);
        }

        [Fact]
        public void Non_dll_entries_are_ignored()
        {
            var found = PluginInstallCheck.ForeignCopies(
                new[] { Self, "/game/BepInEx/plugins/ChillFocused.dll.bak",
                        "/game/BepInEx/plugins/ChillFocused.txt", null, "" },
                Self);

            Assert.Empty(found);
        }

        [Fact]
        public void A_null_listing_is_not_a_crash()
        {
            Assert.Empty(PluginInstallCheck.ForeignCopies(null, Self));
        }
    }

    /// <summary>
    /// The released DLL has to know its own version: users have exactly one file to
    /// look at, and a bug report that says "0.1.0" is only useful if that is true.
    /// </summary>
    public class VersionConsistencyTests
    {
        [Fact]
        public void The_attribute_version_matches_the_project_file()
        {
            // The [BepInPlugin] attribute needs a compile-time constant, so the
            // number has to exist twice; this is the test that keeps them equal.
            var project = File.ReadAllText(ProjectFile());

            var match = Regex.Match(project, @"<Version>([^<]+)</Version>");
            Assert.True(match.Success, "no <Version> in ChillFocused.csproj");

            Assert.Equal(FocusPluginInfo.Version, match.Groups[1].Value.Trim());
        }

        [Fact]
        public void No_source_file_hardcodes_a_second_version_string()
        {
            // Guard against the real failure mode: somebody edits the constant and
            // leaves a literal behind somewhere else.
            var root = Path.GetDirectoryName(ProjectFile());
            var offenders = Directory
                .GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains("/obj/") && !file.Contains("/bin/"))
                .Where(file => file.EndsWith("PluginInfo.cs", StringComparison.Ordinal) == false)
                .Where(file => Regex.IsMatch(File.ReadAllText(file), @"[""']\d+\.\d+\.\d+[""']"))
                .ToArray();

            Assert.Empty(offenders);
        }

        private static string ProjectFile()
        {
            // Walk up from the test binary to the repository, then into the plugin.
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                // Both layouts exist: this working tree keeps the plugin under
                // plugin/, the published repository has it at the root.
                foreach (var relative in new[]
                         {
                             Path.Combine("plugin", "ChillFocused", "ChillFocused.csproj"),
                             Path.Combine("ChillFocused", "ChillFocused.csproj"),
                         })
                {
                    var candidate = Path.Combine(directory.FullName, relative);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("ChillFocused.csproj not found above " + AppContext.BaseDirectory);
        }
    }
}
