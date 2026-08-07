using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using PlugHub.Contracts.Features;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Composition;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;
using PlugHub.Framework.Packages;
using PlugHub.Framework.RibbonEditing;
using PlugHub.Framework.Runtime;
using PlugHub.Framework.Settings;
using PlugHub.Framework.Sources;
using PlugHub.Framework.Updates;
using PlugHub.Manager.Settings;
using PlugHub.Manager.Maintenance;

namespace PlugHub.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            var tests = new[]
            {
                new TestCase("package manifest writer omits runtime and layout fields", PackageManifestWriterOmitsRuntimeAndLayoutFields),
                new TestCase("configuration loader applies defaults and view preset overrides", ConfigurationLoaderAppliesDefaultsAndViewPresetOverrides),
                new TestCase("configuration loader tolerates malformed optional json", ConfigurationLoaderToleratesMalformedOptionalJson),
                new TestCase("package manifest reader discovers nested manifests", PackageManifestReaderDiscoversNestedManifests),
                new TestCase("module source resolver ignores git manifests", ModuleSourceResolverIgnoresGitManifests),
                new TestCase("settings configuration store ignores git manifests", SettingsConfigurationStoreIgnoresGitManifests),
                new TestCase("settings configuration store resolves package manifest base directories", SettingsConfigurationStoreResolvesPackageManifestBaseDirectories),
                new TestCase("module source resolver rejects manifest path escape", ModuleSourceResolverRejectsManifestPathEscape),
                new TestCase("settings configuration store rejects manifest path escape", SettingsConfigurationStoreRejectsManifestPathEscape),
                new TestCase("settings configuration store rejects unowned save paths", SettingsConfigurationStoreRejectsUnownedSavePaths),
                new TestCase("settings configuration store creates its missing sources manifest", SettingsConfigurationStoreCreatesItsMissingSourcesManifest),
                new TestCase("settings metrics count unique modules features and enabled repositories", SettingsMetricsCountUniqueModulesFeaturesAndEnabledRepositories),
                new TestCase("repository display name uses custom name before fallback", RepositoryDisplayNameUsesCustomNameBeforeFallback),
                new TestCase("repository display name uses owner repository url fallback", RepositoryDisplayNameUsesOwnerRepositoryUrlFallback),
                new TestCase("plugHub logger keeps only recent three days", PlugHubLoggerKeepsOnlyRecentThreeDays),
                new TestCase("framework runtime writes session log on load", FrameworkRuntimeWritesSessionLogOnLoad),
                new TestCase("package repository service browses local folder repositories", PackageRepositoryServiceBrowsesLocalFolderRepositories),
                new TestCase("repository archive sync flattens provider wrapper for deep paths", RepositoryArchiveSyncFlattensProviderWrapperForDeepPaths),
                new TestCase("repository archive sync falls back and preserves old cache on total failure", RepositoryArchiveSyncFallsBackAndPreservesOldCacheOnTotalFailure),
                new TestCase("repository archive sync redacts private tokens from diagnostics", RepositoryArchiveSyncRedactsPrivateTokensFromDiagnostics),
                new TestCase("repository archive sync rejects unsafe entries and preserves cache", RepositoryArchiveSyncRejectsUnsafeEntriesAndPreservesCache),
                new TestCase("package install service copies selected module payload only", PackageInstallServiceCopiesSelectedModulePayloadOnly),
                new TestCase("package install service rejects rooted payload paths", PackageInstallServiceRejectsRootedPayloadPaths),
                new TestCase("package repository service maintains install update uninstall state", PackageRepositoryServiceMaintainsInstallUpdateUninstallState),
                new TestCase("package repository service cancels locked update and restores manifest", PackageRepositoryServiceCancelsLockedUpdateAndRestoresManifest),
                new TestCase("package repository service applies locked delete after unlock", PackageRepositoryServiceAppliesLockedDeleteAfterUnlock),
                new TestCase("package repository service cleans duplicate module manifests", PackageRepositoryServiceCleansDuplicateModuleManifests),
                new TestCase("package repository service drops unknown and missing staging pending operations", PackageRepositoryServiceDropsUnknownAndMissingStagingPendingOperations),
                new TestCase("package repository service partially cleans duplicate multi module manifests", PackageRepositoryServicePartiallyCleansDuplicateMultiModuleManifests),
                new TestCase("package repository service cancels locked uninstall and restores manifest", PackageRepositoryServiceCancelsLockedUninstallAndRestoresManifest),
                new TestCase("package repository service rejects pending manifest backup path escape", PackageRepositoryServiceRejectsPendingManifestBackupPathEscape),
                new TestCase("package repository service rejects pending delete path escape", PackageRepositoryServiceRejectsPendingDeletePathEscape),
                new TestCase("package repository service rejects pending update staging path escape", PackageRepositoryServiceRejectsPendingUpdateStagingPathEscape),
                new TestCase("package repository service rejects cancelled update staging path escape", PackageRepositoryServiceRejectsCancelledUpdateStagingPathEscape),
                new TestCase("package repository service rejects dot segment cache ids", PackageRepositoryServiceRejectsDotSegmentCacheIds),
                new TestCase("package repository service rejects dot segment package ids", PackageRepositoryServiceRejectsDotSegmentPackageIds),
                new TestCase("repository package row treats absent Revit host as settled state", RepositoryPackageRowTreatsAbsentRevitHostAsSettledState),
                new TestCase("feature slot allocator assigns stable bounded bidirectional mappings", FeatureSlotAllocatorAssignsStableBoundedBidirectionalMappings),
                new TestCase("ribbon layout composer builds configured layout and default fallback", RibbonLayoutComposerBuildsConfiguredLayoutAndDefaultFallback),
                new TestCase("ribbon layout composer filters invalid container children", RibbonLayoutComposerFiltersInvalidContainerChildren),
                new TestCase("framework update selects only exact version asset", FrameworkUpdateSelectsOnlyExactVersionAsset),
                new TestCase("release client selects latest test prerelease", ReleaseClientSelectsLatestTestPrerelease),
                new TestCase("framework update checks GitHub test prereleases for TV builds", FrameworkUpdateChecksGitHubTestPrereleasesForTvBuilds),
                new TestCase("framework update treats test channel tags as comparable versions", FrameworkUpdateTreatsTestChannelTagsAsComparableVersions),
                new TestCase("ribbon designer mapper hydrates configured feature icons", RibbonDesignerMapperHydratesConfiguredFeatureIcons),
                new TestCase("ribbon layout editor merges panels and restores visible features", RibbonLayoutEditorMergesPanelsAndRestoresVisibleFeatures),
                new TestCase("ribbon layout editor normalizes stacks before save", RibbonLayoutEditorNormalizesStacksBeforeSave),
                new TestCase("ribbon layout editor rejects invalid layouts", RibbonLayoutEditorRejectsInvalidLayouts),
                new TestCase("framework update package accepts single manager maintenance payload", FrameworkUpdatePackageAcceptsSingleManagerMaintenancePayload),
                new TestCase("framework update package rejects missing manager maintenance payload", FrameworkUpdatePackageRejectsMissingManagerMaintenancePayload),
                new TestCase("manager updater validates maintenance payload", ManagerUpdaterValidatesMaintenancePayload),
                new TestCase("manager maintenance shares install root safety policy", ManagerMaintenanceSharesInstallRootSafetyPolicy),
                new TestCase("manager updater removes stale standalone maintenance pdbs", ManagerUpdaterRemovesStaleStandaloneMaintenancePdbs),
                new TestCase("manager updater rejects non PlugHub directory", ManagerUpdaterRejectsNonPlugHubDirectory),
                new TestCase("manager updater rejects marker validated parent directory", ManagerUpdaterRejectsMarkerValidatedParentDirectory),
                new TestCase("manager uninstaller accepts local Revit build output directory", ManagerUninstallerAcceptsLocalRevitBuildOutputDirectory),
                new TestCase("manager uninstaller rejects non PlugHub directory", ManagerUninstallerRejectsNonPlugHubDirectory),
                new TestCase("manager uninstaller rejects marker validated parent directory", ManagerUninstallerRejectsMarkerValidatedParentDirectory),
                new TestCase("manager uninstaller rejects unmarked PlugHub named directory", ManagerUninstallerRejectsUnmarkedPlugHubNamedDirectory),
                new TestCase("manager maintenance logger does not recreate deleted install directory", ManagerMaintenanceLoggerDoesNotRecreateDeletedInstallDirectory),
                new TestCase("sensitive text redactor masks repository tokens", SensitiveTextRedactorMasksRepositoryTokens)
            };

            var failures = new List<string>();
            foreach (var test in tests)
            {
                try
                {
                    test.Body();
                    Console.WriteLine("PASS " + test.Name);
                }
                catch (Exception ex)
                {
                    var failure = TestFailureMessage(ex);
                    failures.Add(test.Name + ": " + failure);
                    Console.Error.WriteLine("FAIL " + test.Name + ": " + failure);
                }
            }

            if (failures.Count == 0)
            {
                Console.WriteLine("passed: " + tests.Length);
                return 0;
            }

            Console.Error.WriteLine("failed: " + failures.Count);
            return 1;
        }

        private static string TestFailureMessage(Exception exception)
        {
            var current = exception;
            while (current is TargetInvocationException && current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current.Message;
        }

        private static void PackageManifestWriterOmitsRuntimeAndLayoutFields()
        {
            var manifest = new ModulesConfiguration
            {
                SchemaVersion = "1.0",
                IndexVersion = "1.2.3",
                PackageDirectories = new List<string> { "legacy-packages" },
                ModuleSources = new List<ModuleSourceConfiguration>
                {
                    new ModuleSourceConfiguration { Id = "legacy-source", Enabled = true }
                },
                Repositories = new List<PackageRepositoryConfiguration>
                {
                    new PackageRepositoryConfiguration { Id = "repo", Enabled = true, ApiKey = "secret" }
                },
                ConflictPolicy = new ConflictPolicyConfiguration { DuplicateModuleId = "warn" },
                Modules = new List<ModuleConfiguration>
                {
                    new ModuleConfiguration
                    {
                        Id = "module.one",
                        Version = "1.0.0",
                        Assembly = "bin/ModuleOne.dll",
                        Type = "Legacy.Type",
                        SourceId = "legacy-source",
                        ResolvedBaseDirectory = @"C:\legacy",
                        Enabled = false,
                        Visible = false,
                        Order = 99,
                        DisplayName = "Module One",
                        Category = "Review",
                        Tags = new List<string> { "model", "model" },
                        Features = new List<FeatureConfiguration>
                        {
                            new FeatureConfiguration
                            {
                                Id = "feature.one",
                                DisplayName = "Feature One",
                                CommandType = "Vendor.Command",
                                CommandAssembly = "bin/LegacyCommand.dll",
                                CommandKey = "legacy-command-key",
                                ButtonSize = "small",
                                DefaultState = "Hidden",
                                Category = "Old Category",
                                Group = "Old Group",
                                Order = 10,
                                IconPath = "icons/feature-one.png",
                                Tags = new List<string> { "legacy" }
                            }
                        }
                    }
                }
            };

            var json = new PackageManifestWriter().SerializePackageManifest(manifest);

            RequireContains(json, "\"schemaVersion\"");
            RequireContains(json, "\"indexVersion\"");
            RequireContains(json, "\"modules\"");
            RequireContains(json, "\"commandType\"");
            RequireContains(json, "\"iconPath\"");

            foreach (var forbidden in new[]
            {
                "\"packageDirectories\"",
                "\"moduleSources\"",
                "\"repositories\"",
                "\"conflictPolicy\"",
                "\"type\"",
                "\"sourceId\"",
                "\"resolvedBaseDirectory\"",
                "\"enabled\"",
                "\"visible\"",
                "\"order\"",
                "\"dependsOn\"",
                "\"category\":\"Old Category\"",
                "\"group\"",
                "\"tags\":[\"legacy\"]",
                "\"defaultState\"",
                "\"commandKey\"",
                "\"commandAssembly\"",
                "\"buttonSize\""
            })
            {
                RequireDoesNotContain(json, forbidden);
            }
        }

        private static void ConfigurationLoaderAppliesDefaultsAndViewPresetOverrides()
        {
            using (var temp = TempDirectory.Create())
            {
                var configDirectory = Path.Combine(temp.Path, "config");
                WriteText(Path.Combine(configDirectory, "sources.json"),
                    "{" +
                    "\"schemaVersion\":\"1.0\"," +
                    "\"modules\":[" +
                    "{" +
                    "\"id\":\"module.visible\",\"assembly\":\"bin/visible.dll\",\"enabled\":true,\"visible\":true,\"order\":20,\"category\":\"Model\",\"tags\":[\"module-tag\"]," +
                    "\"features\":[{\"id\":\"feature.visible\",\"displayName\":\"Visible Feature\",\"commandType\":\"Vendor.VisibleCommand\",\"tags\":[\"feature-tag\"],\"defaultState\":\"Hidden\"}]" +
                    "}," +
                    "{" +
                    "\"id\":\"module.hidden\",\"assembly\":\"bin/hidden.dll\",\"enabled\":true,\"visible\":true,\"order\":30," +
                    "\"features\":[{\"id\":\"feature.hidden\",\"displayName\":\"Hidden Feature\",\"commandType\":\"Vendor.HiddenCommand\"}]" +
                    "}" +
                    "]" +
                    "}");
                WriteText(Path.Combine(configDirectory, "views.json"),
                    "{" +
                    "\"schemaVersion\":\"1.0\"," +
                    "\"defaultView\":\"model\"," +
                    "\"views\":[{\"id\":\"model\",\"name\":\"Model View\",\"ribbon\":{\"tabName\":\"PlugHub\",\"fallbackPanelName\":\"Model Tools\"}}]" +
                    "}");
                WriteText(Path.Combine(configDirectory, "feature-combinations.json"),
                    "{" +
                    "\"schemaVersion\":\"1.0\"," +
                    "\"presets\":[{\"id\":\"model-preset\",\"viewId\":\"model\",\"moduleOverrides\":[" +
                    "{\"moduleId\":\"module.visible\",\"order\":5}," +
                    "{\"moduleId\":\"module.hidden\",\"visible\":false}" +
                    "]}]" +
                    "}");

                var loader = new FrameworkConfigurationLoader();
                var runtime = loader.LoadRuntime(configDirectory);
                var descriptors = loader.ToFeatureDescriptors(runtime).ToList();

                Require(runtime.ActiveView.Id == "model", "configured default view must be selected.");
                Require(runtime.ActivePreset != null && runtime.ActivePreset.Id == "model-preset", "view-specific preset must be selected.");
                Require(runtime.EffectiveModules.PackageDirectories.SequenceEqual(new[] { "packages" }), "missing packageDirectories must default to packages.");
                Require(runtime.EffectiveModules.Modules.First(module => module.Id == "module.visible").Order == 5, "preset must override module order.");
                Require(!runtime.EffectiveModules.Modules.First(module => module.Id == "module.hidden").Visible, "preset must hide module.");

                Require(descriptors.Count == 1, "hidden modules must not produce feature descriptors.");
                var descriptor = descriptors[0];
                Require(descriptor.Id == "feature.visible", "visible feature descriptor must be returned.");
                Require(descriptor.DefaultState == FeatureState.Hidden, "feature defaultState must be parsed.");
                Require(descriptor.CommandAssembly == "bin/visible.dll", "feature commandAssembly must fall back to module assembly.");
                Require(descriptor.Category == "Model", "feature category must fall back to module category.");
                Require(descriptor.Tags.SequenceEqual(new[] { "module-tag", "feature-tag" }), "module and feature tags must merge in order.");
            }
        }

        private static void ConfigurationLoaderToleratesMalformedOptionalJson()
        {
            using (var temp = TempDirectory.Create())
            {
                var configDirectory = Path.Combine(temp.Path, "config");
                WriteText(Path.Combine(configDirectory, "sources.json"), "{broken sources");
                WriteText(Path.Combine(configDirectory, "views.json"), "{broken views");
                WriteText(Path.Combine(configDirectory, "feature-combinations.json"), "{broken presets");

                var runtime = new FrameworkConfigurationLoader().LoadRuntime(configDirectory);

                Require(runtime.EffectiveModules.PackageDirectories.SequenceEqual(new[] { "packages" }), "malformed sources.json must fall back to default package directories.");
                Require(runtime.ActiveView.Id == "workspace", "malformed views.json must fall back to the default workspace view.");
                Require(runtime.ActivePreset == null, "malformed feature-combinations.json must fall back to no active preset.");
            }
        }

        private static void PackageManifestReaderDiscoversNestedManifests()
        {
            using (var temp = TempDirectory.Create())
            {
                var rootManifest = Path.Combine(temp.Path, "packages.json");
                var nestedManifest = Path.Combine(temp.Path, "nested", "packages.json");
                var adjacentManifest = Path.Combine(temp.Path, "adjacent", "review.packages.json");
                var gitManifest = Path.Combine(temp.Path, ".git", "objects", "packages.json");

                WriteText(rootManifest, "{}");
                WriteText(nestedManifest, "{}");
                WriteText(adjacentManifest, "{}");
                WriteText(gitManifest, "{}");

                var manifests = new PackageManifestReader().FindPackageManifests(temp.Path).ToList();

                Require(manifests.Count == 3, "expected root, nested, and adjacent manifests only.");
                Require(SamePath(manifests[0], rootManifest), "root packages.json must be returned first.");
                Require(manifests.Any(path => SamePath(path, nestedManifest)), "nested packages.json must be discovered.");
                Require(manifests.Any(path => SamePath(path, adjacentManifest)), "adjacent *.packages.json must be discovered.");
                Require(!manifests.Any(path => path.IndexOf(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0), ".git manifests must be ignored.");
            }
        }

        private static void ModuleSourceResolverIgnoresGitManifests()
        {
            using (var temp = TempDirectory.Create())
            {
                WritePackageManifest(Path.Combine(temp.Path, "packages", "live", "packages.json"), "module.live");
                WritePackageManifest(Path.Combine(temp.Path, "packages", ".git", "objects", "packages.json"), "module.git");

                var resolved = new ModuleSourceResolver().Resolve(temp.Path, new ModulesConfiguration
                {
                    PackageDirectories = new List<string> { "packages" }
                });

                var moduleIds = resolved.Modules.Modules.Select(module => module.Id).ToList();
                Require(moduleIds.SequenceEqual(new[] { "module.live" }), "module source resolver must ignore manifests inside .git directories.");
            }
        }

        private static void SettingsConfigurationStoreIgnoresGitManifests()
        {
            using (var temp = TempDirectory.Create())
            {
                var configDirectory = Path.Combine(temp.Path, "config");
                WritePackageManifest(Path.Combine(temp.Path, "packages", "live", "packages.json"), "module.live");
                WritePackageManifest(Path.Combine(temp.Path, "packages", ".git", "objects", "packages.json"), "module.git");

                var documents = new SettingsConfigurationStore(configDirectory).LoadModuleDocuments(new FrameworkConfiguration
                {
                    Modules = new ModulesConfiguration
                    {
                        PackageDirectories = new List<string> { "packages" }
                    }
                });

                var documentPaths = documents.Select(document => document.Path).ToList();
                Require(documentPaths.Count == 2, "settings store must own canonical sources.json and load only non-git package manifests.");
                Require(documentPaths.Any(path => SamePath(path, Path.Combine(configDirectory, "sources.json"))), "settings store must retain ownership of canonical sources.json.");
                Require(!documentPaths.Any(path => path.IndexOf(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0), "settings store must ignore manifests inside .git directories.");
                Require(documents.SelectMany(document => document.Modules.Modules).Select(module => module.Id).SequenceEqual(new[] { "module.live" }), "settings store must load the live manifest.");
            }
        }

        private static void SettingsConfigurationStoreResolvesPackageManifestBaseDirectories()
        {
            using (var temp = TempDirectory.Create())
            {
                var configDirectory = Path.Combine(temp.Path, "config");
                var packageDirectory = Path.Combine(temp.Path, "packages", "icon-package");
                WriteText(Path.Combine(configDirectory, "sources.json"), "{\"schemaVersion\":\"1.0\",\"packageDirectories\":[\"packages\"],\"modules\":[]}");
                WriteText(
                    Path.Combine(packageDirectory, "packages.json"),
                    "{\"schemaVersion\":\"1.1\",\"modules\":[{\"id\":\"icon-package\",\"assembly\":\"IconPackage.dll\",\"features\":[{\"id\":\"icon-package.run\",\"iconPath\":\"icons/package.png\"}]}]}");

                var configuration = new FrameworkConfiguration
                {
                    Modules = new ModulesConfiguration
                    {
                        PackageDirectories = new List<string> { "packages" }
                    }
                };

                var documents = new SettingsConfigurationStore(configDirectory).LoadModuleDocuments(configuration);
                var module = documents.SelectMany(document => document.Modules.Modules).Single(item => item.Id == "icon-package");
                Require(SamePath(module.ResolvedBaseDirectory, packageDirectory), "settings package documents must remember their manifest directory for Manager icon resolution.");
            }
        }

        private static void ModuleSourceResolverRejectsManifestPathEscape()
        {
            using (var temp = TempDirectory.Create())
            {
                var sourceDirectory = Path.Combine(temp.Path, "source");
                WritePackageManifest(Path.Combine(sourceDirectory, "packages.json"), "module.live");
                WritePackageManifest(Path.Combine(temp.Path, "outside.packages.json"), "module.escape");

                var resolved = new ModuleSourceResolver().Resolve(temp.Path, new ModulesConfiguration
                {
                    ModuleSources = new List<ModuleSourceConfiguration>
                    {
                        new ModuleSourceConfiguration
                        {
                            Id = "local",
                            Type = "localFolder",
                            Path = "source",
                            ManifestPath = "..\\outside.packages.json",
                            Enabled = true
                        }
                    }
                });

                var moduleIds = resolved.Modules.Modules.Select(module => module.Id).ToList();
                Require(moduleIds.Count == 0, "module source resolver must not load manifests outside the source directory.");
                Require(resolved.Diagnostics.Any(message => message.Code == "PH-SOURCE-MANIFEST"), "escaped manifest path must produce a manifest diagnostic.");
            }
        }

        private static void SettingsConfigurationStoreRejectsManifestPathEscape()
        {
            using (var temp = TempDirectory.Create())
            {
                var configDirectory = Path.Combine(temp.Path, "config");
                var sourceDirectory = Path.Combine(temp.Path, "source");
                WritePackageManifest(Path.Combine(sourceDirectory, "packages.json"), "module.live");
                WritePackageManifest(Path.Combine(temp.Path, "outside.packages.json"), "module.escape");

                var documents = new SettingsConfigurationStore(configDirectory).LoadModuleDocuments(new FrameworkConfiguration
                {
                    Modules = new ModulesConfiguration
                    {
                        ModuleSources = new List<ModuleSourceConfiguration>
                        {
                            new ModuleSourceConfiguration
                            {
                                Id = "local",
                                Type = "localFolder",
                                Path = "source",
                                ManifestPath = "..\\outside.packages.json",
                                Enabled = true
                            }
                        }
                    }
                });

                Require(!documents.Any(document => SamePath(document.Path, Path.Combine(temp.Path, "outside.packages.json"))), "settings store must not load escaped explicit manifests.");
                Require(!documents.Any(document => document.Modules.Modules.Any(module => module.Id == "module.escape")), "settings store must not load escaped manifest modules.");
            }
        }

        private static void SettingsMetricsCountUniqueModulesFeaturesAndEnabledRepositories()
        {
            var modules = new[]
            {
                new ModuleConfiguration
                {
                    Id = "module.same",
                    Features = new List<FeatureConfiguration>
                    {
                        new FeatureConfiguration { Id = "feature.same" }
                    }
                },
                new ModuleConfiguration
                {
                    Id = "module.same",
                    Features = new List<FeatureConfiguration>
                    {
                        new FeatureConfiguration { Id = "feature.same" },
                        new FeatureConfiguration { Id = "feature.other" }
                    }
                },
                new ModuleConfiguration
                {
                    Id = "module.other",
                    Features = new List<FeatureConfiguration>
                    {
                        new FeatureConfiguration { Id = "feature.same" }
                    }
                }
            };
            var repositories = new[]
            {
                new PackageRepositoryConfiguration { Id = "enabled-a", Enabled = true },
                new PackageRepositoryConfiguration { Id = "disabled", Enabled = false },
                new PackageRepositoryConfiguration { Id = "enabled-b", Enabled = true }
            };

            Require(SettingsMetrics.CountUniqueModules(modules) == 2, "settings metrics must count duplicate module ids only once.");
            Require(SettingsMetrics.CountUniqueFeatures(modules) == 2, "settings metrics must count duplicate feature ids only once.");
            Require(SettingsMetrics.CountEnabledRepositories(repositories) == 2, "settings metrics must count only enabled repositories.");
        }

        private static void RepositoryDisplayNameUsesCustomNameBeforeFallback()
        {
            Require(SettingsMetrics.RepositoryDisplayName(new PackageRepositoryConfiguration
            {
                Id = "repo-id",
                DisplayName = "公司插件仓库",
                Repository = "https://gitee.com/company/packages"
            }) == "公司插件仓库", "custom repository display name must win over id and url.");

            Require(SettingsMetrics.RepositoryDisplayName(new PackageRepositoryConfiguration
            {
                Id = "repo-id",
                Repository = "https://gitee.com/company/packages"
            }) == "company/packages", "repository slug must be the first fallback display name.");

            Require(SettingsMetrics.RepositoryDisplayName(new PackageRepositoryConfiguration
            {
                Repository = "https://gitee.com/company/packages"
            }) == "company/packages", "repository slug must be used when no custom name or id exists.");
        }

        private static void RepositoryDisplayNameUsesOwnerRepositoryUrlFallback()
        {
            Require(SettingsMetrics.RepositoryDisplayName(new PackageRepositoryConfiguration
            {
                Provider = "gitee",
                Repository = "https://gitee.com/GaoMengGu/PlugHub_Packages.git"
            }) == "GaoMengGu/PlugHub_Packages", "gitee url fallback must use owner/repository without provider branding.");

            Require(SettingsMetrics.RepositoryDisplayName(new PackageRepositoryConfiguration
            {
                Provider = "github",
                Repository = "https://github.com/GaoMengGu/PlugHub_Packages"
            }) == "GaoMengGu/PlugHub_Packages", "github url fallback must use owner/repository without provider branding.");

            Require(SettingsMetrics.RepositoryDisplayName(new PackageRepositoryConfiguration
            {
                Provider = "local",
                Repository = "/home/yilan/plughub/local-packages"
            }) == "local-packages", "local folder fallback must use the folder name, not a path fragment pair.");
        }

        private static void PackageRepositoryServiceBrowsesLocalFolderRepositories()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "install");
                var repositoryDirectory = Path.Combine(temp.Path, "local-repository");
                WriteRepositoryPackage(repositoryDirectory, "1.2.3", "local module");

                var packages = new PackageRepositoryService().Browse(
                    baseDirectory,
                    new PackageRepositoryConfiguration
                    {
                        Id = "local-source",
                        Enabled = true,
                        Provider = "local",
                        Repository = repositoryDirectory,
                        ManifestPath = "packages.json"
                    },
                    out var diagnostics);

                Require(packages.Count == 1, "local folder repository must expose one package.");
                Require(packages[0].RepositoryId == "local-source", "local package must keep repository id.");
                Require(packages[0].PackageId == "module.repo", "local package id must be read from packages.json.");
                Require(diagnostics.Count == 0, "valid local repository must not emit diagnostics.");
            }
        }

        private static void PlugHubLoggerKeepsOnlyRecentThreeDays()
        {
            using (var temp = TempDirectory.Create())
            {
                var logsDirectory = PlugHubLogger.LogsDirectory(temp.Path);
                Directory.CreateDirectory(logsDirectory);
                var today = DateTime.UtcNow.Date;
                var staleLog = Path.Combine(logsDirectory, "plughub-" + today.AddDays(-4).ToString("yyyyMMdd") + ".log");
                var recentLog = Path.Combine(logsDirectory, "plughub-" + today.AddDays(-2).ToString("yyyyMMdd") + ".log");
                WriteText(staleLog, "old");
                WriteText(recentLog, "recent");

                new PlugHubLogger().Write(temp.Path, new PlugHubLogEntry
                {
                    Severity = DiagnosticSeverity.Info,
                    Code = "TEST-LOG",
                    Operation = "Test",
                    Message = "write"
                });

                RequireFileMissing(staleLog);
                RequireFileExists(recentLog);
                RequireFileExists(Path.Combine(logsDirectory, "plughub-" + today.ToString("yyyyMMdd") + ".log"));
            }
        }

        private static void FrameworkRuntimeWritesSessionLogOnLoad()
        {
            using (var temp = TempDirectory.Create())
            {
                var configDirectory = Path.Combine(temp.Path, "config");
                WriteText(Path.Combine(configDirectory, "sources.json"), "{\"schemaVersion\":\"1.0\",\"packageDirectories\":[\"packages\"],\"modules\":[]}");
                WriteText(Path.Combine(configDirectory, "views.json"), "{\"schemaVersion\":\"1.0\",\"defaultView\":\"workspace\",\"views\":[{\"id\":\"workspace\",\"name\":\"Workspace\"}]}");
                WriteText(Path.Combine(configDirectory, "feature-combinations.json"), "{\"schemaVersion\":\"1.0\",\"presets\":[]}");

                new FrameworkRuntime().Load(temp.Path, configDirectory, false);

                var logPath = Path.Combine(PlugHubLogger.LogsDirectory(temp.Path), "plughub-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
                RequireFileExists(logPath);
                var log = File.ReadAllText(logPath);
                RequireContains(log, "RT-LOAD");
                RequireContains(log, "FrameworkRuntime.Load");
            }
        }

        private static void PackageInstallServiceCopiesSelectedModulePayloadOnly()
        {
            using (var temp = TempDirectory.Create())
            {
                var repositoryDirectory = Path.Combine(temp.Path, "repository");
                var manifestPath = Path.Combine(repositoryDirectory, "packages.json");
                var stagingDirectory = Path.Combine(temp.Path, "staging");

                WriteText(Path.Combine(repositoryDirectory, "bin", "A.dll"), "module a");
                WriteText(Path.Combine(repositoryDirectory, "bin", "B.dll"), "module b");
                WriteText(Path.Combine(repositoryDirectory, "icons", "A.png"), "icon a");
                WriteText(Path.Combine(repositoryDirectory, "docs", "old.txt"), "old documentation");

                new PackageManifestWriter().WritePackageManifest(manifestPath, new ModulesConfiguration
                {
                    SchemaVersion = "1.0",
                    IndexVersion = "2.0.0",
                    Modules = new List<ModuleConfiguration>
                    {
                        new ModuleConfiguration
                        {
                            Id = "module.a",
                            Version = "1.0.0",
                            Assembly = "bin/A.dll",
                            Features = new List<FeatureConfiguration>
                            {
                                new FeatureConfiguration
                                {
                                    Id = "feature.a",
                                    DisplayName = "Feature A",
                                    CommandType = "Vendor.ModuleA.Command",
                                    IconPath = "icons/A.png"
                                }
                            }
                        },
                        new ModuleConfiguration
                        {
                            Id = "module.b",
                            Version = "1.0.0",
                            Assembly = "bin/B.dll",
                            Features = new List<FeatureConfiguration>
                            {
                                new FeatureConfiguration
                                {
                                    Id = "feature.b",
                                    DisplayName = "Feature B",
                                    CommandType = "Vendor.ModuleB.Command"
                                }
                            }
                        }
                    }
                });

                var package = new RepositoryPackageDescriptor
                {
                    RepositoryId = "local",
                    PackageId = "module.a",
                    ModuleId = "module.a",
                    ManifestPath = manifestPath,
                    SourceDirectory = repositoryDirectory
                };

                var result = new PackageInstallService(new PackageManifestReader()).InstallPackagePayload(package, stagingDirectory);

                Require(result.Success, result.Message);
                RequireFileExists(Path.Combine(stagingDirectory, "packages.json"));
                RequireFileExists(Path.Combine(stagingDirectory, "bin", "A.dll"));
                RequireFileExists(Path.Combine(stagingDirectory, "icons", "A.png"));
                RequireFileMissing(Path.Combine(stagingDirectory, "bin", "B.dll"));
                RequireFileMissing(Path.Combine(stagingDirectory, "docs", "old.txt"));

                var installedManifest = File.ReadAllText(Path.Combine(stagingDirectory, "packages.json"));
                RequireContains(installedManifest, "module.a");
                RequireDoesNotContain(installedManifest, "module.b");
                RequireDoesNotContain(installedManifest, "\"indexVersion\"");
            }
        }

        private static void PackageInstallServiceRejectsRootedPayloadPaths()
        {
            using (var temp = TempDirectory.Create())
            {
                var repositoryDirectory = Path.Combine(temp.Path, "repository");
                var manifestPath = Path.Combine(repositoryDirectory, "packages.json");
                var stagingDirectory = Path.Combine(temp.Path, "staging");
                var externalAssembly = Path.Combine(temp.Path, "outside", "ExternalCommand.dll");
                WriteText(externalAssembly, "external");
                Directory.CreateDirectory(repositoryDirectory);

                new PackageManifestWriter().WritePackageManifest(manifestPath, new ModulesConfiguration
                {
                    SchemaVersion = "1.0",
                    Modules = new List<ModuleConfiguration>
                    {
                        new ModuleConfiguration
                        {
                            Id = "module.rooted",
                            Version = "1.0.0",
                            Assembly = externalAssembly,
                            Features = new List<FeatureConfiguration>
                            {
                                new FeatureConfiguration
                                {
                                    Id = "feature.rooted",
                                    DisplayName = "Rooted Feature",
                                    CommandType = "Vendor.RootedCommand"
                                }
                            }
                        }
                    }
                });

                var package = PackageDescriptor(manifestPath, repositoryDirectory, "module.rooted");
                var result = new PackageInstallService(new PackageManifestReader()).InstallPackagePayload(package, stagingDirectory);

                Require(!result.Success, "repository package install must reject rooted payload paths.");
                Require(result.Message.Contains("outside the repository package directory"), "rooted payload rejection should explain the path boundary.");
                RequireFileMissing(Path.Combine(stagingDirectory, "packages.json"));
            }
        }

        private static void PackageRepositoryServiceMaintainsInstallUpdateUninstallState()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var repositoryDirectory = Path.Combine(temp.Path, "repository");
                var manifestPath = WriteRepositoryPackage(repositoryDirectory, "1.0.0", "module v1");
                var package = PackageDescriptor(manifestPath, repositoryDirectory, "module.repo");
                var service = new PackageRepositoryService();

                var install = service.Install(baseDirectory, package);
                Require(install.Success, install.Message);
                RequireFileExists(Path.Combine(baseDirectory, "packages", "module.repo", "bin", "Module.dll"));
                Require(File.ReadAllText(Path.Combine(baseDirectory, "packages", "module.repo", "bin", "Module.dll")) == "module v1", "installed payload must be copied.");
                Require(service.IsInstalled(baseDirectory, package, out var installedVersion), "installed package must be discoverable.");
                Require(installedVersion == "1.0.0", "installed version must come from installed manifest.");
                Require(service.ListPendingOperations(baseDirectory).Count == 0, "install without locks must not persist a restart-only pending operation.");

                manifestPath = WriteRepositoryPackage(repositoryDirectory, "2.0.0", "module v2");
                package = PackageDescriptor(manifestPath, repositoryDirectory, "module.repo");
                var update = service.Update(baseDirectory, package);
                Require(update.Success, update.Message);
                Require(File.ReadAllText(Path.Combine(baseDirectory, "packages", "module.repo", "bin", "Module.dll")) == "module v2", "unlocked update must replace payload.");
                Require(service.IsInstalled(baseDirectory, package, out installedVersion), "updated package must remain discoverable.");
                Require(installedVersion == "2.0.0", "updated version must come from installed manifest.");
                Require(service.ListPendingOperations(baseDirectory).Count == 0, "unlocked update must clear stale restart operations.");

                var uninstall = service.Uninstall(baseDirectory, package);
                Require(uninstall.Success, uninstall.Message);
                RequireFileMissing(Path.Combine(baseDirectory, "packages", "module.repo", "bin", "Module.dll"));
                Require(!service.IsInstalled(baseDirectory, package, out installedVersion), "uninstalled package must not be discoverable.");
                Require(service.ListPendingOperations(baseDirectory).Count == 0, "uninstall without locks must leave no pending operation.");
            }
        }

        private static void PackageRepositoryServiceCancelsLockedUpdateAndRestoresManifest()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var repositoryDirectory = Path.Combine(temp.Path, "repository");
                var manifestPath = WriteRepositoryPackage(repositoryDirectory, "1.0.0", "module v1");
                var package = PackageDescriptor(manifestPath, repositoryDirectory, "module.repo");
                var service = new PackageRepositoryService();

                var install = service.Install(baseDirectory, package);
                Require(install.Success, install.Message);

                var installedDll = Path.Combine(baseDirectory, "packages", "module.repo", "bin", "Module.dll");
                using (File.Open(installedDll, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    manifestPath = WriteRepositoryPackage(repositoryDirectory, "2.0.0", "module v2");
                    package = PackageDescriptor(manifestPath, repositoryDirectory, "module.repo");

                    var update = service.Update(baseDirectory, package);
                    Require(update.Success, update.Message);

                    var pending = service.ListPendingOperations(baseDirectory);
                    Require(pending.Count == 1 && pending[0].Operation == "update", "locked update must queue an update operation.");
                    Require(Directory.Exists(pending[0].StagingDirectory), "locked update staging directory must be retained.");
                    Require(pending[0].ManifestBackups.Count > 0, "locked update must keep manifest backups for cancellation.");
                    Require(!service.IsInstalled(baseDirectory, package, out var hiddenVersion), "locked update must remove the old manifest declaration before restart.");
                    Require(hiddenVersion == string.Empty, "hidden locked update version must be empty.");

                    var cancel = service.CancelPendingOperation(baseDirectory, "module.repo", "module.repo");
                    Require(cancel.Success, cancel.Message);
                    Require(service.ListPendingOperations(baseDirectory).Count == 0, "cancelled locked update must remove pending operations.");
                    Require(!Directory.Exists(pending[0].StagingDirectory), "cancelled locked update must delete staging directory.");
                    Require(service.IsInstalled(baseDirectory, package, out var restoredVersion), "cancelled locked update must restore installed manifest.");
                    Require(restoredVersion == "1.0.0", "cancelled locked update must restore the original installed version.");
                }
            }
        }

        private static void PackageRepositoryServiceAppliesLockedDeleteAfterUnlock()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var repositoryDirectory = Path.Combine(temp.Path, "repository");
                var manifestPath = WriteRepositoryPackage(repositoryDirectory, "1.0.0", "module v1");
                var package = PackageDescriptor(manifestPath, repositoryDirectory, "module.repo");
                var service = new PackageRepositoryService();

                var install = service.Install(baseDirectory, package);
                Require(install.Success, install.Message);

                var installedDll = Path.Combine(baseDirectory, "packages", "module.repo", "bin", "Module.dll");
                using (File.Open(installedDll, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var uninstall = service.Uninstall(baseDirectory, package);
                    Require(uninstall.Success, uninstall.Message);
                    var pending = service.ListPendingOperations(baseDirectory);
                    Require(pending.Count == 1 && pending[0].Operation == "delete", "locked uninstall must queue a delete operation.");
                    Require(!service.IsInstalled(baseDirectory, package, out var hiddenVersion), "locked uninstall must remove the manifest declaration immediately.");
                    Require(hiddenVersion == string.Empty, "hidden locked uninstall version must be empty.");
                    Require(Directory.Exists(Path.Combine(baseDirectory, "packages", "module.repo")), "locked uninstall must keep the directory until unlock.");
                }

                var diagnostics = service.ApplyPendingOperations(baseDirectory);
                Require(diagnostics.Count == 0, "unlocked pending delete must apply without diagnostics.");
                Require(service.ListPendingOperations(baseDirectory).Count == 0, "applied pending delete must clear the queue.");
                Require(!Directory.Exists(Path.Combine(baseDirectory, "packages", "module.repo")), "applied pending delete must remove the install directory.");
            }
        }

        private static void PackageRepositoryServiceCleansDuplicateModuleManifests()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var duplicateDirectory = Path.Combine(baseDirectory, "packages", "old.package");
                WriteText(Path.Combine(duplicateDirectory, "bin", "Old.dll"), "old module");
                new PackageManifestWriter().WritePackageManifest(Path.Combine(duplicateDirectory, "packages.json"), new ModulesConfiguration
                {
                    SchemaVersion = "1.0",
                    Modules = new List<ModuleConfiguration>
                    {
                        new ModuleConfiguration
                        {
                            Id = "module.repo",
                            Version = "0.9.0",
                            Assembly = "bin/Old.dll"
                        }
                    }
                });

                var repositoryDirectory = Path.Combine(temp.Path, "repository");
                var manifestPath = WriteRepositoryPackage(repositoryDirectory, "1.0.0", "module v1");
                var package = PackageDescriptor(manifestPath, repositoryDirectory, "module.repo");
                var service = new PackageRepositoryService();

                var install = service.Install(baseDirectory, package);
                Require(install.Success, install.Message);
                RequireFileExists(Path.Combine(baseDirectory, "packages", "module.repo", "packages.json"));
                Require(!Directory.Exists(duplicateDirectory), "install must remove old duplicate module package directories when they become empty.");
                Require(service.IsInstalled(baseDirectory, package, out var installedVersion), "new package must be installed after duplicate cleanup.");
                Require(installedVersion == "1.0.0", "new package version must win after duplicate cleanup.");
            }
        }

        private static void PackageRepositoryServiceDropsUnknownAndMissingStagingPendingOperations()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var store = new PendingPackageOperationStore();
                store.Write(baseDirectory, new List<PendingPackageOperation>
                {
                    new PendingPackageOperation
                    {
                        Operation = "mystery",
                        PackageId = "module.unknown",
                        ModuleId = "module.unknown",
                        InstallDirectory = Path.Combine(baseDirectory, "packages", "module.unknown")
                    },
                    PendingPackageOperation.Update(
                        "module.missing-staging",
                        "module.missing-staging",
                        Path.Combine(baseDirectory, "packages", "module.missing-staging"),
                        Path.Combine(baseDirectory, "repository-cache", ".package-install", "missing-staging"))
                });

                var diagnostics = new PackageRepositoryService().ApplyPendingOperations(baseDirectory);

                Require(diagnostics.Count == 1, "only unknown pending operations should produce diagnostics.");
                Require(diagnostics[0].Code == "PH-PACKAGE-PENDING-UNKNOWN", "unknown pending operation diagnostic code must be stable.");
                Require(store.Read(baseDirectory).Count == 0, "unknown and missing-staging pending operations must be removed after apply.");
            }
        }

        private static void PackageRepositoryServicePartiallyCleansDuplicateMultiModuleManifests()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var duplicateDirectory = Path.Combine(baseDirectory, "packages", "old.bundle");
                WriteText(Path.Combine(duplicateDirectory, "bin", "Old.dll"), "old module");
                WriteText(Path.Combine(duplicateDirectory, "bin", "Other.dll"), "other module");
                new PackageManifestWriter().WritePackageManifest(Path.Combine(duplicateDirectory, "packages.json"), new ModulesConfiguration
                {
                    SchemaVersion = "1.0",
                    Modules = new List<ModuleConfiguration>
                    {
                        new ModuleConfiguration { Id = "module.repo", Version = "0.9.0", Assembly = "bin/Old.dll" },
                        new ModuleConfiguration { Id = "module.other", Version = "1.0.0", Assembly = "bin/Other.dll" }
                    }
                });

                var repositoryDirectory = Path.Combine(temp.Path, "repository");
                var manifestPath = WriteRepositoryPackage(repositoryDirectory, "1.0.0", "module v1");
                var service = new PackageRepositoryService();
                var install = service.Install(baseDirectory, PackageDescriptor(manifestPath, repositoryDirectory, "module.repo"));

                Require(install.Success, install.Message);
                Require(Directory.Exists(duplicateDirectory), "duplicate bundle must stay when it still contains other modules.");
                var oldManifest = File.ReadAllText(Path.Combine(duplicateDirectory, "packages.json"));
                RequireDoesNotContain(oldManifest, "module.repo");
                RequireContains(oldManifest, "module.other");
                RequireFileExists(Path.Combine(duplicateDirectory, "bin", "Other.dll"));
            }
        }

        private static void PackageRepositoryServiceCancelsLockedUninstallAndRestoresManifest()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var repositoryDirectory = Path.Combine(temp.Path, "repository");
                var manifestPath = WriteRepositoryPackage(repositoryDirectory, "1.0.0", "module v1");
                var package = PackageDescriptor(manifestPath, repositoryDirectory, "module.repo");
                var service = new PackageRepositoryService();

                var install = service.Install(baseDirectory, package);
                Require(install.Success, install.Message);

                var installedDll = Path.Combine(baseDirectory, "packages", "module.repo", "bin", "Module.dll");
                using (File.Open(installedDll, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var uninstall = service.Uninstall(baseDirectory, package);
                    Require(uninstall.Success, uninstall.Message);
                    Require(!service.IsInstalled(baseDirectory, package, out var hiddenVersion), "locked uninstall must hide the module before restart.");
                    Require(hiddenVersion == string.Empty, "hidden locked uninstall version must be empty.");

                    var cancel = service.CancelPendingOperation(baseDirectory, "module.repo", "module.repo");
                    Require(cancel.Success, cancel.Message);
                    Require(service.ListPendingOperations(baseDirectory).Count == 0, "cancelled locked uninstall must remove pending operations.");
                    Require(service.IsInstalled(baseDirectory, package, out var restoredVersion), "cancelled locked uninstall must restore the installed manifest.");
                    Require(restoredVersion == "1.0.0", "cancelled locked uninstall must restore the original version.");
                    RequireFileExists(installedDll);
                }
            }
        }

        private static void PackageRepositoryServiceRejectsPendingManifestBackupPathEscape()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var installDirectory = Path.Combine(baseDirectory, "packages", "module.repo");
                var stagingDirectory = Path.Combine(baseDirectory, "repository-cache", ".package-install", "staging");
                WriteText(Path.Combine(stagingDirectory, "packages.json"), "{}");

                var operation = PendingPackageOperation.Update("module.repo", "module.repo", installDirectory, stagingDirectory);
                operation.ManifestBackups.Add(new PendingManifestBackup
                {
                    ManifestPath = Path.Combine(temp.Path, "outside-packages.json"),
                    Content = "{}"
                });

                var store = new PendingPackageOperationStore();
                store.Write(baseDirectory, new[] { operation });

                var cancel = new PackageRepositoryService().CancelPendingOperation(baseDirectory, "module.repo", "module.repo");

                Require(!cancel.Success, "cancel must reject manifest backup paths outside packages.");
                Require(store.Read(baseDirectory).Count == 1, "rejected cancel must leave the pending operation for retry or manual repair.");
                Require(Directory.Exists(stagingDirectory), "rejected cancel must not delete staging before manifest backups are restored.");
            }
        }

        private static void PackageRepositoryServiceRejectsPendingDeletePathEscape()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var outsideDirectory = Path.Combine(temp.Path, "outside-delete");
                WriteText(Path.Combine(outsideDirectory, "keep.txt"), "keep");

                var store = new PendingPackageOperationStore();
                store.Write(baseDirectory, new[]
                {
                    PendingPackageOperation.Delete("module.escape", "module.escape", outsideDirectory)
                });

                var diagnostics = new PackageRepositoryService().ApplyPendingOperations(baseDirectory);

                Require(Directory.Exists(outsideDirectory), "pending delete must not remove directories outside packages.");
                Require(diagnostics.Any(diagnostic => diagnostic.Code == "PH-PACKAGE-PENDING-DELETE"), "rejected pending delete must report a stable diagnostic.");
                Require(store.Read(baseDirectory).Count == 1, "rejected pending delete must remain queued for manual repair.");
            }
        }

        private static void PackageRepositoryServiceRejectsPendingUpdateStagingPathEscape()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var installDirectory = Path.Combine(baseDirectory, "packages", "module.escape");
                var outsideStagingDirectory = Path.Combine(temp.Path, "outside-staging");
                WriteText(Path.Combine(outsideStagingDirectory, "packages.json"), "{}");

                var store = new PendingPackageOperationStore();
                store.Write(baseDirectory, new[]
                {
                    PendingPackageOperation.Update("module.escape", "module.escape", installDirectory, outsideStagingDirectory)
                });

                var diagnostics = new PackageRepositoryService().ApplyPendingOperations(baseDirectory);

                Require(Directory.Exists(outsideStagingDirectory), "pending update must not move staging directories outside the package-install cache.");
                Require(!Directory.Exists(installDirectory), "pending update must not install from an escaped staging directory.");
                Require(diagnostics.Any(diagnostic => diagnostic.Code == "PH-PACKAGE-PENDING-UPDATE"), "rejected pending update must report a stable diagnostic.");
                Require(store.Read(baseDirectory).Count == 1, "rejected pending update must remain queued for manual repair.");
            }
        }

        private static void PackageRepositoryServiceRejectsCancelledUpdateStagingPathEscape()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var installDirectory = Path.Combine(baseDirectory, "packages", "module.escape");
                var outsideStagingDirectory = Path.Combine(temp.Path, "outside-cancel-staging");
                WriteText(Path.Combine(outsideStagingDirectory, "packages.json"), "{}");

                var store = new PendingPackageOperationStore();
                store.Write(baseDirectory, new[]
                {
                    PendingPackageOperation.Update("module.escape", "module.escape", installDirectory, outsideStagingDirectory)
                });

                var cancel = new PackageRepositoryService().CancelPendingOperation(baseDirectory, "module.escape", "module.escape");

                Require(!cancel.Success, "cancel must reject escaped update staging directories.");
                Require(Directory.Exists(outsideStagingDirectory), "cancel must not delete escaped staging directories.");
                Require(store.Read(baseDirectory).Count == 1, "rejected cancel must leave the pending update for manual repair.");
            }
        }

        private static void PackageRepositoryServiceRejectsDotSegmentCacheIds()
        {
            using (var temp = TempDirectory.Create())
            {
                var repository = new PackageRepositoryConfiguration
                {
                    Id = "..",
                    Provider = "github",
                    Repository = "owner/repository",
                    Enabled = true
                };

                var service = new PackageRepositoryService();
                Require(!service.HasRepositoryCache(temp.Path, repository), "dot-only repository ids must not resolve to the PlugHub base directory.");
            }
        }

        private static void PackageRepositoryServiceRejectsDotSegmentPackageIds()
        {
            using (var temp = TempDirectory.Create())
            {
                var baseDirectory = Path.Combine(temp.Path, "plughub");
                var repositoryDirectory = Path.Combine(temp.Path, "repository");
                var manifestPath = Path.Combine(repositoryDirectory, "packages.json");
                var expectedInstallDirectory = Path.Combine(baseDirectory, "packages", "package");
                WritePackageManifest(manifestPath, "..");

                var packages = new PackageManifestReader().ReadPackagesFromManifest(
                    manifestPath,
                    "repo",
                    baseDirectory,
                    (root, installDirectory, moduleId) => string.Empty,
                    (root, installDirectory, moduleId) => false,
                    (root, packageId, moduleId) => string.Empty);

                Require(packages.Count == 1, "dot segment package manifest must still be readable for diagnostics and user action.");
                Require(SamePath(packages[0].InstallDirectory, expectedInstallDirectory), "dot-only package ids must not resolve to the PlugHub base directory when browsing.");

                var service = new PackageRepositoryService();
                var package = PackageDescriptor(manifestPath, repositoryDirectory, "..");
                service.RefreshInstallState(baseDirectory, package);
                Require(SamePath(package.InstallDirectory, expectedInstallDirectory), "dot-only package ids must not resolve to the PlugHub base directory when refreshing install state.");
            }
        }

        private static void RepositoryPackageRowTreatsAbsentRevitHostAsSettledState()
        {
            Require(RepositoryPackageInstallState.Resolve(false, string.Empty, string.Empty, string.Empty, false, true) == "未安装",
                "absent Revit host must not turn a missing package into pending restart.");
            Require(RepositoryPackageInstallState.Resolve(true, "1.0.0", "1.0.0", string.Empty, false, false) == "已安装",
                "absent Revit host must not mark an installed package as pending restart.");
            Require(RepositoryPackageInstallState.Resolve(false, string.Empty, string.Empty, string.Empty, true, true) == "待重启卸载",
                "running Revit host must still show pending uninstall when the module remains loaded.");
            Require(RepositoryPackageInstallState.Resolve(true, "1.0.0", "1.0.0", string.Empty, true, false) == "已安装待重启",
                "running Revit host must still show pending restart when an installed package is not loaded yet.");
        }

        private static void SettingsConfigurationStoreRejectsUnownedSavePaths()
        {
            using (var temp = TempDirectory.Create())
            {
                var configDirectory = Path.Combine(temp.Path, "config");
                var outsideManifest = Path.Combine(temp.Path, "outside.packages.json");
                WriteText(outsideManifest, "original");
                var store = new SettingsConfigurationStore(configDirectory);
                var forgedDocument = new SettingsConfigurationStore.ModuleManifestDocument(
                    outsideManifest,
                    new ModulesConfiguration { SchemaVersion = "1.1" });
                var rejected = false;

                try
                {
                    store.Save(new FrameworkConfiguration(), new[] { forgedDocument });
                }
                catch (InvalidOperationException ex)
                {
                    rejected = ex.Message.IndexOf("not loaded", StringComparison.OrdinalIgnoreCase) >= 0;
                }

                Require(rejected, "settings store must reject manifest documents that were not loaded by the same store instance.");
                Require(File.ReadAllText(outsideManifest) == "original", "rejected save paths must not overwrite external files.");
            }
        }

        private static void SettingsConfigurationStoreCreatesItsMissingSourcesManifest()
        {
            using (var temp = TempDirectory.Create())
            {
                var configDirectory = Path.Combine(temp.Path, "config");
                var configuration = new FrameworkConfiguration();
                var store = new SettingsConfigurationStore(configDirectory);

                var documents = store.LoadModuleDocuments(configuration);
                store.Save(configuration, documents);

                Require(documents.Any(document => SamePath(document.Path, Path.Combine(configDirectory, "sources.json"))),
                    "the store must own its canonical sources.json document even before the file exists.");
                RequireFileExists(Path.Combine(configDirectory, "sources.json"));
            }
        }

        private static void RepositoryArchiveSyncFlattensProviderWrapperForDeepPaths()
        {
            using (var temp = TempDirectory.Create())
            {
                var cacheDirectory = Path.Combine(temp.Path, "repository-cache", "plughub-public-packages");
                var deepRelativePath = Path.Combine(
                    "tests",
                    "PlugHub.HubeiReportParameters.SchedulePlanningValidation",
                    "PlugHub.HubeiReportParameters.SchedulePlanningValidation.csproj");
                var transport = new ArchiveFixtureTransport(
                    "GaoMengGu-PlugHub_Packages-419ec18",
                    deepRelativePath);
                var synchronizer = new RepositoryArchiveSynchronizer(new RepositoryCredentialService(), transport);
                var diagnostics = new List<DiagnosticMessage>();

                var synced = synchronizer.Sync(
                    new PackageRepositoryConfiguration
                    {
                        Id = "plughub-public-packages",
                        Provider = "github",
                        Visibility = "public",
                        Repository = "GaoMengGu/PlugHub_Packages",
                        Ref = "main",
                        Enabled = true
                    },
                    cacheDirectory,
                    diagnostics);

                Require(synced, "GitHub archive fixture must synchronize successfully: " + string.Join("; ", diagnostics.Select(item => item.Message)));
                RequireFileExists(Path.Combine(cacheDirectory, "packages.json"));
                RequireFileExists(Path.Combine(cacheDirectory, deepRelativePath));
                Require(!Directory.Exists(Path.Combine(cacheDirectory, transport.WrapperDirectory)), "provider archive wrapper must not remain in the repository cache.");
                Require(transport.DownloadedUris.Count == 1 && transport.DownloadedUris[0].Host == "api.github.com", "successful GitHub archive sync must not fall back to Gitee.");
            }
        }

        private static void RepositoryArchiveSyncFallsBackAndPreservesOldCacheOnTotalFailure()
        {
            using (var temp = TempDirectory.Create())
            {
                var repository = new PackageRepositoryConfiguration
                {
                    Id = "plughub-public-packages",
                    Provider = "github",
                    Visibility = "public",
                    Repository = "GaoMengGu/PlugHub_Packages",
                    Ref = "main",
                    Enabled = true
                };
                var cacheDirectory = Path.Combine(temp.Path, "repository-cache", repository.Id);
                var fallbackTransport = new ArchiveFixtureTransport("GaoMengGu-PlugHub_Packages-main", "packages.json");
                fallbackTransport.FailHosts.Add("api.github.com");
                var diagnostics = new List<DiagnosticMessage>();

                var synced = new RepositoryArchiveSynchronizer(new RepositoryCredentialService(), fallbackTransport)
                    .Sync(repository, cacheDirectory, diagnostics);

                Require(synced, "Gitee mirror must be used when GitHub archive download fails.");
                Require(fallbackTransport.DownloadedUris.Select(uri => uri.Host).SequenceEqual(new[] { "api.github.com", "gitee.com" }),
                    "public repository sync must try configured GitHub first and then the Gitee mirror.");
                RequireFileExists(Path.Combine(cacheDirectory, "packages.json"));

                WriteText(Path.Combine(cacheDirectory, "old-cache.marker"), "keep");
                var failingTransport = new ArchiveFixtureTransport("unused", "packages.json") { FailAllDownloads = true };
                diagnostics.Clear();

                synced = new RepositoryArchiveSynchronizer(new RepositoryCredentialService(), failingTransport)
                    .Sync(repository, cacheDirectory, diagnostics);

                Require(!synced, "repository sync must fail when configured source and mirror both fail.");
                RequireFileExists(Path.Combine(cacheDirectory, "old-cache.marker"));
                Require(diagnostics.Any(item => item.Code == "PH-REPOSITORY-ARCHIVE"), "total remote failure must emit PH-REPOSITORY-ARCHIVE.");
            }
        }

        private static void RepositoryArchiveSyncRedactsPrivateTokensFromDiagnostics()
        {
            using (var temp = TempDirectory.Create())
            {
                const string token = "private-secret-token";
                var repository = new PackageRepositoryConfiguration
                {
                    Id = "private-packages",
                    Provider = "gitee",
                    Visibility = "private",
                    Repository = "GaoMengGu/PlugHub_Packages",
                    Ref = "main",
                    ApiKey = token,
                    Enabled = true
                };
                var cacheDirectory = Path.Combine(temp.Path, "repository-cache", repository.Id);
                var transport = new ArchiveFixtureTransport("unused", "packages.json") { FailAllDownloads = true };
                var diagnostics = new List<DiagnosticMessage>();

                var synced = new RepositoryArchiveSynchronizer(new RepositoryCredentialService(), transport)
                    .Sync(repository, cacheDirectory, diagnostics);

                Require(!synced, "private repository fixture must fail so diagnostic redaction can be observed.");
                var diagnosticText = string.Join("; ", diagnostics.Select(item => item.Message));
                Require(!diagnosticText.Contains(token), "repository diagnostics must not expose private access tokens.");
                Require(diagnosticText.Contains("access_token=***"), "repository diagnostics must preserve a redacted access_token marker.");
            }
        }

        private static void RepositoryArchiveSyncRejectsUnsafeEntriesAndPreservesCache()
        {
            using (var temp = TempDirectory.Create())
            {
                var repository = new PackageRepositoryConfiguration
                {
                    Id = "plughub-public-packages",
                    Provider = "github",
                    Visibility = "public",
                    Repository = "GaoMengGu/PlugHub_Packages",
                    Ref = "main",
                    Enabled = true
                };
                var cacheDirectory = Path.Combine(temp.Path, "repository-cache", repository.Id);
                WriteText(Path.Combine(cacheDirectory, "old-cache.marker"), "keep");
                var transport = new ArchiveFixtureTransport("GaoMengGu-PlugHub_Packages-main", "../escaped.txt");
                var diagnostics = new List<DiagnosticMessage>();

                var synced = new RepositoryArchiveSynchronizer(new RepositoryCredentialService(), transport)
                    .Sync(repository, cacheDirectory, diagnostics);

                Require(!synced, "repository sync must reject archive entries containing parent-directory segments.");
                RequireFileExists(Path.Combine(cacheDirectory, "old-cache.marker"));
                Require(!File.Exists(Path.Combine(temp.Path, "escaped.txt")), "unsafe archive entries must not escape the repository cache.");
                Require(diagnostics.Any(item => item.Code == "PH-REPOSITORY-ARCHIVE" && item.Message.Contains("unsafe path")),
                    "unsafe archive entries must produce an actionable repository diagnostic.");
            }
        }

        private static void RibbonLayoutComposerBuildsConfiguredLayoutAndDefaultFallback()
        {
            var view = new ViewConfiguration
            {
                Ribbon = new RibbonConfiguration
                {
                    Panels = new List<RibbonPanelLayoutConfiguration>
                    {
                        new RibbonPanelLayoutConfiguration
                        {
                            Id = "tools",
                            Name = "Tools",
                            Order = 10,
                            Items = new List<RibbonItemLayoutConfiguration>
                            {
                                new RibbonItemLayoutConfiguration
                                {
                                    Type = RibbonItemViewModel.SplitButton,
                                    Id = "split",
                                    Text = "Split",
                                    DefaultFeatureId = "feature.b",
                                    Items = new List<RibbonItemLayoutConfiguration>
                                    {
                                        new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.PushButton, FeatureId = "feature.a", Order = 20 },
                                        new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.PushButton, FeatureId = "feature.b", Order = 10 }
                                    }
                                },
                                new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.PushButton, FeatureId = "feature.a", Order = 20 }
                            }
                        }
                    }
                }
            };

            var layout = new RibbonLayoutComposer().Compose(view, new[]
            {
                Feature("feature.a", "Feature A", 10),
                Feature("feature.b", "Feature B", 20),
                Feature("feature.c", "Feature C", 30)
            });

            Require(layout.Panels.Count == 2, "configured layout must append a default panel for unplaced features.");
            var tools = layout.Panels.First(panel => panel.Name == "Tools");
            var split = tools.Items.First(item => item.Type == RibbonItemViewModel.SplitButton);
            Require(split.Items[0].FeatureId == "feature.b", "split default feature must be ordered first.");
            Require(split.Items.Select(item => item.FeatureId).SequenceEqual(new[] { "feature.b", "feature.a" }), "split children must include each configured feature once.");
            var fallbackPanel = layout.Panels.First(panel => panel.Name == "Group");
            Require(fallbackPanel.Items.Count == 1 && fallbackPanel.Items[0].FeatureId == "feature.c", "unplaced features must fall back to their feature group panel.");
            Require(layout.ClickableFeatures.Select(feature => feature.FeatureId).SequenceEqual(new[] { "feature.c", "feature.b", "feature.a" }), "clickable features must follow composed panel order.");
        }

        private static void RibbonLayoutComposerFiltersInvalidContainerChildren()
        {
            var view = new ViewConfiguration
            {
                Ribbon = new RibbonConfiguration
                {
                    Panels = new List<RibbonPanelLayoutConfiguration>
                    {
                        new RibbonPanelLayoutConfiguration
                        {
                            Id = "invalid",
                            Name = "Invalid",
                            Items = new List<RibbonItemLayoutConfiguration>
                            {
                                new RibbonItemLayoutConfiguration
                                {
                                    Type = RibbonItemViewModel.PulldownButton,
                                    Id = "pulldown",
                                    Text = "Pulldown",
                                    Items = new List<RibbonItemLayoutConfiguration>
                                    {
                                        new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.SplitButton, Id = "nested-split", FeatureId = "feature.invalid", Items = new List<RibbonItemLayoutConfiguration> { new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.PushButton, FeatureId = "feature.invalid" } } },
                                        new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.PushButton, FeatureId = "feature.valid" }
                                    }
                                },
                                new RibbonItemLayoutConfiguration
                                {
                                    Type = RibbonItemViewModel.Stack,
                                    Id = "stack",
                                    Items = new List<RibbonItemLayoutConfiguration>
                                    {
                                        new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.PushButton, FeatureId = "feature.stack.1" },
                                        new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.PushButton, FeatureId = "feature.stack.2" },
                                        new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.PushButton, FeatureId = "feature.stack.3" },
                                        new RibbonItemLayoutConfiguration { Type = RibbonItemViewModel.PushButton, FeatureId = "feature.stack.4" }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var layout = new RibbonLayoutComposer().Compose(view, new[]
            {
                Feature("feature.invalid", "Invalid Feature", 10),
                Feature("feature.valid", "Valid Feature", 20),
                Feature("feature.stack.1", "Stack 1", 30),
                Feature("feature.stack.2", "Stack 2", 40),
                Feature("feature.stack.3", "Stack 3", 50),
                Feature("feature.stack.4", "Stack 4", 60)
            });

            var pulldown = layout.Panels.First(panel => panel.Name == "Invalid").Items.First(item => item.Id == "pulldown");
            Require(pulldown.Items.Count == 1 && pulldown.Items[0].FeatureId == "feature.valid", "pulldown and split containers must only contain push buttons.");
            var stack = layout.Panels.First(panel => panel.Name == "Invalid").Items.First(item => item.Id == "stack");
            Require(stack.Items.Select(item => item.FeatureId).SequenceEqual(new[] { "feature.stack.1", "feature.stack.2", "feature.stack.3" }), "stack must keep at most three legal child items.");
            var fallback = layout.Panels.First(panel => panel.Name == "Group");
            Require(fallback.Items.Select(item => item.FeatureId).SequenceEqual(new[] { "feature.invalid", "feature.stack.4" }), "features from invalid container positions must be returned to their feature group panel.");
        }

        private static void SensitiveTextRedactorMasksRepositoryTokens()
        {
            const string input =
                "https://oauth2:token-value@example.com/repo.git " +
                "https://user:password-value@example.com/repo.git?access_token=query-token " +
                "apiKey=\"plain-api-key\"";

            var redacted = SensitiveTextRedactor.Redact(input);

            RequireDoesNotContain(redacted, "token-value");
            RequireDoesNotContain(redacted, "password-value");
            RequireDoesNotContain(redacted, "query-token");
            RequireDoesNotContain(redacted, "plain-api-key");
            RequireContains(redacted, "https://oauth2:***@example.com");
            RequireContains(redacted, "https://user:***@example.com");
            RequireContains(redacted, "access_token=***");
            RequireContains(redacted, "apiKey=\"***");
        }

        private static void FrameworkUpdatePackageAcceptsSingleManagerMaintenancePayload()
        {
            using (var temp = TempDirectory.Create())
            {
                var zipPath = Path.Combine(temp.Path, "framework.zip");
                WriteUpdatePackage(zipPath, new[]
                {
                    "PlugHub.Revit2020.dll",
                    "PlugHub.Framework.dll",
                    "PlugHub.Contracts.dll",
                    "PlugHub.Wpf.dll",
                    "PlugHub.Manager.exe"
                });

                new FrameworkUpdatePackageValidator().Validate(zipPath);
            }
        }

        private static void FrameworkUpdatePackageRejectsMissingManagerMaintenancePayload()
        {
            using (var temp = TempDirectory.Create())
            {
                var zipPath = Path.Combine(temp.Path, "framework.zip");
                WriteUpdatePackage(zipPath, new[]
                {
                    "PlugHub.Revit2020.dll",
                    "PlugHub.Framework.dll",
                    "PlugHub.Contracts.dll",
                    "PlugHub.Wpf.dll"
                });

                var failed = false;
                try
                {
                    new FrameworkUpdatePackageValidator().Validate(zipPath);
                }
                catch (InvalidDataException ex) when (ex.Message.Contains("PlugHub.Manager.exe"))
                {
                    failed = true;
                }

                Require(failed, "framework update validator must require PlugHub.Manager.exe.");
            }
        }

        private static void FrameworkUpdateSelectsOnlyExactVersionAsset()
        {
            var release = new ReleaseInfo
            {
                TagName = "V2.0.0",
                Assets = new List<ReleaseAssetInfo>
                {
                    new ReleaseAssetInfo
                    {
                        Name = "PlugHub-Revit2020-V1.9.0.zip",
                        DownloadUrl = "https://example.com/PlugHub-Revit2020-V1.9.0.zip"
                    },
                    new ReleaseAssetInfo
                    {
                        Name = "PlugHub-Revit2020-V2.0.0.zip.sigstore.json",
                        DownloadUrl = "https://example.com/PlugHub-Revit2020-V2.0.0.zip.sigstore.json"
                    }
                }
            };

            var selected = FrameworkUpdatePolicy.SelectUpdateAsset(release, "V2.0.0");
            Require(selected == null, "framework update must not select a mismatched Revit2020 zip for the target release version.");

            release.Assets.Add(new ReleaseAssetInfo
            {
                Name = "PlugHub-Revit2020-V2.0.0.zip",
                DownloadUrl = "https://example.com/PlugHub-Revit2020-V2.0.0.zip"
            });

            selected = FrameworkUpdatePolicy.SelectUpdateAsset(release, "V2.0.0");
            Require(selected != null && selected.Name == "PlugHub-Revit2020-V2.0.0.zip", "framework update must select the exact Revit2020 zip for the target release version.");
        }

        private static void ReleaseClientSelectsLatestTestPrerelease()
        {
            var client = new ReleaseClient();
            var release = client.ParseLatestTestPrereleaseJson(
                "[" +
                "{\"tag_name\":\"V1.5.2\",\"prerelease\":false,\"draft\":false,\"body\":\"stable\",\"assets\":[{\"name\":\"PlugHub-Revit2020-V1.5.2.zip\",\"browser_download_url\":\"https://example.com/stable.zip\"}]}," +
                "{\"tag_name\":\"TV1.5.2\",\"prerelease\":true,\"draft\":false,\"body\":\"old test\",\"assets\":[{\"name\":\"PlugHub-Revit2020-TV1.5.2.zip\",\"browser_download_url\":\"https://example.com/old.zip\"}]}," +
                "{\"tag_name\":\"TV1.5.4\",\"prerelease\":true,\"draft\":true,\"body\":\"draft test\",\"assets\":[{\"name\":\"PlugHub-Revit2020-TV1.5.4.zip\",\"browser_download_url\":\"https://example.com/draft.zip\"}]}," +
                "{\"tag_name\":\"TV1.5.3\",\"prerelease\":true,\"draft\":false,\"body\":\"latest test\",\"assets\":[{\"name\":\"PlugHub-Revit2020-TV1.5.3.zip\",\"browser_download_url\":\"https://example.com/latest.zip\"}]}" +
                "]");

            Require(release.TagName == "TV1.5.3", "test update release list must select the newest non-draft TV prerelease.");
            Require(release.Body == "latest test", "test update release list must preserve the selected release notes.");
            Require(release.Assets.Count == 1
                && release.Assets[0].Name == "PlugHub-Revit2020-TV1.5.3.zip"
                && release.Assets[0].DownloadUrl == "https://example.com/latest.zip",
                "test update release list must preserve the selected release asset.");
        }

        private static void FrameworkUpdateChecksGitHubTestPrereleasesForTvBuilds()
        {
            var stableSources = new[]
            {
                new FrameworkUpdateSource(
                    FrameworkUpdateSourceKind.GiteeTagList,
                    "Gitee",
                    new Uri("https://gitee.com/api/v5/repos/GaoMengGu/PlugHub/tags"),
                    "https://gitee.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}"),
                new FrameworkUpdateSource(
                    FrameworkUpdateSourceKind.GitHubLatestRelease,
                    "GitHub",
                    new Uri("https://api.github.com/repos/GaoMengGu/PlugHub/releases/latest"),
                    "https://github.com/GaoMengGu/PlugHub/releases/download/{tag}/{asset}")
            };
            var sources = FrameworkUpdatePolicy.BuildCheckSources("TV1.5.3", stableSources)
                .ToList();
            var firstStableSourceIndex = sources.FindIndex(source => source.Kind != FrameworkUpdateSourceKind.GitHubTestPrereleaseList);
            var firstTestSourceIndex = sources.FindIndex(source => source.Kind == FrameworkUpdateSourceKind.GitHubTestPrereleaseList);

            Require(firstTestSourceIndex >= 0, "TV builds must query a GitHub test prerelease source.");
            Require(firstStableSourceIndex < 0 || firstTestSourceIndex < firstStableSourceIndex, "TV builds must query GitHub test prereleases before Gitee or stable GitHub sources.");

            var testSource = sources[firstTestSourceIndex];
            Require(testSource.Name == "GitHub Test", "TV build test source must be named GitHub Test.");
            Require(testSource.Uri.AbsoluteUri == "https://api.github.com/repos/GaoMengGu/PlugHub/releases", "TV build test source must use the GitHub release list API.");
            Require(testSource.ContinueWhenNoUpdate, "TV build test source must continue to stable sources when no newer test release exists.");
        }

        private static void FrameworkUpdateTreatsTestChannelTagsAsComparableVersions()
        {
            var isNewer = FrameworkUpdatePolicy.IsNewerVersion("TV1.5.2", "V1.5.1");
            Require(isNewer, "test update tag TV1.5.2 must compare newer than stable tag V1.5.1.");

            var sameVersion = FrameworkUpdatePolicy.IsNewerVersion("TV1.5.2", "V1.5.2");
            Require(!sameVersion, "test update tag TV1.5.2 must not compare newer than stable tag V1.5.2.");

            var stableSameVersion = FrameworkUpdatePolicy.IsNewerVersion("V1.5.2", "TV1.5.2");
            Require(stableSameVersion, "stable tag V1.5.2 must compare newer than test tag TV1.5.2 so testers can return to the official channel.");

            var olderStable = FrameworkUpdatePolicy.IsNewerVersion("V1.5.1", "TV1.5.2");
            Require(!olderStable, "stable tag V1.5.1 must not compare newer than test tag TV1.5.2.");
        }

        private static void RibbonDesignerMapperHydratesConfiguredFeatureIcons()
        {
            var feature = DesignerFeature("icon-package.run", "Run Icon Package", "Tools", 100);
            feature.IconPath = "icons/package.png";

            var ribbon = new RibbonConfiguration
            {
                Panels = new List<RibbonPanelLayoutConfiguration>
                {
                    new RibbonPanelLayoutConfiguration
                    {
                        Id = "tools",
                        Name = "Tools",
                        Order = 100,
                        Items = new List<RibbonItemLayoutConfiguration>
                        {
                            new RibbonItemLayoutConfiguration
                            {
                                Type = "pushButton",
                                Id = "icon-package.run",
                                FeatureId = "icon-package.run",
                                TextOverride = "Run Icon Package",
                                Size = "large",
                                Order = 100
                            }
                        }
                    }
                }
            };

            var tabs = new RibbonLayoutEditor().Load(ribbon, new[] { feature });
            var button = tabs.Single().Children.Single().Children.Single();
            Require(button.IconPath == "icons/package.png", "configured layout buttons without an explicit icon override must hydrate the current package feature icon.");
        }

        private static void FeatureSlotAllocatorAssignsStableBoundedBidirectionalMappings()
        {
            var features = new List<FeatureViewModel>
            {
                Feature(string.Empty, "blank", 0),
                Feature("feature-001", "first", 1),
                Feature("FEATURE-001", "duplicate", 2)
            };
            features.AddRange(Enumerable.Range(2, 128)
                .Select(index => Feature("feature-" + index.ToString("000"), "feature " + index, index)));
            features.Add(Feature("FEATURE-129", "skipped duplicate", 130));

            var result = new FeatureSlotAllocator().Allocate(features, 128);

            Require(result.SlotToFeatureId.Count == 128, "only the first 128 unique non-empty feature IDs may consume Revit command slots.");
            Require(result.FeatureIdToSlot.Count == 128, "forward and reverse feature slot mappings must have the same cardinality.");
            Require(result.SlotToFeatureId[1] == "feature-001", "slot allocation must preserve the input order.");
            Require(result.FeatureIdToSlot["FEATURE-001"] == 1, "feature ID lookup must be case-insensitive and duplicates must reuse the first assignment.");
            Require(result.SlotToFeatureId[128] == "feature-128", "the 128th unique feature must receive the final available slot.");
            Require(result.SkippedFeatureIds.SequenceEqual(new[] { "feature-129" }), "the 129th unique feature must be reported as skipped.");
            Require(result.SlotToFeatureId.All(pair => result.FeatureIdToSlot[pair.Value] == pair.Key), "slot mappings must remain bidirectionally consistent.");
        }

        private static void RibbonLayoutEditorMergesPanelsAndRestoresVisibleFeatures()
        {
            var ribbon = new RibbonConfiguration
            {
                Panels = new List<RibbonPanelLayoutConfiguration>
                {
                    DesignerPanel("tools-a", "Tools", "feature.a"),
                    DesignerPanel("tools-b", "tools", "feature.a")
                }
            };
            var features = new[]
            {
                DesignerFeature("feature.a", "Feature A", "Tools", 10),
                DesignerFeature("feature.b", "Feature B", "Other", 20)
            };

            var tabs = new RibbonLayoutEditor().Load(ribbon, features);
            var panels = tabs.Single().Children;
            Require(panels.Count == 2, "same-name panels must merge and missing visible features must be restored in the default panel.");
            Require(panels.Single(panel => panel.Text == "Tools").Children.Count == 1, "same-name panel merge must remove duplicate feature placement.");
            Require(panels.Single(panel => panel.Id == "default").Children.Single().FeatureId == "feature.b", "missing visible feature must be restored to the default panel.");

            var tools = panels.Single(panel => panel.Text == "Tools");
            Require(new RibbonLayoutEditor().RemoveContainer(tabs, tools), "editing seam must remove a selected layout container.");
            Require(tabs.Single().Children.Single(panel => panel.Id == "default").Children.Select(child => child.FeatureId).OrderBy(id => id).SequenceEqual(new[] { "feature.a", "feature.b" }), "removing a container must return its features to the default panel atomically.");
        }

        private static void RibbonLayoutEditorNormalizesStacksBeforeSave()
        {
            var editor = new RibbonLayoutEditor();
            var tab = DesignerNode(RibbonDesignerNodeRow.Tab, "tab");
            var panel = DesignerNode(RibbonDesignerNodeRow.Panel, "panel");
            var emptyStack = DesignerNode(RibbonDesignerNodeRow.Stack, "empty");
            var singleStack = DesignerNode(RibbonDesignerNodeRow.Stack, "single");
            singleStack.Children.Add(DesignerFeatureNode("feature.a"));
            panel.Children.Add(emptyStack);
            panel.Children.Add(singleStack);
            tab.Children.Add(panel);
            var tabs = new List<RibbonDesignerNodeRow> { tab };

            var panels = editor.PrepareForSave(tabs, new[] { DesignerFeature("feature.a", "Feature A", "Tools", 10) });
            Require(panels.Single().Items.Count == 1, "empty stack must be removed and a single-child stack must unwrap before save.");
            Require(panels.Single().Items.Single().Type == RibbonDesignerNodeRow.PushButton, "single-child stack must save as its only button.");
        }

        private static void RibbonLayoutEditorRejectsInvalidLayouts()
        {
            var editor = new RibbonLayoutEditor();
            var outer = DesignerNode(RibbonDesignerNodeRow.Stack, "outer");
            outer.Children.Add(DesignerNode(RibbonDesignerNodeRow.Stack, "inner"));
            RequireInvalidLayout(() => editor.Validate(new[] { outer }), "堆叠控件不能嵌套堆叠");

            var duplicatePanel = DesignerNode(RibbonDesignerNodeRow.Panel, "duplicates");
            duplicatePanel.Children.Add(DesignerFeatureNode("feature.a"));
            duplicatePanel.Children.Add(DesignerFeatureNode("feature.a"));
            RequireInvalidLayout(() => editor.Validate(new[] { duplicatePanel }), "布局中存在重复功能");
        }

        private static void ManagerUpdaterRemovesStaleStandaloneMaintenancePdbs()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "PlugHub");
                Directory.CreateDirectory(installDirectory);
                WritePlugHubInstallMarkers(installDirectory);
                foreach (var stale in new[]
                {
                    "PlugHub.Updater.exe",
                    "PlugHub.Updater.pdb",
                    "PlugHub.Uninstaller.exe",
                    "PlugHub.Uninstaller.pdb",
                    "PlugHub-Uninstall.exe"
                })
                {
                    WriteText(Path.Combine(installDirectory, stale), "stale");
                }

                var zipPath = Path.Combine(temp.Path, "framework.zip");
                WriteUpdatePackage(zipPath, new[]
                {
                    "PlugHub.Revit2020.dll",
                    "PlugHub.Framework.dll",
                    "PlugHub.Contracts.dll",
                    "PlugHub.Wpf.dll",
                    "PlugHub.Manager.exe"
                });

                RunManagerFrameworkUpdate(zipPath, installDirectory);

                RequireFileMissing(Path.Combine(installDirectory, "PlugHub.Updater.pdb"));
                RequireFileMissing(Path.Combine(installDirectory, "PlugHub.Uninstaller.pdb"));
            }
        }

        private static void ManagerUpdaterValidatesMaintenancePayload()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "PlugHub");
                Directory.CreateDirectory(installDirectory);
                WritePlugHubInstallMarkers(installDirectory);

                var zipPath = Path.Combine(temp.Path, "framework.zip");
                WriteUpdatePackage(zipPath, new[]
                {
                    "PlugHub.Revit2020.dll",
                    "PlugHub.Framework.dll",
                    "PlugHub.Contracts.dll",
                    "PlugHub.Wpf.dll"
                });

                var failed = false;
                try
                {
                    RunManagerFrameworkUpdate(zipPath, installDirectory);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("PlugHub.Manager.exe"))
                {
                    failed = true;
                }

                Require(failed, "manager updater must validate maintenance payload before copying framework files.");
            }
        }

        private static void ManagerUpdaterRejectsNonPlugHubDirectory()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "NotPlugHub");
                Directory.CreateDirectory(installDirectory);
                var zipPath = Path.Combine(temp.Path, "framework.zip");
                WriteUpdatePackage(zipPath, new[]
                {
                    "PlugHub.Revit2020.dll",
                    "PlugHub.Framework.dll",
                    "PlugHub.Contracts.dll",
                    "PlugHub.Wpf.dll",
                    "PlugHub.Manager.exe"
                });

                var failed = false;
                try
                {
                    RunManagerFrameworkUpdate(zipPath, installDirectory);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("not a PlugHub install root"))
                {
                    failed = true;
                }

                Require(failed, "manager updater must reject directories without PlugHub install markers.");
            }
        }

        private static void ManagerUpdaterRejectsMarkerValidatedParentDirectory()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "Program Files");
                WritePlugHubInstallMarkers(installDirectory);
                var zipPath = Path.Combine(temp.Path, "framework.zip");
                WriteUpdatePackage(zipPath, new[]
                {
                    "PlugHub.Revit2020.dll",
                    "PlugHub.Framework.dll",
                    "PlugHub.Contracts.dll",
                    "PlugHub.Wpf.dll",
                    "PlugHub.Manager.exe"
                });

                var failed = false;
                try
                {
                    RunManagerFrameworkUpdate(zipPath, installDirectory);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("not a PlugHub install root"))
                {
                    failed = true;
                }

                Require(failed, "manager updater must not update a marker-validated parent directory that is not a PlugHub install root.");
            }
        }

        private static void ManagerMaintenanceLoggerDoesNotRecreateDeletedInstallDirectory()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "PlugHub");
                Directory.CreateDirectory(installDirectory);

                var managerAssembly = Assembly.Load("PlugHub.Manager");
                var loggerType = managerAssembly.GetType("PlugHub.Manager.Maintenance.ManagerMaintenanceLogger", true)!;
                var logger = Activator.CreateInstance(
                    loggerType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new object[] { installDirectory },
                    null)!;

                Directory.Delete(installDirectory, true);
                var info = loggerType.GetMethod("Info", BindingFlags.Instance | BindingFlags.Public)!;
                info.Invoke(logger, new object[] { "after uninstall" });

                Require(!Directory.Exists(installDirectory), "maintenance logger must not recreate the deleted PlugHub install directory.");
            }
        }

        private static void ManagerUninstallerAcceptsLocalRevitBuildOutputDirectory()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "dist", "Revit2020");
                WriteText(Path.Combine(installDirectory, "PlugHub.addin"), "addin");
                WriteText(Path.Combine(installDirectory, "PlugHub.Revit2020.dll"), "revit");
                WriteText(Path.Combine(installDirectory, "PlugHub.Framework.dll"), "framework");
                WriteText(Path.Combine(installDirectory, "PlugHub.Contracts.dll"), "contracts");
                WriteText(Path.Combine(installDirectory, "PlugHub.Wpf.dll"), "wpf");
                WriteText(Path.Combine(installDirectory, "PlugHub.Manager.exe"), "manager");

                var validated = ValidateManagerUninstallDirectory(installDirectory);

                Require(SamePath(validated, installDirectory), "local Revit build output directory must be accepted as a PlugHub install root.");
            }
        }

        private static void ManagerUninstallerRejectsNonPlugHubDirectory()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "dist", "Revit2020");
                Directory.CreateDirectory(installDirectory);

                var failed = false;
                try
                {
                    ValidateManagerUninstallDirectory(installDirectory);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("not a PlugHub install root"))
                {
                    failed = true;
                }

                Require(failed, "manager uninstaller must reject directories without PlugHub install markers.");
            }
        }

        private static void ManagerUninstallerRejectsMarkerValidatedParentDirectory()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "Program Files");
                WritePlugHubInstallMarkers(installDirectory);

                var failed = false;
                try
                {
                    ValidateManagerUninstallDirectory(installDirectory);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("not a PlugHub install root"))
                {
                    failed = true;
                }

                Require(failed, "manager uninstaller must not delete a marker-validated parent directory that is not a PlugHub install root.");
            }
        }

        private static void ManagerUninstallerRejectsUnmarkedPlugHubNamedDirectory()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "PlugHub");
                Directory.CreateDirectory(installDirectory);

                var failed = false;
                try
                {
                    ValidateManagerUninstallDirectory(installDirectory);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("not a PlugHub install root"))
                {
                    failed = true;
                }

                Require(failed, "manager uninstaller must not trust directory name alone without PlugHub install markers.");
            }
        }

        private static string WriteRepositoryPackage(string repositoryDirectory, string version, string assemblyContent)
        {
            var manifestPath = Path.Combine(repositoryDirectory, "packages.json");
            WriteText(Path.Combine(repositoryDirectory, "bin", "Module.dll"), assemblyContent);
            new PackageManifestWriter().WritePackageManifest(manifestPath, new ModulesConfiguration
            {
                SchemaVersion = "1.0",
                IndexVersion = version,
                Modules = new List<ModuleConfiguration>
                {
                    new ModuleConfiguration
                    {
                        Id = "module.repo",
                        Version = version,
                        Assembly = "bin/Module.dll",
                        Features = new List<FeatureConfiguration>
                        {
                            new FeatureConfiguration
                            {
                                Id = "feature.repo",
                                DisplayName = "Repository Feature",
                                CommandType = "Vendor.RepositoryCommand"
                            }
                        }
                    }
                }
            });

            return manifestPath;
        }

        private static void WritePackageManifest(string manifestPath, string moduleId)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? string.Empty);
            new PackageManifestWriter().WritePackageManifest(manifestPath, new ModulesConfiguration
            {
                SchemaVersion = "1.0",
                Modules = new List<ModuleConfiguration>
                {
                    new ModuleConfiguration
                    {
                        Id = moduleId,
                        Version = "1.0.0",
                        Assembly = "bin/Module.dll",
                        Features = new List<FeatureConfiguration>
                        {
                            new FeatureConfiguration
                            {
                                Id = moduleId + ".feature",
                                DisplayName = moduleId,
                                CommandType = "Vendor.Command"
                            }
                        }
                    }
                }
            });
        }

        private static RepositoryPackageDescriptor PackageDescriptor(string manifestPath, string repositoryDirectory, string moduleId)
        {
            return new RepositoryPackageDescriptor
            {
                RepositoryId = "local",
                PackageId = moduleId,
                ModuleId = moduleId,
                ManifestPath = manifestPath,
                SourceDirectory = repositoryDirectory
            };
        }

        private static FeatureViewModel Feature(string id, string name, int order)
        {
            return new FeatureViewModel
            {
                FeatureId = id,
                ModuleId = "module." + id,
                DisplayName = name,
                GroupId = "group",
                GroupName = "Group",
                GroupOrder = 1,
                DisplayOrder = order,
                IsEnabled = true,
                ButtonSize = "large",
                IconPath = "icons/" + id + ".png"
            };
        }

        private static void ManagerMaintenanceSharesInstallRootSafetyPolicy()
        {
            using (var temp = TempDirectory.Create())
            {
                var installDirectory = Path.Combine(temp.Path, "PlugHub");
                WritePlugHubInstallMarkers(installDirectory);

                var updatePath = PlugHubInstallRootPolicy.Validate(installDirectory, PlugHubInstallRootOperation.Update);
                var uninstallPath = PlugHubInstallRootPolicy.Validate(installDirectory, PlugHubInstallRootOperation.Uninstall);

                Require(SamePath(updatePath, installDirectory) && SamePath(uninstallPath, installDirectory),
                    "updater and uninstaller must share one accepted PlugHub install root policy.");
            }
        }

        private static RibbonDesignerFeatureRow DesignerFeature(string id, string name, string groupName, int order)
        {
            return new RibbonDesignerFeatureRow
            {
                FeatureId = id,
                Name = name,
                FeatureName = name,
                DisplayName = name,
                DisplayText = name,
                ModuleId = "module." + id,
                ModuleName = "Module",
                Group = groupName,
                GroupDisplayText = groupName,
                ButtonSize = "large",
                Order = order,
                Visible = true
            };
        }

        private static RibbonPanelLayoutConfiguration DesignerPanel(string id, string name, string featureId)
        {
            return new RibbonPanelLayoutConfiguration
            {
                Id = id,
                Name = name,
                Order = 100,
                Items = new List<RibbonItemLayoutConfiguration>
                {
                    new RibbonItemLayoutConfiguration
                    {
                        Type = RibbonDesignerNodeRow.PushButton,
                        Id = featureId,
                        FeatureId = featureId,
                        Order = 100
                    }
                }
            };
        }

        private static RibbonDesignerNodeRow DesignerNode(string type, string id)
        {
            return new RibbonDesignerNodeRow { NodeType = type, Id = id, Text = id, Order = 100 };
        }

        private static RibbonDesignerNodeRow DesignerFeatureNode(string featureId)
        {
            var row = DesignerNode(RibbonDesignerNodeRow.PushButton, featureId);
            row.FeatureId = featureId;
            return row;
        }

        private static void RequireInvalidLayout(Action action, string expectedMessage)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains(expectedMessage))
            {
                return;
            }

            throw new InvalidOperationException("expected invalid Ribbon layout: " + expectedMessage);
        }

        private static void WriteText(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllText(path, text);
        }

        private static void WriteUpdatePackage(string zipPath, IEnumerable<string> entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath) ?? string.Empty);
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var entry in entries)
                {
                    var archiveEntry = archive.CreateEntry(entry);
                    using (var writer = new StreamWriter(archiveEntry.Open()))
                    {
                        writer.Write(entry);
                    }
                }
            }
        }

        private static void WritePlugHubInstallMarkers(string installDirectory)
        {
            foreach (var marker in new[]
            {
                "PlugHub.Revit2020.dll",
                "PlugHub.Framework.dll",
                "PlugHub.Contracts.dll",
                "PlugHub.Wpf.dll",
                "PlugHub.Manager.exe"
            })
            {
                WriteText(Path.Combine(installDirectory, marker), marker);
            }
        }

        private static void RequireFileExists(string path)
        {
            Require(File.Exists(path), "expected file to exist: " + path);
        }

        private static void RequireFileMissing(string path)
        {
            Require(!File.Exists(path), "expected file to be absent: " + path);
        }

        private static void RequireContains(string text, string expected)
        {
            Require(text.Contains(expected), "expected text to contain: " + expected);
        }

        private static void RequireDoesNotContain(string text, string forbidden)
        {
            Require(!text.Contains(forbidden), "expected text to omit: " + forbidden);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void SetPropertyValue(object target, string propertyName, object value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                throw new InvalidOperationException("Missing property " + propertyName + " on " + target.GetType().FullName);
            }

            property.SetValue(target, value);
        }

        private static string GetPropertyValue(object target, string propertyName)
        {
            var value = GetPropertyObject(target, propertyName);
            if (value is Uri uri)
            {
                return uri.AbsoluteUri;
            }

            return Convert.ToString(value) ?? string.Empty;
        }

        private static object GetPropertyObject(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                throw new InvalidOperationException("Missing property " + propertyName + " on " + target.GetType().FullName);
            }

            return property.GetValue(target) ?? string.Empty;
        }

        private static bool SamePath(string left, string right)
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string ValidateManagerUninstallDirectory(string installDirectory)
        {
            return PlugHubInstallRootPolicy.Validate(installDirectory, PlugHubInstallRootOperation.Uninstall);
        }

        private static void RunManagerFrameworkUpdate(string payloadZip, string installDirectory)
        {
            var managerAssembly = Assembly.Load("PlugHub.Manager");
            var argumentsType = managerAssembly.GetType("PlugHub.Manager.Maintenance.ManagerMaintenanceArguments", true)!;
            var parse = argumentsType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)!;
            var arguments = parse.Invoke(null, new object[]
            {
                new[]
                {
                    "/update",
                    "/payloadZip",
                    payloadZip,
                    "/installDir",
                    installDirectory,
                    "/targetVersion",
                    "V-test"
                }
            })!;

            var loggerType = managerAssembly.GetType("PlugHub.Manager.Maintenance.ManagerMaintenanceLogger", true)!;
            var logger = Activator.CreateInstance(loggerType, installDirectory)!;
            var updaterType = managerAssembly.GetType("PlugHub.Manager.Maintenance.ManagerFrameworkUpdater", true)!;
            var updater = Activator.CreateInstance(updaterType, logger)!;
            var run = updaterType.GetMethod("Run", BindingFlags.Instance | BindingFlags.Public)!;
            try
            {
                run.Invoke(updater, new[] { arguments });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new InvalidOperationException(ex.InnerException.Message, ex.InnerException);
            }
        }

        private sealed class TestCase
        {
            public TestCase(string name, Action body)
            {
                Name = name;
                Body = body;
            }

            public string Name { get; }
            public Action Body { get; }
        }

        private sealed class ArchiveFixtureTransport : IRepositoryRemoteTransport
        {
            private readonly string _relativePath;

            public ArchiveFixtureTransport(string wrapperDirectory, string relativePath)
            {
                WrapperDirectory = wrapperDirectory;
                _relativePath = relativePath;
            }

            public string WrapperDirectory { get; }
            public List<Uri> DownloadedUris { get; } = new List<Uri>();
            public HashSet<string> FailHosts { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public bool FailAllDownloads { get; set; }

            public void Download(Uri uri, string targetPath, string authorizationHeader)
            {
                DownloadedUris.Add(uri);
                if (FailAllDownloads || FailHosts.Contains(uri.Host))
                {
                    throw new InvalidOperationException("fixture download failed: " + uri);
                }

                using (var archive = ZipFile.Open(targetPath, ZipArchiveMode.Create))
                {
                    var manifest = archive.CreateEntry(WrapperDirectory + "/packages.json");
                    using (var writer = new StreamWriter(manifest.Open()))
                    {
                        writer.Write("{\"schemaVersion\":\"1.1\",\"modules\":[]}");
                    }

                    var project = archive.CreateEntry(WrapperDirectory + "/" + _relativePath.Replace('\\', '/'));
                    using (var writer = new StreamWriter(project.Open()))
                    {
                        writer.Write("<Project />");
                    }
                }
            }

            public string ReadText(Uri uri, string accept)
            {
                throw new InvalidOperationException("fixture transport does not provide Gitee API responses: " + uri);
            }
        }

        private sealed class TempDirectory : IDisposable
        {
            private TempDirectory(string path)
            {
                Path = path;
            }

            public string Path { get; }

            public static TempDirectory Create()
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plughub-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path);
                return new TempDirectory(path);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                    {
                        Directory.Delete(Path, true);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
