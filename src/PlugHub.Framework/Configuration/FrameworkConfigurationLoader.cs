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
                Modules = NormalizeModulesConfiguration(ReadOptionalJson(Path.Combine(configDirectory, "sources.json"), DefaultModulesConfiguration())),
                Views = ReadOptionalJson(Path.Combine(configDirectory, "views.json"), DefaultViewsConfiguration()),
                FeatureCombinations = ReadOptionalJson(Path.Combine(configDirectory, "feature-combinations.json"), DefaultFeatureCombinationsConfiguration())
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
                IndexVersion = modules.IndexVersion,
                RevitVersions = new List<string>(modules.RevitVersions ?? new List<string>()),
                FrameworkVersionRange = modules.FrameworkVersionRange,
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
                Repositories = (modules.Repositories ?? new List<PackageRepositoryConfiguration>()).Select(repository => new PackageRepositoryConfiguration
                {
                    Id = repository.Id,
                    DisplayName = repository.DisplayName,
                    Provider = repository.Provider,
                    Visibility = repository.Visibility,
                    Repository = repository.Repository,
                    Ref = repository.Ref,
                    ManifestPath = repository.ManifestPath,
                    ApiKey = repository.ApiKey,
                    EncryptedApiKey = repository.EncryptedApiKey,
                    ApiKeyProtection = repository.ApiKeyProtection,
                    Enabled = repository.Enabled
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

        private T ReadOptionalJson<T>(string path, T fallback)
        {
            if (!File.Exists(path)) return fallback;

            try
            {
                return _serializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback;
            }
            catch (Exception exception) when (IsOptionalJsonReadFailure(exception))
            {
                return fallback;
            }
        }

        private static bool IsOptionalJsonReadFailure(Exception exception)
        {
            return exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is NotSupportedException;
        }

        private static ModulesConfiguration NormalizeModulesConfiguration(ModulesConfiguration modules)
        {
            if (modules.PackageDirectories == null)
            {
                modules.PackageDirectories = new List<string>();
            }

            if (!modules.PackageDirectories.Any())
            {
                modules.PackageDirectories.Add("packages");
            }

            return modules;
        }

        private static ModulesConfiguration DefaultModulesConfiguration()
        {
            return new ModulesConfiguration
            {
                SchemaVersion = "1.0",
                PackageDirectories = new List<string> { "packages" },
                ModuleSources = new List<ModuleSourceConfiguration>(),
                Repositories = new List<PackageRepositoryConfiguration>(),
                ConflictPolicy = new ConflictPolicyConfiguration(),
                Modules = new List<ModuleConfiguration>()
            };
        }

        private static ViewsConfiguration DefaultViewsConfiguration()
        {
            return new ViewsConfiguration
            {
                SchemaVersion = "1.0",
                DefaultView = "workspace",
                Views = new List<ViewConfiguration>
                {
                    new ViewConfiguration
                    {
                        Id = "workspace",
                        Name = "PlugHub",
                        Ribbon = new RibbonConfiguration
                        {
                            TabName = "PlugHub",
                            FallbackPanelName = "External"
                        },
                        Groups = new List<ViewGroupConfiguration>(),
                        Sort = new List<string> { "group.order", "feature.order", "feature.name", "feature.id" }
                    }
                }
            };
        }

        private static FeatureCombinationsConfiguration DefaultFeatureCombinationsConfiguration()
        {
            return new FeatureCombinationsConfiguration
            {
                SchemaVersion = "1.0",
                DefaultPreset = string.Empty,
                Presets = new List<FeatureCombinationPresetConfiguration>()
            };
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
                Version = module.Version,
                Author = module.Author,
                Assembly = module.Assembly,
                Type = module.Type,
                Name = module.Name,
                DisplayName = module.DisplayName,
                Description = module.Description,
                Category = module.Category,
                SourceId = module.SourceId,
                ResolvedBaseDirectory = module.ResolvedBaseDirectory,
                RevitVersions = new List<string>(module.RevitVersions ?? new List<string>()),
                FrameworkVersionRange = module.FrameworkVersionRange,
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
                    Category = FirstNonEmpty(feature.Category, module.Category),
                    Group = feature.Group,
                    Tags = MergeTags(module.Tags ?? new List<string>(), feature.Tags),
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
                ModuleName = DisplayNameResolver.Resolve(module.DisplayName, module.Name, string.Empty, module.Id),
                Name = DisplayNameResolver.Resolve(feature.DisplayName, feature.Name, string.Empty, feature.Id),
                Description = feature.Description,
                Category = FirstNonEmpty(feature.Category, module.Category),
                Group = feature.Group,
                Tags = MergeTags(module.Tags, feature.Tags),
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

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static List<string> MergeTags(IEnumerable<string> moduleTags, IEnumerable<string> featureTags)
        {
            return (moduleTags ?? Enumerable.Empty<string>())
                .Concat(featureTags ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
