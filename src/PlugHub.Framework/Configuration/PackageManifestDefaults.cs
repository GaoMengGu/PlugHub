using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.Framework.Configuration
{
    public static class PackageManifestDefaults
    {
        public static void NormalizeModuleState(Dictionary<string, object> root, ModulesConfiguration modules)
        {
            if (root == null || modules == null) return;

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

                if (!ContainsExactKey(moduleObject, "enabled"))
                {
                    module.Enabled = true;
                }

                if (!ContainsExactKey(moduleObject, "visible"))
                {
                    module.Visible = true;
                }

                NormalizeFeatureRuntimeFields(moduleObject, module);
            }
        }

        private static void NormalizeFeatureRuntimeFields(Dictionary<string, object> moduleObject, ModuleConfiguration module)
        {
            var featureObjects = ArrayValue(moduleObject, "features")
                .OfType<Dictionary<string, object>>()
                .Select(item => new { Id = StringValue(item, "id"), Value = item })
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

            foreach (var feature in module.Features ?? new List<FeatureConfiguration>())
            {
                if (!featureObjects.TryGetValue(feature.Id ?? string.Empty, out var featureObject))
                {
                    continue;
                }

                if (!ContainsExactKey(featureObject, "category"))
                {
                    feature.Category = string.Empty;
                }

                if (!ContainsExactKey(featureObject, "group"))
                {
                    feature.Group = string.Empty;
                }

                if (!ContainsExactKey(featureObject, "tags"))
                {
                    feature.Tags = new List<string>();
                }

                if (!ContainsExactKey(featureObject, "order"))
                {
                    feature.Order = 0;
                }

                if (!ContainsExactKey(featureObject, "defaultState"))
                {
                    feature.DefaultState = "Visible";
                }

                if (!ContainsExactKey(featureObject, "commandKey"))
                {
                    feature.CommandKey = string.Empty;
                }

                if (!ContainsExactKey(featureObject, "commandAssembly"))
                {
                    feature.CommandAssembly = string.Empty;
                }

                if (!ContainsExactKey(featureObject, "buttonSize"))
                {
                    feature.ButtonSize = string.Empty;
                }
            }
        }

        private static IEnumerable<object> ArrayValue(Dictionary<string, object> root, string key)
        {
            return TryGetValue(root, key, out var value) && value is ArrayList list
                ? list.Cast<object>()
                : Enumerable.Empty<object>();
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return TryGetValue(source, key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
        }

        private static bool TryGetValue(Dictionary<string, object> source, string key, out object value)
        {
            foreach (var item in source ?? new Dictionary<string, object>())
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

        private static bool ContainsExactKey(Dictionary<string, object> source, string key)
        {
            return source != null && source.Keys.Any(item => string.Equals(item, key, StringComparison.Ordinal));
        }
    }
}
