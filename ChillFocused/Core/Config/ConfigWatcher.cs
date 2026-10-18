using System;
using System.IO;

namespace ChillFocused.Core
{
    /// <summary>
    /// Watches the BepInEx config file and the effective settings fingerprint.
    /// </summary>
    /// <remarks>
    /// The plugin used to keep the mtime, lock and fingerprint inline with frame
    /// hooks and runner creation. This class owns only "did the config change?":
    /// reloading from disk, comparing a cheap fingerprint, and invoking one callback.
    /// </remarks>
    public sealed class ConfigWatcher
    {
        private readonly string _path;
        private readonly Action _reload;
        private readonly Func<string> _fingerprint;
        private readonly Action _onChanged;
        private readonly Action<string> _log;
        private DateTime _stamp = DateTime.MinValue;
        private string _last;

        public ConfigWatcher(string path, Action reload, Func<string> fingerprint,
                             Action onChanged, Action<string> log)
        {
            _path = path ?? string.Empty;
            _reload = reload ?? (() => { });
            _fingerprint = fingerprint ?? (() => string.Empty);
            _onChanged = onChanged ?? (() => { });
            _log = log ?? (message => { });
            MarkCurrent();
        }

        /// <summary>Treat the current state as the baseline, e.g. after a settings write.</summary>
        public void MarkCurrent()
        {
            _stamp = Stamp();
            _last = _fingerprint();
        }

        /// <summary>Force the next Tick to see a changed fingerprint.</summary>
        public void Invalidate()
        {
            _last = null;
        }

        /// <summary>Reload the file if its mtime changed. Safe to call outside Tick.</summary>
        public void ReloadIfChanged()
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
            {
                return;
            }

            try
            {
                var stamp = File.GetLastWriteTimeUtc(_path);
                if (stamp == _stamp)
                {
                    return;
                }

                _reload();
                // Reload() rewrites the file, changing its mtime; re-stamp or this
                // check would trigger itself forever.
                _stamp = Stamp();
                _log(LogEvent.Line("config.reload"));
            }
            catch (Exception ex)
            {
                _log(LogEvent.Line("config.reload.failed", "reason=" + ex.Message));
            }
        }

        /// <summary>Reload, then run the callback if the effective settings changed.</summary>
        public void Tick()
        {
            ReloadIfChanged();
            var current = _fingerprint();
            if (string.Equals(current, _last, StringComparison.Ordinal))
            {
                return;
            }

            _last = current;
            _onChanged();
        }

        private DateTime Stamp()
        {
            try
            {
                return string.IsNullOrEmpty(_path) || !File.Exists(_path)
                    ? DateTime.MinValue
                    : File.GetLastWriteTimeUtc(_path);
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }
    }
}
