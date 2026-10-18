using System;
using System.IO;

namespace ChillFocused.Core
{
    /// <summary>
    /// Warns when a second copy of the plugin DLL is present under plugins/.
    /// </summary>
    internal static class PluginEnvironmentReport
    {
        internal static void ReportForeignCopies(string pluginRoot, string selfPath,
                                                 Action<string> logWarning, Action<string> logDebug)
        {
            try
            {
                if (string.IsNullOrEmpty(pluginRoot) || !Directory.Exists(pluginRoot))
                {
                    return;
                }

                var dlls = Directory.GetFiles(pluginRoot, "*.dll", SearchOption.AllDirectories);
                var foreign = PluginInstallCheck.ForeignCopies(dlls, selfPath);
                var warning = PluginInstallCheck.Warning(foreign, selfPath);
                if (warning != null)
                {
                    logWarning(warning);
                }
            }
            catch (Exception ex)
            {
                logDebug("install check skipped: " + ex.Message);
            }
        }
    }
}
