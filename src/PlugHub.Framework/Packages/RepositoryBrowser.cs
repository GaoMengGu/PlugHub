using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;

namespace PlugHub.Framework.Packages
{
    public sealed class RepositoryBrowser
    {
        private readonly PackageManifestReader _manifestReader;
        private readonly RepositoryCredentialService _credentialService;
        private readonly Func<string, string, string, string> _installedPackageVersion;
        private readonly Func<string, string, string, bool> _isModuleInstalled;
        private readonly Func<string, string, string, string> _pendingOperationFor;

        public RepositoryBrowser(
            PackageManifestReader manifestReader,
            RepositoryCredentialService credentialService,
            Func<string, string, string, string> installedPackageVersion,
            Func<string, string, string, bool> isModuleInstalled,
            Func<string, string, string, string> pendingOperationFor)
        {
            _manifestReader = manifestReader ?? throw new ArgumentNullException(nameof(manifestReader));
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _installedPackageVersion = installedPackageVersion ?? throw new ArgumentNullException(nameof(installedPackageVersion));
            _isModuleInstalled = isModuleInstalled ?? throw new ArgumentNullException(nameof(isModuleInstalled));
            _pendingOperationFor = pendingOperationFor ?? throw new ArgumentNullException(nameof(pendingOperationFor));
        }

        public IReadOnlyList<RepositoryPackageDescriptor> Browse(string baseDirectory, PackageRepositoryConfiguration repository, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (repository == null) throw new ArgumentNullException(nameof(repository));

            var messages = new List<DiagnosticMessage>();
            diagnostics = messages;

            if (!repository.Enabled)
            {
                AddDiagnostic(messages, repository.Id, "PH-REPOSITORY-DISABLED", "Repository is disabled.");
                return new List<RepositoryPackageDescriptor>();
            }

            if (string.Equals(repository.Visibility, "private", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(_credentialService.ResolveApiKey(repository)))
            {
                AddDiagnostic(messages, repository.Id, "PH-REPOSITORY-APIKEY", "Private repository requires apiKey.");
                return new List<RepositoryPackageDescriptor>();
            }

            var cacheDirectory = RepositoryCacheDirectory(baseDirectory, repository);
            if (!SyncRepositoryCache(repository, cacheDirectory, messages))
            {
                return new List<RepositoryPackageDescriptor>();
            }

            var packages = BrowseCached(baseDirectory, repository.Id, cacheDirectory, out var browseDiagnostics);
            diagnostics = messages.Concat(browseDiagnostics).ToList();
            return packages;
        }

        public IReadOnlyList<RepositoryPackageDescriptor> BrowseCached(string baseDirectory, string repositoryId, string cacheDirectory, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (string.IsNullOrWhiteSpace(cacheDirectory)) throw new ArgumentException("Cache directory is required.", nameof(cacheDirectory));

            var messages = new List<DiagnosticMessage>();
            diagnostics = messages;

            var packages = new List<RepositoryPackageDescriptor>();
            foreach (var manifestPath in _manifestReader.FindPackageManifests(cacheDirectory))
            {
                packages.AddRange(_manifestReader.ReadPackagesFromManifest(manifestPath, repositoryId, baseDirectory, _installedPackageVersion, _isModuleInstalled, _pendingOperationFor));
            }

            if (packages.Count == 0)
            {
                AddDiagnostic(messages, repositoryId, "PH-REPOSITORY-MANIFEST", "No PlugHub package manifests were found in repository.");
            }

            return packages
                .GroupBy(package => package.PackageId + "\n" + package.ModuleId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(package => package.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<RepositoryPackageDescriptor> BrowseCached(string baseDirectory, PackageRepositoryConfiguration repository, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            if (repository == null) throw new ArgumentNullException(nameof(repository));
            diagnostics = new List<DiagnosticMessage>();
            var cacheDirectory = RepositoryCacheDirectory(baseDirectory, repository);
            return Directory.Exists(cacheDirectory)
                ? BrowseCached(baseDirectory, repository.Id, cacheDirectory, out diagnostics)
                : new List<RepositoryPackageDescriptor>();
        }

        public bool HasRepositoryCache(string baseDirectory, PackageRepositoryConfiguration repository)
        {
            if (repository == null) return false;
            return Directory.Exists(RepositoryCacheDirectory(baseDirectory, repository));
        }

        public string RepositoryUrl(PackageRepositoryConfiguration repository, bool includeCredential)
        {
            var provider = string.Equals(repository.Provider, "gitee", StringComparison.OrdinalIgnoreCase) ? "gitee" : "github";
            var url = StripUrlUserInfo(repository.Repository.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? repository.Repository.Trim()
                : RepositoryHost(provider) + repository.Repository.Trim().TrimEnd('/') + ".git");
            var apiKey = _credentialService.ResolveApiKey(repository);

            if (!includeCredential
                || !string.Equals(repository.Visibility, "private", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(apiKey))
            {
                return url;
            }

            if (provider == "gitee" && url.StartsWith("https://gitee.com/", StringComparison.OrdinalIgnoreCase))
            {
                return "https://oauth2:" + Uri.EscapeDataString(apiKey.Trim()) + "@" + url.Substring("https://".Length);
            }

            if (provider == "github" && url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                return "https://x-access-token:" + Uri.EscapeDataString(apiKey.Trim()) + "@" + url.Substring("https://".Length);
            }

            return url;
        }

        private bool SyncRepositoryCache(PackageRepositoryConfiguration repository, string cacheDirectory, ICollection<DiagnosticMessage> diagnostics)
        {
            if (!IsSupportedRepositoryProvider(repository.Provider))
            {
                AddDiagnostic(diagnostics, repository.Id, "PH-REPOSITORY-PROVIDER", "Unsupported repository provider: " + repository.Provider);
                return false;
            }

            if (string.IsNullOrWhiteSpace(repository.Repository))
            {
                AddDiagnostic(diagnostics, repository.Id, "PH-REPOSITORY-URL", "Repository is required.");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(cacheDirectory) ?? cacheDirectory);

            var publicUrl = RepositoryUrl(repository, false);
            var authenticatedUrl = RepositoryUrl(repository, true);
            var gitRef = string.IsNullOrWhiteSpace(repository.Ref) ? "main" : repository.Ref.Trim();
            var fetchRef = "refs/plughub/fetch";

            if (!Directory.Exists(Path.Combine(cacheDirectory, ".git")))
            {
                Directory.CreateDirectory(cacheDirectory);
                if (!RunGit("init --quiet " + Quote(cacheDirectory), repository.Id, diagnostics)
                    || !RunGit("-C " + Quote(cacheDirectory) + " remote add origin " + Quote(publicUrl), repository.Id, diagnostics))
                {
                    return false;
                }
            }
            else if (!RunGit("-C " + Quote(cacheDirectory) + " remote set-url origin " + Quote(publicUrl), repository.Id, diagnostics))
            {
                return false;
            }

            if (!DeleteStaleFetchHead(cacheDirectory, repository.Id, diagnostics))
            {
                return false;
            }

            if (!ConfigureSparseCheckout(cacheDirectory, repository.Id, diagnostics))
            {
                return false;
            }

            if (!RunGit("-C " + Quote(cacheDirectory) + " fetch --quiet --no-write-fetch-head --filter=blob:none --depth 1 " + Quote(authenticatedUrl) + " " + Quote(gitRef + ":" + fetchRef), repository.Id, diagnostics))
            {
                return false;
            }

            return RunGit("-C " + Quote(cacheDirectory) + " checkout --quiet " + Quote(fetchRef), repository.Id, diagnostics)
                && ConfigureSparseCheckout(cacheDirectory, repository.Id, diagnostics);
        }

        private static bool ConfigureSparseCheckout(string cacheDirectory, string repositoryId, ICollection<DiagnosticMessage> diagnostics)
        {
            return RunGit("-C " + Quote(cacheDirectory) + " sparse-checkout set --no-cone " + string.Join(" ", SparseCheckoutPatterns().Select(Quote)), repositoryId, diagnostics);
        }

        private static bool DeleteStaleFetchHead(string cacheDirectory, string repositoryId, ICollection<DiagnosticMessage> diagnostics)
        {
            var fetchHeadPath = Path.Combine(cacheDirectory, ".git", "FETCH_HEAD");
            if (!File.Exists(fetchHeadPath))
            {
                return true;
            }

            try
            {
                File.Delete(fetchHeadPath);
                return true;
            }
            catch (IOException ex)
            {
                AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-FETCHHEAD", "Could not delete stale FETCH_HEAD: " + fetchHeadPath + " " + ex.Message);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-FETCHHEAD", "Could not delete stale FETCH_HEAD: " + fetchHeadPath + " " + ex.Message);
                return false;
            }
        }

        private static IEnumerable<string> SparseCheckoutPatterns()
        {
            return new[]
            {
                "package.json",
                "*.package.json",
                "**/package.json",
                "**/*.package.json",
                "**/*.dll",
                "**/*.png",
                "**/*.jpg",
                "**/*.jpeg",
                "**/*.ico",
                "**/*.bmp",
                "**/*.webp"
            };
        }

        private static bool RunGit(string arguments, string repositoryId, ICollection<DiagnosticMessage> diagnostics, bool reportFailure = true)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        if (reportFailure) AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-GIT", "Could not start git process.");
                        return false;
                    }

                    if (!process.WaitForExit(30000))
                    {
                        try { process.Kill(); } catch (Exception) { }
                        if (reportFailure) AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-GIT", "Git operation timed out.");
                        return false;
                    }

                    var error = process.StandardError.ReadToEnd();
                    if (process.ExitCode != 0)
                    {
                        if (reportFailure) AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-GIT", string.IsNullOrWhiteSpace(error) ? "Git operation failed." : error.Trim());
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                if (reportFailure) AddDiagnostic(diagnostics, repositoryId, "PH-REPOSITORY-GIT", ex.Message);
                return false;
            }
        }

        private static string RepositoryCacheDirectory(string baseDirectory, PackageRepositoryConfiguration repository)
        {
            return Path.Combine(baseDirectory, "repository-cache", SafePathSegment(repository.Id));
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

        private static bool IsSupportedRepositoryProvider(string provider)
        {
            return string.Equals(provider, "github", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "gitee", StringComparison.OrdinalIgnoreCase);
        }

        private static string RepositoryHost(string provider)
        {
            return string.Equals(provider, "gitee", StringComparison.OrdinalIgnoreCase)
                ? "https://gitee.com/"
                : "https://github.com/";
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static string SafePathSegment(string value)
        {
            var chars = (value ?? string.Empty)
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_')
                .ToArray();
            var segment = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(segment) ? "package" : segment;
        }

        private static void AddDiagnostic(ICollection<DiagnosticMessage> diagnostics, string repositoryId, string code, string message)
        {
            AddDiagnostic(diagnostics, repositoryId, code, message, DiagnosticSeverity.Warning);
        }

        private static void AddDiagnostic(ICollection<DiagnosticMessage> diagnostics, string repositoryId, string code, string message, DiagnosticSeverity severity)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                ModuleId = repositoryId ?? string.Empty,
                Severity = severity,
                Code = code ?? string.Empty,
                Message = SensitiveTextRedactor.Redact(message ?? string.Empty)
            });
        }
    }
}
