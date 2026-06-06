using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace PlugHub.Installer
{
    internal static class InstallerPayload
    {
        private const string ResourceName = "PlugHubPayload.zip";

        public static void ExtractTo(string installDirectory)
        {
            var fullInstallDirectory = ValidateInstallDirectory(installDirectory);

            var stagingRoot = Path.Combine(Path.GetTempPath(), "PlugHubInstaller", Guid.NewGuid().ToString("N"));
            var payloadZip = Path.Combine(stagingRoot, ResourceName);
            var extractedDirectory = Path.Combine(stagingRoot, "payload");
            try
            {
                Directory.CreateDirectory(stagingRoot);
                Directory.CreateDirectory(extractedDirectory);
                WritePayloadZip(payloadZip);
                ExtractPayloadZip(payloadZip, extractedDirectory);
                CopyDirectory(extractedDirectory, fullInstallDirectory);
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, true);
                }
            }
        }

        private static string ValidateInstallDirectory(string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new ArgumentException("Install directory is required.", nameof(installDirectory));
            }

            var fullPath = Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to install into a drive root: " + fullPath);
            }

            if (!string.Equals(Path.GetFileName(fullPath), "PlugHub", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Install directory must be named PlugHub: " + fullPath);
            }

            return fullPath;
        }

        private static void WritePayloadZip(string targetPath)
        {
            using (var source = OpenPayloadStream())
            using (var target = File.Create(targetPath))
            {
                source.CopyTo(target);
            }
        }

        private static Stream OpenPayloadStream()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream != null)
            {
                return stream;
            }

            var directory = Path.GetDirectoryName(assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            var adjacentPayload = Directory.GetFiles(directory, "PlugHub-Revit2020-*.zip")
                .Concat(new[] { Path.Combine(directory, ResourceName) })
                .FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(adjacentPayload))
            {
                return File.OpenRead(adjacentPayload);
            }

            throw new FileNotFoundException("PlugHub installer payload was not embedded and no adjacent PlugHub-Revit2020 zip was found.");
        }

        private static void ExtractPayloadZip(string payloadZip, string targetDirectory)
        {
            using (var archive = ZipFile.OpenRead(payloadZip))
            {
                foreach (var entry in archive.Entries)
                {
                    var targetPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!IsUnderDirectory(targetDirectory, targetPath))
                    {
                        throw new InvalidDataException("Installer payload contains an unsafe path: " + entry.FullName);
                    }

                    if (string.IsNullOrWhiteSpace(entry.Name))
                    {
                        Directory.CreateDirectory(targetPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? targetDirectory);
                    entry.ExtractToFile(targetPath, true);
                }
            }
        }

        private static void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);
            foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(targetDirectory, RelativePath(sourceDirectory, directory));
                Directory.CreateDirectory(target);
            }

            foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(targetDirectory, RelativePath(sourceDirectory, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? targetDirectory);
                File.Copy(file, target, true);
            }
        }

        private static string RelativePath(string root, string path)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(fullRoot.Length)
                : Path.GetFileName(path);
        }

        private static bool IsUnderDirectory(string parentDirectory, string childPath)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }
    }
}
