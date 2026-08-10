using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Packages
{
    public sealed class PackageRepositoryService
    {
        private const string DefaultPackageManifestName = "packages.json";
        private const string PackagesDirectoryName = "packages";

        private readonly PendingPackageOperationLifecycle _pendingLifecycle;
        private readonly RepositoryCredentialService _credentialService = new RepositoryCredentialService();
        private readonly PackageManifestReader _manifestReader;
        private readonly RepositoryBrowser _repositoryBrowser;
        private readonly PackageInstallService _packageInstallService;

        public PackageRepositoryService()
        {
            _pendingLifecycle = new PendingPackageOperationLifecycle(new PendingPackageOperationStore());
            _manifestReader = new PackageManifestReader();
            _repositoryBrowser = new RepositoryBrowser(
                _manifestReader,
                _credentialService,
                InstalledPackageVersion,
                IsModuleInstalled,
                _pendingLifecycle.OperationFor);
            _packageInstallService = new PackageInstallService(_manifestReader);
        }

        public IReadOnlyList<RepositoryPackageDescriptor> Browse(string baseDirectory, PackageRepositoryConfiguration repository, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            return _repositoryBrowser.Browse(baseDirectory, repository, out diagnostics);
        }

        public IReadOnlyList<RepositoryPackageDescriptor> BrowseCached(string baseDirectory, string repositoryId, string cacheDirectory, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            return _repositoryBrowser.BrowseCached(baseDirectory, repositoryId, cacheDirectory, out diagnostics);
        }

        public IReadOnlyList<RepositoryPackageDescriptor> BrowseCached(string baseDirectory, PackageRepositoryConfiguration repository, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            return _repositoryBrowser.BrowseCached(baseDirectory, repository, out diagnostics);
        }

        public bool HasRepositoryCache(string baseDirectory, PackageRepositoryConfiguration repository)
        {
            return _repositoryBrowser.HasRepositoryCache(baseDirectory, repository);
        }

        public IReadOnlyList<DiagnosticMessage> ApplyPendingOperations(string baseDirectory)
        {
            return _pendingLifecycle.Apply(baseDirectory);
        }

        public IReadOnlyList<PendingPackageOperation> ListPendingOperations(string baseDirectory)
        {
            return _pendingLifecycle.List(baseDirectory);
        }

        public PackageRepositoryOperationResult CancelPendingOperation(string baseDirectory, string packageId, string moduleId)
        {
            return _pendingLifecycle.Cancel(baseDirectory, packageId, moduleId);
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
            package.PendingOperation = _pendingLifecycle.OperationFor(baseDirectory, package.PackageId, moduleId);
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
                if (Directory.Exists(installDirectory) && PendingPackageOperationLifecycle.TryFindLockedFile(installDirectory, out var lockedFile))
                {
                    var manifestBackups = new List<PendingManifestBackup>();
                    if (!TryRemoveModuleFromInstalledManifests(installRoot, moduleId, string.Empty, manifestBackups, false, out var lockedCleanedManifests, out var lockedCleanupError))
                    {
                        return PackageRepositoryOperationResult.Failed("无法清理插件包清单。请关闭并重启 Revit 后重试: " + lockedCleanupError);
                    }

                    var operation = PendingPackageOperation.Delete(package.PackageId, moduleId, installDirectory);
                    operation.ManifestBackups = manifestBackups;
                    _pendingLifecycle.Queue(baseDirectory, operation);
                    var queuedCleanupMessage = lockedCleanedManifests > 0 ? " 已先从 packages.json 移除插件声明。" : string.Empty;
                    return PackageRepositoryOperationResult.Succeeded("插件包已标记为待卸载。当前 DLL 正被 Revit 占用，请重启 Revit 后自动删除: " + package.PackageId + queuedCleanupMessage + " 占用文件: " + lockedFile);
                }

                if (!TryRemoveModuleFromInstalledManifests(installRoot, moduleId, string.Empty, out var cleanedManifests, out var cleanupError))
                {
                    return PackageRepositoryOperationResult.Failed("无法清理插件包清单。请关闭并重启 Revit 后重试: " + cleanupError);
                }

                if (Directory.Exists(installDirectory))
                {
                    Directory.Delete(installDirectory, true);
                }

                _pendingLifecycle.Remove(baseDirectory, package.PackageId, moduleId);
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
            if (targetDirectoryExists)
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

                var installResult = _packageInstallService.InstallPackagePayload(package, stagingDirectory);
                if (!installResult.Success)
                {
                    DeleteDirectoryQuietly(stagingDirectory);
                    return installResult;
                }

                if (replaceExisting && Directory.Exists(installDirectory) && PendingPackageOperationLifecycle.TryFindLockedFile(installDirectory, out var lockedFile))
                {
                    var manifestBackups = new List<PendingManifestBackup>();
                    if (!TryRemoveModuleFromInstalledManifests(installRoot, moduleId, string.Empty, manifestBackups, false, out var lockedCleanedManifests, out var lockedCleanupError))
                    {
                        DeleteDirectoryQuietly(stagingDirectory);
                        return PackageRepositoryOperationResult.Failed("插件包更新已暂存，但清理旧插件包清单失败。请关闭并重启 Revit 后重试: " + lockedCleanupError);
                    }

                    var operation = PendingPackageOperation.Update(package.PackageId, moduleId, installDirectory, stagingDirectory);
                    operation.ManifestBackups = manifestBackups;
                    _pendingLifecycle.Queue(baseDirectory, operation);
                    var lockedCleanupMessage = lockedCleanedManifests > 0 ? " 已先从 packages.json 移除旧插件声明。" : string.Empty;
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
                _pendingLifecycle.Remove(baseDirectory, package.PackageId, moduleId);
                if (!TryRemoveModuleFromInstalledManifests(installRoot, moduleId, installDirectory, out var cleanedManifests, out var cleanupError))
                {
                    return PackageRepositoryOperationResult.Failed("插件包已写入 packages，但清理旧插件包清单失败。请重启 Revit 后重试: " + cleanupError);
                }

                var cleanupMessage = cleanedManifests > 0 ? " 已同步清理旧整包清单中的重复插件声明。" : string.Empty;
                if (replaceExisting)
                {
                    return PackageRepositoryOperationResult.Succeeded("插件包已更新。若 Revit 正在运行，请关闭并重新打开 Revit 后显示新版本: " + package.PackageId + cleanupMessage);
                }

                return PackageRepositoryOperationResult.Succeeded("插件包已安装。若 Revit 正在运行，请关闭并重新打开 Revit 后显示新按钮: " + package.PackageId + cleanupMessage);
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
                return ModuleVersion(modules, moduleId);
            }

            foreach (var manifestPath in InstalledManifestsContainingModule(InstalledPackagesRoot(baseDirectory), moduleId))
            {
                if (TryReadManifest(manifestPath, out root, out modules)
                    && ManifestContainsModule(modules, moduleId))
                {
                    return ModuleVersion(modules, moduleId);
                }
            }

            return string.Empty;
        }

        private IEnumerable<string> InstalledManifestsContainingModule(string installRoot, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) yield break;

            foreach (var manifestPath in _manifestReader.FindPackageManifests(installRoot))
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

        private static string ModuleVersion(ModulesConfiguration modules, string moduleId)
        {
            return (modules.Modules ?? new List<ModuleConfiguration>())
                .FirstOrDefault(module => string.Equals(module.Id, moduleId, StringComparison.OrdinalIgnoreCase))
                ?.Version ?? string.Empty;
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

        private bool TryRemoveModuleFromInstalledManifests(string installRoot, string moduleId, string excludedDirectory, out int cleanedManifests, out string error)
        {
            return TryRemoveModuleFromInstalledManifests(installRoot, moduleId, excludedDirectory, null, out cleanedManifests, out error);
        }

        private bool TryRemoveModuleFromInstalledManifests(
            string installRoot,
            string moduleId,
            string excludedDirectory,
            ICollection<PendingManifestBackup>? manifestBackups,
            out int cleanedManifests,
            out string error)
        {
            return TryRemoveModuleFromInstalledManifests(installRoot, moduleId, excludedDirectory, manifestBackups, true, out cleanedManifests, out error);
        }

        private bool TryRemoveModuleFromInstalledManifests(
            string installRoot,
            string moduleId,
            string excludedDirectory,
            ICollection<PendingManifestBackup>? manifestBackups,
            bool deleteEmptyPackageDirectories,
            out int cleanedManifests,
            out string error)
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

                var originalManifest = string.Empty;
                if (manifestBackups != null)
                {
                    try
                    {
                        originalManifest = File.ReadAllText(manifestPath);
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

                if (!TryRemoveModuleFromManifest(manifestPath, moduleId, deleteEmptyPackageDirectories, out var changed, out error))
                {
                    return false;
                }

                if (changed)
                {
                    cleanedManifests++;
                    manifestBackups?.Add(new PendingManifestBackup
                    {
                        ManifestPath = manifestPath,
                        Content = originalManifest
                    });
                }
            }

            return true;
        }

        private bool TryRemoveModuleFromManifest(string manifestPath, string moduleId, out bool changed, out string error)
        {
            return TryRemoveModuleFromManifest(manifestPath, moduleId, true, out changed, out error);
        }

        private bool TryRemoveModuleFromManifest(string manifestPath, string moduleId, bool deleteEmptyPackageDirectory, out bool changed, out string error)
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
            if (remainingModules.Length == 0 && !IsInstalledPackagesRoot(manifestDirectory) && deleteEmptyPackageDirectory)
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
            if (!PendingPackageOperationLifecycle.TryFindLockedFile(packageDirectory, out var lockedFile))
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
            return _manifestReader.TryWriteManifest(manifestPath, root, out error);
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

        private static bool IsUnderDirectory(string parentDirectory, string childPath)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryReadManifest(string manifestPath, out Dictionary<string, object> root, out ModulesConfiguration modules)
        {
            return _manifestReader.TryReadManifest(manifestPath, out root, out modules);
        }

        private static IEnumerable<object> ArrayValue(Dictionary<string, object> root, string key)
        {
            return TryGetValue(root, key, out var value) && value is System.Collections.ArrayList list
                ? list.Cast<object>()
                : Enumerable.Empty<object>();
        }

        private static string SafePathSegment(string value)
        {
            var chars = (value ?? string.Empty)
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_')
                .ToArray();
            var segment = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(segment) || segment.All(ch => ch == '.') ? "package" : segment;
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return TryGetValue(source, key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
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

    }
}
