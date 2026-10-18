using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Decodes Focused's base64 PNG process icons and keeps their Sprites alive.
    /// No Windows/Wine shell icon API is involved; the backend already resolved
    /// the desktop icon for the process.
    /// </summary>
    internal sealed class ProcessIconCache
    {
        private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private Sprite _fallback;

        internal Sprite Get(string name, string base64, out bool real)
        {
            real = false;
            var key = (name ?? string.Empty) + "|" + (base64 ?? string.Empty);
            Sprite sprite;
            if (_cache.TryGetValue(key, out sprite) && sprite != null)
            {
                real = true;
                return sprite;
            }

            if (!string.IsNullOrEmpty(base64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(base64);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (texture.LoadImage(bytes))
                    {
                        texture.wrapMode = TextureWrapMode.Clamp;
                        texture.filterMode = FilterMode.Bilinear;
                        sprite = Sprite.Create(texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f), 100f);
                        _cache[key] = sprite;
                        real = true;
                        return sprite;
                    }
                    UnityEngine.Object.Destroy(texture);
                }
                catch (Exception)
                {
                    // Fall through to the letter tile.
                }
            }

            return Fallback(name);
        }

        private Sprite Fallback(string name)
        {
            if (_fallback != null)
            {
                return _fallback;
            }

            // A neutral tile is enough when an app did not publish an icon.
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var colors = new Color32[32 * 32];
            var tint = new Color32(38, 198, 218, 255);
            for (var i = 0; i < colors.Length; i++) colors[i] = tint;
            texture.SetPixels32(colors);
            texture.Apply(false, true);
            _fallback = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f),
                new Vector2(0.5f, 0.5f), 100f);
            _fallback.name = "ProcessFallback";
            return _fallback;
        }
    }
}
