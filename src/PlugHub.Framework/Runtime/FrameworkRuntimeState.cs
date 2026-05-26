using System;

namespace PlugHub.Framework.Runtime
{
    public static class FrameworkRuntimeState
    {
        private static readonly object Sync = new object();

        public static FrameworkRuntimeSnapshot? Current { get; private set; }
        public static string BaseDirectory { get; private set; } = string.Empty;
        public static string ConfigDirectory { get; private set; } = string.Empty;

        public static void SetCurrent(string baseDirectory, string configDirectory, FrameworkRuntimeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            lock (Sync)
            {
                BaseDirectory = baseDirectory ?? string.Empty;
                ConfigDirectory = configDirectory ?? string.Empty;
                Current = snapshot;
            }
        }

        public static FrameworkRuntimeSnapshot Refresh()
        {
            lock (Sync)
            {
                if (string.IsNullOrWhiteSpace(BaseDirectory) || string.IsNullOrWhiteSpace(ConfigDirectory))
                {
                    throw new InvalidOperationException("PlugHub runtime directories are not available.");
                }
            }

            return new FrameworkRuntime().Load(BaseDirectory, ConfigDirectory);
        }
    }
}
