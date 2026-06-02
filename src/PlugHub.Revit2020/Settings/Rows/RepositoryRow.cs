using System;
using PlugHub.Framework.Configuration;

namespace PlugHub.Revit2020.Settings.Rows
{
    internal sealed class RepositoryRow
    {
        private const string DefaultPackageManifestName = "package.json";
        private const string DefaultRepositoryProvider = "gitee";

        public string Id { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string Provider { get; set; } = DefaultRepositoryProvider;
        public string Visibility { get; set; } = "public";
        public string Repository { get; set; } = string.Empty;
        public string Ref { get; set; } = "main";
        public string ManifestPath { get; set; } = DefaultPackageManifestName;
        public string ApiKey { get; set; } = string.Empty;
        public string PlainApiKey { get; set; } = string.Empty;
        public string EncryptedApiKey { get; set; } = string.Empty;
        public string ApiKeyProtection { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public PackageRepositoryConfiguration ToConfiguration()
        {
            return new PackageRepositoryConfiguration
            {
                Id = Id ?? string.Empty,
                Enabled = Enabled,
                Provider = string.IsNullOrWhiteSpace(Provider) ? DefaultRepositoryProvider : Provider,
                Visibility = string.Equals(Visibility, "private", StringComparison.OrdinalIgnoreCase) ? "private" : "public",
                Repository = Repository ?? string.Empty,
                Ref = string.IsNullOrWhiteSpace(Ref) ? "main" : Ref.Trim(),
                ManifestPath = string.IsNullOrWhiteSpace(ManifestPath) ? DefaultPackageManifestName : ManifestPath.Trim(),
                ApiKey = string.IsNullOrWhiteSpace(ApiKey) ? PlainApiKey ?? string.Empty : ApiKey ?? string.Empty,
                EncryptedApiKey = EncryptedApiKey ?? string.Empty,
                ApiKeyProtection = ApiKeyProtection ?? string.Empty
            };
        }
    }
}
