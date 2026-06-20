using System;
using System.Collections.Generic;
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
        private readonly RepositoryArchiveSynchronizer _archiveSynchronizer;
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
            _archiveSynchronizer = new RepositoryArchiveSynchronizer(_credentialService);
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

            if (RepositoryAddress.IsLocal(repository))
            {
                var localDirectory = LocalRepositoryDirectory(repository);
                if (string.IsNullOrWhiteSpace(localDirectory) || !Directory.Exists(localDirectory))
                {
                    AddDiagnostic(messages, repository.Id, "PH-REPOSITORY-LOCAL", "Local repository folder was not found: " + repository.Repository);
                    return new List<RepositoryPackageDescriptor>();
                }

                return BrowseRepositoryDirectory(baseDirectory, repository.Id, localDirectory, repository.ManifestPath, out diagnostics);
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
                AddDiagnostic(messages, repositoryId, "PH-REPOSITORY-MANIFEST", "No PlugHub packages.json manifests were found in repository.");
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
            if (RepositoryAddress.IsLocal(repository))
            {
                return BrowseRepositoryDirectory(baseDirectory, repository.Id, LocalRepositoryDirectory(repository), repository.ManifestPath, out diagnostics);
            }

            var cacheDirectory = RepositoryCacheDirectory(baseDirectory, repository);
            return Directory.Exists(cacheDirectory)
                ? BrowseCached(baseDirectory, repository.Id, cacheDirectory, out diagnostics)
                : new List<RepositoryPackageDescriptor>();
        }

        public bool HasRepositoryCache(string baseDirectory, PackageRepositoryConfiguration repository)
        {
            if (repository == null) return false;
            if (RepositoryAddress.IsLocal(repository)) return Directory.Exists(LocalRepositoryDirectory(repository));
            return Directory.Exists(RepositoryCacheDirectory(baseDirectory, repository));
        }

        public string RepositoryUrl(PackageRepositoryConfiguration repository, bool includeCredential)
        {
            if (RepositoryAddress.IsLocal(repository)) return LocalRepositoryDirectory(repository);
            var provider = RepositoryAddress.NormalizeCloudProvider(repository.Provider);
            return RepositoryAddress.StripRepositorySuffix(RepositoryAddress.StripUrlUserInfo(repository.Repository.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? repository.Repository.Trim()
                : RepositoryHost(provider) + repository.Repository.Trim().TrimEnd('/')));
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
            return _archiveSynchronizer.Sync(repository, cacheDirectory, diagnostics);
        }

        private IReadOnlyList<RepositoryPackageDescriptor> BrowseRepositoryDirectory(string baseDirectory, string repositoryId, string repositoryDirectory, string manifestPath, out IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            var messages = new List<DiagnosticMessage>();
            diagnostics = messages;
            var fullRepositoryDirectory = Path.GetFullPath(repositoryDirectory);
            var packages = new List<RepositoryPackageDescriptor>();

            if (!string.IsNullOrWhiteSpace(manifestPath))
            {
                var fullManifestPath = Path.GetFullPath(Path.Combine(fullRepositoryDirectory, manifestPath));
                if (!IsUnderDirectory(fullRepositoryDirectory, fullManifestPath))
                {
                    AddDiagnostic(messages, repositoryId, "PH-REPOSITORY-MANIFEST", "Repository manifest path is outside the local repository folder: " + manifestPath);
                    return packages;
                }

                if (File.Exists(fullManifestPath))
                {
                    packages.AddRange(_manifestReader.ReadPackagesFromManifest(fullManifestPath, repositoryId, baseDirectory, _installedPackageVersion, _isModuleInstalled, _pendingOperationFor));
                }
            }

            if (packages.Count == 0)
            {
                foreach (var foundManifestPath in _manifestReader.FindPackageManifests(fullRepositoryDirectory))
                {
                    packages.AddRange(_manifestReader.ReadPackagesFromManifest(foundManifestPath, repositoryId, baseDirectory, _installedPackageVersion, _isModuleInstalled, _pendingOperationFor));
                }
            }

            if (packages.Count == 0)
            {
                AddDiagnostic(messages, repositoryId, "PH-REPOSITORY-MANIFEST", "No PlugHub packages.json manifests were found in repository.");
            }

            return packages
                .GroupBy(package => package.PackageId + "\n" + package.ModuleId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(package => package.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string RepositoryCacheDirectory(string baseDirectory, PackageRepositoryConfiguration repository)
        {
            return Path.Combine(baseDirectory, "repository-cache", SafePathSegment(repository.Id));
        }

        private static bool IsSupportedRepositoryProvider(string provider)
        {
            return string.Equals(provider, "github", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "gitee", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provider, "local", StringComparison.OrdinalIgnoreCase);
        }

        private static string LocalRepositoryDirectory(PackageRepositoryConfiguration repository)
        {
            return string.IsNullOrWhiteSpace(repository.Repository)
                ? string.Empty
                : Path.GetFullPath(repository.Repository.Trim());
        }

        private static bool IsUnderDirectory(string parentDirectory, string childPath)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private static string RepositoryHost(string provider)
        {
            return string.Equals(provider, "gitee", StringComparison.OrdinalIgnoreCase)
                ? "https://gitee.com/"
                : "https://github.com/";
        }

        private static string SafePathSegment(string value)
        {
            var chars = (value ?? string.Empty)
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_')
                .ToArray();
            var segment = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(segment) || segment.All(ch => ch == '.') ? "package" : segment;
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
