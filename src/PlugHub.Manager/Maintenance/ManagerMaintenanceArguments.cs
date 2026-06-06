using System;
using System.Collections.Generic;
using System.Text;

namespace PlugHub.Manager.Maintenance
{
    internal sealed class ManagerMaintenanceArguments
    {
        public ManagerMaintenanceMode Mode { get; private set; }

        public string PayloadZip { get; private set; } = string.Empty;

        public string InstallDirectory { get; private set; } = string.Empty;

        public string TargetVersion { get; private set; } = string.Empty;

        public IReadOnlyList<int> WaitProcessIds { get; private set; } = Array.Empty<int>();

        public static ManagerMaintenanceArguments Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var waitProcessIds = new List<int>();
            var mode = ManagerMaintenanceMode.None;
            var input = args ?? Array.Empty<string>();

            for (var index = 0; index < input.Length; index++)
            {
                var key = input[index] ?? string.Empty;
                if (string.Equals(key, "/update", StringComparison.OrdinalIgnoreCase))
                {
                    mode = ManagerMaintenanceMode.Update;
                    continue;
                }

                if (string.Equals(key, "/uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    mode = ManagerMaintenanceMode.Uninstall;
                    continue;
                }

                if (!key.StartsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = index + 1 < input.Length ? input[++index] ?? string.Empty : string.Empty;
                if (string.Equals(key, "/waitProcessId", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out var processId) && processId > 0)
                    {
                        waitProcessIds.Add(processId);
                    }

                    continue;
                }

                values[key] = value;
            }

            var parsed = new ManagerMaintenanceArguments
            {
                Mode = mode,
                PayloadZip = Value(values, "/payloadZip", "/payloadZipBase64"),
                InstallDirectory = Value(values, "/installDir", "/installDirBase64"),
                TargetVersion = Value(values, "/targetVersion", "/targetVersionBase64"),
                WaitProcessIds = waitProcessIds
            };

            if (parsed.Mode == ManagerMaintenanceMode.None)
            {
                return parsed;
            }

            if (string.IsNullOrWhiteSpace(parsed.InstallDirectory))
            {
                throw new ArgumentException("/installDir is required.");
            }

            if (parsed.Mode == ManagerMaintenanceMode.Update)
            {
                if (string.IsNullOrWhiteSpace(parsed.PayloadZip)) throw new ArgumentException("/payloadZip is required.");
                if (string.IsNullOrWhiteSpace(parsed.TargetVersion)) throw new ArgumentException("/targetVersion is required.");
            }

            return parsed;
        }

        private static string Value(Dictionary<string, string> values, string plainKey, string base64Key)
        {
            if (values.TryGetValue(base64Key, out var encoded))
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(TrimWrappingQuotes(encoded)));
            }

            return values.TryGetValue(plainKey, out var plain) ? TrimWrappingQuotes(plain) : string.Empty;
        }

        private static string TrimWrappingQuotes(string value)
        {
            value = (value ?? string.Empty).Trim();
            while (value.Length >= 2
                && ((value[0] == '"' && value[value.Length - 1] == '"')
                    || (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                value = value.Substring(1, value.Length - 2).Trim();
            }

            return value;
        }
    }
}
