using System;
using System.Collections.Generic;
using System.IO;
using PlugHub.Contracts.Features;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Composition;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;
using PlugHub.Framework.Discovery;
using PlugHub.Framework.Packages;
using PlugHub.Framework.Registry;
using PlugHub.Framework.Sources;

namespace PlugHub.Framework.Runtime
{
    public sealed class FrameworkRuntime
    {
        private readonly FrameworkConfigurationLoader _configurationLoader = new FrameworkConfigurationLoader();
        private readonly ModuleDiscoveryService _moduleDiscovery = new ModuleDiscoveryService();
        private readonly ModuleSourceResolver _moduleSourceResolver = new ModuleSourceResolver();
        private readonly PackageRepositoryService _packageRepositoryService = new PackageRepositoryService();
        private readonly FeatureViewComposer _composer = new FeatureViewComposer();

        public FrameworkRuntimeSnapshot Load(string configDirectory)
        {
            var baseDirectory = Directory.GetParent(configDirectory)?.FullName ?? configDirectory;
            return Load(baseDirectory, configDirectory);
        }

        public FrameworkRuntimeSnapshot Load(string configDirectory, bool applyPendingPackageOperations)
        {
            var baseDirectory = Directory.GetParent(configDirectory)?.FullName ?? configDirectory;
            return Load(baseDirectory, configDirectory, applyPendingPackageOperations);
        }

        public FrameworkRuntimeSnapshot Load(string baseDirectory, string configDirectory)
        {
            return Load(baseDirectory, configDirectory, true);
        }

        public FrameworkRuntimeSnapshot Load(string baseDirectory, string configDirectory, bool applyPendingPackageOperations)
        {
            var diagnostics = new DiagnosticsSink();
            var featureRegistry = new FeatureRegistry();

            if (applyPendingPackageOperations)
            {
                diagnostics.AddRange(_packageRepositoryService.ApplyPendingOperations(baseDirectory));
            }

            var configuration = _configurationLoader.Load(configDirectory);
            var sourceResult = _moduleSourceResolver.Resolve(baseDirectory, configuration.Modules);
            configuration.Modules = sourceResult.Modules;
            diagnostics.AddRange(sourceResult.Diagnostics);

            var view = _configurationLoader.GetDefaultView(configuration);
            var preset = _configurationLoader.GetPresetForView(configuration, view);
            var effectiveModules = _configurationLoader.ApplyPreset(configuration.Modules, preset);
            var runtimeConfiguration = new FrameworkRuntimeConfiguration(configuration, view, preset, effectiveModules);
            var discoveryResult = _moduleDiscovery.Discover(baseDirectory, runtimeConfiguration.EffectiveModules);
            diagnostics.AddRange(discoveryResult.Diagnostics);

            RegisterModules(runtimeConfiguration, discoveryResult.Modules, featureRegistry);

            var allFeatures = featureRegistry.All();
            var composition = _composer.ComposeDetailed(allFeatures, runtimeConfiguration.ActiveView);

            diagnostics.AddRange(featureRegistry.Diagnostics);
            if (composition.SkippedFeatures.Count > 0)
            {
                diagnostics.Warning(string.Empty, "RT-COMPOSE-SKIPPED", $"Skipped {composition.SkippedFeatures.Count} features while composing the active view.");
            }

            var snapshot = new FrameworkRuntimeSnapshot(
                runtimeConfiguration,
                allFeatures,
                composition,
                diagnostics.Messages);

            FrameworkRuntimeState.SetCurrent(baseDirectory, configDirectory, snapshot);
            return snapshot;
        }

        private void RegisterModules(
            FrameworkRuntimeConfiguration runtimeConfiguration,
            IReadOnlyList<ModuleDescriptor> modules,
            FeatureRegistry featureRegistry)
        {
            foreach (var module in modules)
            {
                featureRegistry.Register(module, runtimeConfiguration.Configuration.Modules.ConflictPolicy);
            }
        }
    }

    public sealed class FrameworkRuntimeSnapshot
    {
        public FrameworkRuntimeSnapshot(
            FrameworkRuntimeConfiguration configuration,
            IReadOnlyList<FeatureDescriptor> features,
            FeatureViewCompositionResult composition,
            IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Features = features ?? new List<FeatureDescriptor>();
            Composition = composition ?? throw new ArgumentNullException(nameof(composition));
            Diagnostics = diagnostics ?? new List<DiagnosticMessage>();
        }

        public FrameworkRuntimeConfiguration Configuration { get; }
        public IReadOnlyList<FeatureDescriptor> Features { get; }
        public FeatureViewCompositionResult Composition { get; }
        public IReadOnlyList<DiagnosticMessage> Diagnostics { get; }
    }
}
