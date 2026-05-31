using System;
using PlugHub.Framework.Packages;

namespace PlugHub.Revit2020.Settings.Rows
{
    internal sealed class RepositoryPackageRow
    {
        public string RepositoryId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ManifestPath { get; set; } = string.Empty;
        public string SourceDirectory { get; set; } = string.Empty;
        public string InstallDirectory { get; set; } = string.Empty;
        public string InstalledVersion { get; set; } = string.Empty;
        public string PendingOperation { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public string InstallState { get; set; } = string.Empty;

        public static RepositoryPackageRow FromDescriptor(RepositoryPackageDescriptor descriptor, bool isLoadedInCurrentRuntime)
        {
            return new RepositoryPackageRow
            {
                RepositoryId = descriptor.RepositoryId,
                PackageId = descriptor.PackageId,
                ModuleId = descriptor.ModuleId,
                DisplayName = descriptor.DisplayName,
                Version = descriptor.Version,
                ManifestPath = descriptor.ManifestPath,
                SourceDirectory = descriptor.SourceDirectory,
                InstallDirectory = descriptor.InstallDirectory,
                InstalledVersion = descriptor.InstalledVersion,
                PendingOperation = descriptor.PendingOperation,
                IsInstalled = descriptor.IsInstalled,
                InstallState = InstallStateFor(descriptor.IsInstalled, descriptor.Version, descriptor.InstalledVersion, descriptor.PendingOperation, isLoadedInCurrentRuntime)
            };
        }

        public RepositoryPackageDescriptor ToDescriptor()
        {
            return new RepositoryPackageDescriptor
            {
                RepositoryId = RepositoryId ?? string.Empty,
                PackageId = PackageId ?? string.Empty,
                ModuleId = ModuleId ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                Version = Version ?? string.Empty,
                ManifestPath = ManifestPath ?? string.Empty,
                SourceDirectory = SourceDirectory ?? string.Empty,
                InstallDirectory = InstallDirectory ?? string.Empty,
                InstalledVersion = InstalledVersion ?? string.Empty,
                PendingOperation = PendingOperation ?? string.Empty,
                IsInstalled = IsInstalled
            };
        }

        public static string InstallStateFor(bool isInstalled, string version, string installedVersion, string pendingOperation, bool isLoadedInCurrentRuntime)
        {
            if (string.Equals(pendingOperation, "delete", StringComparison.OrdinalIgnoreCase)) return "待重启卸载";
            if (string.Equals(pendingOperation, "update", StringComparison.OrdinalIgnoreCase)) return "待重启更新";
            if (string.Equals(pendingOperation, "restart", StringComparison.OrdinalIgnoreCase) && isInstalled) return "已安装待重启";
            if (!isInstalled && isLoadedInCurrentRuntime) return "待重启卸载";
            if (!isInstalled) return "未安装";
            if (!isLoadedInCurrentRuntime) return "已安装待重启";
            if (!string.IsNullOrWhiteSpace(version)
                && !string.IsNullOrWhiteSpace(installedVersion)
                && !string.Equals(version, installedVersion, StringComparison.OrdinalIgnoreCase))
            {
                return "可更新";
            }

            return "已安装";
        }
    }
}
