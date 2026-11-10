using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Terminals.Common.Configuration
{
    /// <summary>
    /// Resolves independently installed executables. Explicit paths take precedence over
    /// user and system PATH entries.
    /// </summary>
    public static class ExternalExecutablePaths
    {
        public static string Putty { get; set; }
        public static string Pageant { get; set; }

        public static string Find(string executableName, string configuredPath)
        {
            return Find(executableName, configuredPath,
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine));
        }

        // Explicit PATH arguments permit deterministic filesystem tests.
        public static string Find(string executableName, string configuredPath, string userPath, string machinePath)
        {
            if (string.IsNullOrWhiteSpace(executableName) ||
                Path.GetFileName(executableName) != executableName ||
                !executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("An executable filename is required.", "executableName");

            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                string explicitPath = Environment.ExpandEnvironmentVariables(configuredPath.Trim().Trim('"'));
                if (!Path.IsPathRooted(explicitPath) || !File.Exists(explicitPath))
                    throw new FileNotFoundException("Configured " + executableName +
                        " was not found. Update its location under Options > Plugins.", explicitPath);
                return Path.GetFullPath(explicitPath);
            }

            var inspected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> directories = SplitPath(userPath)
                .Concat(SplitPath(machinePath));

            foreach (string entry in directories)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;
                try
                {
                    string directory = Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"'));
                    if (!Path.IsPathRooted(directory))
                        continue;

                    directory = Path.GetFullPath(directory);
                    if (!inspected.Add(directory))
                        continue;

                    string candidate = Path.Combine(directory, executableName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (PathTooLongException) { }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }

            return null;
        }

        private static IEnumerable<string> SplitPath(string path)
        {
            return string.IsNullOrEmpty(path) ? Enumerable.Empty<string>() : path.Split(';');
        }

        public static string Require(string executableName, string configuredPath)
        {
            string resolved = Find(executableName, configuredPath);
            if (resolved == null)
                throw new FileNotFoundException(executableName +
                    " could not be found in the user or system PATH. " +
                    "Configure its location under Options > Plugins.");
            return resolved;
        }
    }
}
