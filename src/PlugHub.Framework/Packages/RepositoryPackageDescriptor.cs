using System.Collections.Generic;

namespace PlugHub.Framework.Packages
{
    public sealed class RepositoryPackageDescriptor
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
        public string Description { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> Categories { get; set; } = new List<string>();
        public bool IsInstalled { get; set; }
    }
}
