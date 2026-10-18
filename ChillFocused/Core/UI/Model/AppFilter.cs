using System;

namespace ChillFocused.Core
{
    /// <summary>
    /// The picker's filter predicate.
    /// </summary>
    /// <remarks>
    /// Case-insensitive substring matching, extracted so it can be tested: the filter
    /// box is the only way to find one app among the dozens a desktop session reports.
    /// </remarks>
    public static class AppFilter
    {
        /// <summary>True when the name contains the filter, or no filter is set.</summary>
        public static bool Matches(string filter, string name)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            return name != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
