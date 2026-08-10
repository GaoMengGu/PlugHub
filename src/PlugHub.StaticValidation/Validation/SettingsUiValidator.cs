using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class SettingsUiValidator
    {
        private readonly ValidationSource _source;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public SettingsUiValidator(ValidationSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public void Validate()
        {
            ValidateSettingsPaneV21Specification();
            ValidateFrameworkSettingsWindowSectionBoundaries();
            ValidateSettingsRibbonCleanupSpecification();
            ValidateBuiltinOnlySpecification();
            ValidateSettingsCreationAndSortingSpecification();
            ValidateDefaultIconSpecification();
            ValidateRasterBrandIconSpecification();
            ValidateRevitWpfUiDesignSpecification();
            ValidateSettingsGroupFeatureEditingBehavior();
        }

        private void ValidateSettingsPaneV21Specification()
        {
            var settingsWindow = _source.ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var settingsViewModel = _source.ReadText("src/PlugHub.Manager/Settings/FrameworkSettingsViewModel.cs");
            var settingsCommand = _source.ReadText("src/PlugHub.Revit2020/FrameworkSettingsCommand.cs");
            var externalSettingsLauncher = _source.ReadText("src/PlugHub.Revit2020/ExternalManagerLauncher.cs");
            var externalSettingsProject = _source.ReadText("src/PlugHub.Manager/PlugHub.Manager.csproj");
            var settingsAppProgram = _source.ReadText("src/PlugHub.Manager/Program.cs");
            var settingsStore = _source.ReadText("src/PlugHub.Framework/Settings/SettingsConfigurationStore.cs");
            var repositoryPackageRow = _source.ReadText("src/PlugHub.Manager/Settings/Rows/RepositoryPackageRow.cs");
            var repositoryPackageInstallState = _source.ReadText("src/PlugHub.Framework/Packages/RepositoryPackageInstallState.cs");
            var statusWindow = _source.ReadText("src/PlugHub.Wpf/FrameworkStatusWindow.cs");
            var featureCommand = _source.ReadText("src/PlugHub.Revit2020/FrameworkFeatureCommand.cs");
            var ribbonBuilder = _source.ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var runtime = _source.ReadText("src/PlugHub.Framework/Runtime/FrameworkRuntime.cs");
            var ribbonDesignerDropService = _source.ReadText("src/PlugHub.Framework/RibbonEditing/RibbonDesignerDropService.cs");
            var ribbonLayoutEditor = _source.ReadText("src/PlugHub.Framework/RibbonEditing/RibbonLayoutEditor.cs");
            var sourceResolver = _source.ReadText("src/PlugHub.Framework/Sources/ModuleSourceResolver.cs");
            var configurationModels = _source.ReadText("src/PlugHub.Framework/Configuration/ConfigurationModels.cs");
            var revitProject = _source.ReadText("src/PlugHub.Revit2020/PlugHub.Revit2020.csproj");

            Require(!File.Exists(_source.FullPath("src/PlugHub.Revit2020/FrameworkSettingsForm.cs")), "legacy WinForms settings form must be removed.");
            Require(!File.Exists(_source.FullPath("src/PlugHub.Revit2020/FrameworkSettingsPane.cs")), "legacy DockablePane settings provider must be removed.");
            Require(!_source.ReadAllCSharp("src/PlugHub.Revit2020").Contains("System.Windows.Forms") && !_source.ReadAllCSharp("src/PlugHub.Revit2020").Contains("WindowsFormsHost"), "Revit settings/feature UI must not reference WinForms hosting.");
            Require(settingsCommand.Contains("ExternalManagerLauncher") && settingsCommand.Contains("TryLaunch") && settingsCommand.Contains("FrameworkStatusWindow"), "settings ribbon command must launch the PlugHub Manager and report failures through WPF status.");
            Require(!settingsCommand.Contains("FrameworkSettingsWindow") && !settingsCommand.Contains("ShowDialog") && !settingsCommand.Contains("FrameworkConfigurationLoader.LoadFromDirectory"), "Revit settings ribbon command must not host the full settings window or load editable settings in-process.");
            Require(!File.Exists(_source.FullPath("src/PlugHub.Revit2020/FrameworkExternalSettingsCommand.cs")), "parallel Windows settings command must be removed after settings becomes the external-app entry.");
            Require(!settingsCommand.Contains("GetDockablePane") && !settingsCommand.Contains("pane.Hide") && !settingsCommand.Contains("pane.Show"), "settings command must not toggle a DockablePane.");
            Require(externalSettingsLauncher.Contains("PlugHub.Manager.exe") && externalSettingsLauncher.Contains("--config") && externalSettingsLauncher.Contains("--hostProcessId") && externalSettingsLauncher.Contains("FrameworkRuntimeState.BaseDirectory"), "external settings launcher must locate PlugHub.Manager.exe and pass the runtime config directory plus Revit host process id.");
            Require(externalSettingsProject.Contains("<TargetFramework>net48</TargetFramework>") && externalSettingsProject.Contains("<OutputType>WinExe</OutputType>") && externalSettingsProject.Contains("PresentationFramework"), "PlugHub Manager must be a net48 WPF Windows executable.");
            Require(externalSettingsProject.Contains("<AssemblyName>PlugHub.Manager</AssemblyName>") && !externalSettingsProject.Contains("ProjectReference Include=\"..\\PlugHub.Revit2020") && !externalSettingsProject.Contains("Autodesk.Revit"), "PlugHub Manager must be the WPF manager executable without depending on the Revit adapter project or Revit API.");
            Require(!externalSettingsProject.Contains("PlugHub.SettingsApp") && !externalSettingsProject.Contains("PlugHub.SettingsUi") && !externalSettingsProject.Contains("SharedSettings"), "PlugHub Manager project must not keep legacy SettingsApp/SettingsUi naming or linked Revit settings sources.");
            Require(settingsAppProgram.Contains("ManagerMaintenanceArguments.Parse") && settingsAppProgram.Contains("ManagerMaintenanceRunner") && settingsAppProgram.Contains("FrameworkSettingsWindow") && settingsAppProgram.Contains("new FrameworkRuntime().Load(configDirectory, ShouldApplyPendingPackageOperations(hostProcessId))") && !File.Exists(_source.FullPath("src/PlugHub.Manager/SettingsMainWindow.cs")), "PlugHub Manager must route maintenance mode before loading a local runtime snapshot and hosting the existing FrameworkSettingsWindow.");
            Require(settingsAppProgram.Contains("ShouldApplyPendingPackageOperations") && settingsAppProgram.Contains("Process.GetProcessById(hostProcessId)"), "PlugHub Manager must only apply pending package operations when the associated Revit host is not running.");
            Require(settingsAppProgram.Contains("TryAcquireSingleInstance") && settingsAppProgram.Contains("new Mutex(true") && settingsAppProgram.Contains("SingleInstanceMutexName(configDirectory)"), "PlugHub Manager normal mode must be single-instance per configuration directory so settings ribbon clicks and EXE launches do not open duplicate managers.");
            Require(settingsWindow.Contains("var isRevitHostRunning = IsRevitHostProcessRunning();") && settingsWindow.Contains("!IsRevitHostProcessRunning()) return false") && repositoryPackageInstallState.Contains("bool isRevitHostRunning") && repositoryPackageInstallState.Contains("bool isLoadedInCurrentRuntime") && repositoryPackageRow.Contains("RepositoryPackageInstallState.Resolve"), "external Manager must separate Revit host liveness from runtime-loaded package state through a WPF-independent rule.");
            Require(settingsStore.Contains("namespace PlugHub.Framework.Settings") && settingsStore.Contains("public sealed class SettingsConfigurationStore") && !settingsStore.Contains("Autodesk.Revit"), "shared settings store must live in PlugHub.Framework.Settings and stay Revit-independent.");
            Require(featureCommand.Contains("ShowRuntimeStatus"), "status command must use the focused runtime status view.");
            Require(featureCommand.Contains("FrameworkStatusWindow") && !featureCommand.Contains("TaskDialog.Show"), "framework fallback feature feedback must use WPF.");
            Require(ribbonBuilder.Contains("LoadFeatureIcon") && ribbonBuilder.Contains("LargeImage"), "configured feature icons must be applied to Revit ribbon buttons.");
            Require(ribbonBuilder.Contains("FrameworkSettingsCommand"), "framework Ribbon panel must expose settings command.");
            Require(!ribbonBuilder.Contains("FrameworkStatusCommand") && !ribbonBuilder.Contains("PlugHub_Framework_Status") && !ribbonBuilder.Contains("\"状态\""), "framework Ribbon panel must not expose a status button; Revit keeps only the settings light entry.");
            Require(!ribbonBuilder.Contains("FrameworkExternalSettingsCommand") && !ribbonBuilder.Contains("PlugHub_Framework_ExternalSettings") && !ribbonBuilder.Contains("Windows设置"), "framework Ribbon panel must not expose a duplicate Windows settings entry.");
            Require(!runtime.Contains(".Browse(") && !runtime.Contains("RepositoryBrowser") && !runtime.Contains("RepositoryArchiveSynchronizer") && !runtime.Contains("FrameworkUpdateService"), "Revit startup runtime must not access remote repositories or framework update services.");

            foreach (var token in new[] { "class FrameworkSettingsWindow", ": Window", "TabControl", "BuildRibbonLayoutTab", "BuildRepositoriesTab", "RepositoryRow", "RepositoryPackageRow", "GroupRow", "ReloadFromDisk", "ContextMenu", "DragDrop" })
            {
                Require(settingsWindow.Contains(token), "WPF settings UI token missing: " + token);
            }
            foreach (var removedGrid in new[] { "_pluginPackagesGrid", "_featuresGrid", "_groupsGrid", "_diagnosticsGrid", "AttachGridBehaviors", "GridDrop", "SafeRefreshGrid", "EndGridEdits" })
            {
                Require(!settingsWindow.Contains(removedGrid), "settings window must not keep the removed hidden DataGrid implementation: " + removedGrid);
            }
            foreach (var removedRow in new[] { "ModuleRow.cs", "PendingPackageOperationRow.cs", "DiagnosticRow.cs", "RibbonFeaturePoolRow.cs" })
            {
                Require(!File.Exists(_source.FullPath(Path.Combine("src/PlugHub.Manager/Settings/Rows", removedRow))), "removed hidden-grid row must not return: " + removedRow);
            }

            Require(settingsWindow.Contains("BuildRibbonLayoutTab"), "settings window must expose a Ribbon layout tab.");
            Require(settingsWindow.Contains("LoadRibbonLayoutRows"), "settings window must load ribbon layout rows.");
            Require(settingsWindow.Contains("ApplyRibbonLayoutRows"), "settings window must save ribbon layout rows.");
            Require(settingsWindow.Contains("ResetDefaultRibbonLayout"), "settings window must reset to the framework default layout.");
            foreach (var editingFile in new[] { "RibbonDesignerNodeRow.cs", "RibbonDesignerFeatureRow.cs", "RibbonDesignerMapper.cs", "RibbonDesignerDropService.cs", "RibbonLayoutDiffService.cs", "RibbonLayoutEditor.cs" })
            {
                Require(File.Exists(_source.FullPath(Path.Combine("src/PlugHub.Framework/RibbonEditing", editingFile))), "pure Ribbon editing file must live in Framework: " + editingFile);
                Require(!File.Exists(_source.FullPath(Path.Combine("src/PlugHub.Manager/Settings/RibbonDesigner", editingFile))), "Manager must not own pure Ribbon editing implementation: " + editingFile);
            }
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
            Require(settingsWindow.Contains("_ribbonLayoutEditor.Synchronize") && ribbonLayoutEditor.Contains("EnsureAllVisibleFeatures"), "visual designer must automatically place all visible installed features through the editing seam.");
            Require(settingsWindow.Contains("_ribbonLayoutEditor.RemoveContainer") && ribbonLayoutEditor.Contains("EnsureDefaultPanel"), "removing a layout container must atomically return contained features to the 默认 panel through the editing seam.");
            Require(settingsWindow.Contains("ResolveRibbonDesignerDropPlan"), "visual designer must move existing canvas items directly between containers.");
            Require(settingsWindow.Contains("InsertRibbonDesignerNode"), "visual designer must insert moved or new items at the resolved drop position.");
            Require(!settingsWindow.Contains("RibbonDesignerFeatureListDrop") && !settingsWindow.Contains("RemoveRibbonDesignerFeatureFromCanvas"), "visual designer must not remove functions by dragging them to a separate feature list.");
            Require(!settingsWindow.Contains("BuildRibbonDesignerIconSelector") && !settingsWindow.Contains("RibbonDesignerIconOptions"), "layout page must not expose custom button icon editing.");
            Require(!settingsWindow.Contains("RibbonDesignerBrowseIconAction") && !settingsWindow.Contains("RibbonDesignerClearIconAction"), "layout page must not expose browse or clear icon actions.");
            Require(!settingsWindow.Contains("BuildRibbonDesignerPropertyField(\"图标\""), "layout property panel must not show an icon field.");
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
            Require(!settingsWindow.Contains("CanEditRibbonDesignerIcon") && !settingsWindow.Contains("_selectedRibbonDesignerIcon"), "visual designer properties must not edit button icons from the layout page.");
            Require(settingsWindow.Contains("_ribbonLayoutEditor.PrepareForSave") && ribbonLayoutEditor.Contains("NormalizeStacks"), "settings save must remove empty stacks and unwrap single-button stacks through the editing seam.");
            Require(ribbonLayoutEditor.Contains("FindNestedStack") && ribbonLayoutEditor.Contains("堆叠控件不能嵌套堆叠"), "settings save must reject nested stacks through the editing seam.");
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
            Require(ribbonLayoutEditor.Contains("布局中存在重复功能"), "settings save must validate unique feature placement through the editing seam.");
            Require(ribbonLayoutEditor.Contains("MergePanelsByDisplayName"), "settings must merge same-name layout panels before showing the canvas.");
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
            Require(settingsWindow.Contains("_viewModel") && !settingsWindow.Contains("ObservableCollection<FeatureRow> _featureRows") && !settingsWindow.Contains("ObservableCollection<GroupRow> _groupRows"), "FrameworkSettingsWindow row state must be held by FrameworkSettingsViewModel.");
            Require(!settingsWindow.Contains("private sealed class ModuleManifestDocument"), "FrameworkSettingsWindow must not keep a stale private ModuleManifestDocument type.");
            foreach (var collection in new[] { "Features", "Groups", "Repositories", "RepositoryPackages" })
            {
                Require(settingsViewModel.Contains("ObservableCollection") && settingsViewModel.Contains(collection), "FrameworkSettingsViewModel must expose " + collection + ".");
            }
            foreach (var removedCollection in new[] { "ObservableCollection<ModuleRow>", "ObservableCollection<PendingPackageOperationRow>", "ObservableCollection<DiagnosticRow>" })
            {
                Require(!settingsViewModel.Contains(removedCollection), "FrameworkSettingsViewModel must not keep hidden-grid-only state: " + removedCollection);
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

        private void ValidateFrameworkSettingsWindowSectionBoundaries()
        {
            var settingsWindow = _source.ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");

            var repositoriesTab = _source.MethodBody(settingsWindow, "BuildRepositoriesTab");
            foreach (var token in new[] { "BuildRepositoryToolbar", "BuildRepositorySourceCards", "BuildRepositoryPackageToolbar", "BuildRepositoryPackageList", "Grid.SetRow" })
            {
                Require(repositoriesTab.Contains(token), "repositories tab must own its section composition: " + token);
            }

            var repositoryToolbar = _source.MethodBody(settingsWindow, "BuildRepositoryToolbar");
            foreach (var token in new[] { "CheckRepositoryUpdates", "AddRepository", "BuildToolbarHeader" })
            {
                Require(repositoryToolbar.Contains(token), "repository toolbar must expose repository source actions: " + token);
            }

            var repositorySources = _source.MethodBody(settingsWindow, "BuildRepositorySourceCards");
            foreach (var token in new[] { "_repositorySourcesList", "BuildRepositoryMenu", "BuildRepositorySourceCardTemplate", "BuildRepositorySourceScrollViewer" })
            {
                Require(repositorySources.Contains(token), "repository source cards must keep source list controls together: " + token);
            }

            var packageToolbar = _source.MethodBody(settingsWindow, "BuildRepositoryPackageToolbar");
            foreach (var token in new[] { "_repositoryPackageSearchText", "_repositoryPackageStateFilter", "_repositoryPackageTagFilter", "RepositoryPackageFilterChanged" })
            {
                Require(packageToolbar.Contains(token), "repository package toolbar must keep package filters together: " + token);
            }

            var packageList = _source.MethodBody(settingsWindow, "BuildRepositoryPackageList");
            foreach (var token in new[] { "_warehousePackageList", "BuildRepositoryPackageMenu", "BuildRepositoryPackageTemplate", "BuildRepositoryPackageItemsPanel" })
            {
                Require(packageList.Contains(token), "repository package list must own package browsing controls: " + token);
            }

            var designerTab = _source.MethodBody(settingsWindow, "BuildVisualRibbonDesignerTab");
            Require(designerTab.Contains("BuildRibbonDesignerEditorBody") && designerTab.Contains("SyncSelectedRibbonDesignerEditor"), "visual ribbon designer tab must build and sync the editor.");

            var designerBody = _source.MethodBody(settingsWindow, "BuildRibbonDesignerEditorBody");
            Require(designerBody.Contains("BuildRibbonDesignerCanvas") && designerBody.Contains("BuildRibbonDesignerPropertyPanel"), "visual ribbon designer body must keep canvas and property editor as distinct sections.");

            var designerCanvas = _source.MethodBody(settingsWindow, "BuildRibbonDesignerCanvas");
            Require(designerCanvas.Contains("_ribbonDesignerCanvas") && designerCanvas.Contains("BuildRibbonDesignerCanvasMenu") && designerCanvas.Contains("ScrollViewer"), "visual ribbon designer canvas must expose a scrollable canvas with its context menu.");

            var designerMenu = _source.MethodBody(settingsWindow, "BuildRibbonDesignerCanvasMenu");
            foreach (var token in new[] { "RibbonDesignerNodeRow.Panel", "RibbonDesignerNodeRow.PulldownButton", "RibbonDesignerNodeRow.SplitButton", "RibbonDesignerNodeRow.Stack", "RemoveSelectedRibbonDesignerNode", "ResetDefaultRibbonLayout" })
            {
                Require(designerMenu.Contains(token), "visual ribbon designer context menu must keep layout operations discoverable: " + token);
            }

            var designerProperties = _source.MethodBody(settingsWindow, "BuildRibbonDesignerPropertyPanel");
            foreach (var token in new[] { "_selectedRibbonDesignerText", "_selectedRibbonDesignerType", "_selectedRibbonDesignerDefaultFeature", "SelectedRibbonDesignerPropertySelectionChanged" })
            {
                Require(designerProperties.Contains(token), "visual ribbon designer property panel must keep selected-node editors together: " + token);
            }
            Require(!designerProperties.Contains("BuildRibbonDesignerPropertyField(\"图标\""), "visual ribbon designer property panel must not expose layout-level icon editing.");

            var aboutTab = _source.MethodBody(settingsWindow, "BuildAboutTab");
            foreach (var token in new[] { "BuildAboutLeftPanel", "BuildAboutAssetPanel", "BuildAboutPathPanel", "BuildAboutDiagnosticsPanel", "ListPendingOperations", "Grid" })
            {
                Require(aboutTab.Contains(token), "about tab must keep framework metadata and diagnostics together: " + token);
            }
            Require(!aboutTab.Contains("ScrollViewer"), "about tab must stay on one page without an overall scrollbar.");

            var aboutLeftPanel = _source.MethodBody(settingsWindow, "BuildAboutLeftPanel");
            foreach (var token in new[] { "BuildAboutHeader", "BuildDonationCodes", "反馈邮箱", "交流群号" })
            {
                Require(aboutLeftPanel.Contains(token), "about tab left panel must keep brand, contact, and donation content together: " + token);
            }

            var aboutHeader = _source.MethodBody(settingsWindow, "BuildAboutHeader");
            Require(aboutHeader.Contains("AssemblyVersionText") && aboutHeader.Contains("CreateIconButton(\"refresh\"") && aboutHeader.Contains("CheckFrameworkUpdate"), "about header must expose framework version and the compact update action.");

            var checkUpdate = _source.MethodBody(settingsWindow, "CheckFrameworkUpdate");
            foreach (var token in new[] { "_frameworkUpdateService.Check", "AssemblyVersionText", "ShowFrameworkUpdateDialog", "UpdateFramework", "_checkFrameworkIconButton.IsEnabled" })
            {
                Require(checkUpdate.Contains(token), "check-update flow must keep metadata query, prompt, and button state together: " + token);
            }

            var updateFramework = _source.MethodBody(settingsWindow, "UpdateFramework");
            Require(updateFramework.Contains("_frameworkUpdateService.Download") && updateFramework.Contains("ManagerMaintenanceLauncher.StartUpdate") && updateFramework.Contains("MaintenanceWaitProcessIds"), "update flow must download and hand off to PlugHub Manager maintenance mode.");

            var updateDialog = _source.MethodBody(settingsWindow, "ShowFrameworkUpdateDialog");
            Require(updateDialog.Contains("ReleaseNotesText") && updateDialog.Contains("LatestVersion") && updateDialog.Contains("DialogResult"), "update dialog must show target version, release notes, and return a user decision.");
        }

        private void ValidateSettingsRibbonCleanupSpecification()
        {
            var settingsWindow = _source.ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var settingsStore = _source.ReadText("src/PlugHub.Framework/Settings/SettingsConfigurationStore.cs");
            var ribbonLayoutEditor = _source.ReadText("src/PlugHub.Framework/RibbonEditing/RibbonLayoutEditor.cs");
            var ribbonBuilder = _source.ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var addinTemplate = _source.ReadText("manifests/PlugHub.addin.template");
            var buildProps = _source.ReadText("build/Directory.Build.props");
            var views = _source.ReadObject("config/views.example.json");

            Require(settingsWindow.Contains("LoadModuleDocuments") && !settingsWindow.Contains(RemovedSamplesDirectory()), "settings must not reference removed sample module manifests.");
            Require(settingsWindow.Contains("FeatureIdsForModule(") && settingsWindow.Contains("_ribbonLayoutEditor.RemoveFeatures(_configuration.Views, removedModuleFeatureIds)") && ribbonLayoutEditor.Contains("foreach (var view in views.Views") && ribbonLayoutEditor.Contains("RemoveConfiguredFeatures"), "successful package uninstall must remove the module's features from every workspace layout.");
            Require(settingsWindow.Split(new[] { "_packageRepositoryService.Uninstall(BaseDirectory(), package), true" }, StringSplitOptions.None).Length == 3, "both package uninstall entry points must enable persistent layout cleanup.");
            Require(settingsWindow.Contains("重启 Revit 后工具栏更新"), "package uninstall status must explain that the current Revit Ribbon updates after restart.");
            Require(settingsStore.Contains("Save(") && settingsStore.Contains("ModuleManifestDocument"), "settings must save edits back to their owning module manifest through SettingsConfigurationStore.");
            Require(settingsStore.Contains("public void SaveViews(ViewsConfiguration views)") && settingsWindow.Contains("_configurationStore.SaveViews(_configuration.Views)"), "package uninstall must persist only the cleaned views.json without rewriting unrelated manifests.");
            Require(!settingsStore.Contains("Save(configuration, LoadModuleDocuments(configuration))"), "SettingsConfigurationStore must not expose a Save overload that reloads module documents from disk.");
            Require(settingsStore.Contains("ValidateOwnedDocuments(documents)") && settingsStore.Contains("foreach (var document in documents)") && settingsStore.Contains("SaveModuleDocument(document)"), "SettingsConfigurationStore Save must validate ownership before persisting the provided module documents.");
            Require(settingsStore.Contains("_loadedManifestPaths") && settingsStore.Contains("was not loaded by this store") && settingsStore.IndexOf("ValidateOwnedDocuments(documents)", StringComparison.Ordinal) < settingsStore.IndexOf("Directory.CreateDirectory(ConfigDirectory)", StringComparison.Ordinal), "SettingsConfigurationStore must reject unowned manifest paths before any configuration write begins.");
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

        private void ValidateBuiltinOnlySpecification()
        {
            var modules = AllModules().ToList();
            var allText = _source.ReadProductionCSharp() + "\n" + _source.ReadText("PlugHub.sln") + "\n" + _source.ReadText("PlugHub.slnx") + "\n" + _source.ReadText("config/sources.example.json") + "\n" + _source.ReadText("config/views.example.json");

            Require(modules.Count == 0, "framework runtime configuration must expose no bundled modules.");
            Require(modules.SelectMany(Features).Count() == 0, "framework runtime configuration must expose no bundled features.");
            Require(!Directory.Exists(_source.FullPath("src/" + RemovedSampleProject())), "sample module project must be removed.");
            Require(!Directory.Exists(_source.FullPath(RemovedSamplesDirectory())), "sample module manifests must be removed.");
            foreach (var forbidden in RemovedContentTokens())
            {
                Require(!allText.Contains(forbidden), "removed module content must be absent: " + forbidden);
            }
        }

        private void ValidateSettingsCreationAndSortingSpecification()
        {
            var settingsWindow = _source.ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");

            foreach (var token in new[] { "BuildVisualRibbonDesignerTab", "RibbonDesignerTabs", "BuildRibbonDesignerEditorBody", "BuildRibbonDesignerCanvas", "BuildRibbonDesignerPropertyPanel", "RefreshRibbonDesignerChangeSummary", "ResetDefaultRibbonLayout", "_ribbonLayoutEditor.PrepareForSave", "ApplyRibbonLayoutRows", "RefreshRibbonDesignerCanvas" })
            {
                Require(settingsWindow.Contains(token), "settings must manage Ribbon layout from the layout tab: " + token);
            }

            foreach (var forbidden in new[] { "新建模块", "新建功能", "private void AddModule(", "private void AddFeature(", "CreateModule(", "CreateFeature(", "所属模块", "ModuleIdsForFeatureRows" })
            {
                Require(!settingsWindow.Contains(forbidden), "settings must not create placeholder modules/features or expose module placement: " + forbidden);
            }

            Require(!settingsWindow.Contains("BuildTab(\"功能\"") && !settingsWindow.Contains("BuildTab(\"分组\""), "settings must not expose separate feature or group tabs.");
            foreach (var rowClass in new[] { "FeatureRow", "GroupRow", "RepositoryRow", "RepositoryPackageRow" })
            {
                Require(!settingsWindow.Contains("private sealed class " + rowClass), rowClass + " must be extracted from FrameworkSettingsWindow.");
            }

            Require(settingsWindow.Contains("PendingPackageOperationsStatusText"), "settings window must report pending package operations through the footer status text.");
            Require(!settingsWindow.Contains("BuildPendingPackageOperationsSummary"), "settings window must not show a dedicated pending restart operation list.");
            Require(!settingsWindow.Contains("CancelSelectedPendingPackageOperation"), "settings window must not expose pending operation cancellation in the settings UI.");
            Require(settingsWindow.Contains("ListPendingOperations(BaseDirectory())"), "settings window must still read pending package operations for status reminders.");
            var packageOperationStart = settingsWindow.IndexOf("private void RunRepositoryPackageOperation(", StringComparison.Ordinal);
            var packageOperationResult = packageOperationStart < 0 ? -1 : settingsWindow.IndexOf("var result = operation(row.ToDescriptor());", packageOperationStart, StringComparison.Ordinal);
            var packageOperationStatus = packageOperationResult < 0 ? -1 : settingsWindow.IndexOf("RefreshStatusWithPendingPackageOperations(statusMessage)", packageOperationResult, StringComparison.Ordinal);
            Require(packageOperationStart >= 0 && packageOperationResult >= 0 && packageOperationStatus > packageOperationResult, "repository package operations must refresh footer pending-operation status with the persisted layout cleanup result.");

            var ribbonDesignerMapper = _source.ReadText("src/PlugHub.Framework/RibbonEditing/RibbonDesignerMapper.cs");
            Require(!settingsWindow.Contains("body.Children.Add(BuildRibbonDesignerPreviewButton(tab"), "layout canvas must not render a synthetic PlugHub tab button above panels.");
            Require(ribbonDesignerMapper.Contains("GroupBy(DefaultPanelKey") && ribbonDesignerMapper.Contains("GroupDisplayText") && ribbonDesignerMapper.Contains("ModuleName"), "layout designer default panels must match runtime grouped ribbon layout.");
            Require(ribbonDesignerMapper.Contains("featuresById") && ribbonDesignerMapper.Contains("IconPathForDisplay") && ribbonDesignerMapper.Contains("feature.IconPath"), "layout designer configured rows must hydrate current package feature icons.");
        }

        private void ValidateDefaultIconSpecification()
        {
            var ribbonBuilder = _source.ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var iconProvider = _source.ReadText("src/PlugHub.Wpf/DefaultRibbonIconProvider.cs");
            var settingsWindow = _source.ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var modulesText = _source.ReadText("config/sources.example.json");

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
            Require(!settingsWindow.Contains("BuildBuiltinIconMenu") && !settingsWindow.Contains("SetSelectedFeatureBuiltinIcon"), "visual Ribbon settings must not keep the removed hidden-grid feature icon menu.");
            Require(!modulesText.Contains("commandAssembly"), "framework config must not ship command-backed feature entries.");
        }

        private void ValidateRasterBrandIconSpecification()
        {
            var iconProvider = _source.ReadText("src/PlugHub.Wpf/DefaultRibbonIconProvider.cs");
            var wpfProject = _source.ReadText("src/PlugHub.Wpf/PlugHub.Wpf.csproj");
            var managerProject = _source.ReadText("src/PlugHub.Manager/PlugHub.Manager.csproj");
            var installerProject = _source.ReadText("src/PlugHub.Installer/PlugHub.Installer.csproj");
            var installerForm = _source.ReadText("src/PlugHub.Installer/InstallerForm.cs");
            var settingsWindow = _source.ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");

            foreach (var rootIcon in new[] { "SETTINGS.png", "LOGO.png", "LOGO.ico" })
            {
                Require(!File.Exists(_source.FullPath(rootIcon)), "brand icon assets must not remain at the repository root: " + rootIcon);
            }

            foreach (var wpfResource in new[] { "src/PlugHub.Wpf/Resources/SETTINGS.png", "src/PlugHub.Wpf/Resources/LOGO.png" })
            {
                Require(File.Exists(_source.FullPath(wpfResource)), "shared WPF icon resource is missing: " + wpfResource);
            }

            Require(File.Exists(_source.FullPath("src/PlugHub.Manager/Resources/LOGO.ico")), "Manager executable icon resource is missing.");
            Require(File.Exists(_source.FullPath("src/PlugHub.Installer/Resources/LOGO.ico")), "Installer executable icon resource is missing.");
            Require(wpfProject.Contains("Resources\\SETTINGS.png") && wpfProject.Contains("Resources\\LOGO.png"), "PlugHub.Wpf must embed the raster settings and logo PNG resources.");
            Require(managerProject.Contains("<ApplicationIcon>Resources\\LOGO.ico</ApplicationIcon>"), "PlugHub.Manager must use the logo ICO as its executable icon.");
            Require(managerProject.Contains("Resources\\LOGO.ico"), "PlugHub.Manager project must include the logo ICO resource.");
            Require(installerProject.Contains("<ApplicationIcon>Resources\\LOGO.ico</ApplicationIcon>"), "PlugHub.Installer must use the logo ICO as its executable icon.");
            Require(installerForm.Contains("Icon =") && installerForm.Contains("Application.ExecutablePath"), "PlugHub installer window must apply the executable logo icon to the form.");
            Require(iconProvider.Contains("SettingsResourcePath") && iconProvider.Contains("LogoResourcePath") && iconProvider.Contains("CreateRasterIcon"), "default ribbon icon provider must load the supplied settings and logo raster resources.");
            Require(iconProvider.Contains("CreatePaddedRasterIcon") && iconProvider.Contains("Brushes.Transparent") && iconProvider.Contains("size - padding * 2"), "raster ribbon icons must render inside a fixed transparent canvas with safe padding so Revit does not clip the supplied artwork.");
            Require(settingsWindow.Contains("DefaultRibbonIconProvider.CreateLogoIcon") && settingsWindow.Contains("BuildHeaderLogo"), "PlugHub Manager must apply the logo to the window and header.");
        }

        private void ValidateRevitWpfUiDesignSpecification()
        {
            var theme = _source.ReadText("src/PlugHub.Wpf/RevitUiTheme.cs");
            var settingsWindow = _source.ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var statusWindow = _source.ReadText("src/PlugHub.Wpf/FrameworkStatusWindow.cs");
            var iconProvider = _source.ReadText("src/PlugHub.Wpf/DefaultRibbonIconProvider.cs");
            var buildScript = _source.ReadText("scripts/build-revit2020.ps1");

            Require(theme.Contains("class RevitUiPalette") && theme.Contains("class RevitUiTheme"), "Revit WPF UI must centralize theme tokens in RevitUiTheme.");
            Require(theme.Contains("UIThemeManager") && theme.Contains("AppsUseLightTheme"), "Revit WPF UI theme detection must prefer Revit host theme and fall back to Windows app theme.");
            Require(theme.Contains("ButtonStyle") && theme.Contains("TabItem") && theme.Contains("DataGridRow"), "Revit WPF UI theme must provide shared styles for buttons, tabs, and grids.");
            Require(theme.Contains("resources.Add(typeof(ComboBoxItem), ComboBoxItemStyle(palette))") && theme.Contains("ComboBoxItemTemplate"), "Revit WPF UI theme must explicitly style ComboBox dropdown items instead of leaving selected items on system colors.");
            Require(theme.Contains("ComboBoxTemplate(palette)") && theme.Contains("SelectionBoxItem") && theme.Contains("PART_Popup") && theme.Contains("PART_EditableTextBox") && theme.Contains("ComboBoxToggleTemplate"), "Revit WPF UI theme must explicitly template closed and editable ComboBox states so dark theme selectors cannot remain white.");
            Require(theme.Contains("OpenComboBoxDropDown") && theme.Contains("UIElement.PreviewMouseLeftButtonDownEvent"), "ComboBox closed fields must open from the full control surface, not only from the arrow toggle.");
            Require(theme.Contains("ComboBoxItem.IsHighlightedProperty") && theme.Contains("Selector.IsSelectedProperty") && theme.Contains("Control.BackgroundProperty, palette.SelectionBrush") && theme.Contains("Control.ForegroundProperty, palette.TextBrush"), "ComboBox dropdown hover and selected states must keep readable themed foreground/background colors.");
            Require(theme.Contains("SystemColors.WindowBrushKey") && theme.Contains("SystemColors.HighlightBrushKey") && theme.Contains("palette.ControlBackground"), "ComboBox dropdown popups must override WPF system window/highlight colors so dark theme menus cannot remain white.");
            Require(theme.Contains("ScrollViewer.MaxHeightProperty") && theme.Contains("ComboBox.MaxDropDownHeightProperty"), "ComboBox dropdown templates must honor MaxDropDownHeight so long filter lists scroll instead of growing unbounded.");
            Require(theme.Contains("ContentTemplateSelectorProperty") && theme.Contains("ContentPresenter.ContentTemplateSelectorProperty"), "custom ComboBox item templates must preserve DisplayMemberPath through the generated content template selector.");
            Require(theme.Contains("TabItemTemplate(palette)") && theme.Contains("ControlTemplate(typeof(TabItem))") && theme.Contains("RootBorder") && theme.Contains("Control.BorderBrushProperty, palette.AccentBrush"), "selected settings tabs must use an explicit template so WPF system colors cannot turn the selected tab white.");
            Require(theme.Contains("MenuItemTemplate") && theme.Contains("PART_Popup") && theme.Contains("SubmenuArrow"), "context menus must use a compact MenuItem template without the default icon slot.");
            Require(settingsWindow.Contains("RevitUiTheme.Apply(this)") && statusWindow.Contains("RevitUiTheme.Apply(this)"), "settings and status windows must share the Revit WPF theme.");
            Require(settingsWindow.Contains("public override string ToString()") && settingsWindow.Contains("return DisplayText;"), "layout designer combo option objects must fall back to user-facing labels if WPF ignores DisplayMemberPath.");
            Require(settingsWindow.Contains("BuildAboutTab") && settingsWindow.Contains("tabs.Items.Add(BuildAboutTab())"), "settings window must include an About tab.");
            Require(settingsWindow.Contains("BuildAboutBadge") && settingsWindow.Contains("BuildAboutInfoRow") && settingsWindow.Contains("Revit 2020"), "About tab must show concise project/runtime metadata.");
            Require(settingsWindow.Contains("核心作者") && settingsWindow.Contains("GaoMengGu") && settingsWindow.Contains("https://qm.qq.com/q/NN2psby1cQ") && settingsWindow.Contains("https://github.com/GaoMengGu/PlugHub"), "About tab must show updated author and clickable community/source links.");
            Require(settingsWindow.Contains("欢迎请作者喝一杯咖啡") && settingsWindow.Contains("☕") && settingsWindow.Contains("Width = 128") && settingsWindow.Contains("Height = 128"), "About tab must use the updated coffee support copy and 128px payment QR codes.");
            var aboutTab = _source.MethodBody(settingsWindow, "BuildAboutTab");
            Require(!aboutTab.Contains("ScrollViewer"), "About tab must fit in one page without triggering an overall scrollbar.");
            Require(aboutTab.Contains("right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) })"), "About diagnostics section must fill the right column remainder so its bottom aligns with the left column.");
            Require(settingsWindow.Contains("BuildCompactAboutSection") && settingsWindow.Contains("BuildAboutSection") && settingsWindow.Contains("MaxHeight = 38"), "About tab must use compact right-side sections so diagnostics are not clipped.");
            Require(!settingsWindow.Contains("BuildValidationCommandRow") && !settingsWindow.Contains("复制指令"), "About diagnostics must not expose developer-only static-validation command copying to normal users.");
            Require(settingsWindow.Contains("BuildButtonContent") && settingsWindow.Contains("IconKeyForButtonText"), "settings window buttons must use consistent vector icon content where appropriate.");
            Require(iconProvider.Contains("\"about\"") && iconProvider.Contains("\"repository\"") && iconProvider.Contains("\"layout\""), "built-in icon suite must include common settings/about/repository/layout icons.");
            Require(!Directory.Exists(_source.FullPath("src/PlugHub.Revit2020/Resources")), "Revit adapter must not keep obsolete file-based icon resources.");
            Require(buildScript.Contains("(Join-Path $OutputDir \"Resources\")"), "Revit build must remove stale generated Resources output after file-based icons are removed.");
        }

        private void ValidateSettingsGroupFeatureEditingBehavior()
        {
            var settingsWindow = _source.ReadText("src/PlugHub.Manager/FrameworkSettingsWindow.cs");
            var ribbonBuilder = _source.ReadText("src/PlugHub.Revit2020/FeatureRibbonBuilder.cs");
            var ribbonLayoutComposer = _source.ReadText("src/PlugHub.Framework/Composition/RibbonLayoutComposer.cs");

            Require(settingsWindow.Contains("LoadFeatureRows") && settingsWindow.Contains("LoadGroupRows"), "settings must load feature and group rows as layout data sources.");
            Require(settingsWindow.Contains("BuildRibbonDesignerPropertyPanel") && settingsWindow.Contains("CommitSelectedRibbonDesignerPropertiesFromEditor"), "layout tab must edit selected visual designer element without exposing Ribbon node internals.");
            Require(settingsWindow.Contains("RefreshFeaturePositionsByGroup"), "feature ordering must be recalculated per workspace group before visual Ribbon composition.");
            Require(settingsWindow.Contains("SortFeatureRowsForRuntimeOrder"), "feature rows must be ordered the same way runtime Ribbon composition is ordered.");
            var ribbonLayoutEditor = _source.ReadText("src/PlugHub.Framework/RibbonEditing/RibbonLayoutEditor.cs");
            Require(ribbonLayoutEditor.Contains("IsEmptyContainer") && ribbonLayoutEditor.Contains("RemoveUnavailableFeatures(row.Children, visibleFeatureIds)"), "layout editor must remove empty containers and panels after unavailable feature nodes are pruned.");
            Require(settingsWindow.Contains("TrySave") && settingsWindow.Contains("ReportSettingsError"), "settings save must catch exceptions and report them inline.");
            Require(settingsWindow.Contains("ResolveRibbonDesignerDropPlan") && settingsWindow.Contains("RefreshRibbonDesignerAfterLayoutChange"), "visual Ribbon drag behavior must update the current layout through the designer seam.");

            Require(!settingsWindow.Contains("MessageBox.Show"), "settings window must not show pop-up prompts for normal settings operations.");
            Require(!settingsWindow.Contains("BuildInstalledPackagesTab") && !settingsWindow.Contains("BuildPluginPackagesTab") && !settingsWindow.Contains("ApplyPluginPackageRows();"), "settings window must not expose the installed package settings tab.");
            Require(ribbonBuilder.Contains("new RibbonLayoutComposer().Compose")
                && ribbonLayoutComposer.Contains(".OrderBy(feature => feature.DisplayOrder)")
                && ribbonLayoutComposer.Contains(".ThenBy(feature => feature.FeatureId"),
                "Ribbon layout composer must explicitly order features inside each panel.");
        }


        private static ArrayList ArrayValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value) && value is ArrayList array ? array : new ArrayList();
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

        private static IEnumerable<Dictionary<string, object>> Modules(Dictionary<string, object> root)
        {
            return ArrayValue(root, "modules").Cast<Dictionary<string, object>>();
        }

        private static IEnumerable<Dictionary<string, object>> Views(Dictionary<string, object> root)
        {
            return ArrayValue(root, "views").Cast<Dictionary<string, object>>();
        }

        private static IEnumerable<Dictionary<string, object>> Features(Dictionary<string, object> module)
        {
            return ArrayValue(module, "features").Cast<Dictionary<string, object>>();
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
