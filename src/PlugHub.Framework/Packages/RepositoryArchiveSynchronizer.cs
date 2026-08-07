using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;

namespace PlugHub.Framework.Packages
{
    public sealed class RepositoryArchiveSynchronizer
    {
        private const string ArchiveDownloadUserAgent = "curl/8.0.1";
        private const string RepositoryCacheRootName = "repository-cache";
        private const int GiteeApiRetryCount = 2;

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

            var fullCacheDirectory = ValidateCacheDirectory(cacheDirectory);
            var address = RepositoryAddress.From(repository);
            if (address == null)
            {
                AddDiagnostic(diagnostics, repository.Id, "PH-REPOSITORY-URL", "Repository URL must point to a GitHub or Gitee owner/repository.");
                return false;
            }

            var parentDirectory = Path.GetDirectoryName(fullCacheDirectory) ?? throw new InvalidOperationException("Repository cache directory must have a parent directory.");
            var stagingDirectory = string.Empty;

            try
            {
                Directory.CreateDirectory(parentDirectory);
                stagingDirectory = SyncConfiguredCloudRepositoryWithMirrorFallback(address, repository, parentDirectory);

                ReplaceCacheDirectory(stagingDirectory, fullCacheDirectory);
                stagingDirectory = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                AddDiagnostic(diagnostics, repository.Id, "PH-REPOSITORY-ARCHIVE", SensitiveTextRedactor.Redact(ex.Message));
                return false;
            }
            finally
            {
                DeleteDirectoryQuietly(stagingDirectory);
            }
        }

        private string SyncConfiguredCloudRepositoryWithMirrorFallback(RepositoryAddress address, PackageRepositoryConfiguration repository, string parentDirectory)
        {
            var candidates = CloudSyncCandidates(address, repository).ToList();
            var errors = new List<string>();
            foreach (var candidate in candidates)
            {
                try
                {
                    return SyncCloudRepositoryCandidate(candidate, repository, parentDirectory);
                }
                catch (Exception ex)
                {
                    errors.Add(candidate.Provider + ": " + SensitiveTextRedactor.Redact(ex.Message));
                }
            }

            throw new InvalidOperationException("Cloud repository sync failed for the configured source and its mirror: " + string.Join("；", errors));
        }

        private string SyncCloudRepositoryCandidate(RepositoryAddress address, PackageRepositoryConfiguration repository, string parentDirectory)
        {
            var stagingDirectory = Path.Combine(parentDirectory, SafePathSegment(repository.Id) + "." + address.Provider + ".download." + Guid.NewGuid().ToString("N"));
            var archivePath = Path.Combine(parentDirectory, SafePathSegment(repository.Id) + "." + address.Provider + ".archive." + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                Directory.CreateDirectory(stagingDirectory);
                try
                {
                    var archiveUrl = ArchiveDownloadUrl(address, repository);
                    DownloadArchive(archiveUrl, address, repository, archivePath);
                    ValidateArchiveFile(archivePath, archiveUrl);
                    ExtractArchive(archivePath, stagingDirectory);
                    return stagingDirectory;
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
                    return stagingDirectory;
                }
            }
            catch
            {
                DeleteFileQuietly(archivePath);
                DeleteDirectoryQuietly(stagingDirectory);
                throw;
            }
            finally
            {
                DeleteFileQuietly(archivePath);
            }
        }

        private static IEnumerable<RepositoryAddress> CloudSyncCandidates(RepositoryAddress address, PackageRepositoryConfiguration repository)
        {
            if (RepositoryRequiresToken(repository))
            {
                yield return address;
                yield break;
            }

            if (string.Equals(address.Provider, "gitee", StringComparison.OrdinalIgnoreCase))
            {
                yield return address;
                yield return new RepositoryAddress("github", address.Owner, address.Name);
                yield break;
            }

            yield return address;
            yield return new RepositoryAddress("gitee", address.Owner, address.Name);
        }

        private bool ShouldUseGiteeApiFallback(RepositoryAddress address, PackageRepositoryConfiguration repository, Exception exception)
        {
            if (!string.Equals(address.Provider, "gitee", StringComparison.OrdinalIgnoreCase)) return false;
            if (RepositoryRequiresToken(repository) && string.IsNullOrWhiteSpace(_credentialService.ResolveApiKey(repository))) return false;
            if (exception is InvalidDataException) return true;

            var webException = exception as WebException;
            var response = webException?.Response as HttpWebResponse;
            if (response == null) return false;

            return response.StatusCode == HttpStatusCode.BadRequest
                || response.StatusCode == HttpStatusCode.Forbidden
                || response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.NotFound
                || response.StatusCode == HttpStatusCode.MethodNotAllowed;
        }

        private void SyncGiteeRepositoryViaApi(RepositoryAddress address, PackageRepositoryConfiguration repository, string stagingDirectory)
        {
            var apiKey = _credentialService.ResolveApiKey(repository);
            if (RepositoryRequiresToken(repository) && string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Private Gitee repository requires an access token.");
            }

            var gitRef = string.IsNullOrWhiteSpace(repository.Ref) ? "main" : repository.Ref.Trim();
            var retryForbidden = !RepositoryRequiresToken(repository);
            var tree = ReadJsonObject(GiteeApiUrl(address, "git/trees/" + Uri.EscapeDataString(gitRef), apiKey, "recursive=1"), retryForbidden);
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
                DownloadGiteeApiFile(address, gitRef, path, apiKey, stagingDirectory, retryForbidden);
            }
        }

        private void DownloadGiteeApiFile(
            RepositoryAddress address,
            string gitRef,
            string repositoryPath,
            string apiKey,
            string stagingDirectory,
            bool retryForbidden)
        {
            var file = ReadJsonObject(GiteeApiUrl(address, "contents/" + EscapePath(repositoryPath), apiKey, "ref=" + Uri.EscapeDataString(gitRef)), retryForbidden);
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

        private Dictionary<string, object> ReadJsonObject(Uri uri, bool retryForbidden)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
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
                        EnsureHttpsResponse(response.ResponseUri);
                        if (source == null)
                        {
                            throw new InvalidOperationException("Gitee API response did not contain a body.");
                        }

                        using (var reader = new StreamReader(source, Encoding.UTF8))
                        {
                            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };
                            return serializer.Deserialize<Dictionary<string, object>>(reader.ReadToEnd()) ?? new Dictionary<string, object>();
                        }
                    }
                }
                catch (WebException ex)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (!IsTransientGiteeRateLimit(response, retryForbidden))
                    {
                        throw;
                    }

                    if (attempt >= GiteeApiRetryCount)
                    {
                        throw new InvalidOperationException("Gitee API rate limit persisted after retries; try again later or use the configured mirror.", ex);
                    }

                    Thread.Sleep((attempt + 1) * 1000);
                }
            }
        }

        private static bool IsTransientGiteeRateLimit(HttpWebResponse response, bool retryForbidden)
        {
            return response != null
                && ((retryForbidden && response.StatusCode == HttpStatusCode.Forbidden) || (int)response.StatusCode == 429);
        }

        private static Uri GiteeApiUrl(RepositoryAddress address, string apiPath, string apiKey, string extraQuery)
        {
            var queryParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                queryParts.Add("access_token=" + Uri.EscapeDataString(apiKey.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(extraQuery))
            {
                queryParts.Add(extraQuery.TrimStart('&', '?'));
            }

            return new Uri("https://gitee.com/api/v5/repos/"
                + Uri.EscapeDataString(address.Owner)
                + "/"
                + Uri.EscapeDataString(address.Name)
                + "/"
                + apiPath
                + (queryParts.Count == 0 ? string.Empty : "?" + string.Join("&", queryParts)));
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

        private Uri ArchiveDownloadUrl(RepositoryAddress address, PackageRepositoryConfiguration repository)
        {
            var archiveUrl = ArchiveUrl(address, repository);
            return ShouldAppendArchiveCacheBust(address) ? WithCacheBust(archiveUrl) : archiveUrl;
        }

        private static bool ShouldAppendArchiveCacheBust(RepositoryAddress address)
        {
            return string.Equals(address.Provider, "github", StringComparison.OrdinalIgnoreCase);
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

        private void DownloadArchive(Uri archiveUrl, RepositoryAddress address, PackageRepositoryConfiguration repository, string archivePath)
        {
            var request = (HttpWebRequest)WebRequest.Create(archiveUrl);
            request.Method = "GET";
            request.AllowAutoRedirect = true;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.Reload);
            request.UserAgent = ArchiveDownloadUserAgent;

            var apiKey = _credentialService.ResolveApiKey(repository);
            if (string.Equals(address.Provider, "github", StringComparison.OrdinalIgnoreCase)
                && RepositoryRequiresToken(repository)
                && !string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                EnsureHttpsResponse(response.ResponseUri);
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
        }

        private static Uri WithCacheBust(Uri uri)
        {
            var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
            return new Uri(uri + separator + "plughubCacheBust=" + DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static bool RepositoryRequiresToken(PackageRepositoryConfiguration repository)
        {
            return string.Equals(repository.Visibility, "private", StringComparison.OrdinalIgnoreCase);
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

        private static string ValidateCacheDirectory(string cacheDirectory)
        {
            var fullCacheDirectory = Path.GetFullPath(cacheDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parentDirectory = Path.GetDirectoryName(fullCacheDirectory);
            if (string.IsNullOrWhiteSpace(parentDirectory)
                || !string.Equals(Path.GetFileName(parentDirectory), RepositoryCacheRootName, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(Path.GetFileName(fullCacheDirectory)))
            {
                throw new InvalidOperationException("Repository cache directory must be a child of the repository-cache directory.");
            }

            return fullCacheDirectory;
        }

        private static void EnsureHttpsResponse(Uri responseUri)
        {
            if (responseUri == null || !string.Equals(responseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Repository archive download redirected to a non-HTTPS URL.");
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
            return string.IsNullOrWhiteSpace(segment) || segment.All(ch => ch == '.') ? "repository" : segment;
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

    }
}
