using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;

namespace PlugHub.Framework.Packages
{
    public sealed class RepositoryArchiveSynchronizer
    {
        private const string ArchiveDownloadUserAgent = "curl/8.0.1";

        private readonly RepositoryCredentialService _credentialService;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };

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

                try
                {
                    var archiveUrl = ArchiveUrl(address, repository);
                    DownloadArchive(archiveUrl, repository, archivePath);
                    ValidateArchiveFile(archivePath, archiveUrl);
                    ExtractArchive(archivePath, stagingDirectory);
                }
                catch (Exception ex)
                {
                    if (!ShouldUseGiteeApiFallback(address, repository, ex))
                    {
                        throw;
                    }

                    DeleteFileQuietly(archivePath);
                    DeleteDirectoryQuietly(stagingDirectory);
                    Directory.CreateDirectory(stagingDirectory);
                    SyncGiteeRepositoryViaApi(address, repository, stagingDirectory);
                }

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

        private bool ShouldUseGiteeApiFallback(RepositoryAddress address, PackageRepositoryConfiguration repository, Exception exception)
        {
            if (!string.Equals(address.Provider, "gitee", StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.Equals(repository.Visibility, "private", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrWhiteSpace(_credentialService.ResolveApiKey(repository))) return false;
            if (exception is InvalidDataException) return true;

            var webException = exception as WebException;
            var response = webException?.Response as HttpWebResponse;
            if (response == null) return false;

            return response.StatusCode == HttpStatusCode.Forbidden
                || response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.NotFound;
        }

        private void SyncGiteeRepositoryViaApi(RepositoryAddress address, PackageRepositoryConfiguration repository, string stagingDirectory)
        {
            var apiKey = _credentialService.ResolveApiKey(repository);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Private Gitee repository requires an access token.");
            }

            var gitRef = string.IsNullOrWhiteSpace(repository.Ref) ? "main" : repository.Ref.Trim();
            var tree = ReadJsonObject(GiteeApiUrl(address, "git/trees/" + Uri.EscapeDataString(gitRef), apiKey, "recursive=1"));
            var entries = ArrayValue(tree, "tree")
                .Select(item => item as Dictionary<string, object>)
                .Where(item => item != null)
                .Cast<Dictionary<string, object>>()
                .Where(item => string.Equals(StringValue(item, "type"), "blob", StringComparison.OrdinalIgnoreCase))
                .Select(item => StringValue(item, "path"))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (entries.Count == 0)
            {
                throw new InvalidDataException("Gitee repository tree did not contain downloadable files.");
            }

            foreach (var path in entries)
            {
                DownloadGiteeApiFile(address, gitRef, path, apiKey, stagingDirectory);
            }
        }

        private void DownloadGiteeApiFile(
            RepositoryAddress address,
            string gitRef,
            string repositoryPath,
            string apiKey,
            string stagingDirectory)
        {
            var file = ReadJsonObject(GiteeApiUrl(address, "contents/" + EscapePath(repositoryPath), apiKey, "ref=" + Uri.EscapeDataString(gitRef)));
            var type = StringValue(file, "type");
            if (!string.Equals(type, "file", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var content = StringValue(file, "content").Replace("\r", string.Empty).Replace("\n", string.Empty);
            var encoding = StringValue(file, "encoding");
            var bytes = string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(content)
                : Encoding.UTF8.GetBytes(content);
            var targetPath = Path.GetFullPath(Path.Combine(stagingDirectory, repositoryPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsUnderDirectory(stagingDirectory, targetPath))
            {
                throw new InvalidOperationException("Gitee API file path is unsafe: " + repositoryPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? stagingDirectory);
            File.WriteAllBytes(targetPath, bytes);
        }

        private Dictionary<string, object> ReadJsonObject(Uri uri)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.UserAgent = ArchiveDownloadUserAgent;
            request.Accept = "application/json";

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var source = response.GetResponseStream())
            {
                if (source == null)
                {
                    throw new InvalidOperationException("Gitee API response did not contain a body.");
                }

                using (var reader = new StreamReader(source, Encoding.UTF8))
                {
                    return _serializer.Deserialize<Dictionary<string, object>>(reader.ReadToEnd()) ?? new Dictionary<string, object>();
                }
            }
        }

        private static Uri GiteeApiUrl(RepositoryAddress address, string apiPath, string apiKey, string extraQuery)
        {
            var query = "access_token=" + Uri.EscapeDataString(apiKey.Trim());
            if (!string.IsNullOrWhiteSpace(extraQuery))
            {
                query += "&" + extraQuery.TrimStart('&', '?');
            }

            return new Uri("https://gitee.com/api/v5/repos/"
                + Uri.EscapeDataString(address.Owner)
                + "/"
                + Uri.EscapeDataString(address.Name)
                + "/"
                + apiPath
                + "?"
                + query);
        }

        private static string EscapePath(string path)
        {
            return string.Join("/", (path ?? string.Empty)
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        }

        private static IEnumerable<object> ArrayValue(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return Enumerable.Empty<object>();
            return value is ArrayList list ? list.Cast<object>() : Enumerable.Empty<object>();
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return string.Empty;
            return Convert.ToString(value) ?? string.Empty;
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
