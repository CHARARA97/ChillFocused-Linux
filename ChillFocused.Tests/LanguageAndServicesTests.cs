using System;
using System.Collections.Generic;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// The panel follows the game's language.  These cover the mapping and the
    /// override order, which is the part that decides what a player actually sees.
    /// </summary>
    public class PanelLanguageTests
    {
        [Theory]
        [InlineData("Japanese", GameLanguage.Japanese)]
        [InlineData("English", GameLanguage.English)]
        [InlineData("ChineseSimplified", GameLanguage.ChineseSimplified)]
        [InlineData("ChineseTraditional", GameLanguage.ChineseTraditional)]
        [InlineData("chinese_traditional", GameLanguage.ChineseTraditional)]
        [InlineData("", GameLanguage.English)]
        [InlineData("Klingon", GameLanguage.English)]
        public void The_game_language_maps_onto_ours(string name, GameLanguage expected)
        {
            Assert.Equal(expected, GameLanguageProbe.Map(name));
        }

        [Fact]
        public void An_unknown_language_falls_back_to_english_not_chinese()
        {
            // English is the game's own fallback, so a player whose setting we cannot
            // read is likelier to want it than Chinese.
            Assert.Equal(GameLanguage.English, GameLanguageProbe.Map("Esperanto"));
        }

        [Fact]
        public void Every_key_has_all_three_languages()
        {
            var text = new PanelText();
            var keys = PanelText.Keys();

            Assert.True(keys.Length > 60, "the text table looks truncated: " + keys.Length);

            for (var i = 0; i < keys.Length; i++)
            {
                PanelText.Language = GameLanguage.ChineseSimplified;
                var zh = text.BuiltIn(keys[i]);
                PanelText.Language = GameLanguage.English;
                var en = text.BuiltIn(keys[i]);
                PanelText.Language = GameLanguage.Japanese;
                var ja = text.BuiltIn(keys[i]);

                Assert.False(string.IsNullOrEmpty(zh), keys[i] + " has no Chinese");
                Assert.False(string.IsNullOrEmpty(en), keys[i] + " has no English");
                Assert.False(string.IsNullOrEmpty(ja), keys[i] + " has no Japanese");
            }

            PanelText.Language = GameLanguage.ChineseSimplified;
        }

        [Fact]
        public void The_language_decides_which_wording_comes_out()
        {
            var text = new PanelText();

            PanelText.Language = GameLanguage.English;
            Assert.Equal("frozen", text.Get("overlay.frozen", "已冻结"));

            PanelText.Language = GameLanguage.Japanese;
            Assert.Equal("凍結", text.Get("overlay.frozen", "已冻结"));

            PanelText.Language = GameLanguage.ChineseSimplified;
            Assert.Equal("已冻结", text.Get("overlay.frozen", "已冻结"));
        }

        [Fact]
        public void A_text_override_beats_the_language()
        {
            // What the user typed in com.chillfocused.text.cfg wins, whatever language
            // the game is set to -- otherwise their wording would silently change the
            // moment they switched language.
            var text = new PanelText();
            text.Apply("overlay.frozen = 已挂起\n", null);

            PanelText.Language = GameLanguage.English;
            Assert.Equal("已挂起", text.Get("overlay.frozen", "已冻结"));
            PanelText.Language = GameLanguage.Japanese;
            Assert.Equal("已挂起", text.Get("overlay.frozen", "已冻结"));

            PanelText.Language = GameLanguage.ChineseSimplified;
        }

        [Fact]
        public void An_unknown_key_still_returns_the_callers_wording()
        {
            var text = new PanelText();
            try
            {
                PanelText.Language = GameLanguage.English;
                Assert.Equal("fallback", text.Get("not.a.key", "fallback"));
            }
            finally
            {
                PanelText.Language = GameLanguage.ChineseSimplified;
            }
        }

        [Fact]
        public void The_generated_file_leaves_values_empty_so_the_language_still_applies()
        {
            // The file is written on first run; if it wrote the wording in, switching
            // the game's language would stop changing the panels.
            var template = new PanelText().Template();

            Assert.Contains("overlay.frozen =", template);
            Assert.DoesNotContain("overlay.frozen = 已冻结", template);
        }
    }

    /// <summary>
    /// "The detection works and the game is in Chinese" and "the detection never
    /// ran" must not look identical in a log; that is how a silent feature failure
    /// survives a release.
    /// </summary>
    public class GameLanguageProbeReportingTests
    {
        private sealed class FakeSupplier
        {
            private readonly object _value;

            public FakeSupplier(object value)
            {
                _value = value;
            }

            public object Get()
            {
                return _value;
            }
        }

        private enum FakeGameLanguage
        {
            ChineseSimplified,
            English,
            Japanese,
        }

        public GameLanguageProbeReportingTests()
        {
            GameLanguageProbe.Reset();
        }

        /// <summary>Every case here ends where it started: the language is global.</summary>
        private static void Restore()
        {
            GameLanguageProbe.Reset();
        }

        private static Func<string, bool, object> Supplier(object instance)
        {
            return (name, force) => instance;
        }

        [Fact]
        public void A_confirmed_language_is_reported_even_when_it_matches_the_default()
        {
            try
            {
            // The default is Chinese; the game being Chinese must still be visible.
            var lines = new List<string>();

            GameLanguageProbe.Tick(lines.Add, lines.Add, true, Supplier(new FakeSupplier(FakeGameLanguage.ChineseSimplified)));

            Assert.Contains("ChineseSimplified", string.Join("\n", lines));
            Assert.True(GameLanguageProbe.SupplierFound);
            Assert.Equal("ChineseSimplified", GameLanguageProbe.DetectedName);
            }
            finally
            {
                Restore();
            }
        }

        [Fact]
        public void It_is_reported_once_not_every_poll()
        {
            try
            {
            var lines = new List<string>();
            var supplier = Supplier(new FakeSupplier(FakeGameLanguage.Japanese));

            GameLanguageProbe.Tick(lines.Add, lines.Add, true, supplier);
            GameLanguageProbe.Tick(lines.Add, lines.Add, true, supplier);
            GameLanguageProbe.Tick(lines.Add, lines.Add, true, supplier);

            Assert.Single(lines);
            Assert.Equal(GameLanguage.Japanese, PanelText.Language);
            }
            finally
            {
                Restore();
            }
        }

        [Fact]
        public void An_unreachable_supplier_says_so_once()
        {
            var lines = new List<string>();

            GameLanguageProbe.Tick(lines.Add, lines.Add, true, (name, force) => null);
            GameLanguageProbe.Tick(lines.Add, lines.Add, true, (name, force) => null);

            Assert.Single(lines);
            Assert.Contains("not readable yet", lines[0]);
            Assert.False(GameLanguageProbe.SupplierFound);
        }

        [Fact]
        public void A_supplier_that_answers_nothing_is_reported_the_same_way()
        {
            // Resolving the supplier proves the DI path works, but a null answer
            // still means "no language detected" -- the panel label says so.
            var lines = new List<string>();

            GameLanguageProbe.Tick(lines.Add, lines.Add, true, Supplier(new FakeSupplier(null)));

            Assert.Single(lines);
            Assert.Contains("not readable yet", lines[0]);
            Assert.False(GameLanguageProbe.SupplierFound);
        }

        [Fact]
        public void A_transient_failure_is_informational_and_a_persistent_one_warns()
        {
            try
            {
            var info = new List<string>();
            var warn = new List<string>();

            // First poll, right at start-up: no supplier yet, and that is normal.
            GameLanguageProbe.Tick(info.Add, warn.Add, true, (name, force) => null, now: 1000);
            Assert.Single(info);
            Assert.Empty(warn);

            // Half a minute later it still has not appeared: now it is worth saying.
            GameLanguageProbe.Tick(info.Add, warn.Add, true, (name, force) => null, now: 1000 + 31000);
            Assert.Single(warn);
            Assert.Contains("still unreadable", warn[0]);

            // ...and only once.
            GameLanguageProbe.Tick(info.Add, warn.Add, true, (name, force) => null, now: 1000 + 99000);
            Assert.Single(warn);
            }
            finally
            {
                Restore();
            }
        }

        [Fact]
        public void A_successful_read_after_a_slow_start_is_still_reported()
        {
            try
            {
            var info = new List<string>();
            var warn = new List<string>();

            GameLanguageProbe.Tick(info.Add, warn.Add, true, (name, force) => null, now: 1000);
            GameLanguageProbe.Tick(
                info.Add, warn.Add, true, Supplier(new FakeSupplier(FakeGameLanguage.Japanese)),
                now: 1000 + 31000);

            Assert.Contains(info, line => line.Contains("Japanese"));
            Assert.True(GameLanguageProbe.SupplierFound);
            }
            finally
            {
                Restore();
            }
        }

        [Fact]
        public void A_later_successful_read_is_still_reported()
        {
            try
            {
            var lines = new List<string>();

            GameLanguageProbe.Tick(lines.Add, lines.Add, true, (name, force) => null);
            GameLanguageProbe.Tick(lines.Add, lines.Add, true, Supplier(new FakeSupplier(FakeGameLanguage.English)));

            Assert.Equal(2, lines.Count);
            Assert.Contains("English", lines[1]);
            Assert.True(GameLanguageProbe.SupplierFound);
            }
            finally
            {
                Restore();
            }
        }

        [Fact]
        public void The_language_changes_when_the_game_says_so()
        {
            var lines = new List<string>();

            GameLanguageProbe.Tick(lines.Add, lines.Add, true, Supplier(new FakeSupplier(FakeGameLanguage.English)));
            Assert.Equal(GameLanguage.English, PanelText.Language);

            GameLanguageProbe.Tick(lines.Add, lines.Add, true, Supplier(new FakeSupplier(FakeGameLanguage.Japanese)));
            Assert.Equal(GameLanguage.Japanese, PanelText.Language);

            // Reset the static so the rest of the suite is unaffected.
            GameLanguageProbe.Reset();
        }
    }

    /// <summary>
    /// The DI lookup is the second way to reach the game's services.  It has to fail
    /// quietly: it runs during start-up, when the container may not exist yet.
    /// </summary>
    public class GameServicesTests
    {
        [Fact]
        public void Resolving_an_unknown_type_returns_null_instead_of_throwing()
        {
            GameServices.Reset();

            Assert.Null(GameServices.Resolve("No.Such.Type.Anywhere", true));
        }

        [Fact]
        public void Resolving_nothing_is_not_an_error()
        {
            Assert.Null(GameServices.Resolve(null, true));
            Assert.Null(GameServices.Resolve(string.Empty, true));
        }

        [Fact]
        public void The_container_type_name_is_the_one_the_game_uses()
        {
            // Other mods for this game resolve from exactly this type; if it ever
            // moves, this test is where the news should arrive.
            Assert.Equal("NestopiSystem.DIContainers.ProjectLifetimeScope",
                         GameServices.ContainerTypeName);
            Assert.Equal("Bulbul.LanguageSupplier", GameLanguageProbe.SupplierTypeName);
        }
    }
}
