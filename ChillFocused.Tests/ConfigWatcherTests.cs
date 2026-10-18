using System;
using System.IO;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    public class ConfigWatcherTests
    {
        [Fact]
        public void Changed_fingerprint_invokes_the_callback_once()
        {
            var fingerprint = "a";
            var changes = 0;
            var watcher = new ConfigWatcher(
                null, () => { }, () => fingerprint, () => changes++, _ => { });

            watcher.MarkCurrent();
            watcher.Tick();
            Assert.Equal(0, changes);

            fingerprint = "b";
            watcher.Tick();
            watcher.Tick();
            Assert.Equal(1, changes);
        }

        [Fact]
        public void Invalidate_forces_the_next_tick_to_report()
        {
            var changes = 0;
            var watcher = new ConfigWatcher(
                null, () => { }, () => "same", () => changes++, _ => { });
            watcher.MarkCurrent();

            watcher.Invalidate();
            watcher.Tick();

            Assert.Equal(1, changes);
        }

        [Fact]
        public void A_newer_file_mtime_triggers_reload()
        {
            var path = Path.Combine(Path.GetTempPath(), "chillfocused-watch-" + Guid.NewGuid() + ".cfg");
            try
            {
                File.WriteAllText(path, "a");
                var reloads = 0;
                var watcher = new ConfigWatcher(
                    path,
                    () => reloads++,
                    () => "same",
                    () => { },
                    _ => { });

                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
                watcher.ReloadIfChanged();

                Assert.Equal(1, reloads);
            }
            finally
            {
                try { File.Delete(path); } catch (Exception) { }
            }
        }
    }
}
