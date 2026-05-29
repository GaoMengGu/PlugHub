using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation
{
    internal static class Program
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static readonly string Root = FindRepositoryRoot();

        private static int Main()
        {
            try
            {
                ValidateRequiredFiles();
                ValidateDocumentationStructure();
                ValidateLayering();
                ValidateConfiguration();
                ValidateViewCompositionExamples();
                ValidateComposerShape();
                ValidateCoreContracts();
                ValidateRevitRibbonAdapter();
                ValidateRuntimeConfigurationLoader();
                ValidateFrameworkRuntimeLoadIsolation();
                ValidateExternalModuleCommandResolution();
                ValidateFrameworkContainsNoBundledModules();
                ValidatePlugHubV2Specification();
                ValidateSettingsPaneV21Specification();
                ValidateSettingsRibbonCleanupSpecification();
                ValidateBuiltinOnlySpecification();
                ValidateSettingsCreationAndSortingSpecification();
                ValidateSettingsGroupFeatureEditingBehavior();
                ValidateDefaultIconSpecification();
                ValidatePackageSourceAndReleaseBehavior();
                ValidateRepositoryInstallFlowBehavior();
                ValidateRepositoryPackageGranularityAndInstallPayload();
                ValidateRuntimeLoadsSerializedInstalledPackageManifest();
                ValidateRepositoryInstallFailureDoesNotCreateOrRemovePackages();
                ValidateLockedPackageOperationBehavior();
                ValidateRevitApiReferenceStrategy();
                ValidateSigningGuidance();
                ValidateRevitDeploymentConfiguration();

                var modules = AllModules().ToList();
                var views = ReadObject("config/views.example.json");
                var presets = ReadObject("config/feature-combinations.example.json");
                var featureCount = modules.SelectMany(Features).Count();

                Console.WriteLine(
                    $"passed: modules={modules.Count}, features={featureCount}, views={Views(views).Count()}, presets={Presets(presets).Count()}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("validation failed: " + ex.Message);
                return 1;
            }
        }

        private static void ValidateRequiredFiles()
        {
            var required = new[]
            {
                "README.md",
                "AGENTS.md",
                ".github/workflows/release.yml",
                ".github/workflows/sync-gitee.yml",
                "PlugHub.sln",
                "PlugHub.slnx",
                "src/PlugHub.Contracts/PlugHub.Contracts.csproj",
                "src/PlugHub.Framework/PlugHub.Framework.csproj",
                "src/PlugHub.Revit2020/PlugHub.Revit2020.csproj",
                "src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj",
                "src/PlugHub.Contracts/Modules/IPlugHubModule.cs",
                "src/PlugHub.Framework/Composition/FeatureViewComposer.cs",
                "src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs",
                "src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs",
                "src/PlugHub.Framework/Runtime/FrameworkRuntime.cs",
                "src/PlugHub.Framework/Registry/FeatureRegistry.cs",
                "src/PlugHub.Revit2020/ExternalApplicationEntry.cs",
                "src/PlugHub.Revit2020/FeatureRibbonBuilder.cs",
                "src/PlugHub.Revit2020/FrameworkFeatureCommand.cs",
                "src/PlugHub.Revit2020/FrameworkRefreshCommand.cs",
                "src/PlugHub.Revit2020/FrameworkSettingsWindow.cs",
                "src/PlugHub.Revit2020/FrameworkStatusWindow.cs",
                "src/PlugHub.Revit2020/DefaultRibbonIconProvider.cs",
                "src/PlugHub.Revit2020/RevitWindowOwner.cs",
                "scripts/sign-revit2020.ps1",
                "config/sources.example.json",
                "config/views.example.json",
                "config/feature-combinations.example.json",
                "config/schemas/sources.schema.json",
                "config/schemas/views.schema.json",
                "docs/README.md",
                "docs/project-overview.md",
                "docs/architecture.md",
                "docs/development.md",
                "docs/signing.md"
            };

            var missing = required.Where(path => !File.Exists(FullPath(path))).ToList();
            Require(!missing.Any(), "missing required files: " + string.Join(", ", missing));
            Require(!File.Exists(FullPath("config/modules.example.json")), "framework source config must be named sources.example.json, not modules.example.json.");
            Require(!File.Exists(FullPath("config/plugin-sources.example.json")), "framework source config must be named sources.example.json, not plugin-sources.example.json.");
            Require(!Directory.Exists(FullPath("modules")), "source workspace must not keep a modules drop-in directory; build output creates package drop-ins.");
            if (Directory.Exists(FullPath("tests")))
            {
                var testProjects = Directory.GetFiles(FullPath("tests"), "*.csproj", SearchOption.AllDirectories);
                Require(testProjects.Length > 0, "tests directory must contain real test projects; move validation notes into docs/development.md instead of keeping a placeholder tests folder.");
            }
        }

        private static void ValidateDocumentationStructure()
        {
            foreach (var obsolete in new[]
            {
                "docs/agent-handbook.md",
                "docs/frontend-ux.md",
                "docs/module-contract.md",
                "docs/requirements.md",
                "docs/review.md",
                "docs/verification.md"
            })
            {
                Require(!File.Exists(FullPath(obsolete)), "obsolete documentation should be consolidated or removed: " + obsolete);
            }

            var index = ReadText("docs/README.md");
            foreach (var requiredLink in new[] { "project-overview.md", "architecture.md", "development.md", "signing.md" })
            {
                Require(index.Contains(requiredLink), "docs index must link to " + requiredLink);
            }

            Require(!ReadText("README.md").Contains("D:\\AI\\code\\PlugHub_Modules"), "root README must not expose local external module paths.");
        }

        private static void ValidateLayering()
        {
            var forbidden = new List<string>();
            foreach (var directory in new[] { "src/PlugHub.Contracts", "src/PlugHub.Framework" })
            {
                foreach (var file in Directory.GetFiles(FullPath(directory), "*.cs", SearchOption.AllDirectories))
                {
                    if (File.ReadAllText(file).Contains("Autodesk.Revit"))
                    {
                        forbidden.Add(RelativePath(file));
                    }
                }
            }

            Require(!forbidden.Any(), "Revit API reference leaked outside adapter: " + string.Join(", ", forbidden));
        }

        private static void ValidateConfiguration()
        {
            var modules = ReadObject("config/sources.example.json");
            var views = ReadObject("config/views.example.json");
            var presets = ReadObject("config/feature-combinations.example.json");

            Require(StringValue(modules, "schemaVersion") == "1.0", "source schemaVersion must be 1.0.");
            Require(StringValue(views, "defaultView") == "workspace", "default view must be workspace.");
            Require(Views(views).Count() == 1, "PlugHub must expose exactly one workspace view.");
            Require(!SequenceValue(modules, "packageDirectories").Contains(RemovedSamplesDirectory()), "sample modules must be removed from packageDirectories.");
            Require(SequenceValue(modules, "packageDirectories").SequenceEqual(new[] { "packages" }), "runtime package discovery must be limited to the packages folder.");
            Require(ArrayValue(modules, "moduleSources").Count == 0, "moduleSources must not configure startup repository loading.");
            Require(Repositories(modules).Count() >= 2, "repositories must include public and private examples.");
            Require(Repositories(modules).Any(repository => StringValue(repository, "visibility") == "public"), "repositories must include a public repository example.");
            Require(Repositories(modules).Any(repository => StringValue(repository, "visibility") == "private" && repository.ContainsKey("apiKey")), "repositories must include a private repository example with apiKey.");
            Require(Repositories(modules).Any(repository => StringValue(repository, "provider") == "gitee"), "repositories must include a Gitee repository example.");
            Require(Repositories(modules).Any(repository =>
                StringValue(repository, "provider") == "gitee"
                && StringValue(repository, "visibility") == "public"
                && StringValue(repository, "repository") == "https://gitee.com/GaoMengGu/PlugHub_Packages"
                && StringValue(repository, "enabled") == "True"), "default public repository must be the enabled Gitee PlugHub_Packages URL.");
            Require(StringValue(ObjectValue(modules, "conflictPolicy"), "duplicateFeatureId") == "fail-feature", "duplicate feature policy must be fail-feature.");

            var seenFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in AllModules())
            {
                foreach (var requiredKey in new[] { "id", "enabled", "visible", "features" })
                {
                    Require(module.ContainsKey(requiredKey), $"module is missing {requiredKey}.");
                }

                foreach (var feature in Features(module))
                {
                    var featureId = StringValue(feature, "id");
                    Require(seenFeatureIds.Add(featureId), "duplicate feature id: " + featureId);
                    Require(new[] { "Visible", "Disabled", "Hidden" }.Contains(StringValue(feature, "defaultState")), "invalid defaultState for " + featureId);
                    Require(feature.ContainsKey("displayName"), "feature is missing displayName: " + featureId);
                    Require(feature.ContainsKey("iconPath"), "feature is missing iconPath: " + featureId);
                }
            }

            var viewIds = new HashSet<string>(Views(views).Select(view => StringValue(view, "id")), StringComparer.OrdinalIgnoreCase);
            Require(viewIds.Contains(StringValue(views, "defaultView")), "defaultView must exist in views.");

            foreach (var view in Views(views))
            {
                Require(SequenceValue(view, "sort").SequenceEqual(new[] { "group.order", "feature.order", "feature.name", "feature.id" }), "view sort order is not stable: " + StringValue(view, "id"));
            }

            foreach (var preset in Presets(presets))
            {
                Require(viewIds.Contains(StringValue(preset, "viewId")), "preset references unknown view: " + StringValue(preset, "id"));
            }
        }

        private static void ValidateViewCompositionExamples()
        {
            var views = ReadObject("config/views.example.json");
            var features = AllModules().SelectMany(Features).ToList();
            var byView = Views(views).ToDictionary(view => StringValue(view, "id"), view => FeatureIdsForView(features, view), StringComparer.OrdinalIgnoreCase);

            Require(byView.ContainsKey("workspace"), "workspace view is required.");
            var workspace = byView["workspace"];
            Require(workspace.Count == 0, "framework workspace should not expose bundled features.");
        }

        private static void ValidateComposerShape()
        {
            var composer = ReadText("src/PlugHub.Framework/Composition/FeatureViewComposer.cs");
            Require(composer.Contains("ComposeDetailed"), "composer must expose a detailed composition result.");
            Require(composer.Contains("MatchesGroup"), "composer must use MatchesGroup.");
            Require(composer.Contains("FeatureViewCompositionResult"), "composer must return a composition result wrapper.");
            Require(composer.Contains("FeatureViewComparer"), "composer must use a deterministic comparer.");
            Require(composer.Contains("feature.category"), "composer must support category sorting.");
            Require(composer.Contains("SkippedFeatures"), "composer must capture skipped features.");
            Require(composer.Contains("CreateFallbackGroup"), "composer must show external module features even when workspace groups are empty.");
        }

        private static void ValidateCoreContracts()
        {
            var contractText = ReadAllCSharp("src/PlugHub.Contracts");
            foreach (var token in new[] { "interface IPlugHubModule", "class ModuleDescriptor", "class FeatureDescriptor", "CommandAssembly", "CommandType", "enum ModuleState", "enum FeatureState", "class DiagnosticMessage", "enum DiagnosticSeverity" })
            {
                Require(contractText.Contains(token), "missing contract token: " + token);
            }
        }

        private static void ValidateRevitRibbonAdapter()
        {
            var adapterText = ReadAllCSharp("src/PlugHub.Revit2020");
            foreach (var token in new[] { "CreateRibbonTab", "CreateRibbonPanel", "PushButtonData", "FeatureRibbonBuilder", "FrameworkFeatureCommand", "ResolveCommandTarget" })
            {
                Require(adapterText.Contains(token), "missing Revit adapter token: " + token);
            }
        }

        private static void ValidateRuntimeConfigurationLoader()
        {
            var frameworkText = ReadAllCSharp("src/PlugHub.Framework");
            foreach (var token in new[] { "class FrameworkConfigurationLoader", "LoadFromDirectory", "LoadRuntime", "ToFeatureDescriptors", "class FrameworkRuntime", "class ModuleDiscoveryService" })
            {
                Require(frameworkText.Contains(token), "missing runtime configuration loader token: " + token);
            }
        }

        private static void ValidateFrameworkRuntimeLoadIsolation()
        {
            var runtimeText = ReadText("src/PlugHub.Framework/Runtime/FrameworkRuntime.cs");
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

        private static void WriteRuntimeIsolationConfiguration(string configDirectory)
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

        private static void WriteRuntimeIsolationManifest(string packageDirectory, string featureId)
        {
            File.WriteAllText(
                Path.Combine(packageDirectory, "package.json"),
                "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"runtime-isolation-module\",\"enabled\":true,\"visible\":true,\"order\":10,\"features\":[{\"id\":\"" + featureId + "\",\"displayName\":\"" + featureId + "\",\"defaultState\":\"Visible\",\"order\":10}]}]}");
        }

        private static void ValidateExternalModuleCommandResolution()
        {
            var discovery = ReadText("src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs");
            Require(discovery.Contains("ResolveFeatureCommandAssembly"), "external module feature command assemblies must be resolved by ModuleDiscoveryService.");
            Require(discovery.Contains("ResolveFeatureAssetPath"), "external module feature icon paths must be resolved by ModuleDiscoveryService.");
            Require(discovery.Contains("Path.IsPathRooted(configuredAssembly)"), "absolute feature command assemblies must remain supported.");
            Require(discovery.Contains("module.ResolvedBaseDirectory"), "relative feature command assemblies must use the module source directory.");
        }

        private static void ValidateFrameworkContainsNoBundledModules()
        {
            var modules = ReadObject("config/sources.example.json");
            var repositoryText = ReadProductionCSharp() + "\n" + ReadText("PlugHub.sln") + "\n" + ReadText("PlugHub.slnx") + "\n" + ReadText("src/PlugHub.Revit2020/PlugHub.Revit2020.csproj") + "\n" + ReadText("config/sources.example.json");

            Require(Modules(modules).Count() == 0, "framework modules config must not contain bundled modules.");
            Require(AllModules().SelectMany(Features).Count() == 0, "framework runtime config must not contain bundled features.");
            Require(!Directory.Exists(FullPath("src/PlugHub.BuiltinModule")), "BuiltinModule must be separated from the framework repository.");
            foreach (var forbidden in new[] { "PlugHub.BuiltinModule", "plughub.builtin", "DuctPreferredJunctionSwitcherCommand", "BatchAddMaterialParameterCommand" })
            {
                Require(!repositoryText.Contains(forbidden), "framework must not reference separated module content: " + forbidden);
            }
        }

        private static void ValidatePlugHubV2Specification()
        {
            Require(File.Exists(FullPath("PlugHub.sln")), "PlugHub.sln is required.");
            Require(File.Exists(FullPath("src/PlugHub.Contracts/PlugHub.Contracts.csproj")), "PlugHub.Contracts project is required.");
            var legacySolution = "Revit" + "Tool.sln";
            Require(!File.Exists(FullPath(legacySolution)), "legacy solution should be removed after rename.");

            var modules = ReadObject("config/sources.example.json");
            var views = ReadObject("config/views.example.json");

            Require(StringValue(views, "defaultView") == "workspace", "PlugHub must use the single workspace view.");
            Require(Views(views).Count() == 1, "PlugHub must expose exactly one workspace view.");
            Require(ArrayValue(modules, "moduleSources").Count == 0, "moduleSources must not include startup repository examples.");
            Require(Repositories(modules).Count() >= 2, "repositories must include public and private repository examples.");
            Require(!SequenceValue(modules, "packageDirectories").Contains(RemovedSamplesDirectory()), "sample modules must be removed from built-in runtime config.");
            Require(SequenceValue(modules, "packageDirectories").SequenceEqual(new[] { "packages" }), "installed packages folder must be the only automatic package loading root.");

            var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            Require(configurationModels.Contains("DisplayName"), "modules config model must support displayName.");
            Require(configurationModels.Contains("IconPath"), "modules config model must support iconPath.");
            Require(configurationModels.Contains("PackageRepositoryConfiguration") && configurationModels.Contains("ApiKey"), "modules config model must support package repositories with apiKey.");
            var modulesText = ReadText("config/sources.example.json");
            Require(modulesText.Contains("\"repositories\""), "modules config must include repository catalog settings.");
            Require(!modulesText.Contains("\"autoUpdate\""), "repository catalog settings must not expose startup autoUpdate.");
            Require(modulesText.Contains("\"provider\": \"gitee\"") && modulesText.Contains("\"repository\": \"https://gitee.com/GaoMengGu/PlugHub_Packages\""), "default repository must point at the public Gitee PlugHub_Packages URL.");
            Require(modulesText.Contains("\"manifestPath\": \"package.json\""), "module source examples must point at package.json.");

            var revitText = ReadAllCSharp("src/PlugHub.Revit2020");
            Require(!revitText.Contains("RegisterDockablePane") && !revitText.Contains("DockablePaneProviderData") && !revitText.Contains("IDockablePaneProvider"), "settings and feature UI must not use Revit DockablePane for this architecture.");
            Require(revitText.Contains("FrameworkSettingsWindow") && revitText.Contains("System.Windows.Window"), "settings UI must use a WPF window.");
            Require(revitText.Contains("FeatureExecutionGate"), "feature execution must be gated by latest runtime configuration.");
        }

        private static void ValidateSettingsPaneV21Specification()
        {
            var settingsWindow = ReadText("src/PlugHub.Revit2020/FrameworkSettingsWindow.cs");
            var settingsCommand = ReadText("src/PlugHub.Revit2020/FrameworkSettingsCommand.cs");
            var refreshCommand = ReadText("src/PlugHub.Revit2020/FrameworkRefreshCommand.cs");
            var statusWindow = ReadText("src/PlugHub.Revit2020/FrameworkStatusWindow.cs");
            var featureCommand = ReadText("src/PlugHub.Revit2020/FrameworkFeatureCommand.cs");
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var sourceResolver = ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            var revitProject = ReadText("src/PlugHub.Revit2020/PlugHub.Revit2020.csproj");

            Require(!File.Exists(FullPath("src/PlugHub.Revit2020/FrameworkSettingsForm.cs")), "legacy WinForms settings form must be removed.");
            Require(!File.Exists(FullPath("src/PlugHub.Revit2020/FrameworkSettingsPane.cs")), "legacy DockablePane settings provider must be removed.");
            Require(!ReadAllCSharp("src/PlugHub.Revit2020").Contains("System.Windows.Forms") && !ReadAllCSharp("src/PlugHub.Revit2020").Contains("WindowsFormsHost"), "Revit settings/feature UI must not reference WinForms hosting.");
            Require(settingsCommand.Contains("FrameworkSettingsWindow") && settingsCommand.Contains("ShowDialog"), "settings ribbon command must open the WPF settings dialog.");
            Require(!settingsCommand.Contains("GetDockablePane") && !settingsCommand.Contains("pane.Hide") && !settingsCommand.Contains("pane.Show"), "settings command must not toggle a DockablePane.");
            Require(refreshCommand.Contains("FrameworkRuntimeState.Refresh") && refreshCommand.Contains("FrameworkStatusWindow"), "runtime refresh must be an explicit Ribbon command with WPF feedback.");
            Require(refreshCommand.Contains("ShowRefreshResult") && !refreshCommand.Contains("BuildRuntimeSummary"), "refresh command must show a focused refresh result instead of repeating runtime status.");
            Require(featureCommand.Contains("ShowRuntimeStatus"), "status command must use the focused runtime status view.");
            Require(featureCommand.Contains("FrameworkStatusWindow") && !featureCommand.Contains("TaskDialog.Show"), "framework fallback feature feedback must use WPF.");
            Require(ribbonBuilder.Contains("LoadFeatureIcon") && ribbonBuilder.Contains("LargeImage"), "configured feature icons must be applied to Revit ribbon buttons.");
            Require(ribbonBuilder.Contains("FrameworkSettingsCommand"), "framework Ribbon panel must expose settings command.");

            foreach (var token in new[] { "class FrameworkSettingsWindow", ": Window", "TabControl", "DataGrid", "BuildFeaturesTab", "BuildGroupsTab", "BuildRepositoriesTab", "BuildLogsTab", "RepositoryRow", "RepositoryPackageRow", "GroupRow", "ReloadFromDisk", "ContextMenu", "DragDrop", "Microsoft.Win32.OpenFileDialog" })
            {
                Require(settingsWindow.Contains(token), "WPF settings UI token missing: " + token);
            }

            foreach (var forbidden in new[] { "FrameworkRuntimeState.Refresh", "Assembly.LoadFrom" })
            {
                Require(!settingsWindow.Contains(forbidden), "settings window must only save configuration and must not run runtime work: " + forbidden);
            }

            Require(statusWindow.Contains("class FrameworkStatusWindow") && statusWindow.Contains(": Window"), "status and feature fallback UI must use a WPF status window.");
            foreach (var token in new[] { "ShowRefreshResult", "ShowRuntimeStatus", "ShowLogs", "showLogs" })
            {
                Require(statusWindow.Contains(token), "status window must separate refresh, status, and log concerns: " + token);
            }
            Require(configurationModels.Contains("PackageRepositoryConfiguration"), "module configuration must expose repository catalog settings.");
            Require(sourceResolver.Contains("AddPackageDirectoryModules"), "package directories must be scanned for drop-in package manifests.");
            Require(sourceResolver.Contains("FindModuleManifests"), "module directory resolver must discover manifests automatically.");
            Require(sourceResolver.Contains("\"package.json\"") && sourceResolver.Contains("\"*.package.json\""), "module directory resolver must discover package.json and DLL-adjacent *.package.json manifests.");
            Require(!sourceResolver.Contains("ProcessStartInfo") && !sourceResolver.Contains("packages/github"), "startup resolver must not access repository caches or run git.");
            Require(!revitProject.Contains("System.Windows.Forms") && !revitProject.Contains("WindowsFormsIntegration"), "Revit adapter should not reference WinForms after moving settings and feature UI to WPF.");
            Require(!revitProject.Contains("PlugHubModuleFiles"), "Revit build must not depend on a source modules folder.");
            Require(revitProject.Contains("packages\\README.md"), "Revit build must create the runtime packages folder.");
        }

        private static void ValidateSettingsRibbonCleanupSpecification()
        {
            var settingsWindow = ReadText("src/PlugHub.Revit2020/FrameworkSettingsWindow.cs");
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var addinTemplate = ReadText("manifests/PlugHub.addin.template");
            var buildProps = ReadText("build/Directory.Build.props");
            var views = ReadObject("config/views.example.json");

            Require(settingsWindow.Contains("LoadModuleDocuments") && !settingsWindow.Contains(RemovedSamplesDirectory()), "settings must not reference removed sample module manifests.");
            Require(settingsWindow.Contains("SaveModuleDocuments"), "settings must save edits back to their owning module manifest.");
            Require(!settingsWindow.Contains("nameof(FeatureRow.Panel)") && !settingsWindow.Contains("feature.Group = row.Panel"), "feature settings must not expose user-editable panel ownership.");
            Require(!settingsWindow.Contains("点击 Ribbon 的「刷新配置」"), "settings UI must not point users to the removed refresh Ribbon button.");

            Require(ribbonBuilder.Contains("\"PlugHub_Framework_Settings\""), "Ribbon must keep the settings entry.");
            Require(!ribbonBuilder.Contains("\"PlugHub_Framework_Refresh\"") && !ribbonBuilder.Contains("\"刷新配置\""), "Ribbon must not expose refresh configuration.");
            Require(!ribbonBuilder.Contains("\"PlugHub_Framework_Status\"") && !ribbonBuilder.Contains("\"状态\""), "Ribbon must not expose status.");

            Require(addinTemplate.Contains("<VendorDescription>GAOMENGGU</VendorDescription>"), "addin publisher description must be GAOMENGGU.");
            Require(buildProps.Contains("<Company>GAOMENGGU</Company>") && buildProps.Contains("<Authors>GAOMENGGU</Authors>"), "assembly metadata publisher must be GAOMENGGU.");

            var groupNames = Views(views)
                .SelectMany(view => ArrayValue(view, "groups").Cast<Dictionary<string, object>>())
                .Select(group => StringValue(group, "name"))
                .ToList();

            foreach (var removed in RemovedWorkspaceGroupNames().Concat(new[] { "机电风管", "族批处理" }))
            {
                Require(!groupNames.Contains(removed), "workspace group should be removed or renamed: " + removed);
            }
        }

        private static void ValidateBuiltinOnlySpecification()
        {
            var modules = AllModules().ToList();
            var allText = ReadProductionCSharp() + "\n" + ReadText("PlugHub.sln") + "\n" + ReadText("PlugHub.slnx") + "\n" + ReadText("config/sources.example.json") + "\n" + ReadText("config/views.example.json");

            Require(modules.Count == 0, "framework runtime configuration must expose no bundled modules.");
            Require(modules.SelectMany(Features).Count() == 0, "framework runtime configuration must expose no bundled features.");
            Require(!Directory.Exists(FullPath("src/" + RemovedSampleProject())), "sample module project must be removed.");
            Require(!Directory.Exists(FullPath(RemovedSamplesDirectory())), "sample module manifests must be removed.");
            foreach (var forbidden in RemovedContentTokens())
            {
                Require(!allText.Contains(forbidden), "removed module content must be absent: " + forbidden);
            }
        }

        private static void ValidateSettingsCreationAndSortingSpecification()
        {
            var settingsWindow = ReadText("src/PlugHub.Revit2020/FrameworkSettingsWindow.cs");

            foreach (var token in new[] { "插件包", "分组", "所属分组", "GroupOptionsForFeatureRows", "ApplyGroupRows", "RefreshGroupPositions" })
            {
                Require(settingsWindow.Contains(token), "settings must manage plugin packages, groups, and feature placement: " + token);
            }

            foreach (var forbidden in new[] { "新建模块", "新建功能", "private void AddModule(", "private void AddFeature(", "CreateModule(", "CreateFeature(", "所属模块", "ModuleIdsForFeatureRows" })
            {
                Require(!settingsWindow.Contains(forbidden), "settings must not create placeholder modules/features or expose module placement: " + forbidden);
            }

            Require(!settingsWindow.Contains("TextColumn(nameof(ModuleRow.Order)") && !settingsWindow.Contains("TextColumn(nameof(FeatureRow.Order)") && !settingsWindow.Contains("TextColumn(nameof(GroupRow.Order)"), "settings must not expose raw numeric order columns.");
            Require(settingsWindow.Contains("PositionText") && settingsWindow.Contains("RefreshPluginPackagePositions") && settingsWindow.Contains("RefreshFeaturePositions") && settingsWindow.Contains("RefreshGroupPositions"), "settings must show human-readable position text and maintain drag/up-down sorting.");
            Require(settingsWindow.Contains("AddCustomGroup") && settingsWindow.Contains("RemoveSelectedGroup"), "settings must allow custom workspace groups to be created and removed.");
            Require(!settingsWindow.Contains("CreateButton(\"新增分组\"") && !settingsWindow.Contains("CreateButton(\"删除分组\""), "custom group create/delete actions must remain in the right-click menu only.");
        }

        private static void ValidateDefaultIconSpecification()
        {
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var iconProvider = ReadText("src/PlugHub.Revit2020/DefaultRibbonIconProvider.cs");
            var settingsWindow = ReadText("src/PlugHub.Revit2020/FrameworkSettingsWindow.cs");
            var modulesText = ReadText("config/sources.example.json");

            Require(ribbonBuilder.Contains("DefaultRibbonIconProvider") && ribbonBuilder.Contains("CreateSmallIcon") && ribbonBuilder.Contains("CreateLargeIcon"), "Ribbon builder must apply built-in default small/large icons.");
            Require(ribbonBuilder.Contains("CreateSmallIcon(\"settings\")") && ribbonBuilder.Contains("CreateLargeIcon(\"settings\")"), "settings ribbon button must use a built-in settings icon.");
            Require(ribbonBuilder.Contains("LoadConfiguredIcon"), "Ribbon builder must resolve configured file icons and built-in icon keys.");
            Require(iconProvider.Contains("CreateSmallIcon") && iconProvider.Contains("CreateLargeIcon"), "default icon provider must expose small and large icon factories.");
            Require(iconProvider.Contains("BuiltinIconKeys") && iconProvider.Contains("settings") && iconProvider.Contains("duct") && iconProvider.Contains("family"), "default icon provider must expose a small built-in icon suite.");
            Require(settingsWindow.Contains("BuildBuiltinIconMenu") && settingsWindow.Contains("SetSelectedFeatureBuiltinIcon"), "settings must let users choose built-in feature icons.");
            Require(!modulesText.Contains("commandAssembly"), "framework config must not ship command-backed feature entries.");
        }

        private static void ValidateSettingsGroupFeatureEditingBehavior()
        {
            var settingsWindow = ReadText("src/PlugHub.Revit2020/FrameworkSettingsWindow.cs");
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");

            Require(settingsWindow.Contains("BuildSelectedFeatureEditor") && settingsWindow.Contains("_selectedFeatureGroupCombo") && settingsWindow.Contains("_selectedFeatureButtonSizeCombo"), "feature group and button size editors must be ordinary selected-feature combo boxes.");
            Require(settingsWindow.Contains("ApplySelectedFeatureGroup") && settingsWindow.Contains("ApplySelectedFeatureButtonSize"), "selected feature combo boxes must write group and button size back to the selected row.");
            Require(settingsWindow.Contains("RefreshFeaturePositionsByGroup"), "feature ordering must be recalculated per workspace group.");
            Require(settingsWindow.Contains("SortFeatureRowsForRuntimeOrder"), "feature grid must be ordered the same way runtime ribbon composition is ordered.");
            Require(settingsWindow.Contains("IsInteractiveGridEditor"), "row drag behavior must ignore combo boxes, text boxes, check boxes, and buttons.");
            Require(settingsWindow.Contains("TrySave") && settingsWindow.Contains("ReportSettingsError"), "settings save must catch exceptions and report them inline.");
            Require(settingsWindow.Contains("SafeRefreshGrid") && settingsWindow.Contains("IsEditTransactionRefreshError"), "settings grid refresh must be safe during DataGrid edit transactions.");
            foreach (var forbiddenRefresh in new[] { "_featuresGrid.Items.Refresh", "_groupsGrid.Items.Refresh", "_repositoriesGrid.Items.Refresh", "_repositoryPackagesGrid.Items.Refresh", "_pluginPackagesGrid.Items.Refresh" })
            {
                Require(!settingsWindow.Contains(forbiddenRefresh), "settings grid refresh must not call Items.Refresh directly: " + forbiddenRefresh);
            }

            Require(!settingsWindow.Contains("MessageBox.Show"), "settings window must not show pop-up prompts for normal settings operations.");
            Require(!settingsWindow.Contains("BuildInstalledPackagesTab") && !settingsWindow.Contains("BuildPluginPackagesTab") && !settingsWindow.Contains("ApplyPluginPackageRows();"), "settings window must not expose the installed package settings tab.");
            Require(ribbonBuilder.Contains("OrderFeaturesForRibbon"), "Ribbon builder must explicitly order features inside each panel.");
        }

        private static void ValidatePackageSourceAndReleaseBehavior()
        {
            var modulesText = ReadText("config/sources.example.json");
            var settingsWindow = ReadText("src/PlugHub.Revit2020/FrameworkSettingsWindow.cs");
            var sourceResolver = ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var packageRepositoryService = ReadText("src/PlugHub.Framework/Packages/PackageRepositoryService.cs");
            var workflow = ReadText(".github/workflows/release.yml");
            var giteeWorkflow = ReadText(".github/workflows/sync-gitee.yml");
            var buildScript = ReadText("scripts/build-revit2020.ps1");
            var readme = ReadText("README.md");

            Require(modulesText.Contains("\"provider\": \"gitee\"") && modulesText.Contains("\"repository\": \"https://gitee.com/GaoMengGu/PlugHub_Packages\""), "default package repository must point at the public Gitee PlugHub_Packages URL.");
            Require(modulesText.Contains("\"packageDirectories\": [") && modulesText.Contains("\"packages\""), "installed package discovery must point at packages.");
            Require(!modulesText.Contains("packages/github/GaoMengGu_PlugHub_Packages"), "repository caches must not live under packages.");
            Require(!modulesText.Contains("GaoMengGu/PlugHub_Modules"), "default github source must not point at PlugHub_Modules.");
            Require(settingsWindow.Contains("DefaultRepositoryProvider = \"gitee\"") && settingsWindow.Contains("https://gitee.com/GaoMengGu/PlugHub_Packages"), "settings repository creation must default to the public Gitee PlugHub_Packages URL.");

            Require(!sourceResolver.Contains("RunGit") && !sourceResolver.Contains("AutoUpdate") && !sourceResolver.Contains("AddGitHubModules"), "runtime source resolver must not pull or load repository packages at startup.");
            Require(settingsWindow.Contains("BuildRepositoriesTab") && settingsWindow.Contains("LoadRepositoryRows"), "settings must present sources as repositories.");
            Require(settingsWindow.Contains("BrowseSelectedRepository") && settingsWindow.Contains("InstallSelectedRepositoryPackage"), "settings must browse repositories and install selected packages.");
            Require(settingsWindow.Contains("UpdateSelectedRepositoryPackage") && settingsWindow.Contains("UninstallSelectedRepositoryPackage"), "settings must support repository package update and uninstall.");
            Require(settingsWindow.Contains("LoadCachedRepositoryPackages") && settingsWindow.Contains("StartRepositoryUpdateCheck") && settingsWindow.Contains("Task.Run"), "settings must show cached repository packages and check for updates in the background.");
            Require(settingsWindow.Contains("ComboColumn(nameof(RepositoryRow.Provider), \"类型\"") && settingsWindow.Contains("new[] { \"github\", \"gitee\" }"), "repository settings must expose a provider type column for GitHub and Gitee.");
            Require(settingsWindow.Contains("MenuItem(\"新增仓库\"") && settingsWindow.Contains("AddRepository()"), "repository context menu must expose one generic add repository action.");
            foreach (var forbiddenAddMenu in new[] { "新增 GitHub 公开仓库", "新增 GitHub 私有仓库", "新增 Gitee 公开仓库", "新增 Gitee 私有仓库" })
            {
                Require(!settingsWindow.Contains(forbiddenAddMenu), "repository context menu must not expose split add repository entries: " + forbiddenAddMenu);
            }

            Require(settingsWindow.Contains("BuildLogsTab") && settingsWindow.Contains("\"日志\"") && !settingsWindow.Contains("BuildDiagnosticsTab"), "settings must present diagnostics as logs.");
            Require(settingsWindow.Contains("ApiKey") && settingsWindow.Contains("Visibility") && settingsWindow.Contains("private"), "settings must support public and private repositories with apiKey.");
            Require(!settingsWindow.Contains("确定卸载插件包") && !settingsWindow.Contains("result.Success ? MessageBoxImage.Information"), "repository package install and uninstall must report status inline without pop-up result prompts.");
            Require(packageRepositoryService.Contains("--sparse") && packageRepositoryService.Contains("sparse-checkout") && packageRepositoryService.Contains("SparseCheckoutPatterns"), "repository browsing must use sparse checkout instead of pulling the whole repository.");
            Require(packageRepositoryService.Contains("\"gitee\"") && packageRepositoryService.Contains("https://gitee.com/") && packageRepositoryService.Contains("oauth2:"), "repository browsing must support Gitee HTTPS repositories with apiKey credentials.");
            Require(packageRepositoryService.Contains("InstallPackagePayload") && packageRepositoryService.Contains("WriteSingleModuleManifest") && !packageRepositoryService.Contains("CopyDirectory("), "repository install must split selected plugins and must not copy the whole repository directory.");
            Require(packageRepositoryService.Contains("ApplyPendingOperations") && packageRepositoryService.Contains("pending-operations.json") && packageRepositoryService.Contains("PendingPackageOperation.Restart"), "repository package operations must defer locked DLL deletion and replacement and mark normal installs as restart-required.");
            Require(settingsWindow.Contains("已安装待重启") && settingsWindow.Contains("PendingOperation") && settingsWindow.Contains("IsLoadedInCurrentRuntime"), "repository package status must distinguish installed from installed-pending-restart.");
            Require(ReadText("src/PlugHub.Framework/Runtime/FrameworkRuntime.cs").Contains("ApplyPendingOperations"), "runtime startup must apply deferred package operations before module discovery.");
            Require(!settingsWindow.Contains("LoadDiagnosticRows(FrameworkRuntimeState.Current);\r\n            LoadSourceRows();"), "settings save must not reload stale runtime diagnostics after saving configuration.");

            Require(workflow.Contains("-UseRelativeAddinAssembly"), "release workflow must build a package with relative addin assembly path.");
            Require(giteeWorkflow.Contains("branches:") && giteeWorkflow.Contains("- main"), "Gitee sync workflow must run for main pushes.");
            Require(giteeWorkflow.Contains("workflow_dispatch"), "Gitee sync workflow must support manual dispatch.");
            Require(giteeWorkflow.Contains("GITEE_PRIVATE_KEY") && giteeWorkflow.Contains("GITEE_TOKEN") && giteeWorkflow.Contains("GITEE_USER"), "Gitee sync workflow must validate configured Gitee secrets.");
            Require(giteeWorkflow.Contains("git@gitee.com:GaoMengGu/PlugHub.git") && giteeWorkflow.Contains("git push gitee HEAD:main"), "Gitee sync workflow must push main to GaoMengGu/PlugHub on Gitee.");
            Require(buildScript.Contains("[switch]$UseRelativeAddinAssembly") && buildScript.Contains("PlugHub.Revit2020.dll"), "build script must support relative release addin assembly paths.");
            Require(workflow.Contains("*.pdb") && workflow.Contains("*.sigstore.json") && !workflow.Contains("Compress-Archive -Path \"dist\\Revit2020\\*\""), "release zip must exclude pdb and sigstore files.");

            Require(readme.Contains("个人使用") && readme.Contains("不得商用"), "README must state the non-commercial personal-use license restriction.");
        }

        private static void ValidateRepositoryInstallFlowBehavior()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var packageDirectory = Path.Combine(tempRoot, "packages", "installed-demo");
                var repositoryCacheDirectory = Path.Combine(tempRoot, "repository-cache", "GaoMengGu_PlugHub_Packages");
                Directory.CreateDirectory(packageDirectory);
                Directory.CreateDirectory(repositoryCacheDirectory);
                File.WriteAllText(
                    Path.Combine(packageDirectory, "package.json"),
                    "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"installed-package\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(
                    Path.Combine(repositoryCacheDirectory, "package.json"),
                    "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"repository-only-package\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");

                var modules = new PlugHub.Framework.Configuration.ModulesConfiguration
                {
                    SchemaVersion = "1.0",
                    PackageDirectories = new List<string> { "packages" },
                    ModuleSources = new List<PlugHub.Framework.Configuration.ModuleSourceConfiguration>
                    {
                        new PlugHub.Framework.Configuration.ModuleSourceConfiguration
                        {
                            Id = "legacy-startup-repository",
                            Type = "github",
                            Path = "repository-cache/GaoMengGu_PlugHub_Packages",
                            Repository = "GaoMengGu/PlugHub_Packages",
                            Ref = "main",
                            ManifestPath = "package.json",
                            Enabled = true,
                            AutoUpdate = true
                        }
                    },
                    Repositories = new List<PlugHub.Framework.Configuration.PackageRepositoryConfiguration>
                    {
                        new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
                        {
                            Id = "public-packages",
                            Provider = "github",
                            Visibility = "public",
                            Repository = "GaoMengGu/PlugHub_Packages",
                            Ref = "main",
                            ManifestPath = "package.json",
                            Enabled = true
                        },
                        new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
                        {
                            Id = "private-packages",
                            Provider = "github",
                            Visibility = "private",
                            Repository = "example/private-packages",
                            Ref = "main",
                            ManifestPath = "package.json",
                            ApiKey = "test-key",
                            Enabled = false
                        }
                    },
                    ConflictPolicy = new PlugHub.Framework.Configuration.ConflictPolicyConfiguration(),
                    Modules = new List<PlugHub.Framework.Configuration.ModuleConfiguration>()
                };

                var result = new PlugHub.Framework.Sources.ModuleSourceResolver().Resolve(tempRoot, modules);
                Require(result.Modules.Repositories.Count == 2, "repository catalog configuration must be preserved during runtime source resolution.");
                Require(result.Modules.Repositories.Any(repository => repository.Visibility == "private" && repository.ApiKey == "test-key"), "private repository apiKey must be preserved.");
                Require(result.Modules.Modules.Any(module => module.Id == "installed-package"), "startup must load packages installed under packages.");
                Require(!result.Modules.Modules.Any(module => module.Id == "repository-only-package"), "startup must not load packages directly from repository cache.");
                Require(!result.Diagnostics.Any(message => message.Code == "PH-SOURCE-GIT"), "startup resolution must not run repository git operations.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void ValidateLockedPackageOperationBehavior()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var installedDirectory = Path.Combine(tempRoot, "packages", "locked-update");
                var sourceDirectory = Path.Combine(tempRoot, "repository-cache", "locked-update");
                Directory.CreateDirectory(installedDirectory);
                Directory.CreateDirectory(sourceDirectory);

                var installedDll = Path.Combine(installedDirectory, "LockedUpdate.dll");
                File.WriteAllText(installedDll, "locked");
                File.WriteAllText(Path.Combine(installedDirectory, "package.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"1.0.0\",\"modules\":[{\"id\":\"locked-update\",\"assembly\":\"LockedUpdate.dll\",\"type\":\"Demo.LockedUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(Path.Combine(sourceDirectory, "LockedUpdate.dll"), "replacement");
                File.WriteAllText(Path.Combine(sourceDirectory, "package.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"2.0.0\",\"modules\":[{\"id\":\"locked-update\",\"assembly\":\"LockedUpdate.dll\",\"type\":\"Demo.LockedUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");

                var descriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "locked-update",
                    ModuleId = "locked-update",
                    DisplayName = "Locked Update",
                    ManifestPath = Path.Combine(sourceDirectory, "package.json"),
                    SourceDirectory = sourceDirectory,
                    InstallDirectory = installedDirectory,
                    IsInstalled = true
                };

                using (File.Open(installedDll, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var service = new PlugHub.Framework.Packages.PackageRepositoryService();
                    var updateResult = service.Update(tempRoot, descriptor);

                    Require(updateResult.Success, "updating a locked Revit package must queue a deferred update instead of failing: " + updateResult.Message);
                    Require(updateResult.Message.Contains("重启") && updateResult.Message.Contains("更新"), "locked update message must tell the user the update is queued for Revit restart.");
                    Require(File.Exists(installedDll), "locked package files must remain in place until Revit restarts.");
                    Require(!File.ReadAllText(Path.Combine(installedDirectory, "package.json")).Contains("locked-update"), "locked update must remove the old module declaration before restart.");
                    Require(Directory.GetFiles(Path.Combine(tempRoot, "repository-cache"), "pending-operations.json", SearchOption.AllDirectories).Any(), "locked update must write a pending operation marker.");
                }

                var updateDiagnostics = new PlugHub.Framework.Packages.PackageRepositoryService().ApplyPendingOperations(tempRoot);
                Require(!updateDiagnostics.Any(message => message.Severity == PlugHub.Contracts.Modules.DiagnosticSeverity.Error), "pending locked update must apply on next startup: " + string.Join("; ", updateDiagnostics.Select(item => item.Message)));
                Require(File.ReadAllText(installedDll) == "replacement", "pending locked update must replace the DLL after restart.");
                Require(File.ReadAllText(Path.Combine(installedDirectory, "package.json")).Contains("locked-update"), "pending locked update must restore the selected module manifest.");

                var uninstallDirectory = Path.Combine(tempRoot, "packages", "locked-uninstall");
                Directory.CreateDirectory(uninstallDirectory);
                var uninstallDll = Path.Combine(uninstallDirectory, "LockedUninstall.dll");
                File.WriteAllText(uninstallDll, "locked");
                File.WriteAllText(Path.Combine(uninstallDirectory, "package.json"), "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"locked-uninstall\",\"assembly\":\"LockedUninstall.dll\",\"type\":\"Demo.LockedUninstallModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                var uninstallDescriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "locked-uninstall",
                    ModuleId = "locked-uninstall",
                    DisplayName = "Locked Uninstall",
                    InstallDirectory = uninstallDirectory,
                    IsInstalled = true
                };

                using (File.Open(uninstallDll, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var service = new PlugHub.Framework.Packages.PackageRepositoryService();
                    var uninstallResult = service.Uninstall(tempRoot, uninstallDescriptor);
                    Require(uninstallResult.Success, "uninstalling a locked Revit package must queue a deferred delete instead of failing: " + uninstallResult.Message);
                    Require(uninstallResult.Message.Contains("重启") && uninstallResult.Message.Contains("卸载"), "locked uninstall message must tell the user the delete is queued for Revit restart.");
                    Require(File.Exists(uninstallDll), "locked package files must remain in place until Revit restarts.");
                    Require(!File.ReadAllText(Path.Combine(uninstallDirectory, "package.json")).Contains("locked-uninstall"), "locked uninstall must remove the module declaration before restart.");

                    var resolvedWhileLocked = new PlugHub.Framework.Sources.ModuleSourceResolver().Resolve(
                        tempRoot,
                        new PlugHub.Framework.Configuration.ModulesConfiguration
                        {
                            SchemaVersion = "1.0",
                            PackageDirectories = new List<string> { "packages" },
                            ModuleSources = new List<PlugHub.Framework.Configuration.ModuleSourceConfiguration>(),
                            ConflictPolicy = new PlugHub.Framework.Configuration.ConflictPolicyConfiguration(),
                            Modules = new List<PlugHub.Framework.Configuration.ModuleConfiguration>()
                        });
                    Require(!resolvedWhileLocked.Modules.Modules.Any(module => module.Id == "locked-uninstall"), "queued locked uninstall must stop the plugin from being discovered after refresh.");
                }

                var uninstallDiagnostics = new PlugHub.Framework.Packages.PackageRepositoryService().ApplyPendingOperations(tempRoot);
                Require(!uninstallDiagnostics.Any(message => message.Severity == PlugHub.Contracts.Modules.DiagnosticSeverity.Error), "pending locked uninstall must apply on next startup: " + string.Join("; ", uninstallDiagnostics.Select(item => item.Message)));
                Require(!Directory.Exists(uninstallDirectory), "pending locked uninstall must delete package files after restart.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void ValidateRepositoryPackageGranularityAndInstallPayload()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var repositoryRoot = Path.Combine(tempRoot, "repository-cache", "public-packages");
                Directory.CreateDirectory(Path.Combine(repositoryRoot, "dist"));
                Directory.CreateDirectory(Path.Combine(repositoryRoot, "src", "ShouldNotInstall"));
                File.WriteAllText(Path.Combine(repositoryRoot, "README.md"), "repository readme");
                File.WriteAllText(Path.Combine(repositoryRoot, "src", "ShouldNotInstall", "Source.cs"), "source");
                File.WriteAllText(Path.Combine(repositoryRoot, "dist", "Duct.dll"), "duct");
                File.WriteAllText(Path.Combine(repositoryRoot, "dist", "Family.dll"), "family");
                File.WriteAllText(
                    Path.Combine(repositoryRoot, "package.json"),
                    "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"duct-package\",\"assembly\":\"dist/Duct.dll\",\"type\":\"Demo.DuctModule\",\"displayName\":\"Duct\",\"enabled\":true,\"visible\":true,\"features\":[{\"id\":\"duct.switch\",\"name\":\"Switch\",\"category\":\"mep\",\"group\":\"duct\",\"order\":1,\"defaultState\":\"Visible\",\"commandAssembly\":\"dist/Duct.dll\",\"commandType\":\"Demo.DuctCommand\"}]},{\"id\":\"family-package\",\"assembly\":\"dist/Family.dll\",\"type\":\"Demo.FamilyModule\",\"displayName\":\"Family\",\"enabled\":true,\"visible\":true,\"features\":[{\"id\":\"family.batch\",\"name\":\"Batch\",\"category\":\"family\",\"group\":\"family\",\"order\":1,\"defaultState\":\"Visible\",\"commandAssembly\":\"dist/Family.dll\",\"commandType\":\"Demo.FamilyCommand\"}]}]}");

                var service = new PlugHub.Framework.Packages.PackageRepositoryService();
                var packages = service.BrowseCached(tempRoot, "public-packages", repositoryRoot, out var diagnostics);
                Require(!diagnostics.Any(), "cached repository package browse should not emit diagnostics: " + string.Join("; ", diagnostics.Select(item => item.Message)));
                Require(packages.Count == 2, "repository root package.json with two modules must browse as two plugin rows.");
                Require(packages.Select(package => package.PackageId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "plugin rows from the same package.json must install independently by module id.");
                Require(packages.Select(package => package.InstallDirectory).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "plugin rows from the same package.json must use independent install directories.");

                var ductPackage = packages.Single(package => package.ModuleId == "duct-package");
                var familyPackage = packages.Single(package => package.ModuleId == "family-package");
                var installResult = service.Install(tempRoot, ductPackage);
                Require(installResult.Success, "repository package install should succeed: " + installResult.Message);

                var ductInstallDirectory = Path.Combine(tempRoot, "packages", "duct-package");
                var familyInstallDirectory = Path.Combine(tempRoot, "packages", "family-package");
                Require(File.Exists(Path.Combine(ductInstallDirectory, "package.json")), "installed plugin must write a package-local manifest.");
                Require(!Directory.Exists(familyInstallDirectory), "installing one plugin must not install another module from the same repository manifest.");
                Require(Directory.GetFiles(Path.Combine(tempRoot, "packages"), "package.json", SearchOption.AllDirectories).Length == 1, "installing one plugin must create only one package.json under packages.");
                Require(File.Exists(Path.Combine(ductInstallDirectory, "dist", "Duct.dll")), "installed plugin must copy its configured assembly.");
                Require(!File.Exists(Path.Combine(ductInstallDirectory, "dist", "Family.dll")), "installed plugin must not copy another plugin assembly.");
                Require(!File.Exists(Path.Combine(ductInstallDirectory, "README.md")), "installed plugin must not copy repository-level files.");
                Require(!Directory.Exists(Path.Combine(ductInstallDirectory, "src")), "installed plugin must not copy repository source folders.");

                var resolved = new PlugHub.Framework.Sources.ModuleSourceResolver().Resolve(
                    tempRoot,
                    new PlugHub.Framework.Configuration.ModulesConfiguration
                    {
                        SchemaVersion = "1.0",
                        PackageDirectories = new List<string> { "packages" },
                        ModuleSources = new List<PlugHub.Framework.Configuration.ModuleSourceConfiguration>(),
                        ConflictPolicy = new PlugHub.Framework.Configuration.ConflictPolicyConfiguration(),
                        Modules = new List<PlugHub.Framework.Configuration.ModuleConfiguration>()
                    });

                Require(resolved.Modules.Modules.Count(module => module.Id == "duct-package") == 1, "installed repository plugin must be discoverable from packages on next startup.");
                Require(!resolved.Modules.Modules.Any(module => module.Id == "family-package"), "uninstalled sibling plugin from the same repository manifest must not load on startup.");
                Require(resolved.Modules.Modules.All(module => !string.IsNullOrWhiteSpace(module.ResolvedBaseDirectory)), "installed package modules must have a resolved base directory for relative DLL loading.");

                Directory.CreateDirectory(Path.Combine(familyInstallDirectory, "dist"));
                File.Copy(Path.Combine(repositoryRoot, "dist", "Duct.dll"), Path.Combine(familyInstallDirectory, "dist", "Duct.dll"));
                File.Copy(Path.Combine(repositoryRoot, "dist", "Family.dll"), Path.Combine(familyInstallDirectory, "dist", "Family.dll"));
                File.Copy(Path.Combine(repositoryRoot, "package.json"), Path.Combine(familyInstallDirectory, "package.json"));

                var uninstallResult = service.Uninstall(tempRoot, ductPackage);
                Require(uninstallResult.Success, "uninstalling an installed plugin should succeed: " + uninstallResult.Message);
                var resolvedAfterUninstall = new PlugHub.Framework.Sources.ModuleSourceResolver().Resolve(
                    tempRoot,
                    new PlugHub.Framework.Configuration.ModulesConfiguration
                    {
                        SchemaVersion = "1.0",
                        PackageDirectories = new List<string> { "packages" },
                        ModuleSources = new List<PlugHub.Framework.Configuration.ModuleSourceConfiguration>(),
                        ConflictPolicy = new PlugHub.Framework.Configuration.ConflictPolicyConfiguration(),
                        Modules = new List<PlugHub.Framework.Configuration.ModuleConfiguration>()
                    });
                Require(!resolvedAfterUninstall.Modules.Modules.Any(module => module.Id == "duct-package"), "uninstalled repository plugin must not load after restart even when an old package-level manifest also declared it.");
                Require(resolvedAfterUninstall.Modules.Modules.Any(module => module.Id == "family-package"), "uninstalling one plugin from a legacy multi-plugin manifest must preserve the sibling plugin.");
                Require(File.ReadAllText(Path.Combine(familyInstallDirectory, "package.json")).Contains("family-package"), "legacy sibling package manifest must keep the remaining module.");
                Require(!File.ReadAllText(Path.Combine(familyInstallDirectory, "package.json")).Contains("duct-package"), "legacy sibling package manifest must remove the uninstalled module.");

                var familyUninstallResult = service.Uninstall(tempRoot, familyPackage);
                Require(familyUninstallResult.Success, "uninstalling the sibling plugin should succeed: " + familyUninstallResult.Message);
                var resolvedAfterFamilyUninstall = new PlugHub.Framework.Sources.ModuleSourceResolver().Resolve(
                    tempRoot,
                    new PlugHub.Framework.Configuration.ModulesConfiguration
                    {
                        SchemaVersion = "1.0",
                        PackageDirectories = new List<string> { "packages" },
                        ModuleSources = new List<PlugHub.Framework.Configuration.ModuleSourceConfiguration>(),
                        ConflictPolicy = new PlugHub.Framework.Configuration.ConflictPolicyConfiguration(),
                        Modules = new List<PlugHub.Framework.Configuration.ModuleConfiguration>()
                    });
                Require(!resolvedAfterFamilyUninstall.Modules.Modules.Any(module => module.Id == "duct-package" || module.Id == "family-package"), "uninstalling all plugins from a legacy multi-plugin manifest must remove them from restart loading.");

                var familyInstallResult = service.Install(tempRoot, familyPackage);
                Require(familyInstallResult.Success, "installing the sibling plugin should succeed: " + familyInstallResult.Message);
                Require(File.Exists(Path.Combine(familyInstallDirectory, "package.json")), "sibling plugin install must write its own package-local manifest.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void ValidateRuntimeLoadsSerializedInstalledPackageManifest()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var packageDirectory = Path.Combine(tempRoot, "packages", "serialized-package");
                Directory.CreateDirectory(packageDirectory);
                File.WriteAllText(Path.Combine(packageDirectory, "Serialized.dll"), "serialized");

                var serializedModules = new PlugHub.Framework.Configuration.ModulesConfiguration
                {
                    SchemaVersion = "1.0",
                    PackageDirectories = new List<string>(),
                    ModuleSources = new List<PlugHub.Framework.Configuration.ModuleSourceConfiguration>(),
                    ConflictPolicy = new PlugHub.Framework.Configuration.ConflictPolicyConfiguration(),
                    Modules = new List<PlugHub.Framework.Configuration.ModuleConfiguration>
                    {
                        new PlugHub.Framework.Configuration.ModuleConfiguration
                        {
                            Id = "serialized-package",
                            Assembly = "Serialized.dll",
                            Type = "Demo.SerializedModule",
                            Enabled = true,
                            Visible = true,
                            Features = new List<PlugHub.Framework.Configuration.FeatureConfiguration>()
                        }
                    }
                };
                File.WriteAllText(Path.Combine(packageDirectory, "package.json"), Json.Serialize(serializedModules));

                var resolved = new PlugHub.Framework.Sources.ModuleSourceResolver().Resolve(
                    tempRoot,
                    new PlugHub.Framework.Configuration.ModulesConfiguration
                    {
                        SchemaVersion = "1.0",
                        PackageDirectories = new List<string> { "packages" },
                        ModuleSources = new List<PlugHub.Framework.Configuration.ModuleSourceConfiguration>(),
                        ConflictPolicy = new PlugHub.Framework.Configuration.ConflictPolicyConfiguration(),
                        Modules = new List<PlugHub.Framework.Configuration.ModuleConfiguration>()
                    });

                Require(resolved.Modules.Modules.Any(module => module.Id == "serialized-package"), "runtime must load installed package manifests after settings serialization rewrites JSON casing.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void ValidateRepositoryInstallFailureDoesNotCreateOrRemovePackages()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var sourceDirectory = Path.Combine(tempRoot, "repository-cache", "broken-package");
                Directory.CreateDirectory(sourceDirectory);
                File.WriteAllText(
                    Path.Combine(sourceDirectory, "package.json"),
                    "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"broken-package\",\"assembly\":\"dist/Missing.dll\",\"type\":\"Demo.BrokenModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");

                var descriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "broken-package",
                    ModuleId = "broken-package",
                    DisplayName = "Broken Package",
                    ManifestPath = Path.Combine(sourceDirectory, "package.json"),
                    SourceDirectory = sourceDirectory,
                    InstallDirectory = Path.Combine(tempRoot, "packages", "broken-package")
                };

                var service = new PlugHub.Framework.Packages.PackageRepositoryService();
                var installResult = service.Install(tempRoot, descriptor);
                Require(!installResult.Success, "installing a package with missing payload must fail.");
                Require(!Directory.Exists(descriptor.InstallDirectory), "failed install must not leave a partial package directory under packages.");

                Directory.CreateDirectory(descriptor.InstallDirectory);
                File.WriteAllText(Path.Combine(descriptor.InstallDirectory, "Existing.dll"), "existing");
                File.WriteAllText(
                    Path.Combine(descriptor.InstallDirectory, "package.json"),
                    "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"broken-package\",\"assembly\":\"Existing.dll\",\"type\":\"Demo.BrokenModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");

                var updateResult = service.Update(tempRoot, descriptor);
                Require(!updateResult.Success, "updating a package with missing payload must fail.");
                Require(File.Exists(Path.Combine(descriptor.InstallDirectory, "Existing.dll")), "failed update must keep the previously installed package files.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void ValidateRevitApiReferenceStrategy()
        {
            var revitProject = ReadText("src/PlugHub.Revit2020/PlugHub.Revit2020.csproj");
            var buildProps = ReadText("build/Directory.Build.props");
            var buildScript = ReadText("scripts/build-revit2020.ps1");
            var workflow = ReadText(".github/workflows/release.yml");

            foreach (var token in new[] { "RevitApiReferenceMode", "Installed", "NuGet", "RevitApiNuGetVersion" })
            {
                Require(revitProject.Contains(token), "Revit project must support installed and NuGet API reference modes: " + token);
                Require(buildProps.Contains(token), "shared build props must expose Revit API reference mode metadata: " + token);
            }

            Require(revitProject.Contains("Autodesk.Revit.SDK"), "CI builds must reference Autodesk.Revit.SDK through NuGet instead of checked-in Revit API DLLs.");
            Require(revitProject.Contains("Condition=\"'$(RevitApiReferenceMode)' == 'NuGet'\""), "NuGet Revit API references must be conditional.");
            Require(revitProject.Contains("Condition=\"'$(RevitApiReferenceMode)' == 'Installed'\""), "installed Revit API references must remain conditional for local builds.");
            Require(revitProject.Contains("EnsureInstalledRevitApiReferences"), "local installed API references must still validate RevitAPI.dll and RevitAPIUI.dll.");

            foreach (var version in new[] { "2018", "2020", "2022", "2024" })
            {
                Require(buildProps.Contains("Revit" + version + "InstallDir"), "shared build props must reserve install-dir metadata for Revit " + version + ".");
            }

            Require(buildProps.Contains("dist\\Revit$(RevitVersion)"), "output path must be version-derived for future Revit adapters.");
            Require(buildScript.Contains("[switch]$UseRevitApiNuGet"), "build script must offer an explicit NuGet API reference mode for CI.");
            Require(buildScript.Contains("/p:RevitApiReferenceMode=NuGet"), "build script must pass NuGet reference mode when requested.");
            Require(workflow.Contains("-UseRevitApiNuGet"), "release workflow must build through NuGet API references.");
            Require(!workflow.Contains("REVIT2020_API_ZIP_BASE64"), "release workflow must not require a secret containing Autodesk Revit API DLLs.");
        }

        private static void ValidateSigningGuidance()
        {
            var signingDoc = ReadText("docs/signing.md");
            var signingScript = ReadText("scripts/sign-revit2020.ps1");
            var workflow = ReadText(".github/workflows/release.yml");

            foreach (var token in new[] { "SignPath Foundation", "self-signed", "signtool", "Thumbprint" })
            {
                Require(signingDoc.Contains(token) || signingScript.Contains(token), "signing guidance must mention: " + token);
            }

            Require(signingScript.Contains("signtool") && signingScript.Contains("/fd SHA256") && signingScript.Contains("/tr"), "signing script must use Authenticode SHA256 signing with timestamp support.");
            Require(workflow.Contains("push:") && workflow.Contains("tags:") && workflow.Contains("\"V*\""), "release workflow must run only for version tag pushes.");
            Require(workflow.Contains("sigstore/cosign-installer") && workflow.Contains("cosign sign-blob") && workflow.Contains("id-token: write"), "release workflow must use keyless cosign blob signing.");
            Require(signingDoc.Contains("Revit API 引用通过 NuGet 仅用于 CI 编译"), "signing guidance must document the NuGet-only CI Revit API reference strategy.");
        }

        private static void ValidateRevitDeploymentConfiguration()
        {
            var outputDirectory = FullPath("dist/Revit2020");
            if (!Directory.Exists(outputDirectory)) return;

            var required = new[]
            {
                "config/sources.json",
                "config/views.json",
                "config/feature-combinations.json",
                "packages/README.md"
            };

            var missing = required
                .Where(path => !File.Exists(Path.Combine(outputDirectory, path.Replace('/', Path.DirectorySeparatorChar))))
                .ToList();
            Require(!missing.Any(), "Revit deployment is missing runtime config files: " + string.Join(", ", missing));

            var staleProject = RemovedSampleProject();
            var stalePaths = new[]
            {
                staleProject + ".dll",
                staleProject + ".pdb",
                "PlugHub.BuiltinModule.dll",
                "PlugHub.BuiltinModule.pdb",
                ("config/" + "modules.json").Replace('/', Path.DirectorySeparatorChar),
                ("config/" + "plugin-sources.json").Replace('/', Path.DirectorySeparatorChar),
                ("packages/" + "dropins").Replace('/', Path.DirectorySeparatorChar),
                ("packages/" + "github").Replace('/', Path.DirectorySeparatorChar),
                ("modules/" + "samples").Replace('/', Path.DirectorySeparatorChar),
                ("modules/" + "dropins").Replace('/', Path.DirectorySeparatorChar),
                "modules"
            };
            var existingStalePaths = stalePaths
                .Where(path => File.Exists(Path.Combine(outputDirectory, path)) || Directory.Exists(Path.Combine(outputDirectory, path)))
                .ToList();
            Require(!existingStalePaths.Any(), "Revit deployment still contains removed module artifacts: " + string.Join(", ", existingStalePaths));
        }

        private static List<string> FeatureIdsForView(List<Dictionary<string, object>> features, Dictionary<string, object> view)
        {
            return features
                .Where(feature => StringValue(feature, "defaultState") == "Visible")
                .Where(feature => !SequenceValue(view, "excludeTags").Intersect(SequenceValue(feature, "tags")).Any())
                .Where(feature => !SequenceValue(view, "excludeCategories").Contains(StringValue(feature, "category")))
                .Where(feature => MatchesViewInclude(feature, view))
                .Where(feature => ArrayValue(view, "groups").Cast<Dictionary<string, object>>().Any(group => MatchesGroup(feature, group)))
                .Select(feature => StringValue(feature, "id"))
                .ToList();
        }

        private static bool MatchesViewInclude(Dictionary<string, object> feature, Dictionary<string, object> view)
        {
            var includeTags = SequenceValue(view, "includeTags");
            var includeCategories = SequenceValue(view, "includeCategories");
            return !includeTags.Any() && !includeCategories.Any()
                || includeTags.Intersect(SequenceValue(feature, "tags")).Any()
                || includeCategories.Contains(StringValue(feature, "category"));
        }

        private static bool MatchesGroup(Dictionary<string, object> feature, Dictionary<string, object> group)
        {
            return StringValue(feature, "group") == StringValue(group, "id")
                || SequenceValue(group, "includeCategories").Contains(StringValue(feature, "category"))
                || SequenceValue(group, "includeTags").Intersect(SequenceValue(feature, "tags")).Any();
        }

        private static Dictionary<string, object> ReadObject(string relativePath)
        {
            return Json.Deserialize<Dictionary<string, object>>(ReadText(relativePath));
        }

        private static string ReadText(string relativePath)
        {
            return File.ReadAllText(FullPath(relativePath));
        }

        private static string ReadAllCSharp(string relativeDirectory)
        {
            return string.Join("\n", Directory.GetFiles(FullPath(relativeDirectory), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        }

        private static string ReadProductionCSharp()
        {
            return string.Join(
                "\n",
                Directory.GetFiles(FullPath("src"), "*.cs", SearchOption.AllDirectories)
                    .Where(path => !RelativePath(path).StartsWith("src" + Path.DirectorySeparatorChar + "PlugHub.StaticValidation", StringComparison.OrdinalIgnoreCase))
                    .Select(File.ReadAllText));
        }

        private static string RemovedSamplesDirectory()
        {
            return "modules/" + "samples";
        }

        private static string RemovedSampleProject()
        {
            return "PlugHub." + "Sample" + "Module";
        }

        private static IEnumerable<string> RemovedWorkspaceGroupNames()
        {
            return new[] { "诊断", "机电工具", "族工具", "入" + "门", "项目" + "流程", "实验", "隐藏" };
        }

        private static IEnumerable<string> RemovedContentTokens()
        {
            return new[] { RemovedSampleProject(), "plughub." + "sample", "place" + "holder", "占" + "位", "入" + "门", "项目" + "流程" };
        }

        private static IEnumerable<Dictionary<string, object>> Modules(Dictionary<string, object> root)
        {
            return ArrayValue(root, "modules").Cast<Dictionary<string, object>>();
        }

        private static IEnumerable<Dictionary<string, object>> AllModules()
        {
            foreach (var module in Modules(ReadObject("config/sources.example.json")))
            {
                yield return module;
            }

            var packagesDirectory = FullPath("packages");
            if (!Directory.Exists(packagesDirectory)) yield break;

            foreach (var file in Directory.GetFiles(packagesDirectory, "package.json", SearchOption.AllDirectories)
                         .Concat(Directory.GetFiles(packagesDirectory, "*.package.json", SearchOption.AllDirectories))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var module in Modules(Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(file))))
                {
                    yield return module;
                }
            }
        }

        private static bool ModuleSourcesUsePackageManifest(Dictionary<string, object> root)
        {
            return ArrayValue(root, "moduleSources")
                .Cast<Dictionary<string, object>>()
                .All(source => StringValue(source, "manifestPath") == "package.json");
        }

        private static IEnumerable<Dictionary<string, object>> Repositories(Dictionary<string, object> root)
        {
            return ArrayValue(root, "repositories").Cast<Dictionary<string, object>>();
        }

        private static IEnumerable<Dictionary<string, object>> Views(Dictionary<string, object> root)
        {
            return ArrayValue(root, "views").Cast<Dictionary<string, object>>();
        }

        private static IEnumerable<Dictionary<string, object>> Presets(Dictionary<string, object> root)
        {
            return ArrayValue(root, "presets").Cast<Dictionary<string, object>>();
        }

        private static IEnumerable<Dictionary<string, object>> Features(Dictionary<string, object> module)
        {
            return ArrayValue(module, "features").Cast<Dictionary<string, object>>();
        }

        private static Dictionary<string, object> ObjectValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value) && value is Dictionary<string, object> result ? result : new Dictionary<string, object>();
        }

        private static ArrayList ArrayValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value) && value is ArrayList result ? result : new ArrayList();
        }

        private static List<string> SequenceValue(Dictionary<string, object> source, string key)
        {
            return ArrayValue(source, key).Cast<object>().Select(value => Convert.ToString(value) ?? string.Empty).ToList();
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static string FullPath(string relativePath)
        {
            return Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string RelativePath(string path)
        {
            return path.Substring(Root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string FindRepositoryRoot()
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(directory))
            {
                if (File.Exists(Path.Combine(directory, "AGENTS.md")) && Directory.Exists(Path.Combine(directory, "src")))
                {
                    return directory!;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new InvalidOperationException("Could not locate repository root.");
        }
    }
}
