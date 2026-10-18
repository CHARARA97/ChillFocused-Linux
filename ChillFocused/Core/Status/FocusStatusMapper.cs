namespace ChillFocused.Core
{
    /// <summary>
    /// Maps the flat wire response onto the UI/application status snapshot.
    /// </summary>
    /// <remarks>
    /// Keeping this mapping at the boundary means FocusStatus does not depend on
    /// FocusStateResponse (a transport DTO). That keeps the snapshot reusable if the
    /// wire shape changes and makes the mapping itself directly testable.
    /// </remarks>
    internal static class FocusStatusMapper
    {
        internal static FocusStatus Apply(FocusStatus current, FocusStateResponse response)
        {
            var version = string.IsNullOrEmpty(response.version)
                ? current.Version
                : response.version;
            return new FocusStatus(
                connected: true,
                dryRun: response.dry_run,
                degraded: response.degraded,
                version: version,
                gateEnabled: current.GateEnabled,
                gateSatisfied: response.active,
                sessionActive: response.active,
                frozenCount: response.frozen_count,
                frozenTotal: response.frozen_total,
                frozenNames: response.frozen_names,
                recentNames: response.recent_names,
                timer: current.Timer,
                lastWarning: current.LastWarning)
                .WithSession(response.session_mode, response.session_phase,
                    response.session_remaining, response.session_elapsed,
                    response.session_cycles, response.manual_session);
        }
    }
}
