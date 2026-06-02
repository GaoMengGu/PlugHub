using System;
using System.Collections.Generic;
using System.Text;

namespace PlugHub.Updater
{
    internal sealed class UpdaterArguments
    {
        public string PayloadZip { get; private set; } = string.Empty;

        public string InstallDirectory { get; private set; } = string.Empty;

        public string TargetVersion { get; private set; } = string.Empty;

        public int RevitProcessId { get; private set; }

        public static UpdaterArguments Parse(string[] args)
        {
            var values = ReadPairs(args ?? Array.Empty<string>());
            var parsed = new UpdaterArguments
            {
                PayloadZip = Value(values, "/payloadZip", "/payloadZipBase64"),
                InstallDirectory = Value(values, "/installDir", "/installDirBase64"),
                TargetVersion = Value(values, "/targetVersion", "/targetVersionBase64")
            };

            if (values.TryGetValue("/revitProcessId", out var processIdText)
                && int.TryParse(processIdText, out var processId))
            {
                parsed.RevitProcessId = processId;
            }

            if (string.IsNullOrWhiteSpace(parsed.PayloadZip)) throw new ArgumentException("/payloadZip is required.");
            if (string.IsNullOrWhiteSpace(parsed.InstallDirectory)) throw new ArgumentException("/installDir is required.");
            if (string.IsNullOrWhiteSpace(parsed.TargetVersion)) throw new ArgumentException("/targetVersion is required.");
            return parsed;
        }

        private static Dictionary<string, string> ReadPairs(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < args.Length; index++)
            {
                var key = args[index];
                if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = index + 1 < args.Length ? args[++index] : string.Empty;
                values[key] = value;
            }

            return values;
        }

        private static string Value(Dictionary<string, string> values, string plainKey, string base64Key)
        {
            if (values.TryGetValue(base64Key, out var encoded))
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }

            return values.TryGetValue(plainKey, out var plain) ? plain : string.Empty;
        }
    }
}
