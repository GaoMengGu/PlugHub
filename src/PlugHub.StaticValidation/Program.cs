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
                ValidateExternalModuleCommandResolution();
                ValidateFrameworkContainsNoBundledModules();
                ValidatePlugHubV2Specification();
                ValidateSettingsPaneV21Specification();
                ValidateSettingsRibbonCleanupSpecification();
                ValidateBuiltinOnlySpecification();
                ValidateSettingsCreationAndSortingSpecification();
                ValidateDefaultIconSpecification();
                ValidatePackageSourceAndReleaseBehavior();
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
            Require(SequenceValue(modules, "packageDirectories").Contains("packages/dropins"), "drop-in package directory must be configurable.");
            Require(ArrayValue(modules, "moduleSources").Count >= 2, "moduleSources must include localFolder and github examples.");
            Require(ModuleSourcesUsePackageManifest(modules), "module source examples must use package.json as manifestPath.");
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
            Require(ArrayValue(modules, "moduleSources").Count >= 2, "moduleSources must include localFolder and github examples.");
            Require(!SequenceValue(modules, "packageDirectories").Contains(RemovedSamplesDirectory()), "sample modules must be removed from built-in runtime config.");
            Require(SequenceValue(modules, "packageDirectories").Contains("packages/dropins"), "drop-in package folder must be available for automatic package loading.");

            var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            Require(configurationModels.Contains("DisplayName"), "modules config model must support displayName.");
            Require(configurationModels.Contains("IconPath"), "modules config model must support iconPath.");
            var modulesText = ReadText("config/sources.example.json");
            Require(modulesText.Contains("\"type\": \"github\""), "modules config must include a github module source example.");
            Require(modulesText.Contains("\"autoUpdate\""), "github module source example must expose autoUpdate.");
            Require(modulesText.Contains("\"repository\": \"GaoMengGu/PlugHub_Packages\""), "default github source must point at GaoMengGu/PlugHub_Packages.");
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

            foreach (var token in new[] { "class FrameworkSettingsWindow", ": Window", "TabControl", "DataGrid", "BuildPluginPackagesTab", "BuildFeaturesTab", "BuildGroupsTab", "BuildSourcesTab", "BuildDiagnosticsTab", "SourceRow", "GroupRow", "ReloadFromDisk", "AutoUpdate", "ContextMenu", "DragDrop", "Microsoft.Win32.OpenFileDialog" })
            {
                Require(settingsWindow.Contains(token), "WPF settings UI token missing: " + token);
            }

            foreach (var forbidden in new[] { "FrameworkRuntimeState.Refresh", "UpdateGitHubCache", "ProcessStartInfo", "Assembly.LoadFrom" })
            {
                Require(!settingsWindow.Contains(forbidden), "settings window must only save configuration and must not run runtime work: " + forbidden);
            }

            Require(statusWindow.Contains("class FrameworkStatusWindow") && statusWindow.Contains(": Window"), "status and feature fallback UI must use a WPF status window.");
            foreach (var token in new[] { "ShowRefreshResult", "ShowRuntimeStatus", "ShowDiagnostics", "showDiagnostics" })
            {
                Require(statusWindow.Contains(token), "status window must separate refresh, status, and diagnostics concerns: " + token);
            }
            Require(configurationModels.Contains("bool AutoUpdate"), "module source configuration must expose autoUpdate.");
            Require(sourceResolver.Contains("AddPackageDirectoryModules"), "package directories must be scanned for drop-in package manifests.");
            Require(sourceResolver.Contains("FindModuleManifests"), "module directory resolver must discover manifests automatically.");
            Require(sourceResolver.Contains("\"package.json\"") && sourceResolver.Contains("\"*.package.json\""), "module directory resolver must discover package.json and DLL-adjacent *.package.json manifests.");
            Require(sourceResolver.Contains("UpdateGitHubCache") && sourceResolver.Contains("ProcessStartInfo"), "github module sources must support clone/fetch into a local cache.");
            Require(sourceResolver.Contains("AddGitHubModules"), "github module sources must be resolved from a local cache directory.");
            Require(sourceResolver.Contains("packages/github"), "github source resolver must use a predictable local cache folder.");
            Require(!revitProject.Contains("System.Windows.Forms") && !revitProject.Contains("WindowsFormsIntegration"), "Revit adapter should not reference WinForms after moving settings and feature UI to WPF.");
            Require(!revitProject.Contains("PlugHubModuleFiles"), "Revit build must not depend on a source modules folder.");
            Require(revitProject.Contains("packages\\dropins"), "Revit build must create a runtime package drop-in folder.");
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

        private static void ValidatePackageSourceAndReleaseBehavior()
        {
            var modulesText = ReadText("config/sources.example.json");
            var settingsWindow = ReadText("src/PlugHub.Revit2020/FrameworkSettingsWindow.cs");
            var sourceResolver = ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var workflow = ReadText(".github/workflows/release.yml");
            var buildScript = ReadText("scripts/build-revit2020.ps1");
            var readme = ReadText("README.md");

            Require(modulesText.Contains("\"repository\": \"GaoMengGu/PlugHub_Packages\""), "default github source must point at GaoMengGu/PlugHub_Packages.");
            Require(modulesText.Contains("packages/github/GaoMengGu_PlugHub_Packages"), "default github cache path must use PlugHub_Packages.");
            Require(!modulesText.Contains("GaoMengGu/PlugHub_Modules"), "default github source must not point at PlugHub_Modules.");
            Require(settingsWindow.Contains("GaoMengGu/PlugHub_Packages"), "settings source creation must default to PlugHub_Packages.");

            Require(sourceResolver.Contains("AddGitHubPackageManifests"), "github package sources must scan package manifests instead of only reading repository-root package.json.");
            Require(sourceResolver.Contains("FindModuleManifests(sourceDirectory)") && sourceResolver.Contains("ignoreNonPlugHubManifest"), "github package scanning must ignore non-PlugHub package.json files.");
            Require(sourceResolver.Contains("RemoteHasChanged") && sourceResolver.Contains("ls-remote") && sourceResolver.Contains("rev-parse HEAD"), "github updates must skip pull when the remote ref has not changed.");
            Require(sourceResolver.Contains("--filter=blob:none") && sourceResolver.Contains("sparse-checkout"), "github clone must avoid downloading the whole repository when possible.");

            Require(workflow.Contains("-UseRelativeAddinAssembly"), "release workflow must build a package with relative addin assembly path.");
            Require(buildScript.Contains("[switch]$UseRelativeAddinAssembly") && buildScript.Contains("PlugHub.Revit2020.dll"), "build script must support relative release addin assembly paths.");
            Require(workflow.Contains("*.pdb") && workflow.Contains("*.sigstore.json") && !workflow.Contains("Compress-Archive -Path \"dist\\Revit2020\\*\""), "release zip must exclude pdb and sigstore files.");

            Require(readme.Contains("个人使用") && readme.Contains("不得商用"), "README must state the non-commercial personal-use license restriction.");
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
                "packages/dropins/README.md"
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
