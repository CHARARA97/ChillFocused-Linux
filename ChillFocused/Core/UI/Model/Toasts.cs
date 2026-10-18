using System;
using System.Collections.Generic;

namespace ChillFocused.Core
{
    /// <summary>
    /// The overlay's transient messages: a bounded queue that expires against a clock
    /// the caller supplies.
    /// </summary>
    /// <remarks>
    /// Extracted from the runner and kept free of Unity types, which is the point: the
    /// clock is a parameter rather than <c>Time.unscaledTime</c>, so expiry and trimming
    /// can be tested without a running game. Drawing stays in the runner, which owns the
    /// styles, textures and screen coordinates.
    /// </remarks>
    public sealed class Toasts
    {
        /// <summary>Fade window: a toast is opaque until this long before it expires.</summary>
        public const float FadeSeconds = 0.6f;

        /// <summary>How many toasts are kept; adding beyond this drops the oldest.</summary>
        public const int MaxEntries = 4;

        /// <summary>Shortest life a toast may have, whatever the settings ask for.</summary>
        public const float MinSeconds = 1f;

        /// <summary>One queued message.</summary>
        public sealed class Toast
        {
            public string Text;
            public float ExpiresAt;
            public bool Warning;
        }

        private readonly List<Toast> _items = new List<Toast>();

        /// <summary>Oldest first. The draw path only reads.</summary>
        public IReadOnlyList<Toast> Items
        {
            get { return _items; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        /// <summary>
        /// Queue a message that lives <paramref name="seconds"/> from <paramref name="now"/>,
        /// keeping at most <paramref name="maxToasts"/> entries.
        /// </summary>
        public void Add(string text, bool warning, float now, float seconds, int maxToasts)
        {
            _items.Add(new Toast
            {
                Text = text ?? string.Empty,
                ExpiresAt = now + Math.Max(MinSeconds, seconds),
                Warning = warning
            });

            // A cap below one would otherwise empty the list and then keep removing.
            var cap = maxToasts < 1 ? 1 : maxToasts;
            while (_items.Count > cap)
            {
                _items.RemoveAt(0);
            }
        }

        /// <summary>Drop every toast whose life has run out at <paramref name="now"/>.</summary>
        public void Expire(float now)
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (now >= _items[i].ExpiresAt)
                {
                    _items.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Opacity for a toast: fully opaque while it has more than
        /// <see cref="FadeSeconds"/> left, fading to zero at expiry.
        /// </summary>
        public static float Alpha(Toast toast, float now)
        {
            if (toast == null)
            {
                return 0f;
            }

            var alpha = (toast.ExpiresAt - now) / FadeSeconds;
            return alpha < 0f ? 0f : (alpha > 1f ? 1f : alpha);
        }
    }
}
