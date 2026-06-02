using System;

namespace PlugHub.Updater
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            UpdaterLogger? logger = null;
            try
            {
                var parsed = UpdaterArguments.Parse(args);
                logger = new UpdaterLogger(parsed.InstallDirectory);
                new FrameworkDllUpdater(logger).Run(parsed);
                return 0;
            }
            catch (Exception ex)
            {
                (logger ?? new UpdaterLogger(string.Empty)).Error("Framework update failed.", ex);
                return 1;
            }
        }
    }
}
