using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace PlugHub.Framework.Updates
{
    public sealed class FrameworkUpdateService
    {
        private static readonly IReadOnlyList<FrameworkUpdateSource> DefaultUpdateSources =
            new[]
            {
                new FrameworkUpdateSource(
                    FrameworkUpdateSourceKind.GiteeTagList,
                    "Gitee",
                    new Uri("https://gitee.com/api/v5/repos/GaoMengGu/PlugHub/tags?per_page=100"),
                    "https://gitee.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}"),
                new FrameworkUpdateSource(
                    FrameworkUpdateSourceKind.GitHubLatestRelease,
                    "GitHub",
                    new Uri("https://api.github.com/repos/GaoMengGu/PlugHub/releases/latest"),
                    "https://github.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}")
            };

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
                    return new FrameworkUpdateCheckResult
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
                            ? "发现框架更新 " + latestVersion + "，点击升级图标查看更新信息。"
                            : "当前框架已是最新版本。"
                    };
                }
                catch (Exception ex)
                {
                    failures.Add(source.Name + "：" + ex.Message);
                }
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

        public FrameworkUpdateOperationResult StartUpdater(string installDirectory, string packagePath, string targetVersion, int revitProcessId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    throw new InvalidOperationException("安装目录为空。");
                }

                if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                {
                    throw new FileNotFoundException("框架更新包不存在。", packagePath);
                }

                _validator.Validate(packagePath);
                var updaterPath = Path.Combine(installDirectory, "PlugHub.Updater.exe");
                if (!File.Exists(updaterPath))
                {
                    throw new FileNotFoundException("PlugHub.Updater.exe 不存在。", updaterPath);
                }

                var arguments = string.Join(" ", new[]
                {
                    "/payloadZipBase64", ToBase64(packagePath),
                    "/installDirBase64", ToBase64(installDirectory),
                    "/targetVersionBase64", ToBase64(targetVersion),
                    "/revitProcessId", revitProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });

                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = installDirectory
                });

                return new FrameworkUpdateOperationResult
                {
                    Success = true,
                    Message = "框架更新已准备好。请重启 Revit，关闭后将静默覆盖框架 DLL。"
                };
            }
            catch (Exception ex)
            {
                return new FrameworkUpdateOperationResult
                {
                    Success = false,
                    Message = "启动框架更新失败：" + ex.Message
                };
            }
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
            if (Version.TryParse(TrimVersionPrefix(latestVersion), out var latest)
                && Version.TryParse(TrimVersionPrefix(currentVersion), out var current))
            {
                return latest > current;
            }

            return !string.Equals(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
        }

        private static ReleaseAssetInfo? SelectUpdateAsset(ReleaseInfo release, string latestVersion)
        {
            if (release == null) return null;
            var expectedName = "PlugHub-Revit2020-" + latestVersion + ".zip";
            var exact = release.Assets.FirstOrDefault(item =>
                string.Equals(item.Name, expectedName, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            return release.Assets.FirstOrDefault(item =>
                item.Name.StartsWith("PlugHub-Revit2020-", StringComparison.OrdinalIgnoreCase)
                && item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
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

        private static string SafeSegment(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = (value ?? string.Empty).Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }

        private static string ToBase64(string value)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private enum FrameworkUpdateSourceKind
        {
            GiteeTagList,
            GitHubLatestRelease
        }

        private sealed class FrameworkUpdateSource
        {
            public FrameworkUpdateSource(FrameworkUpdateSourceKind kind, string name, Uri uri, string downloadUrlTemplate)
            {
                Kind = kind;
                Name = name ?? string.Empty;
                Uri = uri ?? throw new ArgumentNullException(nameof(uri));
                DownloadUrlTemplate = downloadUrlTemplate ?? string.Empty;
            }

            public FrameworkUpdateSourceKind Kind { get; }

            public string Name { get; }

            public Uri Uri { get; }

            public string DownloadUrlTemplate { get; }
        }
    }
}
