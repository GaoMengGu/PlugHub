using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PlugHub.Framework.Updates
{
    internal enum FrameworkUpdateSourceKind
    {
        GiteeTagList,
        GitHubLatestRelease,
        GitHubTestPrereleaseList
    }

    internal sealed class FrameworkUpdateSource
    {
        public FrameworkUpdateSource(FrameworkUpdateSourceKind kind, string name, Uri uri, string downloadUrlTemplate, bool continueWhenNoUpdate = false)
        {
            Kind = kind;
            Name = name ?? string.Empty;
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
            DownloadUrlTemplate = downloadUrlTemplate ?? string.Empty;
            ContinueWhenNoUpdate = continueWhenNoUpdate;
        }

        public FrameworkUpdateSourceKind Kind { get; }
        public string Name { get; }
        public Uri Uri { get; }
        public string DownloadUrlTemplate { get; }
        public bool ContinueWhenNoUpdate { get; }
    }

    internal static class FrameworkUpdatePolicy
    {
        private static readonly Uri GitHubReleaseListUri = new Uri("https://api.github.com/repos/GaoMengGu/PlugHub/releases");

        public static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            if (Version.TryParse(ComparableVersionText(latestVersion), out var latest)
                && Version.TryParse(ComparableVersionText(currentVersion), out var current))
            {
                var comparison = latest.CompareTo(current);
                if (comparison != 0) return comparison > 0;
                return IsStableReleaseTag(latestVersion) && IsTestReleaseTag(currentVersion);
            }

            return !string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
        }

        public static IReadOnlyList<FrameworkUpdateSource> BuildCheckSources(string currentVersion, IReadOnlyList<FrameworkUpdateSource> updateSources)
        {
            if (!IsTestReleaseTag(NormalizeVersionText(currentVersion))
                || updateSources.Any(source => source.Kind == FrameworkUpdateSourceKind.GitHubTestPrereleaseList))
            {
                return updateSources;
            }

            var sources = new List<FrameworkUpdateSource>
            {
                new FrameworkUpdateSource(
                    FrameworkUpdateSourceKind.GitHubTestPrereleaseList,
                    "GitHub Test",
                    GitHubReleaseListUri,
                    string.Empty,
                    true)
            };
            sources.AddRange(updateSources);
            return sources;
        }

        public static ReleaseAssetInfo? SelectUpdateAsset(ReleaseInfo release, string latestVersion)
        {
            if (release == null) return null;
            var expectedName = "PlugHub-Revit2020-" + latestVersion + ".zip";
            return release.Assets.FirstOrDefault(item => string.Equals(item.Name, expectedName, StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeVersionText(string version)
        {
            return string.IsNullOrWhiteSpace(version)
                ? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty
                : version.Trim();
        }

        private static string ComparableVersionText(string version)
        {
            var text = (version ?? string.Empty).Trim();
            var start = text.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
            return start >= 0 ? text.Substring(start) : text.TrimStart('v', 'V');
        }

        private static bool IsStableReleaseTag(string version)
        {
            var text = (version ?? string.Empty).Trim();
            return text.StartsWith("V", StringComparison.OrdinalIgnoreCase) && !IsTestReleaseTag(text);
        }

        private static bool IsTestReleaseTag(string version)
        {
            return (version ?? string.Empty).Trim().StartsWith("TV", StringComparison.OrdinalIgnoreCase);
        }
    }
}
