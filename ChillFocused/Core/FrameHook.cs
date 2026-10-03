using System;
using HarmonyLib;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// A per-frame callback that depends on no GameObject at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The obvious ways to get a per-frame callback -- a MonoBehaviour's
    /// <c>Update</c>, or <c>Application.onBeforeRender</c> -- both turned out to
    /// be unusable here:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// any GameObject created while BepInEx runs its chainloader is created
    /// before the first scene exists, so <c>DontDestroyOnLoad</c> does not stick
    /// and the object is destroyed seconds later, taking every <c>Update</c>,
    /// <c>OnGUI</c> and hotkey with it;
    /// </description></item>
    /// <item><description>
    /// <c>Application.onBeforeRender</c> never fires in this game, which renders
    /// through URP (the event is driven by the built-in pipeline's camera loop).
    /// </description></item>
    /// </list>
    /// <para>
    /// <c>Canvas.SendWillRenderCanvases</c> is called by the native player every
    /// frame for UI rendering. The game is UI-heavy, so it always runs, and a
    /// Harmony patch is static state that no scene change can take away.
    /// </para>
    /// <para>
    /// The callback is wrapped so that a fault can never propagate into the
    /// game's rendering path.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(Canvas), "SendWillRenderCanvases")]
    internal static class FrameHook
    {
        /// <summary>Set by the plugin; invoked once per frame on the main thread.</summary>
        internal static Action Tick;

        [HarmonyPostfix]
        private static void Postfix()
        {
            var tick = Tick;
            if (tick == null)
            {
                return;
            }

            try
            {
                tick();
            }
            catch (Exception)
            {
                // Never let the mod's own failure break the game's UI rendering.
            }
        }
    }
}
