using System;
using HarmonyLib;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Installs and removes the game-side frame/timer probes.
    /// </summary>
    /// <remarks>
    /// The entry point used to know Harmony, Canvas and GameProbe internals. This
    /// class is the only place that patches the game, and it can be removed cleanly
    /// on plugin destruction.
    /// </remarks>
    internal sealed class GameHookInstaller
    {
        private readonly FrameDriver _frames;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;

        internal GameHookInstaller(FrameDriver frames, Action<string> logInfo, Action<string> logWarning)
        {
            _frames = frames;
            _logInfo = logInfo ?? (message => { });
            _logWarning = logWarning ?? (message => { });
        }

        internal void Install()
        {
            InstallFrameHook();
            InstallGameProbe();
        }

        internal void Uninstall()
        {
            try
            {
                FrameHook.Tick = null;
                GameProbe.Tick = null;
            }
            catch (Exception)
            {
                // shutting down; nothing useful to do
            }
        }

        private void InstallFrameHook()
        {
            FrameHook.Tick = () => _frames.Tick("Canvas.SendWillRenderCanvases", Time.frameCount);

            try
            {
                var harmony = new Harmony(FocusPluginInfo.Guid + ".framehook");
                var target = AccessTools.Method(typeof(Canvas), "SendWillRenderCanvases");
                if (target == null)
                {
                    _logWarning("Canvas.SendWillRenderCanvases was not found; falling back to Update only");
                    return;
                }

                harmony.Patch(
                    target,
                    postfix: new HarmonyMethod(
                        AccessTools.Method(typeof(FrameHook), "Postfix")));
                _logInfo(LogEvent.Line("hooks.frame", "target=" + target.FullDescription()));
            }
            catch (Exception ex)
            {
                _logWarning(LogEvent.Line("hooks.frame.failed", "reason=" + ex.Message));
            }
        }

        private void InstallGameProbe()
        {
            try
            {
                var targets = GameProbe.Install(
                    message => _logInfo(message),
                    message => _logWarning(message));

                if (targets == 0)
                {
                    _logWarning(
                        "no game hook could be installed; mode detection and the deferred " +
                        "overlay object will not work");
                    return;
                }

                var description = GameProbe.Describe();
                GameProbe.Tick = () => _frames.Tick("GameProbe:" + description, Time.frameCount);
                _logInfo(LogEvent.Line("hooks.probe", "active=" + description));
            }
            catch (Exception ex)
            {
                _logWarning(LogEvent.Line("hooks.probe.failed", "reason=" + ex.Message));
            }
        }
    }
}
