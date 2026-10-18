using System;
using System.Reflection;

namespace ChillFocused.Core
{
    /// <summary>Identity shared by the plugin, the overlay and the diagnostics.</summary>
    internal static class FocusPluginInfo
    {
        public const string Guid = "com.chillfocused.plugin";
        public const string Name = "ChillFocused";

        /// <summary>
        /// The released version.  Kept in step with &lt;Version&gt; in the project
        /// file by <c>VersionConsistencyTests</c>: the [BepInPlugin] attribute needs a
        /// compile-time constant, so this cannot be read from the assembly, but a
        /// released DLL that lies about its version is worse than a little ceremony.
        /// </summary>
        public const string Version = "0.1.0";

        /// <summary>
        /// The commit this binary was built from, when the build supplied one
        /// (<c>dotnet build -p:SourceRevisionId=$(git rev-parse --short HEAD)</c>).
        /// Empty for a plain local build.
        /// </summary>
        public static string Commit
        {
            get
            {
                var informational = Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                var text = informational == null ? string.Empty : informational.InformationalVersion;
                if (string.IsNullOrEmpty(text))
                {
                    return string.Empty;
                }

                var plus = text.IndexOf('+');
                return plus >= 0 && plus + 1 < text.Length ? text.Substring(plus + 1).Trim() : string.Empty;
            }
        }

        /// <summary>Version plus commit, for logs and the diagnostics line.</summary>
        public static string FullVersion
        {
            get
            {
                var commit = Commit;
                return string.IsNullOrEmpty(commit) ? Version : Version + "+" + commit;
            }
        }
    }
}
