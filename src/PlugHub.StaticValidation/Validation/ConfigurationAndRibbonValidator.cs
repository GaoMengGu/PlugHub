using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class ConfigurationAndRibbonValidator
    {
        private readonly ValidationSource _source;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public ConfigurationAndRibbonValidator(ValidationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public void Validate()
        {
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
        }

        private void ValidateConfiguration()
        {
            var modules = _source.ReadObject("config/sources.example.json");
            var views = _source.ReadObject("config/views.example.json");
            var presets = _source.ReadObject("config/feature-combinations.example.json");

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

        private void ValidateViewCompositionExamples()
        {
            var views = _source.ReadObject("config/views.example.json");
            var features = AllModules().SelectMany(Features).ToList();
            var byView = Views(views).ToDictionary(view => StringValue(view, "id"), view => FeatureIdsForView(features, view), StringComparer.OrdinalIgnoreCase);

            Require(byView.ContainsKey("workspace"), "workspace view is required.");
            var workspace = byView["workspace"];
            Require(workspace.Count == 0, "framework workspace should not expose bundled features.");
        }

        private void ValidateComposerShape()
        {
            var composer = _source.ReadText("src/PlugHub.Framework/Composition/FeatureViewComposer.cs");
            Require(composer.Contains("ComposeDetailed"), "composer must expose a detailed composition result.");
            Require(composer.Contains("MatchesGroup"), "composer must use MatchesGroup.");
            Require(composer.Contains("FeatureViewCompositionResult"), "composer must return a composition result wrapper.");
            Require(composer.Contains("FeatureViewComparer"), "composer must use a deterministic comparer.");
            Require(composer.Contains("feature.category"), "composer must support category sorting.");
            Require(composer.Contains("SkippedFeatures"), "composer must capture skipped features.");
            Require(composer.Contains("CreateFallbackGroup"), "composer must show external module features even when workspace groups are empty.");
        }

        private void ValidateCoreContracts()
        {
            var contractText = _source.ReadAllCSharp("src/PlugHub.Contracts");
            foreach (var token in new[] { "interface IPlugHubModule", "class ModuleDescriptor", "class FeatureDescriptor", "CommandAssembly", "CommandType", "enum ModuleState", "enum FeatureState", "class DiagnosticMessage", "enum DiagnosticSeverity" })
            {
                Require(contractText.Contains(token), "missing contract token: " + token);
            }
        }

        private void ValidateContractsMultiTargetReadiness()
        {
            var contractsProject = _source.ReadText("src/PlugHub.Contracts/PlugHub.Contracts.csproj");
            var frameworkProject = _source.ReadText("src/PlugHub.Framework/PlugHub.Framework.csproj");
            var readme = _source.ReadText("README.md");

            Require(contractsProject.Contains("<TargetFrameworks>net48;netstandard2.1</TargetFrameworks>"), "PlugHub.Contracts must target net48 and netstandard2.1 for future net8 adapters.");
            Require(!_source.ReadAllCSharp("src/PlugHub.Contracts").Contains("System.Web"), "PlugHub.Contracts must stay free of net48-only System.Web dependencies.");
            Require(frameworkProject.Contains("<TargetFramework>net48</TargetFramework>") && frameworkProject.Contains("System.Web.Extensions"), "PlugHub.Framework remains net48 until its JSON serializer boundary is replaced.");
            Require(!readme.Contains("netstandard2.1") && !readme.Contains("System.Web.Script.Serialization"), "root README must not document framework development internals.");
        }

        private void ValidateModulesManifestSchemaAndCompatibility()
        {
            var schema = _source.ReadText("config/schemas/packages.schema.json");
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

            var packageValidation = _source.ReadText("src/PlugHub.StaticValidation/Validation/PackageManifestValidation.cs");
            Require(packageValidation.Contains("Packages manifest") && packageValidation.Contains("IEnumerable") && packageValidation.Contains("Cast<object>().Any()") && !packageValidation.Contains("object[]"), "packages manifest validation must accept JavaScriptSerializer ArrayList modules.");

            var models = _source.ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            Require(models.Contains("IndexVersion") && models.Contains("public string Version") && models.Contains("public string Author") && models.Contains("public string Category") && models.Contains("RevitVersions") && models.Contains("FrameworkVersionRange"), "configuration models must expose packages manifest author, version, and compatibility fields.");

            var featureDescriptor = _source.ReadText("src/PlugHub.Contracts/Features/FeatureDescriptor.cs");
            Require(featureDescriptor.Contains("ModuleName"), "feature descriptors must carry module display names so framework-owned default layouts can avoid technical panel names.");

            var sourceResolver = _source.ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var packageDefaults = _source.ReadText("src/PlugHub.Framework/Configuration/PackageManifestDefaults.cs");
            Require(sourceResolver.Contains("DefaultPackageManifestName = \"packages.json\""), "module source resolver must use packages.json as the only default module manifest.");
            Require(sourceResolver.Contains("AdjacentPackageManifestPattern = \"*.packages.json\""), "module source resolver must scan adjacent *.packages.json manifests.");
            Require(packageDefaults.Contains("ContainsExactKey") && packageDefaults.Contains("module.Enabled = true") && packageDefaults.Contains("module.Visible = true"), "package manifest defaults must treat omitted lowercase enabled/visible as enabled and visible.");
            Require(sourceResolver.Contains("PushRootCompatibilityToModules") && sourceResolver.Contains("module.RevitVersions = new List<string>(modules.RevitVersions)") && sourceResolver.Contains("module.FrameworkVersionRange = modules.FrameworkVersionRange"), "module source resolver must push root compatibility fields down to modules.");

            var configurationLoader = _source.ReadText("src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs");
            foreach (var token in new[] { "IndexVersion = modules.IndexVersion", "RevitVersions = new List<string>(modules.RevitVersions", "FrameworkVersionRange = modules.FrameworkVersionRange", "Version = module.Version", "Author = module.Author", "Category = module.Category", "RevitVersions = new List<string>(module.RevitVersions", "FrameworkVersionRange = module.FrameworkVersionRange" })
            {
                Require(configurationLoader.Contains(token), "framework configuration loader must preserve packages manifest fields: " + token);
            }
            Require(!configurationLoader.Contains("Version = modules.Version") && !configurationLoader.Contains("Sha256 = modules.Sha256") && !configurationLoader.Contains("Signature = modules.Signature"), "framework configuration loader must not preserve obsolete root package version or signature fields.");

            var discovery = _source.ReadText("src/PlugHub.Framework/Discovery/ModuleDiscoveryService.cs");
            Require(discovery.Contains("IsCompatibleWithRuntime"), "module discovery must skip packages incompatible with the active runtime.");
            Require(discovery.Contains("RT-MODULE-COMPATIBILITY") && discovery.Contains("continue;"), "module discovery must warn and skip packages incompatible with the active runtime.");
            Require(discovery.Contains("CurrentRevitVersion") && discovery.Contains(".Trim()") && discovery.Contains("StringComparer.OrdinalIgnoreCase"), "module discovery must normalize declared Revit versions before comparing with the current runtime.");
            Require(discovery.Contains("FrameworkVersionRange") && discovery.Contains("metadata"), "frameworkVersionRange must be explicitly preserved as metadata and not treated as runtime compatibility logic yet.");
            Require(discovery.Contains("ModuleName = DisplayNameResolver.Resolve(module.DisplayName, module.Name, string.Empty, module.Id)"), "module discovery must project module display names onto feature descriptors.");

            var packageInstallService = _source.ReadText("src/PlugHub.Framework/Packages/PackageInstallService.cs");
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

        private void ValidateRuntimeAcceptsWhitespacePaddedRevitVersion()
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

        private void ValidateRuntimeSkipsPresetOverriddenIncompatiblePackage()
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

        private void ValidateInstalledRepositoryPackagePreservesCompatibilityAndSkips()
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

        private void ValidateRepositoryModulesManifestVersionAndDefaults()
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

        private void ValidatePackageManifestWriterProducesCurrentSchema()
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
            var root = _json.Deserialize<Dictionary<string, object>>(text);
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

        private void ValidateRuntimeDefaultLayoutUsesModuleDisplayNames()
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

        private void ValidateRibbonLayoutUsesResolvedPackageIconWhenOverrideIsManifestRelative()
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

        private void WriteRuntimeConfig(string configDirectory, string overrideModuleId = "")
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

        private string ReadInstalledManifest(string path)
        {
            return File.ReadAllText(path).Replace("\\/", "/");
        }

        private void ValidateRibbonLayoutConfigurationModels()
        {
            var configurationModels = _source.ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            Require(configurationModels.Contains("public string LayoutVersion { get; set; }"), "RibbonConfiguration must expose LayoutVersion.");
            Require(configurationModels.Contains("public List<RibbonPanelLayoutConfiguration> Panels { get; set; }"), "RibbonConfiguration must expose Panels.");
            Require(configurationModels.Contains("public sealed class RibbonPanelLayoutConfiguration"), "Ribbon panel layout configuration must exist.");
            Require(configurationModels.Contains("public sealed class RibbonItemLayoutConfiguration"), "Ribbon item layout configuration must exist.");
            Require(configurationModels.Contains("public string Type { get; set; }"), "Ribbon item layout configuration must expose Type.");
            Require(configurationModels.Contains("public string FeatureId { get; set; }"), "Ribbon item layout configuration must expose FeatureId.");
            Require(configurationModels.Contains("public string DefaultFeatureId { get; set; }"), "Ribbon item layout configuration must expose DefaultFeatureId.");
        }

        private void ValidateRibbonLayoutComposerShape()
        {
            var composerPath = "src/PlugHub.Framework/Composition/RibbonLayoutComposer.cs";
            var viewModelPath = "src/PlugHub.Framework/Composition/RibbonLayoutViewModel.cs";
            Require(File.Exists(_source.FullPath(composerPath)), "RibbonLayoutComposer must exist.");
            Require(File.Exists(_source.FullPath(viewModelPath)), "RibbonLayoutViewModel must exist.");

            var composer = _source.ReadText(composerPath);
            var viewModel = _source.ReadText(viewModelPath);
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

        private void ValidateConfiguredRibbonLayoutAppendsUnplacedFeaturesByGroup()
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

        private void ValidateRibbonLayoutRules()
        {
            var views = _source.ReadObject("config/views.example.json");
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

        private void ValidateRibbonLayoutItems(IEnumerable items, ISet<string> featureIds, ISet<string> containerIds, string location)
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

        private bool ChildrenContainFeatureId(IEnumerable<object> children, string featureId)
        {
            return children
                .Select(child => child as Dictionary<string, object>)
                .Where(child => child != null)
                .Any(child => string.Equals(StringValue(child!, "featureId"), featureId, StringComparison.OrdinalIgnoreCase));
        }

        private void ValidateRibbonLayoutSettingsRows()
        {
            var viewModel = _source.ReadText("src/PlugHub.Manager/Settings/FrameworkSettingsViewModel.cs");
            var editorPath = "src/PlugHub.Framework/RibbonEditing/RibbonLayoutEditor.cs";
            Require(File.Exists(_source.FullPath(editorPath)), "RibbonLayoutEditor must own the pure Ribbon editing seam.");
            var editor = _source.ReadText(editorPath);
            Require(viewModel.Contains("RibbonDesignerFeatures"), "settings view model must expose visual designer features.");
            Require(viewModel.Contains("RibbonDesignerTabs"), "settings view model must expose visual designer tabs.");
            Require(editor.Contains("Load(") && editor.Contains("Synchronize(") && editor.Contains("PrepareForSave(") && editor.Contains("Validate("), "RibbonLayoutEditor must expose the complete load, edit, save, and validation lifecycle.");
            Require(!File.Exists(_source.FullPath("src/PlugHub.Manager/Settings/Rows/RibbonLayoutNodeRow.cs")), "legacy RibbonLayoutNodeRow conversion model must not return.");
        }


        private List<string> FeatureIdsForView(List<Dictionary<string, object>> features, Dictionary<string, object> view)
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

        private IEnumerable<Dictionary<string, object>> AllModules()
        {
            foreach (var module in Modules(_source.ReadObject("config/sources.example.json"))) yield return module;
            var packagesDirectory = _source.FullPath("packages");
            if (!Directory.Exists(packagesDirectory)) yield break;
            foreach (var file in Directory.GetFiles(packagesDirectory, "packages.json", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(packagesDirectory, "*.packages.json", SearchOption.AllDirectories))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var module in Modules(_json.Deserialize<Dictionary<string, object>>(File.ReadAllText(file)))) yield return module;
            }
        }

        private static string RemovedSamplesDirectory() => "modules/" + "samples";
        private static IEnumerable<Dictionary<string, object>> Modules(Dictionary<string, object> root) => ArrayValue(root, "modules").Cast<Dictionary<string, object>>();
        private static IEnumerable<Dictionary<string, object>> Repositories(Dictionary<string, object> root) => ArrayValue(root, "repositories").Cast<Dictionary<string, object>>();
        private static IEnumerable<Dictionary<string, object>> Views(Dictionary<string, object> root) => ArrayValue(root, "views").Cast<Dictionary<string, object>>();
        private static IEnumerable<Dictionary<string, object>> Presets(Dictionary<string, object> root) => ArrayValue(root, "presets").Cast<Dictionary<string, object>>();
        private static IEnumerable<Dictionary<string, object>> Features(Dictionary<string, object> module) => ArrayValue(module, "features").Cast<Dictionary<string, object>>();
        private static Dictionary<string, object> ObjectValue(Dictionary<string, object> source, string key) => source.TryGetValue(key, out var value) && value is Dictionary<string, object> result ? result : new Dictionary<string, object>();

        private static bool TryObjectValue(Dictionary<string, object> source, string key, out Dictionary<string, object> result)
        {
            if (source.TryGetValue(key, out var value) && value is Dictionary<string, object> objectValue) { result = objectValue; return true; }
            result = new Dictionary<string, object>();
            return false;
        }

        private static ArrayList ArrayValue(Dictionary<string, object> source, string key) => source.TryGetValue(key, out var value) && value is ArrayList result ? result : new ArrayList();

        private static bool TryArrayValue(Dictionary<string, object> source, string key, out ArrayList result)
        {
            if (source.TryGetValue(key, out var value) && value is ArrayList arrayValue) { result = arrayValue; return true; }
            result = new ArrayList();
            return false;
        }

        private static List<string> SequenceValue(Dictionary<string, object> source, string key) => ArrayValue(source, key).Cast<object>().Select(value => Convert.ToString(value) ?? string.Empty).ToList();
        private static string StringValue(Dictionary<string, object> source, string key) => source.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
