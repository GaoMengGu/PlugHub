using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Contracts.Features;

namespace PlugHub.Framework.Configuration
{
    public sealed class FrameworkConfigurationLoader
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public static FrameworkConfiguration LoadFromDirectory(string configDirectory)
        {
            return new FrameworkConfigurationLoader().Load(configDirectory);
        }

        public FrameworkConfiguration Load(string configDirectory)
        {
            if (string.IsNullOrWhiteSpace(configDirectory)) throw new ArgumentException("Config directory is required.", nameof(configDirectory));

            return new FrameworkConfiguration
            {
                Modules = ReadJson<ModulesConfiguration>(Path.Combine(configDirectory, "modules.json")),
                Views = ReadJson<ViewsConfiguration>(Path.Combine(configDirectory, "views.json")),
                FeatureCombinations = ReadOptionalJson<FeatureCombinationsConfiguration>(Path.Combine(configDirectory, "feature-combinations.json"))
            };
        }

        public FrameworkRuntimeConfiguration LoadRuntime(string configDirectory)
        {
            var configuration = Load(configDirectory);
            var view = GetDefaultView(configuration);
            var preset = GetPresetForView(configuration, view);
            var effectiveModules = ApplyPreset(configuration.Modules, preset);

            return new FrameworkRuntimeConfiguration(configuration, view, preset, effectiveModules);
        }

        public IReadOnlyList<FeatureDescriptor> ToFeatureDescriptors(FrameworkConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            return ToFeatureDescriptors(configuration.Modules, GetPresetForView(configuration, GetDefaultView(configuration)));
        }

        public IReadOnlyList<FeatureDescriptor> ToFeatureDescriptors(FrameworkRuntimeConfiguration runtimeConfiguration)
        {
            if (runtimeConfiguration == null) throw new ArgumentNullException(nameof(runtimeConfiguration));

            return ToFeatureDescriptors(runtimeConfiguration.EffectiveModules);
        }

        public IReadOnlyList<FeatureDescriptor> ToFeatureDescriptors(ModulesConfiguration modules, FeatureCombinationPresetConfiguration? preset = null)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));

            var seenFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var features = new List<FeatureDescriptor>();

            foreach (var module in ResolveModules(modules, preset))
            {
                foreach (var feature in module.Features ?? new List<FeatureConfiguration>())
                {
                    if (!seenFeatureIds.Add(feature.Id)) continue;
                    features.Add(ToDescriptor(module, feature));
                }
            }

            return features;
        }

        public ViewConfiguration GetDefaultView(FrameworkConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var views = configuration.Views.Views ?? new List<ViewConfiguration>();
            var configuredDefault = views.FirstOrDefault(v => string.Equals(v.Id, configuration.Views.DefaultView, StringComparison.OrdinalIgnoreCase));
            if (configuredDefault != null) return configuredDefault;
            if (views.Any()) return views[0];

            return new ViewConfiguration
            {
                Id = "default",
                Name = "PlugHub",
                Ribbon = new RibbonConfiguration()
            };
        }

        public FeatureCombinationPresetConfiguration? GetPresetForView(FrameworkConfiguration configuration, ViewConfiguration view)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (view == null) throw new ArgumentNullException(nameof(view));

            var featureCombinations = configuration.FeatureCombinations;
            var presets = featureCombinations?.Presets ?? new List<FeatureCombinationPresetConfiguration>();
            if (presets.Count == 0) return null;

            var matchedPreset = presets.FirstOrDefault(p => string.Equals(p.ViewId, view.Id, StringComparison.OrdinalIgnoreCase));
            if (matchedPreset != null) return matchedPreset;

            if (!string.IsNullOrWhiteSpace(featureCombinations?.DefaultPreset))
            {
                var configuredDefault = presets.FirstOrDefault(p => string.Equals(p.Id, featureCombinations!.DefaultPreset, StringComparison.OrdinalIgnoreCase));
                if (configuredDefault != null) return configuredDefault;
            }

            return null;
        }

        public ModulesConfiguration ApplyPreset(ModulesConfiguration modules, FeatureCombinationPresetConfiguration? preset)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));

            var overridesByModuleId = (preset?.ModuleOverrides ?? new List<ModuleOverrideConfiguration>())
                .Where(overrideItem => !string.IsNullOrWhiteSpace(overrideItem.ModuleId))
                .GroupBy(overrideItem => overrideItem.ModuleId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            return new ModulesConfiguration
            {
                SchemaVersion = modules.SchemaVersion,
                ModuleDirectories = new List<string>(modules.ModuleDirectories ?? new List<string>()),
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
                ConflictPolicy = new ConflictPolicyConfiguration
                {
                    DuplicateFeatureId = modules.ConflictPolicy.DuplicateFeatureId,
                    DuplicateModuleId = modules.ConflictPolicy.DuplicateModuleId,
                    MissingModuleType = modules.ConflictPolicy.MissingModuleType
                },
                Modules = (modules.Modules ?? new List<ModuleConfiguration>())
                    .Select(module => ApplyOverride(module, overridesByModuleId.TryGetValue(module.Id, out var moduleOverride) ? moduleOverride : null))
                    .ToList()
            };
        }

        private T ReadJson<T>(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Required PlugHub configuration file was not found.", path);
            return _serializer.Deserialize<T>(File.ReadAllText(path));
        }

        private T ReadOptionalJson<T>(string path) where T : new()
        {
            if (!File.Exists(path)) return new T();
            return _serializer.Deserialize<T>(File.ReadAllText(path));
        }

        private IReadOnlyList<ModuleConfiguration> ResolveModules(ModulesConfiguration modules, FeatureCombinationPresetConfiguration? preset)
        {
            return ApplyPreset(modules, preset).Modules
                .Where(module => module.Enabled && module.Visible)
                .OrderBy(module => module.Order)
                .ThenBy(module => module.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ModuleConfiguration ApplyOverride(ModuleConfiguration module, ModuleOverrideConfiguration? moduleOverride)
        {
            if (moduleOverride == null) return module;

            return new ModuleConfiguration
            {
                Id = module.Id,
                Assembly = module.Assembly,
                Type = module.Type,
                Name = module.Name,
                DisplayName = module.DisplayName,
                Description = module.Description,
                SourceId = module.SourceId,
                ResolvedBaseDirectory = module.ResolvedBaseDirectory,
                Enabled = moduleOverride.Enabled ?? module.Enabled,
                Visible = moduleOverride.Visible ?? module.Visible,
                Order = moduleOverride.Order ?? module.Order,
                Tags = new List<string>(module.Tags ?? new List<string>()),
                DependsOn = new List<string>(module.DependsOn ?? new List<string>()),
                Features = (module.Features ?? new List<FeatureConfiguration>()).Select(feature => new FeatureConfiguration
                {
                    Id = feature.Id,
                    Name = feature.Name,
                    DisplayName = feature.DisplayName,
                    Description = feature.Description,
                    Category = feature.Category,
                    Group = feature.Group,
                    Tags = new List<string>(feature.Tags ?? new List<string>()),
                    Order = feature.Order,
                    DefaultState = feature.DefaultState,
                    CommandKey = feature.CommandKey,
                    CommandAssembly = feature.CommandAssembly,
                    CommandType = feature.CommandType,
                    ButtonSize = feature.ButtonSize,
                    IconPath = feature.IconPath
                }).ToList()
            };
        }

        private static FeatureDescriptor ToDescriptor(ModuleConfiguration module, FeatureConfiguration feature)
        {
            return new FeatureDescriptor
            {
                Id = feature.Id,
                ModuleId = module.Id,
                Name = DisplayNameResolver.Resolve(feature.DisplayName, feature.Name, string.Empty, feature.Id),
                Description = feature.Description,
                Category = feature.Category,
                Group = feature.Group,
                Tags = new List<string>(feature.Tags ?? new List<string>()),
                Order = feature.Order,
                DefaultState = ParseFeatureState(feature.DefaultState),
                CommandKey = feature.CommandKey,
                CommandAssembly = string.IsNullOrWhiteSpace(feature.CommandAssembly) ? module.Assembly : feature.CommandAssembly,
                CommandType = feature.CommandType,
                ButtonSize = string.IsNullOrWhiteSpace(feature.ButtonSize) ? "large" : feature.ButtonSize,
                IconPath = feature.IconPath
            };
        }

        private static FeatureState ParseFeatureState(string value)
        {
            return Enum.TryParse(value, true, out FeatureState state) ? state : FeatureState.Visible;
        }
    }

    public sealed class FrameworkRuntimeConfiguration
    {
        public FrameworkRuntimeConfiguration(
            FrameworkConfiguration configuration,
            ViewConfiguration activeView,
            FeatureCombinationPresetConfiguration? activePreset,
            ModulesConfiguration effectiveModules)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            ActiveView = activeView ?? throw new ArgumentNullException(nameof(activeView));
            ActivePreset = activePreset;
            EffectiveModules = effectiveModules ?? throw new ArgumentNullException(nameof(effectiveModules));
        }

        public FrameworkConfiguration Configuration { get; }
        public ViewConfiguration ActiveView { get; }
        public FeatureCombinationPresetConfiguration? ActivePreset { get; }
        public ModulesConfiguration EffectiveModules { get; }
    }
}
