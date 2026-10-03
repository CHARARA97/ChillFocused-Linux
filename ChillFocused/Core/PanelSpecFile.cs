using System;
using System.Collections.Generic;
using System.IO;

namespace ChillFocused.Core
{
    /// <summary>
    /// Reads the panel layout file, writing a commented template when it is absent.
    /// </summary>
    /// <remarks>
    /// Unity-free so the whole load path, template and clamping behaviour can be
    /// unit tested; only the caller that decides *when* to reload lives in the plugin.
    /// </remarks>
    public static class PanelSpecFile
    {
        public const string FileName = "com.chillfocused.panel.cfg";

        /// <summary>The panel file that sits beside the plugin's own config.</summary>
        public static string DefaultPath(string pluginConfigPath)
        {
            if (string.IsNullOrEmpty(pluginConfigPath))
            {
                return FileName;
            }

            var directory = Path.GetDirectoryName(pluginConfigPath);
            return Path.Combine(string.IsNullOrEmpty(directory) ? "." : directory, FileName);
        }

        /// <summary>Create the file with a commented template if it does not exist.</summary>
        public static bool EnsureExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return false;
                }

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, PanelSpec.Template());
                return true;
            }
            catch (Exception)
            {
                // A read-only config directory must not stop the mod from loading.
                return false;
            }
        }

        /// <summary>The file's timestamp, or <c>MinValue</c> when it cannot be read.</summary>
        public static DateTime Stamp(string path)
        {
            try
            {
                return string.IsNullOrEmpty(path) || !File.Exists(path)
                    ? DateTime.MinValue
                    : File.GetLastWriteTimeUtc(path);
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>Load the file over the defaults, clamping and reporting problems.</summary>
        public static PanelSpec Load(string path, List<string> problems)
        {
            var spec = PanelSpec.Defaults();
            spec.Source = path ?? string.Empty;

            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    spec.Apply(File.ReadAllText(path), problems);
                }
            }
            catch (Exception ex)
            {
                if (problems != null)
                {
                    problems.Add("could not read " + path + ": " + ex.Message);
                }
            }

            spec.Clamp(problems);
            return spec;
        }
    }
}
