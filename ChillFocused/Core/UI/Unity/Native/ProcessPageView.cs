using System;

namespace ChillFocused.Core
{
    /// <summary>Layout for the process-picker subpage inside the same tab.</summary>
    internal sealed class ProcessPageView
    {
        internal void Build(NativeRowFactory rows, AppRowView appRows, ProcessIconView icons,
                            ChillFocusPageActions actions, Action back, Action changed)
        {
            rows.Add(rows.Action(ChillFocusCopy.Back, ChillFocusCopy.Back, back));
            rows.Add(rows.Action(ChillFocusCopy.Refresh, ChillFocusCopy.Refresh,
                () => actions.Processes.Load(changed)));
            rows.Add(rows.Section(ChillFocusCopy.RunningApps));

            var processes = actions.Processes.Items;
            if (processes.Length == 0)
            {
                rows.Add(rows.Action(ChillFocusCopy.RunningApps, actions.Processes.Fetching ? ChillFocusCopy.Loading : ChillFocusCopy.Refresh,
                    () => actions.Processes.Load(changed)));
                return;
            }

            foreach (var item in processes)
            {
                if (item == null || string.IsNullOrEmpty(item.name)) continue;
                var id = item.name;
                var selected = actions.Blocked.Contains(id);
                rows.Add(appRows.Process(item, selected, icons.Icon(id),
                    () => { actions.Blocked.Toggle(id); changed(); }));
            }
        }
    }
}
