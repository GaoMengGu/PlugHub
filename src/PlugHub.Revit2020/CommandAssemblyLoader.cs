using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PlugHub.Revit2020
{
    internal interface ICommandAssemblyLoader
    {
        IExternalCommand Create(string assemblyPath, string commandTypeName, string baseDirectory);
    }

    internal sealed class Net48ShadowCopyCommandAssemblyLoader : ICommandAssemblyLoader
    {
        private const string RuntimeCacheDirectoryName = "runtime-cache";
        private const string PendingCleanupFileName = "pending-cleanup.txt";
        private const string ReadyFileName = ".ready";

        public IExternalCommand Create(string assemblyPath, string commandTypeName, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath)) throw new ArgumentException("Command assembly path is required.", nameof(assemblyPath));
            if (string.IsNullOrWhiteSpace(commandTypeName)) throw new ArgumentException("Command type is required.", nameof(commandTypeName));

            var cachedAssemblyPath = PrepareShadowCopy(assemblyPath, baseDirectory);
            var commandType = Assembly.LoadFrom(cachedAssemblyPath).GetType(commandTypeName, throwOnError: false);
            if (commandType == null || !typeof(IExternalCommand).IsAssignableFrom(commandType))
            {
                throw new InvalidOperationException("Command type was not found or does not implement IExternalCommand: " + commandTypeName);
            }

            return (IExternalCommand)Activator.CreateInstance(commandType)!;
        }

        private static string PrepareShadowCopy(string assemblyPath, string baseDirectory)
        {
            var sourceAssemblyPath = Path.GetFullPath(assemblyPath);
            if (string.Equals(sourceAssemblyPath, typeof(FrameworkFeatureCommand).Assembly.Location, StringComparison.OrdinalIgnoreCase))
            {
                return sourceAssemblyPath;
            }

            var plugHubBaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
                ? AppDomain.CurrentDomain.BaseDirectory
                : Path.GetFullPath(baseDirectory);

            ApplyPendingCleanup(plugHubBaseDirectory);

            var packageSource = ResolvePackageSource(plugHubBaseDirectory, sourceAssemblyPath);
            var packageHash = ComputePackageHash(packageSource);
            var packageCacheRoot = Path.Combine(plugHubBaseDirectory, RuntimeCacheDirectoryName, SafePathSegment(packageSource.PackageId));
            var cacheDirectory = Path.Combine(packageCacheRoot, packageHash);
            var relativeAssemblyPath = MakeRelativePath(packageSource.Root, sourceAssemblyPath);
            var cachedAssemblyPath = Path.Combine(cacheDirectory, relativeAssemblyPath);
            var readyPath = Path.Combine(cacheDirectory, ReadyFileName);

            if (!File.Exists(readyPath) || !File.Exists(cachedAssemblyPath))
            {
                CopyPackagePayload(packageSource, cacheDirectory);
                File.WriteAllText(readyPath, DateTime.UtcNow.ToString("O"));
            }

            TryCleanOldCaches(packageCacheRoot, packageHash, plugHubBaseDirectory);
            return cachedAssemblyPath;
        }

        private static PackageCopySource ResolvePackageSource(string plugHubBaseDirectory, string sourceAssemblyPath)
        {
            var packagesRoot = Path.Combine(plugHubBaseDirectory, "packages");
            if (IsUnderDirectory(packagesRoot, sourceAssemblyPath))
            {
                var relativeToPackages = MakeRelativePath(packagesRoot, sourceAssemblyPath);
                var segments = relativeToPackages
                    .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                    .ToArray();
                var firstSegment = segments.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstSegment))
                {
                    if (segments.Length == 1)
                    {
                        var flatAssemblyStem = Path.GetFileNameWithoutExtension(sourceAssemblyPath);
                        return new PackageCopySource(packagesRoot, flatAssemblyStem, flatAssemblyStem);
                    }

                    var packageRoot = Path.Combine(packagesRoot, firstSegment);
                    if (Directory.Exists(packageRoot))
                    {
                        return new PackageCopySource(packageRoot, firstSegment, string.Empty);
                    }
                }
            }

            return new PackageCopySource(
                Path.GetDirectoryName(sourceAssemblyPath) ?? plugHubBaseDirectory,
                Path.GetFileNameWithoutExtension(sourceAssemblyPath),
                string.Empty);
        }

        private static string ComputePackageHash(PackageCopySource packageSource)
        {
            using (var hash = SHA256.Create())
            {
                foreach (var file in EnumeratePayloadFiles(packageSource))
                {
                    var relativePath = MakeRelativePath(packageSource.Root, file).Replace('\\', '/');
                    var fileInfo = new FileInfo(file);
                    var header = Encoding.UTF8.GetBytes(relativePath + "\n" + fileInfo.Length + "\n");
                    hash.TransformBlock(header, 0, header.Length, header, 0);

                    using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        var buffer = new byte[81920];
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            hash.TransformBlock(buffer, 0, read, buffer, 0);
                        }
                    }
                }

                hash.TransformFinalBlock(new byte[0], 0, 0);
                return ToHex(hash.Hash).Substring(0, 32);
            }
        }

        private static void CopyPackagePayload(PackageCopySource packageSource, string cacheDirectory)
        {
            Directory.CreateDirectory(cacheDirectory);
            foreach (var sourceFile in EnumeratePayloadFiles(packageSource))
            {
                var relativePath = MakeRelativePath(packageSource.Root, sourceFile);
                var targetFile = Path.Combine(cacheDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? cacheDirectory);

                CopyFile(sourceFile, targetFile);
            }
        }

        private static void CopyFile(string sourceFile, string targetFile)
        {
            using (var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var target = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(target);
            }
        }

        private static IEnumerable<string> EnumeratePayloadFiles(PackageCopySource packageSource)
        {
            var files = new List<string>();
            if (string.IsNullOrWhiteSpace(packageSource.FlatAssemblyStem))
            {
                files.AddRange(Directory.GetFiles(packageSource.Root, "*", SearchOption.AllDirectories));
            }
            else
            {
                files.AddRange(Directory.GetFiles(packageSource.Root, "*", SearchOption.TopDirectoryOnly)
                    .Where(file => IsFlatPayloadFile(file, packageSource.FlatAssemblyStem)));

                var flatPayloadDirectory = Path.Combine(packageSource.Root, packageSource.FlatAssemblyStem);
                if (Directory.Exists(flatPayloadDirectory))
                {
                    files.AddRange(Directory.GetFiles(flatPayloadDirectory, "*", SearchOption.AllDirectories));
                }
            }

            return files
                .Where(file => !IsIgnoredPayloadFile(file))
                .OrderBy(file => MakeRelativePath(packageSource.Root, file), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsFlatPayloadFile(string file, string flatAssemblyStem)
        {
            var fileName = Path.GetFileName(file);
            return string.Equals(Path.GetFileNameWithoutExtension(file), flatAssemblyStem, StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith(flatAssemblyStem + ".", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsIgnoredPayloadFile(string file)
        {
            var normalized = file.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return normalized.IndexOf(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ApplyPendingCleanup(string plugHubBaseDirectory)
        {
            var pendingPath = PendingCleanupPath(plugHubBaseDirectory);
            if (!File.Exists(pendingPath)) return;

            var remaining = new List<string>();
            string[] pendingDirectories;
            try
            {
                pendingDirectories = File.ReadAllLines(pendingPath);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            var runtimeCacheRoot = Path.Combine(plugHubBaseDirectory, RuntimeCacheDirectoryName);
            foreach (var directory in pendingDirectories.Where(line => !string.IsNullOrWhiteSpace(line)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fullDirectory = Path.GetFullPath(directory);
                if (!IsUnderDirectory(runtimeCacheRoot, fullDirectory)) continue;
                if (!Directory.Exists(fullDirectory)) continue;

                try
                {
                    Directory.Delete(fullDirectory, true);
                }
                catch (IOException)
                {
                    remaining.Add(fullDirectory);
                }
                catch (UnauthorizedAccessException)
                {
                    remaining.Add(fullDirectory);
                }
            }

            if (remaining.Count == 0)
            {
                TryDeleteFile(pendingPath);
            }
            else
            {
                TryWriteAllLines(pendingPath, remaining);
            }
        }

        private static void TryCleanOldCaches(string packageCacheRoot, string currentHash, string plugHubBaseDirectory)
        {
            if (!Directory.Exists(packageCacheRoot)) return;

            string[] cacheDirectories;
            try
            {
                cacheDirectories = Directory.GetDirectories(packageCacheRoot);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            foreach (var cacheDirectory in cacheDirectories)
            {
                if (string.Equals(Path.GetFileName(cacheDirectory), currentHash, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    Directory.Delete(cacheDirectory, true);
                }
                catch (IOException)
                {
                    TryRecordPendingCleanup(plugHubBaseDirectory, cacheDirectory);
                }
                catch (UnauthorizedAccessException)
                {
                    TryRecordPendingCleanup(plugHubBaseDirectory, cacheDirectory);
                }
            }
        }

        private static void TryRecordPendingCleanup(string plugHubBaseDirectory, string cacheDirectory)
        {
            try
            {
                RecordPendingCleanup(plugHubBaseDirectory, cacheDirectory);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void RecordPendingCleanup(string plugHubBaseDirectory, string cacheDirectory)
        {
            var pendingPath = PendingCleanupPath(plugHubBaseDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath) ?? plugHubBaseDirectory);

            var existing = File.Exists(pendingPath)
                ? new HashSet<string>(File.ReadAllLines(pendingPath), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!existing.Add(cacheDirectory)) return;

            File.WriteAllLines(pendingPath, existing.OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
        }

        private static void TryWriteAllLines(string path, IEnumerable<string> lines)
        {
            try
            {
                File.WriteAllLines(path, lines);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string PendingCleanupPath(string plugHubBaseDirectory)
        {
            return Path.Combine(plugHubBaseDirectory, RuntimeCacheDirectoryName, PendingCleanupFileName);
        }

        private static bool IsUnderDirectory(string directory, string path)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(directory));
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private static string MakeRelativePath(string rootDirectory, string path)
        {
            var root = EnsureTrailingSeparator(Path.GetFullPath(rootDirectory));
            var fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(fullPath);
            }

            var rootUri = new Uri(root);
            var pathUri = new Uri(fullPath);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string SafePathSegment(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = (string.IsNullOrWhiteSpace(value) ? "package" : value)
                .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch)
                .ToArray();
            return new string(chars);
        }

        private static string ToHex(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0) return new string('0', 64);

            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        private sealed class PackageCopySource
        {
            public PackageCopySource(string root, string packageId, string flatAssemblyStem)
            {
                Root = root;
                PackageId = packageId;
                FlatAssemblyStem = flatAssemblyStem;
            }

            public string Root { get; }
            public string PackageId { get; }
            public string FlatAssemblyStem { get; }
        }
    }
}
