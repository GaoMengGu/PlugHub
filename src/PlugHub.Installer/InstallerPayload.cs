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
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new ArgumentException("Install directory is required.", nameof(installDirectory));
            }

            var stagingRoot = Path.Combine(Path.GetTempPath(), "PlugHubInstaller", Guid.NewGuid().ToString("N"));
            var payloadZip = Path.Combine(stagingRoot, ResourceName);
            var extractedDirectory = Path.Combine(stagingRoot, "payload");
            try
            {
                Directory.CreateDirectory(stagingRoot);
                Directory.CreateDirectory(extractedDirectory);
                WritePayloadZip(payloadZip);
                ZipFile.ExtractToDirectory(payloadZip, extractedDirectory);
                CopyDirectory(extractedDirectory, installDirectory);
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, true);
                }
            }
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
    }
}
