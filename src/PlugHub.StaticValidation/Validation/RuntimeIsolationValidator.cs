using System;
using System.IO;
using System.Linq;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class RuntimeIsolationValidator
    {
        private readonly ValidationSource _source;

        public RuntimeIsolationValidator(ValidationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public void Validate()
        {
            ValidateRevitRibbonAdapter();
            ValidateFeatureButtonTooltipBehavior();
            ValidateRuntimeRoutingSpecification();
            ValidateRevit2025AlcReadinessSpecification();
            ValidateManifestAuthoritativeDiscoverySpecification();
            ValidateRuntimeConfigurationLoader();
            ValidateRuntimeLoadsPackagesWhenConfigFilesAreMissing();
            ValidateRuntimeToleratesStaleConfigurationFiles();
            ValidateFrameworkRuntimeLoadIsolation();
            ValidateExternalModuleCommandResolution();
        }

        private void ValidateRevitRibbonAdapter()
        {
            var adapterText = _source.ReadAllCSharp("src/PlugHub.Revit2020");
            if (!adapterText.Contains("FeatureCommandDispatcher") || !adapterText.Contains("FeatureSlotRegistry"))
            {
                ValidateRuntimeRoutingSpecification();
            }

            foreach (var token in new[] { "CreateRibbonTab", "CreateRibbonPanel", "PushButtonData", "PulldownButtonData", "SplitButtonData", "FeatureRibbonBuilder", "FrameworkFeatureCommand", "FeatureCommandDispatcher", "FeatureSlotRegistry" })
            {
                Require(adapterText.Contains(token), "missing Revit adapter token: " + token);
            }
        }

        private void ValidateFeatureButtonTooltipBehavior()
        {
            var ribbonBuilder = _source.ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");

            Require(ribbonBuilder.Contains("private static string BuildToolTip(FeatureViewModel feature)"), "feature ribbon builder must centralize feature button tooltip text.");
            Require(ribbonBuilder.Contains("return string.IsNullOrWhiteSpace(feature.Description)") && ribbonBuilder.Contains("feature.Description.Trim()"), "feature button tooltip must only display features[].description.");
            foreach (var metadataToken in new[] { "\"Module: \"", "\"Feature: \"", "\"Category: \"", "\"Command: \"", "\"Command type: \"", "\"Button size: \"" })
            {
                Require(!ribbonBuilder.Contains(metadataToken), "feature button tooltip must not include metadata token " + metadataToken + ".");
            }
        }

        private void ValidateRuntimeRoutingSpecification()
        {
            var ribbonBuilder = _source.ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var featureCommand = _source.ReadText("src/PlugHub.Revit2020/FrameworkFeatureCommand.cs");
            var featureDispatcher = _source.ReadText("src/PlugHub.Revit2020/FeatureCommandDispatcher.cs");
            var featureSlotRegistry = _source.ReadText("src/PlugHub.Revit2020/FeatureSlotRegistry.cs");
            var featureExecutionGate = _source.ReadText("src/PlugHub.Framework/Runtime/FeatureExecutionGate.cs");
            var slotCommandText = _source.ReadText("src/PlugHub.Revit2020/FrameworkFeatureCommandSlots.cs");
            var normalizedSlotCommandText = slotCommandText.Replace("\r\n", "\n").Replace("\r", "\n");
            var revitText = _source.ReadAllCSharp("src/PlugHub.Revit2020");

            Require(revitText.Contains("class FeatureCommandDispatcher"), "runtime routing must use FeatureCommandDispatcher.");
            Require(revitText.Contains("interface ICommandAssemblyLoader"), "runtime routing must isolate command assembly loading behind ICommandAssemblyLoader.");
            Require(revitText.Contains("class Net48ShadowCopyCommandAssemblyLoader"), "runtime routing must use the net48 shadow-copy loader.");
            Require(revitText.Contains("class FeatureSlotRegistry"), "runtime routing must use a feature slot registry.");
            Require(revitText.Contains("class FrameworkFeatureCommandSlot001"), "runtime routing must define the first feature command slot.");
            Require(revitText.Contains("class FrameworkFeatureCommandSlot128"), "runtime routing must define the last feature command slot.");
            Require(revitText.Contains("FrameworkFeatureCommandSlots.CommandTypeFor"), "runtime routing must resolve slot command types through FrameworkFeatureCommandSlots.");
            Require(revitText.Contains("PH-FEATURE-SLOT-LIMIT"), "runtime routing must diagnose visible features that exceed available slots.");

            for (var slot = 1; slot <= 128; slot++)
            {
                var slotClass = "FrameworkFeatureCommandSlot" + slot.ToString("D3");
                var concreteDeclaration = "[Transaction(TransactionMode.Manual)]\n    public sealed class " + slotClass;
                Require(normalizedSlotCommandText.Contains(concreteDeclaration), "feature command slot must declare TransactionMode.Manual on concrete command type: " + slotClass);
            }

            Require(!ribbonBuilder.Contains("new CommandTarget(assemblyPath, feature.CommandType)"), "Revit feature buttons must use framework slots instead of external command assemblies.");
            Require(ribbonBuilder.Contains("FeatureSlotRegistry.Replace"), "Ribbon build must atomically replace feature slot mappings.");
            Require(ribbonBuilder.Contains("new RibbonLayoutComposer().Compose"), "Ribbon builder must consume RibbonLayoutComposer.");
            Require(ribbonBuilder.Contains("AddPulldownButton"), "Ribbon builder must render pulldown buttons.");
            Require(ribbonBuilder.Contains("AddSplitButton"), "Ribbon builder must render split buttons.");
            Require(ribbonBuilder.Contains("AddStackItemData"), "Ribbon builder must render stacked layout item data.");
            Require(ribbonBuilder.Contains("RibbonItemData"), "Ribbon builder must pass generic RibbonItemData into AddStackedItems.");
            Require(ribbonBuilder.Contains("layout.ClickableFeatures"), "Ribbon slot assignment must use clickable features from the layout tree.");
            Require(ribbonBuilder.Contains("FlushSmallPushButtons") && ribbonBuilder.Contains("IsSmall(item.Size)") && ribbonBuilder.Contains("AddStackItemData(panel, smallPushButtons)"), "Ribbon builder must preserve legacy small push button stacking.");
            Require(!featureCommand.Contains("Assembly.LoadFrom"), "FrameworkFeatureCommand must delegate business command loading to ICommandAssemblyLoader.");
            Require(featureExecutionGate.Contains("CanExecuteFeatureId") && featureExecutionGate.Contains("matchCommandKey"), "FeatureExecutionGate must expose an id-only execution path for slot routing.");
            Require(featureDispatcher.Contains("CanExecuteFeatureId(featureId)"), "FeatureCommandDispatcher must validate slot-routed feature ids without matching command keys.");
            Require(featureDispatcher.Contains("CanExecute(featureKey)"), "FeatureCommandDispatcher.ExecuteFeature must preserve legacy journal routing by feature id or command key.");
            Require(featureDispatcher.Contains("catch (Exception ex)") && featureDispatcher.Contains("PH-COMMAND-EXECUTE"), "FeatureCommandDispatcher must catch business command Execute exceptions.");
            Require(featureDispatcher.Contains("try\r\n                {\r\n                    ShowFailure(\"PlugHub 功能执行失败\", message, \"PH-COMMAND-EXECUTE\"") || featureDispatcher.Contains("try\n                {\n                    ShowFailure(\"PlugHub 功能执行失败\", message, \"PH-COMMAND-EXECUTE\""), "FeatureCommandDispatcher must isolate failure UI exceptions after business Execute failures.");
            var logger = _source.ReadText("src/PlugHub.Framework/Diagnostics/PlugHubLogger.cs");
            var exporter = _source.ReadText("src/PlugHub.Framework/Diagnostics/PlugHubLogExporter.cs");
            Require(logger.Contains("plughub-") && logger.Contains(".log"), "PlugHub logger must write daily log files.");
            Require(logger.Contains("public void Write(string baseDirectory, PlugHubLogEntry entry)") || logger.Contains("public void Write(string baseDirectory,\r\n            PlugHubLogEntry entry)") || logger.Contains("public void Write(string baseDirectory,\n            PlugHubLogEntry entry)"), "PlugHub logger must expose public Write(string baseDirectory, PlugHubLogEntry entry).");
            Require(logger.Contains("SensitiveTextRedactor.Redact(entry.Message)") && logger.Contains("SensitiveTextRedactor.Redact(entry.Exception)"), "PlugHub logger must redact message and exception fields.");
            Require(logger.Contains(".Replace(\"\\t\"") && logger.Contains(".Replace(\"\\n\""), "PlugHub logger must normalize tab and newline characters.");
            Require(logger.Contains("catch"), "PlugHub logger writes must catch failures.");
            Require(logger.Contains("public static string LogsDirectory") && logger.Contains("Environment.SpecialFolder.LocalApplicationData"), "PlugHub logger must expose the effective logs folder and fall back to local app data if the install directory is not writable.");
            Require(logger.Contains("RetentionDays = 3") && logger.Contains("DeleteExpiredLogs") && logger.Contains("AddDays(-(RetentionDays - 1))"), "PlugHub logger must retain only the current day and previous two days of daily log files.");
            Require(featureDispatcher.Contains("PH-COMMAND-START") && featureDispatcher.Contains("PH-COMMAND-RESULT") && featureDispatcher.Contains("PH-FEATURE-GATE") && featureDispatcher.Contains("PH-COMMAND-ASSEMBLY"), "FeatureCommandDispatcher must log command starts, results, disabled gates, and assembly failures.");
            Require(exporter.Contains("IsPathInside") && exporter.Contains("StartsWith") && (exporter.Contains("string.Equals(fullTargetPath, fullLogsPath") || exporter.Contains("fullTargetPath == fullLogsDirectory")), "PlugHub log exporter must reject targets inside or equal to the logs directory.");
            Require(!featureSlotRegistry.Contains("new Dictionary<int, string>(slotToFeatureId ??"), "FeatureSlotRegistry must not construct Dictionary directly from an IReadOnlyDictionary fallback under net48.");
            Require(featureSlotRegistry.Contains(".ToDictionary(pair => pair.Key, pair => pair.Value)"), "FeatureSlotRegistry.Replace must clone slot mappings through an enumerable-compatible Dictionary shape.");

            ValidateNet48ShadowCopyCommandLoader(featureDispatcher);
        }

        private void ValidateNet48ShadowCopyCommandLoader(string featureDispatcher)
        {
            const string commandAssemblyLoaderPath = "src/PlugHub.Revit2020/CommandAssemblyLoader.cs";
            Require(File.Exists(_source.FullPath(commandAssemblyLoaderPath)), "runtime routing must keep the net48 command loading strategy in CommandAssemblyLoader.cs.");

            var loader = _source.ReadText(commandAssemblyLoaderPath);
            Require(loader.Contains("class Net48ShadowCopyCommandAssemblyLoader"), "net48 command loader must use a shadow-copy implementation.");
            Require(loader.Contains("runtime-cache"), "shadow-copy loader must copy business assemblies under runtime-cache.");
            Require(loader.Contains("SHA256.Create"), "shadow-copy loader must compute a content hash for cache directories.");
            Require(loader.Contains("CopyPackagePayload"), "shadow-copy loader must copy package payload before loading commands.");
            Require(loader.Contains("IsFlatPayloadFile"), "shadow-copy loader must avoid copying every installed package for flat DLL module manifests.");
            Require(loader.Contains("ApplyPendingCleanup") && loader.Contains("pending-cleanup.txt"), "shadow-copy loader must retry cleanup of old locked cache directories.");
            Require(loader.Contains("runtimeCacheRoot") && loader.Contains("IsUnderDirectory(runtimeCacheRoot"), "shadow-copy pending cleanup must only delete directories under runtime-cache.");
            Require(loader.Contains("segment.All(ch => ch == '.')") && loader.Contains("? \"package\""), "shadow-copy loader cache path segments must reject all-dot package ids.");
            Require(loader.Contains("Assembly.LoadFrom(cachedAssemblyPath)"), "net48 command loader must load the cached business assembly copy.");
            Require(!loader.Contains("Assembly.LoadFrom(assemblyPath)"), "net48 command loader must not load directly from the installed package assembly path.");
            Require(featureDispatcher.Contains("new Net48ShadowCopyCommandAssemblyLoader()"), "FeatureCommandDispatcher must use the shadow-copy command loader.");
            Require(featureDispatcher.Contains("CommandAssemblyLoader.Create(assemblyPath, feature.CommandType, FrameworkRuntimeState.BaseDirectory)"), "FeatureCommandDispatcher must pass the runtime base directory to the shadow-copy loader.");
        }

        private void ValidateRevit2025AlcReadinessSpecification()
        {
            var revitText = _source.ReadAllCSharp("src/PlugHub.Revit2020");
            var alcRules = _source.ReadText("src/PlugHub.Contracts/Loading/AlcLoadRules.cs");
            var readme = _source.ReadText("README.md");

            Require(alcRules.Contains("class AlcLoadRules"), "ALC readiness must define shared assembly load rules.");
            Require(alcRules.Contains("MustUseDefaultContext"), "ALC readiness must expose a default-context decision point.");
            foreach (var sharedAssembly in new[] { "RevitAPI", "RevitAPIUI", "PlugHub.Contracts" })
            {
                Require(alcRules.Contains(sharedAssembly), "future Revit 2025+ ALC loaders must share assembly with the default context: " + sharedAssembly);
            }

            Require(!revitText.Contains("AssemblyLoadContext"), "Revit 2020 adapter must not use AssemblyLoadContext.");
            Require(!revitText.Contains("AssemblyDependencyResolver"), "Revit 2020 adapter must not use AssemblyDependencyResolver.");
            Require(readme.Contains("Revit 2020") && !readme.Contains("Revit 2025+ ALC") && !readme.Contains("AlcLoadRules"), "root README must stay focused on Revit 2020 user guidance.");
        }

        private void ValidateManifestAuthoritativeDiscoverySpecification()
        {
            var discovery = _source.ReadText("src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs");
            Require(!discovery.Contains("Assembly.LoadFrom"), "manifest-authoritative discovery must not load module assemblies at startup.");
            Require(!discovery.Contains("Activator.CreateInstance"), "manifest-authoritative discovery must not instantiate module types at startup.");
            Require(!discovery.Contains(".Describe("), "manifest-authoritative discovery must not call IPlugHubModule.Describe() at startup.");
            Require(!discovery.Contains("GetType(module.Type"), "manifest-authoritative discovery must not reflect configured module types at startup.");
            Require(discovery.Contains("ToDescriptor(baseDirectory, module)") && discovery.Contains("descriptors.Add(descriptor)"), "manifest-authoritative discovery must build module descriptors directly from packages manifests.");

            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var baseDirectory = tempRoot;
                var configDirectory = Path.Combine(baseDirectory, "config");
                var packageDirectory = Path.Combine(baseDirectory, "packages", "manifest-authority");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(packageDirectory);

                WriteRuntimeIsolationConfiguration(configDirectory);
                File.WriteAllText(
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"modules\":[{\"id\":\"manifest-authority-module\",\"version\":\"V1.0.0\",\"assembly\":\"MissingBusiness.dll\",\"type\":\"Missing.Plugin.Module\",\"features\":[{\"id\":\"manifest-authority-feature\",\"displayName\":\"Manifest Feature\"}]}]}");

                var runtime = new PlugHub.Framework.Runtime.FrameworkRuntime();
                var snapshot = runtime.Load(baseDirectory, configDirectory);
                Require(snapshot.Features.Any(feature => feature.Id == "manifest-authority-feature"), "packages manifest features must load even when the optional module assembly/type cannot be validated at startup.");
                Require(!snapshot.Diagnostics.Any(message => message.Code == "RT-MODULE-MANIFEST" || message.Code == "RT-MODULE-ASSEMBLY" || message.Code == "RT-MODULE-TYPE" || message.Code == "RT-MODULE-LOAD"), "manifest-authoritative discovery must not warn or fail only because optional module assembly/type validation is unavailable at startup.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private void ValidateRuntimeConfigurationLoader()
        {
            var frameworkText = _source.ReadAllCSharp("src/PlugHub.Framework");
            foreach (var token in new[] { "class FrameworkConfigurationLoader", "LoadFromDirectory", "LoadRuntime", "ToFeatureDescriptors", "class FrameworkRuntime", "class ModuleDiscoveryService" })
            {
                Require(frameworkText.Contains(token), "missing runtime configuration loader token: " + token);
            }
        }

        private void ValidateFrameworkRuntimeLoadIsolation()
        {
            var runtimeText = _source.ReadText("src/PlugHub.Framework/Runtime/FrameworkRuntime.cs");
            Require(!runtimeText.Contains("private readonly FeatureRegistry _featureRegistry"), "FrameworkRuntime.Load must not reuse a FeatureRegistry across loads.");
            Require(!runtimeText.Contains("private readonly DiagnosticsSink _diagnostics"), "FrameworkRuntime.Load must not reuse a DiagnosticsSink across loads.");
            Require(runtimeText.Contains("var diagnostics = new DiagnosticsSink()") && runtimeText.Contains("var featureRegistry = new FeatureRegistry()"), "FrameworkRuntime.Load must create fresh load-scoped diagnostics and feature registry instances.");

            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var baseDirectory = tempRoot;
                var configDirectory = Path.Combine(baseDirectory, "config");
                var packageDirectory = Path.Combine(baseDirectory, "packages", "runtime-isolation");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(packageDirectory);

                WriteRuntimeIsolationConfiguration(configDirectory);
                WriteRuntimeIsolationManifest(packageDirectory, "first-feature");

                var runtime = new PlugHub.Framework.Runtime.FrameworkRuntime();
                var firstSnapshot = runtime.Load(baseDirectory, configDirectory);
                Require(firstSnapshot.Features.Count == 1 && firstSnapshot.Features[0].Id == "first-feature", "runtime isolation setup must load the first manifest feature.");

                WriteRuntimeIsolationManifest(packageDirectory, "second-feature");
                var secondSnapshot = runtime.Load(baseDirectory, configDirectory);
                Require(secondSnapshot.Features.Count == 1 && secondSnapshot.Features[0].Id == "second-feature", "FrameworkRuntime.Load must not keep stale features when the same runtime instance is loaded again.");
                Require(!secondSnapshot.Diagnostics.Any(message => message.Code == "RT-MODULE-DUPLICATE"), "FrameworkRuntime.Load must not keep stale module ids when the same runtime instance is loaded again.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private void ValidateRuntimeLoadsPackagesWhenConfigFilesAreMissing()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var baseDirectory = tempRoot;
                var configDirectory = Path.Combine(baseDirectory, "config");
                var packageDirectory = Path.Combine(baseDirectory, "packages", "missing-config-module");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(packageDirectory);

                File.WriteAllText(
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"modules\":[{\"id\":\"missing-config-module\",\"version\":\"V1.0.0\",\"displayName\":\"Missing Config Module\",\"assembly\":\"MissingConfig.dll\",\"category\":\"view\",\"features\":[{\"id\":\"missing-config-module.run\",\"displayName\":\"Run Missing Config\"}]}]}");

                PlugHub.Framework.Runtime.FrameworkRuntimeSnapshot snapshot;
                try
                {
                    snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(baseDirectory, configDirectory);
                }
                catch (Exception ex)
                {
                    Require(false, "runtime must load installed packages when config JSON files are missing: " + ex.Message);
                    return;
                }

                Require(snapshot.Features.Any(feature => feature.Id == "missing-config-module.run"), "runtime must load package features when sources.json, views.json, and feature-combinations.json are missing.");
                Require(snapshot.Composition.Features.Any(feature => feature.FeatureId == "missing-config-module.run"), "runtime must compose package features with an internal default view when views.json is missing.");
                Require(snapshot.Configuration.Configuration.Modules.PackageDirectories.SequenceEqual(new[] { "packages" }), "missing sources.json must default runtime discovery to the packages directory.");
                Require(snapshot.Configuration.ActiveView.Ribbon != null && !string.IsNullOrWhiteSpace(snapshot.Configuration.ActiveView.Ribbon.TabName), "missing views.json must provide a usable default ribbon view.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private void ValidateRuntimeToleratesStaleConfigurationFiles()
        {
            ValidateRuntimeLoadsPackagesWhenExistingSourcesOmitPackageDirectories();
            ValidateRuntimeComposesPackagesWhenExistingViewFiltersAreStale();
            ValidateRuntimeLoadsPackagesRewrittenBySettingsSerializer();
        }

        private void ValidateRuntimeLoadsPackagesWhenExistingSourcesOmitPackageDirectories()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var baseDirectory = tempRoot;
                var configDirectory = Path.Combine(baseDirectory, "config");
                var packageDirectory = Path.Combine(baseDirectory, "packages", "stale-sources-module");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(packageDirectory);

                File.WriteAllText(
                    Path.Combine(configDirectory, "sources.json"),
                    "{\"schemaVersion\":\"1.0\",\"packageDirectories\":[],\"moduleSources\":[],\"repositories\":[],\"conflictPolicy\":{\"duplicateFeatureId\":\"fail-feature\",\"duplicateModuleId\":\"fail-module\",\"missingModuleType\":\"warn\"},\"modules\":[]}");
                File.WriteAllText(
                    Path.Combine(configDirectory, "views.json"),
                    "{\"schemaVersion\":\"1.0\",\"defaultView\":\"workspace\",\"views\":[{\"id\":\"workspace\",\"name\":\"PlugHub\",\"ribbon\":{\"tabName\":\"PlugHub\",\"fallbackPanelName\":\"External\"},\"groups\":[],\"sort\":[\"group.order\",\"feature.order\",\"feature.name\",\"feature.id\"]}]}");
                File.WriteAllText(
                    Path.Combine(configDirectory, "feature-combinations.json"),
                    "{\"schemaVersion\":\"1.0\",\"defaultPreset\":\"\",\"presets\":[]}");
                File.WriteAllText(
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"modules\":[{\"id\":\"stale-sources-module\",\"version\":\"V1.0.0\",\"displayName\":\"Stale Sources Module\",\"assembly\":\"StaleSources.dll\",\"category\":\"view\",\"features\":[{\"id\":\"stale-sources-module.run\",\"displayName\":\"Run Stale Sources\"}]}]}");

                var snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(baseDirectory, configDirectory);
                Require(snapshot.Features.Any(feature => feature.Id == "stale-sources-module.run"), "runtime must keep discovering installed packages when an existing old sources.json omits packageDirectories.");
                Require(snapshot.Configuration.Configuration.Modules.PackageDirectories.Contains("packages"), "existing old sources.json must be normalized to include the packages directory.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private void ValidateRuntimeComposesPackagesWhenExistingViewFiltersAreStale()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var baseDirectory = tempRoot;
                var configDirectory = Path.Combine(baseDirectory, "config");
                var packageDirectory = Path.Combine(baseDirectory, "packages", "stale-view-module");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(packageDirectory);

                File.WriteAllText(
                    Path.Combine(configDirectory, "sources.json"),
                    "{\"schemaVersion\":\"1.0\",\"packageDirectories\":[\"packages\"],\"moduleSources\":[],\"repositories\":[],\"conflictPolicy\":{\"duplicateFeatureId\":\"fail-feature\",\"duplicateModuleId\":\"fail-module\",\"missingModuleType\":\"warn\"},\"modules\":[]}");
                File.WriteAllText(
                    Path.Combine(configDirectory, "views.json"),
                    "{\"schemaVersion\":\"1.0\",\"defaultView\":\"workspace\",\"views\":[{\"id\":\"workspace\",\"name\":\"PlugHub\",\"includeCategories\":[\"legacy-only\"],\"ribbon\":{\"tabName\":\"PlugHub\",\"fallbackPanelName\":\"Framework\"},\"groups\":[{\"id\":\"legacy\",\"name\":\"Legacy\",\"includeCategories\":[\"legacy-only\"],\"order\":0}],\"sort\":[\"group.order\",\"feature.order\",\"feature.name\",\"feature.id\"]}]}");
                File.WriteAllText(
                    Path.Combine(configDirectory, "feature-combinations.json"),
                    "{\"schemaVersion\":\"1.0\",\"defaultPreset\":\"\",\"presets\":[]}");
                File.WriteAllText(
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"modules\":[{\"id\":\"stale-view-module\",\"version\":\"V1.0.0\",\"displayName\":\"Stale View Module\",\"assembly\":\"StaleView.dll\",\"category\":\"view\",\"features\":[{\"id\":\"stale-view-module.run\",\"displayName\":\"Run Stale View\"}]}]}");

                var snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(baseDirectory, configDirectory);
                Require(snapshot.Features.Any(feature => feature.Id == "stale-view-module.run"), "stale view filter setup must still discover the installed package feature.");
                Require(snapshot.Composition.Features.Any(feature => feature.FeatureId == "stale-view-module.run"), "runtime must compose installed package features when an existing old views.json include filter no longer matches any package feature.");
                Require(snapshot.Composition.Features.Any(feature => feature.FeatureId == "stale-view-module.run" && feature.GroupName == "Stale View Module"), "stale view filter fallback must use package module displayName for the panel name.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private void ValidateRuntimeLoadsPackagesRewrittenBySettingsSerializer()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var baseDirectory = tempRoot;
                var configDirectory = Path.Combine(baseDirectory, "config");
                var packageDirectory = Path.Combine(baseDirectory, "packages", "settings-rewritten-module");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(packageDirectory);

                File.WriteAllText(
                    Path.Combine(configDirectory, "sources.json"),
                    "{\"schemaVersion\":\"1.0\",\"packageDirectories\":[\"packages\"],\"moduleSources\":[],\"repositories\":[],\"conflictPolicy\":{\"duplicateFeatureId\":\"fail-feature\",\"duplicateModuleId\":\"fail-module\",\"missingModuleType\":\"warn\"},\"modules\":[]}");
                File.WriteAllText(
                    Path.Combine(configDirectory, "views.json"),
                    "{\"schemaVersion\":\"1.0\",\"defaultView\":\"workspace\",\"views\":[{\"id\":\"workspace\",\"name\":\"PlugHub\",\"ribbon\":{\"tabName\":\"PlugHub\",\"fallbackPanelName\":\"External\"},\"groups\":[],\"sort\":[\"group.order\",\"feature.order\",\"feature.name\",\"feature.id\"]}]}");
                File.WriteAllText(
                    Path.Combine(configDirectory, "feature-combinations.json"),
                    "{\"schemaVersion\":\"1.0\",\"defaultPreset\":\"\",\"presets\":[]}");
                File.WriteAllText(
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"SchemaVersion\":\"1.1\",\"RevitVersions\":[\"2020\"],\"FrameworkVersionRange\":\">=1.3.0\",\"PackageDirectories\":[],\"ModuleSources\":[],\"Repositories\":[],\"ConflictPolicy\":{\"DuplicateFeatureId\":\"fail-feature\",\"DuplicateModuleId\":\"fail-module\",\"MissingModuleType\":\"warn\"},\"Modules\":[{\"Id\":\"settings-rewritten-module\",\"Version\":\"V1.0.0\",\"Author\":\"GAOMENGGU\",\"Assembly\":\"SettingsRewritten.dll\",\"DisplayName\":\"Settings Rewritten Module\",\"Category\":\"view\",\"Enabled\":false,\"Visible\":false,\"Features\":[{\"Id\":\"settings-rewritten-module.run\",\"DisplayName\":\"Run Settings Rewritten\",\"Group\":\"view\",\"Order\":10,\"DefaultState\":\"Visible\",\"CommandAssembly\":\"Other.dll\",\"ButtonSize\":\"small\",\"CommandType\":\"Demo.SettingsCommand\",\"IconPath\":\"icons/settings.png\"}]}]}");

                var snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(baseDirectory, configDirectory);
                Require(snapshot.Features.Any(feature => feature.Id == "settings-rewritten-module.run"), "runtime must recover installed packages whose manifests were rewritten with PascalCase Enabled=false and Visible=false defaults by settings serialization.");
                Require(snapshot.Composition.Features.Any(feature => feature.FeatureId == "settings-rewritten-module.run"), "runtime must compose features from settings-rewritten installed package manifests.");
                Require(snapshot.Composition.Features.Any(feature => feature.FeatureId == "settings-rewritten-module.run" && feature.GroupName == "Settings Rewritten Module"), "runtime must ignore stale PascalCase feature Group values from settings-rewritten package manifests.");
                Require(snapshot.Features.Any(feature => feature.Id == "settings-rewritten-module.run" && feature.CommandAssembly.EndsWith("SettingsRewritten.dll", StringComparison.OrdinalIgnoreCase)), "runtime must ignore stale PascalCase feature CommandAssembly values and inherit the module assembly.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private void WriteRuntimeIsolationConfiguration(string configDirectory)
        {
            File.WriteAllText(
                Path.Combine(configDirectory, "sources.json"),
                "{\"schemaVersion\":\"1.0\",\"packageDirectories\":[\"packages\"],\"moduleSources\":[],\"repositories\":[],\"conflictPolicy\":{\"duplicateFeatureId\":\"fail-feature\",\"duplicateModuleId\":\"fail-module\",\"missingModuleType\":\"warn\"},\"modules\":[]}");
            File.WriteAllText(
                Path.Combine(configDirectory, "views.json"),
                "{\"schemaVersion\":\"1.0\",\"defaultView\":\"workspace\",\"views\":[{\"id\":\"workspace\",\"name\":\"PlugHub\",\"ribbon\":{\"tabName\":\"PlugHub\",\"fallbackPanelName\":\"External\"},\"groups\":[],\"sort\":[\"group.order\",\"feature.order\",\"feature.name\",\"feature.id\"]}]}");
            File.WriteAllText(
                Path.Combine(configDirectory, "feature-combinations.json"),
                "{\"schemaVersion\":\"1.0\",\"defaultPreset\":\"\",\"presets\":[]}");
        }

        private void WriteRuntimeIsolationManifest(string packageDirectory, string featureId)
        {
            File.WriteAllText(
                Path.Combine(packageDirectory, "packages.json"),
                "{\"schemaVersion\":\"1.1\",\"modules\":[{\"id\":\"runtime-isolation-module\",\"version\":\"V1.0.0\",\"features\":[{\"id\":\"" + featureId + "\",\"displayName\":\"" + featureId + "\"}]}]}");
        }

        private void ValidateExternalModuleCommandResolution()
        {
            var discovery = _source.ReadText("src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs");
            Require(discovery.Contains("ResolveFeatureCommandAssembly"), "external module feature command assemblies must be resolved by ModuleDiscoveryService.");
            Require(discovery.Contains("ResolveFeatureAssetPath"), "external module feature icon paths must be resolved by ModuleDiscoveryService.");
            Require(discovery.Contains("Path.IsPathRooted(configuredAssembly)"), "absolute feature command assemblies must remain supported.");
            Require(discovery.Contains("module.ResolvedBaseDirectory"), "relative feature command assemblies must use the module source directory.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
