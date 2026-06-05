namespace PlugHub.Framework.Packages
{
    public sealed class PendingManifestBackup
    {
        public string ManifestPath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
