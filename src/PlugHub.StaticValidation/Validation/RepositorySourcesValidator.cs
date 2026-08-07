using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class RepositorySourcesValidator
    {
        private readonly ValidationSource _source;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public RepositorySourcesValidator(ValidationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public void Validate()
        {
            ValidatePackageSourceAndReleaseBehavior();
        }

        private void ValidatePackageSourceAndReleaseBehavior()
        {
            var modulesText = _source.ReadText("config/sources.example.json");
            var settingsWindow = _source.ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var repositorySettingsController = _source.ReadText("src/PlugHub.Manager/Settings/RepositorySettingsController.cs");
            var settingsMetrics = _source.ReadText("src/PlugHub.Manager/Settings/SettingsMetrics.cs");
            var repositoryRow = _source.ReadText("src/PlugHub.Manager/Settings/Rows/RepositoryRow.cs");
            var repositoryPackageRow = _source.ReadText("src/PlugHub.Manager/Settings/Rows/RepositoryPackageRow.cs");
            var repositoryPackageInstallState = _source.ReadText("src/PlugHub.Framework/Packages/RepositoryPackageInstallState.cs");
            var sourceResolver = _source.ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var configurationLoader = _source.ReadText("src/PlugHub.Framework/Configuration/FrameworkConfigurationLoader.cs");
            var packageRepositoryService = _source.ReadText("src/PlugHub.Framework/Packages/PackageRepositoryService.cs");
            var repositoryAddress = _source.ReadText("src/PlugHub.Framework/Packages/RepositoryAddress.cs");
            var repositoryBrowser = _source.ReadText("src/PlugHub.Framework/Packages/RepositoryBrowser.cs");
            var repositoryArchiveSynchronizer = _source.ReadText("src/PlugHub.Framework/Packages/RepositoryArchiveSynchronizer.cs");
            var repositoryRemoteTransport = _source.ReadText("src/PlugHub.Framework/Packages/RepositoryRemoteTransport.cs");
            var packageManifestReader = _source.ReadText("src/PlugHub.Framework/Packages/PackageManifestReader.cs");
            var packageInstallService = _source.ReadText("src/PlugHub.Framework/Packages/PackageInstallService.cs");
            var frameworkUpdateService = _source.ReadText("src/PlugHub.Framework/Updates/FrameworkUpdateService.cs");
            var frameworkUpdatePolicy = _source.ReadText("src/PlugHub.Framework/Updates/FrameworkUpdatePolicy.cs");
            var credentialService = _source.ReadText("src/PlugHub.Framework/Packages/RepositoryCredentialService.cs");
            var redactor = _source.ReadText("src/PlugHub.Framework/Diagnostics/SensitiveTextRedactor.cs");
            var configurationModels = _source.ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            var workflow = _source.ReadText(".github/workflows/release.yml");
            var giteeWorkflow = _source.ReadText(".github/workflows/sync-gitee.yml");
            var buildScript = _source.ReadText("scripts/build-revit2020.ps1");
            var readme = _source.ReadText("README.md");
            var sourcesSchema = _source.ReadText("config/schemas/sources.schema.json");

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
            var repositoryToolbar = _source.MethodBody(settingsWindow, "BuildRepositoryToolbar");
            Require(repositoryToolbar.Contains("一键同步") && !repositoryToolbar.Contains("浏览所选") && !repositoryToolbar.Contains("检查更新"), "repository toolbar must expose manual sync-all and remove redundant repository actions.");
            Require(!settingsWindow.Contains("LoadCachedRepositoryPackages();"), "settings must not auto-populate repository packages before the user manually syncs repositories.");
            var sourceTemplate = _source.MethodBody(settingsWindow, "BuildRepositorySourceCardTemplate");
            Require(sourceTemplate.Contains("border.SetValue(Border.WidthProperty, RepositorySourceCardWidth)") && sourceTemplate.Contains("border.SetValue(Border.MarginProperty, new Thickness(RepositoryCardHorizontalMargin, RepositoryPackageCardVerticalMargin, RepositoryCardHorizontalMargin, RepositorySourceCardBottomMargin))"), "repository source card horizontal gaps must match package card vertical gaps while preserving source-row bottom spacing.");
            var packageItemsPanel = _source.MethodBody(settingsWindow, "BuildRepositoryPackageItemsPanel");
            Require(packageItemsPanel.Contains("new FrameworkElementFactory(typeof(UniformGrid))") && packageItemsPanel.Contains("UniformGrid.ColumnsProperty") && packageItemsPanel.Contains("RepositoryPackageColumns"), "repository package list must use a fixed three-column UniformGrid so seven repository packages render as three rows instead of two columns.");
            Require(settingsWindow.Contains("_warehousePackageList.ItemContainerStyle = BuildRepositoryPackageItemContainerStyle()") && settingsWindow.Contains("RepositoryPackageItemContainerStyle"), "repository package list must remove default ListBoxItem chrome so three cards fit predictably.");
            Require(settingsWindow.Contains("_warehousePackageList.HorizontalContentAlignment = HorizontalAlignment.Center") && settingsWindow.Contains("FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center"), "repository package card grid must keep three columns centered with equal left/right spacing.");
            Require(settingsWindow.Contains("RepositoryPackageActionWidth = 72.0") && settingsWindow.Contains("RepositoryPackageActionHeight = 26.0"), "repository package card action buttons must stay compact with fixed dimensions.");
            var packageTemplate = _source.MethodBody(settingsWindow, "BuildRepositoryPackageTemplate");
            Require(settingsWindow.Contains("RepositoryPackageCardHeight") && packageTemplate.Contains("border.SetValue(FrameworkElement.HeightProperty, RepositoryPackageCardHeight)") && packageTemplate.Contains("border.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top)"), "repository package cards must keep a fixed card height after filtering instead of stretching with the UniformGrid row.");
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
            var primaryActionBackground = _source.MethodBody(settingsWindow, "RepositoryPackageActionBackground");
            var primaryActionForeground = _source.MethodBody(settingsWindow, "RepositoryPackageActionForeground");
            var primaryActionBorder = _source.MethodBody(settingsWindow, "RepositoryPackageActionBorder");
            var primaryActionStyle = _source.MethodBody(settingsWindow, "BuildRepositoryPackagePrimaryActionButton");
            var primaryActionRunner = _source.MethodBody(settingsWindow, "RunRepositoryPackagePrimaryAction");
            var uninstallActionRunner = _source.MethodBody(settingsWindow, "RunRepositoryPackageUninstallAction");
            Require(primaryActionBackground.Contains("RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.SuccessBrush") && primaryActionForeground.Contains("RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.AccentForegroundBrush"), "uninstalled repository packages must show install as white text on green.");
            Require(primaryActionBackground.Contains("RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.UpdateBrush") && primaryActionForeground.Contains("RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.AccentForegroundBrush"), "updatable repository packages must show update as white text on blue.");
            Require(primaryActionBackground.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)") && primaryActionBackground.Contains("isMouseOver") && primaryActionBackground.Contains("theme.SuccessBrush") && primaryActionForeground.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)") && primaryActionForeground.Contains("theme.AccentForegroundBrush") && primaryActionBorder.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)") && primaryActionBorder.Contains("theme.SuccessBrush"), "installed repository packages must switch the primary installed status to green background with white text on hover.");
            Require(primaryActionBackground.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.ControlBackground") && primaryActionForeground.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.TextBrush") && primaryActionBorder.Contains("RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.BorderBrush"), "installed repository packages must default to a passive installed label before hover.");
            Require(primaryActionStyle.Contains("RepositoryPackagePrimaryActionLabelBinding") && settingsWindow.Contains("RepositoryPackagePrimaryActionLabelConverter") && settingsWindow.Contains("\"重安装\""), "installed repository package primary action must change the button label to reinstall while hovered.");
            Require(primaryActionRunner.Contains("RepositoryPackageAction.Reinstall.ToString()") && primaryActionRunner.Contains("_packageRepositoryService.Update(BaseDirectory(), package)"), "clicking hovered installed package status must reinstall by reusing the package update replacement path.");
            Require(uninstallActionRunner.Contains("RepositoryPackageAction.Reinstall.ToString()"), "installed packages with reinstall as primary action must still allow the separate uninstall button.");
            var uninstallButtonStyle = _source.MethodBody(settingsWindow, "RepositoryPackageUninstallButtonStyle");
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
            Require(repositoryArchiveSynchronizer.Contains("IRepositoryRemoteTransport") && repositoryArchiveSynchronizer.Contains("_transport.Download") && repositoryArchiveSynchronizer.Contains("ZipFile.OpenRead") && repositoryArchiveSynchronizer.Contains("ExtractArchive"), "repository archive synchronization must delegate remote I/O through its transport seam and own archive extraction.");
            Require(repositoryRemoteTransport.Contains("HttpWebRequest") && repositoryRemoteTransport.Contains("curl/8.0.1") && !repositoryRemoteTransport.Contains("request.UserAgent = \"PlugHub\""), "repository HTTP transport must use a Gitee-compatible user agent accepted by archive endpoints.");
            Require(repositoryAddress.Contains("ProviderFromHost(uri.Host)") && repositoryAddress.Contains("new RepositoryAddress(hostProvider"), "absolute repository URLs must infer GitHub or Gitee from the URL host instead of failing when the provider field is stale.");
            Require(repositoryArchiveSynchronizer.Contains("ArchiveDownloadUrl(address, repository)") && repositoryArchiveSynchronizer.Contains("ShouldAppendArchiveCacheBust") && repositoryRemoteTransport.Contains("RequestCachePolicy(RequestCacheLevel.Reload)"), "repository source sync must bypass stale GitHub HTTP/archive cache without adding unsupported cache-bust query parameters to Gitee archive URLs.");
            Require(repositoryArchiveSynchronizer.Contains("HttpStatusCode.BadRequest") && repositoryArchiveSynchronizer.Contains("RepositoryRequiresToken(repository)") && repositoryArchiveSynchronizer.Contains("SyncGiteeRepositoryViaApi(address, repository, stagingDirectory)"), "public Gitee archive failures must fall back to the Gitee API file download path instead of surfacing a raw 400 response.");
            Require(!repositoryArchiveSynchronizer.Contains("Parallel.ForEach") && repositoryArchiveSynchronizer.Contains("foreach (var path in entries)") && repositoryArchiveSynchronizer.Contains("GiteeApiRetryCount") && repositoryArchiveSynchronizer.Contains("Thread.Sleep"), "Gitee API fallback must serialize content requests and use bounded rate-limit backoff instead of amplifying public API 403/429 responses.");
            Require(repositoryArchiveSynchronizer.Contains("SyncConfiguredCloudRepositoryWithMirrorFallback") && repositoryArchiveSynchronizer.Contains("CloudSyncCandidates") && repositoryArchiveSynchronizer.Contains("foreach (var candidate in candidates)") && !repositoryArchiveSynchronizer.Contains("Task.WaitAny"), "public cloud repositories must try the configured provider before its mirror rather than racing both providers and multiplying rate-limit traffic.");
            Require(repositoryArchiveSynchronizer.Contains("ValidateArchiveFile") && repositoryArchiveSynchronizer.IndexOf("ValidateArchiveFile(archivePath, archiveUrl)", StringComparison.Ordinal) < repositoryArchiveSynchronizer.IndexOf("ExtractArchive(archivePath, stagingDirectory)", StringComparison.Ordinal), "repository archive synchronizer must validate downloaded zip content before extraction.");
            Require(repositoryArchiveSynchronizer.Contains("Downloaded repository archive is not a zip file") && repositoryArchiveSynchronizer.Contains("Check repository URL, ref, and credentials"), "repository archive synchronizer must report a clear URL/ref diagnostic for non-zip responses.");
            Require(repositoryRemoteTransport.Contains("EnsureHttpsResponse(response.ResponseUri)") && repositoryRemoteTransport.IndexOf("EnsureHttpsResponse(response.ResponseUri)", StringComparison.Ordinal) < repositoryRemoteTransport.IndexOf("source.CopyTo(target)", StringComparison.Ordinal), "repository HTTP transport must reject redirects away from HTTPS before writing archive bytes.");
            Require(repositoryArchiveSynchronizer.Contains("api.github.com/repos") && repositoryArchiveSynchronizer.Contains("/zipball/"), "repository archive synchronizer must support GitHub zipball archives.");
            Require(repositoryArchiveSynchronizer.Contains("https://gitee.com/") && repositoryArchiveSynchronizer.Contains("/repository/archive/"), "repository archive synchronizer must support Gitee repository archive downloads.");
            Require(repositoryArchiveSynchronizer.Contains("access_token") && repositoryRemoteTransport.Contains("Authorization"), "repository archive synchronization must support private Gitee and GitHub repositories with tokens.");
            Require(repositoryArchiveSynchronizer.Contains("ShouldUseGiteeApiFallback") && repositoryArchiveSynchronizer.Contains("HttpStatusCode.Forbidden") && repositoryArchiveSynchronizer.Contains("SyncGiteeRepositoryViaApi") && repositoryArchiveSynchronizer.Contains("git/trees/") && repositoryArchiveSynchronizer.Contains("contents/"), "private Gitee repositories must fall back to the Gitee API when the web archive endpoint returns 403.");
            Require(repositoryArchiveSynchronizer.Contains("retryForbidden") && repositoryArchiveSynchronizer.Contains("Gitee API rate limit persisted after retries"), "only public Gitee 403 responses may be retried as rate limiting, and exhausted retries must produce an actionable diagnostic.");
            Require(repositoryArchiveSynchronizer.Contains("IsUnderDirectory") && repositoryArchiveSynchronizer.Contains("ArchiveWrapperDirectory") && repositoryArchiveSynchronizer.Contains("ExtendedPath") && repositoryArchiveSynchronizer.Contains("File.Create(ExtendedPath(destinationPath))") && repositoryArchiveSynchronizer.Contains("@\"\\\\?\\\"") && repositoryArchiveSynchronizer.Contains("@\"\\\\?\\UNC\\\"") && !repositoryArchiveSynchronizer.Contains("ExtractToFile"), "repository archive extraction must flatten provider wrappers, guard zip-slip paths, and use valid Windows extended-length path prefixes.");
            Require(!repositoryArchiveSynchronizer.Contains("Path.GetFullPath(Path.Combine(targetDirectory") && repositoryArchiveSynchronizer.Contains("Path.IsPathRooted(path) ? path : Path.GetFullPath(path)"), "repository extraction must not normalize an already rooted deep target before applying the Windows extended-length prefix.");
            Require(repositoryArchiveSynchronizer.Contains(".sync-") && repositoryArchiveSynchronizer.Contains(".archive-") && repositoryArchiveSynchronizer.Contains("Substring(0, 12)"), "repository archive synchronization must keep temporary names short enough for deep Windows package trees.");
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
            Require(repositoryArchiveSynchronizer.Contains("ResolveApiKey(repository)") && repositoryArchiveSynchronizer.Contains("SafePathSegment(address.Provider)"), "repository archive synchronizer must resolve protected credentials and use a safe short provider key for staging paths.");
            Require(repositoryArchiveSynchronizer.Contains("DownloadArchive") && repositoryArchiveSynchronizer.Contains("ReplaceCacheDirectory"), "repository archive synchronizer must atomically replace the local repository cache after a successful download.");
            Require(settingsWindow.Contains("已同步最快云端镜像") && settingsWindow.Contains("已读取本地文件夹"), "repository source sync status must distinguish cloud mirror sync from local folder reads.");
            Require(readme.Contains("不需要安装 Git") && readme.Contains("HTTP archive"), "README must state that repository browsing no longer requires user-installed Git.");
            Require(frameworkUpdateService.Contains("PLUGHUB_TEST_UPDATE_RELEASE_URI") && frameworkUpdateService.Contains("GitHub Test") && frameworkUpdateService.Contains("ContinueWhenNoUpdate") && frameworkUpdateService.Contains("GitHubTestPrereleaseList") && frameworkUpdateService.Contains("GetLatestTestPrerelease"), "framework update checks must support a latest-TV test update source without changing stable defaults.");
            Require(frameworkUpdateService.Contains("FrameworkUpdatePolicy.BuildCheckSources(currentVersion, _updateSources)") && frameworkUpdatePolicy.Contains("GitHubReleaseListUri"), "TV builds must delegate prerelease source ordering to FrameworkUpdatePolicy.");
            Require(frameworkUpdatePolicy.Contains("ComparableVersionText") && frameworkUpdatePolicy.Contains("IndexOfAny") && frameworkUpdatePolicy.Contains("IsStableReleaseTag") && frameworkUpdatePolicy.Contains("IsTestReleaseTag") && !frameworkUpdateService.Contains("ComparableVersionText"), "framework update version comparison must live only in FrameworkUpdatePolicy.");
            Require(sourcesSchema.Contains("\"encryptedApiKey\"") && sourcesSchema.Contains("\"apiKeyProtection\""), "sources schema must describe encrypted repository credential fields persisted by the configuration model.");
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
            var pendingStore = _source.ReadText("src/PlugHub.Framework/Packages/PendingPackageOperationStore.cs");
            Require(pendingStore.Contains("pending-operations.json"), "pending operation store must own the pending operation file name.");
            Require(pendingStore.Contains("AddOrReplace") && pendingStore.Contains("Remove") && pendingStore.Contains("Read"), "pending operation store must read, add, and remove operations.");
            Require(repositoryPackageRow.Contains("PendingOperation") && repositoryPackageRow.Contains("RepositoryPackageInstallState.Resolve") && repositoryPackageInstallState.Contains("已安装待重启") && repositoryPackageInstallState.Contains("isRevitHostRunning && !isInstalled && isLoadedInCurrentRuntime") && repositoryPackageInstallState.Contains("isRevitHostRunning && !isLoadedInCurrentRuntime") && settingsWindow.Contains("IsLoadedInCurrentRuntime"), "repository package status must distinguish installed, uninstalled, and pending-restart states without treating absent Revit as a loaded runtime.");
            var frameworkRuntime = _source.ReadText("src/PlugHub.Framework/Runtime/FrameworkRuntime.cs");
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
            Require(giteeWorkflow.Contains("git@gitee.com:GaoMengGu/PlugHub.git") && giteeWorkflow.Contains("refs/heads/main") && giteeWorkflow.Contains("push_with_retry +HEAD:main"), "Gitee sync workflow must mirror main to GaoMengGu/PlugHub on Gitee with GitHub as source of truth.");
            Require(giteeWorkflow.Contains("push_with_retry()") && giteeWorkflow.Contains("git push gitee \"$refspec\""), "Gitee sync workflow must push mirrored refs through the retry helper.");
            Require(giteeWorkflow.Contains("refs/tags/") && giteeWorkflow.Contains("push_with_retry \"+refs/tags/$tag:refs/tags/$tag\""), "Gitee sync workflow must mirror GitHub release tags to Gitee before release.yml mirrors release assets.");
            Require(buildScript.Contains("[switch]$UseRelativeAddinAssembly") && buildScript.Contains("PlugHub.Revit2020.dll"), "build script must support relative release addin assembly paths.");
            Require(workflow.Contains("*.pdb") && workflow.Contains("*.sigstore.json") && !workflow.Contains("Compress-Archive -Path \"dist\\Revit2020\\*\""), "release zip must exclude pdb and sigstore files.");

            Require(readme.Contains("个人使用") && readme.Contains("不得商用"), "README must state the non-commercial personal-use license restriction.");
        }

        private void ValidateRepositoryCredentialAndRedactionBehavior()
        {
            var credentialService = new PlugHub.Framework.Packages.RepositoryCredentialService();
            var repository = new PlugHub.Framework.Configuration.PackageRepositoryConfiguration
            {
                ApiKey = "secret-token"
            };

            credentialService.ProtectForSave(repository);
            Require(string.IsNullOrWhiteSpace(repository.ApiKey), "protecting repository credentials must clear plaintext apiKey.");
            Require(!string.IsNullOrWhiteSpace(repository.EncryptedApiKey), "protecting repository credentials must persist encrypted apiKey.");
            Require(!_json.Serialize(repository).Contains("secret-token"), "serialized repository configuration must not retain plaintext apiKey after protection.");
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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
