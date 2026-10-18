namespace ChillFocused.Core
{
    internal enum SessionMirrorAction
    {
        None,
        Start,
        Apply,
        Stop
    }

    internal sealed class SessionMirrorDecision
    {
        public SessionMirrorAction Action;
        public SessionPlan Plan;
        public string Reason;
    }

    internal enum SessionOwnership
    {
        Game,
        External
    }

    /// <summary>Pure session ownership decision table.</summary>
    internal static class SessionMirrorPolicy
    {
        internal static SessionMirrorDecision Decide(
            bool mirrorEnabled,
            SessionOwnership owner,
            SessionPlan currentPlan,
            ExternalSessionState state)
        {
            var inactive = new SessionMirrorDecision
            {
                Action = SessionMirrorAction.None,
                Reason = "idle"
            };

            if (!mirrorEnabled)
            {
                if (owner != SessionOwnership.External)
                {
                    return inactive;
                }

                return new SessionMirrorDecision
                {
                    Action = SessionMirrorAction.Stop,
                    Reason = "mirror-disabled"
                };
            }

            if (state == null || !state.Active || !state.Manual)
            {
                if (owner != SessionOwnership.External)
                {
                    return inactive;
                }

                return new SessionMirrorDecision
                {
                    Action = SessionMirrorAction.Stop,
                    Reason = "external-ended"
                };
            }

            var plan = SessionPlanFactory.From(state);
            if (plan == null)
            {
                if (owner != SessionOwnership.External)
                {
                    return new SessionMirrorDecision
                    {
                        Action = SessionMirrorAction.None,
                        Reason = "unsupported-mode"
                    };
                }

                return new SessionMirrorDecision
                {
                    Action = SessionMirrorAction.Stop,
                    Reason = "unsupported-mode"
                };
            }

            if (owner == SessionOwnership.Game)
            {
                return new SessionMirrorDecision
                {
                    Action = SessionMirrorAction.Start,
                    Plan = plan,
                    Reason = "external-started"
                };
            }

            if (currentPlan != null && currentPlan.Equals(plan))
            {
                return new SessionMirrorDecision
                {
                    Action = SessionMirrorAction.None,
                    Reason = "plan-unchanged"
                };
            }

            return new SessionMirrorDecision
            {
                Action = SessionMirrorAction.Apply,
                Plan = plan,
                Reason = "plan-changed"
            };
        }
    }
}
