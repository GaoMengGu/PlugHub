using System;
using System.IO;
using System.Text;

namespace PlugHub.Manager.Maintenance
{
    internal sealed class ManagerMaintenanceLogger
    {
        private readonly string _installDirectory;

        public ManagerMaintenanceLogger(string installDirectory)
        {
            _installDirectory = installDirectory ?? string.Empty;
        }

        public void Info(string message)
        {
            Write("INFO", message, string.Empty);
        }

        public void Error(string message, Exception ex)
        {
            Write("ERROR", message, ex?.ToString() ?? string.Empty);
        }

        private void Write(string severity, string message, string exception)
        {
            var line = string.Join("\t", DateTime.UtcNow.ToString("o"), severity, Normalize(message), Normalize(exception));
            foreach (var directory in CandidateLogsDirectories())
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    var path = Path.Combine(directory, "plughub-manager-maintenance-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
                    File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
                    return;
                }
                catch
                {
                    // Logging must not interrupt update or uninstall maintenance.
                }
            }
        }

        private System.Collections.Generic.IEnumerable<string> CandidateLogsDirectories()
        {
            if (Directory.Exists(_installDirectory))
            {
                yield return Path.Combine(_installDirectory, "logs");
            }

            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PlugHub",
                "logs");
            yield return Path.Combine(Path.GetTempPath(), "PlugHub", "logs");
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        }
    }
}
