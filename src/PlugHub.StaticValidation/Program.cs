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
                ValidateLayering();
                ValidateConfiguration();
                ValidateViewCompositionExamples();
                ValidateComposerShape();
                ValidateCoreContracts();
                ValidateRevitRibbonAdapter();
                ValidateRuntimeConfigurationLoader();
                ValidateConfiguredBuiltinModuleTypes();
                ValidatePlugHubV2Specification();
                ValidateSettingsPaneV21Specification();
                ValidateSettingsRibbonCleanupSpecification();
                ValidateBuiltinOnlySpecification();
                ValidateSettingsCreationAndSortingSpecification();
                ValidateDefaultIconSpecification();
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
                "src/PlugHub.BuiltinModule/PlugHub.BuiltinModule.csproj",
                "src/PlugHub.BuiltinModule/BuiltinModule.cs",
                "src/PlugHub.BuiltinModule/Commands/BatchAddMaterialParameterCommand.cs",
                "src/PlugHub.BuiltinModule/Commands/DuctPreferredJunctionSwitcherCommand.cs",
                "config/modules.example.json",
                "config/views.example.json",
                "config/feature-combinations.example.json",
                "config/schemas/modules.schema.json",
                "config/schemas/views.schema.json",
                "modules/dropins/README.md",
                "docs/README.md",
                "docs/agent-handbook.md",
                "docs/module-contract.md",
                "docs/verification.md",
                "docs/review.md"
            };

            var missing = required.Where(path => !File.Exists(FullPath(path))).ToList();
            Require(!missing.Any(), "missing required files: " + string.Join(", ", missing));
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
            var modules = ReadObject("config/modules.example.json");
            var views = ReadObject("config/views.example.json");
            var presets = ReadObject("config/feature-combinations.example.json");

            Require(StringValue(modules, "schemaVersion") == "1.0", "modules schemaVersion must be 1.0.");
            Require(StringValue(views, "defaultView") == "workspace", "default view must be workspace.");
            Require(Views(views).Count() == 1, "PlugHub must expose exactly one workspace view.");
            Require(!SequenceValue(modules, "moduleDirectories").Contains(RemovedSamplesDirectory()), "sample modules must be removed from moduleDirectories.");
            Require(SequenceValue(modules, "moduleDirectories").Contains("modules/dropins"), "drop-in modules directory must be configurable.");
            Require(ArrayValue(modules, "moduleSources").Count >= 2, "moduleSources must include localFolder and github examples.");
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

            var allFeatures = AllModules().SelectMany(Features).ToList();
            Require(allFeatures.Any(), "modules config must define at least one feature.");
            foreach (var view in Views(views))
            {
                Require(ArrayValue(view, "groups").Count > 0, "view must contain groups: " + StringValue(view, "id"));
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
            Require(workspace.Count == 2, "workspace should expose only the two built-in features.");
            Require(workspace.Contains("plughub.builtin.duct-tools.switch-preferred-junction"), "workspace should include duct tool.");
            Require(workspace.Contains("plughub.builtin.family-tools.batch-add-material-parameter"), "workspace should include family tool.");
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

        private static void ValidateConfiguredBuiltinModuleTypes()
        {
            var modules = ReadObject("config/modules.example.json");
            var builtinModules = Modules(modules)
                .Where(module => StringValue(module, "assembly") == "PlugHub.BuiltinModule.dll")
                .ToList();

            Require(builtinModules.Count == 2, "Builtin module config should expose the two migrated Revit API plugins.");

            var builtinText = ReadAllCSharp("src/PlugHub.BuiltinModule");
            var missingModules = builtinModules
                .Select(module => StringValue(module, "type").Split('.').Last())
                .Where(type => !builtinText.Contains("class " + type))
                .ToList();
            Require(!missingModules.Any(), "configured Builtin module types are missing: " + string.Join(", ", missingModules));

            var commandTypes = builtinModules
                .SelectMany(Features)
                .Select(feature => StringValue(feature, "commandType"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Require(commandTypes.Count == 2, "Builtin module features should declare two command types.");

            var missingCommands = commandTypes
                .Select(type => type.Split('.').Last())
                .Where(type => !builtinText.Contains("class " + type))
                .ToList();
            Require(!missingCommands.Any(), "configured Builtin command types are missing: " + string.Join(", ", missingCommands));

            foreach (var feature in builtinModules.SelectMany(Features))
            {
                Require(StringValue(feature, "commandAssembly") == "PlugHub.BuiltinModule.dll", "Builtin feature commandAssembly must target PlugHub.BuiltinModule.dll.");
                Require(!string.IsNullOrWhiteSpace(StringValue(feature, "commandKey")), "Builtin feature commandKey is required.");
                Require(!string.IsNullOrWhiteSpace(StringValue(feature, "commandType")), "Builtin feature commandType is required.");
            }
        }

        private static void ValidatePlugHubV2Specification()
        {
            Require(File.Exists(FullPath("PlugHub.sln")), "PlugHub.sln is required.");
            Require(File.Exists(FullPath("src/PlugHub.Contracts/PlugHub.Contracts.csproj")), "PlugHub.Contracts project is required.");
            var legacySolution = "Revit" + "Tool.sln";
            Require(!File.Exists(FullPath(legacySolution)), "legacy solution should be removed after rename.");

            var modules = ReadObject("config/modules.example.json");
            var views = ReadObject("config/views.example.json");

            Require(StringValue(views, "defaultView") == "workspace", "PlugHub must use the single workspace view.");
            Require(Views(views).Count() == 1, "PlugHub must expose exactly one workspace view.");
            Require(ArrayValue(modules, "moduleSources").Count >= 2, "moduleSources must include localFolder and github examples.");
            Require(!SequenceValue(modules, "moduleDirectories").Contains(RemovedSamplesDirectory()), "sample modules must be removed from built-in runtime config.");
            Require(SequenceValue(modules, "moduleDirectories").Contains("modules/dropins"), "drop-in folder must be available for automatic module loading.");

            var modulesText = ReadText("config/modules.example.json");
            Require(modulesText.Contains("\"displayName\""), "modules config must support displayName.");
            Require(modulesText.Contains("\"iconPath\""), "modules config must support iconPath.");
            Require(modulesText.Contains("\"type\": \"github\""), "modules config must include a github module source example.");
            Require(modulesText.Contains("\"autoUpdate\""), "github module source example must expose autoUpdate.");

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

            foreach (var token in new[] { "class FrameworkSettingsWindow", ": Window", "TabControl", "DataGrid", "BuildModulesTab", "BuildFeaturesTab", "BuildSourcesTab", "BuildDiagnosticsTab", "SourceRow", "ReloadFromDisk", "AutoUpdate", "ContextMenu", "DragDrop", "Microsoft.Win32.OpenFileDialog" })
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
            Require(sourceResolver.Contains("AddModuleDirectoryModules"), "module directories must be scanned for drop-in module manifests.");
            Require(sourceResolver.Contains("FindModuleManifests"), "module directory resolver must discover manifests automatically.");
            Require(sourceResolver.Contains("UpdateGitHubCache") && sourceResolver.Contains("ProcessStartInfo"), "github module sources must support clone/fetch into a local cache.");
            Require(sourceResolver.Contains("AddGitHubModules"), "github module sources must be resolved from a local cache directory.");
            Require(sourceResolver.Contains("modules/github"), "github source resolver must use a predictable local cache folder.");
            Require(!revitProject.Contains("System.Windows.Forms") && !revitProject.Contains("WindowsFormsIntegration"), "Revit adapter should not reference WinForms after moving settings and feature UI to WPF.");
            Require(revitProject.Contains("PlugHubModuleFiles"), "Revit build must copy independent module manifests into the deployment.");
        }

        private static void ValidateSettingsRibbonCleanupSpecification()
        {
            var settingsWindow = ReadText("src/PlugHub.Revit2020/FrameworkSettingsWindow.cs");
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var addinTemplate = ReadText("manifests/PlugHub.addin.template");
            var buildProps = ReadText("build/Directory.Build.props");
            var views = ReadObject("config/views.example.json");
            var rootModules = ReadObject("config/modules.example.json");

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
            foreach (var expected in new[] { "机电风管", "族批处理" })
            {
                Require(groupNames.Contains(expected), "workspace group name must match module display name: " + expected);
            }

            foreach (var removed in RemovedWorkspaceGroupNames())
            {
                Require(!groupNames.Contains(removed), "workspace group should be removed or renamed: " + removed);
            }

            Require(Modules(rootModules).Any(module => StringValue(module, "displayName") == "机电风管"), "duct module displayName must remain 机电风管.");
            Require(Modules(rootModules).Any(module => StringValue(module, "displayName") == "族批处理"), "family module displayName must remain 族批处理.");
        }

        private static void ValidateBuiltinOnlySpecification()
        {
            var modules = AllModules().ToList();
            var allText = ReadProductionCSharp() + "\n" + ReadText("PlugHub.sln") + "\n" + ReadText("PlugHub.slnx") + "\n" + ReadText("config/modules.example.json") + "\n" + ReadText("config/views.example.json");

            Require(modules.Count == 2, "runtime configuration must expose only two built-in modules.");
            Require(modules.SelectMany(Features).Count() == 2, "runtime configuration must expose only two built-in features.");
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

            foreach (var token in new[] { "AddModule", "AddFeature", "CreateModule", "CreateFeature", "所属模块", "ModuleIdsForFeatureRows" })
            {
                Require(settingsWindow.Contains(token), "settings must support creating modules/features and choosing the owning module: " + token);
            }

            Require(!settingsWindow.Contains("TextColumn(nameof(ModuleRow.Order)") && !settingsWindow.Contains("TextColumn(nameof(FeatureRow.Order)"), "settings must not expose raw numeric order columns.");
            Require(settingsWindow.Contains("PositionText") && settingsWindow.Contains("RefreshModulePositions") && settingsWindow.Contains("RefreshFeaturePositions"), "settings must show human-readable position text and maintain drag/up-down sorting.");
        }

        private static void ValidateDefaultIconSpecification()
        {
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var iconProvider = ReadText("src/PlugHub.Revit2020/DefaultRibbonIconProvider.cs");
            var modulesText = ReadText("config/modules.example.json");

            Require(ribbonBuilder.Contains("DefaultRibbonIconProvider") && ribbonBuilder.Contains("CreateSmallIcon") && ribbonBuilder.Contains("CreateLargeIcon"), "Ribbon builder must apply built-in default small/large icons.");
            Require(iconProvider.Contains("CreateSmallIcon") && iconProvider.Contains("CreateLargeIcon"), "default icon provider must expose small and large icon factories.");
            Require(modulesText.Contains("\"iconPath\": \"\""), "builtin feature config can rely on built-in default icons when iconPath is empty.");
        }

        private static void ValidateRevitDeploymentConfiguration()
        {
            var outputDirectory = FullPath("dist/Revit2020");
            if (!Directory.Exists(outputDirectory)) return;

            var required = new[]
            {
                "config/modules.json",
                "config/views.json",
                "config/feature-combinations.json",
                "modules/dropins/README.md"
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
                ("modules/" + "samples").Replace('/', Path.DirectorySeparatorChar)
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
            foreach (var module in Modules(ReadObject("config/modules.example.json")))
            {
                yield return module;
            }

            var modulesDirectory = FullPath("modules");
            if (!Directory.Exists(modulesDirectory)) yield break;

            foreach (var file in Directory.GetFiles(modulesDirectory, "modules.json", SearchOption.AllDirectories))
            {
                foreach (var module in Modules(Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(file))))
                {
                    yield return module;
                }
            }
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
