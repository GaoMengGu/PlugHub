using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Sources
{
    public sealed class ModuleSourceResolver
    {
        private const string DefaultModulesManifestName = "modules.json";
        private const string AdjacentModulesManifestPattern = "*.modules.json";

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
                }
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

        private static IEnumerable<string> FindModuleManifests(string sourceDirectory)
        {
            var rootManifest = Path.Combine(sourceDirectory, DefaultModulesManifestName);
            if (File.Exists(rootManifest))
            {
                yield return rootManifest;
            }

            var manifests = Directory.GetFiles(sourceDirectory, DefaultModulesManifestName, SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(sourceDirectory, AdjacentModulesManifestPattern, SearchOption.AllDirectories))
                .Where(path => !string.Equals(path, rootManifest, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var manifest in manifests)
            {
                yield return manifest;
            }
        }

        private bool AddModulesFromManifest(ModuleSourceConfiguration source, string sourceDirectory, ModulesConfiguration resolved, ICollection<DiagnosticMessage> diagnostics, bool ignoreNonPlugHubManifest = false)
        {
            var manifestPath = Path.Combine(sourceDirectory, string.IsNullOrWhiteSpace(source.ManifestPath) ? DefaultModulesManifestName : source.ManifestPath);
            if (!File.Exists(manifestPath))
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", "Module source manifest was not found: " + manifestPath);
                return false;
            }

            try
            {
                if (!TryReadPlugHubManifest(manifestPath, out var sourceModules, out var manifestError))
                {
                    if (!ignoreNonPlugHubManifest)
                    {
                        AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", manifestError);
                    }

                    return false;
                }

                var loadedAny = false;
                foreach (var module in sourceModules.Modules ?? new List<ModuleConfiguration>())
                {
                    module.SourceId = string.IsNullOrWhiteSpace(module.SourceId) ? source.Id : module.SourceId;
                    module.ResolvedBaseDirectory = sourceDirectory;
                    resolved.Modules.Add(module);
                    loadedAny = true;
                }

                return loadedAny;
            }
            catch (Exception ex)
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", ex.Message);
                return false;
            }
        }

        private bool TryReadPlugHubManifest(string manifestPath, out ModulesConfiguration modules, out string error)
        {
            modules = new ModulesConfiguration();
            error = string.Empty;

            var text = File.ReadAllText(manifestPath);
            var root = _serializer.Deserialize<Dictionary<string, object>>(text);
            if (root == null || !ContainsKey(root, "schemaVersion") || !ContainsKey(root, "modules"))
            {
                error = "Manifest is not a PlugHub modules manifest: " + manifestPath;
                return false;
            }

            modules = _serializer.Deserialize<ModulesConfiguration>(text) ?? new ModulesConfiguration();
            NormalizeRepositoryModuleDefaults(root, modules);
            PushRootCompatibilityToModules(modules);
            return true;
        }

        private static void NormalizeRepositoryModuleDefaults(Dictionary<string, object> root, ModulesConfiguration modules)
        {
            var moduleObjects = ArrayValue(root, "modules")
                .OfType<Dictionary<string, object>>()
                .Select(item => new { Id = StringValue(item, "id"), Value = item })
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

            foreach (var module in modules.Modules ?? new List<ModuleConfiguration>())
            {
                if (!moduleObjects.TryGetValue(module.Id ?? string.Empty, out var moduleObject))
                {
                    continue;
                }

                if (!ContainsKey(moduleObject, "enabled"))
                {
                    module.Enabled = true;
                }

                if (!ContainsKey(moduleObject, "visible"))
                {
                    module.Visible = true;
                }
            }
        }

        private static void PushRootCompatibilityToModules(ModulesConfiguration modules)
        {
            foreach (var module in modules.Modules ?? new List<ModuleConfiguration>())
            {
                if ((module.RevitVersions == null || module.RevitVersions.Count == 0) && modules.RevitVersions != null)
                {
                    module.RevitVersions = new List<string>(modules.RevitVersions);
                }

                if (string.IsNullOrWhiteSpace(module.FrameworkVersionRange))
                {
                    module.FrameworkVersionRange = modules.FrameworkVersionRange ?? string.Empty;
                }
            }
        }

        private static ModulesConfiguration CloneModules(ModulesConfiguration modules)
        {
            return new ModulesConfiguration
            {
                SchemaVersion = modules.SchemaVersion,
                IndexVersion = modules.IndexVersion,
                RevitVersions = new List<string>(modules.RevitVersions ?? new List<string>()),
                FrameworkVersionRange = modules.FrameworkVersionRange,
                PackageDirectories = new List<string>(modules.PackageDirectories ?? new List<string>()),
                ModuleSources = (modules.ModuleSources ?? new List<ModuleSourceConfiguration>())
                    .Select(source => new ModuleSourceConfiguration
                    {
                        Id = source.Id,
                        Type = source.Type,
                        Path = source.Path,
                        Repository = source.Repository,
                        Ref = source.Ref,
                        ManifestPath = source.ManifestPath,
                        Enabled = source.Enabled
                    })
                    .ToList(),
                Repositories = (modules.Repositories ?? new List<PackageRepositoryConfiguration>())
                    .Select(repository => new PackageRepositoryConfiguration
                    {
                        Id = repository.Id,
                        Provider = repository.Provider,
                        Visibility = repository.Visibility,
                        Repository = repository.Repository,
                        Ref = repository.Ref,
                        ManifestPath = repository.ManifestPath,
                        ApiKey = repository.ApiKey,
                        EncryptedApiKey = repository.EncryptedApiKey,
                        ApiKeyProtection = repository.ApiKeyProtection,
                        Enabled = repository.Enabled
                    })
                    .ToList(),
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

        private static bool ContainsKey(Dictionary<string, object> source, string key)
        {
            return source.Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<object> ArrayValue(Dictionary<string, object> root, string key)
        {
            return TryGetValue(root, key, out var value) && value is System.Collections.ArrayList list
                ? list.Cast<object>()
                : Enumerable.Empty<object>();
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return TryGetValue(source, key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
        }

        private static bool TryGetValue(Dictionary<string, object> source, string key, out object value)
        {
            foreach (var item in source)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }

            value = new object();
            return false;
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
