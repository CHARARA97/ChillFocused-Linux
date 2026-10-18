using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// Lets several frame sources call the same work exactly once per game frame.
    /// </summary>
    /// <remarks>
    /// On this game the plugin's own Update may never run, so the mod installs a
    /// Harmony hook and an onBeforeRender listener as extra sources. Whichever
    /// arrives first wins; the frame number is the dedupe key. Keeping this policy
    /// separate makes it testable and keeps the entry point out of the scheduling
    /// business.
    /// </remarks>
    public sealed class FrameDriver
    {
        private readonly Action<string> _tick;
        private int _lastFrame = -1;

        public FrameDriver(Action<string> tick)
        {
            _tick = tick ?? (source => { });
        }

        public int LastFrame
        {
            get { return _lastFrame; }
        }

        public void Tick(string source, int frame)
        {
            if (frame == _lastFrame)
            {
                return;
            }

            _lastFrame = frame;
            _tick(source);
        }
    }
}
