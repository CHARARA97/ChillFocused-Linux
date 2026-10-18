using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChillFocused.Core
{
    /// <summary>
    /// Finds live game objects by name. Owns the scene scan and its cache, so callers
    /// only ever ask a question and never pay for the enumeration themselves.
    /// </summary>
    /// <remarks>
    /// <c>Resources.FindObjectsOfTypeAll</c> walks every loaded object, so the result is
    /// cached for a short while instead of being rebuilt on every re-apply. One second
    /// is short enough that a button created by a phase change is still found quickly.
    /// </remarks>
    internal sealed class TimerButtonLookup
    {
        private const float SnapshotSeconds = 1f;
        private const int MaxCandidates = 20;

        /// <summary>The two containers the timer controls live under, for diagnostics only.</summary>
        private static readonly string[] FacilityNames = { "FacilityPomodoro", "FacilityCountup" };

        private Transform[] _snapshot;
        private float _expire;

        /// <summary>Drop the cache so the next lookup sees objects created since.</summary>
        internal void Reset()
        {
            _snapshot = null;
            _expire = 0f;
        }

        /// <summary>The first active object with this exact name, or null.</summary>
        internal GameObject FindActive(string name)
        {
            var all = Snapshot();
            for (var i = 0; i < all.Length; i++)
            {
                var transform = all[i];
                if (transform == null || transform.name != name) continue;
                var target = transform.gameObject;
                if (target.activeInHierarchy) return target;
            }

            return null;
        }

        /// <summary>
        /// Names of the children under the timer facilities, for the one log line that
        /// says "the game renamed its buttons, here is what it calls them now".
        /// </summary>
        internal List<string> Candidates()
        {
            var found = new List<string>();
            var all = Snapshot();
            for (var i = 0; i < all.Length && found.Count < MaxCandidates; i++)
            {
                var transform = all[i];
                if (transform == null) continue;
                if (Array.IndexOf(FacilityNames, transform.name) < 0) continue;

                foreach (var child in transform.GetComponentsInChildren<Transform>(true))
                {
                    if (found.Count >= MaxCandidates) break;
                    if (child == transform) continue;
                    var label = child.name;
                    if (!child.gameObject.activeInHierarchy) label += " (inactive)";
                    if (!found.Contains(label)) found.Add(label);
                }
            }

            return found;
        }

        private Transform[] Snapshot()
        {
            var now = Time.realtimeSinceStartup;
            if (_snapshot != null && now < _expire) return _snapshot;

            try
            {
                _snapshot = Resources.FindObjectsOfTypeAll<Transform>();
            }
            catch (Exception)
            {
                _snapshot = new Transform[0];
            }

            _expire = now + SnapshotSeconds;
            return _snapshot;
        }
    }
}
