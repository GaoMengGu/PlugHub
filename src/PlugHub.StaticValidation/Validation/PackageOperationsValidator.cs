using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class PackageOperationsValidator
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public void Validate()
        {
            ValidatePendingPackageOperationStoreBehavior();
            ValidateRepositoryInstallFlowBehavior();
            ValidateRepositoryPackageGranularityAndInstallPayload();
            ValidateRuntimeLoadsSerializedInstalledPackageManifest();
            ValidateRepositoryInstallFailureDoesNotCreateOrRemovePackages();
            ValidateLockedPackageOperationBehavior();
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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
