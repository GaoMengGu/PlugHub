using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Packages
{
    public sealed class PackageManifestReader
    {
        private const string DefaultModulesManifestName = "modules.json";
        private const string AdjacentModulesManifestPattern = "*.modules.json";
        private const string PackagesDirectoryName = "packages";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public IReadOnlyList<RepositoryPackageDescriptor> ReadPackagesFromManifest(
            string manifestPath,
            string repositoryId,
            string baseDirectory,
            Func<string, string, string, string> installedPackageVersion,
            Func<string, string, string, bool> isModuleInstalled,
            Func<string, string, string, string> pendingOperationFor)
        {
            if (!TryReadManifest(manifestPath, out var root, out var modules))
            {
                return new List<RepositoryPackageDescriptor>();
            }

            var sourceDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
            var moduleList = modules.Modules ?? new List<ModuleConfiguration>();

            return moduleList
                .Where(module => !string.IsNullOrWhiteSpace(module.Id))
                .Select(module =>
                {
                    var packageId = module.Id;
                    var version = module.Version ?? string.Empty;
                    var installedDirectory = InstalledPackageDirectory(baseDirectory, packageId);
                    var installedVersion = installedPackageVersion(baseDirectory, installedDirectory, module.Id);
                    var displayName = RepositoryPackageDisplayName(module, packageId);
                    var features = module.Features ?? new List<FeatureConfiguration>();

                    return new RepositoryPackageDescriptor
                    {
                        RepositoryId = repositoryId ?? string.Empty,
                        PackageId = packageId,
                        ModuleId = module.Id,
                        DisplayName = displayName,
                        Version = version,
                        ManifestPath = manifestPath,
                        SourceDirectory = sourceDirectory,
                        InstallDirectory = installedDirectory,
                        IsInstalled = isModuleInstalled(baseDirectory, installedDirectory, module.Id),
                        InstalledVersion = installedVersion,
                        PendingOperation = pendingOperationFor(baseDirectory, packageId, module.Id),
                        Description = FirstNonEmpty(module.Description, features.Select(feature => feature.Description).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty),
                        Tags = DistinctText((module.Tags ?? new List<string>()).Concat(features.SelectMany(feature => feature.Tags ?? new List<string>()))),
                        Categories = DistinctText(new[] { module.Category }.Concat(features.Select(feature => feature.Category)))
                    };
                })
                .ToList();
        }

        public IEnumerable<string> FindPackageManifests(string sourceDirectory)
        {
            if (!Directory.Exists(sourceDirectory)) yield break;

            var rootManifest = Path.Combine(sourceDirectory, DefaultModulesManifestName);
            if (File.Exists(rootManifest))
            {
                yield return rootManifest;
            }

            var manifests = Directory.GetFiles(sourceDirectory, DefaultModulesManifestName, SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(sourceDirectory, AdjacentModulesManifestPattern, SearchOption.AllDirectories))
                .Where(path => !string.Equals(path, rootManifest, StringComparison.OrdinalIgnoreCase))
                .Where(path => path.IndexOf(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var manifest in manifests)
            {
                yield return manifest;
            }
        }

        public bool TryReadManifest(string manifestPath, out Dictionary<string, object> root, out ModulesConfiguration modules)
        {
            root = new Dictionary<string, object>();
            modules = new ModulesConfiguration();
            try
            {
                var text = File.ReadAllText(manifestPath);
                root = _serializer.Deserialize<Dictionary<string, object>>(text);
                if (root == null || !ContainsKey(root, "schemaVersion") || !ContainsKey(root, "modules"))
                {
                    root = new Dictionary<string, object>();
                    return false;
                }

                modules = _serializer.Deserialize<ModulesConfiguration>(text) ?? new ModulesConfiguration();
                NormalizeRepositoryModuleDefaults(root, modules);
                return true;
            }
            catch (Exception)
            {
                root = new Dictionary<string, object>();
                modules = new ModulesConfiguration();
                return false;
            }
        }

        public ModuleConfiguration? FindModule(ModulesConfiguration modules, RepositoryPackageDescriptor package)
        {
            return (modules.Modules ?? new List<ModuleConfiguration>())
                .FirstOrDefault(item => string.Equals(item.Id, package.ModuleId, StringComparison.OrdinalIgnoreCase))
                ?? (modules.Modules ?? new List<ModuleConfiguration>())
                    .FirstOrDefault(item => string.Equals(item.Id, package.PackageId, StringComparison.OrdinalIgnoreCase));
        }

        public Dictionary<string, object>? FindModuleObject(Dictionary<string, object> root, string moduleId)
        {
            return ArrayValue(root, "modules")
                .OfType<Dictionary<string, object>>()
                .FirstOrDefault(item => string.Equals(StringValue(item, "id"), moduleId, StringComparison.OrdinalIgnoreCase));
        }

        public bool ManifestContainsModule(string manifestPath, string moduleId)
        {
            return TryReadManifest(manifestPath, out _, out var modules)
                && ManifestContainsModule(modules, moduleId);
        }

        public bool ManifestContainsModule(ModulesConfiguration modules, string moduleId)
        {
            return (modules.Modules ?? new List<ModuleConfiguration>())
                .Any(module => string.Equals(module.Id, moduleId, StringComparison.OrdinalIgnoreCase));
        }

        public bool TryWriteManifest(string manifestPath, Dictionary<string, object> root, out string error)
        {
            error = string.Empty;
            try
            {
                File.WriteAllText(manifestPath, _serializer.Serialize(root));
                return true;
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static IEnumerable<object> ArrayValue(Dictionary<string, object> root, string key)
        {
            return TryGetValue(root, key, out var value) && value is System.Collections.ArrayList list
                ? list.Cast<object>()
                : Enumerable.Empty<object>();
        }

        public static string StringValue(Dictionary<string, object> source, string key)
        {
            return TryGetValue(source, key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
        }

        public static bool TryGetValue(Dictionary<string, object> source, string key, out object value)
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

        private static string InstalledPackageDirectory(string baseDirectory, string packageId)
        {
            return Path.Combine(Path.GetFullPath(Path.Combine(baseDirectory, PackagesDirectoryName)), SafePathSegment(packageId));
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

        private static bool ContainsKey(Dictionary<string, object> source, string key)
        {
            return source.Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
        }

        private static string RepositoryPackageDisplayName(ModuleConfiguration module, string fallback)
        {
            var featureNames = (module.Features ?? new List<FeatureConfiguration>())
                .OrderBy(feature => feature.Order)
                .ThenBy(feature => feature.Id, StringComparer.OrdinalIgnoreCase)
                .Select(feature => FirstNonEmpty(feature.DisplayName, feature.Name, feature.Id))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            if (featureNames.Count > 0)
            {
                return string.Join("、", featureNames);
            }

            return FirstNonEmpty(module.DisplayName, module.Name, fallback);
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static List<string> DistinctText(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string SafePathSegment(string value)
        {
            var chars = (value ?? string.Empty)
                .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.' ? ch : '_')
                .ToArray();
            var segment = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(segment) ? "package" : segment;
        }
    }
}
