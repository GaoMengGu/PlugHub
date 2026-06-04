using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Packages
{
    public sealed class PackageManifestWriter
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public string SerializePackageManifest(ModulesConfiguration manifest, bool includeIndexVersion = true)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            return _serializer.Serialize(ToPackageManifest(manifest, includeIndexVersion));
        }

        public void WritePackageManifest(string path, ModulesConfiguration manifest, bool includeIndexVersion = true)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Manifest path is required.", nameof(path));
            File.WriteAllText(path, SerializePackageManifest(manifest, includeIndexVersion));
        }

        private static Dictionary<string, object> ToPackageManifest(ModulesConfiguration manifest, bool includeIndexVersion)
        {
            var root = new Dictionary<string, object>
            {
                ["schemaVersion"] = FirstNonEmpty(manifest.SchemaVersion, "1.0")
            };

            AddOptional(root, "indexVersion", includeIndexVersion ? manifest.IndexVersion : string.Empty);
            AddOptionalList(root, "revitVersions", manifest.RevitVersions);
            AddOptional(root, "frameworkVersionRange", manifest.FrameworkVersionRange);
            root["modules"] = (manifest.Modules ?? new List<ModuleConfiguration>())
                .Select(ToModuleObject)
                .ToList();
            return root;
        }

        private static Dictionary<string, object> ToModuleObject(ModuleConfiguration module)
        {
            var result = new Dictionary<string, object>
            {
                ["id"] = module.Id ?? string.Empty,
                ["version"] = module.Version ?? string.Empty,
                ["assembly"] = module.Assembly ?? string.Empty
            };

            AddOptional(result, "author", module.Author);
            AddOptional(result, "displayName", FirstNonEmpty(module.DisplayName, module.Name));
            AddOptional(result, "description", module.Description);
            AddOptional(result, "category", module.Category);
            AddOptionalList(result, "tags", module.Tags);
            AddOptionalList(result, "revitVersions", module.RevitVersions);
            AddOptional(result, "frameworkVersionRange", module.FrameworkVersionRange);
            result["features"] = (module.Features ?? new List<FeatureConfiguration>())
                .Select(ToFeatureObject)
                .ToList();
            return result;
        }

        private static Dictionary<string, object> ToFeatureObject(FeatureConfiguration feature)
        {
            var result = new Dictionary<string, object>
            {
                ["id"] = feature.Id ?? string.Empty,
                ["displayName"] = FirstNonEmpty(feature.DisplayName, feature.Name, feature.Id),
                ["commandType"] = feature.CommandType ?? string.Empty
            };

            AddOptional(result, "description", feature.Description);
            AddOptional(result, "iconPath", feature.IconPath);
            return result;
        }

        private static void AddOptional(IDictionary<string, object> target, string key, string? value)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length > 0)
            {
                target[key] = trimmed;
            }
        }

        private static void AddOptionalList(IDictionary<string, object> target, string key, IEnumerable<string>? values)
        {
            var items = DistinctText(values);
            if (items.Count > 0)
            {
                target[key] = items;
            }
        }

        private static List<string> DistinctText(IEnumerable<string>? values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return (values ?? new string?[0]).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
