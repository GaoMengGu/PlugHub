using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlugHub.Contracts.Features;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Discovery
{
    public sealed class ModuleDiscoveryService
    {
        public ModuleDiscoveryResult Discover(string baseDirectory, ModulesConfiguration modulesConfiguration)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (modulesConfiguration == null) throw new ArgumentNullException(nameof(modulesConfiguration));

            var diagnostics = new List<DiagnosticMessage>();
            var descriptors = new List<ModuleDescriptor>();

            foreach (var module in (modulesConfiguration.Modules ?? new List<ModuleConfiguration>()).OrderBy(module => module.Order).ThenBy(module => module.Id, StringComparer.OrdinalIgnoreCase))
            {
                var descriptor = ToDescriptor(baseDirectory, module);
                descriptors.Add(descriptor);

                if (!module.Enabled || !module.Visible)
                {
                    diagnostics.Add(BuildDiagnostic(module.Id, DiagnosticSeverity.Info, "RT-MODULE-SKIP", "Module is disabled or hidden and was not discovered."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(module.Assembly) || string.IsNullOrWhiteSpace(module.Type))
                {
                    diagnostics.Add(BuildDiagnostic(module.Id, DiagnosticSeverity.Warning, "RT-MODULE-MANIFEST", "Module manifest is missing assembly or type."));
                    continue;
                }

                var assemblyPath = ResolveAssemblyPath(baseDirectory, module);
                if (!File.Exists(assemblyPath))
                {
                    diagnostics.Add(BuildDiagnostic(module.Id, SeverityFor(modulesConfiguration.ConflictPolicy.MissingModuleType), "RT-MODULE-ASSEMBLY", "Module assembly was not found: " + assemblyPath));
                    continue;
                }

                try
                {
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    var type = assembly.GetType(module.Type, throwOnError: false, ignoreCase: false);
                    if (type == null)
                    {
                        diagnostics.Add(BuildDiagnostic(module.Id, SeverityFor(modulesConfiguration.ConflictPolicy.MissingModuleType), "RT-MODULE-TYPE", "Module type was not found: " + module.Type));
                        continue;
                    }

                    if (!typeof(IPlugHubModule).IsAssignableFrom(type))
                    {
                        diagnostics.Add(BuildDiagnostic(module.Id, DiagnosticSeverity.Warning, "RT-MODULE-CONTRACT", "Configured type does not implement IPlugHubModule."));
                        continue;
                    }

                    var instance = (IPlugHubModule)Activator.CreateInstance(type)!;
                    var runtimeDescriptor = instance.Describe();
                    ValidateDescriptor(module, runtimeDescriptor, diagnostics);
                }
                catch (Exception ex)
                {
                    diagnostics.Add(BuildDiagnostic(module.Id, SeverityFor(modulesConfiguration.ConflictPolicy.MissingModuleType), "RT-MODULE-LOAD", ex.Message));
                }
            }

            return new ModuleDiscoveryResult(descriptors, diagnostics);
        }

        private static void ValidateDescriptor(ModuleConfiguration manifest, ModuleDescriptor runtimeDescriptor, ICollection<DiagnosticMessage> diagnostics)
        {
            if (runtimeDescriptor == null)
            {
                diagnostics.Add(BuildDiagnostic(manifest.Id, DiagnosticSeverity.Warning, "RT-MODULE-DESCRIPTOR", "Runtime module returned an empty descriptor."));
                return;
            }

            if (!string.Equals(manifest.Id, runtimeDescriptor.Id, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(BuildDiagnostic(manifest.Id, DiagnosticSeverity.Warning, "RT-MODULE-ID", "Runtime module id does not match manifest id: " + runtimeDescriptor.Id));
            }

            if (string.IsNullOrWhiteSpace(manifest.Name) && !string.IsNullOrWhiteSpace(runtimeDescriptor.Name))
            {
                diagnostics.Add(BuildDiagnostic(manifest.Id, DiagnosticSeverity.Info, "RT-MODULE-NAME", "Runtime descriptor supplies the module name."));
            }
        }

        private static string ResolveAssemblyPath(string baseDirectory, ModuleConfiguration module)
        {
            var assemblyName = module.Assembly;
            if (Path.IsPathRooted(assemblyName)) return assemblyName;

            var sourceDirectory = string.IsNullOrWhiteSpace(module.ResolvedBaseDirectory) ? baseDirectory : module.ResolvedBaseDirectory;
            return Path.Combine(sourceDirectory, assemblyName);
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
                    Category = feature.Category,
                    Group = feature.Group,
                    Tags = new List<string>(feature.Tags ?? new List<string>()),
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

        private static DiagnosticSeverity SeverityFor(string policy)
        {
            return string.Equals(policy, "fail-module", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticSeverity.Error
                : DiagnosticSeverity.Warning;
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
