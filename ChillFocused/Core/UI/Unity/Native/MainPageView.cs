using System;

namespace ChillFocused.Core
{
    /// <summary>Layout for the main ChillFocus page.</summary>
    internal sealed class MainPageView
    {
        internal void Build(NativeRowFactory rows, AppRowView appRows, ProcessIconView icons,
                            ChillFocusPageActions actions, FocusStatus status,
                            Action openProcesses, Action changed)
        {
            rows.Add(rows.Section(ChillFocusCopy.PageTitle));
            rows.Add(rows.Status(ChillFocusCopy.Status, ChillFocusCopy.Connection(status.Connected)));
            rows.Add(rows.Status(ChillFocusCopy.Version, status.Version));
            rows.Add(rows.Status(ChillFocusCopy.PausedCount, ChillFocusCopy.Frozen(status.FrozenCount, status.FrozenTotal)));

            rows.Add(rows.Section(ChillFocusCopy.Settings));
            var toggles = new NativeToggleRow(rows);
            rows.Add(toggles.Build(ChillFocusCopy.Mirror, actions.Mirror,
                                   value => actions.Mirror = value));
            rows.Add(toggles.Build(ChillFocusCopy.FollowGameTimer, status.GateEnabled,
                                   value => actions.SetFollowTimer(value)));
            rows.Add(toggles.Build(ChillFocusCopy.LockTimerControls, actions.LockTimerControls,
                                   value => actions.LockTimerControls = value));
            rows.Add(rows.Status(ChillFocusCopy.Sync, actions.SyncText));

            rows.Add(rows.Divider());
            rows.Add(rows.PrimaryAction(ChillFocusCopy.ManageApps, ChillFocusCopy.Open, openProcesses));
            rows.Add(rows.Section(ChillFocusCopy.PausedApps));

            var names = actions.Blocked.Names();
            foreach (var name in names)
            {
                var id = name;
                rows.Add(appRows.Selected(name, icons.Icon(name),
                    () => { actions.Blocked.Remove(id); changed(); }));
            }
            if (names.Count == 0) rows.Add(rows.Status(ChillFocusCopy.NoApps, string.Empty));
        }
    }
}
