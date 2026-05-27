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
                ValidateSampleModuleReferences();
                ValidateConfiguration();
                ValidateViewCompositionExamples();
                ValidateComposerShape();
                ValidateCoreContracts();
                ValidateRevitRibbonAdapter();
                ValidateRuntimeConfigurationLoader();
                ValidateConfiguredSampleModuleTypes();
                ValidateConfiguredBuiltinModuleTypes();
                ValidatePlugHubV2Specification();
                ValidateSettingsPaneV21Specification();
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
                "src/PlugHub.SampleModule/PlugHub.SampleModule.csproj",
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
                "src/PlugHub.BuiltinModule/PlugHub.BuiltinModule.csproj",
                "src/PlugHub.BuiltinModule/BuiltinModule.cs",
                "src/PlugHub.BuiltinModule/Commands/BatchAddMaterialParameterCommand.cs",
                "src/PlugHub.BuiltinModule/Commands/DuctPreferredJunctionSwitcherCommand.cs",
                "src/PlugHub.SampleModule/SampleModule.cs",
                "config/modules.example.json",
                "config/views.example.json",
                "config/feature-combinations.example.json",
                "config/schemas/modules.schema.json",
                "config/schemas/views.schema.json",
                "modules/samples/modules.json",
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
            foreach (var directory in new[] { "src/PlugHub.Contracts", "src/PlugHub.Framework", "src/PlugHub.SampleModule" })
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

        private static void ValidateSampleModuleReferences()
        {
            var csproj = ReadText("src/PlugHub.SampleModule/PlugHub.SampleModule.csproj");
            Require(csproj.Contains("PlugHub.Contracts"), "SampleModule must reference Contracts.");
            Require(!csproj.Contains("PlugHub.Framework"), "SampleModule must not reference Framework.");
        }

        private static void ValidateConfiguration()
        {
            var modules = ReadObject("config/modules.example.json");
            var views = ReadObject("config/views.example.json");
            var presets = ReadObject("config/feature-combinations.example.json");

            Require(StringValue(modules, "schemaVersion") == "1.0", "modules schemaVersion must be 1.0.");
            Require(StringValue(views, "defaultView") == "workspace", "default view must be workspace.");
            Require(Views(views).Count() == 1, "PlugHub must expose exactly one workspace view.");
            Require(SequenceValue(modules, "moduleDirectories").Contains("modules/samples"), "sample modules must be loaded from an independent module directory.");
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
            Require(workspace.Contains("plughub.sample.navigation.open-panel"), "workspace should include open-panel.");
            Require(workspace.Contains("plughub.sample.navigation.show-diagnostics"), "workspace should include diagnostics.");
            Require(workspace.Contains("plughub.sample.project-template.overview"), "workspace should include project overview.");
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

        private static void ValidateConfiguredSampleModuleTypes()
        {
            var rootModules = ReadObject("config/modules.example.json");
            Require(!Modules(rootModules).Any(module => StringValue(module, "assembly") == "PlugHub.SampleModule.dll"), "sample and placeholder modules must not live in the root PlugHub config.");

            var sampleModules = ReadObject("modules/samples/modules.json");
            Require(Modules(sampleModules).Count() == 4, "sample module manifest should expose the independent sample, hidden, and placeholder modules.");

            var configuredTypes = Modules(sampleModules)
                .Where(module => StringValue(module, "assembly") == "PlugHub.SampleModule.dll")
                .Select(module => StringValue(module, "type").Split('.').Last())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var sampleText = ReadAllCSharp("src/PlugHub.SampleModule");
            var missing = configuredTypes.Where(type => !sampleText.Contains("class " + type)).ToList();
            Require(!missing.Any(), "configured sample module types are missing: " + string.Join(", ", missing));
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
            Require(SequenceValue(modules, "moduleDirectories").Contains("modules/samples"), "sample modules must be independent from built-in module config.");
            Require(SequenceValue(modules, "moduleDirectories").Contains("modules/dropins"), "drop-in folder must be available for automatic module loading.");

            var modulesText = ReadText("config/modules.example.json");
            Require(modulesText.Contains("\"displayName\""), "modules config must support displayName.");
            Require(modulesText.Contains("\"iconPath\""), "modules config must support iconPath.");
            Require(modulesText.Contains("\"type\": \"github\""), "modules config must include a github module source example.");
            Require(modulesText.Contains("\"autoUpdate\""), "github module source example must expose autoUpdate.");

            var revitText = ReadAllCSharp("src/PlugHub.Revit2020");
            Require(revitText.Contains("DockablePaneProviderData"), "settings must use a DockablePane provider.");
            Require(revitText.Contains("FeatureExecutionGate"), "feature execution must be gated by latest runtime configuration.");
        }

        private static void ValidateSettingsPaneV21Specification()
        {
            var settingsForm = ReadText("src/PlugHub.Revit2020/FrameworkSettingsForm.cs");
            var settingsPane = ReadAllCSharp("src/PlugHub.Revit2020");
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var sourceResolver = ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            var revitProject = ReadText("src/PlugHub.Revit2020/PlugHub.Revit2020.csproj");

            Require(!settingsForm.Contains("Action closeAction"), "settings form must not own dockable pane close actions.");
            Require(!settingsForm.Contains("HandleClose"), "settings form must not close or hide its host dockable pane.");
            Require(!settingsForm.Contains("Text = \"关闭\""), "settings form must not expose an in-pane close button.");
            Require(!settingsForm.Contains("closeButton.Click += (sender, args) => Close();"), "settings close button must not directly close the hosted form.");
            Require(!settingsPane.Contains("IExternalEventHandler") && !settingsPane.Contains("ExternalEvent.Create"), "settings pane must not hide itself through in-pane external events.");
            Require(ReadText("src/PlugHub.Revit2020/FrameworkSettingsCommand.cs").Contains("pane.IsShown()") && ReadText("src/PlugHub.Revit2020/FrameworkSettingsCommand.cs").Contains("pane.Hide()"), "settings ribbon command must toggle the dockable pane from a Revit command context.");
            Require(ribbonBuilder.Contains("LoadFeatureIcon") && ribbonBuilder.Contains("LargeImage"), "configured feature icons must be applied to Revit ribbon buttons.");

            foreach (var token in new[] { "TabControl", "BuildModulesTab", "BuildFeaturesTab", "BuildSourcesTab", "BuildDiagnosticsTab", "SourceRow", "ReloadFromDisk", "AutoUpdate" })
            {
                Require(settingsForm.Contains(token), "settings V2.1 UI token missing: " + token);
            }

            Require(configurationModels.Contains("bool AutoUpdate"), "module source configuration must expose autoUpdate.");
            Require(sourceResolver.Contains("AddModuleDirectoryModules"), "module directories must be scanned for drop-in module manifests.");
            Require(sourceResolver.Contains("FindModuleManifests"), "module directory resolver must discover manifests automatically.");
            Require(sourceResolver.Contains("UpdateGitHubCache") && sourceResolver.Contains("ProcessStartInfo"), "github module sources must support clone/fetch into a local cache.");
            Require(sourceResolver.Contains("AddGitHubModules"), "github module sources must be resolved from a local cache directory.");
            Require(sourceResolver.Contains("modules/github"), "github source resolver must use a predictable local cache folder.");
            Require(revitProject.Contains("PlugHubModuleFiles"), "Revit build must copy independent module manifests into the deployment.");
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
                "modules/samples/modules.json",
                "modules/dropins/README.md",
                "modules/samples/PlugHub.SampleModule.dll"
            };

            var missing = required
                .Where(path => !File.Exists(Path.Combine(outputDirectory, path.Replace('/', Path.DirectorySeparatorChar))))
                .ToList();
            Require(!missing.Any(), "Revit deployment is missing runtime config files: " + string.Join(", ", missing));
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
