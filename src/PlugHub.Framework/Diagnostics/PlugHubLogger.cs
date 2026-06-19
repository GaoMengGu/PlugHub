using System;
using System.IO;
using System.Text;
using PlugHub.Contracts.Modules;

namespace PlugHub.Framework.Diagnostics
{
    public sealed class PlugHubLogger
    {
        private static readonly object Sync = new object();
        private const int RetentionDays = 3;

        public void Error(
            string baseDirectory,
            string code,
            string moduleId,
            string featureId,
            string operation,
            string message,
            Exception exception)
        {
            Write(baseDirectory, new PlugHubLogEntry
            {
                Severity = DiagnosticSeverity.Error,
                Code = code ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                FeatureId = featureId ?? string.Empty,
                Operation = operation ?? string.Empty,
                Message = message ?? string.Empty,
                Exception = exception?.ToString() ?? string.Empty
            });
        }

        public void Write(string baseDirectory, PlugHubLogEntry entry)
        {
            try
            {
                if (entry == null) return;
                if (entry.TimestampUtc == default(DateTime))
                {
                    entry.TimestampUtc = DateTime.UtcNow;
                }

                var logsDirectory = LogsDirectory(baseDirectory);
                Directory.CreateDirectory(logsDirectory);
                var logPath = Path.Combine(logsDirectory, "plughub-" + entry.TimestampUtc.ToString("yyyyMMdd") + ".log");
                var line = Format(entry);

                lock (Sync)
                {
                    DeleteExpiredLogs(logsDirectory, entry.TimestampUtc.Date);
                    File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never block command execution.
            }
        }

        private static void DeleteExpiredLogs(string logsDirectory, DateTime utcToday)
        {
            var oldestKeptDay = utcToday.AddDays(-(RetentionDays - 1));
            foreach (var path in Directory.GetFiles(logsDirectory, "plughub-*.log"))
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var dateText = fileName.StartsWith("plughub-", StringComparison.OrdinalIgnoreCase)
                    ? fileName.Substring("plughub-".Length)
                    : string.Empty;
                if (!DateTime.TryParseExact(dateText, "yyyyMMdd", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var logDay))
                {
                    continue;
                }

                if (logDay.Date < oldestKeptDay)
                {
                    TryDeleteFile(path);
                }
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }

        private static string Format(PlugHubLogEntry entry)
        {
            return string.Join(
                "\t",
                new[]
                {
                    entry.TimestampUtc.ToString("o"),
                    Normalize(entry.Severity.ToString()),
                    Normalize(entry.Code),
                    Normalize(entry.ModuleId),
                    Normalize(entry.FeatureId),
                    Normalize(entry.Operation),
                    Normalize(SensitiveTextRedactor.Redact(entry.Message)),
                    Normalize(SensitiveTextRedactor.Redact(entry.Exception))
                });
        }

        public static string LogsDirectory(string baseDirectory)
        {
            var root = string.IsNullOrWhiteSpace(baseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : baseDirectory;

            if (string.IsNullOrWhiteSpace(root))
            {
                root = Environment.CurrentDirectory;
            }

            var preferred = Path.Combine(Path.GetFullPath(root), "logs");
            if (TryEnsureDirectory(preferred))
            {
                return preferred;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var fallbackRoot = string.IsNullOrWhiteSpace(localAppData)
                ? Environment.CurrentDirectory
                : localAppData;
            var fallback = Path.Combine(fallbackRoot, "PlugHub", "logs");
            return TryEnsureDirectory(fallback) ? fallback : preferred;
        }

        private static bool TryEnsureDirectory(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value
                .Replace("\t", " ")
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }
    }
}
