using System.Collections.Generic;

namespace PlugHub.Framework.Updates
{
    public sealed class ReleaseAssetInfo
    {
        public string Name { get; set; } = string.Empty;

        public string DownloadUrl { get; set; } = string.Empty;
    }

    public sealed class ReleaseInfo
    {
        public string TagName { get; set; } = string.Empty;

        public List<ReleaseAssetInfo> Assets { get; set; } = new List<ReleaseAssetInfo>();
    }

    public sealed class FrameworkUpdateCheckResult
    {
        public bool Success { get; set; }

        public bool HasUpdate { get; set; }

        public string CurrentVersion { get; set; } = string.Empty;

        public string LatestVersion { get; set; } = string.Empty;

        public string AssetName { get; set; } = string.Empty;

        public string AssetDownloadUrl { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    public sealed class FrameworkUpdateDownloadResult
    {
        public bool Success { get; set; }

        public string PackagePath { get; set; } = string.Empty;

        public string LatestVersion { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    public sealed class FrameworkUpdateOperationResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
