using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlugHub.Contracts.Features;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Discovery
{
    public sealed class ModuleDiscoveryService
    {
        private const string CurrentRevitVersion = "2020";

        public ModuleDiscoveryResult Discover(string baseDirectory, ModulesConfiguration modulesConfiguration)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (modulesConfiguration == null) throw new ArgumentNullException(nameof(modulesConfiguration));

            var diagnostics = new List<DiagnosticMessage>();
            var descriptors = new List<ModuleDescriptor>();

            foreach (var module in (modulesConfiguration.Modules ?? new List<ModuleConfiguration>()).OrderBy(module => module.Order).ThenBy(module => module.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (!IsCompatibleWithRuntime(module, out var compatibilityReason))
                {
                    diagnostics.Add(BuildDiagnostic(module.Id, DiagnosticSeverity.Warning, "RT-MODULE-COMPATIBILITY", compatibilityReason));
                    continue;
                }

                var descriptor = ToDescriptor(baseDirectory, module);
                descriptors.Add(descriptor);

                if (!module.Enabled || !module.Visible)
                {
                    diagnostics.Add(BuildDiagnostic(module.Id, DiagnosticSeverity.Info, "RT-MODULE-SKIP", "Module is disabled or hidden and was not discovered."));
                }
            }

            return new ModuleDiscoveryResult(descriptors, diagnostics);
        }

        private static bool IsCompatibleWithRuntime(ModuleConfiguration module, out string reason)
        {
            reason = string.Empty;
            if (module == null) return true;

            // FrameworkVersionRange is preserved as package metadata; runtime range evaluation is intentionally not enforced yet.
            var revitVersions = (module.RevitVersions ?? new List<string>())
                .Select(version => (version ?? string.Empty).Trim())
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .ToList();
            if (revitVersions.Count > 0 && !revitVersions.Contains(CurrentRevitVersion, StringComparer.OrdinalIgnoreCase))
            {
                reason = "Module does not declare compatibility with Revit 2020.";
                return false;
            }

            return true;
        }

        private static ModuleDescriptor ToDescriptor(string baseDirectory, ModuleConfiguration module)
        {
            return new ModuleDescriptor
            {
                Id = module.Id,
                Name = DisplayNameResolver.Resolve(module.DisplayName, module.Name, string.Empty, module.Id),
                Description = module.Description,
                State = module.Enabled ? (module.Visible ? ModuleState.Enabled : ModuleState.Hidden) : ModuleState.Disabled,
                Order = module.Order,
                Tags = new List<string>(module.Tags ?? new List<string>()),
                Features = (module.Features ?? new List<FeatureConfiguration>()).Select(feature => new PlugHub.Contracts.Features.FeatureDescriptor
                {
                    Id = feature.Id,
                    ModuleId = module.Id,
                    Name = DisplayNameResolver.Resolve(feature.DisplayName, feature.Name, string.Empty, feature.Id),
                    Description = feature.Description,
                    Category = FirstNonEmpty(feature.Category, module.Category),
                    Group = feature.Group,
                    Tags = MergeTags(module.Tags, feature.Tags),
                    Order = feature.Order,
                    DefaultState = ParseFeatureState(feature.DefaultState),
                    CommandKey = feature.CommandKey,
                    CommandAssembly = ResolveFeatureCommandAssembly(baseDirectory, module, feature.CommandAssembly),
                    CommandType = feature.CommandType,
                    ButtonSize = string.IsNullOrWhiteSpace(feature.ButtonSize) ? "large" : feature.ButtonSize,
                    IconPath = ResolveFeatureAssetPath(baseDirectory, module, feature.IconPath)
                }).ToList()
            };
        }

        private static string ResolveFeatureCommandAssembly(string baseDirectory, ModuleConfiguration module, string commandAssembly)
        {
            var configuredAssembly = string.IsNullOrWhiteSpace(commandAssembly) ? module.Assembly : commandAssembly;
            if (string.IsNullOrWhiteSpace(configuredAssembly)) return string.Empty;
            if (Path.IsPathRooted(configuredAssembly)) return configuredAssembly;

            var sourceDirectory = string.IsNullOrWhiteSpace(module.ResolvedBaseDirectory) ? baseDirectory : module.ResolvedBaseDirectory;
            return Path.Combine(sourceDirectory, configuredAssembly);
        }

        private static string ResolveFeatureAssetPath(string baseDirectory, ModuleConfiguration module, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return string.Empty;
            if (Path.IsPathRooted(assetPath)) return assetPath;

            var sourceDirectory = string.IsNullOrWhiteSpace(module.ResolvedBaseDirectory) ? baseDirectory : module.ResolvedBaseDirectory;
            return Path.Combine(sourceDirectory, assetPath);
        }

        private static DiagnosticMessage BuildDiagnostic(string moduleId, DiagnosticSeverity severity, string code, string message)
        {
            return new DiagnosticMessage
            {
                ModuleId = moduleId ?? string.Empty,
                Severity = severity,
                Code = code ?? string.Empty,
                Message = message ?? string.Empty
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

    public sealed class ModuleDiscoveryResult
    {
        public ModuleDiscoveryResult(IReadOnlyList<ModuleDescriptor> modules, IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            Modules = modules ?? new List<ModuleDescriptor>();
            Diagnostics = diagnostics ?? new List<DiagnosticMessage>();
        }

        public IReadOnlyList<ModuleDescriptor> Modules { get; }
        public IReadOnlyList<DiagnosticMessage> Diagnostics { get; }
    }
}
