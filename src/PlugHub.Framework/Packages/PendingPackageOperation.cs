using System;
using System.Collections.Generic;

namespace PlugHub.Framework.Packages
{
    public sealed class PendingPackageOperation
    {
        public string Operation { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string InstallDirectory { get; set; } = string.Empty;
        public string StagingDirectory { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public List<PendingManifestBackup> ManifestBackups { get; set; } = new List<PendingManifestBackup>();

        public static PendingPackageOperation Delete(string packageId, string moduleId, string installDirectory)
        {
            return new PendingPackageOperation
            {
                Operation = "delete",
                PackageId = packageId ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                InstallDirectory = installDirectory ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }

        public static PendingPackageOperation Update(string packageId, string moduleId, string installDirectory, string stagingDirectory)
        {
            return new PendingPackageOperation
            {
                Operation = "update",
                PackageId = packageId ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                InstallDirectory = installDirectory ?? string.Empty,
                StagingDirectory = stagingDirectory ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }

        public static PendingPackageOperation Restart(string packageId, string moduleId, string installDirectory)
        {
            return new PendingPackageOperation
            {
                Operation = "restart",
                PackageId = packageId ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                InstallDirectory = installDirectory ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }
    }
}
