using System;
using System.IO;

namespace PlugHub.Manager.Maintenance
{
    internal sealed class ManagerUninstaller
    {
        private readonly ManagerMaintenanceLogger _logger;

        public ManagerUninstaller(ManagerMaintenanceLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Run(string installDirectory)
        {
            installDirectory = PlugHubInstallRootPolicy.Validate(installDirectory, PlugHubInstallRootOperation.Uninstall);
            RemoveAddinManifest();
            if (Directory.Exists(installDirectory))
            {
                Directory.Delete(installDirectory, true);
            }

            _logger.Info("PlugHub uninstalled from: " + installDirectory);
        }

        private static void RemoveAddinManifest()
        {
            var addinPath = AddinManifestPath();
            if (File.Exists(addinPath))
            {
                File.Delete(addinPath);
            }
        }

        private static string AddinManifestPath()
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(programData))
            {
                throw new InvalidOperationException("ProgramData could not be resolved for machine-wide Revit addin registration.");
            }

            return Path.Combine(programData, "Autodesk", "Revit", "Addins", "2020", "PlugHub.addin");
        }

    }
}
