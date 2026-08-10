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
                new PlugHub.StaticValidation.Validation.ConfigurationAndRibbonValidator(new PlugHub.StaticValidation.Validation.ValidationSource(Root)).Validate();
                new PlugHub.StaticValidation.Validation.RuntimeIsolationValidator(new PlugHub.StaticValidation.Validation.ValidationSource(Root)).Validate();
                ValidateFrameworkContainsNoBundledModules();
                ValidatePlugHubV2Specification();
                new PlugHub.StaticValidation.Validation.SettingsUiValidator(new PlugHub.StaticValidation.Validation.ValidationSource(Root)).Validate();
                new PlugHub.StaticValidation.Validation.RepositorySourcesValidator(new PlugHub.StaticValidation.Validation.ValidationSource(Root)).Validate();
                new PlugHub.StaticValidation.Validation.PackageOperationsValidator().Validate();
                new PlugHub.StaticValidation.Validation.ReleaseAndDeploymentValidator(Root).Validate();

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
                "src/PlugHub.Framework/Updates/FrameworkUpdatePolicy.cs",
                "src/PlugHub.Framework/Updates/FrameworkUpdateModels.cs",
                "src/PlugHub.Framework/Updates/ReleaseClient.cs",
                "src/PlugHub.Framework/Updates/ReleaseAssetDownloader.cs",
                "src/PlugHub.Framework/Updates/FrameworkUpdatePackageValidator.cs",
                "src/PlugHub.Revit2020/PlugHub.Revit2020.csproj",
                "src/PlugHub.Installer/PlugHub.Installer.csproj",
                "src/PlugHub.Installer/app.manifest",
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
                "src/PlugHub.StaticValidation/Validation/PackageOperationsValidator.cs",
                "src/PlugHub.StaticValidation/Validation/ReleaseAndDeploymentValidator.cs",
                "src/PlugHub.StaticValidation/Validation/SettingsUiValidator.cs",
                "src/PlugHub.StaticValidation/Validation/ValidationSource.cs",
                "src/PlugHub.StaticValidation/Validation/ConfigurationAndRibbonValidator.cs",
                "src/PlugHub.StaticValidation/Validation/RuntimeIsolationValidator.cs",
                "src/PlugHub.StaticValidation/Validation/RepositorySourcesValidator.cs",
                "src/PlugHub.Contracts/Modules/IPlugHubModule.cs",
                "src/PlugHub.Contracts/Loading/AlcLoadRules.cs",
                "src/PlugHub.Framework/Composition/FeatureViewComposer.cs",
                "src/PlugHub.Framework/Composition/FeatureSlotAllocator.cs",
                "src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs",
                "src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs",
                "src/PlugHub.Framework/Runtime/FrameworkRuntime.cs",
                "src/PlugHub.Framework/Registry/FeatureRegistry.cs",
                "src/PlugHub.Framework/Packages/RepositoryCredentialService.cs",
                "src/PlugHub.Framework/Packages/RepositoryAddress.cs",
                "src/PlugHub.Framework/Packages/RepositoryBrowser.cs",
                "src/PlugHub.Framework/Packages/RepositoryArchiveSynchronizer.cs",
                "src/PlugHub.Framework/Packages/RepositoryRemoteTransport.cs",
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
                "src/PlugHub.Framework/Packages/PendingPackageOperationLifecycle.cs",
                "src/PlugHub.Framework/Packages/RepositoryPackageInstallState.cs",
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
                "src/PlugHub.Manager/Settings/Rows/FeatureRow.cs",
                "src/PlugHub.Manager/Settings/Rows/GroupRow.cs",
                "src/PlugHub.Manager/Settings/Rows/RepositoryRow.cs",
                "src/PlugHub.Manager/Settings/Rows/RepositoryPackageRow.cs",
                "src/PlugHub.Manager/Resources/alipay.png",
                "src/PlugHub.Manager/Resources/wechatpay.png",
                "src/PlugHub.Manager/Program.cs",
                "src/PlugHub.Manager/Maintenance/ManagerMaintenanceMode.cs",
                "src/PlugHub.Manager/Maintenance/ManagerMaintenanceArguments.cs",
                "src/PlugHub.Manager/Maintenance/ManagerMaintenanceLauncher.cs",
                "src/PlugHub.Manager/Maintenance/ManagerMaintenanceRunner.cs",
                "src/PlugHub.Manager/Maintenance/ManagerFrameworkUpdater.cs",
                "src/PlugHub.Manager/Maintenance/ManagerUninstaller.cs",
                "src/PlugHub.Manager/Maintenance/PlugHubInstallRootPolicy.cs",
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
            Require(validationProgram.Contains("new PlugHub.StaticValidation.Validation.PackageOperationsValidator().Validate()"), "Static validation entrypoint must delegate package operation behavior to PackageOperationsValidator.");
            Require(validationProgram.Contains("new PlugHub.StaticValidation.Validation.ReleaseAndDeploymentValidator(Root).Validate()"), "Static validation entrypoint must delegate release and deployment rules to ReleaseAndDeploymentValidator.");
            Require(!validationProgram.Contains("private static void ValidatePendingPackage" + "OperationStoreBehavior()") && !validationProgram.Contains("private static void ValidateRelease" + "InstallerPackaging()"), "Static validation entrypoint must not absorb extracted package or release validator implementations.");
            var packageOperationsValidator = ReadText("src/PlugHub.StaticValidation/Validation/PackageOperationsValidator.cs");
            var releaseAndDeploymentValidator = ReadText("src/PlugHub.StaticValidation/Validation/ReleaseAndDeploymentValidator.cs");
            Require(packageOperationsValidator.Contains("internal sealed class PackageOperationsValidator") && packageOperationsValidator.Contains("public void Validate()"), "package operation validation must remain a single-entry validator module.");
            Require(packageOperationsValidator.Contains("ValidatePendingPackageOperationStoreBehavior") && packageOperationsValidator.Contains("ValidateLockedPackageOperationBehavior") && packageOperationsValidator.Contains("ValidateRepositoryInstallFailureDoesNotCreateOrRemovePackages"), "PackageOperationsValidator must own pending, locked-file, and rollback behavior.");
            var packageRepositoryService = ReadText("src/PlugHub.Framework/Packages/PackageRepositoryService.cs");
            var pendingLifecycle = ReadText("src/PlugHub.Framework/Packages/PendingPackageOperationLifecycle.cs");
            Require(packageRepositoryService.Contains("PendingPackageOperationLifecycle") && packageRepositoryService.Contains("_pendingLifecycle.Apply(baseDirectory)") && packageRepositoryService.Contains("_pendingLifecycle.Cancel(baseDirectory, packageId, moduleId)"), "PackageRepositoryService must delegate deferred operation execution and cancellation to PendingPackageOperationLifecycle.");
            Require(pendingLifecycle.Contains("internal sealed class PendingPackageOperationLifecycle") && pendingLifecycle.Contains("public IReadOnlyList<DiagnosticMessage> Apply") && pendingLifecycle.Contains("public PackageRepositoryOperationResult Cancel"), "pending package operation lifecycle must own apply and cancel behavior behind one internal module.");
            Require(pendingLifecycle.Contains("RestoreManifestBackups") && pendingLifecycle.Contains("TryValidateInstallDirectory") && pendingLifecycle.Contains("TryValidateStagingDirectory") && pendingLifecycle.Contains("TryFindLockedFile"), "pending package operation lifecycle must own backup restore, path validation, and locked-file retry rules.");
            Require(!packageRepositoryService.Contains("private bool TryApplyPendingDelete") && !packageRepositoryService.Contains("private bool TryApplyPendingUpdate") && !packageRepositoryService.Contains("RestorePendingManifestBackups") && !packageRepositoryService.Contains("TryValidatePendingStagingDirectory"), "PackageRepositoryService must not absorb deferred operation lifecycle implementation.");
            Require(releaseAndDeploymentValidator.Contains("internal sealed class ReleaseAndDeploymentValidator") && releaseAndDeploymentValidator.Contains("public void Validate()"), "release and deployment validation must remain a single-entry validator module.");
            Require(releaseAndDeploymentValidator.Contains("ValidateReleaseInstallerPackaging") && releaseAndDeploymentValidator.Contains("ValidateFrameworkAutoUpdateSpecification") && releaseAndDeploymentValidator.Contains("ValidateRevitDeploymentConfiguration"), "ReleaseAndDeploymentValidator must own installer, update, and deployed-output rules.");
            var settingsUiValidator = ReadText("src/PlugHub.StaticValidation/Validation/SettingsUiValidator.cs");
            Require(validationProgram.Contains("new PlugHub.StaticValidation.Validation.SettingsUiValidator(new PlugHub.StaticValidation.Validation.ValidationSource(Root)).Validate()"), "Static validation entrypoint must delegate Manager/WPF rules to SettingsUiValidator.");
            Require(settingsUiValidator.Contains("internal sealed class SettingsUiValidator") && settingsUiValidator.Contains("public void Validate()") && settingsUiValidator.Contains("ValidationSource"), "settings UI validation must remain a single-entry validator with an internal source context.");
            Require(settingsUiValidator.Contains("ValidateSettingsPaneV21Specification") && settingsUiValidator.Contains("ValidateSettingsGroupFeatureEditingBehavior"), "SettingsUiValidator must own Manager/WPF layout and editing rules.");
            Require(!validationProgram.Contains("private static void ValidateSettingsPaneV21" + "Specification()") && !validationProgram.Contains("private static void ValidateSettingsGroupFeatureEditing" + "Behavior()"), "Static validation entrypoint must not absorb extracted settings UI implementation.");
            var validationSource = ReadText("src/PlugHub.StaticValidation/Validation/ValidationSource.cs");
            Require(validationSource.Contains("line.Contains(\"private \")") && validationSource.Contains("missing method: "), "ValidationSource method-body lookup must resolve private method declarations instead of call sites.");
            var configurationAndRibbonValidator = ReadText("src/PlugHub.StaticValidation/Validation/ConfigurationAndRibbonValidator.cs");
            Require(validationProgram.Contains("new PlugHub.StaticValidation.Validation.ConfigurationAndRibbonValidator(new PlugHub.StaticValidation.Validation.ValidationSource(Root)).Validate()"), "Static validation entrypoint must delegate configuration and Ribbon rules to ConfigurationAndRibbonValidator.");
            Require(configurationAndRibbonValidator.Contains("internal sealed class ConfigurationAndRibbonValidator") && configurationAndRibbonValidator.Contains("public void Validate()"), "configuration and Ribbon validation must remain a single-entry validator module.");
            Require(configurationAndRibbonValidator.Contains("ValidateModulesManifestSchemaAndCompatibility") && configurationAndRibbonValidator.Contains("ValidateRibbonLayoutRules") && configurationAndRibbonValidator.Contains("ValidateConfiguredRibbonLayoutAppendsUnplacedFeaturesByGroup"), "ConfigurationAndRibbonValidator must own manifest compatibility, Ribbon rules, and configured fallback behavior.");
            Require(!validationProgram.Contains("private static void ValidateModulesManifestSchemaAnd" + "Compatibility()") && !validationProgram.Contains("private static void ValidateRibbonLayout" + "Rules()"), "Static validation entrypoint must not absorb extracted configuration or Ribbon implementation.");
            var runtimeIsolationValidator = ReadText("src/PlugHub.StaticValidation/Validation/RuntimeIsolationValidator.cs");
            Require(validationProgram.Contains("new PlugHub.StaticValidation.Validation.RuntimeIsolationValidator(new PlugHub.StaticValidation.Validation.ValidationSource(Root)).Validate()"), "Static validation entrypoint must delegate runtime isolation rules to RuntimeIsolationValidator.");
            Require(runtimeIsolationValidator.Contains("internal sealed class RuntimeIsolationValidator") && runtimeIsolationValidator.Contains("public void Validate()"), "runtime isolation validation must remain a single-entry validator module.");
            Require(runtimeIsolationValidator.Contains("ValidateRuntimeRoutingSpecification") && runtimeIsolationValidator.Contains("ValidateManifestAuthoritativeDiscoverySpecification") && runtimeIsolationValidator.Contains("ValidateFrameworkRuntimeLoadIsolation"), "RuntimeIsolationValidator must own command routing, manifest-authoritative discovery, and runtime isolation rules.");
            Require(!validationProgram.Contains("private static void ValidateRuntimeRouting" + "Specification()") && !validationProgram.Contains("private static void ValidateFrameworkRuntimeLoad" + "Isolation()"), "Static validation entrypoint must not absorb extracted runtime isolation implementation.");
            var repositorySourcesValidator = ReadText("src/PlugHub.StaticValidation/Validation/RepositorySourcesValidator.cs");
            Require(validationProgram.Contains("new PlugHub.StaticValidation.Validation.RepositorySourcesValidator(new PlugHub.StaticValidation.Validation.ValidationSource(Root)).Validate()"), "Static validation entrypoint must delegate repository source rules to RepositorySourcesValidator.");
            Require(repositorySourcesValidator.Contains("internal sealed class RepositorySourcesValidator") && repositorySourcesValidator.Contains("public void Validate()"), "repository source validation must remain a single-entry validator module.");
            Require(repositorySourcesValidator.Contains("ValidatePackageSourceAndReleaseBehavior") && repositorySourcesValidator.Contains("ValidateRepositoryCredentialAndRedactionBehavior"), "RepositorySourcesValidator must own repository source, release mirror, credential, and redaction rules.");
            Require(!validationProgram.Contains("private static void ValidatePackageSourceAndRelease" + "Behavior()") && !validationProgram.Contains("private static void ValidateRepositoryCredentialAndRedaction" + "Behavior()"), "Static validation entrypoint must not absorb extracted repository source implementation.");
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
