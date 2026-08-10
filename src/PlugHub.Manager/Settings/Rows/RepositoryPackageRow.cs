using System;
using System.Collections.Generic;
using System.Linq;
using PlugHub.Framework.Packages;

namespace PlugHub.Manager.Settings.Rows
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
        public string RepositoryDisplayName { get; set; } = string.Empty;
        public int StatusPriority { get; set; } = 90;
        public string PrimaryAction { get; set; } = string.Empty;
        public string PrimaryActionLabel { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public string TagsText { get; set; } = string.Empty;
        public string CategoryText { get; set; } = string.Empty;
        public List<string> TagBadges { get; set; } = new List<string>();
        public string Description { get; set; } = string.Empty;

        public static RepositoryPackageRow FromDescriptor(RepositoryPackageDescriptor descriptor, bool isRevitHostRunning, bool isLoadedInCurrentRuntime)
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
                Description = descriptor.Description,
                TagsText = string.Join(", ", descriptor.Tags ?? new List<string>()),
                CategoryText = string.Join(", ", descriptor.Categories ?? new List<string>()),
                TagBadges = (descriptor.Categories ?? new List<string>())
                    .Concat(descriptor.Tags ?? new List<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToList(),
                InstallState = RepositoryPackageInstallState.Resolve(descriptor.IsInstalled, descriptor.Version, descriptor.InstalledVersion, descriptor.PendingOperation, isRevitHostRunning, isLoadedInCurrentRuntime)
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
                Description = Description ?? string.Empty,
                IsInstalled = IsInstalled
            };
        }

    }
}
