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
            var directory = ResolveLogsDirectory();
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "plughub-manager-maintenance-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
            var line = string.Join("\t", DateTime.UtcNow.ToString("o"), severity, Normalize(message), Normalize(exception));
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }

        private string ResolveLogsDirectory()
        {
            var preferred = Path.Combine(_installDirectory, "logs");
            if (Directory.Exists(_installDirectory))
            {
                try
                {
                    Directory.CreateDirectory(preferred);
                    return preferred;
                }
                catch
                {
                    // Fall through to per-user logs when the install directory is locked or already removed.
                }
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PlugHub",
                "logs");
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        }
    }
}
