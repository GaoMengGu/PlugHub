using System;
using System.Collections.Generic;
using System.Linq;
using PlugHub.Framework.Configuration;

namespace PlugHub.Manager.Settings
{
    public static class SettingsMetrics
    {
        public static int CountUniqueModules(IEnumerable<ModuleConfiguration> modules)
        {
            return (modules ?? Enumerable.Empty<ModuleConfiguration>())
                .Select(module => module?.Id ?? string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        public static int CountUniqueFeatures(IEnumerable<ModuleConfiguration> modules)
        {
            return (modules ?? Enumerable.Empty<ModuleConfiguration>())
                .SelectMany(module => module?.Features ?? new List<FeatureConfiguration>())
                .Select(feature => feature?.Id ?? string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        public static int CountEnabledRepositories(IEnumerable<PackageRepositoryConfiguration> repositories)
        {
            return (repositories ?? Enumerable.Empty<PackageRepositoryConfiguration>())
                .Count(repository => repository != null && repository.Enabled);
        }

        public static string RepositoryDisplayName(PackageRepositoryConfiguration repository)
        {
            if (repository == null) return string.Empty;
            return RepositoryDisplayName(repository.DisplayName, repository.Id, repository.Repository);
        }

        public static string RepositoryDisplayName(string customName, string id, string repository)
        {
            if (!string.IsNullOrWhiteSpace(customName)) return customName.Trim();
            if (!string.IsNullOrWhiteSpace(id)) return id.Trim();
            if (!string.IsNullOrWhiteSpace(repository))
            {
                return repository.Trim().TrimEnd('/').Split('/').LastOrDefault() ?? repository.Trim();
            }

            return "未命名仓库";
        }
    }
}
