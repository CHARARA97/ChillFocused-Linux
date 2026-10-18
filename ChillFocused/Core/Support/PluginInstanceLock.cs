using System;
using System.IO;

namespace ChillFocused.Core
{
    /// <summary>
    /// One process-wide lock file that prevents two copies of the plugin from both
    /// running their overlay and sync thread.
    /// </summary>
    /// <remarks>
    /// BepInEx loads every DLL under <c>plugins/</c>, including subdirectories. A
    /// stale copy from an upgrade therefore creates a second HUD, a second sync
    /// thread and two writers to the same config. A warning is not enough; the
    /// second instance must become inert.
    /// </remarks>
    public sealed class PluginInstanceLock : IDisposable
    {
        private FileStream _stream;

        private PluginInstanceLock(FileStream stream)
        {
            _stream = stream;
        }

        /// <summary>
        /// Acquire the lock, or return <c>null</c> with a user-facing reason.
        /// </summary>
        public static PluginInstanceLock TryAcquire(string path, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(path))
            {
                error = "the instance lock path is empty";
                return null;
            }

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // FileShare.None is the cross-process part: the second process gets
                // an IOException instead of a second overlay.
                var stream = new FileStream(
                    path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return new PluginInstanceLock(stream);
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }

        public void Dispose()
        {
            var stream = _stream;
            _stream = null;
            if (stream != null)
            {
                try { stream.Dispose(); } catch (Exception) { }
            }
        }
    }
}
