using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// The one entry point for the timer lock: turns the switch plus the game's timer
    /// reading into a <see cref="TimerControlPlan"/> and applies it to the live UI.
    /// </summary>
    /// <remarks>
    /// Two decisions worth keeping:
    /// <list type="bullet">
    /// <item>Hiding uses a CanvasGroup (alpha 0, not interactive, no raycasts) instead
    /// of SetActive(false). The node stays where the game put it, and the
    /// <c>blocksRaycasts</c> flag is what actually makes the button unclickable.</item>
    /// <item>The game re-enables these buttons when the phase changes, so the hidden
    /// state is re-applied on a cadence and every original value is restored exactly
    /// once the lock is released.</item>
    /// </list>
    /// Looking controls up is <see cref="TimerButtonLookup"/>'s job and the names are
    /// <see cref="TimerControlNames"/>'s; this class only hides and restores.
    /// </remarks>
    internal sealed class TimerControlGuard
    {
        private const float ReapplySeconds = 0.25f;

        private readonly TimerButtonLookup _lookup = new TimerButtonLookup();
        private readonly List<Hidden> _hidden = new List<Hidden>();
        private readonly HashSet<GameObject> _hiddenTargets = new HashSet<GameObject>();
        private readonly HashSet<string> _loggedHides = new HashSet<string>();
        private readonly HashSet<string> _reportedMissing = new HashSet<string>();

        private float _nextApplyAt = -1f;
        private bool _holding;

        internal void Tick(bool enabled, TimerState timer, Action<string> log)
        {
            var plan = TimerControlPlan.From(enabled, timer);
            if (!plan.Any)
            {
                if (_holding) ReleaseAll();
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now < _nextApplyAt) return;
            _nextApplyAt = now + ReapplySeconds;

            if (!_holding)
            {
                _holding = true;
                if (log != null) log("timer-lock: locking (" + plan.Describe() + ")");
            }

            PruneDestroyed();
            if (plan.HidePause) Apply("pause", TimerControlNames.Pause, log);
            if (plan.HideSkip) Apply("skip", TimerControlNames.Skip, log);
            if (plan.HideStop) Apply("stop", TimerControlNames.Stop, log);
        }

        /// <summary>Put every hidden control back. Safe to call when nothing is hidden.</summary>
        internal void ReleaseAll()
        {
            foreach (var entry in _hidden) entry.Restore();
            _hidden.Clear();
            _hiddenTargets.Clear();
            _lookup.Reset();
            _nextApplyAt = -1f;
            _holding = false;
        }

        private void Apply(string group, string[] names, Action<string> log)
        {
            var found = false;
            foreach (var name in names)
            {
                var target = _lookup.FindActive(name);
                if (target == null) continue;
                found = true;
                Hide(target);
                if (log != null && _loggedHides.Add(name))
                {
                    log("timer-lock: hid " + group + "=" + name);
                }
            }

            if (!found) ReportMissing(group, names, log);
        }

        private void Hide(GameObject target)
        {
            if (target == null || !_hiddenTargets.Add(target)) return;
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            _hidden.Add(new Hidden(target, group));
        }

        /// <summary>
        /// The game may destroy a control (mode switch) or park it inactive. Both mean we
        /// must hand its CanvasGroup back, otherwise a reused object keeps alpha 0.
        /// </summary>
        private void PruneDestroyed()
        {
            for (var i = _hidden.Count - 1; i >= 0; i--)
            {
                var target = _hidden[i].Target;
                if (target != null && target.activeInHierarchy) continue;
                _hidden[i].Restore();
                if (target != null) _hiddenTargets.Remove(target);
                _hidden.RemoveAt(i);
            }
        }

        /// <summary>
        /// A missing group means the game renamed or restructured its timer UI. One line
        /// per group is enough; the candidate dump is what makes the fix obvious.
        /// </summary>
        private void ReportMissing(string group, string[] names, Action<string> log)
        {
            if (log == null || !_reportedMissing.Add(group)) return;
            log("timer-lock: no active object for " + group + " (" + string.Join(", ", names) + ")");

            var candidates = _lookup.Candidates();
            if (candidates.Count > 0)
            {
                log("timer-lock: timer-like objects: " + string.Join(", ", candidates.ToArray()));
            }
        }

        /// <summary>One control we dimmed, plus the values it had before.</summary>
        private sealed class Hidden
        {
            private readonly GameObject _target;
            private readonly CanvasGroup _group;
            private readonly float _alpha;
            private readonly bool _interactable;
            private readonly bool _blocksRaycasts;

            internal Hidden(GameObject target, CanvasGroup group)
            {
                _target = target;
                _group = group;
                _alpha = group.alpha;
                _interactable = group.interactable;
                _blocksRaycasts = group.blocksRaycasts;

                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            internal GameObject Target { get { return _target; } }

            internal void Restore()
            {
                if (_target == null || _group == null) return;
                _group.alpha = _alpha;
                _group.interactable = _interactable;
                _group.blocksRaycasts = _blocksRaycasts;
            }
        }
    }
}
