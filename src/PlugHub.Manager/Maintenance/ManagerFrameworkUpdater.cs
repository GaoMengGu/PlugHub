using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using PlugHub.Framework.Updates;

namespace PlugHub.Manager.Maintenance
{
    internal sealed class ManagerFrameworkUpdater
    {
        private const int MaxBackupDirectoriesToKeep = 3;
        private const string InstalledManagerName = "PlugHub.Manager.exe";

        private static readonly string[] RequiredInstallMarkers =
        {
            "PlugHub.Revit2020.dll",
            "PlugHub.Framework.dll",
            "PlugHub.Contracts.dll",
            "PlugHub.Wpf.dll",
            InstalledManagerName
        };

        private static readonly string[] StaleMaintenanceArtifacts =
        {
            "PlugHub.Updater.exe",
            "PlugHub.Updater.exe.config",
            "PlugHub.Updater.pdb",
            "PlugHub-Uninstall.exe",
            "PlugHub.Uninstaller.exe",
            "PlugHub.Uninstaller.exe.config",
            "PlugHub.Uninstaller.pdb"
        };

        private readonly ManagerMaintenanceLogger _logger;

        public ManagerFrameworkUpdater(ManagerMaintenanceLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Run(ManagerMaintenanceArguments args)
        {
            var installDirectory = SafeInstallDirectory(args.InstallDirectory);
            if (!File.Exists(args.PayloadZip))
            {
                throw new FileNotFoundException("Payload zip was not found.", args.PayloadZip);
            }

            new FrameworkUpdatePackageValidator().Validate(args.PayloadZip);
            WaitForProcesses(args.WaitProcessIds);
            CopyFrameworkFiles(args.PayloadZip, installDirectory, args.TargetVersion);
            DeleteStaleMaintenanceArtifacts(installDirectory);
        }

        private void WaitForProcesses(IEnumerable<int> processIds)
        {
            var currentProcessId = Process.GetCurrentProcess().Id;
            foreach (var processId in (processIds ?? Enumerable.Empty<int>()).Distinct())
            {
                if (processId <= 0 || processId == currentProcessId) continue;
                try
                {
                    var process = Process.GetProcessById(processId);
                    _logger.Info("Waiting for process to exit: " + processId);
                    process.WaitForExit();
                }
                catch (ArgumentException)
                {
                    _logger.Info("Process already exited: " + processId);
                }
            }
        }

        private void CopyFrameworkFiles(string payloadZip, string installDirectory, string targetVersion)
        {
            var backupDirectory = Path.Combine(installDirectory, "update-backup", SafeSegment(targetVersion) + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(backupDirectory);
            Directory.SetCreationTimeUtc(backupDirectory, DateTime.UtcNow);
            Directory.SetLastWriteTimeUtc(backupDirectory, DateTime.UtcNow);
            try
            {
                using (var archive = ZipFile.OpenRead(payloadZip))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!ShouldCopyUpdateEntry(entry.FullName)) continue;

                        var name = Path.GetFileName(entry.FullName);
                        var targetPath = Path.Combine(installDirectory, name);
                        var backupPath = Path.Combine(backupDirectory, name);
                        if (File.Exists(targetPath))
                        {
                            File.Copy(targetPath, backupPath, true);
                        }

                        entry.ExtractToFile(targetPath, true);
                    }
                }

                _logger.Info("Framework file update completed: " + targetVersion);
                PruneOldBackups(Path.GetDirectoryName(backupDirectory) ?? installDirectory);
            }
            catch
            {
                RestoreBackup(backupDirectory, installDirectory);
                throw;
            }
        }

        private static bool ShouldCopyUpdateEntry(string entryName)
        {
            var normalized = (entryName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            if (!string.Equals(Path.GetFileName(normalized), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(Path.GetExtension(normalized), ".dll", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, InstalledManagerName, StringComparison.OrdinalIgnoreCase);
        }

        private void DeleteStaleMaintenanceArtifacts(string installDirectory)
        {
            foreach (var name in StaleMaintenanceArtifacts)
            {
                var path = Path.Combine(installDirectory, name);
                if (!File.Exists(path)) continue;

                try
                {
                    File.Delete(path);
                    _logger.Info("Deleted stale maintenance artifact: " + path);
                }
                catch (Exception ex)
                {
                    _logger.Info("Failed to delete stale maintenance artifact: " + path + " - " + ex.Message);
                }
            }
        }

        private void PruneOldBackups(string backupRoot)
        {
            if (!Directory.Exists(backupRoot)) return;

            var keepSet = new HashSet<string>(Directory.GetDirectories(backupRoot)
                .OrderByDescending(Directory.GetCreationTimeUtc)
                .ThenByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Take(MaxBackupDirectoriesToKeep)
                .Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            foreach (var directory in Directory.GetDirectories(backupRoot))
            {
                var fullPath = Path.GetFullPath(directory);
                if (keepSet.Contains(fullPath)) continue;

                try
                {
                    Directory.Delete(fullPath, true);
                    _logger.Info("Deleted old framework update backup: " + fullPath);
                }
                catch (Exception ex)
                {
                    _logger.Info("Failed to delete old framework update backup: " + fullPath + " - " + ex.Message);
                }
            }
        }

        private static void RestoreBackup(string backupDirectory, string installDirectory)
        {
            if (!Directory.Exists(backupDirectory)) return;
            foreach (var file in Directory.GetFiles(backupDirectory))
            {
                File.Copy(file, Path.Combine(installDirectory, Path.GetFileName(file)), true);
            }
        }

        private static string SafeInstallDirectory(string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new InvalidOperationException("Install directory is required.");
            }

            var full = Path.GetFullPath(installDirectory ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to update an unsafe install directory: " + installDirectory);
            }

            if (!ContainsPlugHubInstallMarkers(full) || !IsAllowedInstallRootName(full))
            {
                throw new InvalidOperationException("Refusing to update a directory that is not a PlugHub install root: " + full);
            }

            return full;
        }

        private static bool ContainsPlugHubInstallMarkers(string directory)
        {
            return Directory.Exists(directory)
                && Array.TrueForAll(RequiredInstallMarkers, marker => File.Exists(Path.Combine(directory, marker)));
        }

        private static bool IsAllowedInstallRootName(string directory)
        {
            var fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFileName(fullPath), "PlugHub", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var parent = Path.GetDirectoryName(fullPath);
            return string.Equals(Path.GetFileName(fullPath), "Revit2020", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(parent)
                && string.Equals(Path.GetFileName(parent), "dist", StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeSegment(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = (value ?? string.Empty).Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
        }
    }
}
