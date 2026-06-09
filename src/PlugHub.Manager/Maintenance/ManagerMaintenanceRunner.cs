using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace PlugHub.Manager.Maintenance
{
    internal sealed class ManagerMaintenanceRunner
    {
        public int Run(ManagerMaintenanceArguments args)
        {
            var logger = new ManagerMaintenanceLogger(args.InstallDirectory);
            try
            {
                if (args.Mode == ManagerMaintenanceMode.Update)
                {
                    new ManagerFrameworkUpdater(logger).Run(args);
                    MessageBox.Show("PlugHub was updated successfully.", "PlugHub Manager - Update", MessageBoxButton.OK, MessageBoxImage.Information);
                    return 0;
                }

                if (args.Mode == ManagerMaintenanceMode.Uninstall)
                {
                    return RunUninstall(args, logger);
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.Error("PlugHub Manager maintenance failed.", ex);
                if (args.Mode == ManagerMaintenanceMode.Update)
                {
                    MessageBox.Show(ex.Message, "PlugHub Manager - Update", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (args.Mode == ManagerMaintenanceMode.Uninstall)
                {
                    MessageBox.Show(ex.Message, "PlugHub Manager - Uninstall", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return 1;
            }
        }

        private static int RunUninstall(ManagerMaintenanceArguments args, ManagerMaintenanceLogger logger)
        {
            var confirmation = MessageBox.Show(
                "Uninstall PlugHub from this computer?\n\nInstall directory: " + args.InstallDirectory,
                "PlugHub Manager - Uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
            {
                return 0;
            }

            WaitForProcesses(args.WaitProcessIds, logger);
            new ManagerUninstaller(logger).Run(args.InstallDirectory);
            MessageBox.Show("PlugHub was uninstalled successfully.", "PlugHub Manager - Uninstall", MessageBoxButton.OK, MessageBoxImage.Information);
            return 0;
        }

        private static void WaitForProcesses(System.Collections.Generic.IEnumerable<int> processIds, ManagerMaintenanceLogger logger)
        {
            var currentProcessId = Process.GetCurrentProcess().Id;
            foreach (var processId in (processIds ?? Enumerable.Empty<int>()).Distinct())
            {
                if (processId <= 0 || processId == currentProcessId) continue;
                try
                {
                    var process = Process.GetProcessById(processId);
                    logger.Info("Waiting for process to exit before uninstall: " + processId);
                    process.WaitForExit();
                }
                catch (ArgumentException)
                {
                    logger.Info("Process already exited before uninstall: " + processId);
                }
            }
        }
    }
}
