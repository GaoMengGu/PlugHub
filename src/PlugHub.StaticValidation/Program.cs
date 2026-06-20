using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
                ValidateModulesManifestSchemaAndCompatibility();
                ValidateRibbonLayoutConfigurationModels();
                ValidateRibbonLayoutComposerShape();
                ValidateConfiguredRibbonLayoutAppendsUnplacedFeaturesByGroup();
                ValidateRibbonLayoutRules();
                ValidateRibbonLayoutSettingsRows();
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
                ValidateFrameworkContainsNoBundledModules();
                ValidatePlugHubV2Specification();
                ValidateSettingsPaneV21Specification();
                ValidateFrameworkSettingsWindowSectionBoundaries();
                ValidateSettingsRibbonCleanupSpecification();
                ValidateBuiltinOnlySpecification();
                ValidateSettingsCreationAndSortingSpecification();
                ValidateSettingsGroupFeatureEditingBehavior();
                ValidateDefaultIconSpecification();
                ValidateRasterBrandIconSpecification();
                ValidateRevitWpfUiDesignSpecification();
                ValidatePackageSourceAndReleaseBehavior();
                ValidatePendingPackageOperationStoreBehavior();
                ValidateRepositoryInstallFlowBehavior();
                ValidateRepositoryPackageGranularityAndInstallPayload();
                ValidateRuntimeLoadsSerializedInstalledPackageManifest();
                ValidateRepositoryInstallFailureDoesNotCreateOrRemovePackages();
                ValidateLockedPackageOperationBehavior();
                ValidateRevitApiReferenceStrategy();
                ValidateReleaseInstallerPackaging();
                ValidateGiteeReleaseMirrorPackaging();
                ValidateMachineWideAddinRegistration();
                ValidateUninstallerPackaging();
                ValidateFrameworkAutoUpdateSpecification();
                ValidateReleaseVersioningWorkflow();
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
                "build/Directory.Build.props",
                ".github/workflows/release.yml",
                ".github/workflows/sync-gitee.yml",
                "PlugHub.sln",
                "PlugHub.slnx",
                "src/PlugHub.Contracts/PlugHub.Contracts.csproj",
                "src/PlugHub.Framework/PlugHub.Framework.csproj",
                "src/PlugHub.Framework/Updates/FrameworkUpdateService.cs",
                "src/PlugHub.Framework/Updates/FrameworkUpdateModels.cs",
                "src/PlugHub.Framework/Updates/ReleaseClient.cs",
                "src/PlugHub.Framework/Updates/ReleaseAssetDownloader.cs",
                "src/PlugHub.Framework/Updates/FrameworkUpdatePackageValidator.cs",
                "src/PlugHub.Revit2020/PlugHub.Revit2020.csproj",
                "src/PlugHub.Installer/PlugHub.Installer.csproj",
                "src/PlugHub.Installer/Program.cs",
                "src/PlugHub.Installer/InstallerForm.cs",
                "src/PlugHub.Installer/InstallerPayload.cs",
                "src/PlugHub.Installer/AddinManifestWriter.cs",
                "src/PlugHub.Tests/PlugHub.Tests.csproj",
                "src/PlugHub.Tests/Program.cs",
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
                "src/PlugHub.Framework/Packages/RepositoryAddress.cs",
                "src/PlugHub.Framework/Packages/RepositoryBrowser.cs",
                "src/PlugHub.Framework/Packages/RepositoryArchiveSynchronizer.cs",
                "src/PlugHub.Framework/Packages/PackageManifestReader.cs",
                "src/PlugHub.Framework/Packages/PackageManifestWriter.cs",
                "src/PlugHub.Framework/Packages/PackageInstallService.cs",
                "src/PlugHub.Framework/Packages/RepositoryPackageDescriptor.cs",
                "src/PlugHub.Framework/Packages/PendingPackageOperationsDocument.cs",
                "src/PlugHub.Framework/Packages/PendingPackageOperation.cs",
                "src/PlugHub.Framework/Packages/PendingManifestBackup.cs",
                "src/PlugHub.Framework/Packages/PackageRepositoryOperationResult.cs",
                "src/PlugHub.Framework/Settings/SettingsConfigurationStore.cs",
                "src/PlugHub.Framework/Diagnostics/PlugHubLogEntry.cs",
                "src/PlugHub.Framework/Diagnostics/PlugHubLogger.cs",
                "src/PlugHub.Framework/Diagnostics/PlugHubLogExporter.cs",
                "src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs",
                "src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs",
                "src/PlugHub.Wpf/PlugHub.Wpf.csproj",
                "src/PlugHub.Wpf/FrameworkStatusWindow.cs",
                "src/PlugHub.Wpf/DefaultRibbonIconProvider.cs",
                "src/PlugHub.Wpf/RevitUiTheme.cs",
                "src/PlugHub.Wpf/RevitWindowOwner.cs",
                "src/PlugHub.Manager/PlugHub.Manager.csproj",
                "src/PlugHub.Revit2020/ExternalApplicationEntry.cs",
                "src/PlugHub.Revit2020/ExternalManagerLauncher.cs",
                "src/PlugHub.Revit2020/FeatureRibbonBuilder.cs",
                "src/PlugHub.Revit2020/FrameworkFeatureCommand.cs",
                "src/PlugHub.Manager/FrameworkSettingsWindow.cs",
                "src/PlugHub.Manager/Settings/FrameworkSettingsViewModel.cs",
                "src/PlugHub.Manager/Settings/RepositorySettingsController.cs",
                "src/PlugHub.Manager/Settings/SettingsMetrics.cs",
                "src/PlugHub.Manager/Settings/Rows/ModuleRow.cs",
                "src/PlugHub.Manager/Settings/Rows/FeatureRow.cs",
                "src/PlugHub.Manager/Settings/Rows/GroupRow.cs",
                "src/PlugHub.Manager/Settings/Rows/RepositoryRow.cs",
                "src/PlugHub.Manager/Settings/Rows/RepositoryPackageRow.cs",
                "src/PlugHub.Manager/Settings/Rows/PendingPackageOperationRow.cs",
                "src/PlugHub.Manager/Settings/Rows/DiagnosticRow.cs",
                "src/PlugHub.Manager/Resources/alipay.png",
                "src/PlugHub.Manager/Resources/wechatpay.png",
                "src/PlugHub.Manager/Program.cs",
                "src/PlugHub.Manager/Maintenance/ManagerMaintenanceMode.cs",
                "src/PlugHub.Manager/Maintenance/ManagerMaintenanceArguments.cs",
                "src/PlugHub.Manager/Maintenance/ManagerMaintenanceLauncher.cs",
                "src/PlugHub.Manager/Maintenance/ManagerMaintenanceRunner.cs",
                "src/PlugHub.Manager/Maintenance/ManagerFrameworkUpdater.cs",
                "src/PlugHub.Manager/Maintenance/ManagerUninstaller.cs",
                "src/PlugHub.Manager/Maintenance/ManagerMaintenanceLogger.cs",
                "scripts/sign-revit2020.ps1",
                "config/sources.example.json",
                "config/views.example.json",
                "config/feature-combinations.example.json",
                "config/schemas/sources.schema.json",
                "config/schemas/views.schema.json",
                "config/schemas/packages.schema.json"
            };

            var missing = required.Where(path => !File.Exists(FullPath(path))).ToList();
            Require(!missing.Any(), "missing required files: " + string.Join(", ", missing));
            ValidateInternalDocsIfPresent();
            var solution = ReadText("PlugHub.sln");
            var solutionX = ReadText("PlugHub.slnx");
            Require(solutionX.Contains("src/PlugHub.Tests/PlugHub.Tests.csproj"), "independent behavior test project must be included in PlugHub.slnx.");
            Require(solution.Contains("src\\PlugHub.Manager\\PlugHub.Manager.csproj"), "PlugHub Manager project must be included in PlugHub.sln.");
            Require(solutionX.Contains("src/PlugHub.Manager/PlugHub.Manager.csproj"), "PlugHub Manager project must be included in PlugHub.slnx.");
            Require(solution.Contains("src\\PlugHub.Wpf\\PlugHub.Wpf.csproj"), "shared WPF project must be included in PlugHub.sln.");
            Require(solutionX.Contains("src/PlugHub.Wpf/PlugHub.Wpf.csproj"), "shared WPF project must be included in PlugHub.slnx.");
            Require(!solution.Contains("PlugHub.Updater") && !solutionX.Contains("PlugHub.Updater"), "solution must not keep the old standalone updater project.");
            Require(!solution.Contains("PlugHub.Uninstaller") && !solutionX.Contains("PlugHub.Uninstaller"), "solution must not keep the old standalone uninstaller project.");
            Require(!solution.Contains("PlugHub.SettingsApp") && !solution.Contains("PlugHub.SettingsUi"), "solution must not keep legacy SettingsApp/SettingsUi project names after Manager rename.");
            Require(!solutionX.Contains("PlugHub.SettingsApp") && !solutionX.Contains("PlugHub.SettingsUi"), "slnx must not keep legacy SettingsApp/SettingsUi project names after Manager rename.");
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
            Require(!File.Exists(FullPath("src/PlugHub.Manager/Settings/SettingsConfigurationStore.cs")), "settings configuration store must live in PlugHub.Framework for PlugHub Manager reuse.");
            Require(!Directory.Exists(FullPath("modules")), "source workspace must not keep a modules drop-in directory; build output creates package drop-ins.");
            if (Directory.Exists(FullPath("tests")))
            {
                var testProjects = Directory.GetFiles(FullPath("tests"), "*.csproj", SearchOption.AllDirectories);
                Require(testProjects.Length > 0, "tests directory must contain real test projects; move validation notes into README.md instead of keeping a placeholder tests folder.");
            }
        }

        private static void ValidateInternalDocsIfPresent()
        {
            if (!Directory.Exists(FullPath("docs"))) return;

            var required = new[]
            {
                "docs/TODO.md",
                "docs/development.md",
                "docs/icon-spec.md",
                "docs/revit-2020-acceptance-template.md"
            };

            var missing = required.Where(path => !File.Exists(FullPath(path))).ToList();
            Require(!missing.Any(), "missing internal docs: " + string.Join(", ", missing));
        }

        private static void ValidateDocumentationStructure()
        {
            var ignore = ReadText(".gitignore");
            var readme = ReadText("README.md");
            Require(ignore.Contains("docs/"), "docs directory must stay local-only for internal architecture, progress, review, and planning records.");
            Require(!readme.Contains("[docs/README.md]"), "root README must not point users at local-only internal docs content.");
            Require(!readme.Contains("D:\\AI\\code\\PlugHub_Modules"), "root README must not expose local external module paths.");
            Require(readme.Contains("面向建模用户") && readme.Contains("框架概览") && readme.Contains("能做什么"), "root README must introduce PlugHub for modeling users.");
            Require(readme.Contains("安装") && readme.Contains("文件夹权限") && readme.Contains(@"D:\Program Files\PlugHub"), "root README must document installation and folder permission guidance.");
            Require(readme.Contains("更新") && readme.Contains("检查更新小图标") && readme.Contains("卸载小图标") && readme.Contains("框架 DLL 和 PlugHub Manager"), "root README must document framework update and Manager maintenance behavior.");
            Require(readme.Contains("布局设置") && readme.Contains("拖拽") && readme.Contains("重启 Revit"), "root README must document how users change Ribbon layout.");
            Require(readme.Contains("仓库源") && readme.Contains("同步仓库源") && readme.Contains("不需要安装 Git"), "root README must document repository source setup for users.");
            Require(readme.Contains("插件安装") && readme.Contains("安装插件") && readme.Contains("packages"), "root README must document plugin installation for users.");
            Require(!readme.Contains("IExternalCommand") && !readme.Contains("StagePlugHubOutput=false") && !readme.Contains("release.yml"), "root README must not include developer or release automation instructions.");
            Require(!readme.Contains("PlugHub.Contracts") && !readme.Contains("System.Web.Script.Serialization") && !readme.Contains("Revit 2025+ ALC"), "root README must not expose framework development internals.");
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
            Require(Repositories(modules).Count() >= 3, "repositories must include public cloud, private cloud, and local folder examples.");
            Require(Repositories(modules).All(repository => repository.ContainsKey("displayName")), "repositories must include editable displayName examples.");
            Require(Repositories(modules).Any(repository => StringValue(repository, "visibility") == "public"), "repositories must include a public repository example.");
            Require(Repositories(modules).Any(repository => StringValue(repository, "visibility") == "private" && repository.ContainsKey("apiKey")), "repositories must include a private repository example with apiKey.");
            Require(Repositories(modules).Any(repository => StringValue(repository, "provider") == "local"), "repositories must include a local folder repository example.");
            Require(Repositories(modules).Any(repository =>
                StringValue(repository, "provider") == "github"
                && StringValue(repository, "visibility") == "public"
                && StringValue(repository, "repository") == "GaoMengGu/PlugHub_Packages"
                && StringValue(repository, "enabled") == "True"), "default public repository must be the enabled owner/repository PlugHub_Packages cloud source.");
            var repositoryOrder = Repositories(modules)
                .Select(repository => StringValue(repository, "provider") + ":" + StringValue(repository, "visibility"))
                .ToList();
            Require(repositoryOrder.Take(3).SequenceEqual(new[] { "github:public", "github:private", "local:public" }), "default repositories must be ordered public cloud, private cloud, local folder.");
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
            var readme = ReadText("README.md");

            Require(contractsProject.Contains("<TargetFrameworks>net48;netstandard2.1</TargetFrameworks>"), "PlugHub.Contracts must target net48 and netstandard2.1 for future net8 adapters.");
            Require(!ReadAllCSharp("src/PlugHub.Contracts").Contains("System.Web"), "PlugHub.Contracts must stay free of net48-only System.Web dependencies.");
            Require(frameworkProject.Contains("<TargetFramework>net48</TargetFramework>") && frameworkProject.Contains("System.Web.Extensions"), "PlugHub.Framework remains net48 until its JSON serializer boundary is replaced.");
            Require(!readme.Contains("netstandard2.1") && !readme.Contains("System.Web.Script.Serialization"), "root README must not document framework development internals.");
        }

        private static void ValidateModulesManifestSchemaAndCompatibility()
        {
            var schema = ReadText("config/schemas/packages.schema.json");
            Require(schema.Contains("\"indexVersion\""), "packages schema must define indexVersion for repository index snapshots.");
            Require(schema.Contains("\"revitVersions\""), "packages schema must define revitVersions.");
            Require(schema.Contains("\"frameworkVersionRange\""), "packages schema must define frameworkVersionRange.");
            foreach (var token in new[]
            {
                "\"version\"",
                "\"author\"",
                "\"assembly\"",
                "\"category\"",
                "\"displayName\"",
                "\"description\"",
                "\"tags\"",
                "\"iconPath\"",
                "\"commandType\""
            })
            {
                Require(schema.Contains(token), "packages schema must define current module or feature field: " + token);
            }

            foreach (var removedToken in new[] { "\"enabled\"", "\"visible\"", "\"order\"", "\"defaultState\"", "\"buttonSize\"", "\"commandAssembly\"", "\"moduleSources\"", "\"repositories\"", "\"packageDirectories\"", "\"conflictPolicy\"", "\"sha256\"", "\"signature\"" })
            {
                Require(!schema.Contains(removedToken), "packages schema must not define layout, runtime state, source config, or stale signature fields: " + removedToken);
            }

            var packageValidation = ReadText("src/PlugHub.StaticValidation/Validation/PackageManifestValidation.cs");
            Require(packageValidation.Contains("Packages manifest") && packageValidation.Contains("IEnumerable") && packageValidation.Contains("Cast<object>().Any()") && !packageValidation.Contains("object[]"), "packages manifest validation must accept JavaScriptSerializer ArrayList modules.");

            var models = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            Require(models.Contains("IndexVersion") && models.Contains("public string Version") && models.Contains("public string Author") && models.Contains("public string Category") && models.Contains("RevitVersions") && models.Contains("FrameworkVersionRange"), "configuration models must expose packages manifest author, version, and compatibility fields.");

            var featureDescriptor = ReadText("src/PlugHub.Contracts/Features/FeatureDescriptor.cs");
            Require(featureDescriptor.Contains("ModuleName"), "feature descriptors must carry module display names so framework-owned default layouts can avoid technical panel names.");

            var sourceResolver = ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var packageDefaults = ReadText("src/PlugHub.Framework/Configuration/PackageManifestDefaults.cs");
            Require(sourceResolver.Contains("DefaultPackageManifestName = \"packages.json\""), "module source resolver must use packages.json as the only default module manifest.");
            Require(sourceResolver.Contains("AdjacentPackageManifestPattern = \"*.packages.json\""), "module source resolver must scan adjacent *.packages.json manifests.");
            Require(sourceResolver.Contains("NormalizeRepositoryModuleDefaults") && sourceResolver.Contains("PackageManifestDefaults.NormalizeModuleState"), "module source resolver must default repository modules through the shared package manifest default normalizer.");
            Require(packageDefaults.Contains("ContainsExactKey") && packageDefaults.Contains("module.Enabled = true") && packageDefaults.Contains("module.Visible = true"), "package manifest defaults must treat omitted lowercase enabled/visible as enabled and visible.");
            Require(sourceResolver.Contains("PushRootCompatibilityToModules") && sourceResolver.Contains("module.RevitVersions = new List<string>(modules.RevitVersions)") && sourceResolver.Contains("module.FrameworkVersionRange = modules.FrameworkVersionRange"), "module source resolver must push root compatibility fields down to modules.");

            var configurationLoader = ReadText("src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs");
            foreach (var token in new[] { "IndexVersion = modules.IndexVersion", "RevitVersions = new List<string>(modules.RevitVersions", "FrameworkVersionRange = modules.FrameworkVersionRange", "Version = module.Version", "Author = module.Author", "Category = module.Category", "RevitVersions = new List<string>(module.RevitVersions", "FrameworkVersionRange = module.FrameworkVersionRange" })
            {
                Require(configurationLoader.Contains(token), "framework configuration loader must preserve packages manifest fields: " + token);
            }
            Require(!configurationLoader.Contains("Version = modules.Version") && !configurationLoader.Contains("Sha256 = modules.Sha256") && !configurationLoader.Contains("Signature = modules.Signature"), "framework configuration loader must not preserve obsolete root package version or signature fields.");

            var discovery = ReadText("src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs");
            Require(discovery.Contains("IsCompatibleWithRuntime"), "module discovery must skip packages incompatible with the active runtime.");
            Require(discovery.Contains("RT-MODULE-COMPATIBILITY") && discovery.Contains("continue;"), "module discovery must warn and skip packages incompatible with the active runtime.");
            Require(discovery.Contains("CurrentRevitVersion") && discovery.Contains(".Trim()") && discovery.Contains("StringComparer.OrdinalIgnoreCase"), "module discovery must normalize declared Revit versions before comparing with the current runtime.");
            Require(discovery.Contains("FrameworkVersionRange") && discovery.Contains("metadata"), "frameworkVersionRange must be explicitly preserved as metadata and not treated as runtime compatibility logic yet.");
            Require(discovery.Contains("ModuleName = DisplayNameResolver.Resolve(module.DisplayName, module.Name, string.Empty, module.Id)"), "module discovery must project module display names onto feature descriptors.");

            var packageInstallService = ReadText("src/PlugHub.Framework/Packages/PackageInstallService.cs");
            Require(packageInstallService.Contains("DefaultPackageManifestName = \"packages.json\""), "repository installs must write packages.json as the local module manifest.");
            Require(packageInstallService.Contains("PackageManifestWriter") && packageInstallService.Contains("WritePackageManifest(targetManifestPath, manifest, false)"), "repository installs must use the current package manifest writer and omit repository index metadata.");
            Require(packageInstallService.Contains("RevitVersions = new List<string>(sourceManifest.RevitVersions") && packageInstallService.Contains("FrameworkVersionRange = sourceManifest.FrameworkVersionRange"), "single-module installed manifests must preserve root compatibility metadata.");
            Require(!packageInstallService.Contains("CopyOptionalManifestValue(root, manifest, \"version\")") && !packageInstallService.Contains("CopyOptionalManifestValue(root, manifest, \"indexVersion\")") && !packageInstallService.Contains("CopyOptionalManifestValue(root, manifest, \"sha256\")") && !packageInstallService.Contains("CopyOptionalManifestValue(root, manifest, \"signature\")"), "single-module installed manifests must not copy root index or signature metadata after rewriting the manifest.");

            ValidateRuntimeAcceptsWhitespacePaddedRevitVersion();
            ValidateRuntimeSkipsPresetOverriddenIncompatiblePackage();
            ValidateInstalledRepositoryPackagePreservesCompatibilityAndSkips();
            ValidateRepositoryModulesManifestVersionAndDefaults();
            ValidatePackageManifestWriterProducesCurrentSchema();
            ValidateRuntimeDefaultLayoutUsesModuleDisplayNames();
            ValidateRibbonLayoutUsesResolvedPackageIconWhenOverrideIsManifestRelative();
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
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"revitVersions\":[\" 2020 \",\"\"],\"frameworkVersionRange\":\">=1.2\",\"modules\":[{\"id\":\"compatible-package\",\"version\":\"V1.0.0\",\"assembly\":\"Compatible.dll\",\"type\":\"Demo.CompatibleModule\",\"features\":[{\"id\":\"compatible-feature\",\"displayName\":\"Compatible\",\"category\":\"test\"}]}]}");

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
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"revitVersions\":[\"2024\"],\"modules\":[{\"id\":\"incompatible-package\",\"version\":\"V1.0.0\",\"assembly\":\"Incompatible.dll\",\"type\":\"Demo.IncompatibleModule\",\"features\":[{\"id\":\"incompatible-feature\",\"displayName\":\"Incompatible\",\"category\":\"test\"}]}]}");

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
                    Path.Combine(repositoryDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"indexVersion\":\"V1.0.0\",\"revitVersions\":[\"2024\"],\"frameworkVersionRange\":\">=1.2\",\"modules\":[{\"id\":\"root-incompatible-package\",\"version\":\"V1.0.0\",\"assembly\":\"Incompatible.dll\",\"type\":\"Demo.IncompatibleModule\",\"features\":[{\"id\":\"root-incompatible-feature\",\"displayName\":\"Root Incompatible\",\"category\":\"test\"}]}]}");

                var package = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "root-incompatible-package",
                    ModuleId = "root-incompatible-package",
                    DisplayName = "Root Incompatible Package",
                    ManifestPath = Path.Combine(repositoryDirectory, "packages.json"),
                    SourceDirectory = repositoryDirectory,
                    InstallDirectory = installDirectory
                };
                var installResult = new PlugHub.Framework.Packages.PackageRepositoryService().Install(tempRoot, package);
                Require(installResult.Success, "installing repository package with root compatibility metadata should succeed: " + installResult.Message);

                var installedManifest = ReadInstalledManifest(Path.Combine(installDirectory, "packages.json"));
                Require(installedManifest.Contains("\"revitVersions\"") && installedManifest.Contains("\"2024\""), "installed single-module manifest must preserve root revitVersions metadata.");
                Require(installedManifest.Contains("\"frameworkVersionRange\""), "installed single-module manifest must preserve root frameworkVersionRange metadata.");
                Require(!installedManifest.Contains("\"indexVersion\""), "installed single-module manifest must not preserve repository index metadata after rewrite.");

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

        private static void ValidateRepositoryModulesManifestVersionAndDefaults()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var configDirectory = Path.Combine(tempRoot, "config");
                var repositoryRoot = Path.Combine(tempRoot, "repository-cache", "modules-index");
                var installDirectory = Path.Combine(tempRoot, "packages", "minimal-module");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(repositoryRoot);
                Directory.CreateDirectory(Path.Combine(repositoryRoot, "icons"));
                File.WriteAllText(Path.Combine(repositoryRoot, "Minimal.dll"), "payload");
                File.WriteAllText(Path.Combine(repositoryRoot, "icons", "minimal.png"), "icon");
                WriteRuntimeConfig(configDirectory);
                File.WriteAllText(
                    Path.Combine(repositoryRoot, "minimal.packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"indexVersion\":\"V9.0.0\",\"revitVersions\":[\"2020\"],\"frameworkVersionRange\":\">=1.3.0\",\"modules\":[{\"id\":\"minimal-module\",\"version\":\"V2.3.4\",\"author\":\"GAOMENGGU\",\"displayName\":\"Minimal Module\",\"description\":\"Minimal repository module.\",\"assembly\":\"Minimal.dll\",\"category\":\"view\",\"tags\":[\"view\",\"minimal\"],\"features\":[{\"id\":\"minimal-module.run\",\"displayName\":\"Run Minimal\",\"description\":\"Run the minimal module.\",\"iconPath\":\"icons/minimal.png\",\"commandType\":\"Demo.MinimalCommand\"}]}]}");

                var service = new PlugHub.Framework.Packages.PackageRepositoryService();
                var packages = service.BrowseCached(tempRoot, "modules-index", repositoryRoot, out var browseDiagnostics);
                Require(!browseDiagnostics.Any(), "*.packages.json repository browse should not emit diagnostics: " + string.Join("; ", browseDiagnostics.Select(item => item.Message)));
                Require(packages.Count == 1, "repository *.packages.json must browse one plugin row for one module.");
                var package = packages[0];
                Require(package.Version == "V2.3.4", "repository package row version must come from modules[].version instead of the root indexVersion.");
                Require(package.Categories.Contains("view"), "repository package category metadata must include module category when features omit category.");
                Require(package.Tags.Contains("minimal"), "repository package tags must include module tags.");

                var installResult = service.Install(tempRoot, package);
                Require(installResult.Success, "installing a minimal packages.json repository module should succeed: " + installResult.Message);
                Require(File.Exists(Path.Combine(installDirectory, "packages.json")), "installed repository module must write packages.json as the package-local manifest.");
                Require(!File.Exists(Path.Combine(installDirectory, "package.json")), "installed repository module must not write legacy package.json.");
                var installedManifest = ReadInstalledManifest(Path.Combine(installDirectory, "packages.json"));
                Require(installedManifest.Contains("\"version\":\"V2.3.4\""), "installed packages.json must preserve the selected module version.");
                Require(installedManifest.Contains("\"author\":\"GAOMENGGU\""), "installed packages.json must preserve the selected module author.");
                Require(!installedManifest.Contains("\"indexVersion\""), "installed packages.json must not preserve repository indexVersion.");

                var refreshed = service.RefreshInstallState(tempRoot, package);
                Require(refreshed.InstalledVersion == "V2.3.4", "installed package version must be read from the installed module version.");

                var snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(configDirectory);
                Require(snapshot.Features.Any(feature => feature.Id == "minimal-module.run"), "runtime must load installed packages.json even when enabled, visible, group, order, defaultState, buttonSize, and commandAssembly are omitted.");
                Require(snapshot.Features.Any(feature => feature.Id == "minimal-module.run" && feature.Category == "view" && feature.CommandAssembly.EndsWith("Minimal.dll", StringComparison.OrdinalIgnoreCase)), "runtime must inherit module category and command assembly defaults for features.");
                Require(snapshot.Features.Any(feature => feature.Id == "minimal-module.run" && feature.ModuleName == "Minimal Module"), "runtime feature descriptors must preserve module displayName for framework default layout naming.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void ValidatePackageManifestWriterProducesCurrentSchema()
        {
            var manifest = new PlugHub.Framework.Configuration.ModulesConfiguration
            {
                SchemaVersion = "1.1",
                IndexVersion = "V9.9.9",
                RevitVersions = new List<string> { "2020" },
                FrameworkVersionRange = ">=1.3.0",
                PackageDirectories = new List<string> { "packages" },
                ModuleSources = new List<PlugHub.Framework.Configuration.ModuleSourceConfiguration>
                {
                    new PlugHub.Framework.Configuration.ModuleSourceConfiguration { Id = "local", Enabled = true }
                },
                Repositories = new List<PlugHub.Framework.Configuration.PackageRepositoryConfiguration>
                {
                    new PlugHub.Framework.Configuration.PackageRepositoryConfiguration { Id = "repo", Enabled = true }
                },
                ConflictPolicy = new PlugHub.Framework.Configuration.ConflictPolicyConfiguration(),
                Modules = new List<PlugHub.Framework.Configuration.ModuleConfiguration>
                {
                    new PlugHub.Framework.Configuration.ModuleConfiguration
                    {
                        Id = "writer-module",
                        Version = "V1.2.3",
                        Author = "GAOMENGGU",
                        Assembly = "dist/Writer.dll",
                        Type = "Legacy.ModuleType",
                        Name = "legacy-name",
                        DisplayName = "Writer Module",
                        Description = "Writer schema validation module.",
                        Category = "view",
                        SourceId = "runtime-source",
                        ResolvedBaseDirectory = "runtime-base",
                        Enabled = false,
                        Visible = false,
                        Order = 42,
                        Tags = new List<string> { "view", "writer" },
                        DependsOn = new List<string> { "old-module" },
                        Features = new List<PlugHub.Framework.Configuration.FeatureConfiguration>
                        {
                            new PlugHub.Framework.Configuration.FeatureConfiguration
                            {
                                Id = "writer-module.run",
                                Name = "legacy-feature-name",
                                DisplayName = "Run Writer",
                                Description = "Runs writer validation.",
                                Category = "runtime-category",
                                Group = "runtime-group",
                                Tags = new List<string> { "feature-tag" },
                                Order = 10,
                                DefaultState = "Hidden",
                                CommandKey = "legacy-key",
                                CommandAssembly = "Other.dll",
                                CommandType = "Demo.WriterCommand",
                                ButtonSize = "small",
                                IconPath = "icons/writer.png"
                            }
                        }
                    }
                }
            };

            var text = new PlugHub.Framework.Packages.PackageManifestWriter().SerializePackageManifest(manifest);
            var root = Json.Deserialize<Dictionary<string, object>>(text);
            var module = Modules(root).Single();
            var feature = Features(module).Single();

            Require(StringValue(root, "schemaVersion") == "1.1", "package manifest writer must preserve schemaVersion.");
            Require(StringValue(root, "indexVersion") == "V9.9.9", "repository package manifest writer must preserve indexVersion when writing repository-style manifests.");
            Require(SequenceValue(root, "revitVersions").SequenceEqual(new[] { "2020" }), "package manifest writer must preserve root revitVersions.");
            Require(StringValue(root, "frameworkVersionRange") == ">=1.3.0", "package manifest writer must preserve root frameworkVersionRange.");
            foreach (var forbiddenRoot in new[] { "PackageDirectories", "ModuleSources", "Repositories", "ConflictPolicy", "packageDirectories", "moduleSources", "repositories", "conflictPolicy" })
            {
                Require(!root.ContainsKey(forbiddenRoot), "package manifest writer must omit framework root field: " + forbiddenRoot);
            }

            foreach (var token in new[] { "id", "version", "author", "displayName", "description", "assembly", "category", "tags", "features" })
            {
                Require(module.ContainsKey(token), "package manifest writer must emit module field: " + token);
            }

            foreach (var forbiddenModule in new[] { "Id", "Version", "Author", "Enabled", "Visible", "Order", "Type", "Name", "SourceId", "ResolvedBaseDirectory", "DependsOn", "enabled", "visible", "order", "type", "name", "sourceId", "resolvedBaseDirectory", "dependsOn" })
            {
                Require(!module.ContainsKey(forbiddenModule), "package manifest writer must omit runtime module field: " + forbiddenModule);
            }

            foreach (var token in new[] { "id", "displayName", "description", "iconPath", "commandType" })
            {
                Require(feature.ContainsKey(token), "package manifest writer must emit feature field: " + token);
            }

            foreach (var forbiddenFeature in new[] { "Id", "DisplayName", "Category", "Group", "Order", "DefaultState", "CommandKey", "CommandAssembly", "ButtonSize", "Name", "Tags", "category", "group", "order", "defaultState", "commandKey", "commandAssembly", "buttonSize", "name", "tags" })
            {
                Require(!feature.ContainsKey(forbiddenFeature), "package manifest writer must omit runtime feature field: " + forbiddenFeature);
            }
        }

        private static void ValidateRuntimeDefaultLayoutUsesModuleDisplayNames()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var configDirectory = Path.Combine(tempRoot, "config");
                var packageDirectory = Path.Combine(tempRoot, "packages", "view-tools");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(packageDirectory);

                WriteRuntimeIsolationConfiguration(configDirectory);
                File.WriteAllText(
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"revitVersions\":[\"2020\"],\"modules\":[{\"id\":\"view-grid\",\"version\":\"V1.0.0\",\"displayName\":\"View Tools\",\"assembly\":\"Grid.dll\",\"category\":\"view\",\"features\":[{\"id\":\"view-grid.toggle\",\"displayName\":\"Toggle Grid\"}]},{\"id\":\"view-level\",\"version\":\"V1.0.0\",\"displayName\":\"View Tools\",\"assembly\":\"Level.dll\",\"category\":\"view\",\"features\":[{\"id\":\"view-level.toggle\",\"displayName\":\"Toggle Level\"}]},{\"id\":\"duct-tools\",\"version\":\"V1.0.0\",\"displayName\":\"Duct Tools\",\"assembly\":\"Duct.dll\",\"category\":\"mep\",\"features\":[{\"id\":\"duct-tools.switch\",\"displayName\":\"Switch Duct\"}]}]}");

                var snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(tempRoot, configDirectory);
                var panelNames = snapshot.Composition.Features
                    .Select(feature => feature.GroupName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Require(panelNames.Contains("View Tools"), "runtime default layout must use module displayName as the fallback panel display name.");
                Require(panelNames.Contains("Duct Tools"), "runtime default layout must use each module displayName for package-derived fallback panels.");
                Require(!panelNames.Contains("view") && !panelNames.Contains("mep") && !panelNames.Any(name => name.StartsWith("view-", StringComparison.OrdinalIgnoreCase)), "runtime default layout must not expose category codes or module ids as package fallback panel names.");
                Require(snapshot.Composition.Features.Count(feature => feature.GroupName == "View Tools") == 2, "runtime default layout must merge modules that intentionally share a module displayName.");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void ValidateRibbonLayoutUsesResolvedPackageIconWhenOverrideIsManifestRelative()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PlugHub.StaticValidation", Guid.NewGuid().ToString("N"));
            try
            {
                var configDirectory = Path.Combine(tempRoot, "config");
                var packageDirectory = Path.Combine(tempRoot, "packages", "icon-package");
                Directory.CreateDirectory(configDirectory);
                Directory.CreateDirectory(Path.Combine(packageDirectory, "icons"));

                WriteRuntimeConfig(configDirectory);
                File.WriteAllText(Path.Combine(packageDirectory, "icons", "package.png"), "icon");
                File.WriteAllText(
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"revitVersions\":[\"2020\"],\"modules\":[{\"id\":\"icon-package\",\"version\":\"V1.0.0\",\"displayName\":\"Icon Package\",\"assembly\":\"IconPackage.dll\",\"category\":\"test\",\"features\":[{\"id\":\"icon-package.run\",\"displayName\":\"Run Icon Package\",\"iconPath\":\"icons/package.png\"}]}]}");
                File.WriteAllText(
                    Path.Combine(configDirectory, "views.json"),
                    "{\"schemaVersion\":\"1.0\",\"defaultView\":\"workspace\",\"views\":[{\"id\":\"workspace\",\"name\":\"Workspace\",\"ribbon\":{\"tabName\":\"PlugHub\",\"fallbackPanelName\":\"Framework\",\"panels\":[{\"id\":\"test\",\"name\":\"Test\",\"order\":100,\"items\":[{\"type\":\"pushButton\",\"id\":\"icon-package.run\",\"featureId\":\"icon-package.run\",\"size\":\"large\",\"textOverride\":\"Run Icon Package\",\"iconPathOverride\":\"icons/package.png\",\"order\":100}]}]},\"groups\":[{\"id\":\"test\",\"name\":\"Test\",\"includeCategories\":[\"test\"],\"order\":0}],\"sort\":[\"group.order\",\"feature.order\",\"feature.name\",\"feature.id\"]}]}");

                var snapshot = new PlugHub.Framework.Runtime.FrameworkRuntime().Load(tempRoot, configDirectory);
                var layout = new PlugHub.Framework.Composition.RibbonLayoutComposer().Compose(
                    snapshot.Configuration.ActiveView,
                    snapshot.Composition.Features);
                var item = layout.Panels.SelectMany(panel => panel.Items).SingleOrDefault();
                var expectedIconPath = Path.Combine(packageDirectory, "icons", "package.png");

                Require(item != null, "runtime ribbon layout must include the icon-package feature.");
                Require(string.Equals(Path.GetFullPath(item!.IconPath), Path.GetFullPath(expectedIconPath), StringComparison.OrdinalIgnoreCase), "ribbon layout must resolve package-relative default icon overrides to the installed package icon path. actual=" + item.IconPath + "; expected=" + expectedIconPath);
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

        private static void ValidateRibbonLayoutConfigurationModels()
        {
            var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            Require(configurationModels.Contains("public string LayoutVersion { get; set; }"), "RibbonConfiguration must expose LayoutVersion.");
            Require(configurationModels.Contains("public List<RibbonPanelLayoutConfiguration> Panels { get; set; }"), "RibbonConfiguration must expose Panels.");
            Require(configurationModels.Contains("public sealed class RibbonPanelLayoutConfiguration"), "Ribbon panel layout configuration must exist.");
            Require(configurationModels.Contains("public sealed class RibbonItemLayoutConfiguration"), "Ribbon item layout configuration must exist.");
            Require(configurationModels.Contains("public string Type { get; set; }"), "Ribbon item layout configuration must expose Type.");
            Require(configurationModels.Contains("public string FeatureId { get; set; }"), "Ribbon item layout configuration must expose FeatureId.");
            Require(configurationModels.Contains("public string DefaultFeatureId { get; set; }"), "Ribbon item layout configuration must expose DefaultFeatureId.");
        }

        private static void ValidateRibbonLayoutComposerShape()
        {
            var composerPath = "src/PlugHub.Framework/Composition/RibbonLayoutComposer.cs";
            var viewModelPath = "src/PlugHub.Framework/Composition/RibbonLayoutViewModel.cs";
            Require(File.Exists(FullPath(composerPath)), "RibbonLayoutComposer must exist.");
            Require(File.Exists(FullPath(viewModelPath)), "RibbonLayoutViewModel must exist.");

            var composer = ReadText(composerPath);
            var viewModel = ReadText(viewModelPath);
            Require(composer.Contains("class RibbonLayoutComposer"), "RibbonLayoutComposer class must exist.");
            Require(composer.Contains("Compose(ViewConfiguration view, IReadOnlyList<FeatureViewModel> features)"), "RibbonLayoutComposer must expose Compose(ViewConfiguration, features).");
            Require(composer.Contains("BuildLegacyLayout"), "RibbonLayoutComposer must preserve legacy group-based layout.");
            Require(composer.Contains("LegacyPanelDisplayKey"), "RibbonLayoutComposer legacy layout must merge panels by final display name.");
            Require(!composer.Contains("new { feature.GroupId, feature.GroupName, feature.GroupOrder }"), "RibbonLayoutComposer legacy layout must not split same-name panels by group id.");
            Require(composer.Contains("BuildConfiguredLayout"), "RibbonLayoutComposer must support configured ribbon panels.");
            Require(composer.Contains("MergeConfiguredPanelsByDisplayName"), "RibbonLayoutComposer configured layout must merge same-name panels.");
            Require(composer.Contains("AppendUnplacedFeatures"), "RibbonLayoutComposer must keep visible unplaced features reachable.");
            Require(composer.Contains("GroupBy(feature => SafeText(feature.GroupName") && composer.Contains("existingPanel.Items.Concat(items)"), "RibbonLayoutComposer must append runtime unplaced features to their resolved group panels instead of one default panel.");
            Require(!composer.Contains("Autodesk.Revit"), "RibbonLayoutComposer must not reference Revit API.");
            Require(viewModel.Contains("public sealed class RibbonLayoutViewModel"), "RibbonLayoutViewModel type must exist.");
            Require(viewModel.Contains("public const string PushButton = \"pushButton\""), "Ribbon layout item type constants must include pushButton.");
            Require(viewModel.Contains("public const string PulldownButton = \"pulldownButton\""), "Ribbon layout item type constants must include pulldownButton.");
            Require(viewModel.Contains("public const string SplitButton = \"splitButton\""), "Ribbon layout item type constants must include splitButton.");
            Require(viewModel.Contains("public const string Stack = \"stack\""), "Ribbon layout item type constants must include stack.");
        }

        private static void ValidateConfiguredRibbonLayoutAppendsUnplacedFeaturesByGroup()
        {
            var ribbon = new PlugHub.Framework.Configuration.RibbonConfiguration
            {
                TabName = "PlugHub",
                FallbackPanelName = "默认",
                Panels = new List<PlugHub.Framework.Configuration.RibbonPanelLayoutConfiguration>
                {
                    new PlugHub.Framework.Configuration.RibbonPanelLayoutConfiguration
                    {
                        Id = "view-tools",
                        Name = "视图工具",
                        Order = 100,
                        Items = new List<PlugHub.Framework.Configuration.RibbonItemLayoutConfiguration>
                        {
                            new PlugHub.Framework.Configuration.RibbonItemLayoutConfiguration
                            {
                                Type = "pushButton",
                                Id = "grid",
                                FeatureId = "view.grid",
                                Order = 100
                            }
                        }
                    }
                }
            };
            var view = new PlugHub.Framework.Configuration.ViewConfiguration { Ribbon = ribbon };
            var features = new List<PlugHub.Framework.Composition.FeatureViewModel>
            {
                new PlugHub.Framework.Composition.FeatureViewModel { FeatureId = "view.grid", DisplayName = "轴网显隐", GroupId = "view", GroupName = "视图工具", GroupOrder = 100, DisplayOrder = 100 },
                new PlugHub.Framework.Composition.FeatureViewModel { FeatureId = "view.reference-plane", DisplayName = "参照平面显隐", GroupId = "view", GroupName = "视图工具", GroupOrder = 100, DisplayOrder = 200 },
                new PlugHub.Framework.Composition.FeatureViewModel { FeatureId = "mep.filter", DisplayName = "机电过滤", GroupId = "mep", GroupName = "机电工具", GroupOrder = 200, DisplayOrder = 100 }
            };

            var layout = new PlugHub.Framework.Composition.RibbonLayoutComposer().Compose(view, features);
            var viewPanel = layout.Panels.SingleOrDefault(panel => panel.Name == "视图工具");
            var mepPanel = layout.Panels.SingleOrDefault(panel => panel.Name == "机电工具");

            Require(viewPanel != null && viewPanel.Items.SelectMany(item => item.ClickableFeatures()).Any(feature => feature.FeatureId == "view.reference-plane"), "configured ribbon layout must append new view features to the existing 视图工具 panel.");
            Require(mepPanel != null && mepPanel.Items.SelectMany(item => item.ClickableFeatures()).Any(feature => feature.FeatureId == "mep.filter"), "configured ribbon layout must create the 机电工具 panel for unplaced MEP features.");
            Require(!layout.Panels.Any(panel => panel.Name == "默认"), "configured ribbon layout must not collect grouped unplaced package features under 默认.");
        }

        private static void ValidateRibbonLayoutRules()
        {
            var views = ReadObject("config/views.example.json");
            var modules = AllModules().ToList();
            var featureIds = new HashSet<string>(
                modules
                    .SelectMany(Features)
                    .Select(feature => StringValue(feature, "id"))
                    .Where(featureId => !string.IsNullOrWhiteSpace(featureId)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var view in Views(views))
            {
                if (!TryObjectValue(view, "ribbon", out var ribbon)) continue;
                if (!TryArrayValue(ribbon, "panels", out var panels)) continue;

                var panelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var panelObject in panels.Cast<object>())
                {
                    var panel = panelObject as Dictionary<string, object>;
                    Require(panel != null, "ribbon panel layout entries must be objects.");
                    var panelId = StringValue(panel!, "id");
                    Require(!string.IsNullOrWhiteSpace(panelId), "ribbon panel layout id is required.");
                    Require(panelIds.Add(panelId), "duplicate ribbon panel layout id: " + panelId);
                    ValidateRibbonLayoutItems(ArrayValue(panel!, "items"), featureIds, new HashSet<string>(StringComparer.OrdinalIgnoreCase), panelId);
                }
            }
        }

        private static void ValidateRibbonLayoutItems(IEnumerable items, ISet<string> featureIds, ISet<string> containerIds, string location)
        {
            if (items == null) return;
            foreach (var itemObject in items.Cast<object>())
            {
                var item = itemObject as Dictionary<string, object>;
                Require(item != null, "ribbon layout item must be an object at " + location);
                var type = StringValue(item!, "type").Trim();
                Require(!string.IsNullOrWhiteSpace(type), "ribbon layout item type is required at " + location);

                if (string.Equals(type, "pushButton", StringComparison.OrdinalIgnoreCase))
                {
                    var featureId = StringValue(item!, "featureId");
                    Require(featureIds.Contains(featureId), "ribbon layout references missing featureId: " + featureId);
                    continue;
                }

                var id = StringValue(item!, "id");
                Require(!string.IsNullOrWhiteSpace(id), "ribbon container id is required at " + location);
                Require(containerIds.Add(id), "duplicate ribbon container id in panel " + location + ": " + id);

                var children = ArrayValue(item!, "items").Cast<object>().ToList();
                if (string.Equals(type, "pulldownButton", StringComparison.OrdinalIgnoreCase))
                {
                    Require(children.Count >= 1, "pulldownButton must contain at least one child: " + id);
                    ValidateRibbonLayoutItems(children, featureIds, containerIds, id);
                    continue;
                }

                if (string.Equals(type, "splitButton", StringComparison.OrdinalIgnoreCase))
                {
                    Require(children.Count >= 2, "splitButton must contain at least two children: " + id);
                    ValidateRibbonLayoutItems(children, featureIds, containerIds, id);
                    var defaultFeatureId = StringValue(item!, "defaultFeatureId");
                    Require(string.IsNullOrWhiteSpace(defaultFeatureId) || ChildrenContainFeatureId(children, defaultFeatureId), "splitButton defaultFeatureId must reference one child feature: " + id);
                    continue;
                }

                if (string.Equals(type, "stack", StringComparison.OrdinalIgnoreCase))
                {
                    Require(children.Count >= 2 && children.Count <= 3, "stack must contain two or three children: " + id);
                    foreach (var child in children)
                    {
                        var childMap = child as Dictionary<string, object>;
                        Require(childMap != null, "stack child item must be an object: " + id);
                        var childType = StringValue(childMap!, "type");
                        Require(string.Equals(childType, "pushButton", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(childType, "pulldownButton", StringComparison.OrdinalIgnoreCase), "stack supports pushButton and pulldownButton children only: " + id);
                    }

                    ValidateRibbonLayoutItems(children, featureIds, containerIds, id);
                    continue;
                }

                Require(false, "unsupported ribbon layout item type: " + type);
            }
        }

        private static bool ChildrenContainFeatureId(IEnumerable<object> children, string featureId)
        {
            return children
                .Select(child => child as Dictionary<string, object>)
                .Where(child => child != null)
                .Any(child => string.Equals(StringValue(child!, "featureId"), featureId, StringComparison.OrdinalIgnoreCase));
        }

        private static void ValidateRibbonLayoutSettingsRows()
        {
            var viewModel = ReadText("src/PlugHub.Manager/Settings/FrameworkSettingsViewModel.cs");
            var nodeRowPath = "src/PlugHub.Manager/Settings/Rows/RibbonLayoutNodeRow.cs";
            var poolRowPath = "src/PlugHub.Manager/Settings/Rows/RibbonFeaturePoolRow.cs";
            Require(File.Exists(FullPath(nodeRowPath)), "RibbonLayoutNodeRow must exist.");
            Require(File.Exists(FullPath(poolRowPath)), "RibbonFeaturePoolRow must exist.");
            var nodeRow = ReadText(nodeRowPath);
            var poolRow = ReadText(poolRowPath);
            Require(viewModel.Contains("RibbonLayoutNodes"), "settings view model must expose RibbonLayoutNodes.");
            Require(viewModel.Contains("RibbonFeaturePool"), "settings view model must expose RibbonFeaturePool.");
            Require(nodeRow.Contains("ObservableCollection<RibbonLayoutNodeRow> Children"), "RibbonLayoutNodeRow must expose child nodes.");
            Require(nodeRow.Contains("ToPanelConfiguration"), "RibbonLayoutNodeRow must convert panel nodes to configuration.");
            Require(nodeRow.Contains("ToItemConfiguration"), "RibbonLayoutNodeRow must convert item nodes to configuration.");
            Require(poolRow.Contains("FeatureId") && poolRow.Contains("ModuleName"), "RibbonFeaturePoolRow must identify feature and module.");
            Require(poolRow.Contains("IsPlaced") && poolRow.Contains("DisplayText"), "RibbonFeaturePoolRow must expose placement state for the layout canvas.");
        }

        private static void ValidateRevitRibbonAdapter()
        {
            var adapterText = ReadAllCSharp("src/PlugHub.Revit2020");
            if (!adapterText.Contains("FeatureCommandDispatcher") || !adapterText.Contains("FeatureSlotRegistry"))
            {
                ValidateRuntimeRoutingSpecification();
            }

            foreach (var token in new[] { "CreateRibbonTab", "CreateRibbonPanel", "PushButtonData", "PulldownButtonData", "SplitButtonData", "FeatureRibbonBuilder", "FrameworkFeatureCommand", "FeatureCommandDispatcher", "FeatureSlotRegistry" })
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
            var logger = ReadText("src/PlugHub.Framework/Diagnostics/PlugHubLogger.cs");
            var exporter = ReadText("src/PlugHub.Framework/Diagnostics/PlugHubLogExporter.cs");
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

        private static void ValidateNet48ShadowCopyCommandLoader(string featureDispatcher)
        {
            const string commandAssemblyLoaderPath = "src/PlugHub.Revit2020/CommandAssemblyLoader.cs";
            Require(File.Exists(FullPath(commandAssemblyLoaderPath)), "runtime routing must keep the net48 command loading strategy in CommandAssemblyLoader.cs.");

            var loader = ReadText(commandAssemblyLoaderPath);
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

        private static void ValidateRevit2025AlcReadinessSpecification()
        {
            var revitText = ReadAllCSharp("src/PlugHub.Revit2020");
            var alcRules = ReadText("src/PlugHub.Contracts/Loading/AlcLoadRules.cs");
            var readme = ReadText("README.md");

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

        private static void ValidateManifestAuthoritativeDiscoverySpecification()
        {
            var discovery = ReadText("src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs");
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

        private static void ValidateRuntimeLoadsPackagesWhenConfigFilesAreMissing()
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

        private static void ValidateRuntimeToleratesStaleConfigurationFiles()
        {
            ValidateRuntimeLoadsPackagesWhenExistingSourcesOmitPackageDirectories();
            ValidateRuntimeComposesPackagesWhenExistingViewFiltersAreStale();
            ValidateRuntimeLoadsPackagesRewrittenBySettingsSerializer();
        }

        private static void ValidateRuntimeLoadsPackagesWhenExistingSourcesOmitPackageDirectories()
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

        private static void ValidateRuntimeComposesPackagesWhenExistingViewFiltersAreStale()
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

        private static void ValidateRuntimeLoadsPackagesRewrittenBySettingsSerializer()
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
                Path.Combine(packageDirectory, "packages.json"),
                "{\"schemaVersion\":\"1.1\",\"modules\":[{\"id\":\"runtime-isolation-module\",\"version\":\"V1.0.0\",\"features\":[{\"id\":\"" + featureId + "\",\"displayName\":\"" + featureId + "\"}]}]}");
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
            Require(modulesText.Contains("\"provider\": \"github\"") && modulesText.Contains("\"repository\": \"GaoMengGu/PlugHub_Packages\""), "default repository must use owner/repository shorthand for the public cloud PlugHub_Packages source.");
            Require(modulesText.Contains("\"provider\": \"local\""), "repository examples must include a local folder source.");
            Require(modulesText.Contains("\"manifestPath\": \"packages.json\""), "repository examples must point at packages.json.");

            var revitText = ReadAllCSharp("src/PlugHub.Revit2020");
            var settingsUiText = ReadAllCSharp("src/PlugHub.Manager");
            Require(!revitText.Contains("RegisterDockablePane") && !revitText.Contains("DockablePaneProviderData") && !revitText.Contains("IDockablePaneProvider"), "settings and feature UI must not use Revit DockablePane for this architecture.");
            Require(!revitText.Contains("class FrameworkSettingsWindow") && !Directory.Exists(FullPath("src/PlugHub.Revit2020/Settings")), "Revit adapter must not own the full settings UI source.");
            Require(settingsUiText.Contains("FrameworkSettingsWindow") && settingsUiText.Contains(": Window"), "settings UI must live in PlugHub.Manager as a WPF window.");
            Require(!Directory.Exists(FullPath("src/PlugHub.SettingsApp")) && !Directory.Exists(FullPath("src/PlugHub.SettingsUi")), "legacy SettingsApp/SettingsUi source directories must be removed after Manager rename.");
            Require(revitText.Contains("FeatureExecutionGate"), "feature execution must be gated by latest runtime configuration.");
        }

        private static void ValidateSettingsPaneV21Specification()
        {
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var settingsViewModel = ReadText("src/PlugHub.Manager/Settings/FrameworkSettingsViewModel.cs");
            var settingsCommand = ReadText("src/PlugHub.Revit2020/FrameworkSettingsCommand.cs");
            var externalSettingsLauncher = ReadText("src/PlugHub.Revit2020/ExternalManagerLauncher.cs");
            var externalSettingsProject = ReadText("src/PlugHub.Manager/PlugHub.Manager.csproj");
            var settingsAppProgram = ReadText("src/PlugHub.Manager/Program.cs");
            var settingsStore = ReadText("src/PlugHub.Framework/Settings/SettingsConfigurationStore.cs");
            var repositoryPackageRow = ReadText("src/PlugHub.Manager/Settings/Rows/RepositoryPackageRow.cs");
            var statusWindow = ReadText("src/PlugHub.Wpf/FrameworkStatusWindow.cs");
            var featureCommand = ReadText("src/PlugHub.Revit2020/FrameworkFeatureCommand.cs");
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var runtime = ReadText("src/PlugHub.Framework/Runtime/FrameworkRuntime.cs");
            var ribbonDesignerDropService = ReadText("src/PlugHub.Manager/Settings/RibbonDesigner/RibbonDesignerDropService.cs");
            var sourceResolver = ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            var revitProject = ReadText("src/PlugHub.Revit2020/PlugHub.Revit2020.csproj");

            Require(!File.Exists(FullPath("src/PlugHub.Revit2020/FrameworkSettingsForm.cs")), "legacy WinForms settings form must be removed.");
            Require(!File.Exists(FullPath("src/PlugHub.Revit2020/FrameworkSettingsPane.cs")), "legacy DockablePane settings provider must be removed.");
            Require(!ReadAllCSharp("src/PlugHub.Revit2020").Contains("System.Windows.Forms") && !ReadAllCSharp("src/PlugHub.Revit2020").Contains("WindowsFormsHost"), "Revit settings/feature UI must not reference WinForms hosting.");
            Require(settingsCommand.Contains("ExternalManagerLauncher") && settingsCommand.Contains("TryLaunch") && settingsCommand.Contains("FrameworkStatusWindow"), "settings ribbon command must launch the PlugHub Manager and report failures through WPF status.");
            Require(!settingsCommand.Contains("FrameworkSettingsWindow") && !settingsCommand.Contains("ShowDialog") && !settingsCommand.Contains("FrameworkConfigurationLoader.LoadFromDirectory"), "Revit settings ribbon command must not host the full settings window or load editable settings in-process.");
            Require(!File.Exists(FullPath("src/PlugHub.Revit2020/FrameworkExternalSettingsCommand.cs")), "parallel Windows settings command must be removed after settings becomes the external-app entry.");
            Require(!settingsCommand.Contains("GetDockablePane") && !settingsCommand.Contains("pane.Hide") && !settingsCommand.Contains("pane.Show"), "settings command must not toggle a DockablePane.");
            Require(externalSettingsLauncher.Contains("PlugHub.Manager.exe") && externalSettingsLauncher.Contains("--config") && externalSettingsLauncher.Contains("--hostProcessId") && externalSettingsLauncher.Contains("FrameworkRuntimeState.BaseDirectory"), "external settings launcher must locate PlugHub.Manager.exe and pass the runtime config directory plus Revit host process id.");
            Require(externalSettingsProject.Contains("<TargetFramework>net48</TargetFramework>") && externalSettingsProject.Contains("<OutputType>WinExe</OutputType>") && externalSettingsProject.Contains("PresentationFramework"), "PlugHub Manager must be a net48 WPF Windows executable.");
            Require(externalSettingsProject.Contains("<AssemblyName>PlugHub.Manager</AssemblyName>") && !externalSettingsProject.Contains("ProjectReference Include=\"..\\PlugHub.Revit2020") && !externalSettingsProject.Contains("Autodesk.Revit"), "PlugHub Manager must be the WPF manager executable without depending on the Revit adapter project or Revit API.");
            Require(!externalSettingsProject.Contains("PlugHub.SettingsApp") && !externalSettingsProject.Contains("PlugHub.SettingsUi") && !externalSettingsProject.Contains("SharedSettings"), "PlugHub Manager project must not keep legacy SettingsApp/SettingsUi naming or linked Revit settings sources.");
            Require(settingsAppProgram.Contains("ManagerMaintenanceArguments.Parse") && settingsAppProgram.Contains("ManagerMaintenanceRunner") && settingsAppProgram.Contains("FrameworkSettingsWindow") && settingsAppProgram.Contains("new FrameworkRuntime().Load(configDirectory, ShouldApplyPendingPackageOperations(hostProcessId))") && !File.Exists(FullPath("src/PlugHub.Manager/SettingsMainWindow.cs")), "PlugHub Manager must route maintenance mode before loading a local runtime snapshot and hosting the existing FrameworkSettingsWindow.");
            Require(settingsAppProgram.Contains("ShouldApplyPendingPackageOperations") && settingsAppProgram.Contains("Process.GetProcessById(hostProcessId)"), "PlugHub Manager must only apply pending package operations when the associated Revit host is not running.");
            Require(settingsAppProgram.Contains("TryAcquireSingleInstance") && settingsAppProgram.Contains("new Mutex(true") && settingsAppProgram.Contains("SingleInstanceMutexName(configDirectory)"), "PlugHub Manager normal mode must be single-instance per configuration directory so settings ribbon clicks and EXE launches do not open duplicate managers.");
            Require(settingsWindow.Contains("var isRevitHostRunning = IsRevitHostProcessRunning();") && settingsWindow.Contains("!IsRevitHostProcessRunning()) return false") && repositoryPackageRow.Contains("bool isRevitHostRunning, bool isLoadedInCurrentRuntime"), "external Manager must separate Revit host liveness from runtime-loaded package state so absent Revit does not create false pending restart states.");
            Require(settingsStore.Contains("namespace PlugHub.Framework.Settings") && settingsStore.Contains("public sealed class SettingsConfigurationStore") && !settingsStore.Contains("Autodesk.Revit"), "shared settings store must live in PlugHub.Framework.Settings and stay Revit-independent.");
            Require(featureCommand.Contains("ShowRuntimeStatus"), "status command must use the focused runtime status view.");
            Require(featureCommand.Contains("FrameworkStatusWindow") && !featureCommand.Contains("TaskDialog.Show"), "framework fallback feature feedback must use WPF.");
            Require(ribbonBuilder.Contains("LoadFeatureIcon") && ribbonBuilder.Contains("LargeImage"), "configured feature icons must be applied to Revit ribbon buttons.");
            Require(ribbonBuilder.Contains("FrameworkSettingsCommand"), "framework Ribbon panel must expose settings command.");
            Require(!ribbonBuilder.Contains("FrameworkStatusCommand") && !ribbonBuilder.Contains("PlugHub_Framework_Status") && !ribbonBuilder.Contains("\"状态\""), "framework Ribbon panel must not expose a status button; Revit keeps only the settings light entry.");
            Require(!ribbonBuilder.Contains("FrameworkExternalSettingsCommand") && !ribbonBuilder.Contains("PlugHub_Framework_ExternalSettings") && !ribbonBuilder.Contains("Windows设置"), "framework Ribbon panel must not expose a duplicate Windows settings entry.");
            Require(!runtime.Contains(".Browse(") && !runtime.Contains("RepositoryBrowser") && !runtime.Contains("RepositoryArchiveSynchronizer") && !runtime.Contains("FrameworkUpdateService"), "Revit startup runtime must not access remote repositories or framework update services.");

            foreach (var token in new[] { "class FrameworkSettingsWindow", ": Window", "TabControl", "DataGrid", "BuildRibbonLayoutTab", "BuildRepositoriesTab", "RepositoryRow", "RepositoryPackageRow", "GroupRow", "ReloadFromDisk", "ContextMenu", "DragDrop", "Microsoft.Win32.OpenFileDialog" })
            {
                Require(settingsWindow.Contains(token), "WPF settings UI token missing: " + token);
            }

            Require(settingsWindow.Contains("BuildRibbonLayoutTab"), "settings window must expose a Ribbon layout tab.");
            Require(settingsWindow.Contains("LoadRibbonLayoutRows"), "settings window must load ribbon layout rows.");
            Require(settingsWindow.Contains("ApplyRibbonLayoutRows"), "settings window must save ribbon layout rows.");
            Require(settingsWindow.Contains("ResetDefaultRibbonLayout"), "settings window must reset to the framework default layout.");
            Require(File.Exists(FullPath("src/PlugHub.Manager/Settings/RibbonDesigner/RibbonDesignerNodeRow.cs")), "visual ribbon designer node row must exist.");
            Require(File.Exists(FullPath("src/PlugHub.Manager/Settings/RibbonDesigner/RibbonDesignerFeatureRow.cs")), "visual ribbon designer feature row must exist.");
            Require(File.Exists(FullPath("src/PlugHub.Manager/Settings/RibbonDesigner/RibbonDesignerMapper.cs")), "visual ribbon designer mapper must exist.");
            Require(File.Exists(FullPath("src/PlugHub.Manager/Settings/RibbonDesigner/RibbonDesignerDropService.cs")), "visual ribbon designer drop service must exist.");
            Require(File.Exists(FullPath("src/PlugHub.Manager/Settings/RibbonDesigner/RibbonLayoutDiffService.cs")), "visual ribbon designer diff service must exist.");
            Require(settingsViewModel.Contains("RibbonDesignerFeatures"), "FrameworkSettingsViewModel must expose visual designer feature rows.");
            Require(settingsViewModel.Contains("RibbonDesignerTabs"), "FrameworkSettingsViewModel must expose visual designer layout tabs.");
            Require(settingsWindow.Contains("BuildVisualRibbonDesignerTab"), "settings layout page must use visual ribbon designer.");
            Require(!settingsWindow.Contains("BuildRibbonDesignerFeaturePool"), "settings layout page must not expose a separate feature list.");
            Require(!settingsWindow.Contains("_ribbonDesignerFeatureList"), "settings layout page must not keep a separate feature list control.");
            Require(settingsWindow.Contains("BuildRibbonDesignerCanvas"), "settings layout page must expose WYSIWYG ribbon canvas.");
            Require(settingsWindow.Contains("BuildRibbonDesignerPropertyPanel"), "settings layout page must expose selected-element properties.");
            Require(settingsWindow.Contains("BuildRibbonDesignerEditorBody"), "settings layout page must use a top canvas and bottom property editor.");
            Require(settingsWindow.Contains("RefreshRibbonDesignerChangeSummary"), "settings layout page must summarize unsaved layout changes without a preview grid.");
            Require(settingsWindow.Contains("RefreshRibbonDesignerCanvas"), "settings window must refresh the visual Ribbon canvas after layout changes.");
            Require(settingsWindow.Contains("RefreshRibbonDesignerLayoutState"), "settings window must refresh layout state without a separate feature pool.");
            Require(settingsWindow.Contains("RibbonDesignerNodeTypeOptions") && settingsWindow.Contains("常规按钮"), "visual designer must show localized control type names and label pushButton as 常规按钮.");
            Require(settingsWindow.Contains("DefaultRibbonDesignerPanelName") && settingsWindow.Contains("\"默认\""), "visual designer must keep unplaced features in a 默认 panel.");
            Require(settingsWindow.Contains("EnsureAllVisibleFeaturesInRibbonDesignerLayout"), "visual designer must automatically place all visible installed features in the canvas.");
            Require(settingsWindow.Contains("MoveRibbonDesignerChildrenToDefaultPanel"), "removing a layout container must return contained features to the 默认 panel.");
            Require(settingsWindow.Contains("ResolveRibbonDesignerDropPlan"), "visual designer must move existing canvas items directly between containers.");
            Require(settingsWindow.Contains("InsertRibbonDesignerNode"), "visual designer must insert moved or new items at the resolved drop position.");
            Require(!settingsWindow.Contains("RibbonDesignerFeatureListDrop") && !settingsWindow.Contains("RemoveRibbonDesignerFeatureFromCanvas"), "visual designer must not remove functions by dragging them to a separate feature list.");
            Require(settingsWindow.Contains("BuildRibbonDesignerIconSelector"), "visual designer must use one unified icon selector for custom and built-in icons.");
            Require(settingsWindow.Contains("IsEditable = true") && settingsWindow.Contains("MaxDropDownHeight"), "unified icon selector must accept custom paths and keep built-in choices bounded.");
            Require(settingsWindow.Contains("RibbonDesignerIconOptions") && settingsWindow.Contains("RibbonDesignerBrowseIconAction") && settingsWindow.Contains("RibbonDesignerClearIconAction"), "unified icon selector must keep browse and clear actions inside the icon dropdown.");
            Require(!settingsWindow.Contains("BuildRibbonDesignerIconActions"), "visual designer must not expose separate icon action buttons outside the icon dropdown.");
            Require(settingsWindow.Contains("CombineRibbonDesignerPushButtons"), "visual designer must support direct drag-to-combine for canvas push buttons.");
            Require(settingsWindow.Contains("CreateRibbonDesignerStackFromDrop"), "visual designer must create a stack when a push button is dropped onto another push button.");
            Require(settingsWindow.Contains("ResolveRibbonDesignerEventNode") && settingsWindow.Contains("IsRibbonDesignerDirectEventNode"), "visual designer drag/drop must resolve the direct node from OriginalSource so parent containers do not steal child drags.");
            Require(settingsWindow.Contains("ResolveRibbonDesignerDropTarget"), "visual designer must resolve drops to the nearest valid parent container for cross-panel moves.");
            Require(settingsWindow.Contains("FindRibbonDesignerParent"), "visual designer must know parent containers when resolving drag targets.");
            Require(settingsWindow.Contains("BuildRibbonDesignerPanelDropSurface"), "visual designer panels must expose a stable drop surface above the bottom title.");
            Require(settingsWindow.Contains("Grid.SetRow(title, 1)"), "visual designer panel title must stay at the bottom, including empty panels.");
            Require(settingsWindow.Contains("RibbonDesignerPanelPreviewMinWidth") && settingsWindow.Contains("IsSinglePushButtonRibbonDesignerPanel"), "visual designer panels with one regular button must shrink instead of keeping the multi-button panel width.");
            Require(settingsWindow.Contains("items.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center)"), "visual designer panel contents must center when a panel has a single regular button.");
            Require(settingsWindow.Contains("BuildRibbonDesignerCanvasMenu") && settingsWindow.Contains("重新生成默认布局"), "visual designer layout operations must move to the canvas context menu.");
            Require(settingsWindow.Contains("CommitSelectedRibbonDesignerPropertiesFromEditor") && settingsWindow.Contains("SelectedRibbonDesignerTextLostFocus"), "visual designer properties must auto-apply editor changes without an apply button.");
            Require(settingsWindow.Contains("BuildFooter") && settingsWindow.Contains("Grid.SetColumn(_statusText, 0)"), "settings status text must be shown at the bottom left.");
            Require(!settingsWindow.Contains("_ribbonDesignerChangeSummary"), "layout change status must be merged into the bottom-left settings status text.");
            Require(settingsWindow.Contains("RefreshRibbonDesignerChangeSummary") && settingsWindow.Contains("RefreshStatus(message)"), "layout change summary must write to the shared bottom-left status text.");
            Require(settingsWindow.Contains("工具栏布局"), "layout canvas title must use a user-facing Chinese label.");
            Require(settingsWindow.Contains("BuildRibbonDesignerLargeButtonPreview") && settingsWindow.Contains("BuildRibbonDesignerSmallButtonPreview"), "visual designer canvas must distinguish large panel buttons from small stacked/menu buttons.");
            Require(settingsWindow.Contains("BuildRibbonDesignerContainerPreview") && settingsWindow.Contains("BuildRibbonDesignerStackPreview"), "visual designer canvas must render dropdown, split, and stack controls with Ribbon-like forms.");
            Require(settingsWindow.Contains("BuildRibbonDesignerSelectionChrome") && settingsWindow.Contains("RibbonDesignerNodeMouseLeftButtonUp"), "visual designer canvas must refresh selected-state chrome for containers as well as regular buttons.");
            Require(settingsWindow.Contains("BuildRibbonDesignerDropArrow"), "visual designer canvas must show dropdown affordances on dropdown and split controls.");
            Require(settingsWindow.Contains("_expandedRibbonDesignerNodeIds") && settingsWindow.Contains("ToggleRibbonDesignerContainerExpansion"), "visual designer canvas must simulate dropdown and split expand/collapse without persisting preview state.");
            Require(settingsWindow.Contains("LoadRibbonDesignerIcon") && settingsWindow.Contains("LoadConfiguredRibbonDesignerIcon") && settingsWindow.Contains("DefaultRibbonIconProvider.Create"), "visual designer canvas must load configured icons and fall back to built-in defaults.");
            Require(settingsWindow.Contains("ModuleBaseDirectory = module.ResolvedBaseDirectory") && settingsWindow.Contains("feature.ModuleBaseDirectory") && settingsWindow.Contains("ResolveRibbonDesignerPackageIconPath"), "visual designer must resolve package-relative icons against the installed package base directory.");
            Require(settingsWindow.Contains("CanEditRibbonDesignerDisplayName") && settingsWindow.Contains("_selectedRibbonDesignerText.IsEnabled = CanEditRibbonDesignerDisplayName"), "visual designer properties must make control display names read-only while keeping feature button names editable.");
            Require(settingsWindow.Contains("SelectedRibbonDesignerTextKeyDown") && settingsWindow.Contains("CommitSelectedRibbonDesignerPropertiesFromEditor()"), "visual designer display-name editor must commit on Enter.");
            Require(settingsWindow.Contains("CommitSelectedRibbonDesignerPropertiesBeforeCanvasInteraction"), "visual designer canvas clicks must commit pending property edits before changing selection.");
            Require(settingsWindow.Contains("CanEditRibbonDesignerIcon") && settingsWindow.Contains("_selectedRibbonDesignerIcon.IsEnabled = CanEditRibbonDesignerIcon"), "visual designer properties must only allow feature button icons to be edited.");
            Require(settingsWindow.Contains("NormalizeInvalidRibbonDesignerStacksForSave"), "settings save must remove empty stacks and unwrap single-button stacks before writing Revit layout.");
            Require(settingsWindow.Contains("ValidateNoNestedRibbonDesignerStacks"), "settings save must reject nested stacks.");
            Require(settingsWindow.Contains("CanConvertRibbonDesignerNodeType") && settingsWindow.Contains("不能在堆叠中嵌套堆叠"), "visual designer must block converting stack children into stacks.");
            Require(settingsWindow.Contains("CanConvertRibbonDesignerNodeType") && settingsWindow.Contains("下拉按钮和拆分按钮内部只能放常规按钮"), "visual designer must block converting pulldown/split children into containers.");
            Require(settingsWindow.Contains("RibbonDesignerNodeTypeOptions(row)") && settingsWindow.Contains("RibbonDesignerAllowedNodeTypes"), "visual designer must hide invalid type choices for the selected parent container.");
            Require(ribbonDesignerDropService.Contains("CanStackContainNode") && ribbonDesignerDropService.Contains("CanPulldownOrSplitContainNode"), "visual designer drop service must name Ribbon containment rules explicitly.");
            Require(settingsWindow.Contains("LoadDllSiblingRibbonDesignerIcon"), "visual designer canvas must auto-load same-name icons beside feature DLLs before falling back to default icons.");
            Require(settingsWindow.Contains("常规按钮不能移除，只能拖动位置。") && !settingsWindow.Contains("需要隐藏功能请在插件管理中处理"), "visual designer push button removal message must stay concise and not reference plugin management.");
            Require(ribbonDesignerDropService.Contains("RibbonDesignerNodeRow.SplitButton"), "visual designer stack drop rules must allow split buttons because Revit AddStackedItems supports them.");
            Require(!settingsWindow.Contains("panel.Children.Add(EditorLabel(\"按钮大小\"))"), "layout property panel must not expose button size.");
            Require(!settingsWindow.Contains("_selectedRibbonDesignerSize"), "visual designer must infer button size from layout structure instead of editing it directly.");
            Require(!settingsWindow.Contains("BuildRibbonDesignerAddButton"), "layout page must not expose the old add-layout-item toolbar button.");
            Require(!settingsWindow.Contains("CreateButton(\"应用属性\""), "layout property panel must not expose a manual apply button.");
            Require(!settingsWindow.Contains("CreateButton(\"移除所选\""), "layout property panel must not expose a manual remove button.");
            Require(!settingsWindow.Contains("CreateButton(\"恢复默认布局\""), "layout page must not expose reset-default as a toolbar button.");
            Require(!settingsWindow.Contains("BuildRibbonLayoutDiffPreview"), "layout page must not reserve canvas space for a low-value change preview grid.");
            Require(!settingsWindow.Contains("_ribbonLayoutDiffGrid"), "layout page must not keep the old change preview grid.");
            Require(!settingsWindow.Contains("_selectedRibbonDesignerIconPath"), "visual designer must not split custom icon path from the unified icon selector.");
            Require(!settingsWindow.Contains("_selectedRibbonDesignerBuiltinIcon"), "visual designer must not split built-in icon selection from the unified icon selector.");
            Require(!settingsWindow.Contains("EditorLabel(\"内置图标\")"), "visual designer properties must not expose a separate built-in icon field.");
            Require(!settingsWindow.Contains("RibbonFeatureLayoutRow"), "visual designer must not use flat feature intent rows as the main layout model.");
            Require(!settingsWindow.Contains("BuildRibbonIntentLayoutTab"), "visual designer must replace the old intent layout tab.");
            Require(!settingsWindow.Contains("ApplyRibbonFeatureIntentRows"), "visual designer must not save through flat feature intent rows.");
            Require(!settingsWindow.Contains("BuildIntentRibbonLayoutNodes"), "visual designer must not synthesize layout from flat feature intent rows.");
            Require(!settingsWindow.Contains("LiveAgent"), "first-stage visual designer must not claim live Revit UI mutation.");
            Require(!settingsWindow.Contains("RadioGroupTemplate"), "first-stage visual designer must not ship unsupported RadioGroup UI.");
            Require(!settingsWindow.Contains("TextBoxRibbonItem"), "first-stage visual designer must not ship unsupported Ribbon TextBox items.");
            Require(!settingsWindow.Contains("ComboBoxRibbonItem"), "first-stage visual designer must not ship unsupported Ribbon ComboBox items.");
            Require(!settingsWindow.Contains("RibbonLayoutModeOption"), "visual designer must not add mode-switching complexity.");
            Require(!settingsWindow.Contains("_ribbonLayoutModeCombo"), "visual designer must not rely on layout modes.");
            Require(!settingsWindow.Contains("BuildRibbonCanvasMoreButton"), "visual designer must not expose a Ribbon-node more menu.");
            Require(!settingsWindow.Contains("与后一个组合为"), "visual designer must not expose manual combine commands.");
            Require(!settingsWindow.Contains("ToggleRibbonAdvancedProperties"), "visual designer must not expose internal diagnostics in the main layout flow.");
            Require(!settingsWindow.Contains("BuildRibbonPanelWorkbench"), "settings layout page must not expose the button-heavy panel workbench.");
            Require(!settingsWindow.Contains("BuildPanelActions") && !settingsWindow.Contains("BuildPanelItemActions"), "settings layout page must not expose button-heavy panel and item action bars.");
            Require(!settingsWindow.Contains("_ribbonPanelList") && !settingsWindow.Contains("_ribbonPanelItemList"), "settings layout page must not split editing into panel and current-item list boxes.");
            Require(!settingsWindow.Contains("_ribbonMoveTargetPanelCombo"), "settings layout page must not require a separate move-target combo.");
            Require(!settingsWindow.Contains("BuildReadOnlyRibbonPreview"), "settings layout page must make the Ribbon canvas the editor instead of a read-only preview.");
            Require(settingsWindow.Contains("FeatureIdExistsInRibbonLayout"), "settings window must keep duplicate feature placement guarded for legacy layout parsing.");
            Require(settingsWindow.Contains("ValidateUniqueRibbonFeaturePlacement"), "settings save must validate unique feature placement.");
            Require(settingsWindow.Contains("DefaultRibbonPanelKey"), "default layout must merge features by final panel display key.");
            Require(settingsWindow.Contains("MergeRibbonPanelsByDisplayName"), "settings must merge same-name layout panels before showing the canvas.");
            Require(!settingsWindow.Contains("RibbonTreeMouseMove") && !settingsWindow.Contains("RibbonTreeDrop"), "layout canvas must not depend on TreeView drag/drop.");
            Require(!settingsWindow.Contains("BuildRibbonCanvasItemContextMenu"), "intent layout must not expose Ribbon-node context menus.");
            Require(!settingsWindow.Contains("CombineSelectedRibbonItems"), "intent layout must not expose manual container composition.");
            Require(!settingsWindow.Contains("UngroupSelectedRibbonContainer"), "intent layout must not expose manual container decomposition.");
            Require(!settingsWindow.Contains("EditorLabel(\"ID\")"), "layout property panel must hide internal node id.");
            Require(!settingsWindow.Contains("EditorLabel(\"功能 ID\")"), "layout property panel must hide internal feature id.");
            Require(!settingsWindow.Contains("EditorLabel(\"默认功能 ID\")"), "layout property panel must hide internal default feature id.");
            Require(settingsWindow.Contains("BuildTab(\"布局\""), "settings window must label the ribbon layout tab as layout.");
            Require(!settingsWindow.Contains("tabs.Items.Add(BuildFeaturesTab())"), "settings window must not expose the feature settings tab.");
            Require(!settingsWindow.Contains("tabs.Items.Add(BuildGroupsTab())"), "settings window must not expose the group settings tab.");
            Require(!settingsWindow.Contains("迁移为高级布局") && !settingsWindow.Contains("MigrateBasicRibbonLayout"), "settings window must not expose migration-based layout setup.");
            Require(!settingsWindow.Contains("恢复基础布局") && !settingsWindow.Contains("RestoreBasicRibbonLayout"), "settings window must not expose legacy group layout restore.");

            Require(settingsWindow.Contains("SettingsConfigurationStore"), "FrameworkSettingsWindow must use SettingsConfigurationStore.");
            Require(settingsStore.Contains("ApplyResolvedBaseDirectory") && settingsStore.Contains("module.ResolvedBaseDirectory = baseDirectory"), "settings store must preserve each package manifest directory for Manager icon resolution.");
            Require(settingsWindow.Contains("ExportLogs"), "FrameworkSettingsWindow must expose log export.");
            Require(settingsWindow.Contains("OpenLogsDirectory"), "FrameworkSettingsWindow must expose a focused open-log-folder diagnostic action.");
            Require(settingsWindow.Contains("WriteManagerLog") && settingsWindow.Contains("PH-REPOSITORY-BROWSE") && settingsWindow.Contains("PH-PACKAGE-OPERATION") && settingsWindow.Contains("PH-LOGS-EXPORT"), "FrameworkSettingsWindow must write logs for repository browsing, package operations, and log export.");
            Require(settingsWindow.Contains("LogDiagnostics") && settingsWindow.Contains("new PlugHubLogger().Error(BaseDirectory(), \"PH-SETTINGS\""), "FrameworkSettingsWindow must persist repository diagnostics and settings exceptions to PlugHub logs.");
            Require(!settingsWindow.Contains("BuildLogsTab") && !settingsWindow.Contains("BuildDiagnosticsTab") && !settingsWindow.Contains("BuildTab(\"日志\""), "settings must not expose logs as a primary tab for normal users.");
            Require(!settingsWindow.Contains("Path.Combine(BaseDirectory(), \"logs\", \"plughub-logs.zip\")"), "settings log export target must not be inside the logs directory.");
            Require(settingsWindow.Contains("_configurationStore.Save(_configuration, _moduleDocuments)"), "FrameworkSettingsWindow must save the current in-memory module documents explicitly.");
            Require(settingsWindow.Contains("Path.Combine(BaseDirectory(), \"exports\", \"plughub-logs.zip\")"), "settings log export target must be under the exports directory.");
            Require(settingsWindow.Contains("new PlugHubLogExporter().Export(BaseDirectory(), targetPath)"), "settings log export must call PlugHubLogExporter with BaseDirectory and targetPath.");
            Require(settingsWindow.Contains("_viewModel") && !settingsWindow.Contains("ObservableCollection<ModuleRow> _moduleRows") && !settingsWindow.Contains("ObservableCollection<FeatureRow> _featureRows") && !settingsWindow.Contains("ObservableCollection<GroupRow> _groupRows"), "FrameworkSettingsWindow row state must be held by FrameworkSettingsViewModel.");
            Require(!settingsWindow.Contains("private sealed class ModuleManifestDocument"), "FrameworkSettingsWindow must not keep a stale private ModuleManifestDocument type.");
            foreach (var collection in new[] { "Modules", "Features", "Groups", "Repositories", "RepositoryPackages", "PendingOperations", "Diagnostics" })
            {
                Require(settingsViewModel.Contains("ObservableCollection") && settingsViewModel.Contains(collection), "FrameworkSettingsViewModel must expose " + collection + ".");
            }

            foreach (var forbidden in new[] { "FrameworkRuntimeState.Refresh", "Assembly.LoadFrom" })
            {
                Require(!settingsWindow.Contains(forbidden), "settings window must only save configuration and must not run runtime work: " + forbidden);
            }

            Require(statusWindow.Contains("class FrameworkStatusWindow") && statusWindow.Contains(": Window"), "status and feature fallback UI must use a WPF status window.");
            foreach (var token in new[] { "ShowRuntimeStatus", "ShowLogs", "showLogs" })
            {
                Require(statusWindow.Contains(token), "status window must separate status and log concerns: " + token);
            }
            Require(configurationModels.Contains("PackageRepositoryConfiguration"), "module configuration must expose repository catalog settings.");
            Require(sourceResolver.Contains("AddPackageDirectoryModules"), "package directories must be scanned for drop-in packages manifests.");
            Require(sourceResolver.Contains("FindModuleManifests"), "module directory resolver must discover manifests automatically.");
            Require(sourceResolver.Contains("\"packages.json\"") && sourceResolver.Contains("\"*.packages.json\"") && !sourceResolver.Contains("\"package.json\"") && !sourceResolver.Contains("\"*.package.json\""), "module directory resolver must discover only packages.json and *.packages.json manifests.");
            Require(!sourceResolver.Contains("ProcessStartInfo") && !sourceResolver.Contains("packages/github"), "startup resolver must not access repository caches or run git.");
            Require(!revitProject.Contains("System.Windows.Forms") && !revitProject.Contains("WindowsFormsIntegration"), "Revit adapter should not reference WinForms after moving settings and feature UI to WPF.");
            Require(!revitProject.Contains("PlugHubModuleFiles"), "Revit build must not depend on a source modules folder.");
            Require(revitProject.Contains("packages\\README.md"), "Revit build must create the runtime packages folder.");
        }

        private static void ValidateFrameworkSettingsWindowSectionBoundaries()
        {
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");

            var repositoriesTab = MethodBody(settingsWindow, "BuildRepositoriesTab");
            foreach (var token in new[] { "BuildRepositoryToolbar", "BuildRepositorySourceCards", "BuildRepositoryPackageToolbar", "BuildRepositoryPackageList", "Grid.SetRow" })
            {
                Require(repositoriesTab.Contains(token), "repositories tab must own its section composition: " + token);
            }

            var repositoryToolbar = MethodBody(settingsWindow, "BuildRepositoryToolbar");
            foreach (var token in new[] { "CheckRepositoryUpdates", "AddRepository", "BuildToolbarHeader" })
            {
                Require(repositoryToolbar.Contains(token), "repository toolbar must expose repository source actions: " + token);
            }

            var repositorySources = MethodBody(settingsWindow, "BuildRepositorySourceCards");
            foreach (var token in new[] { "_repositorySourcesList", "BuildRepositoryMenu", "BuildRepositorySourceCardTemplate", "BuildRepositorySourceScrollViewer" })
            {
                Require(repositorySources.Contains(token), "repository source cards must keep source list controls together: " + token);
            }

            var packageToolbar = MethodBody(settingsWindow, "BuildRepositoryPackageToolbar");
            foreach (var token in new[] { "_repositoryPackageSearchText", "_repositoryPackageStateFilter", "_repositoryPackageTagFilter", "RepositoryPackageFilterChanged" })
            {
                Require(packageToolbar.Contains(token), "repository package toolbar must keep package filters together: " + token);
            }

            var packageList = MethodBody(settingsWindow, "BuildRepositoryPackageList");
            foreach (var token in new[] { "_warehousePackageList", "BuildRepositoryPackageMenu", "BuildRepositoryPackageTemplate", "BuildRepositoryPackageItemsPanel" })
            {
                Require(packageList.Contains(token), "repository package list must own package browsing controls: " + token);
            }

            var designerTab = MethodBody(settingsWindow, "BuildVisualRibbonDesignerTab");
            Require(designerTab.Contains("BuildRibbonDesignerEditorBody") && designerTab.Contains("SyncSelectedRibbonDesignerEditor"), "visual ribbon designer tab must build and sync the editor.");

            var designerBody = MethodBody(settingsWindow, "BuildRibbonDesignerEditorBody");
            Require(designerBody.Contains("BuildRibbonDesignerCanvas") && designerBody.Contains("BuildRibbonDesignerPropertyPanel"), "visual ribbon designer body must keep canvas and property editor as distinct sections.");

            var designerCanvas = MethodBody(settingsWindow, "BuildRibbonDesignerCanvas");
            Require(designerCanvas.Contains("_ribbonDesignerCanvas") && designerCanvas.Contains("BuildRibbonDesignerCanvasMenu") && designerCanvas.Contains("ScrollViewer"), "visual ribbon designer canvas must expose a scrollable canvas with its context menu.");

            var designerMenu = MethodBody(settingsWindow, "BuildRibbonDesignerCanvasMenu");
            foreach (var token in new[] { "RibbonDesignerNodeRow.Panel", "RibbonDesignerNodeRow.PulldownButton", "RibbonDesignerNodeRow.SplitButton", "RibbonDesignerNodeRow.Stack", "RemoveSelectedRibbonDesignerNode", "ResetDefaultRibbonLayout" })
            {
                Require(designerMenu.Contains(token), "visual ribbon designer context menu must keep layout operations discoverable: " + token);
            }

            var designerProperties = MethodBody(settingsWindow, "BuildRibbonDesignerPropertyPanel");
            foreach (var token in new[] { "_selectedRibbonDesignerText", "_selectedRibbonDesignerType", "BuildRibbonDesignerIconSelector", "_selectedRibbonDesignerDefaultFeature", "SelectedRibbonDesignerPropertySelectionChanged" })
            {
                Require(designerProperties.Contains(token), "visual ribbon designer property panel must keep selected-node editors together: " + token);
            }

            var aboutTab = MethodBody(settingsWindow, "BuildAboutTab");
            foreach (var token in new[] { "BuildAboutLeftPanel", "BuildAboutAssetPanel", "BuildAboutPathPanel", "BuildAboutDiagnosticsPanel", "ListPendingOperations", "Grid" })
            {
                Require(aboutTab.Contains(token), "about tab must keep framework metadata and diagnostics together: " + token);
            }
            Require(!aboutTab.Contains("ScrollViewer"), "about tab must stay on one page without an overall scrollbar.");

            var aboutLeftPanel = MethodBody(settingsWindow, "BuildAboutLeftPanel");
            foreach (var token in new[] { "BuildAboutHeader", "BuildDonationCodes", "反馈邮箱", "交流群号" })
            {
                Require(aboutLeftPanel.Contains(token), "about tab left panel must keep brand, contact, and donation content together: " + token);
            }

            var aboutHeader = MethodBody(settingsWindow, "BuildAboutHeader");
            Require(aboutHeader.Contains("AssemblyVersionText") && aboutHeader.Contains("CreateIconButton(\"refresh\"") && aboutHeader.Contains("CheckFrameworkUpdate"), "about header must expose framework version and the compact update action.");

            var checkUpdate = MethodBody(settingsWindow, "CheckFrameworkUpdate");
            foreach (var token in new[] { "_frameworkUpdateService.Check", "AssemblyVersionText", "ShowFrameworkUpdateDialog", "UpdateFramework", "_checkFrameworkIconButton.IsEnabled" })
            {
                Require(checkUpdate.Contains(token), "check-update flow must keep metadata query, prompt, and button state together: " + token);
            }

            var updateFramework = MethodBody(settingsWindow, "UpdateFramework");
            Require(updateFramework.Contains("_frameworkUpdateService.Download") && updateFramework.Contains("ManagerMaintenanceLauncher.StartUpdate") && updateFramework.Contains("MaintenanceWaitProcessIds"), "update flow must download and hand off to PlugHub Manager maintenance mode.");

            var updateDialog = MethodBody(settingsWindow, "ShowFrameworkUpdateDialog");
            Require(updateDialog.Contains("ReleaseNotesText") && updateDialog.Contains("LatestVersion") && updateDialog.Contains("DialogResult"), "update dialog must show target version, release notes, and return a user decision.");
        }

        private static void ValidateSettingsRibbonCleanupSpecification()
        {
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var settingsStore = ReadText("src/PlugHub.Framework/Settings/SettingsConfigurationStore.cs");
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var addinTemplate = ReadText("manifests/PlugHub.addin.template");
            var buildProps = ReadText("build/Directory.Build.props");
            var views = ReadObject("config/views.example.json");

            Require(settingsWindow.Contains("LoadModuleDocuments") && !settingsWindow.Contains(RemovedSamplesDirectory()), "settings must not reference removed sample module manifests.");
            Require(settingsStore.Contains("Save(") && settingsStore.Contains("ModuleManifestDocument"), "settings must save edits back to their owning module manifest through SettingsConfigurationStore.");
            Require(!settingsStore.Contains("Save(configuration, LoadModuleDocuments(configuration))"), "SettingsConfigurationStore must not expose a Save overload that reloads module documents from disk.");
            Require(settingsStore.Contains("foreach (var document in moduleDocuments)") && settingsStore.Contains("SaveModuleDocument(document)"), "SettingsConfigurationStore Save must persist the provided moduleDocuments through the document-aware save path.");
            Require(settingsStore.Contains("IsModulesManifestFileName(Path.GetFileName(document.Path))") && settingsStore.Contains("SavePackageManifest(document.Path, document.Modules)") && settingsStore.Contains("SaveJson(document.Path, document.Modules)"), "SettingsConfigurationStore must write package manifests through the package writer while preserving sources.json as full runtime configuration.");
            Require(settingsStore.Contains("PackageManifestWriter"), "SettingsConfigurationStore must use the current package manifest writer for packages.json and adjacent package manifests.");
            Require(settingsStore.Contains("NormalizePackageManifestDefaults"), "SettingsConfigurationStore must normalize package manifests before saving so omitted module state is not serialized as disabled.");
            Require(settingsStore.Contains("AdjacentPackageManifestPattern = \"*.packages.json\""), "settings configuration store must discover adjacent *.packages.json manifests.");
            Require(settingsWindow.Contains("Name = DefaultGroupDisplayName(module, feature)") && settingsWindow.Contains("GroupIdForFeature(module, feature)") && settingsWindow.Contains("module.Category"), "settings layout defaults must derive stable group ids from module category and display panel names from module displayName.");
            Require(!settingsWindow.Contains("nameof(FeatureRow.Panel)") && !settingsWindow.Contains("feature.Group = row.Panel"), "feature settings must not expose user-editable panel ownership.");
            Require(!settingsWindow.Contains("点击 Ribbon 的「刷新配置」"), "settings UI must not point users to the removed refresh Ribbon button.");

            Require(ribbonBuilder.Contains("\"PlugHub_Framework_Settings\""), "Ribbon must keep the settings entry.");
            Require(!ribbonBuilder.Contains("\"PlugHub_Framework_Refresh\"") && !ribbonBuilder.Contains("\"刷新配置\""), "Ribbon must not expose refresh configuration as a full settings substitute.");
            Require(!ribbonBuilder.Contains("\"PlugHub_Framework_Status\"") && !ribbonBuilder.Contains("\"状态\""), "Ribbon must not expose a status button.");

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
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");

            foreach (var token in new[] { "BuildVisualRibbonDesignerTab", "RibbonDesignerTabs", "BuildRibbonDesignerEditorBody", "BuildRibbonDesignerCanvas", "BuildRibbonDesignerPropertyPanel", "RefreshRibbonDesignerChangeSummary", "ResetDefaultRibbonLayout", "CreateDefaultRibbonLayoutNodes", "DefaultRibbonPanelKey", "ApplyRibbonLayoutRows", "RefreshRibbonDesignerCanvas" })
            {
                Require(settingsWindow.Contains(token), "settings must manage Ribbon layout from the layout tab: " + token);
            }

            foreach (var forbidden in new[] { "新建模块", "新建功能", "private void AddModule(", "private void AddFeature(", "CreateModule(", "CreateFeature(", "所属模块", "ModuleIdsForFeatureRows" })
            {
                Require(!settingsWindow.Contains(forbidden), "settings must not create placeholder modules/features or expose module placement: " + forbidden);
            }

            Require(!settingsWindow.Contains("TextColumn(nameof(ModuleRow.Order)") && !settingsWindow.Contains("TextColumn(nameof(FeatureRow.Order)") && !settingsWindow.Contains("TextColumn(nameof(GroupRow.Order)"), "settings must not expose raw numeric order columns.");
            Require(settingsWindow.Contains("PositionText") && settingsWindow.Contains("RefreshPluginPackagePositions") && settingsWindow.Contains("RefreshFeaturePositions") && settingsWindow.Contains("RefreshGroupPositions"), "settings internal rows must show human-readable position text and maintain runtime ordering.");
            Require(!settingsWindow.Contains("BuildTab(\"功能\"") && !settingsWindow.Contains("BuildTab(\"分组\""), "settings must not expose separate feature or group tabs.");
            foreach (var rowClass in new[] { "ModuleRow", "FeatureRow", "GroupRow", "RepositoryRow", "RepositoryPackageRow", "DiagnosticRow" })
            {
                Require(!settingsWindow.Contains("private sealed class " + rowClass), rowClass + " must be extracted from FrameworkSettingsWindow.");
            }

            Require(settingsWindow.Contains("PendingPackageOperationsStatusText"), "settings window must report pending package operations through the footer status text.");
            Require(!settingsWindow.Contains("BuildPendingPackageOperationsSummary"), "settings window must not show a dedicated pending restart operation list.");
            Require(!settingsWindow.Contains("CancelSelectedPendingPackageOperation"), "settings window must not expose pending operation cancellation in the settings UI.");
            Require(settingsWindow.Contains("ListPendingOperations(BaseDirectory())"), "settings window must still read pending package operations for status reminders.");
            var packageOperationStart = settingsWindow.IndexOf("private void RunRepositoryPackageOperation(", StringComparison.Ordinal);
            var packageOperationResult = packageOperationStart < 0 ? -1 : settingsWindow.IndexOf("var result = operation(row.ToDescriptor());", packageOperationStart, StringComparison.Ordinal);
            var packageOperationStatus = packageOperationResult < 0 ? -1 : settingsWindow.IndexOf("RefreshStatusWithPendingPackageOperations(result.Message)", packageOperationResult, StringComparison.Ordinal);
            Require(packageOperationStart >= 0 && packageOperationResult >= 0 && packageOperationStatus > packageOperationResult, "repository package operations must refresh footer pending-operation status.");

            var ribbonDesignerMapper = ReadText("src/PlugHub.Manager/Settings/RibbonDesigner/RibbonDesignerMapper.cs");
            Require(!settingsWindow.Contains("body.Children.Add(BuildRibbonDesignerPreviewButton(tab"), "layout canvas must not render a synthetic PlugHub tab button above panels.");
            Require(ribbonDesignerMapper.Contains("GroupBy(DefaultPanelKey") && ribbonDesignerMapper.Contains("GroupDisplayText") && ribbonDesignerMapper.Contains("ModuleName"), "layout designer default panels must match runtime grouped ribbon layout.");
            Require(ribbonDesignerMapper.Contains("featuresById") && ribbonDesignerMapper.Contains("IconPathForDisplay") && ribbonDesignerMapper.Contains("feature.IconPath"), "layout designer configured rows must hydrate current package feature icons.");
        }

        private static void ValidateDefaultIconSpecification()
        {
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var iconProvider = ReadText("src/PlugHub.Wpf/DefaultRibbonIconProvider.cs");
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var modulesText = ReadText("config/sources.example.json");

            Require(ribbonBuilder.Contains("DefaultRibbonIconProvider") && ribbonBuilder.Contains("CreateSmallIcon") && ribbonBuilder.Contains("CreateLargeIcon"), "Ribbon builder must apply built-in default small/large icons.");
            Require(ribbonBuilder.Contains("\"PlugHub_Framework_Settings\"") && ribbonBuilder.Contains("\"settings\""), "settings ribbon button must use a built-in settings icon.");
            Require(!ribbonBuilder.Contains("\"PlugHub_Framework_Status\"") && !ribbonBuilder.Contains("FrameworkStatusCommand"), "status ribbon button must not be rendered.");
            Require(ribbonBuilder.Contains("LoadConfiguredIcon"), "Ribbon builder must resolve configured file icons and built-in icon keys.");
            Require(ribbonBuilder.Contains("CreateSizedRasterIcon") && ribbonBuilder.Contains("large ? 32 : 16") && ribbonBuilder.Contains("image.DecodePixelWidth"), "Ribbon builder must resize configured package icons to fixed 16/32 canvases so stacked small buttons are not clipped.");
            Require(ribbonBuilder.Contains("LoadDllSiblingIcon") && ribbonBuilder.Contains("SameNameIconExtensions"), "Ribbon builder must auto-load same-name icons beside feature DLLs before falling back to defaults.");
            Require(ribbonBuilder.Contains("AddSingleStackChildFallback"), "Ribbon builder must render a single-item stack as its child instead of dropping the feature.");
            Require(ribbonBuilder.Contains("CreateContainerButtonData") && ribbonBuilder.Contains("ApplyRibbonItemIcon"), "Ribbon builder must apply configured icons to container ribbon buttons.");
            Require(iconProvider.Contains("CreateSmallIcon") && iconProvider.Contains("CreateLargeIcon"), "default icon provider must expose small and large icon factories.");
            Require(iconProvider.Contains("BuiltinIconKeys") && iconProvider.Contains("FeatureIconKeys") && iconProvider.Contains("UiIconKeys"), "default icon provider must split feature icon choices from UI button icons.");
            Require(iconProvider.Contains("\"upgrade\"") && iconProvider.Contains("DrawUpgrade"), "default icon provider must expose a distinct small upgrade arrow icon.");
            Require(iconProvider.Contains("settings") && iconProvider.Contains("duct") && iconProvider.Contains("family"), "default icon provider must expose UI and feature icon suites.");
            Require(settingsWindow.Contains("BuildBuiltinIconMenu") && settingsWindow.Contains("SetSelectedFeatureBuiltinIcon"), "settings must let users choose built-in feature icons.");
            var featureIconOptions = MethodBody(settingsWindow, "BuiltinIconOptions");
            Require(featureIconOptions.Contains("DefaultRibbonIconProvider.FeatureIconKeys") && !featureIconOptions.Contains("DefaultRibbonIconProvider.BuiltinIconKeys"), "feature icon menus must not include UI-only icons such as settings/save/install/about.");
            Require(!modulesText.Contains("commandAssembly"), "framework config must not ship command-backed feature entries.");
        }

        private static void ValidateRasterBrandIconSpecification()
        {
            var iconProvider = ReadText("src/PlugHub.Wpf/DefaultRibbonIconProvider.cs");
            var wpfProject = ReadText("src/PlugHub.Wpf/PlugHub.Wpf.csproj");
            var managerProject = ReadText("src/PlugHub.Manager/PlugHub.Manager.csproj");
            var installerProject = ReadText("src/PlugHub.Installer/PlugHub.Installer.csproj");
            var installerForm = ReadText("src/PlugHub.Installer/InstallerForm.cs");
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");

            foreach (var rootIcon in new[] { "SETTINGS.png", "LOGO.png", "LOGO.ico" })
            {
                Require(!File.Exists(FullPath(rootIcon)), "brand icon assets must not remain at the repository root: " + rootIcon);
            }

            foreach (var wpfResource in new[] { "src/PlugHub.Wpf/Resources/SETTINGS.png", "src/PlugHub.Wpf/Resources/LOGO.png" })
            {
                Require(File.Exists(FullPath(wpfResource)), "shared WPF icon resource is missing: " + wpfResource);
            }

            Require(File.Exists(FullPath("src/PlugHub.Manager/Resources/LOGO.ico")), "Manager executable icon resource is missing.");
            Require(File.Exists(FullPath("src/PlugHub.Installer/Resources/LOGO.ico")), "Installer executable icon resource is missing.");
            Require(wpfProject.Contains("Resources\\SETTINGS.png") && wpfProject.Contains("Resources\\LOGO.png"), "PlugHub.Wpf must embed the raster settings and logo PNG resources.");
            Require(managerProject.Contains("<ApplicationIcon>Resources\\LOGO.ico</ApplicationIcon>"), "PlugHub.Manager must use the logo ICO as its executable icon.");
            Require(managerProject.Contains("Resources\\LOGO.ico"), "PlugHub.Manager project must include the logo ICO resource.");
            Require(installerProject.Contains("<ApplicationIcon>Resources\\LOGO.ico</ApplicationIcon>"), "PlugHub.Installer must use the logo ICO as its executable icon.");
            Require(installerForm.Contains("Icon =") && installerForm.Contains("Application.ExecutablePath"), "PlugHub installer window must apply the executable logo icon to the form.");
            Require(iconProvider.Contains("SettingsResourcePath") && iconProvider.Contains("LogoResourcePath") && iconProvider.Contains("CreateRasterIcon"), "default ribbon icon provider must load the supplied settings and logo raster resources.");
            Require(iconProvider.Contains("CreatePaddedRasterIcon") && iconProvider.Contains("Brushes.Transparent") && iconProvider.Contains("size - padding * 2"), "raster ribbon icons must render inside a fixed transparent canvas with safe padding so Revit does not clip the supplied artwork.");
            Require(settingsWindow.Contains("DefaultRibbonIconProvider.CreateLogoIcon") && settingsWindow.Contains("BuildHeaderLogo"), "PlugHub Manager must apply the logo to the window and header.");
        }

        private static void ValidateRevitWpfUiDesignSpecification()
        {
            var theme = ReadText("src/PlugHub.Wpf/RevitUiTheme.cs");
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var statusWindow = ReadText("src/PlugHub.Wpf/FrameworkStatusWindow.cs");
            var iconProvider = ReadText("src/PlugHub.Wpf/DefaultRibbonIconProvider.cs");
            var buildScript = ReadText("scripts/build-revit2020.ps1");

            Require(theme.Contains("class RevitUiPalette") && theme.Contains("class RevitUiTheme"), "Revit WPF UI must centralize theme tokens in RevitUiTheme.");
            Require(theme.Contains("UIThemeManager") && theme.Contains("AppsUseLightTheme"), "Revit WPF UI theme detection must prefer Revit host theme and fall back to Windows app theme.");
            Require(theme.Contains("ButtonStyle") && theme.Contains("TabItem") && theme.Contains("DataGridRow"), "Revit WPF UI theme must provide shared styles for buttons, tabs, and grids.");
            Require(theme.Contains("resources.Add(typeof(ComboBoxItem), ComboBoxItemStyle(palette))") && theme.Contains("ComboBoxItemTemplate"), "Revit WPF UI theme must explicitly style ComboBox dropdown items instead of leaving selected items on system colors.");
            Require(theme.Contains("ComboBoxTemplate(palette)") && theme.Contains("SelectionBoxItem") && theme.Contains("PART_Popup") && theme.Contains("PART_EditableTextBox") && theme.Contains("ComboBoxToggleTemplate"), "Revit WPF UI theme must explicitly template closed and editable ComboBox states so dark theme selectors cannot remain white.");
            Require(theme.Contains("ComboBoxItem.IsHighlightedProperty") && theme.Contains("Selector.IsSelectedProperty") && theme.Contains("Control.BackgroundProperty, palette.SelectionBrush") && theme.Contains("Control.ForegroundProperty, palette.TextBrush"), "ComboBox dropdown hover and selected states must keep readable themed foreground/background colors.");
            Require(theme.Contains("SystemColors.WindowBrushKey") && theme.Contains("SystemColors.HighlightBrushKey") && theme.Contains("palette.ControlBackground"), "ComboBox dropdown popups must override WPF system window/highlight colors so dark theme menus cannot remain white.");
            Require(theme.Contains("ContentTemplateSelectorProperty") && theme.Contains("ContentPresenter.ContentTemplateSelectorProperty"), "custom ComboBox item templates must preserve DisplayMemberPath through the generated content template selector.");
            Require(theme.Contains("TabItemTemplate(palette)") && theme.Contains("ControlTemplate(typeof(TabItem))") && theme.Contains("RootBorder") && theme.Contains("Control.BorderBrushProperty, palette.AccentBrush"), "selected settings tabs must use an explicit template so WPF system colors cannot turn the selected tab white.");
            Require(theme.Contains("MenuItemTemplate") && theme.Contains("PART_Popup") && theme.Contains("SubmenuArrow"), "context menus must use a compact MenuItem template without the default icon slot.");
            Require(settingsWindow.Contains("RevitUiTheme.Apply(this)") && statusWindow.Contains("RevitUiTheme.Apply(this)"), "settings and status windows must share the Revit WPF theme.");
            Require(settingsWindow.Contains("public override string ToString()") && settingsWindow.Contains("return DisplayText;"), "layout designer combo option objects must fall back to user-facing labels if WPF ignores DisplayMemberPath.");
            Require(settingsWindow.Contains("BuildAboutTab") && settingsWindow.Contains("tabs.Items.Add(BuildAboutTab())"), "settings window must include an About tab.");
            Require(settingsWindow.Contains("BuildAboutBadge") && settingsWindow.Contains("BuildAboutInfoRow") && settingsWindow.Contains("Revit 2020"), "About tab must show concise project/runtime metadata.");
            Require(settingsWindow.Contains("核心作者") && settingsWindow.Contains("GaoMengGu") && settingsWindow.Contains("https://qm.qq.com/q/NN2psby1cQ") && settingsWindow.Contains("https://github.com/GaoMengGu/PlugHub"), "About tab must show updated author and clickable community/source links.");
            Require(settingsWindow.Contains("欢迎请作者喝一杯咖啡") && settingsWindow.Contains("☕") && settingsWindow.Contains("Width = 108") && settingsWindow.Contains("Height = 108"), "About tab must use the updated coffee support copy and larger payment QR codes.");
            var aboutTab = MethodBody(settingsWindow, "BuildAboutTab");
            Require(!aboutTab.Contains("ScrollViewer"), "About tab must fit in one page without triggering an overall scrollbar.");
            Require(!settingsWindow.Contains("BuildValidationCommandRow") && !settingsWindow.Contains("复制指令"), "About diagnostics must not expose developer-only static-validation command copying to normal users.");
            Require(settingsWindow.Contains("BuildButtonContent") && settingsWindow.Contains("IconKeyForButtonText"), "settings window buttons must use consistent vector icon content where appropriate.");
            Require(iconProvider.Contains("\"about\"") && iconProvider.Contains("\"repository\"") && iconProvider.Contains("\"layout\""), "built-in icon suite must include common settings/about/repository/layout icons.");
            Require(!Directory.Exists(FullPath("src/PlugHub.Revit2020/Resources")), "Revit adapter must not keep obsolete file-based icon resources.");
            Require(buildScript.Contains("(Join-Path $OutputDir \"Resources\")"), "Revit build must remove stale generated Resources output after file-based icons are removed.");
        }

        private static void ValidateSettingsGroupFeatureEditingBehavior()
        {
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var ribbonLayoutComposer = ReadText("src/PlugHub.Framework/Composition/RibbonLayoutComposer.cs");

            Require(settingsWindow.Contains("LoadFeatureRows") && settingsWindow.Contains("LoadGroupRows"), "settings must load feature and group rows as layout data sources.");
            Require(settingsWindow.Contains("BuildRibbonDesignerPropertyPanel") && settingsWindow.Contains("CommitSelectedRibbonDesignerPropertiesFromEditor"), "layout tab must edit selected visual designer element without exposing Ribbon node internals.");
            Require(settingsWindow.Contains("RefreshFeaturePositionsByGroup"), "feature ordering must be recalculated per workspace group.");
            Require(settingsWindow.Contains("SortFeatureRowsForRuntimeOrder"), "feature grid must be ordered the same way runtime ribbon composition is ordered.");
            Require(settingsWindow.Contains("ShouldRemoveEmptyRibbonDesignerContainer") && settingsWindow.Contains("RemoveUnavailableRibbonDesignerFeatures(child, visibleFeatureIds)"), "layout designer must remove empty containers and panels after unavailable feature nodes are pruned.");
            Require(settingsWindow.Contains("IsInteractiveGridEditor"), "row drag behavior must ignore combo boxes, text boxes, check boxes, and buttons.");
            Require(settingsWindow.Contains("TrySave") && settingsWindow.Contains("ReportSettingsError"), "settings save must catch exceptions and report them inline.");
            Require(settingsWindow.Contains("SafeRefreshGrid") && settingsWindow.Contains("IsEditTransactionRefreshError"), "settings grid refresh must be safe during DataGrid edit transactions.");
            foreach (var forbiddenRefresh in new[] { "_featuresGrid.Items.Refresh", "_groupsGrid.Items.Refresh", "_repositoriesGrid.Items.Refresh", "_repositoryPackagesGrid.Items.Refresh", "_pluginPackagesGrid.Items.Refresh" })
            {
                Require(!settingsWindow.Contains(forbiddenRefresh), "settings grid refresh must not call Items.Refresh directly: " + forbiddenRefresh);
            }

            Require(!settingsWindow.Contains("MessageBox.Show"), "settings window must not show pop-up prompts for normal settings operations.");
            Require(!settingsWindow.Contains("BuildInstalledPackagesTab") && !settingsWindow.Contains("BuildPluginPackagesTab") && !settingsWindow.Contains("ApplyPluginPackageRows();"), "settings window must not expose the installed package settings tab.");
            Require(ribbonBuilder.Contains("new RibbonLayoutComposer().Compose")
                && ribbonLayoutComposer.Contains(".OrderBy(feature => feature.DisplayOrder)")
                && ribbonLayoutComposer.Contains(".ThenBy(feature => feature.FeatureId"),
                "Ribbon layout composer must explicitly order features inside each panel.");
        }

        private static void ValidatePackageSourceAndReleaseBehavior()
        {
            var modulesText = ReadText("config/sources.example.json");
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var repositorySettingsController = ReadText("src/PlugHub.Manager/Settings/RepositorySettingsController.cs");
            var settingsMetrics = ReadText("src/PlugHub.Manager/Settings/SettingsMetrics.cs");
            var repositoryRow = ReadText("src/PlugHub.Manager/Settings/Rows/RepositoryRow.cs");
            var repositoryPackageRow = ReadText("src/PlugHub.Manager/Settings/Rows/RepositoryPackageRow.cs");
            var sourceResolver = ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var configurationLoader = ReadText("src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs");
            var packageRepositoryService = ReadText("src/PlugHub.Framework/Packages/PackageRepositoryService.cs");
            var repositoryAddress = ReadText("src/PlugHub.Framework/Packages/RepositoryAddress.cs");
            var repositoryBrowser = ReadText("src/PlugHub.Framework/Packages/RepositoryBrowser.cs");
            var repositoryArchiveSynchronizer = ReadText("src/PlugHub.Framework/Packages/RepositoryArchiveSynchronizer.cs");
            var packageManifestReader = ReadText("src/PlugHub.Framework/Packages/PackageManifestReader.cs");
            var packageInstallService = ReadText("src/PlugHub.Framework/Packages/PackageInstallService.cs");
            var frameworkUpdateService = ReadText("src/PlugHub.Framework/Updates/FrameworkUpdateService.cs");
            var credentialService = ReadText("src/PlugHub.Framework/Packages/RepositoryCredentialService.cs");
            var redactor = ReadText("src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs");
            var configurationModels = ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            var workflow = ReadText(".github/workflows/release.yml");
            var giteeWorkflow = ReadText(".github/workflows/sync-gitee.yml");
            var buildScript = ReadText("scripts/build-revit2020.ps1");
            var readme = ReadText("README.md");

            Require(modulesText.Contains("\"provider\": \"github\"") && modulesText.Contains("\"repository\": \"GaoMengGu/PlugHub_Packages\""), "default package repository must use owner/repository shorthand for the public cloud PlugHub_Packages repository.");
            Require(modulesText.Contains("\"displayName\": \"PlugHub 公共插件仓库\""), "default package repository examples must show custom displayName usage.");
            Require(modulesText.Contains("\"provider\": \"local\"") && modulesText.Contains("本地文件夹插件仓库"), "default package repository examples must include a local folder repository form.");
            Require(modulesText.Contains("\"packageDirectories\": [") && modulesText.Contains("\"packages\""), "installed package discovery must point at packages.");
            Require(!modulesText.Contains("packages/github/GaoMengGu_PlugHub_Packages"), "repository caches must not live under packages.");
            Require(!modulesText.Contains("GaoMengGu/PlugHub_Modules"), "default github source must not point at PlugHub_Modules.");
            Require(settingsWindow.Contains("DefaultRepositoryProvider = \"github\"") && settingsWindow.Contains("DefaultPublicRepository = \"GaoMengGu/PlugHub_Packages\""), "settings repository creation must default to the owner/repository cloud PlugHub_Packages source.");

            Require(!sourceResolver.Contains("RunGit") && !sourceResolver.Contains("AutoUpdate") && !sourceResolver.Contains("AddGitHubModules"), "runtime source resolver must not pull or load repository packages at startup.");
            Require(settingsWindow.Contains("BuildRepositoriesTab") && settingsWindow.Contains("LoadRepositoryRows"), "settings must present sources as repositories.");
            Require(settingsWindow.Contains("BrowseSelectedRepository") && settingsWindow.Contains("InstallSelectedRepositoryPackage"), "settings must browse repositories and install selected packages.");
            Require(settingsWindow.Contains("UpdateSelectedRepositoryPackage") && settingsWindow.Contains("UninstallSelectedRepositoryPackage"), "settings must support repository package update and uninstall.");
            Require(settingsWindow.Contains("LoadCachedRepositoryPackages") && settingsWindow.Contains("CheckRepositoryUpdates") && settingsWindow.Contains("Task.Run"), "settings must show cached repository packages and allow explicit remote update checks.");
            Require(!settingsWindow.Contains("StartRepositoryUpdateCheck"), "settings must not start remote repository checks automatically when the settings window opens.");
            foreach (var token in new[] { "ApplyPackageFilters", "SortRepositoryPackages", "PrimaryActionFor", "BuildSearchText", "RepositoryDisplayName" })
            {
                Require(repositorySettingsController.Contains(token), "repository settings controller must own package browsing behavior: " + token);
            }
            Require(settingsMetrics.Contains("CountUniqueModules") && settingsMetrics.Contains("CountUniqueFeatures") && settingsMetrics.Contains("CountEnabledRepositories") && settingsMetrics.Contains("RepositoryDisplayName"), "settings metrics must centralize unique module/feature counts, enabled repository count, and repository display-name fallback.");

            foreach (var token in new[] { "BuildRepositorySourceCards", "BuildRepositoryPackageList", "BuildRepositoryDiagnosticsMenu" })
            {
                Require(settingsWindow.Contains(token), "repository settings UI must use package-manager layout: " + token);
            }

            Require(settingsWindow.Contains("RepositorySettingsDefaultWidth = 1140.0") && settingsWindow.Contains("Width = RepositorySettingsDefaultWidth"), "settings window must default to the requested 1140 width.");
            Require(settingsWindow.Contains("RepositorySettingsDefaultHeight = 600.0") && settingsWindow.Contains("Height = RepositorySettingsDefaultHeight"), "settings window must default to the requested 600 height.");
            Require(settingsWindow.Contains("SettingsWindowOuterMargin = 12.0") && settingsWindow.Contains("Margin = new Thickness(SettingsWindowOuterMargin)"), "settings window outer margin must be a shared constant used by layout width calculations.");
            Require(settingsWindow.Contains("SettingsWindowOuterMarginWidth = SettingsWindowOuterMargin * 2.0") && settingsWindow.Contains("RepositoryCardRowChromeReserve = 60.0"), "repository card row width must reserve root margins and tab chrome at the default window width.");
            Require(settingsWindow.Contains("RepositoryCardRowWidth = RepositorySettingsDefaultWidth - SettingsWindowOuterMarginWidth - RepositoryCardRowChromeReserve"), "repository card row width must fit within the default settings content area so four source cards do not trigger the horizontal scrollbar.");
            Require(settingsWindow.Contains("RepositorySourceColumns = 4.0") && settingsWindow.Contains("RepositoryPackageColumns = 3"), "repository layout must target four source cards and three package cards per row.");
            Require(settingsWindow.Contains("RepositoryPackageCardVerticalMargin = 4.0") && settingsWindow.Contains("RepositoryCardHorizontalMargin = RepositoryPackageCardVerticalMargin"), "repository card horizontal half-gap must match the package card vertical half-gap.");
            Require(settingsWindow.Contains("RepositoryCardHorizontalMarginWidth = RepositoryCardHorizontalMargin * 2.0"), "repository card horizontal margins must be shared across source and package cards.");
            Require(settingsWindow.Contains("RepositorySourceScrollbarSafetyReserve = 16.0") && settingsWindow.Contains("RepositorySourceCardRowWidth = RepositoryCardRowWidth - RepositorySourceScrollbarSafetyReserve"), "repository source cards must keep a small safety reserve so four default cards do not trigger the horizontal scrollbar.");
            Require(!settingsWindow.Contains("RepositoryPackageGridSafetyReserve"), "repository package cards must not use a fixed safety reserve that leaves large side gaps at wider manager widths.");
            Require(settingsWindow.Contains("RepositoryPackageCardWidthBinding") && settingsWindow.Contains("RepositoryPackageCardWidthConverter"), "repository package cards must calculate width from the package list ActualWidth.");
            Require(settingsWindow.Contains("RepositoryPackageScrollbarSafetyReserve") && settingsWindow.Contains("RepositoryPackageCardMinWidth"), "repository package card width calculation must reserve scrollbar space and keep a stable minimum card width.");
            Require(settingsWindow.Contains("RepositorySourceCardSlotWidth = RepositorySourceCardRowWidth / RepositorySourceColumns"), "repository source cards must keep their default-width safety-reserved slot calculation.");
            Require(settingsWindow.Contains("RepositorySourceCardWidth = RepositorySourceCardSlotWidth - RepositoryCardHorizontalMarginWidth"), "repository source cards must subtract shared horizontal margins.");
            Require(settingsWindow.Contains("BuildRepositorySourceScrollViewer") && settingsWindow.Contains("ScrollViewer.CanContentScrollProperty, false"), "repository source cards must be hosted in an explicit horizontal ScrollViewer so overflow can be scrolled.");
            Require(settingsWindow.Contains("BuildRepositorySourceMoreGlyph") && settingsWindow.Contains("ToolTip") && settingsWindow.Contains("CheckBox"), "repository source cards must use compact glyph actions and a checkbox enabled state.");
            Require(settingsWindow.Contains("AddRepositoryEditorRow(form, 0, \"名称\", customName)") && settingsWindow.Contains("AddRepositoryEditorRow(form, 7, \"Token\", apiKey)") && !settingsWindow.Contains("AddRepositoryEditorRow(form, 7, \"ApiKey\", apiKey)"), "repository source editor must expose a custom name field and label the credential field as Token for users.");
            Require(settingsWindow.Contains("LineStackingStrategy.BlockLineHeight") && settingsWindow.Contains("VerticalAlignmentProperty, VerticalAlignment.Top"), "repository source ellipsis glyph must align tightly to the top-right of the card.");
            Require(!settingsWindow.Contains("new Binding(nameof(RepositoryRow.Status))"), "repository source cards must not duplicate footer status text.");
            Require(!settingsWindow.Contains("RepositoryEnabledLabelConverter"), "repository source cards must not spend card width on enable/disable text buttons.");
            Require(settingsWindow.Contains("RepositorySourceSelectionChanged"), "repository package list must follow the selected repository source card.");
            Require(settingsWindow.Contains("ToolTipProperty, new Binding(nameof(RepositoryRow.DisplayName))"), "repository source card title must show the full repository name as a tooltip.");
            Require(settingsWindow.Contains("云端仓库 · ") && settingsWindow.Contains("本地文件夹 · ") && !settingsWindow.Contains("provider + \" / \" + visibility + \" / \" + state"), "repository source metadata must avoid provider branding, duplicated enabled state, and slash-separated labels.");
            Require(settingsWindow.Contains("BuildRepositorySourceMoreGlyph") && settingsWindow.Contains("OpenRepositorySourceMenuFromCard"), "repository source cards must use a shared ellipsis menu for secondary actions.");
            Require(settingsWindow.Contains("BrowseRepositorySourceCacheFromCard") && settingsWindow.Contains("_packageRepositoryService.BrowseCached(BaseDirectory(), row.ToConfiguration()"), "clicking a repository source card must browse its local cached packages without remote sync.");
            Require(settingsWindow.Contains("同步仓库源") && !settingsWindow.Contains("同步仓库插件包") && settingsWindow.Contains("编辑仓库源") && settingsWindow.Contains("删除仓库"), "repository source menu must expose edit, sync, and delete actions with source-level wording.");
            Require(settingsWindow.Contains("RevitUiTheme.Current.DangerBrush"), "repository source delete menu item must be visually highlighted as destructive.");
            var repositoryToolbar = MethodBody(settingsWindow, "BuildRepositoryToolbar");
            Require(repositoryToolbar.Contains("一键同步") && !repositoryToolbar.Contains("浏览所选") && !repositoryToolbar.Contains("检查更新"), "repository toolbar must expose manual sync-all and remove redundant repository actions.");
            Require(!settingsWindow.Contains("LoadCachedRepositoryPackages();"), "settings must not auto-populate repository packages before the user manually syncs repositories.");
            var sourceTemplate = MethodBody(settingsWindow, "BuildRepositorySourceCardTemplate");
            Require(sourceTemplate.Contains("border.SetValue(Border.WidthProperty, RepositorySourceCardWidth)") && sourceTemplate.Contains("border.SetValue(Border.MarginProperty, new Thickness(RepositoryCardHorizontalMargin, RepositoryPackageCardVerticalMargin, RepositoryCardHorizontalMargin, RepositorySourceCardBottomMargin))"), "repository source card horizontal gaps must match package card vertical gaps while preserving source-row bottom spacing.");
            var packageItemsPanel = MethodBody(settingsWindow, "BuildRepositoryPackageItemsPanel");
            Require(packageItemsPanel.Contains("new FrameworkElementFactory(typeof(UniformGrid))") && packageItemsPanel.Contains("UniformGrid.ColumnsProperty") && packageItemsPanel.Contains("RepositoryPackageColumns"), "repository package list must use a fixed three-column UniformGrid so seven repository packages render as three rows instead of two columns.");
            Require(settingsWindow.Contains("_warehousePackageList.ItemContainerStyle = BuildRepositoryPackageItemContainerStyle()") && settingsWindow.Contains("RepositoryPackageItemContainerStyle"), "repository package list must remove default ListBoxItem chrome so three cards fit predictably.");
            Require(settingsWindow.Contains("_warehousePackageList.HorizontalContentAlignment = HorizontalAlignment.Center") && settingsWindow.Contains("FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center"), "repository package card grid must keep three columns centered with equal left/right spacing.");
            Require(settingsWindow.Contains("RepositoryPackageActionWidth = 72.0") && settingsWindow.Contains("RepositoryPackageActionHeight = 26.0"), "repository package card action buttons must stay compact with fixed dimensions.");
            var packageTemplate = MethodBody(settingsWindow, "BuildRepositoryPackageTemplate");
            Require(packageTemplate.Contains("var border = new FrameworkElementFactory(typeof(Border))") && packageTemplate.Contains("border.SetBinding(Border.WidthProperty, RepositoryPackageCardWidthBinding())") && packageTemplate.Contains("border.SetValue(Border.MarginProperty, new Thickness(RepositoryCardHorizontalMargin, RepositoryPackageCardVerticalMargin, RepositoryCardHorizontalMargin, RepositoryPackageCardVerticalMargin))"), "repository package card width must bind to the live manager package list width while preserving compact card gaps.");
            Require(packageTemplate.Contains("border.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8))") && packageTemplate.Contains("border.SetValue(Border.BorderThicknessProperty, new Thickness(1))"), "repository package cards must draw their own border with the same card padding as repository source cards.");
            Require(!packageTemplate.Contains("var slot = new FrameworkElementFactory(typeof(Border))") && packageTemplate.Contains("return new DataTemplate { VisualTree = border }"), "repository package cards must not wrap the real card in a fixed-width slot.");
            Require(!packageTemplate.Contains("rightEdge") && !packageTemplate.Contains("Panel.ZIndexProperty"), "repository package cards must not use overlay edge workarounds.");
            Require(packageTemplate.Contains("var row = new FrameworkElementFactory(typeof(DockPanel))") && packageTemplate.Contains("var actionRail = new FrameworkElementFactory(typeof(Border))"), "repository package cards must use a valid WPF action rail instead of fake Grid column definitions.");
            Require(packageTemplate.Contains("actionRail.SetValue(DockPanel.DockProperty, Dock.Right)") && packageTemplate.Contains("actionRail.SetValue(FrameworkElement.WidthProperty, RepositoryPackageActionWidth)") && packageTemplate.Contains("actionRail.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0))"), "repository package card actions must sit inside the outer card padding so they cannot hide the right border.");
            Require(!packageTemplate.Contains("new FrameworkElementFactory(typeof(ColumnDefinition))"), "repository package data templates must not append ColumnDefinition through FrameworkElementFactory.");
            Require(packageTemplate.Contains("body.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 10, 0))"), "repository package card body must keep spacing away from the action rail inside the padded card.");
            Require(settingsWindow.Contains("action.SetValue(FrameworkElement.WidthProperty, RepositoryPackageActionWidth)"), "repository package action buttons must use fixed width so they cannot cover the card border.");
            Require(!settingsWindow.Contains("Color.FromRgb(51, 122, 183)") && !settingsWindow.Contains("icon.SetValue(DockPanel.DockProperty, Dock.Left)"), "repository package cards must not spend width on a leading decorative icon.");
            Require(settingsWindow.Contains("BuildRepositoryPackageTagsControl") && settingsWindow.Contains("BuildRepositoryTagChipTemplate") && settingsWindow.Contains("WrapPanel"), "repository package tags must render as compact chips instead of slash-separated text.");
            Require(!settingsWindow.Contains("RepositoryPackageTagLabelConverter"), "repository package tags must not be rendered as one long slash-separated text line.");
            Require(settingsWindow.Contains("BuildRepositoryPackagePrimaryActionButton") && settingsWindow.Contains("BuildRepositoryPackageUninstallButton"), "repository package cards must split install/update status and uninstall into two stacked buttons.");
            Require(!settingsWindow.Contains("state.SetBinding(TextBlock.TextProperty, new Binding(nameof(RepositoryPackageRow.InstallState)))"), "repository package cards must not render install state as a separate label beside action buttons.");
            Require(!settingsWindow.Contains("\"，已装 \"") && !settingsWindow.Contains("InstalledVersion) ? string.Empty"), "repository package card meta line above tag chips must not append install status.");
            Require(settingsWindow.Contains("\"本 \" + localVersion + \" · 仓 \" + repositoryVersion") && !settingsWindow.Contains("return row.RepositoryDisplayName + \"，\" + version"), "repository package card meta line must use compact local/repository versions without repository source text.");
            Require(settingsWindow.Contains("RepositoryPackageActionButtonStyle") && settingsWindow.Contains("RepositoryPackageUninstallButtonStyle") && settingsWindow.Contains("RepositoryPackageButtonTemplate"), "repository package action buttons must use an explicit WPF template so button chrome honors the requested colors.");
            Require(!settingsWindow.Contains("Button.MouseEnterEvent, new MouseEventHandler(RepositoryPackageUninstallHoverEnter)") && !settingsWindow.Contains("Button.MouseLeaveEvent, new MouseEventHandler(RepositoryPackageUninstallHoverLeave)"), "repository package uninstall hover must be style-driven instead of event-driven.");
            Require(settingsWindow.Contains("RepositoryPackageActionBrushConverter") && settingsWindow.Contains("RepositoryPackageActionForegroundConverter"), "repository package primary actions must have state-specific visual weight.");
            var primaryActionBackground = MethodBody(settingsWindow, "RepositoryPackageActionBackground");
            var primaryActionForeground = MethodBody(settingsWindow, "RepositoryPackageActionForeground");
            var primaryActionBorder = MethodBody(settingsWindow, "RepositoryPackageActionBorder");
            var primaryActionStyle = MethodBody(settingsWindow, "BuildRepositoryPackagePrimaryActionButton");
            var primaryActionRunner = MethodBody(settingsWindow, "RunRepositoryPackagePrimaryAction");
            var uninstallActionRunner = MethodBody(settingsWindow, "RunRepositoryPackageUninstallAction");
            Require(primaryActionBackground.Contains("RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.SuccessBrush") && primaryActionForeground.Contains("RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.AccentForegroundBrush"), "uninstalled repository packages must show install as white text on green.");
            Require(primaryActionBackground.Contains("RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.UpdateBrush") && primaryActionForeground.Contains("RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.AccentForegroundBrush"), "updatable repository packages must show update as white text on blue.");
            Require(primaryActionBackground.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)") && primaryActionBackground.Contains("isMouseOver") && primaryActionBackground.Contains("theme.SuccessBrush") && primaryActionForeground.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)") && primaryActionForeground.Contains("theme.AccentForegroundBrush") && primaryActionBorder.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)") && primaryActionBorder.Contains("theme.SuccessBrush"), "installed repository packages must switch the primary installed status to green background with white text on hover.");
            Require(primaryActionBackground.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.ControlBackground") && primaryActionForeground.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.TextBrush") && primaryActionBorder.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.BorderBrush"), "installed repository packages must default to a passive installed label before hover.");
            Require(primaryActionStyle.Contains("RepositoryPackagePrimaryActionLabelBinding") && settingsWindow.Contains("RepositoryPackagePrimaryActionLabelConverter") && settingsWindow.Contains("\"重安装\""), "installed repository package primary action must change the button label to reinstall while hovered.");
            Require(primaryActionRunner.Contains("RepositoryPackageAction.Reinstall.ToString()") && primaryActionRunner.Contains("_packageRepositoryService.Update(BaseDirectory(), package)"), "clicking hovered installed package status must reinstall by reusing the package update replacement path.");
            Require(uninstallActionRunner.Contains("RepositoryPackageAction.Reinstall.ToString()"), "installed packages with reinstall as primary action must still allow the separate uninstall button.");
            var uninstallButtonStyle = MethodBody(settingsWindow, "RepositoryPackageUninstallButtonStyle");
            Require(uninstallButtonStyle.Contains("UIElement.IsMouseOverProperty") && uninstallButtonStyle.Contains("Control.BackgroundProperty, RevitUiTheme.Current.DangerBrush") && uninstallButtonStyle.Contains("Control.ForegroundProperty, RevitUiTheme.Current.AccentForegroundBrush"), "repository package uninstall hover must switch to red background with white text.");
            Require(repositoryPackageRow.Contains("Take(3)") && repositoryPackageRow.Contains("TagBadges"), "repository package row must expose at most three key chip-ready tag badges when four cannot fit.");
            Require(repositorySettingsController.Contains("return \"已安装\";"), "installed repository packages must default to a passive installed label instead of a visible uninstall label.");
            Require(repositorySettingsController.Contains("RepositoryPackageAction.Reinstall.ToString()"), "installed repository packages without updates must expose reinstall as the primary hover action.");
            Require(repositorySettingsController.Contains("return \"有更新\";"), "updatable repository packages must show a distinct update label.");

            foreach (var forbiddenGrid in new[] { "_repositoriesGrid", "_repositoryPackagesGrid", "_pendingPackageOperationsGrid" })
            {
                Require(!settingsWindow.Contains(forbiddenGrid), "repository settings must not use DataGrid as the main warehouse surface: " + forbiddenGrid);
            }

            foreach (var forbiddenRibbonMutation in new[] { "FindRevitRibbonItem", "LiveAgent", "ItemText =" })
            {
                Require(!settingsWindow.Contains(forbiddenRibbonMutation), "repository settings must not promise live Revit Ribbon mutation: " + forbiddenRibbonMutation);
            }

            Require(settingsWindow.Contains("BuildRepositoryPackageItemsPanel") && settingsWindow.Contains("ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled"), "repository package browsing must use a vertical scrolling responsive grid instead of a single virtualized column.");
            Require(settingsWindow.Contains("ApplyRepositoryPackageFilter") && settingsWindow.Contains("RepositoryPackageFilterChanged") && settingsWindow.Contains("ApplyPackageFilters"), "repository package browsing must support controller-backed search and state filters for large plugin catalogs.");
            Require(settingsWindow.Contains("ApplyRepositoryPackageFilter") && settingsWindow.Contains("RepositoryPackageFilterChanged"), "repository package browsing must support search and state filters for large plugin catalogs.");
            foreach (var rowToken in new[] { "RepositoryDisplayName", "StatusPriority", "PrimaryAction", "PrimaryActionLabel", "SearchText", "TagsText", "CategoryText" })
            {
                Require(repositoryPackageRow.Contains(rowToken), "repository package row must expose user-facing browsing metadata: " + rowToken);
            }

            Require(settingsWindow.Contains("CreateButton(\"新增仓库\"") && settingsWindow.Contains("AddRepository()"), "repository toolbar must expose one generic add repository action.");
            foreach (var forbiddenAddMenu in new[] { "新增 GitHub 公开仓库", "新增 GitHub 私有仓库", "新增 Gitee 公开仓库", "新增 Gitee 私有仓库" })
            {
                Require(!settingsWindow.Contains(forbiddenAddMenu), "repository context menu must not expose split add repository entries: " + forbiddenAddMenu);
            }

            Require(!settingsWindow.Contains("tabs.Items.Add(BuildLogsTab())"), "settings must keep logs out of the main tab set.");
            Require(settingsWindow.Contains("ApiKey") && settingsWindow.Contains("Visibility") && settingsWindow.Contains("private"), "settings must support public and private repositories with apiKey.");
            Require(!settingsWindow.Contains("确定卸载插件包") && !settingsWindow.Contains("result.Success ? MessageBoxImage.Information"), "repository package install and uninstall must report status inline without pop-up result prompts.");
            Require(!repositoryBrowser.Contains("ProcessStartInfo") && !repositoryBrowser.Contains("FileName = \"git\"") && !repositoryBrowser.Contains("RunGit("), "repository browsing must not require a user-installed git executable.");
            Require(!repositoryBrowser.Contains("sparse-checkout") && !repositoryBrowser.Contains("fetch --quiet") && !repositoryBrowser.Contains(".git"), "repository browsing must no longer depend on git sparse checkout caches.");
            Require(repositoryBrowser.Contains("RepositoryArchiveSynchronizer") && repositoryBrowser.Contains("_archiveSynchronizer.Sync"), "repository browsing must delegate remote cache refresh to the HTTP archive synchronizer.");
            Require(repositoryArchiveSynchronizer.Contains("HttpWebRequest") && repositoryArchiveSynchronizer.Contains("ZipFile.OpenRead") && repositoryArchiveSynchronizer.Contains("ExtractArchive"), "repository archive synchronizer must download and extract repository zip archives.");
            Require(repositoryArchiveSynchronizer.Contains("ArchiveDownloadUserAgent") && repositoryArchiveSynchronizer.Contains("curl/8.0.1") && !repositoryArchiveSynchronizer.Contains("request.UserAgent = \"PlugHub\""), "repository archive downloads must use a Gitee-compatible user agent accepted by archive endpoints.");
            Require(repositoryAddress.Contains("ProviderFromHost(uri.Host)") && repositoryAddress.Contains("new RepositoryAddress(hostProvider"), "absolute repository URLs must infer GitHub or Gitee from the URL host instead of failing when the provider field is stale.");
            Require(repositoryArchiveSynchronizer.Contains("ArchiveDownloadUrl(address, repository)") && repositoryArchiveSynchronizer.Contains("ShouldAppendArchiveCacheBust") && repositoryArchiveSynchronizer.Contains("RequestCachePolicy(RequestCacheLevel.Reload)"), "repository source sync must bypass stale GitHub HTTP/archive cache without adding unsupported cache-bust query parameters to Gitee archive URLs.");
            Require(repositoryArchiveSynchronizer.Contains("HttpStatusCode.BadRequest") && repositoryArchiveSynchronizer.Contains("RepositoryRequiresToken(repository)") && repositoryArchiveSynchronizer.Contains("SyncGiteeRepositoryViaApi(address, repository, stagingDirectory)"), "public Gitee archive failures must fall back to the Gitee API file download path instead of surfacing a raw 400 response.");
            Require(repositoryArchiveSynchronizer.Contains("Parallel.ForEach") && repositoryArchiveSynchronizer.Contains("MaxDegreeOfParallelism") && repositoryArchiveSynchronizer.Contains("GiteeApiDownloadParallelism"), "Gitee API fallback must download repository files with bounded parallelism so public archive failures do not make source sync unnecessarily slow.");
            Require(repositoryArchiveSynchronizer.Contains("SyncFastestCloudRepository") && repositoryArchiveSynchronizer.Contains("CloudSyncCandidates") && repositoryArchiveSynchronizer.Contains("Task.WaitAny"), "public cloud repositories must race available Gitee/GitHub mirrors and use the first valid response.");
            Require(repositoryArchiveSynchronizer.Contains("ValidateArchiveFile") && repositoryArchiveSynchronizer.IndexOf("ValidateArchiveFile(archivePath, archiveUrl)", StringComparison.Ordinal) < repositoryArchiveSynchronizer.IndexOf("ExtractArchive(archivePath, stagingDirectory)", StringComparison.Ordinal), "repository archive synchronizer must validate downloaded zip content before extraction.");
            Require(repositoryArchiveSynchronizer.Contains("Downloaded repository archive is not a zip file") && repositoryArchiveSynchronizer.Contains("Check repository URL, ref, and credentials"), "repository archive synchronizer must report a clear URL/ref diagnostic for non-zip responses.");
            Require(repositoryArchiveSynchronizer.Contains("EnsureHttpsResponse(response.ResponseUri)") && repositoryArchiveSynchronizer.IndexOf("EnsureHttpsResponse(response.ResponseUri)", StringComparison.Ordinal) < repositoryArchiveSynchronizer.IndexOf("source.CopyTo(target)", StringComparison.Ordinal), "repository archive downloads must reject redirects away from HTTPS before writing archive bytes.");
            Require(repositoryArchiveSynchronizer.Contains("api.github.com/repos") && repositoryArchiveSynchronizer.Contains("/zipball/"), "repository archive synchronizer must support GitHub zipball archives.");
            Require(repositoryArchiveSynchronizer.Contains("https://gitee.com/") && repositoryArchiveSynchronizer.Contains("/repository/archive/"), "repository archive synchronizer must support Gitee repository archive downloads.");
            Require(repositoryArchiveSynchronizer.Contains("access_token") && repositoryArchiveSynchronizer.Contains("Authorization"), "repository archive synchronizer must support private Gitee and GitHub repositories with tokens.");
            Require(repositoryArchiveSynchronizer.Contains("ShouldUseGiteeApiFallback") && repositoryArchiveSynchronizer.Contains("HttpStatusCode.Forbidden") && repositoryArchiveSynchronizer.Contains("SyncGiteeRepositoryViaApi") && repositoryArchiveSynchronizer.Contains("git/trees/") && repositoryArchiveSynchronizer.Contains("contents/"), "private Gitee repositories must fall back to the Gitee API when the web archive endpoint returns 403.");
            Require(repositoryArchiveSynchronizer.Contains("IsUnderDirectory") && repositoryArchiveSynchronizer.Contains("ExtractToFile"), "repository archive extraction must guard against zip-slip paths.");
            Require(repositoryArchiveSynchronizer.Contains("SensitiveTextRedactor.Redact"), "repository archive diagnostics must redact tokens and URLs before showing errors.");
            Require(repositoryArchiveSynchronizer.Contains("ValidateCacheDirectory(cacheDirectory)") && repositoryArchiveSynchronizer.IndexOf("ValidateCacheDirectory(cacheDirectory)", StringComparison.Ordinal) < repositoryArchiveSynchronizer.IndexOf("ReplaceCacheDirectory(stagingDirectory, fullCacheDirectory)", StringComparison.Ordinal), "repository archive synchronizer must validate cache directory ownership before replacing it.");
            Require(packageRepositoryService.Contains("new RepositoryBrowser"), "PackageRepositoryService must delegate browsing to RepositoryBrowser.");
            Require(packageRepositoryService.Contains("new PackageManifestReader"), "PackageRepositoryService must delegate manifest reading to PackageManifestReader.");
            Require(packageRepositoryService.Contains("new PackageInstallService"), "PackageRepositoryService must delegate payload installation to PackageInstallService.");
            Require(packageManifestReader.Contains("ReadPackagesFromManifest") && packageManifestReader.Contains("RepositoryPackageDisplayName"), "repository manifest reading must live in PackageManifestReader.");
            Require(packageManifestReader.Contains("segment.All(ch => ch == '.')") && packageRepositoryService.Contains("segment.All(ch => ch == '.')"), "repository package path segments must reject all-dot package ids.");
            Require(packageManifestReader.Contains("AdjacentPackageManifestPattern = \"*.packages.json\""), "repository manifest reader must discover adjacent *.packages.json manifests.");
            Require(packageInstallService.Contains("InstallPackagePayload") && packageInstallService.Contains("CopyPackagePayload") && packageInstallService.Contains("WriteSingleModuleManifest") && !packageInstallService.Contains("CopyDirectory("), "repository install must split selected plugins and must not copy the whole repository directory.");
            Require(packageRepositoryService.Contains("ApplyPendingOperations"), "repository package operations must defer locked DLL deletion and replacement through pending operations.");
            Require(!packageRepositoryService.Contains("PendingPackageOperation.Restart("), "normal package installs and unlocked updates must not persist restart-only pending operations from the external Manager.");
            Require(!packageRepositoryService.Contains("PendingOperationsPath(") && !packageRepositoryService.Contains("PendingOperationsFileName"), "PackageRepositoryService must not duplicate pending operation store path ownership.");
            Require(packageRepositoryService.Contains("ListPendingOperations"), "package repository service must expose pending operation listing.");
            Require(packageRepositoryService.Contains("CancelPendingOperation"), "package repository service must expose pending operation cancellation.");
            Require(credentialService.Contains("ProtectedData.Protect") && credentialService.Contains("ProtectedData.Unprotect"), "repository credential service must use DPAPI.");
            Require(redactor.Contains("Redact") && redactor.Contains("x-access-token") && redactor.Contains("oauth2") && redactor.Contains("access_token"), "diagnostic redactor must mask repository tokens.");
            Require(configurationModels.Contains("public string DisplayName { get; set; } = string.Empty;") && configurationModels.Contains("EncryptedApiKey"), "repository configuration must persist custom displayName and encrypted apiKey separately.");
            Require(repositoryArchiveSynchronizer.Contains("ResolveApiKey(repository)") && repositoryArchiveSynchronizer.Contains("SafePathSegment(repository.Id)"), "repository archive synchronizer must resolve protected credentials and stage downloads under a repository-specific cache path.");
            Require(repositoryArchiveSynchronizer.Contains("DownloadArchive") && repositoryArchiveSynchronizer.Contains("ReplaceCacheDirectory"), "repository archive synchronizer must atomically replace the local repository cache after a successful download.");
            Require(settingsWindow.Contains("已同步最快云端镜像") && settingsWindow.Contains("已读取本地文件夹"), "repository source sync status must distinguish cloud mirror sync from local folder reads.");
            Require(readme.Contains("不需要安装 Git") && readme.Contains("HTTP archive"), "README must state that repository browsing no longer requires user-installed Git.");
            Require(frameworkUpdateService.Contains("PLUGHUB_TEST_UPDATE_RELEASE_URI") && frameworkUpdateService.Contains("GitHub Test") && frameworkUpdateService.Contains("ContinueWhenNoUpdate") && frameworkUpdateService.Contains("GitHubTestPrereleaseList") && frameworkUpdateService.Contains("GetLatestTestPrerelease"), "framework update checks must support a latest-TV test update source without changing stable defaults.");
            Require(frameworkUpdateService.Contains("BuildDefaultCheckSources") && frameworkUpdateService.Contains("BuildCheckSources(currentVersion, _updateSources)") && frameworkUpdateService.Contains("GitHubReleaseListUri"), "TV builds must query the GitHub prerelease list before Gitee stable tags.");
            Require(frameworkUpdateService.Contains("ComparableVersionText") && frameworkUpdateService.Contains("IndexOfAny") && frameworkUpdateService.Contains("IsStableReleaseTag") && frameworkUpdateService.Contains("IsTestReleaseTag"), "framework update version comparison must handle TVx.y.z test tags and allow same-number stable releases to replace test builds.");
            Require(settingsWindow.Contains("RepositoryCredentialService") && settingsWindow.Contains("ProtectForSave(repository)"), "settings save must protect repository apiKey before serializing sources.");
            Require(settingsWindow.Contains("SettingsMetrics.CountUniqueModules(EditableModules())") && settingsWindow.Contains("SettingsMetrics.CountUniqueFeatures(EditableModules())") && settingsWindow.Contains("SettingsMetrics.CountEnabledRepositories(_configuration.Modules.Repositories)"), "settings header/about metrics must count unique modules, unique features, and enabled repositories.");
            Require(settingsWindow.Contains("ApiKey = string.Empty") && settingsWindow.Contains("PlainApiKey = repository.ApiKey"), "settings repository rows must keep legacy plaintext apiKey available without echoing it in the UI.");
            Require(settingsWindow.Contains("CustomName = repository.DisplayName") && repositoryRow.Contains("DisplayName = CustomName ?? string.Empty") && repositoryRow.Contains("CustomName"), "settings repository rows must edit and persist custom repository displayName separately from the resolved card title.");
            Require(repositoryRow.Contains("string.IsNullOrWhiteSpace(ApiKey) ? PlainApiKey"), "repository row ToConfiguration must preserve legacy plaintext apiKey when the user did not enter a replacement token.");
            Require(settingsWindow.Contains("EncryptedApiKey = repository.EncryptedApiKey") && settingsWindow.Contains("ApiKeyProtection = repository.ApiKeyProtection"), "settings repository rows must preserve encrypted apiKey metadata.");
            Require(repositoryRow.Contains("EncryptedApiKey = EncryptedApiKey ?? string.Empty") && repositoryRow.Contains("ApiKeyProtection = ApiKeyProtection ?? string.Empty"), "repository row ToConfiguration must retain encrypted apiKey metadata.");
            Require(configurationLoader.Contains("DisplayName = repository.DisplayName") && configurationLoader.Contains("EncryptedApiKey = repository.EncryptedApiKey") && configurationLoader.Contains("ApiKeyProtection = repository.ApiKeyProtection"), "configuration loader must preserve repository displayName and encrypted credentials when applying presets.");
            Require(sourceResolver.Contains("DisplayName = repository.DisplayName") && sourceResolver.Contains("EncryptedApiKey = repository.EncryptedApiKey") && sourceResolver.Contains("ApiKeyProtection = repository.ApiKeyProtection"), "module source resolver must preserve repository displayName and encrypted credentials.");
            ValidateRepositoryCredentialAndRedactionBehavior();
            var pendingStore = ReadText("src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs");
            Require(pendingStore.Contains("pending-operations.json"), "pending operation store must own the pending operation file name.");
            Require(pendingStore.Contains("AddOrReplace") && pendingStore.Contains("Remove") && pendingStore.Contains("Read"), "pending operation store must read, add, and remove operations.");
            Require(repositoryPackageRow.Contains("已安装待重启") && repositoryPackageRow.Contains("PendingOperation") && repositoryPackageRow.Contains("isRevitHostRunning && !isInstalled && isLoadedInCurrentRuntime") && repositoryPackageRow.Contains("isRevitHostRunning && !isLoadedInCurrentRuntime") && settingsWindow.Contains("IsLoadedInCurrentRuntime"), "repository package status must distinguish installed, uninstalled, and pending-restart states without treating absent Revit as a loaded runtime.");
            var frameworkRuntime = ReadText("src/PlugHub.Framework/Runtime/FrameworkRuntime.cs");
            Require(frameworkRuntime.Contains("ApplyPendingOperations"), "runtime startup must apply deferred package operations before module discovery.");
            Require(frameworkRuntime.Contains("applyPendingPackageOperations") && frameworkRuntime.Contains("Load(baseDirectory, configDirectory, true)"), "external Manager must be able to load a local runtime snapshot without applying deferred package operations while Revit is still running.");
            Require(!settingsWindow.Contains("LoadDiagnosticRows(FrameworkRuntimeState.Current);\r\n            LoadSourceRows();"), "settings save must not reload stale runtime diagnostics after saving configuration.");

            Require(workflow.Contains("-UseRelativeAddinAssembly"), "release workflow must build a package with relative addin assembly path.");
            Require(giteeWorkflow.Contains("branches:") && giteeWorkflow.Contains("- main"), "Gitee sync workflow must run for main pushes.");
            Require(giteeWorkflow.Contains("tags:") && giteeWorkflow.Contains("- \"V*\""), "Gitee sync workflow must run for V* tag pushes.");
            Require(giteeWorkflow.Contains("workflow_dispatch"), "Gitee sync workflow must support manual dispatch.");
            Require(giteeWorkflow.Contains("actions/checkout@v6") && !giteeWorkflow.Contains("actions/checkout@v4"), "Gitee sync workflow must use the Node 24 checkout action line.");
            Require(giteeWorkflow.Contains("GITEE_PRIVATE_KEY") && giteeWorkflow.Contains("GITEE_USER"), "Gitee sync workflow must validate configured Gitee SSH secrets.");
            Require(!giteeWorkflow.Contains("GITEE_TOKEN"), "Gitee sync workflow must not require the release API token; release.yml owns Gitee release publishing.");
            Require(giteeWorkflow.Contains("git@gitee.com:GaoMengGu/PlugHub.git") && giteeWorkflow.Contains("git push gitee +HEAD:main"), "Gitee sync workflow must mirror main to GaoMengGu/PlugHub on Gitee with GitHub as source of truth.");
            Require(giteeWorkflow.Contains("refs/tags/") && giteeWorkflow.Contains("git push gitee \"+refs/tags/$tag:refs/tags/$tag\""), "Gitee sync workflow must mirror GitHub release tags to Gitee before release.yml mirrors release assets.");
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
            var redactedAccessToken = PlugHub.Framework.Diagnostics.SensitiveTextRedactor.Redact("https://gitee.com/owner/repo/repository/archive/main.zip?access_token=secret");
            Require(!redactedAccessToken.Contains("secret") && redactedAccessToken.Contains("access_token=***"), "diagnostic redactor must mask Gitee access_token query values.");

            var manifestReader = new PlugHub.Framework.Packages.PackageManifestReader();
            var credentialResolver = new PlugHub.Framework.Packages.RepositoryCredentialService();
            var browser = new PlugHub.Framework.Packages.RepositoryBrowser(
                manifestReader,
                credentialResolver,
                (baseDirectory, installDirectory, moduleId) => string.Empty,
                (baseDirectory, installDirectory, moduleId) => false,
                (baseDirectory, packageId, moduleId) => string.Empty);
            var repositoryUrl = typeof(PlugHub.Framework.Packages.RepositoryBrowser).GetMethod("RepositoryUrl", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            Require(repositoryUrl != null, "repository browser must expose a repository URL helper.");
            var publicUrl = Convert.ToString(repositoryUrl!.Invoke(browser, new object[]
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
            Require(!publicUrl.Contains("secret") && !publicUrl.Contains("user:") && publicUrl == "https://example.com/owner/repo", "public repository URL must strip userinfo and normalize repository suffixes for archive access.");
            var fullGiteeUrl = Convert.ToString(repositoryUrl.Invoke(browser, new object[]
            {
                new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
                {
                    Provider = "gitee",
                    Visibility = "public",
                    Repository = "https://gitee.com/GaoMengGu/PlugHub_Packages"
                },
                false
            })) ?? string.Empty;
            var shorthandGiteeUrl = Convert.ToString(repositoryUrl.Invoke(browser, new object[]
            {
                new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
                {
                    Provider = "gitee",
                    Visibility = "public",
                    Repository = "GaoMengGu/PlugHub_Packages"
                },
                false
            })) ?? string.Empty;
            var fullGitHubUrl = Convert.ToString(repositoryUrl.Invoke(browser, new object[]
            {
                new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
                {
                    Provider = "github",
                    Visibility = "public",
                    Repository = "https://github.com/GaoMengGu/PlugHub_Packages"
                },
                false
            })) ?? string.Empty;
            var shorthandGitHubUrl = Convert.ToString(repositoryUrl.Invoke(browser, new object[]
            {
                new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
                {
                    Provider = "github",
                    Visibility = "public",
                    Repository = "GaoMengGu/PlugHub_Packages"
                },
                false
            })) ?? string.Empty;
            Require(fullGiteeUrl == "https://gitee.com/GaoMengGu/PlugHub_Packages" && shorthandGiteeUrl == "https://gitee.com/GaoMengGu/PlugHub_Packages", "Gitee repository URLs must support both full URL and owner/repository shorthand forms.");
            Require(fullGitHubUrl == "https://github.com/GaoMengGu/PlugHub_Packages" && shorthandGitHubUrl == "https://github.com/GaoMengGu/PlugHub_Packages", "GitHub repository URLs must support both full URL and owner/repository shorthand forms.");
            var service = new PlugHub.Framework.Packages.PackageRepositoryService();
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
                var installA = Path.Combine(baseDirectory, "packages", "install-a");
                var installB = Path.Combine(baseDirectory, "packages", "install-b");
                var stagingRoot = Path.Combine(baseDirectory, "repository-cache", ".package-install");
                var staging = Path.Combine(stagingRoot, "staging-a");
                var stagingSameA = Path.Combine(stagingRoot, "staging-same-a");
                var stagingSameB = Path.Combine(stagingRoot, "staging-same-b");

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

                var deleteInstall = Path.Combine(baseDirectory, "packages", "delete-install");
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
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"installed-package\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(
                    Path.Combine(repositoryCacheDirectory, "packages.json"),
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
                            ManifestPath = "packages.json",
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
                            ManifestPath = "packages.json",
                            Enabled = true
                        },
                        new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
                        {
                            Id = "private-packages",
                            Provider = "github",
                            Visibility = "private",
                            Repository = "example/private-packages",
                            Ref = "main",
                            ManifestPath = "packages.json",
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
                File.WriteAllText(Path.Combine(directInstalledDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"1.0.0\",\"modules\":[{\"id\":\"direct-update\",\"assembly\":\"DirectUpdate.dll\",\"type\":\"Demo.DirectUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(Path.Combine(directSourceDirectory, "DirectUpdate.dll"), "replacement");
                File.WriteAllText(Path.Combine(directSourceDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"2.0.0\",\"modules\":[{\"id\":\"direct-update\",\"assembly\":\"DirectUpdate.dll\",\"type\":\"Demo.DirectUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");

                var directDescriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "direct-update",
                    ModuleId = "direct-update",
                    DisplayName = "Direct Update",
                    ManifestPath = Path.Combine(directSourceDirectory, "packages.json"),
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
                File.WriteAllText(Path.Combine(installedDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"1.0.0\",\"modules\":[{\"id\":\"locked-update\",\"assembly\":\"LockedUpdate.dll\",\"type\":\"Demo.LockedUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(Path.Combine(sourceDirectory, "LockedUpdate.dll"), "replacement");
                File.WriteAllText(Path.Combine(sourceDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"2.0.0\",\"modules\":[{\"id\":\"locked-update\",\"assembly\":\"LockedUpdate.dll\",\"type\":\"Demo.LockedUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");

                var descriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "locked-update",
                    ModuleId = "locked-update",
                    DisplayName = "Locked Update",
                    ManifestPath = Path.Combine(sourceDirectory, "packages.json"),
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
                    Require(!File.ReadAllText(Path.Combine(installedDirectory, "packages.json")).Contains("locked-update"), "locked update must remove the old module declaration before restart.");
                    Require(Directory.GetFiles(Path.Combine(tempRoot, "repository-cache"), "pending-operations.json", SearchOption.AllDirectories).Any(), "locked update must write a pending operation marker.");
                }

                var updateDiagnostics = new PlugHub.Framework.Packages.PackageRepositoryService().ApplyPendingOperations(tempRoot);
                Require(!updateDiagnostics.Any(message => message.Severity == PlugHub.Contracts.Modules.DiagnosticSeverity.Error), "pending locked update must apply on next startup: " + string.Join("; ", updateDiagnostics.Select(item => item.Message)));
                Require(File.ReadAllText(installedDll) == "replacement", "pending locked update must replace the DLL after restart.");
                Require(File.ReadAllText(Path.Combine(installedDirectory, "packages.json")).Contains("locked-update"), "pending locked update must restore the selected module manifest.");

                var cancelUpdateDirectory = Path.Combine(tempRoot, "packages", "cancel-locked-update");
                var cancelUpdateSourceDirectory = Path.Combine(tempRoot, "repository-cache", "cancel-locked-update");
                var cancelUpdateDuplicateDirectory = Path.Combine(tempRoot, "packages", "cancel-locked-update-duplicate");
                Directory.CreateDirectory(cancelUpdateDirectory);
                Directory.CreateDirectory(cancelUpdateSourceDirectory);
                Directory.CreateDirectory(cancelUpdateDuplicateDirectory);
                var cancelUpdateDll = Path.Combine(cancelUpdateDirectory, "CancelLockedUpdate.dll");
                var cancelUpdateDuplicateDll = Path.Combine(cancelUpdateDuplicateDirectory, "CancelLockedUpdateDuplicate.dll");
                var cancelUpdateManifest = Path.Combine(cancelUpdateDirectory, "packages.json");
                File.WriteAllText(cancelUpdateDll, "locked");
                File.WriteAllText(cancelUpdateDuplicateDll, "duplicate");
                File.WriteAllText(cancelUpdateManifest, "{\"schemaVersion\":\"1.0\",\"version\":\"1.0.0\",\"modules\":[{\"id\":\"cancel-locked-update\",\"assembly\":\"CancelLockedUpdate.dll\",\"type\":\"Demo.CancelLockedUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(Path.Combine(cancelUpdateDuplicateDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"1.0.0\",\"modules\":[{\"id\":\"cancel-locked-update\",\"assembly\":\"CancelLockedUpdateDuplicate.dll\",\"type\":\"Demo.CancelLockedUpdateDuplicateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(Path.Combine(cancelUpdateSourceDirectory, "CancelLockedUpdate.dll"), "replacement");
                File.WriteAllText(Path.Combine(cancelUpdateSourceDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"version\":\"2.0.0\",\"modules\":[{\"id\":\"cancel-locked-update\",\"assembly\":\"CancelLockedUpdate.dll\",\"type\":\"Demo.CancelLockedUpdateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                var cancelUpdateDescriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "cancel-locked-update",
                    ModuleId = "cancel-locked-update",
                    DisplayName = "Cancel Locked Update",
                    ManifestPath = Path.Combine(cancelUpdateSourceDirectory, "packages.json"),
                    SourceDirectory = cancelUpdateSourceDirectory,
                    InstallDirectory = cancelUpdateDirectory,
                    IsInstalled = true
                };
                using (File.Open(cancelUpdateDll, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var service = new PlugHub.Framework.Packages.PackageRepositoryService();
                    var updateResult = service.Update(tempRoot, cancelUpdateDescriptor);
                    Require(updateResult.Success, "locked update prepared for cancellation must queue successfully: " + updateResult.Message);
                    Require(!File.ReadAllText(cancelUpdateManifest).Contains("cancel-locked-update"), "locked update prepared for cancellation must remove the old module declaration first.");
                    var cancelResult = service.CancelPendingOperation(tempRoot, "cancel-locked-update", "cancel-locked-update");
                    Require(cancelResult.Success, "cancel pending locked update must succeed: " + cancelResult.Message);
                    Require(File.ReadAllText(cancelUpdateManifest).Contains("cancel-locked-update"), "cancel pending locked update must restore the original module manifest.");
                    Require(File.Exists(cancelUpdateDuplicateDll), "cancel pending locked update must not leave duplicate package payload deleted.");
                    Require(File.ReadAllText(Path.Combine(cancelUpdateDuplicateDirectory, "packages.json")).Contains("cancel-locked-update"), "cancel pending locked update must restore duplicate module manifests.");
                    Require(string.IsNullOrWhiteSpace(service.RefreshInstallState(tempRoot, cancelUpdateDescriptor).PendingOperation), "cancel pending locked update must clear pending operation metadata.");
                }

                var uninstallDirectory = Path.Combine(tempRoot, "packages", "locked-uninstall");
                Directory.CreateDirectory(uninstallDirectory);
                var uninstallDll = Path.Combine(uninstallDirectory, "LockedUninstall.dll");
                File.WriteAllText(uninstallDll, "locked");
                File.WriteAllText(Path.Combine(uninstallDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"locked-uninstall\",\"assembly\":\"LockedUninstall.dll\",\"type\":\"Demo.LockedUninstallModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
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
                    Require(!File.ReadAllText(Path.Combine(uninstallDirectory, "packages.json")).Contains("locked-uninstall"), "locked uninstall must remove the module declaration before restart.");

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

                var unlockedUninstallDirectory = Path.Combine(tempRoot, "packages", "unlocked-uninstall");
                var unlockedUninstallDuplicateDirectory = Path.Combine(tempRoot, "packages", "unlocked-uninstall-duplicate");
                Directory.CreateDirectory(unlockedUninstallDirectory);
                Directory.CreateDirectory(unlockedUninstallDuplicateDirectory);
                File.WriteAllText(Path.Combine(unlockedUninstallDirectory, "UnlockedUninstall.dll"), "unlocked");
                File.WriteAllText(Path.Combine(unlockedUninstallDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"unlocked-uninstall\",\"assembly\":\"UnlockedUninstall.dll\",\"type\":\"Demo.UnlockedUninstallModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(Path.Combine(unlockedUninstallDuplicateDirectory, "UnlockedUninstallDuplicate.dll"), "duplicate");
                File.WriteAllText(Path.Combine(unlockedUninstallDuplicateDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"unlocked-uninstall\",\"assembly\":\"UnlockedUninstallDuplicate.dll\",\"type\":\"Demo.UnlockedUninstallDuplicateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                var unlockedUninstallDescriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "unlocked-uninstall",
                    ModuleId = "unlocked-uninstall",
                    DisplayName = "Unlocked Uninstall",
                    InstallDirectory = unlockedUninstallDirectory,
                    IsInstalled = true
                };
                var unlockedUninstallResult = new PlugHub.Framework.Packages.PackageRepositoryService().Uninstall(tempRoot, unlockedUninstallDescriptor);
                Require(unlockedUninstallResult.Success, "unlocked uninstall with duplicate module manifests must succeed: " + unlockedUninstallResult.Message);
                Require(!Directory.Exists(unlockedUninstallDirectory), "unlocked uninstall must delete the selected package directory.");
                Require(!Directory.Exists(unlockedUninstallDuplicateDirectory), "unlocked uninstall must delete duplicate package directories that only contain the same module.");

                var cancelUninstallDirectory = Path.Combine(tempRoot, "packages", "cancel-locked-uninstall");
                var cancelUninstallDuplicateDirectory = Path.Combine(tempRoot, "packages", "cancel-locked-uninstall-duplicate");
                Directory.CreateDirectory(cancelUninstallDirectory);
                Directory.CreateDirectory(cancelUninstallDuplicateDirectory);
                var cancelUninstallDll = Path.Combine(cancelUninstallDirectory, "CancelLockedUninstall.dll");
                var cancelUninstallDuplicateDll = Path.Combine(cancelUninstallDuplicateDirectory, "CancelLockedUninstallDuplicate.dll");
                var cancelUninstallManifest = Path.Combine(cancelUninstallDirectory, "packages.json");
                File.WriteAllText(cancelUninstallDll, "locked");
                File.WriteAllText(cancelUninstallDuplicateDll, "duplicate");
                File.WriteAllText(cancelUninstallManifest, "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"cancel-locked-uninstall\",\"assembly\":\"CancelLockedUninstall.dll\",\"type\":\"Demo.CancelLockedUninstallModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                File.WriteAllText(Path.Combine(cancelUninstallDuplicateDirectory, "packages.json"), "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"cancel-locked-uninstall\",\"assembly\":\"CancelLockedUninstallDuplicate.dll\",\"type\":\"Demo.CancelLockedUninstallDuplicateModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");
                var cancelUninstallDescriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "cancel-locked-uninstall",
                    ModuleId = "cancel-locked-uninstall",
                    DisplayName = "Cancel Locked Uninstall",
                    InstallDirectory = cancelUninstallDirectory,
                    IsInstalled = true
                };
                using (File.Open(cancelUninstallDll, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var service = new PlugHub.Framework.Packages.PackageRepositoryService();
                    var uninstallResult = service.Uninstall(tempRoot, cancelUninstallDescriptor);
                    Require(uninstallResult.Success, "locked uninstall prepared for cancellation must queue successfully: " + uninstallResult.Message);
                    Require(!File.ReadAllText(cancelUninstallManifest).Contains("cancel-locked-uninstall"), "locked uninstall prepared for cancellation must remove the module declaration first.");
                    var cancelResult = service.CancelPendingOperation(tempRoot, "cancel-locked-uninstall", "cancel-locked-uninstall");
                    Require(cancelResult.Success, "cancel pending locked uninstall must succeed: " + cancelResult.Message);
                    Require(File.ReadAllText(cancelUninstallManifest).Contains("cancel-locked-uninstall"), "cancel pending locked uninstall must restore the original module manifest.");
                    Require(File.Exists(cancelUninstallDuplicateDll), "cancel pending locked uninstall must not leave duplicate package payload deleted.");
                    Require(File.ReadAllText(Path.Combine(cancelUninstallDuplicateDirectory, "packages.json")).Contains("cancel-locked-uninstall"), "cancel pending locked uninstall must restore duplicate module manifests.");
                    Require(string.IsNullOrWhiteSpace(service.RefreshInstallState(tempRoot, cancelUninstallDescriptor).PendingOperation), "cancel pending locked uninstall must clear pending operation metadata.");
                }
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
                    Path.Combine(repositoryRoot, "packages.json"),
                    "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"duct-package\",\"assembly\":\"dist/Duct.dll\",\"type\":\"Demo.DuctModule\",\"displayName\":\"Duct\",\"enabled\":true,\"visible\":true,\"features\":[{\"id\":\"duct.switch\",\"name\":\"Switch\",\"category\":\"mep\",\"group\":\"duct\",\"order\":1,\"defaultState\":\"Visible\",\"commandAssembly\":\"dist/Duct.dll\",\"commandType\":\"Demo.DuctCommand\"}]},{\"id\":\"family-package\",\"assembly\":\"dist/Family.dll\",\"type\":\"Demo.FamilyModule\",\"displayName\":\"Family\",\"enabled\":true,\"visible\":true,\"features\":[{\"id\":\"family.batch\",\"name\":\"Batch\",\"category\":\"family\",\"group\":\"family\",\"order\":1,\"defaultState\":\"Visible\",\"commandAssembly\":\"dist/Family.dll\",\"commandType\":\"Demo.FamilyCommand\"}]}]}");

                var service = new PlugHub.Framework.Packages.PackageRepositoryService();
                var packages = service.BrowseCached(tempRoot, "public-packages", repositoryRoot, out var diagnostics);
                Require(!diagnostics.Any(), "cached repository package browse should not emit diagnostics: " + string.Join("; ", diagnostics.Select(item => item.Message)));
                Require(packages.Count == 2, "repository root packages.json with two modules must browse as two plugin rows.");
                Require(packages.Select(package => package.PackageId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "plugin rows from the same packages.json must install independently by module id.");
                Require(packages.Select(package => package.InstallDirectory).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "plugin rows from the same packages.json must use independent install directories.");

                var ductPackage = packages.Single(package => package.ModuleId == "duct-package");
                var familyPackage = packages.Single(package => package.ModuleId == "family-package");
                Require(ductPackage.DisplayName == "Switch", "repository package rows must display the feature name instead of the module or group name.");
                var installResult = service.Install(tempRoot, ductPackage);
                Require(installResult.Success, "repository package install should succeed: " + installResult.Message);

                var ductInstallDirectory = Path.Combine(tempRoot, "packages", "duct-package");
                var familyInstallDirectory = Path.Combine(tempRoot, "packages", "family-package");
                Require(File.Exists(Path.Combine(ductInstallDirectory, "packages.json")), "installed plugin must write a package-local manifest.");
                Require(!Directory.Exists(familyInstallDirectory), "installing one plugin must not install another module from the same repository manifest.");
                Require(Directory.GetFiles(Path.Combine(tempRoot, "packages"), "packages.json", SearchOption.AllDirectories).Length == 1, "installing one plugin must create only one packages.json under packages.");
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
                File.Copy(Path.Combine(repositoryRoot, "packages.json"), Path.Combine(familyInstallDirectory, "packages.json"));

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
                Require(File.ReadAllText(Path.Combine(familyInstallDirectory, "packages.json")).Contains("family-package"), "sibling packages manifest must keep the remaining module.");
                Require(!File.ReadAllText(Path.Combine(familyInstallDirectory, "packages.json")).Contains("duct-package"), "sibling packages manifest must remove the uninstalled module.");

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
                Require(File.Exists(Path.Combine(familyInstallDirectory, "packages.json")), "sibling plugin install must write its own package-local manifest.");
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
                File.WriteAllText(Path.Combine(packageDirectory, "packages.json"), Json.Serialize(serializedModules));

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

                Require(resolved.Modules.Modules.Any(module => module.Id == "serialized-package"), "runtime must load installed packages manifests after settings serialization rewrites JSON casing.");
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
                    Path.Combine(sourceDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":\"broken-package\",\"assembly\":\"dist/Missing.dll\",\"type\":\"Demo.BrokenModule\",\"enabled\":true,\"visible\":true,\"features\":[]}]}");

                var descriptor = new PlugHub.Framework.Packages.RepositoryPackageDescriptor
                {
                    RepositoryId = "test-repository",
                    PackageId = "broken-package",
                    ModuleId = "broken-package",
                    DisplayName = "Broken Package",
                    ManifestPath = Path.Combine(sourceDirectory, "packages.json"),
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
                    Path.Combine(descriptor.InstallDirectory, "packages.json"),
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

        private static void ValidateReleaseInstallerPackaging()
        {
            var installerProject = ReadText("src/PlugHub.Installer/PlugHub.Installer.csproj");
            var installerForm = ReadText("src/PlugHub.Installer/InstallerForm.cs");
            var installerPayload = ReadText("src/PlugHub.Installer/InstallerPayload.cs");
            var addinWriter = ReadText("src/PlugHub.Installer/AddinManifestWriter.cs");
            var workflow = ReadText(".github/workflows/release.yml");
            var testUpdateWorkflow = ReadText(".github/workflows/test-update-release.yml");
            var solution = ReadText("PlugHub.sln");
            var solutionX = ReadText("PlugHub.slnx");
            var readme = ReadText("README.md");

            Require(installerProject.Contains("<OutputType>WinExe</OutputType>"), "installer project must build a Windows EXE.");
            Require(installerProject.Contains("<TargetFramework>net48</TargetFramework>"), "installer project must target net48.");
            Require(installerProject.Contains("InstallerPayloadZip") && installerProject.Contains("PlugHubPayload.zip"), "installer project must embed the release payload zip through InstallerPayloadZip.");
            Require(solution.Contains("src\\PlugHub.Installer\\PlugHub.Installer.csproj"), "installer project must be included in PlugHub.sln.");
            Require(solutionX.Contains("src/PlugHub.Installer/PlugHub.Installer.csproj"), "installer project must be included in PlugHub.slnx.");
            Require(installerForm.Contains(@"D:\Program Files\PlugHub"), "installer UI must default the install directory to D:\\Program Files\\PlugHub.");
            Require(installerForm.Contains("FolderBrowserDialog"), "installer UI must let users choose the install directory.");
            Require(installerForm.Contains("ApplyModernInstallerStyle") && installerForm.Contains("BuildPrimaryButton") && installerForm.Contains("BuildSecondaryButton") && installerForm.Contains("PlugHub 安装器"), "installer UI must use a simple branded one-page layout with explicit primary and secondary actions.");
            Require(installerForm.Contains("BackColor = Color.White") && installerForm.Contains("Color.FromArgb(0, 120, 212)") && installerForm.Contains("FlatStyle.Flat"), "installer UI must use a clean white surface, a restrained PlugHub accent, and flat button chrome.");
            Require(installerPayload.Contains("GetManifestResourceStream") && installerPayload.Contains("ExtractPayloadZip"), "installer payload must be embedded and extracted by the installer.");
            Require(installerPayload.Contains("ValidateInstallDirectory") && installerPayload.Contains("Install directory must be named PlugHub"), "installer payload must refuse broad parent directories and only install into a PlugHub-named directory.");
            Require(installerPayload.Contains("IsUnderDirectory") && installerPayload.Contains("ExtractToFile") && !installerPayload.Contains("ZipFile.ExtractToDirectory"), "installer payload extraction must guard against zip-slip paths.");
            Require(addinWriter.Contains("Autodesk") && addinWriter.Contains("Revit") && addinWriter.Contains("Addins") && addinWriter.Contains("2020"), "installer must write the Revit 2020 addins manifest directory.");
            Require(addinWriter.Contains("PlugHub.Revit2020.dll") && addinWriter.Contains("Assembly") && addinWriter.Contains("Backup"), "installer must rewrite addin Assembly to the installed DLL path and backup existing manifests.");
            Require(workflow.Contains("runs-on: windows-2022") && !workflow.Contains("runs-on: windows-latest"), "release workflow must pin Windows packaging to windows-2022 instead of drifting with windows-latest.");
            Require(workflow.Contains("actions/checkout@v6") && !workflow.Contains("actions/checkout@v4"), "release workflow must use the Node 24 checkout action line.");
            Require(workflow.Contains("sigstore/cosign-installer@v4.1.2") && !workflow.Contains("sigstore/cosign-installer@v3"), "release workflow must pin the resolvable cosign installer v4 action line.");
            Require(workflow.Contains("softprops/action-gh-release@v3") && !workflow.Contains("softprops/action-gh-release@v2"), "release workflow must use the Node 24 GitHub release action line.");
            Require(workflow.Contains("Build PlugHub installer") && workflow.Contains("-t:Rebuild") && workflow.Contains("InstallerPayloadZip") && workflow.Contains("PlugHub-Setup-$tag.exe"), "release workflow must rebuild and upload PlugHub installer EXE.");
            Require(testUpdateWorkflow.Contains("push:") && testUpdateWorkflow.Contains("- codex") && testUpdateWorkflow.Contains("workflow_dispatch") && testUpdateWorkflow.Contains("TV\\d+\\.\\d+\\.\\d+") && testUpdateWorkflow.Contains("$releaseTag = $testTag") && testUpdateWorkflow.Contains("--prerelease"), "test update workflow must publish TVx.y.z prerelease assets from codex pushes or manual dispatch without using the production V* namespace.");
            Require(testUpdateWorkflow.Contains("git fetch --force --tags origin") && testUpdateWorkflow.Contains("$latestTest") && testUpdateWorkflow.Contains("Increment-Patch") && testUpdateWorkflow.Contains("New-TestTag"), "test update workflow must increment from the latest production or test TV tag on codex pushes.");
            Require(testUpdateWorkflow.Contains("Delete previous test prerelease") && testUpdateWorkflow.Contains("continue-on-error: true") && testUpdateWorkflow.Contains("--cleanup-tag"), "test update workflow must tolerate the first publish when no previous TVx.y.z prerelease exists.");
            Require(testUpdateWorkflow.Contains("actions/checkout@v6") && testUpdateWorkflow.Contains("PlugHub-Revit2020-$testTag.zip") && testUpdateWorkflow.Contains("PlugHub-Setup-$testTag.exe"), "test update workflow must use current checkout and publish test zip plus installer assets.");
            Require(testUpdateWorkflow.Contains("PLUGHUB_TEST_UPDATE_RELEASE_URI") && testUpdateWorkflow.Contains("The test channel automatically selects the newest TV* prerelease.") && testUpdateWorkflow.Contains("https://api.github.com/repos/$env:GITHUB_REPOSITORY/releases") && !testUpdateWorkflow.Contains("releases/tags/$testTag"), "test update workflow release notes must point Manager at the stable latest-test release list.");
            Require(readme.Contains("PlugHub-Setup") && readme.Contains(@"D:\Program Files\PlugHub"), "README must document the installer EXE and default install directory.");
            Require(readme.Contains("PlugHub-Setup") && readme.Contains("写入 addin") && readme.Contains("Revit 2020"), "README must document release installer behavior.");
        }

        private static void ValidateGiteeReleaseMirrorPackaging()
        {
            var releaseWorkflow = ReadText(".github/workflows/release.yml");
            Require(!File.Exists(FullPath(".workflow/PlugHubRelease.yml")), "Gitee Go release workflow must be removed; release publishing is mirrored from GitHub release.yml.");
            Require(!File.Exists(FullPath(".workflow/scripts/gitee-release.ps1")), "Gitee Go release script must be removed; release.yml owns Gitee release publishing.");
            Require(!File.Exists(FullPath(".gitee/workflows/release.yml")), "Gitee release publishing must not use a copied GitHub Actions .gitee/workflows file.");

            Require(releaseWorkflow.Contains("Publish GitHub release") && releaseWorkflow.Contains("Publish Gitee release"), "GitHub release workflow must publish GitHub release first, then mirror it to Gitee.");
            Require(releaseWorkflow.Contains("Wait for Gitee tag") && releaseWorkflow.Contains("git ls-remote https://gitee.com/GaoMengGu/PlugHub.git \"refs/tags/$tag\""), "GitHub release workflow must wait until the tag exists on Gitee before creating a Gitee release.");
            Require(releaseWorkflow.Contains("GITEE_TOKEN: ${{ secrets.GITEE_TOKEN }}") && releaseWorkflow.Contains("GITEE_TOKEN is required"), "GitHub release workflow must use the Gitee API token for release publishing.");
            Require(releaseWorkflow.Contains("api/v5/repos/GaoMengGu/PlugHub/releases") && releaseWorkflow.Contains("target_commitish = $env:GITHUB_SHA"), "GitHub release workflow must create Gitee releases against the exact GitHub release commit.");
            Require(releaseWorkflow.Contains("DeleteExistingGiteeRelease") && releaseWorkflow.Contains("Invoke-RestMethod -Method Get -Uri \"$ReleaseBaseUri/tags/$EscapedTag\"") && releaseWorkflow.Contains("Invoke-RestMethod -Method Delete") && releaseWorkflow.Contains("$deleteBody = New-FormBody") && releaseWorkflow.Contains("if ($statusCode -eq 404)"), "GitHub release workflow must delete and recreate existing Gitee releases before uploading replacement assets.");
            Require(releaseWorkflow.Contains("New-FormBody") && releaseWorkflow.Contains("application/x-www-form-urlencoded; charset=utf-8"), "GitHub release workflow must submit Gitee release metadata as explicit UTF-8 form data.");
            Require(releaseWorkflow.Contains("attach_files") && releaseWorkflow.Contains("curl.exe -sS -f"), "GitHub release workflow must upload Gitee release attachments and fail on HTTP upload errors.");

            foreach (var asset in new[]
            {
                "PlugHub-Setup-$tag.exe",
                "PlugHub-Setup-$tag.exe.sigstore.json",
                "PlugHub-Revit2020-$tag.zip",
                "PlugHub-Revit2020-$tag.zip.sigstore.json"
            })
            {
                Require(releaseWorkflow.Contains(asset), "GitHub release workflow must mirror Gitee release asset: " + asset);
            }

            Require(!releaseWorkflow.Contains("PlugHub*.dll.sigstore.json"), "Gitee release mirror must not reintroduce DLL signature bundles.");
        }

        private static void ValidateFeatureButtonTooltipBehavior()
        {
            var ribbonBuilder = ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");

            Require(ribbonBuilder.Contains("private static string BuildToolTip(FeatureViewModel feature)"), "feature ribbon builder must centralize feature button tooltip text.");
            Require(ribbonBuilder.Contains("return string.IsNullOrWhiteSpace(feature.Description)") && ribbonBuilder.Contains("feature.Description.Trim()"), "feature button tooltip must only display features[].description.");
            foreach (var metadataToken in new[] { "\"Module: \"", "\"Feature: \"", "\"Category: \"", "\"Command: \"", "\"Command type: \"", "\"Button size: \"" })
            {
                Require(!ribbonBuilder.Contains(metadataToken), "feature button tooltip must not include metadata token " + metadataToken + ".");
            }
        }

        private static void ValidateMachineWideAddinRegistration()
        {
            var addinWriter = ReadText("src/PlugHub.Installer/AddinManifestWriter.cs");
            var installerForm = ReadText("src/PlugHub.Installer/InstallerForm.cs");
            var buildScript = ReadText("scripts/build-revit2020.ps1");
            var installScript = ReadText("scripts/install-addin.ps1");
            var buildProps = ReadText("build/Directory.Build.props");
            var readme = ReadText("README.md");

            Require(addinWriter.Contains("Environment.SpecialFolder.CommonApplicationData"), "installer must resolve the machine-wide ProgramData addins directory.");
            Require(!addinWriter.Contains("Environment.SpecialFolder.ApplicationData"), "installer must not register addins under the current user's APPDATA directory.");
            Require(addinWriter.Contains("Autodesk") && addinWriter.Contains("Revit") && addinWriter.Contains("Addins") && addinWriter.Contains("2020"), "installer must still target the Revit 2020 addins subdirectory.");
            Require(installerForm.Contains("all Windows users") || installerForm.Contains("machine-wide"), "installer UI must describe machine-wide Revit addin registration.");
            Require(buildScript.Contains("$env:ProgramData") && installScript.Contains("$env:ProgramData"), "build and install scripts must use ProgramData for Revit addin manifests.");
            Require(!buildScript.Contains("$env:APPDATA") && !installScript.Contains("$env:APPDATA"), "build and install scripts must not use APPDATA for Revit addin manifests.");
            Require(buildProps.Contains("$(ProgramData)\\Autodesk\\Revit\\Addins\\$(RevitVersion)"), "MSBuild default RevitAddinsDir must use ProgramData.");
            Require(readme.Contains(@"C:\ProgramData\Autodesk\Revit\Addins\2020\PlugHub.addin"), "README must document the machine-wide ProgramData addin path.");
        }

        private static void ValidateUninstallerPackaging()
        {
            var managerProgram = ReadText("src/PlugHub.Manager/Program.cs");
            var maintenanceArguments = ReadText("src/PlugHub.Manager/Maintenance/ManagerMaintenanceArguments.cs");
            var maintenanceLauncher = ReadText("src/PlugHub.Manager/Maintenance/ManagerMaintenanceLauncher.cs");
            var maintenanceRunner = ReadText("src/PlugHub.Manager/Maintenance/ManagerMaintenanceRunner.cs");
            var managerUninstaller = ReadText("src/PlugHub.Manager/Maintenance/ManagerUninstaller.cs");
            var installerProject = ReadText("src/PlugHub.Installer/PlugHub.Installer.csproj");
            var installerPayload = ReadText("src/PlugHub.Installer/InstallerPayload.cs");
            var installerForm = ReadText("src/PlugHub.Installer/InstallerForm.cs");
            var githubWorkflow = ReadText(".github/workflows/release.yml");
            var solution = ReadText("PlugHub.sln");
            var solutionX = ReadText("PlugHub.slnx");
            var readme = ReadText("README.md");

            Require(!File.Exists(FullPath("src/PlugHub.Uninstaller/PlugHub.Uninstaller.csproj")), "standalone uninstaller project file must be removed.");
            Require(!solution.Contains("PlugHub.Uninstaller") && !solutionX.Contains("PlugHub.Uninstaller"), "solutions must not include the old standalone uninstaller project.");
            Require(managerProgram.Contains("ManagerMaintenanceArguments.Parse") && managerProgram.Contains("ManagerMaintenanceRunner"), "PlugHub Manager must dispatch maintenance mode before opening the settings window.");
            Require(maintenanceArguments.Contains("/uninstall") && maintenanceArguments.Contains("/installDirBase64") && maintenanceArguments.Contains("Encoding.UTF8"), "Manager maintenance arguments must support uninstall with UTF-8 Base64 install directory.");
            Require(maintenanceLauncher.Contains("StartUninstall") && maintenanceLauncher.Contains("CreateTemporaryManagerCopy") && maintenanceLauncher.Contains("Path.GetTempPath()") && maintenanceLauncher.Contains("PlugHub.Manager.exe"), "Manager uninstall must run from a temporary PlugHub.Manager.exe copy.");
            Require(maintenanceRunner.Contains("PlugHub Manager - Uninstall") && maintenanceRunner.Contains("MessageBoxButton.YesNo") && maintenanceRunner.Contains("WaitForProcesses"), "Manager uninstall maintenance mode must confirm and wait for locking processes.");
            Require(managerUninstaller.Contains("PlugHub.addin") && managerUninstaller.Contains("SpecialFolder.CommonApplicationData"), "Manager uninstaller must remove the machine-wide ProgramData addin manifest.");
            Require(managerUninstaller.Contains("Directory.Delete") && managerUninstaller.Contains("Refusing to delete a drive root") && managerUninstaller.Contains("RequiredInstallMarkers") && managerUninstaller.Contains("ContainsPlugHubInstallMarkers") && managerUninstaller.Contains("IsAllowedInstallRootName") && managerUninstaller.Contains("Revit2020"), "Manager uninstaller must delete only a marker-validated PlugHub install directory or the local dist/Revit2020 test output.");
            Require(!installerProject.Contains("InstallerUninstallerExe") && !installerProject.Contains("PlugHubUninstaller.exe"), "installer project must not embed a standalone uninstaller.");
            Require(!installerPayload.Contains("PlugHub-Uninstall.exe") && !installerPayload.Contains("WriteUninstaller"), "installer payload must not write a standalone uninstaller.");
            Require(!installerForm.Contains("PlugHub-Uninstall.exe"), "installer UI must not report a standalone uninstaller.");
            Require(!githubWorkflow.Contains("Build PlugHub uninstaller") && !githubWorkflow.Contains("InstallerUninstallerExe"), "GitHub release workflow must not build or embed a standalone uninstaller.");
            Require(!readme.Contains("PlugHub-Uninstall.exe"), "README must not document a standalone uninstaller.");
        }

        private static void ValidateFrameworkAutoUpdateSpecification()
        {
            var solution = ReadText("PlugHub.sln");
            var solutionX = ReadText("PlugHub.slnx");
            var buildProps = ReadText("build/Directory.Build.props");
            var settingsWindow = ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var managerProject = ReadText("src/PlugHub.Manager/PlugHub.Manager.csproj");
            var managerProgram = ReadText("src/PlugHub.Manager/Program.cs");
            var maintenanceArguments = ReadText("src/PlugHub.Manager/Maintenance/ManagerMaintenanceArguments.cs");
            var maintenanceLauncher = ReadText("src/PlugHub.Manager/Maintenance/ManagerMaintenanceLauncher.cs");
            var maintenanceRunner = ReadText("src/PlugHub.Manager/Maintenance/ManagerMaintenanceRunner.cs");
            var managerUpdater = ReadText("src/PlugHub.Manager/Maintenance/ManagerFrameworkUpdater.cs");
            var maintenanceLogger = ReadText("src/PlugHub.Manager/Maintenance/ManagerMaintenanceLogger.cs");
            var frameworkProject = ReadText("src/PlugHub.Framework/PlugHub.Framework.csproj");
            var service = ReadText("src/PlugHub.Framework/Updates/FrameworkUpdateService.cs");
            var releaseClient = ReadText("src/PlugHub.Framework/Updates/ReleaseClient.cs");
            var releaseAssetDownloader = ReadText("src/PlugHub.Framework/Updates/ReleaseAssetDownloader.cs");
            var validator = ReadText("src/PlugHub.Framework/Updates/FrameworkUpdatePackageValidator.cs");
            var githubWorkflow = ReadText(".github/workflows/release.yml");
            var buildScript = ReadText("scripts/build-revit2020.ps1");
            var readme = ReadText("README.md");

            Require(frameworkProject.Contains("System.Web.Extensions"), "framework update release JSON parsing must keep using available net48 framework references.");
            Require(!File.Exists(FullPath("src/PlugHub.Updater/PlugHub.Updater.csproj")), "standalone updater project file must be removed.");
            Require(!solution.Contains("PlugHub.Updater") && !solutionX.Contains("PlugHub.Updater"), "solutions must not include the old standalone updater project.");
            Require(managerProject.Contains("System.IO.Compression") && managerProject.Contains("System.IO.Compression.FileSystem"), "PlugHub Manager must include compression references for update maintenance mode.");
            Require(buildProps.Contains("PlugHubVersion") && buildProps.Contains("PlugHubReleaseTag") && buildProps.Contains("<InformationalVersion>$(PlugHubReleaseTag)</InformationalVersion>"), "shared build props must stamp release tags into assembly informational version.");
            Require(settingsWindow.Contains("AssemblyInformationalVersionAttribute") && settingsWindow.Contains("InformationalVersion") && settingsWindow.Contains("Split('+')"), "About tab and update checks must read the release tag from assembly informational version.");
            Require(settingsWindow.Contains("BuildAboutHeader") && settingsWindow.Contains("AssemblyVersionText()"), "About tab header must show PlugHub followed by the current framework version.");
            Require(settingsWindow.Contains("CreateIconButton(\"refresh\"") && settingsWindow.Contains("CheckFrameworkUpdate"), "About tab must use a compact check-update icon beside the framework version.");
            Require(settingsWindow.Contains("CreateIconButton(\"uninstall\"") && settingsWindow.Contains("LaunchUninstaller"), "About tab must expose a compact uninstall icon after the update icon.");
            Require(!settingsWindow.Contains("CreateIconButton(\"upgrade\"") && !settingsWindow.Contains("ConfirmFrameworkUpdate"), "About tab must merge upgrade into the check-update icon and remove the separate upgrade action.");
            Require(settingsWindow.Contains("CreateBorderlessIconButtonStyle") && settingsWindow.Contains("Control.BorderThicknessProperty") && settingsWindow.Contains("new Thickness(0)"), "About tab icon buttons must be borderless compact glyph actions.");
            Require(!settingsWindow.Contains("CreateButton(\"检查更新\"") && !settingsWindow.Contains("CreateButton(\"更新框架\""), "About tab must not show the old text check/update buttons.");
            Require(settingsWindow.Contains("ShowFrameworkUpdateDialog") && settingsWindow.Contains("ReleaseNotes"), "Check-update icon must show target version and release notes when an update is available.");
            Require(settingsWindow.Contains("if (task.Result.Success && task.Result.HasUpdate)") && settingsWindow.Contains("ShowFrameworkUpdateDialog(task.Result)") && settingsWindow.Contains("UpdateFramework(task.Result)"), "Check-update action must automatically prompt and start the updater when a newer framework release exists.");
            Require(settingsWindow.Contains("CheckFrameworkUpdate") && settingsWindow.Contains("RefreshStatus"), "About tab update actions must write to the bottom-left status text.");
            Require(settingsWindow.Contains("ManagerMaintenanceLauncher.StartUpdate") && settingsWindow.Contains("ManagerMaintenanceLauncher.StartUninstall") && settingsWindow.Contains("MaintenanceWaitProcessIds"), "About tab update and uninstall actions must hand off to PlugHub Manager maintenance mode.");
            Require(managerProgram.Contains("ManagerMaintenanceArguments.Parse") && managerProgram.Contains("ManagerMaintenanceRunner") && managerProgram.Contains("ReadIntOption(args, \"--hostProcessId\")"), "PlugHub Manager must route /update and /uninstall maintenance modes before opening settings and remember the Revit host process id.");
            Require(maintenanceArguments.Contains("/update") && maintenanceArguments.Contains("/uninstall") && maintenanceArguments.Contains("/payloadZipBase64") && maintenanceArguments.Contains("/waitProcessId"), "Manager maintenance arguments must support update, uninstall, encoded paths, and wait process ids.");
            Require(maintenanceLauncher.Contains("StartUpdate") && maintenanceLauncher.Contains("CreateTemporaryManagerCopy") && maintenanceLauncher.Contains("Path.GetTempPath()") && maintenanceLauncher.Contains("Directory.GetFiles(sourceDirectory, \"PlugHub.*.dll\")"), "Manager maintenance launcher must run a temporary manager copy with required framework DLLs.");
            Require(service.Contains("https://gitee.com/api/v5/repos/GaoMengGu/PlugHub/tags") && service.Contains("https://api.github.com/repos/GaoMengGu/PlugHub/releases/latest") && service.Contains("PlugHub-Revit2020-"), "framework update service must use Gitee first and GitHub as fallback for latest release assets.");
            Require(service.Contains("DefaultUpdateSources") && service.Contains("GiteeTagList") && service.Contains("GitHubLatestRelease"), "framework update service must name update source types explicitly.");
            Require(service.Contains("BuildDefaultCheckSources") && service.Contains("BuildCheckSources(currentVersion, _updateSources)") && service.Contains("GitHubReleaseListUri"), "TV builds must query GitHub test prereleases before stable Gitee/GitHub release sources.");
            Require(service.Contains("AssetDownloadUrls") && service.Contains("DownloadFallbackUrls"), "framework update service must preserve fallback asset URLs for blocked or failed hosts.");
            Require(service.Contains("SelectUpdateAsset") && service.Contains("ReleaseNotes = release.Body"), "framework update service must select the update zip and preserve release notes.");
            Require(!service.Contains("升级图标"), "framework update check message must not reference the removed upgrade icon.");
            Require(releaseClient.Contains("Body = StringValue(root, \"body\")") && releaseClient.Contains("AssetObjects("), "release client must parse release body and release assets from GitHub/Gitee JSON.");
            Require(releaseClient.Contains("ParseLatestTestPrereleaseJson") && releaseClient.Contains("prerelease") && releaseClient.Contains("draft") && releaseClient.Contains("IsTestReleaseTag"), "release client must parse the newest non-draft TV prerelease from GitHub release lists.");
            Require(releaseClient.Contains("ParseGiteeTagsJson") && releaseClient.Contains("CreateGiteeReleaseDownloadUrl"), "release client must parse Gitee tags and generate Gitee release asset URLs.");
            Require(!service.Contains("PlugHub.Updater.exe") && !service.Contains("StartUpdater") && !service.Contains("CreateTemporaryUpdaterCopy"), "framework update service must not know about a standalone updater executable.");
            Require(releaseClient.Contains("HttpWebRequest") && releaseClient.Contains("UserAgent") && releaseClient.Contains("JavaScriptSerializer"), "release client must use net48-compatible HTTP and JSON APIs.");
            Require(releaseClient.Contains("EnsureSecureTransport()") && releaseAssetDownloader.Contains("EnsureSecureTransport()") && releaseAssetDownloader.Contains("KeepAlive = false"), "framework update HTTP client and asset downloader must harden TLS transport before HTTPS requests.");
            Require(releaseClient.Contains("EnsureHttpsResponse(response.ResponseUri)") && releaseClient.IndexOf("EnsureHttpsResponse(response.ResponseUri)", StringComparison.Ordinal) < releaseClient.IndexOf("reader.ReadToEnd()", StringComparison.Ordinal), "release client must reject redirects away from HTTPS before reading release API text.");
            Require(releaseAssetDownloader.Contains("ResolveTargetPath(targetDirectory, fileName)") && releaseAssetDownloader.Contains("EnsureHttpsResponse(response.ResponseUri)") && releaseAssetDownloader.IndexOf("EnsureHttpsResponse(response.ResponseUri)", StringComparison.Ordinal) < releaseAssetDownloader.IndexOf("stream.CopyTo(target)", StringComparison.Ordinal), "framework update asset downloader must keep target paths local and reject redirects away from HTTPS before writing bytes.");
            Require(validator.Contains("PlugHub.Revit2020.dll") && validator.Contains("PlugHub.Framework.dll") && validator.Contains("PlugHub.Contracts.dll") && validator.Contains("PlugHub.Wpf.dll") && !validator.Contains("PlugHub.Manager.dll") && !validator.Contains("PlugHub.Updater.exe") && validator.Contains("PlugHub.Manager.exe") && !validator.Contains("PlugHub-Uninstall.exe"), "update package validator must require the core framework DLLs and manager app only.");
            Require(validator.Contains("IsSafeZipEntry") && validator.Contains("Path.DirectorySeparatorChar"), "update package validator must reject unsafe zip paths.");
            Require(maintenanceRunner.Contains("ManagerFrameworkUpdater") && maintenanceRunner.Contains("ManagerUninstaller") && maintenanceRunner.Contains("PlugHub Manager maintenance failed"), "Manager maintenance runner must dispatch update and uninstall maintenance modes.");
            Require(maintenanceRunner.Contains("PlugHub Manager - Update") && maintenanceRunner.Contains("PlugHub was updated successfully"), "Manager update maintenance must show a completion prompt after files are replaced.");
            Require(maintenanceRunner.Contains("ShowThemedUpdateCompletedDialog") && maintenanceRunner.Contains("RevitUiTheme.Apply(dialog)") && !maintenanceRunner.Contains("MessageBox.Show(\"PlugHub was updated successfully."), "Manager update completion must use a themed WPF dialog instead of the native MessageBox.");
            Require(managerUpdater.Contains("WaitForExit") && managerUpdater.Contains("CopyFrameworkFiles") && managerUpdater.Contains("ShouldCopyUpdateEntry"), "Manager update maintenance must wait for locking processes before copying framework files.");
            Require(managerUpdater.Contains("FrameworkUpdatePackageValidator") && managerUpdater.IndexOf("new FrameworkUpdatePackageValidator().Validate(args.PayloadZip)", StringComparison.Ordinal) < managerUpdater.IndexOf("CopyFrameworkFiles(args.PayloadZip", StringComparison.Ordinal), "Manager update maintenance must validate payload zip before copying framework files.");
            Require(managerUpdater.Contains("RequiredInstallMarkers") && managerUpdater.Contains("ContainsPlugHubInstallMarkers") && managerUpdater.Contains("IsAllowedInstallRootName") && managerUpdater.Contains("Revit2020"), "Manager update maintenance must refuse marker-validated parent directories that are not PlugHub install roots.");
            Require(managerUpdater.Contains("PlugHub.Manager.exe") && managerUpdater.Contains("StaleMaintenanceArtifacts") && managerUpdater.Contains("PlugHub.Updater.exe") && managerUpdater.Contains("PlugHub.Updater.pdb") && managerUpdater.Contains("PlugHub.Uninstaller.pdb") && managerUpdater.Contains("PlugHub-Uninstall.exe"), "Manager update maintenance must replace Manager and clean stale standalone maintenance artifacts.");
            Require(managerUpdater.Contains("MaxBackupDirectoriesToKeep = 3") && managerUpdater.Contains("PruneOldBackups") && managerUpdater.Contains("GetCreationTimeUtc"), "Manager update maintenance must keep only the latest three update backup directories.");
            Require(!managerUpdater.Contains("PlugHub.addin") && !managerUpdater.Contains("Directory.Delete(installDirectory"), "Manager update maintenance must avoid addin/config/packages replacement and install directory deletion.");
            Require(maintenanceLogger.Contains("plughub-manager-maintenance-") && maintenanceLogger.Contains("Encoding.UTF8"), "Manager maintenance must write UTF-8 logs with a distinct file prefix.");
            Require(!githubWorkflow.Contains("Build PlugHub updater") && !githubWorkflow.Contains("PlugHub.Updater.csproj"), "GitHub release workflow must not build a standalone updater.");
            Require(githubWorkflow.Contains("Publish Gitee release") && githubWorkflow.Contains("PlugHub-Revit2020-$tag.zip"), "GitHub release workflow must mirror the built updater package to Gitee release assets.");
            Require(!buildScript.Contains("PlugHub.Updater.csproj") && !buildScript.Contains("PlugHub.Uninstaller.csproj"), "local Revit 2020 build script must not build standalone maintenance executables.");
            Require(buildScript.Contains("PlugHub.Manager.csproj") && buildScript.Contains("PlugHub.Manager.exe"), "local Revit 2020 build script must stage PlugHub.Manager.exe.");
            Require(buildScript.Contains("PlugHub.Updater.exe") && buildScript.Contains("PlugHub-Uninstall.exe") && buildScript.Contains("StaleOutputPaths"), "local Revit 2020 build script must remove stale standalone maintenance executables from dist.");
            Require(buildScript.Contains("Resolve-PlugHubReleaseVersion") && buildScript.Contains("^T?V(?<version>\\d+\\.\\d+\\.\\d+)$") && buildScript.Contains("/p:PlugHubVersion=$($PlugHubReleaseVersion.Version)") && buildScript.Contains("/p:PlugHubReleaseTag=$($PlugHubReleaseVersion.ReleaseTag)"), "local, release, and test update builds must stamp PlugHub DLLs with the release tag version.");
            Require(readme.Contains("检查更新小图标") && readme.Contains("卸载小图标") && readme.Contains("自动弹出目标版本号") && readme.Contains("框架 DLL 和 PlugHub Manager") && !readme.Contains("PlugHub.Updater.exe") && !readme.Contains("PlugHub-Uninstall.exe"), "README must document single-Manager framework update and uninstall behavior.");
            Require(readme.Contains("已是最新版本") && readme.Contains("关闭弹窗则退出更新") && !readme.Contains("Revit 实机测试成功"), "README must document updater behavior without claiming Revit real-machine validation.");

            ValidateReleaseClientParsesUpdatePackageAndNotes();
            ValidateFrameworkUpdateSelectsExactReleaseAsset();
            ValidateFrameworkUpdatePackageRejectsUnsafeZip();
            ValidateFrameworkUpdatePackageAcceptsSingleManagerMaintenancePayload();
            ValidateFrameworkUpdatePackageRequiresManager();
        }

        private static void ValidateReleaseClientParsesUpdatePackageAndNotes()
        {
            var client = new PlugHub.Framework.Updates.ReleaseClient();
            var release = client.ParseReleaseJson(
                "{\"tag_name\":\"V9.8.7\",\"body\":\"- 修复更新检查\",\"assets\":[{\"name\":\"PlugHub-Revit2020-V9.8.7.zip\",\"browser_download_url\":\"https://github.com/GaoMengGu/PlugHub/releases/download/V9.8.7/PlugHub-Revit2020-V9.8.7.zip\"},{\"name\":\"PlugHub-Revit2020-V9.8.7.zip.sigstore.json\",\"browser_download_url\":\"https://github.com/GaoMengGu/PlugHub/releases/download/V9.8.7/PlugHub-Revit2020-V9.8.7.zip.sigstore.json\"}]}");

            Require(release.Body.Contains("修复更新检查"), "release client must preserve release body for update confirmation.");
            Require(release.Assets.Any(asset => asset.Name == "PlugHub-Revit2020-V9.8.7.zip" && asset.DownloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)), "release client must parse the PlugHub-Revit2020 zip asset download URL.");

            var giteeRelease = client.ParseGiteeTagsJson(
                "[{\"name\":\"V1.4.8\",\"commit\":{\"sha\":\"a\"}},{\"name\":\"V1.4.10\",\"commit\":{\"sha\":\"b\"}},{\"name\":\"not-release\"}]",
                "https://gitee.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}");
            Require(giteeRelease.TagName == "V1.4.10", "release client must select the newest semantic V* tag from Gitee tags.");
            Require(giteeRelease.Assets.Any(asset => asset.DownloadUrl == "https://gitee.com/GaoMengGu/PlugHub/releases/download/V1.4.10/PlugHub-Revit2020-V1.4.10.zip"), "release client must generate the Gitee release zip download URL.");
        }

        private static void ValidateFrameworkUpdateSelectsExactReleaseAsset()
        {
            var method = typeof(PlugHub.Framework.Updates.FrameworkUpdateService).GetMethod(
                "SelectUpdateAsset",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("framework update asset selector must remain verifiable.");
            }

            var release = new PlugHub.Framework.Updates.ReleaseInfo
            {
                TagName = "V2.0.0",
                Assets = new List<PlugHub.Framework.Updates.ReleaseAssetInfo>
                {
                    new PlugHub.Framework.Updates.ReleaseAssetInfo
                    {
                        Name = "PlugHub-Revit2020-V1.9.0.zip",
                        DownloadUrl = "https://example.com/PlugHub-Revit2020-V1.9.0.zip"
                    },
                    new PlugHub.Framework.Updates.ReleaseAssetInfo
                    {
                        Name = "PlugHub-Revit2020-V2.0.0.zip.sigstore.json",
                        DownloadUrl = "https://example.com/PlugHub-Revit2020-V2.0.0.zip.sigstore.json"
                    }
                }
            };

            var selected = method.Invoke(null, new object[] { release, "V2.0.0" });
            Require(selected == null, "framework update check must not accept a mismatched Revit2020 zip when the exact target release asset is missing.");

            release.Assets.Add(new PlugHub.Framework.Updates.ReleaseAssetInfo
            {
                Name = "PlugHub-Revit2020-V2.0.0.zip",
                DownloadUrl = "https://example.com/PlugHub-Revit2020-V2.0.0.zip"
            });

            selected = method.Invoke(null, new object[] { release, "V2.0.0" });
            Require(selected is PlugHub.Framework.Updates.ReleaseAssetInfo asset && asset.Name == "PlugHub-Revit2020-V2.0.0.zip", "framework update check must select the exact target release zip.");
        }

        private static void ValidateFrameworkUpdatePackageRejectsUnsafeZip()
        {
            var validator = new PlugHub.Framework.Updates.FrameworkUpdatePackageValidator();
            var temp = Path.Combine(Path.GetTempPath(), "PlugHubStaticValidation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var zip = Path.Combine(temp, "unsafe.zip");
                using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                {
                    archive.CreateEntry("../PlugHub.Revit2020.dll");
                }

                var failed = false;
                try
                {
                    validator.Validate(zip);
                }
                catch (InvalidDataException)
                {
                    failed = true;
                }

                Require(failed, "framework update validator must reject unsafe zip entry paths.");
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        private static void ValidateFrameworkUpdatePackageAcceptsSingleManagerMaintenancePayload()
        {
            var validator = new PlugHub.Framework.Updates.FrameworkUpdatePackageValidator();
            var temp = Path.Combine(Path.GetTempPath(), "PlugHubStaticValidation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var zip = Path.Combine(temp, "manager-maintenance.zip");
                using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                {
                    archive.CreateEntry("PlugHub.Revit2020.dll");
                    archive.CreateEntry("PlugHub.Framework.dll");
                    archive.CreateEntry("PlugHub.Contracts.dll");
                    archive.CreateEntry("PlugHub.Wpf.dll");
                    archive.CreateEntry("PlugHub.Manager.exe");
                }

                validator.Validate(zip);
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        private static void ValidateFrameworkUpdatePackageRequiresManager()
        {
            var validator = new PlugHub.Framework.Updates.FrameworkUpdatePackageValidator();
            var temp = Path.Combine(Path.GetTempPath(), "PlugHubStaticValidation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                var zip = Path.Combine(temp, "missing-settings-app.zip");
                using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                {
                    archive.CreateEntry("PlugHub.Revit2020.dll");
                    archive.CreateEntry("PlugHub.Framework.dll");
                    archive.CreateEntry("PlugHub.Contracts.dll");
                    archive.CreateEntry("PlugHub.Wpf.dll");
                }

                var failed = false;
                try
                {
                    validator.Validate(zip);
                }
                catch (InvalidDataException ex) when (ex.Message.Contains("PlugHub.Manager.exe"))
                {
                    failed = true;
                }

                Require(failed, "framework update validator must require PlugHub.Manager.exe in release packages.");
            }
            finally
            {
                Directory.Delete(temp, true);
            }
        }

        private static void ValidateReleaseVersioningWorkflow()
        {
            var releaseWorkflow = ReadText(".github/workflows/release.yml");
            var syncWorkflow = ReadText(".github/workflows/sync-gitee.yml");
            var readme = ReadText("README.md");

            Require(!File.Exists(FullPath(".github/workflows/auto-version.yml")), "release versioning must live in release.yml, not a separate auto-version workflow.");
            Require(!releaseWorkflow.Contains("branches:") && releaseWorkflow.Contains("tags:") && releaseWorkflow.Contains("\"V*\""), "release workflow must run for V* tag pushes instead of automatic main push releases.");
            Require(releaseWorkflow.Contains("workflow_dispatch") && releaseWorkflow.Contains("version:"), "release workflow must support manual explicit version input.");
            Require(releaseWorkflow.Contains("PLUGHUB_RELEASE_INPUT_VERSION") && releaseWorkflow.Contains("$env:GITHUB_REF") && releaseWorkflow.Contains("refs/tags/V*"), "release workflow must resolve tag pushes from GitHub environment variables.");
            Require(releaseWorkflow.Contains("git fetch --force --tags origin"), "release workflow must force-refresh tags to avoid local tag clobber conflicts on tag-triggered runs.");
            Require(releaseWorkflow.Contains("Resolve release tag") && releaseWorkflow.Contains("--sort=-v:refname") && releaseWorkflow.Contains("+ 1"), "release workflow must increment the latest patch version by default.");
            Require(releaseWorkflow.Contains("^V\\d+\\.\\d+\\.\\d+$") && releaseWorkflow.Contains("git ls-remote"), "release workflow must validate Vx.y.z tags and reject existing remote tags.");
            Require(releaseWorkflow.Contains("git tag") && releaseWorkflow.Contains("git push origin \"refs/tags/${{ steps.resolve.outputs.tag }}\""), "release workflow must create and push the resolved release tag.");
            Require(releaseWorkflow.Contains("actions: write") && releaseWorkflow.Contains("contents: write"), "release workflow must have permissions to create tags and dispatch workflows.");
            Require(releaseWorkflow.Contains("PLUGHUB_RELEASE_TAG") && releaseWorkflow.Contains("needs.resolve-release-tag.outputs.tag"), "release workflow must use the resolved release tag for packaging and publishing.");
            Require(releaseWorkflow.Contains("PlugHub-Setup-${{ env.PLUGHUB_RELEASE_TAG }}.exe.sigstore.json") && releaseWorkflow.Contains("PlugHub-Revit2020-${{ env.PLUGHUB_RELEASE_TAG }}.zip.sigstore.json"), "GitHub release workflow must publish zip and setup signature bundles.");
            Require(!releaseWorkflow.Contains("PlugHub*.dll.sigstore.json"), "GitHub release workflow must not publish DLL signature bundles.");
            Require(releaseWorkflow.Contains("Generate release notes") && releaseWorkflow.Contains("body_path: PlugHub-ReleaseNotes-${{ env.PLUGHUB_RELEASE_TAG }}.md"), "GitHub release workflow must publish concise release notes.");
            Require(releaseWorkflow.Contains("sync-gitee.yml/dispatches"), "release workflow must dispatch Gitee sync after creating a tag.");
            Require(syncWorkflow.Contains("workflow_dispatch"), "Gitee sync workflow must support explicit dispatch from release.yml.");
            Require(!readme.Contains("release.yml") && !readme.Contains("workflow_dispatch") && !readme.Contains("每次 `main` 推送"), "root README must not document release automation.");
        }

        private static void ValidateSigningGuidance()
        {
            var signingScript = ReadText("scripts/sign-revit2020.ps1");
            var workflow = ReadText(".github/workflows/release.yml");

            foreach (var token in new[] { "self-signed", "signtool", "Thumbprint" })
            {
                Require(signingScript.Contains(token), "signing guidance must mention: " + token);
            }

            Require(signingScript.Contains("signtool") && signingScript.Contains("/fd SHA256") && signingScript.Contains("/tr"), "signing script must use Authenticode SHA256 signing with timestamp support.");
            Require(workflow.Contains("push:") && workflow.Contains("tags:") && workflow.Contains("\"V*\"") && workflow.Contains("workflow_dispatch"), "release workflow must run for version tag pushes and explicit dispatch.");
            Require(workflow.Contains("sigstore/cosign-installer") && workflow.Contains("cosign sign-blob") && workflow.Contains("id-token: write"), "release workflow must use keyless cosign blob signing.");
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
                "PlugHub.Manager.exe",
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
                "PlugHub.Updater.exe",
                "PlugHub.Updater.exe.config",
                "PlugHub.Updater.pdb",
                "PlugHub.Uninstaller.exe",
                "PlugHub.Uninstaller.exe.config",
                "PlugHub.Uninstaller.pdb",
                "PlugHub-Uninstall.exe",
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

        private static string MethodBody(string source, string methodName)
        {
            var token = methodName + "(";
            var start = -1;
            var search = 0;
            while (search < source.Length)
            {
                var candidate = source.IndexOf(token, search, StringComparison.Ordinal);
                if (candidate < 0) break;

                var lineStart = source.LastIndexOf('\n', candidate);
                var line = source.Substring(lineStart + 1, candidate - lineStart - 1);
                if (line.Contains("private ") && !line.Contains("="))
                {
                    start = lineStart + 1;
                    break;
                }

                search = candidate + token.Length;
            }

            Require(start >= 0, "missing method: " + methodName);

            var next = source.IndexOf("\n        private ", start + methodName.Length, StringComparison.Ordinal);
            return next >= 0 ? source.Substring(start, next - start) : source.Substring(start);
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

            foreach (var file in Directory.GetFiles(packagesDirectory, "packages.json", SearchOption.AllDirectories)
                         .Concat(Directory.GetFiles(packagesDirectory, "*.packages.json", SearchOption.AllDirectories))
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
                .All(source => StringValue(source, "manifestPath") == "packages.json");
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

        private static bool TryObjectValue(Dictionary<string, object> source, string key, out Dictionary<string, object> result)
        {
            if (source.TryGetValue(key, out var value) && value is Dictionary<string, object> objectValue)
            {
                result = objectValue;
                return true;
            }

            result = new Dictionary<string, object>();
            return false;
        }

        private static ArrayList ArrayValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value) && value is ArrayList result ? result : new ArrayList();
        }

        private static bool TryArrayValue(Dictionary<string, object> source, string key, out ArrayList result)
        {
            if (source.TryGetValue(key, out var value) && value is ArrayList arrayValue)
            {
                result = arrayValue;
                return true;
            }

            result = new ArrayList();
            return false;
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
