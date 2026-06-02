using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;

namespace PlugHub.Framework.Packages
{
    public sealed class RepositoryArchiveSynchronizer
    {
        private const string ArchiveDownloadUserAgent = "curl/8.0.1";

        private readonly RepositoryCredentialService _credentialService;

        public RepositoryArchiveSynchronizer(RepositoryCredentialService credentialService)
        {
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        }

        public bool Sync(PackageRepositoryConfiguration repository, string cacheDirectory, ICollection<DiagnosticMessage> diagnostics)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            if (string.IsNullOrWhiteSpace(cacheDirectory)) throw new ArgumentException("Cache directory is required.", nameof(cacheDirectory));
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));

            var address = RepositoryAddress.From(repository);
            if (address == null)
            {
                AddDiagnostic(diagnostics, repository.Id, "PH-REPOSITORY-URL", "Repository URL must point to a GitHub or Gitee owner/repository.");
                return false;
            }

            var parentDirectory = Path.GetDirectoryName(cacheDirectory) ?? cacheDirectory;
            var stagingDirectory = Path.Combine(parentDirectory, SafePathSegment(repository.Id) + ".download." + Guid.NewGuid().ToString("N"));
            var archivePath = Path.Combine(parentDirectory, SafePathSegment(repository.Id) + ".archive." + Guid.NewGuid().ToString("N") + ".zip");

            try
            {
                Directory.CreateDirectory(parentDirectory);
                Directory.CreateDirectory(stagingDirectory);

                var archiveUrl = ArchiveUrl(address, repository);
                DownloadArchive(archiveUrl, repository, archivePath);
                ValidateArchiveFile(archivePath, archiveUrl);
                ExtractArchive(archivePath, stagingDirectory);
                ReplaceCacheDirectory(stagingDirectory, cacheDirectory);
                return true;
            }
            catch (Exception ex)
            {
                AddDiagnostic(diagnostics, repository.Id, "PH-REPOSITORY-ARCHIVE", SensitiveTextRedactor.Redact(ex.Message));
                return false;
            }
            finally
            {
                DeleteFileQuietly(archivePath);
                DeleteDirectoryQuietly(stagingDirectory);
            }
        }

        private Uri ArchiveUrl(RepositoryAddress address, PackageRepositoryConfiguration repository)
        {
            var gitRef = string.IsNullOrWhiteSpace(repository.Ref) ? "main" : repository.Ref.Trim();
            var escapedOwner = Uri.EscapeDataString(address.Owner);
            var escapedName = Uri.EscapeDataString(address.Name);
            var escapedRef = Uri.EscapeDataString(gitRef);
            var apiKey = _credentialService.ResolveApiKey(repository);

            if (string.Equals(address.Provider, "github", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri("https://api.github.com/repos/" + escapedOwner + "/" + escapedName + "/zipball/" + escapedRef);
            }

            var url = "https://gitee.com/" + escapedOwner + "/" + escapedName + "/repository/archive/" + escapedRef + ".zip";
            if (string.Equals(repository.Visibility, "private", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(apiKey))
            {
                url += "?access_token=" + Uri.EscapeDataString(apiKey.Trim());
            }

            return new Uri(url);
        }

        private void DownloadArchive(Uri archiveUrl, PackageRepositoryConfiguration repository, string archivePath)
        {
            var request = (HttpWebRequest)WebRequest.Create(archiveUrl);
            request.Method = "GET";
            request.AllowAutoRedirect = true;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.UserAgent = ArchiveDownloadUserAgent;

            var apiKey = _credentialService.ResolveApiKey(repository);
            if (string.Equals(repository.Provider, "github", StringComparison.OrdinalIgnoreCase)
                && string.Equals(repository.Visibility, "private", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var source = response.GetResponseStream())
            using (var target = File.Create(archivePath))
            {
                if (source == null)
                {
                    throw new InvalidOperationException("Repository archive response did not contain a body.");
                }

                source.CopyTo(target);
            }
        }

        private static void ExtractArchive(string archivePath, string targetDirectory)
        {
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    var destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!IsUnderDirectory(targetDirectory, destinationPath))
                    {
                        throw new InvalidOperationException("Repository archive contains an unsafe path: " + entry.FullName);
                    }

                    if (string.IsNullOrWhiteSpace(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? targetDirectory);
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }

        private static void ValidateArchiveFile(string archivePath, Uri archiveUrl)
        {
            var length = File.Exists(archivePath) ? new FileInfo(archivePath).Length : 0;
            if (length < 4)
            {
                throw InvalidArchive(archiveUrl);
            }

            var header = new byte[4];
            using (var source = File.OpenRead(archivePath))
            {
                if (source.Read(header, 0, header.Length) != header.Length)
                {
                    throw InvalidArchive(archiveUrl);
                }
            }

            if (header[0] != 0x50
                || header[1] != 0x4B
                || !((header[2] == 0x03 && header[3] == 0x04)
                    || (header[2] == 0x05 && header[3] == 0x06)
                    || (header[2] == 0x07 && header[3] == 0x08)))
            {
                throw InvalidArchive(archiveUrl);
            }
        }

        private static InvalidDataException InvalidArchive(Uri archiveUrl)
        {
            return new InvalidDataException("Downloaded repository archive is not a zip file. Check repository URL, ref, and credentials: " + SensitiveTextRedactor.Redact(archiveUrl.ToString()));
        }

        private static void ReplaceCacheDirectory(string stagingDirectory, string cacheDirectory)
        {
            var backupDirectory = cacheDirectory + ".previous." + Guid.NewGuid().ToString("N");
            var hasBackup = false;

            if (Directory.Exists(cacheDirectory))
            {
                Directory.Move(cacheDirectory, backupDirectory);
                hasBackup = true;
            }

            try
            {
                Directory.Move(stagingDirectory, cacheDirectory);
            }
            catch
            {
                if (hasBackup && Directory.Exists(backupDirectory) && !Directory.Exists(cacheDirectory))
                {
                    Directory.Move(backupDirectory, cacheDirectory);
                    hasBackup = false;
                }

                throw;
            }

            if (hasBackup)
            {
                DeleteDirectoryQuietly(backupDirectory);
            }
        }

        private static bool IsUnderDirectory(string parentDirectory, string childPath)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteFileQuietly(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

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

        private static string SafePathSegment(string value)
        {
            var chars = (value ?? string.Empty)
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_')
                .ToArray();
            var segment = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(segment) ? "repository" : segment;
        }

        private static void AddDiagnostic(ICollection<DiagnosticMessage> diagnostics, string repositoryId, string code, string message)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                ModuleId = repositoryId ?? string.Empty,
                Severity = DiagnosticSeverity.Warning,
                Code = code ?? string.Empty,
                Message = SensitiveTextRedactor.Redact(message ?? string.Empty)
            });
        }

        private sealed class RepositoryAddress
        {
            private RepositoryAddress(string provider, string owner, string name)
            {
                Provider = provider;
                Owner = owner;
                Name = name;
            }

            public string Provider { get; }
            public string Owner { get; }
            public string Name { get; }

            public static RepositoryAddress? From(PackageRepositoryConfiguration repository)
            {
                var provider = string.Equals(repository.Provider, "gitee", StringComparison.OrdinalIgnoreCase) ? "gitee" : "github";
                var value = StripRepositorySuffix(StripUrlUserInfo(repository.Repository ?? string.Empty).Trim().TrimEnd('/'));

                if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                    {
                        return null;
                    }

                    var expectedHost = string.Equals(provider, "gitee", StringComparison.OrdinalIgnoreCase) ? "gitee.com" : "github.com";
                    if (!string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    var segments = uri.AbsolutePath
                        .Trim('/')
                        .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    return segments.Length >= 2
                        ? new RepositoryAddress(provider, segments[0], StripRepositorySuffix(segments[1]))
                        : null;
                }

                var shorthand = value
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                return shorthand.Length >= 2
                    ? new RepositoryAddress(provider, shorthand[0], StripRepositorySuffix(shorthand[1]))
                    : null;
            }

            private static string StripRepositorySuffix(string value)
            {
                return value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                    ? value.Substring(0, value.Length - ".git".Length)
                    : value;
            }

            private static string StripUrlUserInfo(string url)
            {
                if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
                {
                    return url;
                }

                var builder = new UriBuilder(uri)
                {
                    UserName = string.Empty,
                    Password = string.Empty
                };
                return builder.Uri.AbsoluteUri;
            }
        }
    }
}
