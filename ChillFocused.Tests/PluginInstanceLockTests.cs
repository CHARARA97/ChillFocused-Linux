using System;
using System.IO;
using ChillFocused.Core;
using Xunit;

namespace ChillFocused.Tests
{
    /// <summary>
    /// A second BepInEx-loaded copy must become inert, not run a second overlay.
    /// </summary>
    public class PluginInstanceLockTests
    {
        [Fact]
        public void The_second_holder_is_refused_until_the_first_is_disposed()
        {
            var path = Path.Combine(Path.GetTempPath(), "chillfocused-lock-" + Guid.NewGuid() + ".lock");
            try
            {
                string error;
                var first = PluginInstanceLock.TryAcquire(path, out error);
                Assert.NotNull(first);
                Assert.Null(error);

                var second = PluginInstanceLock.TryAcquire(path, out error);
                Assert.Null(second);
                Assert.False(string.IsNullOrEmpty(error));

                first.Dispose();
                var third = PluginInstanceLock.TryAcquire(path, out error);
                Assert.NotNull(third);
                third.Dispose();
            }
            finally
            {
                try { File.Delete(path); } catch (Exception) { }
            }
        }

        [Fact]
        public void An_empty_path_fails_with_a_reason()
        {
            string error;
            var lockHandle = PluginInstanceLock.TryAcquire(string.Empty, out error);

            Assert.Null(lockHandle);
            Assert.Contains("empty", error);
        }
    }
}
