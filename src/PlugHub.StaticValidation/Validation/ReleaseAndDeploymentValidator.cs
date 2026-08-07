using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class ReleaseAndDeploymentValidator
    {
        private readonly string _root;

        public ReleaseAndDeploymentValidator(string root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Validate()
        {
            ValidateRevitApiReferenceStrategy();
            ValidateReleaseInstallerPackaging();
            ValidateGiteeReleaseMirrorPackaging();
            ValidateMachineWideAddinRegistration();
            ValidateUninstallerPackaging();
            ValidateFrameworkAutoUpdateSpecification();
            ValidateReleaseVersioningWorkflow();
            ValidateSigningGuidance();
            ValidateRevitDeploymentConfiguration();
        }

        private void ValidateRevitApiReferenceStrategy()
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

        private void ValidateReleaseInstallerPackaging()
        {
            var installerProject = ReadText("src/PlugHub.Installer/PlugHub.Installer.csproj");
            var installerForm = ReadText("src/PlugHub.Installer/InstallerForm.cs");
            var installerPayload = ReadText("src/PlugHub.Installer/InstallerPayload.cs");
            var addinWriter = ReadText("src/PlugHub.Installer/AddinManifestWriter.cs");
            var installerManifest = ReadText("src/PlugHub.Installer/app.manifest");
            var workflow = ReadText(".github/workflows/release.yml");
            var testUpdateWorkflow = ReadText(".github/workflows/test-update-release.yml");
            var solution = ReadText("PlugHub.sln");
            var solutionX = ReadText("PlugHub.slnx");
            var readme = ReadText("README.md");

            Require(installerProject.Contains("<OutputType>WinExe</OutputType>"), "installer project must build a Windows EXE.");
            Require(installerProject.Contains("<TargetFramework>net48</TargetFramework>"), "installer project must target net48.");
            Require(installerProject.Contains("<ApplicationManifest>app.manifest</ApplicationManifest>") && installerManifest.Contains("requestedExecutionLevel level=\"requireAdministrator\""), "installer must request UAC elevation before copying protected files or registering the machine-wide addin.");
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

        private void ValidateGiteeReleaseMirrorPackaging()
        {
            var releaseWorkflow = ReadText(".github/workflows/release.yml");
            var syncWorkflow = ReadText(".github/workflows/sync-gitee.yml");
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
            Require(syncWorkflow.Contains("timeout 20 ssh-keyscan -T 10") && syncWorkflow.Contains("host_keys=\"$(mktemp)\"") && syncWorkflow.Contains("ConnectTimeout 20") && syncWorkflow.Contains("push_with_retry") && syncWorkflow.Contains("for attempt in 1 2 3") && syncWorkflow.Contains("Gitee SSH push failed after 3 attempts"), "Gitee git mirroring must retain host keys only after a bounded successful lookup, then retry transient SSH failures with a diagnostic that distinguishes reachability from credentials.");
        }

        private void ValidateMachineWideAddinRegistration()
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

        private void ValidateUninstallerPackaging()
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
            Require(maintenanceLauncher.Contains("StartUninstall") && maintenanceLauncher.Contains("CreateTemporaryManagerCopy") && maintenanceLauncher.Contains("Path.GetTempPath()") && maintenanceLauncher.Contains("PlugHub.Manager.exe") && maintenanceLauncher.Contains("UseShellExecute = true") && maintenanceLauncher.Contains("Verb = \"runas\""), "Manager update and uninstall must run from a temporary elevated PlugHub.Manager.exe copy so protected install directories remain maintainable.");
            Require(maintenanceRunner.Contains("PlugHub Manager - Uninstall") && maintenanceRunner.Contains("MessageBoxButton.YesNo") && maintenanceRunner.Contains("WaitForProcesses"), "Manager uninstall maintenance mode must confirm and wait for locking processes.");
            Require(managerUninstaller.Contains("PlugHub.addin") && managerUninstaller.Contains("SpecialFolder.CommonApplicationData"), "Manager uninstaller must remove the machine-wide ProgramData addin manifest.");
            Require(managerUninstaller.Contains("Directory.Delete") && managerUninstaller.Contains("Refusing to delete a drive root") && managerUninstaller.Contains("RequiredInstallMarkers") && managerUninstaller.Contains("ContainsPlugHubInstallMarkers") && managerUninstaller.Contains("IsAllowedInstallRootName") && managerUninstaller.Contains("Revit2020"), "Manager uninstaller must delete only a marker-validated PlugHub install directory or the local dist/Revit2020 test output.");
            Require(!installerProject.Contains("InstallerUninstallerExe") && !installerProject.Contains("PlugHubUninstaller.exe"), "installer project must not embed a standalone uninstaller.");
            Require(!installerPayload.Contains("PlugHub-Uninstall.exe") && !installerPayload.Contains("WriteUninstaller"), "installer payload must not write a standalone uninstaller.");
            Require(!installerForm.Contains("PlugHub-Uninstall.exe"), "installer UI must not report a standalone uninstaller.");
            Require(!githubWorkflow.Contains("Build PlugHub uninstaller") && !githubWorkflow.Contains("InstallerUninstallerExe"), "GitHub release workflow must not build or embed a standalone uninstaller.");
            Require(!readme.Contains("PlugHub-Uninstall.exe"), "README must not document a standalone uninstaller.");
        }

        private void ValidateFrameworkAutoUpdateSpecification()
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
            Require(maintenanceLogger.Contains("CandidateLogsDirectories") && maintenanceLogger.Contains("Environment.SpecialFolder.LocalApplicationData") && maintenanceLogger.Contains("Path.GetTempPath()"), "Manager maintenance logging must fall back from the install directory to user and temporary log directories.");
            Require(maintenanceLogger.Contains("Logging must not interrupt update or uninstall maintenance") && maintenanceLogger.Contains("catch"), "Manager maintenance logging failures must not interrupt update or uninstall.");
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

        private void ValidateReleaseVersioningWorkflow()
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

        private void ValidateSigningGuidance()
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

        private void ValidateRevitDeploymentConfiguration()
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

        private string ReadText(string relativePath)
        {
            return File.ReadAllText(FullPath(relativePath));
        }

        private string FullPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string RemovedSampleProject()
        {
            return "PlugHub." + "Sample" + "Module";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
