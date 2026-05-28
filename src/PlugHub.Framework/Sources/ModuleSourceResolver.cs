using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Sources
{
    public sealed class ModuleSourceResolver
    {
        private const string DefaultPackageManifestName = "package.json";
        private const string AdjacentPackageManifestPattern = "*.package.json";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public ModuleSourceResolutionResult Resolve(string baseDirectory, ModulesConfiguration modules)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (modules == null) throw new ArgumentNullException(nameof(modules));

            var diagnostics = new List<DiagnosticMessage>();
            var resolved = CloneModules(modules);

            foreach (var packageDirectory in modules.PackageDirectories ?? new List<string>())
            {
                AddPackageDirectoryModules(baseDirectory, packageDirectory, resolved, diagnostics);
            }

            foreach (var source in modules.ModuleSources ?? new List<ModuleSourceConfiguration>())
            {
                if (!source.Enabled) continue;

                if (string.Equals(source.Type, "localFolder", StringComparison.OrdinalIgnoreCase))
                {
                    AddLocalFolderModules(baseDirectory, source, resolved, diagnostics);
                    continue;
                }

                if (string.Equals(source.Type, "github", StringComparison.OrdinalIgnoreCase))
                {
                    AddGitHubModules(baseDirectory, source, resolved, diagnostics);
                    continue;
                }

                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", "Unknown module source type: " + source.Type);
            }

            return new ModuleSourceResolutionResult(resolved, diagnostics);
        }

        private void AddLocalFolderModules(string baseDirectory, ModuleSourceConfiguration source, ModulesConfiguration resolved, ICollection<DiagnosticMessage> diagnostics)
        {
            var sourceDirectory = ResolvePath(baseDirectory, source.Path);
            if (!Directory.Exists(sourceDirectory))
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MISSING", "Module source folder was not found: " + sourceDirectory);
                return;
            }

            AddModulesFromManifest(source, sourceDirectory, resolved, diagnostics);
        }

        private void AddPackageDirectoryModules(string baseDirectory, string packageDirectory, ModulesConfiguration resolved, ICollection<DiagnosticMessage> diagnostics)
        {
            var sourceDirectory = ResolvePath(baseDirectory, packageDirectory);
            if (!Directory.Exists(sourceDirectory))
            {
                AddSourceDiagnostic(diagnostics, packageDirectory, "PH-SOURCE-MISSING", "Package directory was not found: " + sourceDirectory);
                return;
            }

            foreach (var manifestPath in FindModuleManifests(sourceDirectory))
            {
                var source = new ModuleSourceConfiguration
                {
                    Id = "directory:" + Path.GetFileName(Path.GetDirectoryName(manifestPath) ?? sourceDirectory),
                    Type = "localFolder",
                    Path = Path.GetDirectoryName(manifestPath) ?? sourceDirectory,
                    ManifestPath = Path.GetFileName(manifestPath),
                    Enabled = true
                };
                AddModulesFromManifest(source, Path.GetDirectoryName(manifestPath) ?? sourceDirectory, resolved, diagnostics, true);
            }
        }

        private void AddGitHubModules(string baseDirectory, ModuleSourceConfiguration source, ModulesConfiguration resolved, ICollection<DiagnosticMessage> diagnostics)
        {
            var sourceDirectory = ResolveGitHubCachePath(baseDirectory, source);
            if (source.AutoUpdate)
            {
                UpdateGitHubCache(source, sourceDirectory, diagnostics);
            }

            if (!Directory.Exists(sourceDirectory))
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MISSING", "GitHub module source cache was not found: " + sourceDirectory);
                return;
            }

            AddModulesFromManifest(source, sourceDirectory, resolved, diagnostics);
        }

        private static IEnumerable<string> FindModuleManifests(string sourceDirectory)
        {
            var rootManifest = Path.Combine(sourceDirectory, DefaultPackageManifestName);
            if (File.Exists(rootManifest))
            {
                yield return rootManifest;
            }

            var manifests = Directory.GetFiles(sourceDirectory, DefaultPackageManifestName, SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(sourceDirectory, AdjacentPackageManifestPattern, SearchOption.AllDirectories))
                .Where(path => !string.Equals(path, rootManifest, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var manifest in manifests)
            {
                yield return manifest;
            }
        }

        private void AddModulesFromManifest(ModuleSourceConfiguration source, string sourceDirectory, ModulesConfiguration resolved, ICollection<DiagnosticMessage> diagnostics, bool ignoreNonPlugHubManifest = false)
        {
            var manifestPath = Path.Combine(sourceDirectory, string.IsNullOrWhiteSpace(source.ManifestPath) ? DefaultPackageManifestName : source.ManifestPath);
            if (!File.Exists(manifestPath))
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", "Module source manifest was not found: " + manifestPath);
                return;
            }

            try
            {
                if (!TryReadPlugHubManifest(manifestPath, out var sourceModules, out var manifestError))
                {
                    if (!ignoreNonPlugHubManifest)
                    {
                        AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", manifestError);
                    }

                    return;
                }

                foreach (var module in sourceModules.Modules ?? new List<ModuleConfiguration>())
                {
                    module.SourceId = string.IsNullOrWhiteSpace(module.SourceId) ? source.Id : module.SourceId;
                    module.ResolvedBaseDirectory = sourceDirectory;
                    resolved.Modules.Add(module);
                }
            }
            catch (Exception ex)
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", ex.Message);
            }
        }

        private bool TryReadPlugHubManifest(string manifestPath, out ModulesConfiguration modules, out string error)
        {
            modules = new ModulesConfiguration();
            error = string.Empty;

            var text = File.ReadAllText(manifestPath);
            var root = _serializer.Deserialize<Dictionary<string, object>>(text);
            if (root == null || !root.ContainsKey("schemaVersion") || !root.ContainsKey("modules"))
            {
                error = "Manifest is not a PlugHub package manifest: " + manifestPath;
                return false;
            }

            modules = _serializer.Deserialize<ModulesConfiguration>(text) ?? new ModulesConfiguration();
            return true;
        }

        private static string ResolveGitHubCachePath(string baseDirectory, ModuleSourceConfiguration source)
        {
            if (!string.IsNullOrWhiteSpace(source.Path))
            {
                return ResolvePath(baseDirectory, source.Path);
            }

            var repository = string.IsNullOrWhiteSpace(source.Repository) ? source.Id : source.Repository;
            return Path.Combine(baseDirectory, "packages/github", SafePathSegment(repository));
        }

        private static void UpdateGitHubCache(ModuleSourceConfiguration source, string sourceDirectory, ICollection<DiagnosticMessage> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(source.Repository))
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-GIT", "GitHub module source requires repository when autoUpdate is enabled.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(sourceDirectory) ?? sourceDirectory);
            var repositoryUrl = source.Repository.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? source.Repository
                : "https://github.com/" + source.Repository.Trim().TrimEnd('/') + ".git";
            var gitRef = string.IsNullOrWhiteSpace(source.Ref) ? "main" : source.Ref.Trim();

            if (!Directory.Exists(Path.Combine(sourceDirectory, ".git")))
            {
                RunGit("clone --depth 1 --branch " + Quote(gitRef) + " " + Quote(repositoryUrl) + " " + Quote(sourceDirectory), source.Id, diagnostics);
                return;
            }

            RunGit("-C " + Quote(sourceDirectory) + " fetch --all --prune", source.Id, diagnostics);
            RunGit("-C " + Quote(sourceDirectory) + " checkout " + Quote(gitRef), source.Id, diagnostics);
            RunGit("-C " + Quote(sourceDirectory) + " pull --ff-only", source.Id, diagnostics);
        }

        private static void RunGit(string arguments, string sourceId, ICollection<DiagnosticMessage> diagnostics)
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
                        AddSourceDiagnostic(diagnostics, sourceId, "PH-SOURCE-GIT", "Could not start git process.");
                        return;
                    }

                    if (!process.WaitForExit(30000))
                    {
                        try { process.Kill(); } catch (Exception) { }
                        AddSourceDiagnostic(diagnostics, sourceId, "PH-SOURCE-GIT", "Git operation timed out.");
                        return;
                    }

                    if (process.ExitCode != 0)
                    {
                        var error = process.StandardError.ReadToEnd();
                        AddSourceDiagnostic(diagnostics, sourceId, "PH-SOURCE-GIT", string.IsNullOrWhiteSpace(error) ? "Git operation failed." : error.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                AddSourceDiagnostic(diagnostics, sourceId, "PH-SOURCE-GIT", ex.Message);
            }
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
            return string.IsNullOrWhiteSpace(segment) ? "github-source" : segment;
        }

        private static ModulesConfiguration CloneModules(ModulesConfiguration modules)
        {
            return new ModulesConfiguration
            {
                SchemaVersion = modules.SchemaVersion,
                PackageDirectories = new List<string>(modules.PackageDirectories ?? new List<string>()),
                ModuleSources = (modules.ModuleSources ?? new List<ModuleSourceConfiguration>()).Select(source => new ModuleSourceConfiguration
                {
                    Id = source.Id,
                    Type = source.Type,
                    Path = source.Path,
                    Repository = source.Repository,
                    Ref = source.Ref,
                    ManifestPath = source.ManifestPath,
                    Enabled = source.Enabled,
                    AutoUpdate = source.AutoUpdate
                }).ToList(),
                ConflictPolicy = modules.ConflictPolicy ?? new ConflictPolicyConfiguration(),
                Modules = new List<ModuleConfiguration>(modules.Modules ?? new List<ModuleConfiguration>())
            };
        }

        private static string ResolvePath(string baseDirectory, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return baseDirectory;
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
        }

        private static void AddSourceDiagnostic(ICollection<DiagnosticMessage> diagnostics, string sourceId, string code, string message)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                ModuleId = sourceId ?? string.Empty,
                Severity = DiagnosticSeverity.Warning,
                Code = code ?? string.Empty,
                Message = message ?? string.Empty
            });
        }
    }

    public sealed class ModuleSourceResolutionResult
    {
        public ModuleSourceResolutionResult(ModulesConfiguration modules, IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            Modules = modules ?? throw new ArgumentNullException(nameof(modules));
            Diagnostics = diagnostics ?? new List<DiagnosticMessage>();
        }

        public ModulesConfiguration Modules { get; }
        public IReadOnlyList<DiagnosticMessage> Diagnostics { get; }
    }
}
