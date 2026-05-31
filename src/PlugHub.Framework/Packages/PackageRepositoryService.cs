using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Packages
{
    public sealed class PackageRepositoryService
    {
        private const string DefaultPackageManifestName = "package.json";
        private const string AdjacentPackageManifestPattern = "*.package.json";
        private const string PackagesDirectoryName = "packages";
        private const string PendingOperationsFileName = "pending-operations.json";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public IReadOnlyList<RepositoryPackageDescriptor> Browse(string baseDirectory, PackageRepositoryConfiguration repository, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (repository == null) throw new ArgumentNullException(nameof(repository));

            var messages = new List<DiagnosticMessage>();
            diagnostics = messages;

            if (!repository.Enabled)
            {
                AddDiagnostic(messages, repository.Id, "PH-REPOSITORY-DISABLED", "Repository is disabled.");
                return new List<RepositoryPackageDescriptor>();
            }

            if (string.Equals(repository.Visibility, "private", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(repository.ApiKey))
            {
                AddDiagnostic(messages, repository.Id, "PH-REPOSITORY-APIKEY", "Private repository requires apiKey.");
                return new List<RepositoryPackageDescriptor>();
            }

            var cacheDirectory = RepositoryCacheDirectory(baseDirectory, repository);
            if (!SyncRepositoryCache(repository, cacheDirectory, messages))
            {
                return new List<RepositoryPackageDescriptor>();
            }

            var packages = BrowseCached(baseDirectory, repository.Id, cacheDirectory, out var browseDiagnostics);
            diagnostics = messages.Concat(browseDiagnostics).ToList();
            return packages;
        }

        public IReadOnlyList<RepositoryPackageDescriptor> BrowseCached(string baseDirectory, string repositoryId, string cacheDirectory, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (string.IsNullOrWhiteSpace(cacheDirectory)) throw new ArgumentException("Cache directory is required.", nameof(cacheDirectory));

            var messages = new List<DiagnosticMessage>();
            diagnostics = messages;

            var packages = new List<RepositoryPackageDescriptor>();
            foreach (var manifestPath in FindPackageManifests(cacheDirectory))
            {
                packages.AddRange(ReadPackagesFromManifest(manifestPath, repositoryId, baseDirectory));
            }

            if (packages.Count == 0)
            {
                AddDiagnostic(messages, repositoryId, "PH-REPOSITORY-MANIFEST", "No PlugHub package manifests were found in repository.");
            }

            return packages
                .GroupBy(package => package.PackageId + "\n" + package.ModuleId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(package => package.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<RepositoryPackageDescriptor> BrowseCached(string baseDirectory, PackageRepositoryConfiguration repository, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            diagnostics = new List<DiagnosticMessage>();
            var cacheDirectory = RepositoryCacheDirectory(baseDirectory, repository);
            return Directory.Exists(cacheDirectory)
                ? BrowseCached(baseDirectory, repository.Id, cacheDirectory, out diagnostics)
                : new List<RepositoryPackageDescriptor>();
        }

        public bool HasRepositoryCache(string baseDirectory, PackageRepositoryConfiguration repository)
        {
            if (repository == null) return false;
            return Directory.Exists(RepositoryCacheDirectory(baseDirectory, repository));
        }

        public IReadOnlyList<DiagnosticMessage> ApplyPendingOperations(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));

            var diagnostics = new List<DiagnosticMessage>();
            var remaining = new List<PendingPackageOperation>();
            foreach (var operation in ReadPendingOperations(baseDirectory))
            {
                if (string.Equals(operation.Operation, "delete", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryApplyPendingDelete(operation, out var error))
                    {
                        remaining.Add(operation);
                        AddDiagnostic(diagnostics, operation.PackageId, "PH-PACKAGE-PENDING-DELETE", "延迟卸载插件包失败，下次启动会重试: " + error, DiagnosticSeverity.Warning);
                    }

                    continue;
                }

                if (string.Equals(operation.Operation, "update", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryApplyPendingUpdate(baseDirectory, operation, out var error))
                    {
                        remaining.Add(operation);
                        AddDiagnostic(diagnostics, operation.PackageId, "PH-PACKAGE-PENDING-UPDATE", "延迟更新插件包失败，下次启动会重试: " + error, DiagnosticSeverity.Warning);
                    }

                    continue;
                }

                if (string.Equals(operation.Operation, "restart", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddDiagnostic(diagnostics, operation.PackageId, "PH-PACKAGE-PENDING-UNKNOWN", "已忽略未知的延迟插件包操作: " + operation.Operation, DiagnosticSeverity.Warning);
            }

            WritePendingOperations(baseDirectory, remaining);
            return diagnostics;
        }

        public RepositoryPackageDescriptor RefreshInstallState(string baseDirectory, RepositoryPackageDescriptor package)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (package == null) throw new ArgumentNullException(nameof(package));

            var installDirectory = string.IsNullOrWhiteSpace(package.InstallDirectory)
                ? InstalledPackageDirectory(baseDirectory, package.PackageId)
                : package.InstallDirectory;
            var moduleId = FirstNonEmpty(package.ModuleId, package.PackageId);
            package.InstallDirectory = installDirectory;
            package.IsInstalled = IsModuleInstalled(baseDirectory, installDirectory, moduleId);
            package.InstalledVersion = InstalledPackageVersion(baseDirectory, installDirectory, moduleId);
            package.PendingOperation = PendingOperationFor(baseDirectory, package.PackageId, moduleId);
            return package;
        }

        public bool IsInstalled(string baseDirectory, RepositoryPackageDescriptor package, out string installedVersion)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (package == null) throw new ArgumentNullException(nameof(package));

            var installDirectory = string.IsNullOrWhiteSpace(package.InstallDirectory)
                ? InstalledPackageDirectory(baseDirectory, package.PackageId)
                : package.InstallDirectory;
            var moduleId = FirstNonEmpty(package.ModuleId, package.PackageId);
            installedVersion = InstalledPackageVersion(baseDirectory, installDirectory, moduleId);
            return IsModuleInstalled(baseDirectory, installDirectory, moduleId);
        }

        public PackageRepositoryOperationResult Install(string baseDirectory, RepositoryPackageDescriptor package)
        {
            return CopyPackageToInstallRoot(baseDirectory, package, false);
        }

        public PackageRepositoryOperationResult Update(string baseDirectory, RepositoryPackageDescriptor package)
        {
            return CopyPackageToInstallRoot(baseDirectory, package, true);
        }

        public PackageRepositoryOperationResult Uninstall(string baseDirectory, RepositoryPackageDescriptor package)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (package == null) throw new ArgumentNullException(nameof(package));

            var installRoot = InstalledPackagesRoot(baseDirectory);
            var installDirectory = InstalledPackageDirectory(baseDirectory, package.PackageId);
            var moduleId = FirstNonEmpty(package.ModuleId, package.PackageId);
            var installedManifests = InstalledManifestsContainingModule(installRoot, moduleId).ToList();
            if (!Directory.Exists(installDirectory) && installedManifests.Count == 0)
            {
                return PackageRepositoryOperationResult.Failed("插件包尚未安装: " + package.PackageId);
            }

            try
            {
                if (!TryRemoveModuleFromInstalledManifests(installRoot, moduleId, string.Empty, out var cleanedManifests, out var cleanupError))
                {
                    return PackageRepositoryOperationResult.Failed("无法清理插件包清单。请关闭并重启 Revit 后重试: " + cleanupError);
                }

                if (Directory.Exists(installDirectory))
                {
                    if (TryFindLockedFile(installDirectory, out var lockedFile))
                    {
                        QueuePendingOperation(baseDirectory, PendingPackageOperation.Delete(package.PackageId, moduleId, installDirectory));
                        var queuedCleanupMessage = cleanedManifests > 0 ? " 已先从 package.json 移除插件声明。" : string.Empty;
                        return PackageRepositoryOperationResult.Succeeded("插件包已标记为待卸载。当前 DLL 正被 Revit 占用，请重启 Revit 后自动删除: " + package.PackageId + queuedCleanupMessage + " 占用文件: " + lockedFile);
                    }

                    Directory.Delete(installDirectory, true);
                }

                RemovePendingOperations(baseDirectory, package.PackageId, moduleId);
                var cleanupMessage = cleanedManifests > 0 ? " 已同步清理旧整包清单中的同名插件声明。" : string.Empty;
                return PackageRepositoryOperationResult.Succeeded("插件包已卸载。请重启 Revit 让当前会话释放已加载的 DLL: " + package.PackageId + cleanupMessage);
            }
            catch (IOException ex)
            {
                return PackageRepositoryOperationResult.Failed("无法卸载插件包。文件可能正被 Revit 占用，请关闭并重启 Revit 后重试: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return PackageRepositoryOperationResult.Failed("无法卸载插件包。文件可能正被 Revit 占用，请关闭并重启 Revit 后重试: " + ex.Message);
            }
        }

        private PackageRepositoryOperationResult CopyPackageToInstallRoot(string baseDirectory, RepositoryPackageDescriptor package, bool replaceExisting)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (!Directory.Exists(package.SourceDirectory))
            {
                return PackageRepositoryOperationResult.Failed("插件包来源目录不存在: " + package.SourceDirectory);
            }

            var installRoot = InstalledPackagesRoot(baseDirectory);
            var installDirectory = InstalledPackageDirectory(baseDirectory, package.PackageId);
            var moduleId = FirstNonEmpty(package.ModuleId, package.PackageId);
            EnsureUnderDirectory(installRoot, installDirectory);

            var targetDirectoryExists = Directory.Exists(installDirectory);
            var moduleInstalled = IsModuleInstalled(baseDirectory, installDirectory, moduleId);
            if (targetDirectoryExists || moduleInstalled)
            {
                if (!replaceExisting)
                {
                    return PackageRepositoryOperationResult.Failed("插件包已安装。如需覆盖，请使用更新: " + package.PackageId);
                }
            }

            var temporaryRoot = TemporaryPackageRoot(baseDirectory);
            var stagingDirectory = TemporaryPackageDirectory(temporaryRoot, package.PackageId, "installing");
            var backupDirectory = TemporaryPackageDirectory(temporaryRoot, package.PackageId, "backup");

            try
            {
                Directory.CreateDirectory(installRoot);
                Directory.CreateDirectory(temporaryRoot);

                var installResult = InstallPackagePayload(package, stagingDirectory);
                if (!installResult.Success)
                {
                    DeleteDirectoryQuietly(stagingDirectory);
                    return installResult;
                }

                if (replaceExisting && Directory.Exists(installDirectory) && TryFindLockedFile(installDirectory, out var lockedFile))
                {
                    if (!TryRemoveModuleFromInstalledManifests(installRoot, moduleId, string.Empty, out var lockedCleanedManifests, out var lockedCleanupError))
                    {
                        DeleteDirectoryQuietly(stagingDirectory);
                        return PackageRepositoryOperationResult.Failed("插件包更新已暂存，但清理旧插件包清单失败。请关闭并重启 Revit 后重试: " + lockedCleanupError);
                    }

                    QueuePendingOperation(baseDirectory, PendingPackageOperation.Update(package.PackageId, moduleId, installDirectory, stagingDirectory));
                    var lockedCleanupMessage = lockedCleanedManifests > 0 ? " 已先从 package.json 移除旧插件声明。" : string.Empty;
                    return PackageRepositoryOperationResult.Succeeded("插件包已标记为待更新。当前 DLL 正被 Revit 占用，请重启 Revit 后自动替换: " + package.PackageId + lockedCleanupMessage + " 占用文件: " + lockedFile);
                }

                if (Directory.Exists(installDirectory))
                {
                    try
                    {
                        Directory.Move(installDirectory, backupDirectory);
                    }
                    catch (IOException ex)
                    {
                        DeleteDirectoryQuietly(stagingDirectory);
                        return PackageRepositoryOperationResult.Failed("无法更新插件包。文件可能正被 Revit 占用，请关闭并重启 Revit 后重试: " + ex.Message);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        DeleteDirectoryQuietly(stagingDirectory);
                        return PackageRepositoryOperationResult.Failed("无法更新插件包。文件可能正被 Revit 占用，请关闭并重启 Revit 后重试: " + ex.Message);
                    }
                }

                try
                {
                    Directory.Move(stagingDirectory, installDirectory);
                }
                catch (IOException ex)
                {
                    RestorePackageBackup(backupDirectory, installDirectory);
                    DeleteDirectoryQuietly(stagingDirectory);
                    return PackageRepositoryOperationResult.Failed("无法复制插件包文件。旧插件包已保留，请关闭并重启 Revit 后重试: " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    RestorePackageBackup(backupDirectory, installDirectory);
                    DeleteDirectoryQuietly(stagingDirectory);
                    return PackageRepositoryOperationResult.Failed("无法复制插件包文件。旧插件包已保留，请关闭并重启 Revit 后重试: " + ex.Message);
                }

                DeleteDirectoryQuietly(backupDirectory);
                RemovePendingOperations(baseDirectory, package.PackageId, moduleId);
                if (!TryRemoveModuleFromInstalledManifests(installRoot, moduleId, installDirectory, out var cleanedManifests, out var cleanupError))
                {
                    return PackageRepositoryOperationResult.Failed("插件包已写入 packages，但清理旧插件包清单失败。请重启 Revit 后重试: " + cleanupError);
                }

                var cleanupMessage = cleanedManifests > 0 ? " 已同步清理旧整包清单中的重复插件声明。" : string.Empty;
                if (replaceExisting)
                {
                    if (!targetDirectoryExists || cleanedManifests > 0)
                    {
                        QueuePendingOperation(baseDirectory, PendingPackageOperation.Restart(package.PackageId, moduleId, installDirectory));
                        return PackageRepositoryOperationResult.Succeeded("插件包已更新。请重启 Revit 后加载新版本: " + package.PackageId + cleanupMessage);
                    }

                    return PackageRepositoryOperationResult.Succeeded("插件包已更新。未检测到安装目录 DLL 占用，后续点击会从更新后的文件加载: " + package.PackageId + cleanupMessage);
                }

                QueuePendingOperation(baseDirectory, PendingPackageOperation.Restart(package.PackageId, moduleId, installDirectory));
                return PackageRepositoryOperationResult.Succeeded("插件包已安装。请重启 Revit 后加载: " + package.PackageId + cleanupMessage);
            }
            catch (IOException ex)
            {
                RestorePackageBackup(backupDirectory, installDirectory);
                DeleteDirectoryQuietly(stagingDirectory);
                return PackageRepositoryOperationResult.Failed("无法复制插件包文件。文件可能正被 Revit 占用，请关闭并重启 Revit 后重试: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                RestorePackageBackup(backupDirectory, installDirectory);
                DeleteDirectoryQuietly(stagingDirectory);
                return PackageRepositoryOperationResult.Failed("无法复制插件包文件。文件可能正被 Revit 占用，请关闭并重启 Revit 后重试: " + ex.Message);
            }
        }

        private IReadOnlyList<RepositoryPackageDescriptor> ReadPackagesFromManifest(string manifestPath, string repositoryId, string baseDirectory)
        {
            if (!TryReadManifest(manifestPath, out var root, out var modules))
            {
                return new List<RepositoryPackageDescriptor>();
            }

            var sourceDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
            var version = StringValue(root, "version");
            var moduleList = modules.Modules ?? new List<ModuleConfiguration>();

            return moduleList
                .Where(module => !string.IsNullOrWhiteSpace(module.Id))
                .Select(module =>
                {
                    var packageId = module.Id;
                    var installedDirectory = InstalledPackageDirectory(baseDirectory, packageId);
                    var installedVersion = InstalledPackageVersion(baseDirectory, installedDirectory, module.Id);
                    var displayName = RepositoryPackageDisplayName(module, packageId);

                    return new RepositoryPackageDescriptor
                    {
                        RepositoryId = repositoryId ?? string.Empty,
                        PackageId = packageId,
                        ModuleId = module.Id,
                        DisplayName = displayName,
                        Version = version,
                        ManifestPath = manifestPath,
                        SourceDirectory = sourceDirectory,
                        InstallDirectory = installedDirectory,
                        IsInstalled = IsModuleInstalled(baseDirectory, installedDirectory, module.Id),
                        InstalledVersion = installedVersion,
                        PendingOperation = PendingOperationFor(baseDirectory, packageId, module.Id)
                    };
                })
                .ToList();
        }

        private bool SyncRepositoryCache(PackageRepositoryConfiguration repository, string cacheDirectory, ICollection<DiagnosticMessage> diagnostics)
        {
            if (!IsSupportedRepositoryProvider(repository.Provider))
            {
                AddDiagnostic(diagnostics, repository.Id, "PH-REPOSITORY-PROVIDER", "Unsupported repository provider: " + repository.Provider);
                return false;
            }

            if (string.IsNullOrWhiteSpace(repository.Repository))
            {
                AddDiagnostic(diagnostics, repository.Id, "PH-REPOSITORY-URL", "Repository is required.");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cacheDirectory) ?? cacheDirectory);

            var publicUrl = RepositoryUrl(repository, false);
            var authenticatedUrl = RepositoryUrl(repository, true);
            var gitRef = string.IsNullOrWhiteSpace(repository.Ref) ? "main" : repository.Ref.Trim();

            if (!Directory.Exists(Path.Combine(cacheDirectory, ".git")))
            {
                if (!RunGit("clone --quiet --filter=blob:none --depth 1 --sparse --branch " + Quote(gitRef) + " " + Quote(authenticatedUrl) + " " + Quote(cacheDirectory), repository.Id, diagnostics))
                {
                    return false;
                }

                if (!ConfigureSparseCheckout(cacheDirectory, repository.Id, diagnostics))
                {
                    return false;
                }

                RunGit("-C " + Quote(cacheDirectory) + " remote set-url origin " + Quote(publicUrl), repository.Id, diagnostics, false);
                return true;
            }

            ConfigureSparseCheckout(cacheDirectory, repository.Id, diagnostics);
            if (!RunGit("-C " + Quote(cacheDirectory) + " fetch --quiet --depth 1 " + Quote(authenticatedUrl) + " " + Quote(gitRef), repository.Id, diagnostics))
            {
                return false;
            }

            return RunGit("-C " + Quote(cacheDirectory) + " checkout --quiet FETCH_HEAD", repository.Id, diagnostics)
                && ConfigureSparseCheckout(cacheDirectory, repository.Id, diagnostics);
        }

        private static bool ConfigureSparseCheckout(string cacheDirectory, string repositoryId, ICollection<DiagnosticMessage> diagnostics)
        {
            return RunGit("-C " + Quote(cacheDirectory) + " sparse-checkout set --no-cone " + string.Join(" ", SparseCheckoutPatterns().Select(Quote)), repositoryId, diagnostics);
        }

        private static IEnumerable<string> SparseCheckoutPatterns()
        {
            return new[]
            {
                "package.json",
                "*.package.json",
                "**/package.json",
                "**/*.package.json",
                "**/*.dll",
                "**/*.png",
                "**/*.jpg",
                "**/*.jpeg",
                "**/*.ico",
                "**/*.bmp",
                "**/*.webp"
            };
        }

        private static IEnumerable<string> FindPackageManifests(string sourceDirectory)
        {
            if (!Directory.Exists(sourceDirectory)) yield break;

            var rootManifest = Path.Combine(sourceDirectory, DefaultPackageManifestName);
            if (File.Exists(rootManifest))
            {
                yield return rootManifest;
            }

            var manifests = Directory.GetFiles(sourceDirectory, DefaultPackageManifestName, SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(sourceDirectory, AdjacentPackageManifestPattern, SearchOption.AllDirectories))
                .Where(path => !string.Equals(path, rootManifest, StringComparison.OrdinalIgnoreCase))
                .Where(path => path.IndexOf(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var manifest in manifests)
            {
                yield return manifest;
            }
        }

        private static string RepositoryCacheDirectory(string baseDirectory, PackageRepositoryConfiguration repository)
        {
            return Path.Combine(baseDirectory, "repository-cache", SafePathSegment(repository.Id));
        }

        private static string InstalledPackagesRoot(string baseDirectory)
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, PackagesDirectoryName));
        }

        private static string InstalledPackageDirectory(string baseDirectory, string packageId)
        {
            return Path.Combine(InstalledPackagesRoot(baseDirectory), SafePathSegment(packageId));
        }

        private bool IsModuleInstalled(string baseDirectory, string installDirectory, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) return false;
            var installRoot = InstalledPackagesRoot(baseDirectory);
            return InstalledManifestsContainingModule(installRoot, moduleId).Any()
                || ManifestContainsModule(Path.Combine(installDirectory, DefaultPackageManifestName), moduleId);
        }

        private string InstalledPackageVersion(string baseDirectory, string installDirectory, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) return string.Empty;

            var preferredManifest = Path.Combine(installDirectory, DefaultPackageManifestName);
            if (TryReadManifest(preferredManifest, out var root, out var modules)
                && ManifestContainsModule(modules, moduleId))
            {
                return StringValue(root, "version");
            }

            foreach (var manifestPath in InstalledManifestsContainingModule(InstalledPackagesRoot(baseDirectory), moduleId))
            {
                if (TryReadManifest(manifestPath, out root, out modules)
                    && ManifestContainsModule(modules, moduleId))
                {
                    return StringValue(root, "version");
                }
            }

            return string.Empty;
        }

        private IEnumerable<string> InstalledManifestsContainingModule(string installRoot, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) yield break;

            foreach (var manifestPath in FindPackageManifests(installRoot))
            {
                if (ManifestContainsModule(manifestPath, moduleId))
                {
                    yield return manifestPath;
                }
            }
        }

        private bool ManifestContainsModule(string manifestPath, string moduleId)
        {
            return TryReadManifest(manifestPath, out _, out var modules)
                && ManifestContainsModule(modules, moduleId);
        }

        private static bool ManifestContainsModule(ModulesConfiguration modules, string moduleId)
        {
            return (modules.Modules ?? new List<ModuleConfiguration>())
                .Any(module => string.Equals(module.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        }

        private static string TemporaryPackageRoot(string baseDirectory)
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, "repository-cache", ".package-install"));
        }

        private static string TemporaryPackageDirectory(string temporaryRoot, string packageId, string suffix)
        {
            return Path.Combine(temporaryRoot, SafePathSegment(packageId) + "." + suffix + "." + Guid.NewGuid().ToString("N"));
        }

        private static void RestorePackageBackup(string backupDirectory, string installDirectory)
        {
            if (!Directory.Exists(backupDirectory) || Directory.Exists(installDirectory)) return;

            try
            {
                Directory.Move(backupDirectory, installDirectory);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void DeleteDirectoryQuietly(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool TryApplyPendingDelete(PendingPackageOperation operation, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(operation.InstallDirectory) || !Directory.Exists(operation.InstallDirectory))
            {
                return true;
            }

            if (TryFindLockedFile(operation.InstallDirectory, out var lockedFile))
            {
                error = "文件仍被占用: " + lockedFile;
                return false;
            }

            try
            {
                Directory.Delete(operation.InstallDirectory, true);
                return true;
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool TryApplyPendingUpdate(string baseDirectory, PendingPackageOperation operation, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(operation.StagingDirectory) || !Directory.Exists(operation.StagingDirectory))
            {
                return true;
            }

            if (TryFindLockedFile(operation.InstallDirectory, out var lockedFile))
            {
                error = "文件仍被占用: " + lockedFile;
                return false;
            }

            var temporaryRoot = TemporaryPackageRoot(baseDirectory);
            var backupDirectory = TemporaryPackageDirectory(temporaryRoot, operation.PackageId, "pending-backup");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(operation.InstallDirectory) ?? InstalledPackagesRoot(baseDirectory));
                if (Directory.Exists(operation.InstallDirectory))
                {
                    Directory.Move(operation.InstallDirectory, backupDirectory);
                }

                Directory.Move(operation.StagingDirectory, operation.InstallDirectory);
                DeleteDirectoryQuietly(backupDirectory);
                return true;
            }
            catch (IOException ex)
            {
                RestorePackageBackup(backupDirectory, operation.InstallDirectory);
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                RestorePackageBackup(backupDirectory, operation.InstallDirectory);
                error = ex.Message;
                return false;
            }
        }

        private void QueuePendingOperation(string baseDirectory, PendingPackageOperation operation)
        {
            var operations = ReadPendingOperations(baseDirectory)
                .Where(item => !SamePendingPackage(item, operation.PackageId, operation.ModuleId))
                .ToList();
            operations.Add(operation);
            WritePendingOperations(baseDirectory, operations);
        }

        private void RemovePendingOperations(string baseDirectory, string packageId, string moduleId)
        {
            var operations = ReadPendingOperations(baseDirectory)
                .Where(item => !SamePendingPackage(item, packageId, moduleId))
                .ToList();
            WritePendingOperations(baseDirectory, operations);
        }

        private string PendingOperationFor(string baseDirectory, string packageId, string moduleId)
        {
            return ReadPendingOperations(baseDirectory)
                .Where(item => SamePendingPackage(item, packageId, moduleId))
                .Select(item => item.Operation ?? string.Empty)
                .FirstOrDefault() ?? string.Empty;
        }

        private static bool SamePendingPackage(PendingPackageOperation operation, string packageId, string moduleId)
        {
            return string.Equals(operation.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(operation.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase);
        }

        private List<PendingPackageOperation> ReadPendingOperations(string baseDirectory)
        {
            var path = PendingOperationsPath(baseDirectory);
            if (!File.Exists(path))
            {
                return new List<PendingPackageOperation>();
            }

            try
            {
                var document = _serializer.Deserialize<PendingPackageOperationsDocument>(File.ReadAllText(path));
                return document?.Operations ?? new List<PendingPackageOperation>();
            }
            catch (Exception)
            {
                return new List<PendingPackageOperation>();
            }
        }

        private void WritePendingOperations(string baseDirectory, IReadOnlyList<PendingPackageOperation> operations)
        {
            var path = PendingOperationsPath(baseDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? TemporaryPackageRoot(baseDirectory));
            if (operations == null || operations.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            File.WriteAllText(path, _serializer.Serialize(new PendingPackageOperationsDocument
            {
                Operations = operations.ToList()
            }));
        }

        private static string PendingOperationsPath(string baseDirectory)
        {
            return Path.Combine(TemporaryPackageRoot(baseDirectory), PendingOperationsFileName);
        }

        private bool TryRemoveModuleFromInstalledManifests(string installRoot, string moduleId, string excludedDirectory, out int cleanedManifests, out string error)
        {
            cleanedManifests = 0;
            error = string.Empty;

            foreach (var manifestPath in InstalledManifestsContainingModule(installRoot, moduleId).ToList())
            {
                if (!string.IsNullOrWhiteSpace(excludedDirectory)
                    && Directory.Exists(excludedDirectory)
                    && IsUnderDirectory(excludedDirectory, manifestPath))
                {
                    continue;
                }

                if (!TryRemoveModuleFromManifest(manifestPath, moduleId, out var changed, out error))
                {
                    return false;
                }

                if (changed)
                {
                    cleanedManifests++;
                }
            }

            return true;
        }

        private bool TryRemoveModuleFromManifest(string manifestPath, string moduleId, out bool changed, out string error)
        {
            changed = false;
            error = string.Empty;

            if (!TryReadManifest(manifestPath, out var root, out _))
            {
                return true;
            }

            var moduleObjects = ArrayValue(root, "modules")
                .OfType<Dictionary<string, object>>()
                .ToList();
            var remainingModules = moduleObjects
                .Where(module => !string.Equals(StringValue(module, "id"), moduleId, StringComparison.OrdinalIgnoreCase))
                .Cast<object>()
                .ToArray();

            if (remainingModules.Length == moduleObjects.Count)
            {
                return true;
            }

            changed = true;
            var manifestDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
            if (remainingModules.Length == 0 && !IsInstalledPackagesRoot(manifestDirectory))
            {
                if (TryDeletePackageDirectoryOrClearManifest(manifestDirectory, manifestPath, root, out error))
                {
                    return true;
                }

                return false;
            }

            root["modules"] = remainingModules;
            return TryWriteManifest(manifestPath, root, out error);
        }

        private bool TryDeletePackageDirectoryOrClearManifest(string packageDirectory, string manifestPath, Dictionary<string, object> root, out string error)
        {
            error = string.Empty;
            if (!TryFindLockedFile(packageDirectory, out var lockedFile))
            {
                try
                {
                    Directory.Delete(packageDirectory, true);
                    return true;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            root["modules"] = new object[0];
            if (TryWriteManifest(manifestPath, root, out error))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(lockedFile))
            {
                error = "文件正被 Revit 或其他进程占用，且无法改写清单: " + lockedFile + " " + error;
            }

            return false;
        }

        private bool TryWriteManifest(string manifestPath, Dictionary<string, object> root, out string error)
        {
            error = string.Empty;
            try
            {
                File.WriteAllText(manifestPath, _serializer.Serialize(root));
                return true;
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsInstalledPackagesRoot(string directory)
        {
            return string.Equals(Path.GetFileName(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), PackagesDirectoryName, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureUnderDirectory(string parentDirectory, string childDirectory)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!child.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Package install path is outside the packages directory: " + childDirectory);
            }
        }

        private PackageRepositoryOperationResult InstallPackagePayload(RepositoryPackageDescriptor package, string installDirectory)
        {
            if (!TryReadManifest(package.ManifestPath, out var root, out var modules))
            {
                return PackageRepositoryOperationResult.Failed("Package manifest could not be read: " + package.ManifestPath);
            }

            var module = FindModule(modules, package);
            if (module == null)
            {
                return PackageRepositoryOperationResult.Failed("Package module was not found in manifest: " + package.ModuleId);
            }

            var moduleObject = FindModuleObject(root, module.Id);
            if (moduleObject == null)
            {
                return PackageRepositoryOperationResult.Failed("Package module was not found in manifest: " + package.ModuleId);
            }

            Directory.CreateDirectory(installDirectory);
            WriteSingleModuleManifest(root, moduleObject, Path.Combine(installDirectory, DefaultPackageManifestName));
            foreach (var relativePath in PayloadPaths(module))
            {
                if (!CopyPayloadFile(package.SourceDirectory, installDirectory, relativePath, out var error))
                {
                    return PackageRepositoryOperationResult.Failed(error);
                }
            }

            return PackageRepositoryOperationResult.Succeeded("Package payload installed.");
        }

        private static ModuleConfiguration? FindModule(ModulesConfiguration modules, RepositoryPackageDescriptor package)
        {
            return (modules.Modules ?? new List<ModuleConfiguration>())
                .FirstOrDefault(item => string.Equals(item.Id, package.ModuleId, StringComparison.OrdinalIgnoreCase))
                ?? (modules.Modules ?? new List<ModuleConfiguration>())
                    .FirstOrDefault(item => string.Equals(item.Id, package.PackageId, StringComparison.OrdinalIgnoreCase));
        }

        private static Dictionary<string, object>? FindModuleObject(Dictionary<string, object> root, string moduleId)
        {
            return ArrayValue(root, "modules")
                .OfType<Dictionary<string, object>>()
                .FirstOrDefault(item => string.Equals(StringValue(item, "id"), moduleId, StringComparison.OrdinalIgnoreCase));
        }

        private void WriteSingleModuleManifest(Dictionary<string, object> root, Dictionary<string, object> moduleObject, string targetManifestPath)
        {
            var manifest = new Dictionary<string, object>
            {
                ["schemaVersion"] = FirstNonEmpty(StringValue(root, "schemaVersion"), "1.0"),
                ["packageDirectories"] = new object[0],
                ["moduleSources"] = new object[0],
                ["repositories"] = new object[0],
                ["conflictPolicy"] = root.TryGetValue("conflictPolicy", out var conflictPolicy) ? conflictPolicy : new Dictionary<string, object>
                {
                    ["duplicateFeatureId"] = "fail-feature",
                    ["duplicateModuleId"] = "fail-module",
                    ["missingModuleType"] = "warn"
                },
                ["modules"] = new object[] { moduleObject }
            };
            CopyOptionalManifestValue(root, manifest, "version");

            File.WriteAllText(targetManifestPath, _serializer.Serialize(manifest));
        }

        private static void CopyOptionalManifestValue(Dictionary<string, object> source, Dictionary<string, object> target, string key)
        {
            if (TryGetValue(source, key, out var value))
            {
                target[key] = value;
            }
        }

        private static IEnumerable<string> PayloadPaths(ModuleConfiguration module)
        {
            var paths = new List<string>();
            AddPayloadPath(paths, module.Assembly);
            foreach (var feature in module.Features ?? new List<FeatureConfiguration>())
            {
                AddPayloadPath(paths, feature.CommandAssembly);
                AddPayloadPath(paths, feature.IconPath);
            }

            return paths.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void AddPayloadPath(ICollection<string> paths, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (Path.IsPathRooted(path)) return;
            paths.Add(path.Trim());
        }

        private static bool CopyPayloadFile(string sourceDirectory, string installDirectory, string relativePath, out string error)
        {
            error = string.Empty;
            var sourceFile = Path.GetFullPath(Path.Combine(sourceDirectory, relativePath));
            var targetFile = Path.GetFullPath(Path.Combine(installDirectory, relativePath));
            if (!IsUnderDirectory(sourceDirectory, sourceFile))
            {
                error = "Package payload path is outside the repository package directory: " + relativePath;
                return false;
            }

            if (!IsUnderDirectory(installDirectory, targetFile))
            {
                error = "Package payload path is outside the install directory: " + relativePath;
                return false;
            }

            if (!File.Exists(sourceFile))
            {
                error = "Package payload file was not found: " + sourceFile;
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? installDirectory);
            File.Copy(sourceFile, targetFile, true);
            return true;
        }

        private static bool IsUnderDirectory(string parentDirectory, string childPath)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryReadManifest(string manifestPath, out Dictionary<string, object> root, out ModulesConfiguration modules)
        {
            root = new Dictionary<string, object>();
            modules = new ModulesConfiguration();
            try
            {
                var text = File.ReadAllText(manifestPath);
                root = _serializer.Deserialize<Dictionary<string, object>>(text);
                if (root == null || !ContainsKey(root, "schemaVersion") || !ContainsKey(root, "modules"))
                {
                    root = new Dictionary<string, object>();
                    return false;
                }

                modules = _serializer.Deserialize<ModulesConfiguration>(text) ?? new ModulesConfiguration();
                return true;
            }
            catch (Exception)
            {
                root = new Dictionary<string, object>();
                modules = new ModulesConfiguration();
                return false;
            }
        }

        private static IEnumerable<object> ArrayValue(Dictionary<string, object> root, string key)
        {
            return TryGetValue(root, key, out var value) && value is System.Collections.ArrayList list
                ? list.Cast<object>()
                : Enumerable.Empty<object>();
        }

        private static bool TryFindLockedFile(string directory, out string lockedFile)
        {
            lockedFile = string.Empty;
            if (!Directory.Exists(directory)) return false;

            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (file.IndexOf(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) continue;

                try
                {
                    using (File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                    }
                }
                catch (IOException)
                {
                    lockedFile = file;
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    lockedFile = file;
                    return true;
                }
            }

            return false;
        }

        private static bool RunGit(string arguments, string repositoryId, ICollection<DiagnosticMessage> diagnostics, bool reportFailure = true)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        if (reportFailure) AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-GIT", "Could not start git process.");
                        return false;
                    }

                    if (!process.WaitForExit(30000))
                    {
                        try { process.Kill(); } catch (Exception) { }
                        if (reportFailure) AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-GIT", "Git operation timed out.");
                        return false;
                    }

                    var error = process.StandardError.ReadToEnd();
                    if (process.ExitCode != 0)
                    {
                        if (reportFailure) AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-GIT", string.IsNullOrWhiteSpace(error) ? "Git operation failed." : error.Trim());
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                if (reportFailure) AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-GIT", ex.Message);
                return false;
            }
        }

        private static string RepositoryUrl(PackageRepositoryConfiguration repository, bool includeCredential)
        {
            var provider = string.Equals(repository.Provider, "gitee", StringComparison.OrdinalIgnoreCase) ? "gitee" : "github";
            var url = repository.Repository.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? repository.Repository.Trim()
                : RepositoryHost(provider) + repository.Repository.Trim().TrimEnd('/') + ".git";

            if (!includeCredential
                || !string.Equals(repository.Visibility, "private", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(repository.ApiKey))
            {
                return url;
            }

            if (provider == "gitee" && url.StartsWith("https://gitee.com/", StringComparison.OrdinalIgnoreCase))
            {
                return "https://oauth2:" + Uri.EscapeDataString(repository.ApiKey.Trim()) + "@" + url.Substring("https://".Length);
            }

            if (provider == "github" && url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                return "https://x-access-token:" + Uri.EscapeDataString(repository.ApiKey.Trim()) + "@" + url.Substring("https://".Length);
            }

            return url;
        }

        private static bool IsSupportedRepositoryProvider(string provider)
        {
            return string.Equals(provider, "github", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "gitee", StringComparison.OrdinalIgnoreCase);
        }

        private static string RepositoryHost(string provider)
        {
            return string.Equals(provider, "gitee", StringComparison.OrdinalIgnoreCase)
                ? "https://gitee.com/"
                : "https://github.com/";
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string SafePathSegment(string value)
        {
            var chars = (value ?? string.Empty)
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_')
                .ToArray();
            var segment = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(segment) ? "package" : segment;
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return TryGetValue(source, key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
        }

        private static bool ContainsKey(Dictionary<string, object> source, string key)
        {
            return source.Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryGetValue(Dictionary<string, object> source, string key, out object value)
        {
            foreach (var item in source)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }

            value = new object();
            return false;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static string RepositoryPackageDisplayName(ModuleConfiguration module, string fallback)
        {
            var featureNames = (module.Features ?? new List<FeatureConfiguration>())
                .OrderBy(feature => feature.Order)
                .ThenBy(feature => feature.Id, StringComparer.OrdinalIgnoreCase)
                .Select(feature => FirstNonEmpty(feature.DisplayName, feature.Name, feature.Id))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            if (featureNames.Count > 0)
            {
                return string.Join("、", featureNames);
            }

            return FirstNonEmpty(module.DisplayName, module.Name, fallback);
        }

        private static void AddDiagnostic(ICollection<DiagnosticMessage> diagnostics, string repositoryId, string code, string message)
        {
            AddDiagnostic(diagnostics, repositoryId, code, message, DiagnosticSeverity.Warning);
        }

        private static void AddDiagnostic(ICollection<DiagnosticMessage> diagnostics, string repositoryId, string code, string message, DiagnosticSeverity severity)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                ModuleId = repositoryId ?? string.Empty,
                Severity = severity,
                Code = code ?? string.Empty,
                Message = message ?? string.Empty
            });
        }
    }

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
        public bool IsInstalled { get; set; }
    }

    public sealed class PendingPackageOperationsDocument
    {
        public List<PendingPackageOperation> Operations { get; set; } = new List<PendingPackageOperation>();
    }

    public sealed class PendingPackageOperation
    {
        public string Operation { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string InstallDirectory { get; set; } = string.Empty;
        public string StagingDirectory { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;

        public static PendingPackageOperation Delete(string packageId, string moduleId, string installDirectory)
        {
            return new PendingPackageOperation
            {
                Operation = "delete",
                PackageId = packageId ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                InstallDirectory = installDirectory ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }

        public static PendingPackageOperation Update(string packageId, string moduleId, string installDirectory, string stagingDirectory)
        {
            return new PendingPackageOperation
            {
                Operation = "update",
                PackageId = packageId ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                InstallDirectory = installDirectory ?? string.Empty,
                StagingDirectory = stagingDirectory ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }

        public static PendingPackageOperation Restart(string packageId, string moduleId, string installDirectory)
        {
            return new PendingPackageOperation
            {
                Operation = "restart",
                PackageId = packageId ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                InstallDirectory = installDirectory ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow.ToString("o")
            };
        }
    }

    public sealed class PackageRepositoryOperationResult
    {
        private PackageRepositoryOperationResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }

        public static PackageRepositoryOperationResult Succeeded(string message)
        {
            return new PackageRepositoryOperationResult(true, message);
        }

        public static PackageRepositoryOperationResult Failed(string message)
        {
            return new PackageRepositoryOperationResult(false, message);
        }
    }
}
