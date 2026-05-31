using System;
using System.IO;
using System.IO.Compression;

namespace PlugHub.Framework.Diagnostics
{
    public sealed class PlugHubLogExporter
    {
        public void Export(string baseDirectory, string targetZipPath)
        {
            if (string.IsNullOrWhiteSpace(targetZipPath))
            {
                throw new ArgumentException("Target zip path is required.", nameof(targetZipPath));
            }

            var logsDirectory = PlugHubLogger.LogsDirectory(baseDirectory);
            Directory.CreateDirectory(logsDirectory);

            var fullLogsDirectory = EnsureTrailingSeparator(Path.GetFullPath(logsDirectory));
            var fullTargetPath = Path.GetFullPath(targetZipPath);
            if (IsPathInside(fullTargetPath, fullLogsDirectory))
            {
                throw new InvalidOperationException("PlugHub log export target must not be inside the source logs directory.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullTargetPath) ?? Environment.CurrentDirectory);

            var tempPath = fullTargetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                ZipFile.CreateFromDirectory(logsDirectory, tempPath);
                if (File.Exists(fullTargetPath))
                {
                    File.Delete(fullTargetPath);
                }

                File.Move(tempPath, fullTargetPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static bool IsPathInside(string targetPath, string parentDirectory)
        {
            return targetPath.StartsWith(parentDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
