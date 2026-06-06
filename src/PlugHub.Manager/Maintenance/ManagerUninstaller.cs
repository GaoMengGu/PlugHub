using System;
using System.IO;

namespace PlugHub.Manager.Maintenance
{
    internal sealed class ManagerUninstaller
    {
        private static readonly string[] RequiredInstallMarkers =
        {
            "PlugHub.Revit2020.dll",
            "PlugHub.Framework.dll",
            "PlugHub.Contracts.dll",
            "PlugHub.Wpf.dll",
            "PlugHub.Manager.exe"
        };

        private readonly ManagerMaintenanceLogger _logger;

        public ManagerUninstaller(ManagerMaintenanceLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Run(string installDirectory)
        {
            installDirectory = ValidateInstallDirectory(installDirectory);
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

        private static string ValidateInstallDirectory(string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new InvalidOperationException("Install directory is required.");
            }

            var fullPath = Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete a drive root: " + fullPath);
            }

            if (ContainsPlugHubInstallMarkers(fullPath) && IsAllowedInstallRootName(fullPath))
            {
                return fullPath;
            }

            throw new InvalidOperationException("Refusing to delete a directory that is not a PlugHub install root: " + fullPath);
        }

        private static bool ContainsPlugHubInstallMarkers(string directory)
        {
            return Directory.Exists(directory)
                && Array.TrueForAll(RequiredInstallMarkers, marker => File.Exists(Path.Combine(directory, marker)));
        }

        private static bool IsAllowedInstallRootName(string directory)
        {
            var fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFileName(fullPath), "PlugHub", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var parent = Path.GetDirectoryName(fullPath);
            return string.Equals(Path.GetFileName(fullPath), "Revit2020", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(parent)
                && string.Equals(Path.GetFileName(parent), "dist", StringComparison.OrdinalIgnoreCase);
        }
    }
}
