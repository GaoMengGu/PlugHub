using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Packages;

namespace PlugHub.Framework.Settings
{
    public sealed class SettingsConfigurationStore
    {
        private const string SourcesFileName = "sources.json";
        private const string DefaultPackageManifestName = "packages.json";
        private const string AdjacentPackageManifestPattern = "*.packages.json";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };
        private readonly PackageManifestWriter _packageManifestWriter = new PackageManifestWriter();
        private readonly HashSet<string> _loadedManifestPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public SettingsConfigurationStore(string configDirectory)
        {
            ConfigDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
        }

        public string ConfigDirectory { get; }

        public FrameworkConfiguration Load(FrameworkConfiguration current)
        {
            return current ?? throw new ArgumentNullException(nameof(current));
        }

        public FrameworkConfiguration LoadConfiguration()
        {
            return FrameworkConfigurationLoader.LoadFromDirectory(ConfigDirectory);
        }

        public List<ModuleManifestDocument> LoadModuleDocuments(FrameworkConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            _loadedManifestPaths.Clear();

            var documents = new List<ModuleManifestDocument>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddModuleDocument(documents, seenPaths, Path.Combine(ConfigDirectory, SourcesFileName), configuration.Modules);

            var baseDirectory = BaseDirectory();
            foreach (var packageDirectory in configuration.Modules.PackageDirectories ?? new List<string>())
            {
                foreach (var manifestPath in FindModuleManifests(ResolvePath(baseDirectory, packageDirectory)))
                {
                    var manifest = TryReadModulesConfiguration(manifestPath);
                    if (manifest != null)
                    {
                        AddModuleDocument(documents, seenPaths, manifestPath, manifest);
                    }
                }
            }

            foreach (var source in (configuration.Modules.ModuleSources ?? new List<ModuleSourceConfiguration>())
                .Where(source => source.Enabled && string.Equals(source.Type, "localFolder", StringComparison.OrdinalIgnoreCase)))
            {
                var sourceDirectory = ResolveSourceDirectory(baseDirectory, source);
                if (IsDefaultManifestPath(source.ManifestPath))
                {
                    foreach (var manifestPath in FindModuleManifests(sourceDirectory))
                    {
                        var manifest = TryReadModulesConfiguration(manifestPath);
                        if (manifest != null)
                        {
                            AddModuleDocument(documents, seenPaths, manifestPath, manifest);
                        }
                    }

                    continue;
                }

                var explicitManifestPath = ResolveManifestPath(sourceDirectory, source.ManifestPath.Trim());
                if (string.IsNullOrWhiteSpace(explicitManifestPath))
                {
                    continue;
                }

                var explicitManifest = TryReadModulesConfiguration(explicitManifestPath);
                if (explicitManifest != null)
                {
                    AddModuleDocument(documents, seenPaths, explicitManifestPath, explicitManifest);
                }
            }

            foreach (var document in documents)
            {
                _loadedManifestPaths.Add(Path.GetFullPath(document.Path));
            }

            return documents;
        }

        public void Save(FrameworkConfiguration configuration, IEnumerable<ModuleManifestDocument> moduleDocuments)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (moduleDocuments == null) throw new ArgumentNullException(nameof(moduleDocuments));
            var documents = moduleDocuments.ToList();
            ValidateOwnedDocuments(documents);

            Directory.CreateDirectory(ConfigDirectory);
            foreach (var document in documents)
            {
                SaveModuleDocument(document);
            }

            SaveJson(Path.Combine(ConfigDirectory, "views.json"), configuration.Views);
            SaveJson(Path.Combine(ConfigDirectory, "feature-combinations.json"), configuration.FeatureCombinations);
        }

        public string BaseDirectory()
        {
            return Directory.GetParent(ConfigDirectory)?.FullName ?? ConfigDirectory;
        }

        private void SaveJson(string path, object value)
        {
            File.WriteAllText(path, _serializer.Serialize(value));
        }

        private void ValidateOwnedDocuments(IEnumerable<ModuleManifestDocument> documents)
        {
            foreach (var document in documents)
            {
                if (document == null)
                {
                    throw new InvalidOperationException("Settings cannot save a null module manifest document.");
                }

                var fullPath = Path.GetFullPath(document.Path);
                if (!_loadedManifestPaths.Contains(fullPath))
                {
                    throw new InvalidOperationException("Settings cannot save a module manifest that was not loaded by this store: " + fullPath);
                }
            }
        }

        private void SaveModuleDocument(ModuleManifestDocument document)
        {
            if (IsModulesManifestFileName(Path.GetFileName(document.Path)))
            {
                SavePackageManifest(document.Path, document.Modules);
                return;
            }

            SaveJson(document.Path, document.Modules);
        }

        private void SavePackageManifest(string path, ModulesConfiguration modules)
        {
            _packageManifestWriter.WritePackageManifest(path, modules);
        }

        private ModulesConfiguration? TryReadModulesConfiguration(string path)
        {
            if (!File.Exists(path)) return null;

            Dictionary<string, object>? root;
            try
            {
                root = _serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }

            if (root == null || !ContainsKey(root, "schemaVersion") || !ContainsKey(root, "modules")) return null;

            var modules = _serializer.Deserialize<ModulesConfiguration>(File.ReadAllText(path));
            NormalizePackageManifestDefaults(root, modules);
            ApplyResolvedBaseDirectory(path, modules);
            return modules;
        }

        private static void ApplyResolvedBaseDirectory(string path, ModulesConfiguration? modules)
        {
            if (modules == null) return;
            var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
            foreach (var module in modules.Modules ?? new List<ModuleConfiguration>())
            {
                if (string.IsNullOrWhiteSpace(module.ResolvedBaseDirectory))
                {
                    module.ResolvedBaseDirectory = baseDirectory;
                }
            }
        }

        private static void NormalizePackageManifestDefaults(Dictionary<string, object> root, ModulesConfiguration? modules)
        {
            if (modules == null) return;

            PackageManifestDefaults.NormalizeModuleState(root, modules);
        }

        private static bool ContainsKey(Dictionary<string, object> source, string key)
        {
            return source.Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddModuleDocument(ICollection<ModuleManifestDocument> documents, ISet<string> seenPaths, string path, ModulesConfiguration modules)
        {
            if (string.IsNullOrWhiteSpace(path) || modules == null) return;
            var fullPath = Path.GetFullPath(path);
            var fileName = Path.GetFileName(fullPath);
            if (!File.Exists(fullPath)
                && !IsModulesManifestFileName(fileName)
                && !string.Equals(fileName, SourcesFileName, StringComparison.OrdinalIgnoreCase)) return;
            if (!seenPaths.Add(fullPath)) return;
            documents.Add(new ModuleManifestDocument(fullPath, modules));
        }

        private static IEnumerable<string> FindModuleManifests(string sourceDirectory)
        {
            if (!Directory.Exists(sourceDirectory)) yield break;

            var rootManifest = Path.Combine(sourceDirectory, DefaultPackageManifestName);
            if (File.Exists(rootManifest) && IsOutsideGitDirectory(rootManifest))
            {
                yield return rootManifest;
            }

            var manifests = Directory.GetFiles(sourceDirectory, DefaultPackageManifestName, SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(sourceDirectory, AdjacentPackageManifestPattern, SearchOption.AllDirectories))
                .Where(path => !string.Equals(path, rootManifest, StringComparison.OrdinalIgnoreCase))
                .Where(IsOutsideGitDirectory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var manifest in manifests)
            {
                yield return manifest;
            }
        }

        private static bool IsOutsideGitDirectory(string path)
        {
            return path.IndexOf(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsModulesManifestFileName(string fileName)
        {
            return string.Equals(fileName, DefaultPackageManifestName, StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".packages.json", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolvePath(string baseDirectory, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return baseDirectory;
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
        }

        private static string ResolveManifestPath(string sourceDirectory, string manifestPath)
        {
            var fullSourceDirectory = Path.GetFullPath(sourceDirectory);
            var fullManifestPath = Path.GetFullPath(Path.Combine(fullSourceDirectory, manifestPath ?? string.Empty));
            return IsUnderDirectory(fullSourceDirectory, fullManifestPath) ? fullManifestPath : string.Empty;
        }

        private static bool IsUnderDirectory(string parentDirectory, string childPath)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveSourceDirectory(string baseDirectory, ModuleSourceConfiguration source)
        {
            if (source == null) return baseDirectory;
            if (!string.IsNullOrWhiteSpace(source.Path)) return ResolvePath(baseDirectory, source.Path);

            return baseDirectory;
        }

        private static bool IsDefaultManifestPath(string manifestPath)
        {
            return string.IsNullOrWhiteSpace(manifestPath)
                || string.Equals(manifestPath.Trim(), DefaultPackageManifestName, StringComparison.OrdinalIgnoreCase);
        }

        public sealed class ModuleManifestDocument
        {
            public ModuleManifestDocument(string path, ModulesConfiguration modules)
            {
                Path = path ?? string.Empty;
                Modules = modules ?? new ModulesConfiguration();
            }

            public string Path { get; }
            public ModulesConfiguration Modules { get; }
        }
    }
}
