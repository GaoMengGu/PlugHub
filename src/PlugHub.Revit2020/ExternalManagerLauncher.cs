using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    internal sealed class ExternalManagerLauncher
    {
        private const string ManagerFileName = "PlugHub.Manager.exe";

        public bool TryLaunch(string configDirectory, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (string.IsNullOrWhiteSpace(configDirectory))
            {
                diagnostic = "PlugHub config directory is empty.";
                return false;
            }

            var appPath = CandidateDirectories(configDirectory)
                .Select(directory => Path.Combine(directory, ManagerFileName))
                .FirstOrDefault(File.Exists);

            if (string.IsNullOrWhiteSpace(appPath))
            {
                diagnostic = "PlugHub Manager was not found: " + ManagerFileName;
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = appPath,
                    Arguments = "--config " + QuoteArgument(configDirectory)
                        + " --hostProcessId " + Process.GetCurrentProcess().Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(appPath) ?? string.Empty
                };
                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = ex.Message;
                return false;
            }
        }

        private static IEnumerable<string> CandidateDirectories(string configDirectory)
        {
            var directories = new List<string>();
            AddDirectory(directories, Path.GetDirectoryName(typeof(ExternalManagerLauncher).Assembly.Location));
            AddDirectory(directories, FrameworkRuntimeState.BaseDirectory);
            AddDirectory(directories, Directory.GetParent(configDirectory)?.FullName);
            return directories;
        }

        private static void AddDirectory(ICollection<string> directories, string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return;
            var fullPath = Path.GetFullPath(directory);
            if (directories.Any(existing => string.Equals(existing, fullPath, StringComparison.OrdinalIgnoreCase))) return;
            directories.Add(fullPath);
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
