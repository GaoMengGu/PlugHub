using System;
using System.IO;

namespace PlugHub.Manager.Maintenance
{
    internal enum PlugHubInstallRootOperation
    {
        Update,
        Uninstall
    }

    internal static class PlugHubInstallRootPolicy
    {
        private static readonly string[] RequiredInstallMarkers =
        {
            "PlugHub.Revit2020.dll",
            "PlugHub.Framework.dll",
            "PlugHub.Contracts.dll",
            "PlugHub.Wpf.dll",
            "PlugHub.Manager.exe"
        };

        public static string Validate(string installDirectory, PlugHubInstallRootOperation operation)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new InvalidOperationException("Install directory is required.");
            }

            var fullPath = Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(operation == PlugHubInstallRootOperation.Update
                    ? "Refusing to update an unsafe install directory: " + installDirectory
                    : "Refusing to delete a drive root: " + fullPath);
            }

            if (ContainsInstallMarkers(fullPath) && IsAllowedInstallRootName(fullPath))
            {
                return fullPath;
            }

            throw new InvalidOperationException(operation == PlugHubInstallRootOperation.Update
                ? "Refusing to update a directory that is not a PlugHub install root: " + fullPath
                : "Refusing to delete a directory that is not a PlugHub install root: " + fullPath);
        }

        private static bool ContainsInstallMarkers(string directory)
        {
            return Directory.Exists(directory)
                && Array.TrueForAll(RequiredInstallMarkers, marker => File.Exists(Path.Combine(directory, marker)));
        }

        private static bool IsAllowedInstallRootName(string directory)
        {
            var fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFileName(fullPath), "PlugHub", StringComparison.OrdinalIgnoreCase)) return true;

            var parent = Path.GetDirectoryName(fullPath);
            return string.Equals(Path.GetFileName(fullPath), "Revit2020", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(parent)
                && string.Equals(Path.GetFileName(parent), "dist", StringComparison.OrdinalIgnoreCase);
        }
    }
}
