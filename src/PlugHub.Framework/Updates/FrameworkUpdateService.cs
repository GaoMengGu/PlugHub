using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace PlugHub.Framework.Updates
{
    public sealed class FrameworkUpdateService
    {
        private const string TestUpdateReleaseUriEnvironmentVariable = "PLUGHUB_TEST_UPDATE_RELEASE_URI";
        private const string TestUpdateDownloadTemplateEnvironmentVariable = "PLUGHUB_TEST_UPDATE_DOWNLOAD_TEMPLATE";

        private static readonly IReadOnlyList<FrameworkUpdateSource> DefaultUpdateSources =
            BuildDefaultUpdateSources();

        private readonly ReleaseClient _releaseClient;
        private readonly ReleaseAssetDownloader _downloader;
        private readonly FrameworkUpdatePackageValidator _validator;
        private readonly IReadOnlyList<FrameworkUpdateSource> _updateSources;

        public FrameworkUpdateService()
            : this(new ReleaseClient(), new ReleaseAssetDownloader(), new FrameworkUpdatePackageValidator(), DefaultUpdateSources)
        {
        }

        internal FrameworkUpdateService(
            ReleaseClient releaseClient,
            ReleaseAssetDownloader downloader,
            FrameworkUpdatePackageValidator validator,
            Uri latestReleaseUri)
            : this(
                releaseClient,
                downloader,
                validator,
                new[]
                {
                    new FrameworkUpdateSource(
                        FrameworkUpdateSourceKind.GitHubLatestRelease,
                        "GitHub",
                        latestReleaseUri,
                        "https://github.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}")
                })
        {
        }

        private FrameworkUpdateService(
            ReleaseClient releaseClient,
            ReleaseAssetDownloader downloader,
            FrameworkUpdatePackageValidator validator,
            IReadOnlyList<FrameworkUpdateSource> updateSources)
        {
            _releaseClient = releaseClient ?? throw new ArgumentNullException(nameof(releaseClient));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _updateSources = updateSources == null || updateSources.Count == 0
                ? throw new ArgumentException("At least one update source is required.", nameof(updateSources))
                : updateSources;
        }

        public FrameworkUpdateCheckResult Check(string currentVersion)
        {
            var failures = new List<string>();
            FrameworkUpdateCheckResult? noUpdateResult = null;
            foreach (var source in _updateSources)
            {
                try
                {
                    var release = LoadRelease(source);
                    var latestVersion = NormalizeVersionText(release.TagName);
                    var current = NormalizeVersionText(currentVersion);

                    if (string.IsNullOrWhiteSpace(latestVersion))
                    {
                        failures.Add(source.Name + " 未能解析版本");
                        continue;
                    }

                    var asset = SelectUpdateAsset(release, latestVersion);
                    if (asset == null || string.IsNullOrWhiteSpace(asset.DownloadUrl))
                    {
                        failures.Add(source.Name + " latest release 未找到 PlugHub-Revit2020 更新包");
                        continue;
                    }

                    var downloadUrls = DownloadFallbackUrls(source, latestVersion, asset.Name, asset.DownloadUrl);
                    var hasUpdate = IsNewerVersion(latestVersion, current);
                    var result = new FrameworkUpdateCheckResult
                    {
                        Success = true,
                        HasUpdate = hasUpdate,
                        CurrentVersion = current,
                        LatestVersion = latestVersion,
                        AssetName = asset.Name,
                        AssetDownloadUrl = downloadUrls.FirstOrDefault() ?? asset.DownloadUrl,
                        AssetDownloadUrls = downloadUrls,
                        ReleaseNotes = release.Body,
                        Message = hasUpdate
                            ? "发现框架更新 " + latestVersion + "，请确认是否更新。"
                            : "当前框架已是最新版本。"
                    };

                    if (hasUpdate || !source.ContinueWhenNoUpdate)
                    {
                        return result;
                    }

                    noUpdateResult = result;
                }
                catch (Exception ex)
                {
                    failures.Add(source.Name + "：" + ex.Message);
                }
            }

            if (noUpdateResult != null)
            {
                return noUpdateResult;
            }

            return FailureCheck(currentVersion, "检查更新失败：" + string.Join("；", failures));
        }

        public FrameworkUpdateDownloadResult Download(FrameworkUpdateCheckResult checkResult)
        {
            if (checkResult == null) throw new ArgumentNullException(nameof(checkResult));
            if (!checkResult.Success || !checkResult.HasUpdate)
            {
                return new FrameworkUpdateDownloadResult { Success = false, Message = "没有可下载的框架更新。" };
            }

            try
            {
                var targetDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PlugHub",
                    "updates",
                    SafeSegment(checkResult.LatestVersion));
                var failures = new List<string>();
                foreach (var downloadUrl in DownloadCandidateUrls(checkResult))
                {
                    try
                    {
                        var packagePath = _downloader.Download(downloadUrl, targetDirectory, checkResult.AssetName);
                        _validator.Validate(packagePath);
                        return new FrameworkUpdateDownloadResult
                        {
                            Success = true,
                            PackagePath = packagePath,
                            LatestVersion = checkResult.LatestVersion,
                            Message = "框架更新已下载，关闭并重新打开 Revit 后生效。"
                        };
                    }
                    catch (Exception ex)
                    {
                        failures.Add(HostName(downloadUrl) + "：" + ex.Message);
                    }
                }

                return new FrameworkUpdateDownloadResult
                {
                    Success = false,
                    LatestVersion = checkResult.LatestVersion,
                    Message = "下载框架更新失败：" + string.Join("；", failures)
                };
            }
            catch (Exception ex)
            {
                return new FrameworkUpdateDownloadResult
                {
                    Success = false,
                    LatestVersion = checkResult.LatestVersion,
                    Message = "下载框架更新失败：" + ex.Message
                };
            }
        }

        private ReleaseInfo LoadRelease(FrameworkUpdateSource source)
        {
            if (source.Kind == FrameworkUpdateSourceKind.GiteeTagList)
            {
                return _releaseClient.GetGiteeTags(source.Uri, source.DownloadUrlTemplate);
            }

            return _releaseClient.GetLatestRelease(source.Uri);
        }

        private static FrameworkUpdateCheckResult FailureCheck(string currentVersion, string message)
        {
            return new FrameworkUpdateCheckResult
            {
                Success = false,
                CurrentVersion = NormalizeVersionText(currentVersion),
                Message = message
            };
        }

        private static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            if (Version.TryParse(ComparableVersionText(latestVersion), out var latest)
                && Version.TryParse(ComparableVersionText(currentVersion), out var current))
            {
                var comparison = latest.CompareTo(current);
                if (comparison != 0)
                {
                    return comparison > 0;
                }

                return IsStableReleaseTag(latestVersion) && IsTestReleaseTag(currentVersion);
            }

            return !string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<FrameworkUpdateSource> BuildDefaultUpdateSources()
        {
            var sources = new List<FrameworkUpdateSource>();
            var testReleaseUri = Environment.GetEnvironmentVariable(TestUpdateReleaseUriEnvironmentVariable);
            if (Uri.TryCreate(testReleaseUri, UriKind.Absolute, out var parsedTestReleaseUri)
                && string.Equals(parsedTestReleaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                sources.Add(new FrameworkUpdateSource(
                    FrameworkUpdateSourceKind.GitHubLatestRelease,
                    "GitHub Test",
                    parsedTestReleaseUri,
                    Environment.GetEnvironmentVariable(TestUpdateDownloadTemplateEnvironmentVariable) ?? string.Empty,
                    true));
            }

            sources.Add(new FrameworkUpdateSource(
                FrameworkUpdateSourceKind.GiteeTagList,
                "Gitee",
                new Uri("https://gitee.com/api/v5/repos/GaoMengGu/PlugHub/tags?per_page=100"),
                "https://gitee.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}"));
            sources.Add(new FrameworkUpdateSource(
                FrameworkUpdateSourceKind.GitHubLatestRelease,
                "GitHub",
                new Uri("https://api.github.com/repos/GaoMengGu/PlugHub/releases/latest"),
                "https://github.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}"));
            return sources;
        }

        private static ReleaseAssetInfo? SelectUpdateAsset(ReleaseInfo release, string latestVersion)
        {
            if (release == null) return null;
            var expectedName = "PlugHub-Revit2020-" + latestVersion + ".zip";
            return release.Assets.FirstOrDefault(item =>
                string.Equals(item.Name, expectedName, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> DownloadFallbackUrls(FrameworkUpdateSource source, string latestVersion, string assetName, string primaryUrl)
        {
            var urls = new List<string>();
            AddUrl(urls, primaryUrl);
            AddUrl(urls, ReleaseClient.CreateGiteeReleaseDownloadUrl(
                "https://gitee.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}",
                latestVersion,
                assetName));
            AddUrl(urls, ReleaseClient.CreateGiteeReleaseDownloadUrl(
                "https://github.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}",
                latestVersion,
                assetName));

            if (!string.IsNullOrWhiteSpace(source.DownloadUrlTemplate))
            {
                AddUrl(urls, ReleaseClient.CreateGiteeReleaseDownloadUrl(source.DownloadUrlTemplate, latestVersion, assetName));
            }

            return urls;
        }

        private static IEnumerable<string> DownloadCandidateUrls(FrameworkUpdateCheckResult checkResult)
        {
            if (checkResult.AssetDownloadUrls != null)
            {
                foreach (var url in checkResult.AssetDownloadUrls)
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        yield return url;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(checkResult.AssetDownloadUrl))
            {
                yield return checkResult.AssetDownloadUrl;
            }
        }

        private static void AddUrl(List<string> urls, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (urls.Any(item => string.Equals(item, url, StringComparison.OrdinalIgnoreCase))) return;
            urls.Add(url);
        }

        private static string HostName(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                ? uri.Host
                : "unknown";
        }

        private static string NormalizeVersionText(string version)
        {
            return string.IsNullOrWhiteSpace(version)
                ? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty
                : version.Trim();
        }

        private static string TrimVersionPrefix(string version)
        {
            return (version ?? string.Empty).Trim().TrimStart('v', 'V');
        }

        private static string ComparableVersionText(string version)
        {
            var text = (version ?? string.Empty).Trim();
            var start = text.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
            return start >= 0 ? text.Substring(start) : TrimVersionPrefix(text);
        }

        private static bool IsStableReleaseTag(string version)
        {
            var text = (version ?? string.Empty).Trim();
            return text.StartsWith("V", StringComparison.OrdinalIgnoreCase)
                && !IsTestReleaseTag(text);
        }

        private static bool IsTestReleaseTag(string version)
        {
            return (version ?? string.Empty).Trim().StartsWith("TV", StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeSegment(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = (value ?? string.Empty).Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private enum FrameworkUpdateSourceKind
        {
            GiteeTagList,
            GitHubLatestRelease
        }

        private sealed class FrameworkUpdateSource
        {
            public FrameworkUpdateSource(FrameworkUpdateSourceKind kind, string name, Uri uri, string downloadUrlTemplate)
                : this(kind, name, uri, downloadUrlTemplate, false)
            {
            }

            public FrameworkUpdateSource(FrameworkUpdateSourceKind kind, string name, Uri uri, string downloadUrlTemplate, bool continueWhenNoUpdate)
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
    }
}
