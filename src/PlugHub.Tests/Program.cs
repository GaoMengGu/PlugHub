using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using PlugHub.Contracts.Features;
using PlugHub.Framework.Composition;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;
using PlugHub.Framework.Packages;
using PlugHub.Framework.Settings;
using PlugHub.Framework.Sources;
using PlugHub.Framework.Updates;

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
                new TestCase("package install service copies selected module payload only", PackageInstallServiceCopiesSelectedModulePayloadOnly),
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
                new TestCase("ribbon layout composer builds configured layout and default fallback", RibbonLayoutComposerBuildsConfiguredLayoutAndDefaultFallback),
                new TestCase("ribbon layout composer filters invalid container children", RibbonLayoutComposerFiltersInvalidContainerChildren),
                new TestCase("framework update package accepts single manager maintenance payload", FrameworkUpdatePackageAcceptsSingleManagerMaintenancePayload),
                new TestCase("framework update package rejects missing manager maintenance payload", FrameworkUpdatePackageRejectsMissingManagerMaintenancePayload),
                new TestCase("manager updater removes stale standalone maintenance pdbs", ManagerUpdaterRemovesStaleStandaloneMaintenancePdbs),
                new TestCase("manager updater rejects non PlugHub directory", ManagerUpdaterRejectsNonPlugHubDirectory),
                new TestCase("manager uninstaller accepts local Revit build output directory", ManagerUninstallerAcceptsLocalRevitBuildOutputDirectory),
                new TestCase("manager uninstaller rejects non PlugHub directory", ManagerUninstallerRejectsNonPlugHubDirectory),
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
                    failures.Add(test.Name + ": " + ex.Message);
                    Console.Error.WriteLine("FAIL " + test.Name + ": " + ex.Message);
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
                Require(documentPaths.Count == 1, "settings store must load only non-git module manifests.");
                Require(!documentPaths.Any(path => path.IndexOf(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0), "settings store must ignore manifests inside .git directories.");
                Require(documents[0].Modules.Modules.Select(module => module.Id).SequenceEqual(new[] { "module.live" }), "settings store must load the live manifest.");
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
                var pendingAfterInstall = service.ListPendingOperations(baseDirectory);
                Require(pendingAfterInstall.Count == 1 && pendingAfterInstall[0].Operation == "restart", "install must queue a restart operation.");

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
            var fallbackPanel = layout.Panels.First(panel => panel.Id == "default");
            Require(fallbackPanel.Items.Count == 1 && fallbackPanel.Items[0].FeatureId == "feature.c", "unplaced features must fall back to the default panel.");
            Require(layout.ClickableFeatures.Select(feature => feature.FeatureId).SequenceEqual(new[] { "feature.b", "feature.a", "feature.c" }), "clickable features must match the composed layout order.");
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
            var fallback = layout.Panels.First(panel => panel.Id == "default");
            Require(fallback.Items.Select(item => item.FeatureId).SequenceEqual(new[] { "feature.invalid", "feature.stack.4" }), "features from invalid container positions must be returned to the default panel.");
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

        private static bool SamePath(string left, string right)
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string ValidateManagerUninstallDirectory(string installDirectory)
        {
            var managerAssembly = Assembly.Load("PlugHub.Manager");
            var uninstallerType = managerAssembly.GetType("PlugHub.Manager.Maintenance.ManagerUninstaller", true)!;
            var validate = uninstallerType.GetMethod("ValidateInstallDirectory", BindingFlags.Static | BindingFlags.NonPublic)!;
            try
            {
                return (string)validate.Invoke(null, new object[] { installDirectory })!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new InvalidOperationException(ex.InnerException.Message, ex.InnerException);
            }
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
