using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Re-reads the settings snapshot at most once per second.
    /// </summary>
    /// <remarks>
    /// The panel and the .cfg file edit the same values. The runner used to keep
    /// this timer inline with rendering and polling; keeping it here makes the
    /// "rules changed vs HUD-only change" behaviour independently testable.
    /// </remarks>
    internal sealed class SettingsRefresher
    {
        private readonly Func<FocusSettings> _provider;
        private float _nextRefreshAt;

        internal SettingsRefresher(Func<FocusSettings> provider)
        {
            _provider = provider;
        }

        internal bool TryRefresh(float now, ref FocusSettings settings)
        {
            if (_provider == null || now < _nextRefreshAt || settings == null)
            {
                return false;
            }

            _nextRefreshAt = now + 1f;
            var fresh = _provider();
            if (fresh == null)
            {
                return false;
            }

            if (!fresh.RulesEqual(settings))
            {
                settings = fresh;
                return true;
            }

            // The HUD mode can change without changing the rules. Reuse the
            // existing object so callers holding it see the live mode.
            var changed = settings.Hud != fresh.Hud;
            settings.Hud = fresh.Hud;
            return changed;
        }
    }
}
