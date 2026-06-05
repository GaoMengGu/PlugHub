using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PlugHub.Updater
{
    internal sealed class FrameworkDllUpdater
    {
        private const int MaxBackupDirectoriesToKeep = 3;
        private const string InstalledUpdaterName = "PlugHub.Updater.exe";
        private const string InstalledSettingsAppName = "PlugHub.SettingsApp.exe";
        private const string InstalledUninstallerName = "PlugHub-Uninstall.exe";

        private readonly UpdaterLogger _logger;

        public FrameworkDllUpdater(UpdaterLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Run(UpdaterArguments args)
        {
            var installDirectory = SafeInstallDirectory(args.InstallDirectory);
            if (!File.Exists(args.PayloadZip))
            {
                throw new FileNotFoundException("Payload zip was not found.", args.PayloadZip);
            }

            WaitForRevitExit(args.RevitProcessId);
            CopyFrameworkFilesAndUninstaller(args.PayloadZip, installDirectory, args.TargetVersion);
        }

        private void WaitForRevitExit(int processId)
        {
            if (processId <= 0) return;
            try
            {
                var process = Process.GetProcessById(processId);
                _logger.Info("Waiting for Revit process to exit: " + processId);
                process.WaitForExit();
            }
            catch (ArgumentException)
            {
                _logger.Info("Revit process already exited: " + processId);
            }
        }

        private void CopyFrameworkFilesAndUninstaller(string payloadZip, string installDirectory, string targetVersion)
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
                        if (string.Equals(name, "PlugHub.addin", StringComparison.OrdinalIgnoreCase)) continue;

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

        private static bool ShouldCopyUpdateEntry(string entryName)
        {
            var normalized = (entryName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            if (!string.Equals(Path.GetFileName(normalized), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(Path.GetExtension(normalized), ".dll", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, InstalledUpdaterName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, InstalledSettingsAppName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, InstalledUninstallerName, StringComparison.OrdinalIgnoreCase);
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
            var full = Path.GetFullPath(installDirectory ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(full) || string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to update an unsafe install directory: " + installDirectory);
            }

            return full;
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
