using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Diagnostics;

namespace PlugHub.Framework.Packages
{
    internal sealed class PendingPackageOperationLifecycle
    {
        private readonly PendingPackageOperationStore _store;

        public PendingPackageOperationLifecycle(PendingPackageOperationStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public IReadOnlyList<DiagnosticMessage> Apply(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));

            var diagnostics = new List<DiagnosticMessage>();
            var remaining = new List<PendingPackageOperation>();
            foreach (var operation in _store.Read(baseDirectory))
            {
                if (string.Equals(operation.Operation, "delete", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryApplyDelete(baseDirectory, operation, out var error))
                    {
                        remaining.Add(operation);
                        AddDiagnostic(diagnostics, operation.PackageId, "PH-PACKAGE-PENDING-DELETE", "延迟卸载插件包失败，下次启动会重试: " + error);
                    }

                    continue;
                }

                if (string.Equals(operation.Operation, "update", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryApplyUpdate(baseDirectory, operation, out var error))
                    {
                        remaining.Add(operation);
                        AddDiagnostic(diagnostics, operation.PackageId, "PH-PACKAGE-PENDING-UPDATE", "延迟更新插件包失败，下次启动会重试: " + error);
                    }

                    continue;
                }

                if (string.Equals(operation.Operation, "restart", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddDiagnostic(diagnostics, operation.PackageId, "PH-PACKAGE-PENDING-UNKNOWN", "已忽略未知的延迟插件包操作: " + operation.Operation);
            }

            _store.Write(baseDirectory, remaining);
            return diagnostics;
        }

        public IReadOnlyList<PendingPackageOperation> List(string baseDirectory)
        {
            return _store.Read(baseDirectory);
        }

        public PackageRepositoryOperationResult Cancel(string baseDirectory, string packageId, string moduleId)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(moduleId))
            {
                return PackageRepositoryOperationResult.Failed("取消待处理插件包操作必须提供 packageId 和 moduleId。");
            }

            var operation = _store.Find(baseDirectory, packageId, moduleId);
            if (operation == null)
            {
                return PackageRepositoryOperationResult.Failed("未找到待处理插件包操作: " + packageId);
            }

            if (!RestoreManifestBackups(baseDirectory, operation, out var restoreError))
            {
                return PackageRepositoryOperationResult.Failed("无法恢复待处理插件包清单: " + restoreError);
            }

            if (string.Equals(operation.Operation, "update", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryValidateStagingDirectory(baseDirectory, operation, out var stagingError))
                {
                    return PackageRepositoryOperationResult.Failed("Invalid pending update staging directory: " + stagingError);
                }

                DeleteDirectoryQuietly(operation.StagingDirectory);
            }

            _store.Remove(baseDirectory, packageId, moduleId);
            return PackageRepositoryOperationResult.Succeeded("已取消待处理插件包操作: " + packageId);
        }

        public void Queue(string baseDirectory, PendingPackageOperation operation)
        {
            _store.AddOrReplace(baseDirectory, operation);
        }

        public void Remove(string baseDirectory, string packageId, string moduleId)
        {
            _store.Remove(baseDirectory, packageId, moduleId);
        }

        public string OperationFor(string baseDirectory, string packageId, string moduleId)
        {
            return _store.Find(baseDirectory, packageId, moduleId)?.Operation ?? string.Empty;
        }

        internal static bool TryFindLockedFile(string directory, out string lockedFile)
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

        private static bool TryApplyDelete(string baseDirectory, PendingPackageOperation operation, out string error)
        {
            error = string.Empty;
            if (!TryValidateInstallDirectory(baseDirectory, operation, out error))
            {
                return false;
            }

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

        private static bool TryApplyUpdate(string baseDirectory, PendingPackageOperation operation, out string error)
        {
            error = string.Empty;
            if (!TryValidateInstallDirectory(baseDirectory, operation, out error)
                || !TryValidateStagingDirectory(baseDirectory, operation, out error))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(operation.StagingDirectory) || !Directory.Exists(operation.StagingDirectory))
            {
                return true;
            }

            if (TryFindLockedFile(operation.InstallDirectory, out var lockedFile))
            {
                error = "文件仍被占用: " + lockedFile;
                return false;
            }

            var backupDirectory = TemporaryPackageDirectory(TemporaryPackageRoot(baseDirectory), operation.PackageId, "pending-backup");
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

        private static bool RestoreManifestBackups(string baseDirectory, PendingPackageOperation operation, out string error)
        {
            error = string.Empty;
            var backups = operation.ManifestBackups ?? new List<PendingManifestBackup>();
            if (backups.Count == 0) return true;

            var installRoot = InstalledPackagesRoot(baseDirectory);
            foreach (var backup in backups.Where(item => item != null))
            {
                if (string.IsNullOrWhiteSpace(backup.ManifestPath)) continue;

                var manifestPath = Path.GetFullPath(backup.ManifestPath);
                if (!IsUnderDirectory(installRoot, manifestPath))
                {
                    error = "Manifest backup path is outside the packages directory: " + backup.ManifestPath;
                    return false;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? installRoot);
                    File.WriteAllText(manifestPath, backup.Content ?? string.Empty);
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

            return true;
        }

        private static bool TryValidateInstallDirectory(string baseDirectory, PendingPackageOperation operation, out string error)
        {
            error = string.Empty;
            if (operation == null)
            {
                error = "Pending operation is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(operation.InstallDirectory))
            {
                error = "Pending operation install directory is missing.";
                return false;
            }

            var installDirectory = Path.GetFullPath(operation.InstallDirectory);
            if (!IsUnderDirectory(InstalledPackagesRoot(baseDirectory), installDirectory))
            {
                error = "Pending operation install directory is outside packages: " + operation.InstallDirectory;
                return false;
            }

            return true;
        }

        private static bool TryValidateStagingDirectory(string baseDirectory, PendingPackageOperation operation, out string error)
        {
            error = string.Empty;
            if (operation == null)
            {
                error = "Pending operation is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(operation.StagingDirectory))
            {
                error = "Pending operation staging directory is missing.";
                return false;
            }

            var stagingDirectory = Path.GetFullPath(operation.StagingDirectory);
            if (!IsUnderDirectory(TemporaryPackageRoot(baseDirectory), stagingDirectory))
            {
                error = "Pending operation staging directory is outside package-install cache: " + operation.StagingDirectory;
                return false;
            }

            return true;
        }

        private static string InstalledPackagesRoot(string baseDirectory)
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, "packages"));
        }

        private static string TemporaryPackageRoot(string baseDirectory)
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, "repository-cache", ".package-install"));
        }

        private static string TemporaryPackageDirectory(string temporaryRoot, string packageId, string suffix)
        {
            return Path.Combine(temporaryRoot, SafePathSegment(packageId) + "." + suffix + "." + Guid.NewGuid().ToString("N"));
        }

        private static string SafePathSegment(string value)
        {
            var chars = (value ?? string.Empty)
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_')
                .ToArray();
            var segment = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(segment) || segment.All(ch => ch == '.') ? "package" : segment;
        }

        private static bool IsUnderDirectory(string parentDirectory, string childPath)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
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

        private static void AddDiagnostic(ICollection<DiagnosticMessage> diagnostics, string packageId, string code, string message)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                ModuleId = packageId ?? string.Empty,
                Severity = DiagnosticSeverity.Warning,
                Code = code ?? string.Empty,
                Message = SensitiveTextRedactor.Redact(message ?? string.Empty)
            });
        }
    }
}
