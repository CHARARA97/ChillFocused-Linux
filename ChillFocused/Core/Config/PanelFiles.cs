using System;
using System.IO;

namespace ChillFocused.Core
{
    /// <summary>
    /// Locates and creates the panel layout/text files next to the plugin config.
    /// </summary>
    /// <remarks>
    /// Template creation is a first-run concern, not entry-point logic. Keeping it
    /// here makes the startup path easier to test and keeps Plugin from carrying
    /// file layout details.
    /// </remarks>
    internal sealed class PanelFiles
    {
        internal string SpecPath { get; private set; }
        internal string TextPath { get; private set; }

        internal static PanelFiles Ensure(string configPath, Action<string> logInfo, Action<string> logWarning)
        {
            var files = new PanelFiles
            {
                SpecPath = PanelSpecFile.DefaultPath(configPath),
            };
            files.TextPath = Path.Combine(
                Path.GetDirectoryName(files.SpecPath) ?? ".", "com.chillfocused.text.cfg");

            if (!File.Exists(files.TextPath))
            {
                try
                {
                    File.WriteAllText(files.TextPath, new PanelText().Template());
                    logInfo("wrote a panel text template to " + files.TextPath);
                }
                catch (Exception ex)
                {
                    logWarning("cannot write the panel text file: " + ex.Message);
                }
            }

            logInfo(PanelSpecFile.EnsureExists(files.SpecPath)
                ? "wrote a panel layout template to " + files.SpecPath
                : "panel layout file: " + files.SpecPath);
            return files;
        }
    }
}
