using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PlugHub.Framework.Updates
{
    public sealed class FrameworkUpdateService
    {
        private static readonly Uri DefaultLatestReleaseUri =
            new Uri("https://api.github.com/repos/GaoMengGu/PlugHub/releases/latest");

        private readonly ReleaseClient _releaseClient;
        private readonly ReleaseAssetDownloader _downloader;
        private readonly FrameworkUpdatePackageValidator _validator;
        private readonly Uri _latestReleaseUri;

        public FrameworkUpdateService()
            : this(new ReleaseClient(), new ReleaseAssetDownloader(), new FrameworkUpdatePackageValidator(), DefaultLatestReleaseUri)
        {
        }

        internal FrameworkUpdateService(
            ReleaseClient releaseClient,
            ReleaseAssetDownloader downloader,
            FrameworkUpdatePackageValidator validator,
            Uri latestReleaseUri)
        {
            _releaseClient = releaseClient ?? throw new ArgumentNullException(nameof(releaseClient));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _latestReleaseUri = latestReleaseUri ?? throw new ArgumentNullException(nameof(latestReleaseUri));
        }

        public FrameworkUpdateCheckResult Check(string currentVersion)
        {
            try
            {
                var release = _releaseClient.GetLatest(_latestReleaseUri);
                var latestVersion = NormalizeVersionText(release.TagName);
                var current = NormalizeVersionText(currentVersion);
                var asset = release.Assets.FirstOrDefault(item =>
                    item.Name.StartsWith("PlugHub-Revit2020-", StringComparison.OrdinalIgnoreCase)
                    && item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(latestVersion))
                {
                    return FailureCheck(current, "未能从 latest release 解析版本。");
                }

                if (asset == null || string.IsNullOrWhiteSpace(asset.DownloadUrl))
                {
                    return FailureCheck(current, "latest release 未找到 PlugHub-Revit2020 更新包。");
                }

                var hasUpdate = IsNewerVersion(latestVersion, current);
                return new FrameworkUpdateCheckResult
                {
                    Success = true,
                    HasUpdate = hasUpdate,
                    CurrentVersion = current,
                    LatestVersion = latestVersion,
                    AssetName = asset.Name,
                    AssetDownloadUrl = asset.DownloadUrl,
                    Message = hasUpdate
                        ? "发现框架更新 " + latestVersion + "，可点击更新框架。"
                        : "当前框架已是最新版本。"
                };
            }
            catch (Exception ex)
            {
                return FailureCheck(currentVersion, "检查更新失败：" + ex.Message);
            }
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
                var packagePath = _downloader.Download(checkResult.AssetDownloadUrl, targetDirectory, checkResult.AssetName);
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
                return new FrameworkUpdateDownloadResult
                {
                    Success = false,
                    LatestVersion = checkResult.LatestVersion,
                    Message = "下载框架更新失败：" + ex.Message
                };
            }
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
    }
}
