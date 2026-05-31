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
        private const int ValidationFailedExitCode = 1;
        private const int ArgumentErrorExitCode = 2;
        private const int ReportWriteErrorExitCode = 3;

        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static readonly string Root = FindRepositoryRoot();

        private static int Main(string[] args)
        {
            ValidationArguments arguments;
            try
            {
                arguments = ParseArguments(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage();
                return ArgumentErrorExitCode;
            }

            try
            {
                ValidateRequiredFiles();
                ValidateDocumentationStructure();
                ValidateLayering();
                ValidateConfiguration();
                ValidateViewCompositionExamples();
                ValidateComposerShape();
                ValidateCoreContracts();
                ValidateContractsMultiTargetReadiness();
                ValidatePackageManifestSchemaAndCompatibility();
                ValidateRevitRibbonAdapter();
                ValidateRuntimeRoutingSpecification();
                ValidateRevit2025AlcReadinessSpecification();
                ValidateManifestAuthoritativeDiscoverySpecification();
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
                ValidatePendingPackageOperationStoreBehavior();
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

                var report = new PlugHub.StaticValidation.Validation.ValidationReport();
                if (!TryWriteReports(arguments, report))
                {
                    return ReportWriteErrorExitCode;
                }

                Console.WriteLine(
                    $"passed: modules={modules.Count}, features={featureCount}, views={Views(views).Count()}, presets={Presets(presets).Count()}");
                return 0;
            }
            catch (Exception ex)
            {
                var report = new PlugHub.StaticValidation.Validation.ValidationReport();
                report.Error("PH-VALIDATION-FAILED", string.Empty, ex.Message, "Read the failing validation message and update the referenced PlugHub file.");
                Console.Error.WriteLine("validation failed: " + ex.Message);
                TryWriteReports(arguments, report);
                return ValidationFailedExitCode;
            }
        }

        private static ValidationArguments ParseArguments(string[] args)
        {
            var result = new ValidationArguments();
            for (var index = 0; index < args.Length; index++)
            {
                if (args[index] == "--report-json")
                {
                    result.JsonReportPath = ReadReportPath(args, ref index, args[index]);
                    continue;
                }

                if (args[index] == "--report-html")
                {
                    result.HtmlReportPath = ReadReportPath(args, ref index, args[index]);
                    continue;
                }

                throw new ArgumentException("Unknown argument: " + args[index]);
            }

            return result;
        }

        private static string ReadReportPath(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException(option + " requires a path.");
            }

            index++;
            return args[index];
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Usage: PlugHub.StaticValidation [--report-json <path>] [--report-html <path>]");
        }

        private static bool TryWriteReports(ValidationArguments arguments, PlugHub.StaticValidation.Validation.ValidationReport report)
        {
            var success = true;
            if (!string.IsNullOrWhiteSpace(arguments.JsonReportPath))
            {
                success &= TryWriteReport("--report-json", () =>
                    PlugHub.StaticValidation.Validation.ValidationReportWriter.WriteJson(arguments.JsonReportPath, report));
            }

            if (!string.IsNullOrWhiteSpace(arguments.HtmlReportPath))
            {
                success &= TryWriteReport("--report-html", () =>
                    PlugHub.StaticValidation.Validation.ValidationReportWriter.WriteHtml(arguments.HtmlReportPath, report));
            }

            return success;
        }

        private static bool TryWriteReport(string option, Action write)
        {
            try
            {
                write();
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("report write failed (" + option + "): " + ex.Message);
                return false;
            }
        }

        private sealed class ValidationArguments
        {
            public string JsonReportPath { get; set; } = string.Empty;
            public string HtmlReportPath { get; set; } = string.Empty;
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
                "src/PlugHub.StaticValidation/Validation/ValidationSeverity.cs",
                "src/PlugHub.StaticValidation/Validation/ValidationIssue.cs",
                "src/PlugHub.StaticValidation/Validation/ValidationReport.cs",
                "src/PlugHub.StaticValidation/Validation/ValidationReportWriter.cs",
                "src/PlugHub.StaticValidation/Validation/PackageManifestValidation.cs",
                "src/PlugHub.Contracts/Modules/IPlugHubModule.cs",
                "src/PlugHub.Contracts/Loading/AlcLoadRules.cs",
                "src/PlugHub.Framework/Composition/FeatureViewComposer.cs",
                "src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs",
                "src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs",
                "src/PlugHub.Framework/Runtime/FrameworkRuntime.cs",
                "src/PlugHub.Framework/Registry/FeatureRegistry.cs",
                "src/PlugHub.Framework/Packages/RepositoryCredentialService.cs",
                "src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs",
                "src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs",
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
                "config/schemas/package.schema.json",
                "docs/README.md",
                "docs/project-overview.md",
                "docs/architecture.md",
                "docs/development.md",
                "docs/signing.md"
            };

            var missing = required.Where(path => !File.Exists(FullPath(path))).ToList();
            Require(!missing.Any(), "missing required files: " + string.Join(", ", missing));
            var validationProgram = ReadText("src/PlugHub.StaticValidation/Program.cs");
            Require(validationProgram.Contains("string[] args"), "Static validation entrypoint must accept command-line arguments.");
            Require(validationProgram.Contains("--report-json") && validationProgram.Contains("--report-html"), "Static validation must support JSON and HTML report arguments.");
            Require(validationProgram.Contains("Validation" + "Arguments Parse" + "Arguments("), "Static validation arguments must be parsed centrally.");
            Require(validationProgram.Contains("\"" + "Usage:"), "Invalid static validation arguments must print usage.");
            Require(validationProgram.Contains("Argument" + "Error" + "Exit" + "Code ="), "Invalid static validation arguments must return a fixed error code.");
            Require(validationProgram.Contains("bool Try" + "Write" + "Reports("), "Validation and report writing failures must be handled separately.");
            var reportWriter = ReadText("src/PlugHub.StaticValidation/Validation/ValidationReportWriter.cs");
            Require(reportWriter.Contains("issues"), "JSON validation report must emit an issues field.");
            Require(reportWriter.Contains("Encoding.UTF8"), "JSON validation report must be written with UTF-8 encoding.");
            foreach (var field in new[] { "severity", "code", "file", "message", "suggestion" })
            {
                Require(reportWriter.Contains(field), "Validation report issue fields must use lowercase names: " + field);
                Require(reportWriter.Contains("<th>" + field + "</th>"), "HTML validation report must include a table header for " + field + ".");
            }
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

        private static void ValidateContractsMultiTargetReadiness()
        {
            var contractsProject = ReadText("src/PlugHub.Contracts/PlugHub.Contracts.csproj");
            var frameworkProject = ReadText("src/PlugHub.Framework/PlugHub.Framework.csproj");
            var development = ReadText("docs/development.md");

            Require(contractsProject.Contains("<TargetFrameworks>net48;netstandard2.1</TargetFrameworks>"), "PlugHub.Contracts must target net48 and netstandard2.1 for future net8 adapters.");
            Require(!ReadAllCSharp("src/PlugHub.Contracts").Contains("System.Web"), "PlugHub.Contracts must stay free of net48-only System.Web dependencies.");
            Require(frameworkProject.Contains("<TargetFramework>net48</TargetFramework>") && frameworkProject.Contains("System.Web.Extensions"), "PlugHub.Framework remains net48 until its JSON serializer boundary is replaced.");
            Require(development.Contains("PlugHub.Contracts") && development.Contains("netstandard2.1") && development.Contains("System.Web.Script.Serialization"), "development docs must describe the Contracts multi-target boundary and Framework blocker.");
        }

        private static void ValidatePackageManifestSchemaAndCompatibility()
        {
            var schema = ReadText("config/schemas/package.schema.json");
            Require(schema.Contains("\"revitVersions\""), "package schema must define revitVersions.");
            Require(schema.Contains("\"frameworkVersionRange\""), "package schema must define frameworkVersionRange.");
            Require(schema.Contains("\"sha256\""), "package schema must define sha256.");
            Require(schema.Contains("\"signature\""), "package schema must define signature.");

            var packageValidation = ReadText("src/PlugHub.StaticValidation/Validation/PackageManifestValidation.cs");
            Require(packageValidation.Contains("IEnumerable") && packageValidation.Contains("Cast<object>().Any()") && !packageValidation.Contains("object[]"), "package manifest validation must accept JavaScriptSerializer ArrayList modules.");

            var models = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            Require(models.Contains("RevitVersions") && models.Contains("FrameworkVersionRange"), "configuration models must expose package compatibility fields.");

            var sourceResolver = ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            Require(sourceResolver.Contains("PushRootCompatibilityToModules") && sourceResolver.Contains("module.RevitVersions = new List<string>(modules.RevitVersions)") && sourceResolver.Contains("module.FrameworkVersionRange = modules.FrameworkVersionRange"), "module source resolver must push root compatibility fields down to modules.");

            var configurationLoader = ReadText("src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs");
            foreach (var token in new[] { "Version = modules.Version", "RevitVersions = new List<string>(modules.RevitVersions", "FrameworkVersionRange = modules.FrameworkVersionRange", "Sha256 = modules.Sha256", "Signature = modules.Signature", "RevitVersions = new List<string>(module.RevitVersions", "FrameworkVersionRange = module.FrameworkVersionRange" })
            {
                Require(configurationLoader.Contains(token), "framework configuration loader must preserve package compatibility fields: " + token);
            }

            var discovery = ReadText("src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs");
            Require(discovery.Contains("IsCompatibleWithRuntime"), "module discovery must skip packages incompatible with the active runtime.");
            Require(discovery.Contains("RT-MODULE-COMPATIBILITY") && discovery.Contains("continue;"), "module discovery must warn and skip packages incompatible with the active runtime.");
            Require(discovery.Contains("CurrentRevitVersion") && discovery.Contains(".Trim()") && discovery.Contains("StringComparer.OrdinalIgnoreCase"), "module discovery must normalize declared Revit versions before comparing with the current runtime.");
            Require(discovery.Contains("FrameworkVersionRange") && discovery.Contains("metadata"), "frameworkVersionRange must be explicitly preserved as metadata and not treated as runtime compatibility logic yet.");

            var packageRepositoryService = ReadText("src/PlugHub.Framework/Packages/PackageRepositoryService.cs");
            Require(packageRepositoryService.Contains("\"revitVersions\"") && packageRepositoryService.Contains("\"frameworkVersionRange\""), "single-module installed manifests must preserve root compatibility metadata.");
            Require(!packageRepositoryService.Contains("CopyOptionalManifestValue(root, manifest, \"sha256\")") && !packageRepositoryService.Contains("CopyOptionalManifestValue(root, manifest, \"signature\")"), "single-module installed manifests must not copy root sha256 or signature after rewriting the manifest.");

            ValidateRuntimeAcceptsWhitespacePaddedRevitVersion();
            ValidateRuntimeSkipsPresetOverriddenIncompatiblePackage();
            ValidateInstalledRepositoryPackagePreservesCompatibilityAndSkips();
        }

        private static void ValidateRuntimeAcceptsWhitespacePaddedRevitVersion()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var configDirectory = Path.Combine(tempRoot, "config");
                var packageDirectory = Path.Combine(tempRoot, "packages", "compatible-package");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(packageDirectory);

                WriteRuntimeConfig(configDirectory);
                File.WriteAllText(
                    Path.Combine(packageDirectory, "package.json"),
                    "{\"schemaVersion\":\"1.0\",\"revitVersions\":[\" 2020 \",\"\"],\"frameworkVersionRange\":\">=1.2\",\"modules\":[{\"id\":\"compatible-package\",\"assembly\":\"Compatible.dll\",\"type\":\"Demo.CompatibleModule\",\"enabled\":true,\"visible\":true,\"features\":[{\"id\":\"compatible-feature\",\"displayName\":\"Compatible\",\"category\":\"test\",\"group\":\"test\",\"defaultState\":\"Visible\"}]}]}");

                var snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(configDirectory);
                Require(snapshot.Features.Any(feature => feature.ModuleId == "compatible-package"), "runtime must accept whitespace-padded Revit 2020 compatibility declarations.");
                Require(!snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == "RT-MODULE-COMPATIBILITY" && diagnostic.ModuleId == "compatible-package"), "runtime must not warn for whitespace-padded Revit 2020 compatibility declarations.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void ValidateRuntimeSkipsPresetOverriddenIncompatiblePackage()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var configDirectory = Path.Combine(tempRoot, "config");
                var packageDirectory = Path.Combine(tempRoot, "packages", "incompatible-package");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(packageDirectory);

                WriteRuntimeConfig(configDirectory, "incompatible-package");
                File.WriteAllText(
                    Path.Combine(packageDirectory, "package.json"),
                    "{\"schemaVersion\":\"1.0\",\"revitVersions\":[\"2024\"],\"modules\":[{\"id\":\"incompatible-package\",\"assembly\":\"Incompatible.dll\",\"type\":\"Demo.IncompatibleModule\",\"enabled\":true,\"visible\":true,\"features\":[{\"id\":\"incompatible-feature\",\"displayName\":\"Incompatible\",\"category\":\"test\",\"group\":\"test\",\"defaultState\":\"Visible\"}]}]}");

                var snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(configDirectory);
                Require(!snapshot.Features.Any(feature => feature.ModuleId == "incompatible-package"), "runtime must skip preset-overridden packages incompatible with Revit 2020.");
                Require(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == "RT-MODULE-COMPATIBILITY" && diagnostic.ModuleId == "incompatible-package"), "runtime must report RT-MODULE-COMPATIBILITY for skipped incompatible packages.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void ValidateInstalledRepositoryPackagePreservesCompatibilityAndSkips()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var configDirectory = Path.Combine(tempRoot, "config");
                var repositoryDirectory = Path.Combine(tempRoot, "repository", "root-incompatible-package");
                var installDirectory = Path.Combine(tempRoot, "packages", "root-incompatible-package");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(repositoryDirectory);
                File.WriteAllText(Path.Combine(repositoryDirectory, "Incompatible.dll"), "payload");
                WriteRuntimeConfig(configDirectory);
                File.WriteAllText(
                    Path.Combine(repositoryDirectory, "package.json"),
                    "{\"schemaVersion\":\"1.0\",\"version\":\"1.0.0\",\"revitVersions\":[\"2024\"],\"frameworkVersionRange\":\">=1.2\",\"sha256\":\"stale-after-rewrite\",\"signature\":\"stale-after-rewrite\",\"modules\":[{\"id\":\"root-incompatible-package\",\"assembly\":\"Incompatible.dll\",\"type\":\"Demo.IncompatibleModule\",\"enabled\":true,\"visible\":true,\"features\":[{\"id\":\"root-incompatible-feature\",\"displayName\":\"Root Incompatible\",\"category\":\"test\",\"group\":\"test\",\"defaultState\":\"Visible\"}]}]}");

                var package = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "root-incompatible-package",
                    ModuleId = "root-incompatible-package",
                    DisplayName = "Root Incompatible Package",
                    ManifestPath = Path.Combine(repositoryDirectory, "package.json"),
                    SourceDirectory = repositoryDirectory,
                    InstallDirectory = installDirectory
                };
                var installResult = new PlugHub.Framework.Packages.PackageRepositoryService().Install(tempRoot, package);
                Require(installResult.Success, "installing repository package with root compatibility metadata should succeed: " + installResult.Message);

                var installedManifest = ReadInstalledManifest(Path.Combine(installDirectory, "package.json"));
                Require(installedManifest.Contains("\"revitVersions\"") && installedManifest.Contains("\"2024\""), "installed single-module manifest must preserve root revitVersions metadata.");
                Require(installedManifest.Contains("\"frameworkVersionRange\""), "installed single-module manifest must preserve root frameworkVersionRange metadata.");
                Require(!installedManifest.Contains("\"sha256\"") && !installedManifest.Contains("\"signature\""), "installed single-module manifest must not preserve stale root signature metadata after rewrite.");

                var snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(configDirectory);
                Require(!snapshot.Features.Any(feature => feature.ModuleId == "root-incompatible-package"), "runtime must skip installed repository packages whose root manifest declared incompatible Revit versions.");
                Require(snapshot.Diagnostics.Any(diagnostic => diagnostic.Code == "RT-MODULE-COMPATIBILITY" && diagnostic.ModuleId == "root-incompatible-package"), "runtime must report RT-MODULE-COMPATIBILITY for installed repository packages with incompatible root metadata.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void WriteRuntimeConfig(string configDirectory, string overrideModuleId = "")
        {
            File.WriteAllText(
                Path.Combine(configDirectory, "sources.json"),
                "{\"schemaVersion\":\"1.0\",\"packageDirectories\":[\"packages\"],\"moduleSources\":[],\"repositories\":[],\"conflictPolicy\":{\"duplicateFeatureId\":\"fail-feature\",\"duplicateModuleId\":\"fail-module\",\"missingModuleType\":\"warn\"},\"modules\":[]}");
            File.WriteAllText(
                Path.Combine(configDirectory, "views.json"),
                "{\"schemaVersion\":\"1.0\",\"defaultView\":\"workspace\",\"views\":[{\"id\":\"workspace\",\"name\":\"Workspace\",\"ribbon\":{\"tabName\":\"PlugHub\",\"fallbackPanelName\":\"Framework\"},\"groups\":[{\"id\":\"test\",\"name\":\"Test\",\"includeCategories\":[\"test\"],\"order\":0}],\"sort\":[\"group.order\",\"feature.order\",\"feature.name\",\"feature.id\"]}]}");

            var overrides = string.IsNullOrWhiteSpace(overrideModuleId)
                ? "[]"
                : "[{\"moduleId\":\"" + overrideModuleId + "\",\"visible\":true}]";
            File.WriteAllText(
                Path.Combine(configDirectory, "feature-combinations.json"),
                "{\"schemaVersion\":\"1.0\",\"defaultPreset\":\"workspace-preset\",\"presets\":[{\"id\":\"workspace-preset\",\"viewId\":\"workspace\",\"moduleOverrides\":" + overrides + "}]}");
        }

        private static string ReadInstalledManifest(string path)
        {
            return File.ReadAllText(path).Replace("\\/", "/");
        }

        private static void ValidateRevitRibbonAdapter()
        {
            var adapterText = ReadAllCSharp("src/PlugHub.Revit2020");
            if (!adapterText.Contains("FeatureCommandDispatcher") || !adapterText.Contains("FeatureSlotRegistry"))
            {
                ValidateRuntimeRoutingSpecification();
            }

            foreach (var token in new[] { "CreateRibbonTab", "CreateRibbonPanel", "PushButtonData", "FeatureRibbonBuilder", "FrameworkFeatureCommand", "FeatureCommandDispatcher", "FeatureSlotRegistry" })
            {
                Require(adapterText.Contains(token), "missing Revit adapter token: " + token);
            }
        }

        private static void ValidateRuntimeRoutingSpecification()
        {
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var featureCommand = ReadText("src/PlugHub.Revit2020/FrameworkFeatureCommand.cs");
            var featureDispatcher = ReadText("src/PlugHub.Revit2020/FeatureCommandDispatcher.cs");
            var featureSlotRegistry = ReadText("src/PlugHub.Revit2020/FeatureSlotRegistry.cs");
            var featureExecutionGate = ReadText("src/PlugHub.Framework/Runtime/FeatureExecutionGate.cs");
            var slotCommandText = ReadText("src/PlugHub.Revit2020/FrameworkFeatureCommandSlots.cs");
            var normalizedSlotCommandText = slotCommandText.Replace("\r\n", "\n").Replace("\r", "\n");
            var revitText = ReadAllCSharp("src/PlugHub.Revit2020");

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
            Require(!featureCommand.Contains("Assembly.LoadFrom"), "FrameworkFeatureCommand must delegate business command loading to ICommandAssemblyLoader.");
            Require(featureExecutionGate.Contains("CanExecuteFeatureId") && featureExecutionGate.Contains("matchCommandKey"), "FeatureExecutionGate must expose an id-only execution path for slot routing.");
            Require(featureDispatcher.Contains("CanExecuteFeatureId(featureId)"), "FeatureCommandDispatcher must validate slot-routed feature ids without matching command keys.");
            Require(featureDispatcher.Contains("CanExecute(featureKey)"), "FeatureCommandDispatcher.ExecuteFeature must preserve legacy journal routing by feature id or command key.");
            Require(!featureSlotRegistry.Contains("new Dictionary<int, string>(slotToFeatureId ??"), "FeatureSlotRegistry must not construct Dictionary directly from an IReadOnlyDictionary fallback under net48.");
            Require(featureSlotRegistry.Contains(".ToDictionary(pair => pair.Key, pair => pair.Value)"), "FeatureSlotRegistry.Replace must clone slot mappings through an enumerable-compatible Dictionary shape.");

            ValidateNet48ShadowCopyCommandLoader(featureDispatcher);
        }

        private static void ValidateNet48ShadowCopyCommandLoader(string featureDispatcher)
        {
            const string commandAssemblyLoaderPath = "src/PlugHub.Revit2020/CommandAssemblyLoader.cs";
            Require(File.Exists(FullPath(commandAssemblyLoaderPath)), "runtime routing must keep the net48 command loading strategy in CommandAssemblyLoader.cs.");

            var loader = ReadText(commandAssemblyLoaderPath);
            Require(loader.Contains("class Net48ShadowCopyCommandAssemblyLoader"), "net48 command loader must use a shadow-copy implementation.");
            Require(loader.Contains("runtime-cache"), "shadow-copy loader must copy business assemblies under runtime-cache.");
            Require(loader.Contains("SHA256.Create"), "shadow-copy loader must compute a content hash for cache directories.");
            Require(loader.Contains("CopyPackagePayload"), "shadow-copy loader must copy package payload before loading commands.");
            Require(loader.Contains("IsFlatPayloadFile"), "shadow-copy loader must avoid copying every installed package for flat DLL package manifests.");
            Require(loader.Contains("ApplyPendingCleanup") && loader.Contains("pending-cleanup.txt"), "shadow-copy loader must retry cleanup of old locked cache directories.");
            Require(loader.Contains("Assembly.LoadFrom(cachedAssemblyPath)"), "net48 command loader must load the cached business assembly copy.");
            Require(!loader.Contains("Assembly.LoadFrom(assemblyPath)"), "net48 command loader must not load directly from the installed package assembly path.");
            Require(featureDispatcher.Contains("new Net48ShadowCopyCommandAssemblyLoader()"), "FeatureCommandDispatcher must use the shadow-copy command loader.");
            Require(featureDispatcher.Contains("CommandAssemblyLoader.Create(assemblyPath, feature.CommandType, FrameworkRuntimeState.BaseDirectory)"), "FeatureCommandDispatcher must pass the runtime base directory to the shadow-copy loader.");
        }

        private static void ValidateRevit2025AlcReadinessSpecification()
        {
            var revitText = ReadAllCSharp("src/PlugHub.Revit2020");
            var alcRules = ReadText("src/PlugHub.Contracts/Loading/AlcLoadRules.cs");
            var architecture = ReadText("docs/architecture.md");
            var development = ReadText("docs/development.md");

            Require(alcRules.Contains("class AlcLoadRules"), "ALC readiness must define shared assembly load rules.");
            Require(alcRules.Contains("MustUseDefaultContext"), "ALC readiness must expose a default-context decision point.");
            foreach (var sharedAssembly in new[] { "RevitAPI", "RevitAPIUI", "PlugHub.Contracts" })
            {
                Require(alcRules.Contains(sharedAssembly), "future Revit 2025+ ALC loaders must share assembly with the default context: " + sharedAssembly);
            }

            Require(!revitText.Contains("AssemblyLoadContext"), "Revit 2020 adapter must not use AssemblyLoadContext.");
            Require(!revitText.Contains("AssemblyDependencyResolver"), "Revit 2020 adapter must not use AssemblyDependencyResolver.");
            Require(architecture.Contains("Revit 2025+ ALC") && architecture.Contains("AlcLoadRules"), "architecture docs must describe the Revit 2025+ ALC readiness boundary.");
            Require(development.Contains(".NET SDK 8") && development.Contains("不声明 Revit 2025 实机支持"), "development docs must state the local Revit 2025+ ALC prerequisites and non-support boundary.");
        }

        private static void ValidateManifestAuthoritativeDiscoverySpecification()
        {
            var discovery = ReadText("src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs");
            Require(!discovery.Contains("Assembly.LoadFrom"), "manifest-authoritative discovery must not load module assemblies at startup.");
            Require(!discovery.Contains("Activator.CreateInstance"), "manifest-authoritative discovery must not instantiate module types at startup.");
            Require(!discovery.Contains(".Describe("), "manifest-authoritative discovery must not call IPlugHubModule.Describe() at startup.");
            Require(!discovery.Contains("GetType(module.Type"), "manifest-authoritative discovery must not reflect configured module types at startup.");
            Require(discovery.Contains("ToDescriptor(baseDirectory, module)") && discovery.Contains("descriptors.Add(descriptor)"), "manifest-authoritative discovery must build module descriptors directly from package manifests.");

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
                    Path.Combine(packageDirectory, "package.json"),
                    "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"manifest-authority-module\",\"assembly\":\"MissingBusiness.dll\",\"type\":\"Missing.Plugin.Module\",\"enabled\":true,\"visible\":true,\"order\":10,\"features\":[{\"id\":\"manifest-authority-feature\",\"displayName\":\"Manifest Feature\",\"defaultState\":\"Visible\",\"order\":10}]}]}");

                var runtime = new PlugHub.Framework.Runtime.FrameworkRuntime();
                var snapshot = runtime.Load(baseDirectory, configDirectory);
                Require(snapshot.Features.Any(feature => feature.Id == "manifest-authority-feature"), "package manifest features must load even when the optional module assembly/type cannot be validated at startup.");
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
            var configurationLoader = ReadText("src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs");
            var packageRepositoryService = ReadText("src/PlugHub.Framework/Packages/PackageRepositoryService.cs");
            var credentialService = ReadText("src/PlugHub.Framework/Packages/RepositoryCredentialService.cs");
            var redactor = ReadText("src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs");
            var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
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
            Require(packageRepositoryService.Contains("sparse-checkout") && packageRepositoryService.Contains("SparseCheckoutPatterns") && packageRepositoryService.Contains("ConfigureSparseCheckout"), "repository browsing must use sparse checkout instead of pulling the whole repository.");
            Require(packageRepositoryService.Contains("\"gitee\"") && packageRepositoryService.Contains("https://gitee.com/") && packageRepositoryService.Contains("oauth2:"), "repository browsing must support Gitee HTTPS repositories with apiKey credentials.");
            Require(packageRepositoryService.Contains("InstallPackagePayload") && packageRepositoryService.Contains("WriteSingleModuleManifest") && !packageRepositoryService.Contains("CopyDirectory("), "repository install must split selected plugins and must not copy the whole repository directory.");
            Require(packageRepositoryService.Contains("ApplyPendingOperations") && packageRepositoryService.Contains("pending-operations.json") && packageRepositoryService.Contains("PendingPackageOperation.Restart"), "repository package operations must defer locked DLL deletion and replacement and mark normal installs as restart-required.");
            Require(packageRepositoryService.Contains("ListPendingOperations"), "package repository service must expose pending operation listing.");
            Require(packageRepositoryService.Contains("CancelPendingOperation"), "package repository service must expose pending operation cancellation.");
            Require(credentialService.Contains("ProtectedData.Protect") && credentialService.Contains("ProtectedData.Unprotect"), "repository credential service must use DPAPI.");
            Require(redactor.Contains("Redact") && redactor.Contains("x-access-token") && redactor.Contains("oauth2"), "diagnostic redactor must mask repository tokens.");
            Require(configurationModels.Contains("EncryptedApiKey"), "repository configuration must persist encrypted apiKey separately.");
            Require(packageRepositoryService.Contains("ResolveApiKey(repository)") && packageRepositoryService.Contains("SensitiveTextRedactor.Redact"), "repository service must resolve protected credentials and redact git diagnostics.");
            Require(!packageRepositoryService.Contains("clone --quiet --filter=blob:none --depth 1 --sparse --branch \" + Quote(gitRef) + \" \" + Quote(authenticatedUrl)"), "repository sync must not clone with authenticatedUrl because git persists clone URL as origin.");
            Require(packageRepositoryService.Contains("init --quiet") && packageRepositoryService.Contains("remote add origin \" + Quote(publicUrl)") && packageRepositoryService.Contains("fetch --quiet --filter=blob:none --depth 1 \" + Quote(authenticatedUrl)"), "repository sync must initialize a public origin and fetch authenticated URLs without persisting credentials.");
            Require(settingsWindow.Contains("RepositoryCredentialService") && settingsWindow.Contains("ProtectForSave(repository)"), "settings save must protect repository apiKey before serializing sources.");
            Require(settingsWindow.Contains("ApiKey = string.Empty") && settingsWindow.Contains("PlainApiKey = repository.ApiKey"), "settings repository rows must keep legacy plaintext apiKey available without echoing it in the UI.");
            Require(settingsWindow.Contains("string.IsNullOrWhiteSpace(ApiKey) ? PlainApiKey"), "repository row ToConfiguration must preserve legacy plaintext apiKey when the user did not enter a replacement token.");
            Require(settingsWindow.Contains("EncryptedApiKey = repository.EncryptedApiKey") && settingsWindow.Contains("ApiKeyProtection = repository.ApiKeyProtection"), "settings repository rows must preserve encrypted apiKey metadata.");
            Require(settingsWindow.Contains("EncryptedApiKey = EncryptedApiKey ?? string.Empty") && settingsWindow.Contains("ApiKeyProtection = ApiKeyProtection ?? string.Empty"), "repository row ToConfiguration must retain encrypted apiKey metadata.");
            Require(configurationLoader.Contains("EncryptedApiKey = repository.EncryptedApiKey") && configurationLoader.Contains("ApiKeyProtection = repository.ApiKeyProtection"), "configuration loader must preserve encrypted repository credentials when applying presets.");
            Require(sourceResolver.Contains("EncryptedApiKey = repository.EncryptedApiKey") && sourceResolver.Contains("ApiKeyProtection = repository.ApiKeyProtection"), "module source resolver must preserve encrypted repository credentials.");
            ValidateRepositoryCredentialAndRedactionBehavior();
            var pendingStore = ReadText("src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs");
            Require(pendingStore.Contains("AddOrReplace") && pendingStore.Contains("Remove") && pendingStore.Contains("Read"), "pending operation store must read, add, and remove operations.");
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

        private static void ValidateRepositoryCredentialAndRedactionBehavior()
        {
            var credentialService = new PlugHub.Framework.Packages.RepositoryCredentialService();
            var repository = new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
            {
                ApiKey = "secret-token"
            };

            credentialService.ProtectForSave(repository);
            Require(string.IsNullOrWhiteSpace(repository.ApiKey), "protecting repository credentials must clear plaintext apiKey.");
            Require(!string.IsNullOrWhiteSpace(repository.EncryptedApiKey), "protecting repository credentials must persist encrypted apiKey.");
            Require(!Json.Serialize(repository).Contains("secret-token"), "serialized repository configuration must not retain plaintext apiKey after protection.");
            Require(credentialService.ResolveApiKey(repository) == "secret-token", "protected repository credentials must round-trip through DPAPI.");

            repository.ApiKey = "replacement-token";
            Require(credentialService.ResolveApiKey(repository) == "replacement-token", "plaintext apiKey must take precedence over encrypted apiKey for replacement tokens.");

            var damagedRepository = new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
            {
                Enabled = true,
                Visibility = "private",
                EncryptedApiKey = "not valid base64",
                ApiKeyProtection = "dpapi-current-user"
            };
            Require(credentialService.ResolveApiKey(damagedRepository) == string.Empty, "damaged encrypted repository credentials must not throw or resolve to plaintext.");

            var encryptedOnlyRepository = new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
            {
                Id = "encrypted-private",
                Visibility = "private",
                EncryptedApiKey = "ciphertext",
                ApiKeyProtection = "dpapi-current-user",
                Enabled = true
            };
            var modules = new PlugHub.Framework.Configuration.ModulesConfiguration
            {
                Repositories = new List<PlugHub.Framework.Configuration.PackageRepositoryConfiguration> { encryptedOnlyRepository }
            };
            var applied = new PlugHub.Framework.Configuration.FrameworkConfigurationLoader().ApplyPreset(modules, null);
            Require(applied.Repositories[0].EncryptedApiKey == "ciphertext" && applied.Repositories[0].ApiKeyProtection == "dpapi-current-user", "configuration loader preset application must keep encrypted repository credentials.");

            var resolved = new PlugHub.Framework.Sources.ModuleSourceResolver().Resolve(Path.GetTempPath(), modules);
            Require(resolved.Modules.Repositories[0].EncryptedApiKey == "ciphertext" && resolved.Modules.Repositories[0].ApiKeyProtection == "dpapi-current-user", "module source resolver must keep encrypted repository credentials.");

            var redactedOauth = PlugHub.Framework.Diagnostics.SensitiveTextRedactor.Redact("https://oauth2:secret@gitee.com/owner/repo.git");
            Require(!redactedOauth.Contains("secret") && redactedOauth.Contains("gitee.com/owner/repo.git"), "diagnostic redactor must mask oauth2 tokens while preserving repository host.");
            var redactedGitHub = PlugHub.Framework.Diagnostics.SensitiveTextRedactor.Redact("https://x-access-token:secret@github.com/owner/repo.git");
            Require(!redactedGitHub.Contains("secret") && redactedGitHub.Contains("github.com/owner/repo.git"), "diagnostic redactor must mask x-access-token credentials while preserving repository host.");
            var redactedUserInfo = PlugHub.Framework.Diagnostics.SensitiveTextRedactor.Redact("https://user:secret@example.com/owner/repo.git");
            Require(!redactedUserInfo.Contains("secret") && redactedUserInfo.Contains("example.com/owner/repo.git"), "diagnostic redactor must mask generic URL userinfo while preserving repository host.");
            var redactedApiKey = PlugHub.Framework.Diagnostics.SensitiveTextRedactor.Redact("apiKey=\"secret\"");
            Require(!redactedApiKey.Contains("secret") && redactedApiKey.Contains("***"), "diagnostic redactor must mask apiKey values.");

            var service = new PlugHub.Framework.Packages.PackageRepositoryService();
            var repositoryUrl = typeof(PlugHub.Framework.Packages.PackageRepositoryService).GetMethod("RepositoryUrl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Require(repositoryUrl != null, "repository service must expose a repository URL helper.");
            var publicUrl = Convert.ToString(repositoryUrl!.Invoke(service, new object[]
            {
                new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
                {
                    Provider = "github",
                    Visibility = "private",
                    Repository = "https://user:secret@example.com/owner/repo.git",
                    ApiKey = "replacement-token"
                },
                false
            })) ?? string.Empty;
            Require(!publicUrl.Contains("secret") && !publicUrl.Contains("user:") && publicUrl.Contains("example.com/owner/repo.git"), "public repository URL must strip userinfo before being written to git remote config.");
            service.Browse(Path.GetTempPath(), damagedRepository, out var diagnostics);
            Require(diagnostics.Any(message => message.Code == "PH-REPOSITORY-APIKEY"), "private repository with damaged encrypted credentials must ask for a replacement apiKey.");
        }

        private static void ValidatePendingPackageOperationStoreBehavior()
        {
            var baseDirectory = Path.Combine(Path.GetTempPath(), "plughub-static-validation-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new PlugHub.Framework.Packages.PendingPackageOperationStore();
                var service = new PlugHub.Framework.Packages.PackageRepositoryService();
                var installA = Path.Combine(baseDirectory, "install-a");
                var installB = Path.Combine(baseDirectory, "install-b");
                var staging = Path.Combine(baseDirectory, "staging-a");
                var stagingSameA = Path.Combine(baseDirectory, "staging-same-a");
                var stagingSameB = Path.Combine(baseDirectory, "staging-same-b");

                Directory.CreateDirectory(stagingSameA);
                Directory.CreateDirectory(stagingSameB);
                File.WriteAllText(Path.Combine(stagingSameA, "payload.txt"), "pending-a");
                File.WriteAllText(Path.Combine(stagingSameB, "payload.txt"), "pending-b");
                store.AddOrReplace(baseDirectory, PlugHub.Framework.Packages.PendingPackageOperation.Update("same-package", "module-a", installA, stagingSameA));
                store.AddOrReplace(baseDirectory, PlugHub.Framework.Packages.PendingPackageOperation.Update("same-package", "module-b", installB, stagingSameB));

                var cancelWithoutModule = service.CancelPendingOperation(baseDirectory, "same-package", string.Empty);
                var cancelWithoutPackage = service.CancelPendingOperation(baseDirectory, string.Empty, "module-a");
                var samePackageRemaining = store.Read(baseDirectory)
                    .Where(operation => operation.PackageId == "same-package")
                    .ToList();
                Require(!cancelWithoutModule.Success, "pending operation cancellation must require a module id.");
                Require(!cancelWithoutPackage.Success, "pending operation cancellation must require a package id.");
                Require(samePackageRemaining.Count == 2, "empty-module pending cancellation must not remove same-package metadata.");
                Require(Directory.Exists(stagingSameA) && Directory.Exists(stagingSameB), "empty-module pending cancellation must not delete any update staging directories.");

                var cancelSameModule = service.CancelPendingOperation(baseDirectory, "same-package", "module-a");
                var samePackageAfterExactCancel = store.Read(baseDirectory)
                    .Where(operation => operation.PackageId == "same-package")
                    .ToList();
                Require(cancelSameModule.Success, "exact pending update cancellation must succeed.");
                Require(samePackageAfterExactCancel.Count == 1 && samePackageAfterExactCancel[0].ModuleId == "module-b", "exact pending update cancellation must remove only the matching module metadata.");
                Require(!Directory.Exists(stagingSameA) && Directory.Exists(stagingSameB), "exact pending update cancellation must delete only the matching staging directory.");

                Directory.CreateDirectory(staging);
                File.WriteAllText(Path.Combine(staging, "payload.txt"), "pending");
                store.AddOrReplace(baseDirectory, PlugHub.Framework.Packages.PendingPackageOperation.Update("shared-package", "module-a", installA, staging));
                store.AddOrReplace(baseDirectory, PlugHub.Framework.Packages.PendingPackageOperation.Restart("shared-package", "module-b", installB));

                var cancelUpdate = service.CancelPendingOperation(baseDirectory, "shared-package", "module-a");
                var remaining = store.Read(baseDirectory)
                    .Where(operation => operation.PackageId == "shared-package")
                    .ToList();
                Require(cancelUpdate.Success, "pending operation cancellation must succeed for an existing update.");
                Require(remaining.Count == 1 && remaining[0].PackageId == "shared-package" && remaining[0].ModuleId == "module-b", "cancel pending operation must not remove another module from the same package.");
                Require(!Directory.Exists(staging), "cancel pending update must remove the staging directory.");

                var deleteInstall = Path.Combine(baseDirectory, "delete-install");
                Directory.CreateDirectory(deleteInstall);
                store.AddOrReplace(baseDirectory, PlugHub.Framework.Packages.PendingPackageOperation.Delete("delete-package", "delete-module", deleteInstall));
                var cancelDelete = service.CancelPendingOperation(baseDirectory, "delete-package", "delete-module");
                Require(cancelDelete.Success, "pending delete cancellation must succeed.");
                Require(Directory.Exists(deleteInstall), "cancel pending delete must not remove the install directory.");

                File.WriteAllText(store.PathFor(baseDirectory), "{broken json");
                Require(store.Read(baseDirectory).Count == 0, "pending operation store must tolerate corrupted pending operation files.");
            }
            finally
            {
                if (Directory.Exists(baseDirectory))
                {
                    Directory.Delete(baseDirectory, true);
                }
            }
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
                var directInstalledDirectory = Path.Combine(tempRoot, "packages", "direct-update");
                var directSourceDirectory = Path.Combine(tempRoot, "repository-cache", "direct-update");
                Directory.CreateDirectory(directInstalledDirectory);
                Directory.CreateDirectory(directSourceDirectory);

                var directInstalledDll = Path.Combine(directInstalledDirectory, "DirectUpdate.dll");
                File.WriteAllText(directInstalledDll, "old");
                File.WriteAllText(Path.Combine(directInstalledDirectory, "package.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"1.0.0\",\"modules\":[{\"id\":\"direct-update\",\"assembly\":\"DirectUpdate.dll\",\"type\":\"Demo.DirectUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(Path.Combine(directSourceDirectory, "DirectUpdate.dll"), "replacement");
                File.WriteAllText(Path.Combine(directSourceDirectory, "package.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"2.0.0\",\"modules\":[{\"id\":\"direct-update\",\"assembly\":\"DirectUpdate.dll\",\"type\":\"Demo.DirectUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");

                var directDescriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "direct-update",
                    ModuleId = "direct-update",
                    DisplayName = "Direct Update",
                    ManifestPath = Path.Combine(directSourceDirectory, "package.json"),
                    SourceDirectory = directSourceDirectory,
                    InstallDirectory = directInstalledDirectory,
                    IsInstalled = true
                };

                var directService = new PlugHub.Framework.Packages.PackageRepositoryService();
                var directUpdateResult = directService.Update(tempRoot, directDescriptor);
                Require(directUpdateResult.Success, "updating an unlocked package must succeed immediately: " + directUpdateResult.Message);
                Require(File.ReadAllText(directInstalledDll) == "replacement", "unlocked package update must replace files immediately.");
                var directRefreshed = directService.RefreshInstallState(tempRoot, directDescriptor);
                Require(string.IsNullOrWhiteSpace(directRefreshed.PendingOperation), "unlocked package update must not leave a restart pending operation.");

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
                Require(ductPackage.DisplayName == "Switch", "repository package rows must display the feature name instead of the module or group name.");
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
            var installScript = ReadText("scripts/install-addin.ps1");
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
            Require(revitProject.Contains("<StagePlugHubOutput Condition=\"'$(StagePlugHubOutput)' == ''\">true</StagePlugHubOutput>"), "Revit project must default StagePlugHubOutput to true.");
            Require(revitProject.Contains("Condition=\"'$(StagePlugHubOutput)' == 'true'\""), "StagePlugHubOutput target must be guarded by StagePlugHubOutput=true.");
            Require(buildScript.Contains("[switch]$UseRevitApiNuGet"), "build script must offer an explicit NuGet API reference mode for CI.");
            Require(buildScript.Contains("/p:RevitApiReferenceMode=NuGet"), "build script must pass NuGet reference mode when requested.");
            Require(buildScript.Contains("[switch]$NoStage"), "build script must expose -NoStage.");
            Require(buildScript.Contains("[switch]$Clean"), "build script must expose -Clean.");
            Require(buildScript.Contains("Assert-PathInsideRoot"), "build script must verify clean targets stay inside the repository.");
            Require(buildScript.Contains("Assert-PathInsideRoot $OutputDir"), "build script must verify OutputDir stays inside the repository before creating or cleaning it.");
            Require(buildScript.Contains("function Remove-StaleOutputPath"), "build script must route stale output cleanup through a protected function.");
            Require(buildScript.Contains("Remove-StaleOutputPath $StaleOutputPath"), "build script must use the protected stale output cleanup function.");
            Require(!buildScript.Contains("Remove-Item -LiteralPath $StaleOutputPath"), "stale output cleanup must not call Remove-Item directly.");
            Require(installScript.Contains("Backup-ExistingAddin") && installScript.Contains("Restore-AddinBackup"), "addin install script must backup and restore the addin manifest.");
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
