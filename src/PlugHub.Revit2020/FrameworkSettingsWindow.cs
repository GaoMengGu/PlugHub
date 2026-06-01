using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;
using PlugHub.Framework.Packages;
using PlugHub.Framework.Runtime;
using PlugHub.Revit2020.Settings;
using PlugHub.Revit2020.Settings.RibbonDesigner;
using PlugHub.Revit2020.Settings.Rows;

namespace PlugHub.Revit2020
{
    internal sealed class FrameworkSettingsWindow : Window
    {
        private const string DefaultPackageManifestName = "package.json";
        private const string DefaultRepositoryProvider = "gitee";
        private const string DefaultPublicRepository = "https://gitee.com/GaoMengGu/PlugHub_Packages";
        private const string DefaultRibbonDesignerPanelId = "default";
        private const string DefaultRibbonDesignerPanelName = "默认";
        private const string RibbonDesignerBrowseIconAction = "__browse_icon__";
        private const string RibbonDesignerClearIconAction = "__clear_icon__";
        private static readonly string[] SameNameIconExtensions = { ".png", ".ico", ".jpg", ".jpeg", ".bmp" };

        private readonly SettingsConfigurationStore _configurationStore;
        private readonly FrameworkSettingsViewModel _viewModel = new FrameworkSettingsViewModel();
        private FrameworkConfiguration _configuration;
        private readonly PackageRepositoryService _packageRepositoryService = new PackageRepositoryService();
        private readonly RepositoryCredentialService _credentialService = new RepositoryCredentialService();
        private readonly RibbonDesignerMapper _ribbonDesignerMapper = new RibbonDesignerMapper();
        private readonly RibbonDesignerDropService _ribbonDesignerDropService = new RibbonDesignerDropService();
        private readonly RibbonLayoutDiffService _ribbonLayoutDiffService = new RibbonLayoutDiffService();
        private readonly DataGrid _pluginPackagesGrid = CreateGrid();
        private readonly DataGrid _featuresGrid = CreateGrid();
        private readonly DataGrid _groupsGrid = CreateGrid();
        private readonly DataGrid _repositoriesGrid = CreateGrid();
        private readonly DataGrid _repositoryPackagesGrid = CreateGrid();
        private readonly DataGrid _pendingPackageOperationsGrid = CreateGrid();
        private readonly DataGrid _diagnosticsGrid = CreateGrid();
        private readonly TextBlock _statusText = new TextBlock();
        private List<SettingsConfigurationStore.ModuleManifestDocument> _moduleDocuments = new List<SettingsConfigurationStore.ModuleManifestDocument>();
        private List<RibbonDesignerNodeRow> _originalRibbonDesignerTabs = new List<RibbonDesignerNodeRow>();
        private readonly ObservableCollection<GroupOption> _groupOptions = new ObservableCollection<GroupOption>();
        private readonly IReadOnlyList<string> _buttonSizeOptions = new[] { "large", "small" };
        private readonly TextBlock _selectedFeatureName = new TextBlock();
        private readonly ComboBox _selectedFeatureGroupCombo = new ComboBox();
        private readonly ComboBox _selectedFeatureButtonSizeCombo = new ComboBox();
        private readonly StackPanel _ribbonDesignerCanvas = new StackPanel { Orientation = Orientation.Vertical };
        private readonly TextBlock _selectedRibbonDesignerName = new TextBlock();
        private readonly TextBox _selectedRibbonDesignerText = new TextBox();
        private readonly ComboBox _selectedRibbonDesignerType = new ComboBox();
        private readonly ComboBox _selectedRibbonDesignerIcon = new ComboBox();
        private readonly ComboBox _selectedRibbonDesignerDefaultFeature = new ComboBox();
        private readonly HashSet<string> _expandedRibbonDesignerNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Point _ribbonDesignerDragStartPoint;
        private int _dragSourceRowIndex = -1;
        private DataGrid? _dragSourceGrid;
        private bool _syncingSelectedFeatureEditor;
        private bool _syncingSelectedRibbonDesignerEditor;

        public FrameworkSettingsWindow(string configDirectory, FrameworkConfiguration configuration)
        {
            _configurationStore = new SettingsConfigurationStore(configDirectory ?? throw new ArgumentNullException(nameof(configDirectory)));
            _configuration = _configurationStore.Load(configuration ?? throw new ArgumentNullException(nameof(configuration)));
            _moduleDocuments = _configurationStore.LoadModuleDocuments(_configuration);

            Title = "PlugHub 设置";
            Width = 1060;
            Height = 680;
            MinWidth = 860;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 252));

            Content = BuildLayout();
            LoadRows();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            header.Children.Add(new TextBlock
            {
                Text = "PlugHub 设置",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(24, 34, 48))
            });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var tabs = new TabControl
            {
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 226, 234))
            };
            tabs.Items.Add(BuildRibbonLayoutTab());
            tabs.Items.Add(BuildRepositoriesTab());
            tabs.Items.Add(BuildLogsTab());
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            return root;
        }

        private UIElement BuildFooter()
        {
            var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusText.VerticalAlignment = VerticalAlignment.Center;
            _statusText.TextWrapping = TextWrapping.Wrap;
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(72, 84, 101));
            Grid.SetColumn(_statusText, 0);
            footer.Children.Add(_statusText);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttons.Children.Add(CreateButton("重新加载", (sender, args) => ReloadFromDisk()));
            buttons.Children.Add(CreateButton("保存配置", (sender, args) => TrySave()));
            buttons.Children.Add(CreateButton("关闭", (sender, args) => Close()));
            Grid.SetColumn(buttons, 1);
            footer.Children.Add(buttons);

            return footer;
        }

        private static TextBlock EditorLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(72, 84, 101))
            };
        }

        private TabItem BuildRibbonLayoutTab()
        {
            return BuildVisualRibbonDesignerTab();
        }

        private TabItem BuildVisualRibbonDesignerTab()
        {
            var root = BuildRibbonDesignerEditorBody();
            SyncSelectedRibbonDesignerEditor();
            return BuildTab("布局", root);
        }

        private UIElement BuildRibbonDesignerEditorBody()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var layoutColumn = BuildRibbonDesignerCanvas();
            var propertyColumn = BuildRibbonDesignerPropertyPanel();
            Grid.SetRow(layoutColumn, 0);
            Grid.SetRow(propertyColumn, 1);
            root.Children.Add(layoutColumn);
            root.Children.Add(propertyColumn);
            return root;
        }

        private UIElement BuildRibbonDesignerCanvas()
        {
            _ribbonDesignerCanvas.Margin = new Thickness(8);
            _ribbonDesignerCanvas.ContextMenu = BuildRibbonDesignerCanvasMenu();
            var canvasScroll = new ScrollViewer
            {
                Content = _ribbonDesignerCanvas,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            canvasScroll.ContextMenu = BuildRibbonDesignerCanvasMenu();
            return BuildRibbonLayoutColumn("工具栏布局", canvasScroll);
        }

        private ContextMenu BuildRibbonDesignerCanvasMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("新增面板", (sender, args) => AddRibbonDesignerNode(RibbonDesignerNodeRow.Panel)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("新增下拉按钮", (sender, args) => AddRibbonDesignerNode(RibbonDesignerNodeRow.PulldownButton)));
            menu.Items.Add(MenuItem("新增拆分按钮", (sender, args) => AddRibbonDesignerNode(RibbonDesignerNodeRow.SplitButton)));
            menu.Items.Add(MenuItem("新增堆叠", (sender, args) => AddRibbonDesignerNode(RibbonDesignerNodeRow.Stack)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("移除所选", (sender, args) => RemoveSelectedRibbonDesignerNode()));
            menu.Items.Add(MenuItem("重新生成默认布局", (sender, args) => ResetDefaultRibbonLayout()));
            return menu;
        }

        private static UIElement BuildRibbonLayoutColumn(string title, UIElement content)
        {
            var panel = new DockPanel();
            var header = SectionHeader(title);
            DockPanel.SetDock(header, Dock.Top);
            panel.Children.Add(header);
            panel.Children.Add(content);
            return panel;
        }

        private UIElement BuildRibbonDesignerPropertyPanel()
        {
            var panel = new WrapPanel
            {
                Margin = new Thickness(8),
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };

            _selectedRibbonDesignerName.FontWeight = FontWeights.SemiBold;
            _selectedRibbonDesignerName.Width = 100;
            _selectedRibbonDesignerName.Margin = new Thickness(0, 18, 12, 0);
            _selectedRibbonDesignerText.Height = 26;
            _selectedRibbonDesignerText.LostFocus += SelectedRibbonDesignerTextLostFocus;
            _selectedRibbonDesignerText.KeyDown += SelectedRibbonDesignerTextKeyDown;
            _selectedRibbonDesignerType.Height = 26;
            _selectedRibbonDesignerType.ItemsSource = RibbonDesignerNodeTypeOptions(null);
            _selectedRibbonDesignerType.DisplayMemberPath = nameof(RibbonDisplayModeOption.DisplayText);
            _selectedRibbonDesignerType.SelectedValuePath = nameof(RibbonDisplayModeOption.Value);
            _selectedRibbonDesignerType.SelectionChanged += SelectedRibbonDesignerPropertySelectionChanged;
            var iconSelector = BuildRibbonDesignerIconSelector();
            _selectedRibbonDesignerDefaultFeature.Height = 26;
            _selectedRibbonDesignerDefaultFeature.DisplayMemberPath = nameof(RibbonDesignerFeatureRow.DisplayText);
            _selectedRibbonDesignerDefaultFeature.SelectedValuePath = nameof(RibbonDesignerFeatureRow.FeatureId);
            _selectedRibbonDesignerDefaultFeature.SelectionChanged += SelectedRibbonDesignerPropertySelectionChanged;

            panel.Children.Add(_selectedRibbonDesignerName);
            panel.Children.Add(BuildRibbonDesignerPropertyField("显示名", _selectedRibbonDesignerText, 180));
            panel.Children.Add(BuildRibbonDesignerPropertyField("控件类型", _selectedRibbonDesignerType, 140));
            panel.Children.Add(BuildRibbonDesignerPropertyField("图标", iconSelector, 200));
            panel.Children.Add(BuildRibbonDesignerPropertyField("默认功能", _selectedRibbonDesignerDefaultFeature, 220));

            return BuildRibbonLayoutColumn("属性", panel);
        }

        private static UIElement BuildRibbonDesignerPropertyField(string label, Control editor, double width)
        {
            var panel = new StackPanel
            {
                Width = width,
                Margin = new Thickness(0, 0, 12, 8)
            };
            panel.Children.Add(EditorLabel(label));
            editor.Margin = new Thickness(0, 2, 0, 0);
            panel.Children.Add(editor);
            return panel;
        }

        private ComboBox BuildRibbonDesignerIconSelector()
        {
            _selectedRibbonDesignerIcon.Height = 26;
            _selectedRibbonDesignerIcon.Margin = new Thickness(0, 2, 0, 8);
            _selectedRibbonDesignerIcon.IsEditable = true;
            _selectedRibbonDesignerIcon.MaxDropDownHeight = 220;
            _selectedRibbonDesignerIcon.ItemsSource = RibbonDesignerIconOptions();
            _selectedRibbonDesignerIcon.DisplayMemberPath = nameof(IconOption.DisplayText);
            _selectedRibbonDesignerIcon.SelectedValuePath = nameof(IconOption.Value);
            _selectedRibbonDesignerIcon.SelectionChanged += RibbonDesignerIconSelectionChanged;
            return _selectedRibbonDesignerIcon;
        }

        private TabItem BuildRepositoriesTab()
        {
            _repositoriesGrid.ContextMenu = BuildRepositoryMenu();
            _repositoryPackagesGrid.ContextMenu = BuildRepositoryPackageMenu();
            _pendingPackageOperationsGrid.ContextMenu = BuildPendingPackageOperationMenu();

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.42, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.38, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.20, GridUnitType.Star) });

            var repositoriesHeader = SectionHeader("仓库");
            Grid.SetRow(repositoriesHeader, 0);
            layout.Children.Add(repositoriesHeader);
            Grid.SetRow(_repositoriesGrid, 1);
            layout.Children.Add(_repositoriesGrid);

            var packagesHeader = SectionHeader("仓库插件包");
            Grid.SetRow(packagesHeader, 2);
            layout.Children.Add(packagesHeader);
            Grid.SetRow(_repositoryPackagesGrid, 3);
            layout.Children.Add(_repositoryPackagesGrid);

            var pendingHeader = SectionHeader("待处理操作");
            Grid.SetRow(pendingHeader, 4);
            layout.Children.Add(pendingHeader);
            Grid.SetRow(_pendingPackageOperationsGrid, 5);
            layout.Children.Add(_pendingPackageOperationsGrid);

            return BuildTab("仓库", layout);
        }

        private TabItem BuildLogsTab()
        {
            _diagnosticsGrid.IsReadOnly = true;
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 8, 8, 6)
            };
            buttons.Children.Add(CreateButton("导出日志", (sender, args) => ExportLogs()));
            Grid.SetRow(buttons, 0);
            layout.Children.Add(buttons);

            Grid.SetRow(_diagnosticsGrid, 1);
            layout.Children.Add(_diagnosticsGrid);
            return BuildTab("日志", layout);
        }

        private static TabItem BuildTab(string title, UIElement content)
        {
            return new TabItem
            {
                Header = title,
                Padding = new Thickness(12, 6, 12, 6),
                Content = new Border
                {
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(220, 226, 234)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(1),
                    Child = content
                }
            };
        }

        private static Button CreateButton(string text, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 92,
                Height = 30,
                Margin = new Thickness(8, 0, 0, 0)
            };
            button.Click += handler;
            return button;
        }

        private static TextBlock SectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 8, 8, 6),
                Foreground = new SolidColorBrush(Color.FromRgb(45, 56, 72))
            };
        }

        private static DataGrid CreateGrid()
        {
            return new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                RowHeight = 30,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(250, 252, 255))
            };
        }

        private void LoadRows()
        {
            LoadGroupRows();
            LoadFeatureRows();
            LoadRibbonLayoutRows();
            LoadRepositoryRows();
            LoadRepositoryPackageRows(new List<RepositoryPackageDescriptor>());
            LoadPendingPackageOperationRows();
            LoadDiagnosticRows(FrameworkRuntimeState.Current);
            RefreshStatus("已加载配置。布局和图标需重启 Revit 重绘。");
            LoadCachedRepositoryPackages();
            StartRepositoryUpdateCheck();
        }

        private void LoadPluginPackageRows()
        {
            _pluginPackagesGrid.Columns.Clear();
            _pluginPackagesGrid.Columns.Add(TextColumn(nameof(ModuleRow.PositionText), "位置", true, 0.7));
            _pluginPackagesGrid.Columns.Add(TextColumn(nameof(ModuleRow.Name), "插件包", true, 2.1));
            _pluginPackagesGrid.Columns.Add(TextColumn(nameof(ModuleRow.DisplayName), "显示名", false, 2.2));
            _pluginPackagesGrid.Columns.Add(CheckColumn(nameof(ModuleRow.Enabled), "启用"));
            _pluginPackagesGrid.Columns.Add(CheckColumn(nameof(ModuleRow.Visible), "显示"));
            _pluginPackagesGrid.Columns.Add(TextColumn(nameof(ModuleRow.SourceId), "来源", false, 1.2));

            _viewModel.Modules.Clear();
            foreach (var row in EditableModules()
                .OrderBy(module => module.Order)
                .ThenBy(module => DisplayName(module.DisplayName, module.Name, module.Id), StringComparer.OrdinalIgnoreCase)
                .Select(module => new ModuleRow
                {
                    Id = module.Id,
                    Name = DisplayName(module.DisplayName, module.Name, module.Id),
                    DisplayName = module.DisplayName,
                    Enabled = module.Enabled,
                    Visible = module.Visible,
                    SourceId = string.IsNullOrWhiteSpace(module.SourceId) ? "custom" : module.SourceId,
                    Order = module.Order
                }))
            {
                _viewModel.Modules.Add(row);
            }
            RefreshPluginPackagePositions();
            _pluginPackagesGrid.ItemsSource = _viewModel.Modules;
        }

        private void LoadGroupRows()
        {
            _groupsGrid.Columns.Clear();
            _groupsGrid.Columns.Add(TextColumn(nameof(GroupRow.PositionText), "位置", true, 0.7));
            _groupsGrid.Columns.Add(TextColumn(nameof(GroupRow.Id), "分组 ID", true, 1.8));
            _groupsGrid.Columns.Add(TextColumn(nameof(GroupRow.Name), "显示名", false, 2.2));
            _groupsGrid.Columns.Add(TextColumn(nameof(GroupRow.FeatureCountText), "功能数", true, 0.8));

            var viewGroups = WorkspaceView().Groups ?? new List<ViewGroupConfiguration>();
            var viewGroupsById = viewGroups
                .Where(group => !string.IsNullOrWhiteSpace(group.Id))
                .GroupBy(group => group.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var featureGroups = EditableModules()
                .SelectMany(module => (module.Features ?? new List<FeatureConfiguration>()).Select(feature => new
                {
                    Id = GroupIdForFeature(module, feature),
                    Feature = feature
                }))
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    viewGroupsById.TryGetValue(group.Key, out var viewGroup);
                    return new GroupRow
                    {
                        Id = group.Key,
                        Name = DisplayName(viewGroup?.Name ?? string.Empty, group.Key, group.Key),
                        Order = viewGroup?.Order > 0 ? viewGroup.Order : group.Min(item => item.Feature.Order),
                        FeatureCount = group.Count()
                    };
                })
                .ToList();

            var featureGroupIds = new HashSet<string>(featureGroups.Select(group => group.Id), StringComparer.OrdinalIgnoreCase);
            featureGroups.AddRange(viewGroups
                .Where(group => !string.IsNullOrWhiteSpace(group.Id) && !featureGroupIds.Contains(group.Id))
                .Select(group => new GroupRow
                {
                    Id = group.Id,
                    Name = DisplayName(group.Name, group.Id, group.Id),
                    Order = group.Order,
                    FeatureCount = 0
                }));

            _viewModel.Groups.Clear();
            foreach (var row in featureGroups
                .OrderBy(group => group.Order)
                .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Id, StringComparer.OrdinalIgnoreCase))
            {
                _viewModel.Groups.Add(row);
            }
            RefreshGroupPositions();
            RefreshFeatureGroupOptions();
            _groupsGrid.ItemsSource = _viewModel.Groups;
        }

        private void LoadFeatureRows()
        {
            _featuresGrid.Columns.Clear();
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.PositionText), "位置", true, 0.7));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.Name), "功能", true, 2.0));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.DisplayName), "显示名", false, 1.8));
            _featuresGrid.Columns.Add(CheckColumn(nameof(FeatureRow.Visible), "显示"));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.ModuleName), "插件包", true, 1.5));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.GroupDisplayText), "所属分组", true, 1.6));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.ButtonSizeDisplayText), "图标大小", true, 0.8));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.IconPath), "图标", false, 1.5));

            _viewModel.Features.Clear();
            foreach (var row in EditableModules()
                .SelectMany(module => (module.Features ?? new List<FeatureConfiguration>()).Select(feature => new FeatureRow
                {
                    ModuleId = module.Id,
                    OriginalModuleId = module.Id,
                    FeatureId = feature.Id,
                    ModuleName = DisplayName(module.DisplayName, module.Name, module.Id),
                    Name = DisplayName(feature.DisplayName, feature.Name, feature.Id),
                    ConfigName = feature.Name,
                    DisplayName = feature.DisplayName,
                    Description = feature.Description,
                    Category = feature.Category,
                    Group = GroupIdForFeature(module, feature),
                    Tags = new List<string>(feature.Tags ?? new List<string>()),
                    Visible = string.Equals(feature.DefaultState, "Visible", StringComparison.OrdinalIgnoreCase),
                    IconPath = feature.IconPath,
                    Order = feature.Order,
                    ButtonSize = NormalizeButtonSize(feature.ButtonSize),
                    CommandKey = feature.CommandKey,
                    CommandAssembly = feature.CommandAssembly,
                    CommandType = feature.CommandType
                }))
                .OrderBy(row => row.ModuleName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Order)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase))
            {
                _viewModel.Features.Add(row);
            }
            SortFeatureRowsForRuntimeOrder();
            RefreshFeaturePositionsByGroup();
            _featuresGrid.ItemsSource = _viewModel.Features;
            if (_viewModel.Features.Count > 0)
            {
                _featuresGrid.SelectedIndex = 0;
            }

            SyncSelectedFeatureEditor();
        }

        private void LoadRibbonLayoutRows()
        {
            _viewModel.RibbonLayoutNodes.Clear();
            _viewModel.RibbonFeaturePool.Clear();
            _viewModel.RibbonDesignerFeatures.Clear();
            _viewModel.RibbonDesignerTabs.Clear();

            var ribbon = WorkspaceView().Ribbon ?? new RibbonConfiguration();
            var panels = ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>();
            if (panels.Count > 0)
            {
                var merged = MergeRibbonPanelsByDisplayName(panels.Select(RibbonLayoutNodeRow.FromPanel).ToList());
                ribbon.Panels = merged.Select(row => row.ToPanelConfiguration()).ToList();
            }

            foreach (var tab in _ribbonDesignerMapper.FromConfiguration(ribbon, _viewModel.Features))
            {
                _viewModel.RibbonDesignerTabs.Add(tab);
            }

            foreach (var feature in _viewModel.Features
                .Where(row => row.Visible)
                .OrderBy(row => row.ModuleName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Order)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.FeatureId, StringComparer.OrdinalIgnoreCase))
            {
                var displayName = DisplayName(feature.DisplayName, feature.Name, feature.FeatureId);
                _viewModel.RibbonDesignerFeatures.Add(new RibbonDesignerFeatureRow
                {
                    ModuleId = feature.ModuleId,
                    ModuleName = feature.ModuleName,
                    FeatureId = feature.FeatureId,
                    FeatureName = feature.Name,
                    DisplayName = displayName,
                    SearchText = (displayName + " " + feature.Name + " " + feature.ModuleName + " " + feature.FeatureId).Trim(),
                    IconPath = feature.IconPath,
                    ButtonSize = NormalizeButtonSize(feature.ButtonSize),
                    DisplayText = displayName
                });
            }

            RefreshRibbonDesignerLayoutState();
            InitializeRibbonDesignerContainerExpansionState();
            _originalRibbonDesignerTabs = RibbonDesignerMapper.CloneTabs(_viewModel.RibbonDesignerTabs);
            RefreshRibbonDesignerCanvas();
            RefreshRibbonDesignerChangeSummary();
            SelectRibbonDesignerNode(_viewModel.RibbonDesignerTabs.FirstOrDefault()?.Children.FirstOrDefault());
        }

        private void RefreshRibbonDesignerLayoutState()
        {
            RemoveUnavailableRibbonDesignerFeatures();
            EnsureAllVisibleFeaturesInRibbonDesignerLayout();
            NormalizeRibbonDesignerLayout();
        }

        private void RemoveUnavailableRibbonDesignerFeatures()
        {
            var visibleFeatureIds = new HashSet<string>(
                _viewModel.RibbonDesignerFeatures
                    .Where(feature => !string.IsNullOrWhiteSpace(feature.FeatureId))
                    .Select(feature => feature.FeatureId),
                StringComparer.OrdinalIgnoreCase);
            foreach (var tab in _viewModel.RibbonDesignerTabs)
            {
                RemoveUnavailableRibbonDesignerFeatures(tab, visibleFeatureIds);
            }
        }

        private static void RemoveUnavailableRibbonDesignerFeatures(RibbonDesignerNodeRow parent, ISet<string> visibleFeatureIds)
        {
            for (var index = parent.Children.Count - 1; index >= 0; index--)
            {
                var child = parent.Children[index];
                if (RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.PushButton)
                    && !string.IsNullOrWhiteSpace(child.FeatureId)
                    && !visibleFeatureIds.Contains(child.FeatureId))
                {
                    parent.Children.RemoveAt(index);
                    continue;
                }

                RemoveUnavailableRibbonDesignerFeatures(child, visibleFeatureIds);
            }
        }

        private void EnsureAllVisibleFeaturesInRibbonDesignerLayout()
        {
            var visibleFeatures = _viewModel.RibbonDesignerFeatures
                .Where(feature => !string.IsNullOrWhiteSpace(feature.FeatureId))
                .ToList();
            var missingFeatures = visibleFeatures
                .Where(feature => !_ribbonDesignerDropService.IsFeaturePlaced(_viewModel.RibbonDesignerTabs, feature.FeatureId))
                .ToList();
            if (missingFeatures.Count == 0)
            {
                return;
            }

            var defaultPanel = EnsureDefaultRibbonDesignerPanel();
            foreach (var feature in missingFeatures)
            {
                defaultPanel.Children.Add(RibbonDesignerMapper.CreateFeatureNode(feature, (defaultPanel.Children.Count + 1) * 100));
            }
        }

        private RibbonDesignerNodeRow EnsureDefaultRibbonDesignerPanel()
        {
            var tab = _viewModel.RibbonDesignerTabs.FirstOrDefault();
            if (tab == null)
            {
                tab = new RibbonDesignerNodeRow
                {
                    NodeType = RibbonDesignerNodeRow.Tab,
                    Id = "tab",
                    Text = "PlugHub",
                    Order = 100
                };
                _viewModel.RibbonDesignerTabs.Add(tab);
            }

            var panel = tab.Children.FirstOrDefault(row =>
                RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Panel)
                && (string.Equals(row.Id, DefaultRibbonDesignerPanelId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.Text, DefaultRibbonDesignerPanelName, StringComparison.OrdinalIgnoreCase)));
            if (panel != null)
            {
                panel.Id = DefaultRibbonDesignerPanelId;
                panel.Text = DefaultRibbonDesignerPanelName;
                return panel;
            }

            panel = RibbonDesignerMapper.CreateContainerNode(
                RibbonDesignerNodeRow.Panel,
                DefaultRibbonDesignerPanelName,
                (tab.Children.Count + 1) * 100);
            panel.Id = DefaultRibbonDesignerPanelId;
            tab.Children.Add(panel);
            return panel;
        }

        private void NormalizeRibbonDesignerLayout()
        {
            foreach (var tab in _viewModel.RibbonDesignerTabs)
            {
                NormalizeRibbonDesignerChildSizes(tab);
            }
        }

        private void NormalizeRibbonDesignerChildSizes(RibbonDesignerNodeRow parent)
        {
            foreach (var child in parent.Children)
            {
                child.Size = InferredRibbonDesignerButtonSize(parent, child);
                if (!CanEditRibbonDesignerIcon(child))
                {
                    child.IconPath = string.Empty;
                }

                NormalizeRibbonDesignerChildSizes(child);
            }
        }

        private static string InferredRibbonDesignerButtonSize(RibbonDesignerNodeRow parent, RibbonDesignerNodeRow child)
        {
            if (RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.Panel)
                || RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.Tab))
            {
                return "large";
            }

            return RibbonDesignerMapper.IsType(parent, RibbonDesignerNodeRow.Panel) ? "large" : "small";
        }

        private void RefreshRibbonDesignerCanvas()
        {
            _ribbonDesignerCanvas.Children.Clear();
            foreach (var tab in _viewModel.RibbonDesignerTabs)
            {
                _ribbonDesignerCanvas.Children.Add(BuildRibbonDesignerTabPreview(tab));
            }
        }

        private void RefreshRibbonDesignerChangeSummary()
        {
            var message = RibbonDesignerChangeSummaryMessage();
            RefreshStatus(message);
        }

        private string RibbonDesignerChangeSummaryMessage()
        {
            var changedRows = _ribbonLayoutDiffService
                .Compare(_originalRibbonDesignerTabs, _viewModel.RibbonDesignerTabs)
                .Where(row => !string.Equals(row.ChangeType, "无变更", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (changedRows.Count == 0)
            {
                return "布局未变更";
            }

            return "未保存布局变更: " + changedRows.Count + " 项，保存后需重启 Revit 生效";
        }

        private UIElement BuildRibbonDesignerTabPreview(RibbonDesignerNodeRow tab)
        {
            var body = new StackPanel();
            body.Children.Add(BuildRibbonDesignerPreviewButton(tab, DisplayName(tab.Text, tab.Id, "PlugHub"), 180, 30));

            var panels = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            foreach (var panel in tab.Children)
            {
                panels.Children.Add(BuildRibbonDesignerPanelPreview(panel));
            }

            body.Children.Add(panels);
            return BuildRibbonDesignerDropBorder(tab, body, new Thickness(0, 0, 0, 12), new Thickness(8), Brushes.Transparent);
        }

        private UIElement BuildRibbonDesignerPanelPreview(RibbonDesignerNodeRow panel)
        {
            var body = new Grid { MinHeight = 118 };
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 76 });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var dropSurface = BuildRibbonDesignerPanelDropSurface(panel);
            Grid.SetRow(dropSurface, 0);
            body.Children.Add(dropSurface);

            var title = new TextBlock
            {
                Text = DisplayName(panel.Text, panel.Id, "面板"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(title, 1);
            body.Children.Add(title);

            return BuildRibbonDesignerDropBorder(panel, body, new Thickness(0, 0, 10, 10), new Thickness(8), Brushes.White);
        }

        private UIElement BuildRibbonDesignerPanelDropSurface(RibbonDesignerNodeRow panel)
        {
            var items = new WrapPanel { Orientation = Orientation.Horizontal, MinHeight = 72 };
            foreach (var child in panel.Children)
            {
                items.Children.Add(BuildRibbonDesignerItemPreview(child));
            }

            var surface = new Border
            {
                Tag = panel,
                MinHeight = 76,
                Background = panel.Children.Count == 0 ? new SolidColorBrush(Color.FromRgb(250, 252, 255)) : Brushes.Transparent,
                BorderBrush = panel.Children.Count == 0 ? new SolidColorBrush(Color.FromRgb(226, 232, 240)) : Brushes.Transparent,
                BorderThickness = new Thickness(panel.Children.Count == 0 ? 1 : 0),
                Child = items,
                AllowDrop = true
            };
            surface.PreviewMouseLeftButtonDown += RibbonDesignerNodeMouseLeftButtonDown;
            surface.PreviewMouseMove += RibbonDesignerNodeMouseMove;
            surface.DragOver += RibbonDesignerNodeDragOver;
            surface.Drop += RibbonDesignerNodeDrop;
            return surface;
        }

        private UIElement BuildRibbonDesignerItemPreview(RibbonDesignerNodeRow row)
        {
            if (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Stack))
            {
                return BuildRibbonDesignerStackPreview(row);
            }

            if (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PulldownButton)
                || RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.SplitButton))
            {
                return BuildRibbonDesignerContainerPreview(row);
            }

            return BuildRibbonDesignerPushButtonPreview(row);
        }

        private UIElement BuildRibbonDesignerPushButtonPreview(RibbonDesignerNodeRow row)
        {
            var parent = FindRibbonDesignerParent(row);
            var size = parent == null ? NormalizeButtonSize(row.Size) : InferredRibbonDesignerButtonSize(parent, row);
            return string.Equals(size, "small", StringComparison.OrdinalIgnoreCase)
                ? BuildRibbonDesignerSmallButtonPreview(row)
                : BuildRibbonDesignerLargeButtonPreview(row);
        }

        private UIElement BuildRibbonDesignerContainerPreview(RibbonDesignerNodeRow row)
        {
            var parent = FindRibbonDesignerParent(row);
            if (parent != null && RibbonDesignerMapper.IsType(parent, RibbonDesignerNodeRow.Stack))
            {
                return BuildRibbonDesignerSmallButtonPreview(row);
            }

            var body = new StackPanel
            {
                Width = 148
            };
            var containerButton = BuildRibbonDesignerLargeButtonPreview(row);
            containerButton.HorizontalAlignment = HorizontalAlignment.Center;
            body.Children.Add(containerButton);

            if (IsRibbonDesignerContainerExpanded(row) && row.Children.Count > 0)
            {
                body.Children.Add(BuildRibbonDesignerContainerMenuPreview(row, new Thickness(0, -4, 8, 8)));
            }

            return BuildRibbonDesignerDropBorder(row, body, new Thickness(0, 0, 8, 8), new Thickness(4), new SolidColorBrush(Color.FromRgb(247, 249, 252)));
        }

        private UIElement BuildRibbonDesignerStackPreview(RibbonDesignerNodeRow row)
        {
            var body = new StackPanel
            {
                Width = 148,
                MinHeight = 58
            };
            foreach (var child in row.Children)
            {
                body.Children.Add(BuildRibbonDesignerSmallButtonPreview(child));
                if (IsRibbonDesignerContainerExpanded(child) && child.Children.Count > 0)
                {
                    body.Children.Add(BuildRibbonDesignerContainerMenuPreview(child, new Thickness(18, -4, 4, 4)));
                }
            }

            if (row.Children.Count == 0)
            {
                body.Children.Add(new TextBlock
                {
                    Text = "拖入 2-3 个控件",
                    Margin = new Thickness(4, 0, 4, 4),
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    TextAlignment = TextAlignment.Center
                });
            }

            return BuildRibbonDesignerDropBorder(row, body, new Thickness(0, 0, 8, 8), new Thickness(4), new SolidColorBrush(Color.FromRgb(247, 249, 252)));
        }

        private UIElement BuildRibbonDesignerContainerMenuPreview(RibbonDesignerNodeRow row, Thickness margin)
        {
            var menuItems = new StackPanel();
            foreach (var child in row.Children)
            {
                menuItems.Children.Add(BuildRibbonDesignerSmallButtonPreview(child));
            }

            return new Border
            {
                Margin = margin,
                Padding = new Thickness(4),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 218, 229)),
                BorderThickness = new Thickness(1),
                Child = menuItems
            };
        }

        private Button BuildRibbonDesignerLargeButtonPreview(RibbonDesignerNodeRow row)
        {
            var label = DisplayName(row.Text, row.FeatureId, RibbonNodeTypeDisplayName(row.NodeType));
            var body = new Grid();
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var icon = BuildRibbonDesignerIconPreview(row, 28);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(icon, 0);
            body.Children.Add(icon);

            var text = BuildRibbonDesignerText(label, TextAlignment.Center, TextWrapping.Wrap);
            text.MaxHeight = 32;
            Grid.SetRow(text, 1);
            body.Children.Add(text);

            var arrow = BuildRibbonDesignerDropArrow(row);
            if (arrow != null)
            {
                Grid.SetRow(arrow, 2);
                body.Children.Add(arrow);
            }

            return BuildRibbonDesignerPreviewButton(row, body, 86, 76);
        }

        private Button BuildRibbonDesignerSmallButtonPreview(RibbonDesignerNodeRow row)
        {
            var label = DisplayName(row.Text, row.FeatureId, RibbonNodeTypeDisplayName(row.NodeType));
            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = BuildRibbonDesignerIconPreview(row, 16);
            icon.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(icon, 0);
            body.Children.Add(icon);

            var text = BuildRibbonDesignerText(label, TextAlignment.Left, TextWrapping.NoWrap);
            text.Margin = new Thickness(5, 0, 4, 0);
            text.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(text, 1);
            body.Children.Add(text);

            var arrow = BuildRibbonDesignerDropArrow(row);
            if (arrow != null)
            {
                arrow.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(arrow, 2);
                body.Children.Add(arrow);
            }

            return BuildRibbonDesignerPreviewButton(row, body, 136, 24);
        }

        private FrameworkElement BuildRibbonDesignerIconPreview(RibbonDesignerNodeRow row, double size)
        {
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromRgb(232, 238, 247)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(190, 203, 221)),
                BorderThickness = new Thickness(1),
                Child = new Image
                {
                    Source = LoadRibbonDesignerIcon(row, size > 16),
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(size <= 16 ? 1 : 2)
                }
            };
        }

        private ImageSource LoadRibbonDesignerIcon(RibbonDesignerNodeRow row, bool large)
        {
            return LoadConfiguredRibbonDesignerIcon(RibbonDesignerIconPath(row), large)
                ?? LoadDllSiblingRibbonDesignerIcon(row, large)
                ?? (large ? DefaultRibbonIconProvider.CreateLargeIcon() : DefaultRibbonIconProvider.CreateSmallIcon());
        }

        private string RibbonDesignerIconPath(RibbonDesignerNodeRow row)
        {
            if (row == null) return string.Empty;
            if (!RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PushButton)) return string.Empty;
            if (!string.IsNullOrWhiteSpace(row.IconPath)) return row.IconPath;
            if (string.IsNullOrWhiteSpace(row.FeatureId)) return string.Empty;

            return _viewModel.RibbonDesignerFeatures
                .FirstOrDefault(feature => string.Equals(feature.FeatureId, row.FeatureId, StringComparison.OrdinalIgnoreCase))
                ?.IconPath ?? string.Empty;
        }

        private ImageSource? LoadDllSiblingRibbonDesignerIcon(RibbonDesignerNodeRow row, bool large)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.FeatureId)) return null;
            var commandAssembly = _viewModel.Features
                .FirstOrDefault(feature => string.Equals(feature.FeatureId, row.FeatureId, StringComparison.OrdinalIgnoreCase))
                ?.CommandAssembly ?? string.Empty;
            if (string.IsNullOrWhiteSpace(commandAssembly)) return null;

            var resolvedAssembly = Path.IsPathRooted(commandAssembly)
                ? commandAssembly
                : Path.GetFullPath(Path.Combine(BaseDirectory(), commandAssembly));
            var directory = Path.GetDirectoryName(resolvedAssembly);
            var stem = Path.GetFileNameWithoutExtension(resolvedAssembly);
            if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(stem)) return null;

            foreach (var extension in SameNameIconExtensions)
            {
                var candidate = Path.Combine(directory, stem + extension);
                var icon = LoadConfiguredRibbonDesignerIcon(candidate, large);
                if (icon != null) return icon;
            }

            return null;
        }

        private ImageSource? LoadConfiguredRibbonDesignerIcon(string iconPath, bool large)
        {
            if (string.IsNullOrWhiteSpace(iconPath)) return null;
            if (DefaultRibbonIconProvider.TryCreateIcon(iconPath, large, out var builtinIcon))
            {
                return builtinIcon;
            }

            var resolvedPath = Path.IsPathRooted(iconPath)
                ? iconPath
                : Path.GetFullPath(Path.Combine(BaseDirectory(), iconPath));
            if (!File.Exists(resolvedPath)) return null;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(resolvedPath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static TextBlock BuildRibbonDesignerText(string text, TextAlignment alignment, TextWrapping wrapping)
        {
            return new TextBlock
            {
                Text = text,
                TextAlignment = alignment,
                TextWrapping = wrapping,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 11,
                LineHeight = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(24, 34, 48)),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private TextBlock? BuildRibbonDesignerDropArrow(RibbonDesignerNodeRow row)
        {
            if (!RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PulldownButton)
                && !RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.SplitButton))
            {
                return null;
            }

            return new TextBlock
            {
                Text = RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.SplitButton)
                    ? (IsRibbonDesignerContainerExpanded(row) ? "| ^" : "| v")
                    : (IsRibbonDesignerContainerExpanded(row) ? "^" : "v"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(72, 84, 101)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private void InitializeRibbonDesignerContainerExpansionState()
        {
            _expandedRibbonDesignerNodeIds.Clear();
            foreach (var row in _ribbonDesignerDropService.Flatten(_viewModel.RibbonDesignerTabs))
            {
                if (IsExpandableRibbonDesignerContainer(row))
                {
                    _expandedRibbonDesignerNodeIds.Add(RibbonDesignerNodeKey(row));
                }
            }
        }

        private bool IsRibbonDesignerContainerExpanded(RibbonDesignerNodeRow row)
        {
            if (!IsExpandableRibbonDesignerContainer(row)) return false;
            return _expandedRibbonDesignerNodeIds.Contains(RibbonDesignerNodeKey(row));
        }

        private bool IsExpandableRibbonDesignerContainer(RibbonDesignerNodeRow row)
        {
            if (row == null) return false;
            if (!RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PulldownButton)
                && !RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.SplitButton))
            {
                return false;
            }

            return true;
        }

        private void ToggleRibbonDesignerContainerExpansion(RibbonDesignerNodeRow row)
        {
            if (!IsExpandableRibbonDesignerContainer(row)) return;
            var key = RibbonDesignerNodeKey(row);
            var expanded = !_expandedRibbonDesignerNodeIds.Contains(key);
            if (expanded)
            {
                _expandedRibbonDesignerNodeIds.Add(key);
            }
            else
            {
                _expandedRibbonDesignerNodeIds.Remove(key);
            }

            SelectRibbonDesignerNode(row);
            RefreshStatus((expanded ? "已展开 " : "已收起 ") + DisplayName(row.Text, row.Id, RibbonNodeTypeDisplayName(row.NodeType)) + "。");
        }

        private static string RibbonDesignerNodeKey(RibbonDesignerNodeRow row)
        {
            if (row == null) return string.Empty;
            return string.Join("|", new[]
            {
                row.NodeType ?? string.Empty,
                row.Id ?? string.Empty,
                row.FeatureId ?? string.Empty,
                row.Order.ToString()
            });
        }

        private Border BuildRibbonDesignerDropBorder(RibbonDesignerNodeRow row, UIElement child, Thickness margin, Thickness padding, Brush background)
        {
            var selected = ReferenceEquals(row, _viewModel.SelectedRibbonDesignerNode);
            var border = new Border
            {
                Tag = row,
                MinWidth = RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Panel) ? 180 : 0,
                Margin = margin,
                Padding = padding,
                Background = background,
                BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(25, 118, 210)) : new SolidColorBrush(Color.FromRgb(210, 218, 229)),
                BorderThickness = new Thickness(selected ? 2 : 1),
                Child = child,
                AllowDrop = true
            };
            border.PreviewMouseLeftButtonDown += RibbonDesignerNodeMouseLeftButtonDown;
            border.PreviewMouseMove += RibbonDesignerNodeMouseMove;
            border.DragOver += RibbonDesignerNodeDragOver;
            border.Drop += RibbonDesignerNodeDrop;
            return border;
        }

        private Button BuildRibbonDesignerPreviewButton(RibbonDesignerNodeRow row, object content, double width, double height)
        {
            var selected = ReferenceEquals(row, _viewModel.SelectedRibbonDesignerNode);
            var button = new Button
            {
                Tag = row,
                Content = content,
                Width = width,
                Height = height,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(6, 2, 6, 2),
                Background = selected ? new SolidColorBrush(Color.FromRgb(227, 242, 253)) : Brushes.White,
                BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(25, 118, 210)) : new SolidColorBrush(Color.FromRgb(198, 210, 225)),
                BorderThickness = new Thickness(selected ? 2 : 1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                AllowDrop = true
            };
            button.Click += (sender, args) =>
            {
                if (IsExpandableRibbonDesignerContainer(row))
                {
                    ToggleRibbonDesignerContainerExpansion(row);
                }
                else
                {
                    SelectRibbonDesignerNode(row);
                }

                args.Handled = true;
            };
            button.PreviewMouseLeftButtonDown += RibbonDesignerNodeMouseLeftButtonDown;
            button.PreviewMouseMove += RibbonDesignerNodeMouseMove;
            button.DragOver += RibbonDesignerNodeDragOver;
            button.Drop += RibbonDesignerNodeDrop;
            return button;
        }

        private void RibbonDesignerNodeMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
        {
            if (!IsRibbonDesignerDirectEventNode(sender, args.OriginalSource)) return;
            CommitSelectedRibbonDesignerPropertiesBeforeCanvasInteraction();
            _ribbonDesignerDragStartPoint = args.GetPosition(this);
            if (ResolveRibbonDesignerEventNode(args.OriginalSource, sender) is RibbonDesignerNodeRow row)
            {
                SelectRibbonDesignerNode(row, false);
            }
        }

        private void RibbonDesignerNodeMouseMove(object sender, MouseEventArgs args)
        {
            if (args.LeftButton != MouseButtonState.Pressed) return;
            if (!(sender is FrameworkElement element)) return;
            if (!IsRibbonDesignerDirectEventNode(sender, args.OriginalSource)) return;
            if (!(ResolveRibbonDesignerEventNode(args.OriginalSource, sender) is RibbonDesignerNodeRow row)) return;
            if (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Tab)) return;

            var current = args.GetPosition(this);
            if (Math.Abs(current.X - _ribbonDesignerDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(current.Y - _ribbonDesignerDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(element, row, DragDropEffects.Move);
        }

        private bool IsRibbonDesignerDirectEventNode(object sender, object originalSource)
        {
            if (!(sender is FrameworkElement element) || !(element.Tag is RibbonDesignerNodeRow row)) return false;
            return ReferenceEquals(row, ResolveRibbonDesignerEventNode(originalSource, sender));
        }

        private RibbonDesignerNodeRow? ResolveRibbonDesignerEventNode(object originalSource, object? fallbackSource)
        {
            var current = originalSource as DependencyObject;
            while (current != null)
            {
                if (current is FrameworkElement element && element.Tag is RibbonDesignerNodeRow row)
                {
                    return row;
                }

                current = VisualTreeParent(current);
            }

            if (fallbackSource is FrameworkElement fallbackElement && fallbackElement.Tag is RibbonDesignerNodeRow fallbackRow)
            {
                return fallbackRow;
            }

            return null;
        }

        private static DependencyObject? VisualTreeParent(DependencyObject current)
        {
            try
            {
                return VisualTreeHelper.GetParent(current);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private void RibbonDesignerNodeDragOver(object sender, DragEventArgs args)
        {
            var target = ResolveRibbonDesignerEventNode(args.OriginalSource, sender);
            if (target == null) return;

            if (args.Data.GetDataPresent(typeof(RibbonDesignerFeatureRow))
                && args.Data.GetData(typeof(RibbonDesignerFeatureRow)) is RibbonDesignerFeatureRow feature)
            {
                args.Effects = ResolveRibbonDesignerDropPlan(target, feature)?.IsAllowed == true ? DragDropEffects.Copy : DragDropEffects.None;
                args.Handled = true;
                return;
            }

            if (args.Data.GetDataPresent(typeof(RibbonDesignerNodeRow))
                && args.Data.GetData(typeof(RibbonDesignerNodeRow)) is RibbonDesignerNodeRow source)
            {
                args.Effects = ResolveRibbonDesignerDropPlan(target, source)?.IsAllowed == true ? DragDropEffects.Move : DragDropEffects.None;
                args.Handled = true;
            }
        }

        private void RibbonDesignerNodeDrop(object sender, DragEventArgs args)
        {
            var target = ResolveRibbonDesignerEventNode(args.OriginalSource, sender);
            if (target == null) return;

            if (args.Data.GetData(typeof(RibbonDesignerFeatureRow)) is RibbonDesignerFeatureRow feature)
            {
                var plan = ResolveRibbonDesignerDropPlan(target, feature);
                if (plan?.IsAllowed != true)
                {
                    RefreshStatus("功能只能放入面板、下拉、拆分或未满的堆叠控件。");
                    args.Handled = true;
                    return;
                }

                var node = RibbonDesignerMapper.CreateFeatureNode(feature, (plan.Parent!.Children.Count + 1) * 100);
                _ribbonDesignerDropService.InsertRibbonDesignerNode(node, plan);
                SelectRibbonDesignerNode(node);
                RefreshRibbonDesignerAfterLayoutChange("已放置功能，保存并重启 Revit 后生效。");
                args.Handled = true;
                return;
            }

            if (args.Data.GetData(typeof(RibbonDesignerNodeRow)) is RibbonDesignerNodeRow source)
            {
                if (CombineRibbonDesignerPushButtons(source, target))
                {
                    args.Handled = true;
                    return;
                }

                var plan = ResolveRibbonDesignerDropPlan(target, source);
                if (plan?.IsAllowed != true)
                {
                    RefreshStatus("该布局项不能放入目标位置。");
                    args.Handled = true;
                    return;
                }

                if (_ribbonDesignerDropService.ApplyDrop(_viewModel.RibbonDesignerTabs, source, plan))
                {
                    SelectRibbonDesignerNode(source);
                    RefreshRibbonDesignerAfterLayoutChange("已移动布局项，保存并重启 Revit 后生效。");
                }

                args.Handled = true;
            }
        }

        private bool CombineRibbonDesignerPushButtons(RibbonDesignerNodeRow source, RibbonDesignerNodeRow target)
        {
            if (source == null || target == null || ReferenceEquals(source, target)) return false;
            if (!RibbonDesignerMapper.IsType(source, RibbonDesignerNodeRow.PushButton)
                || !RibbonDesignerMapper.IsType(target, RibbonDesignerNodeRow.PushButton))
            {
                return false;
            }

            var targetParent = FindRibbonDesignerParent(target);
            if (targetParent == null || !RibbonDesignerMapper.IsType(targetParent, RibbonDesignerNodeRow.Panel))
            {
                return false;
            }

            var stack = CreateRibbonDesignerStackFromDrop(source, target);
            if (stack == null)
            {
                return false;
            }

            if (!RemoveRibbonDesignerNode(_viewModel.RibbonDesignerTabs, source))
            {
                return false;
            }

            targetParent = FindRibbonDesignerParent(target);
            if (targetParent == null)
            {
                return false;
            }

            var targetIndex = targetParent.Children.IndexOf(target);
            if (targetIndex < 0)
            {
                return false;
            }

            targetParent.Children.RemoveAt(targetIndex);
            targetParent.Children.Insert(targetIndex, stack);
            SelectRibbonDesignerNode(stack);
            RefreshRibbonDesignerAfterLayoutChange("已把两个功能组合为堆叠控件，可在属性栏切换为下拉或拆分。");
            return true;
        }

        private RibbonDesignerNodeRow? CreateRibbonDesignerStackFromDrop(RibbonDesignerNodeRow source, RibbonDesignerNodeRow target)
        {
            if (string.IsNullOrWhiteSpace(source.FeatureId) || string.IsNullOrWhiteSpace(target.FeatureId))
            {
                return null;
            }

            var stack = RibbonDesignerMapper.CreateContainerNode(
                RibbonDesignerNodeRow.Stack,
                DisplayName(target.Text, target.FeatureId, "组合"),
                target.Order);
            target.Order = 100;
            source.Order = 200;
            stack.Children.Add(target);
            stack.Children.Add(source);
            return stack;
        }

        private RibbonDesignerDropPlan? ResolveRibbonDesignerDropPlan(RibbonDesignerNodeRow target, RibbonDesignerFeatureRow feature)
        {
            foreach (var candidate in RibbonDesignerDropTargetChain(target))
            {
                var plan = _ribbonDesignerDropService.PlanFeatureDrop(_viewModel.RibbonDesignerTabs, feature, candidate);
                if (plan.IsAllowed)
                {
                    ApplyRibbonDesignerSibling(target, candidate, plan);
                    return plan;
                }
            }

            return null;
        }

        private RibbonDesignerDropPlan? ResolveRibbonDesignerDropPlan(RibbonDesignerNodeRow target, RibbonDesignerNodeRow source)
        {
            if (ReferenceEquals(target, source)) return null;
            foreach (var candidate in RibbonDesignerDropTargetChain(target))
            {
                var plan = _ribbonDesignerDropService.PlanNodeMove(_viewModel.RibbonDesignerTabs, source, candidate);
                if (plan.IsAllowed)
                {
                    ApplyRibbonDesignerSibling(target, candidate, plan);
                    return plan;
                }
            }

            return null;
        }

        private void ApplyRibbonDesignerSibling(RibbonDesignerNodeRow target, RibbonDesignerNodeRow parent, RibbonDesignerDropPlan plan)
        {
            if (!ReferenceEquals(target, parent) && parent.Children.Contains(target))
            {
                plan.Sibling = target;
                plan.Placement = RibbonDesignerDropPlacement.After;
            }
        }

        private RibbonDesignerNodeRow? ResolveRibbonDesignerDropTarget(RibbonDesignerNodeRow target, RibbonDesignerFeatureRow feature)
        {
            return ResolveRibbonDesignerDropPlan(target, feature)?.Parent;
        }

        private RibbonDesignerNodeRow? ResolveRibbonDesignerDropTarget(RibbonDesignerNodeRow target, RibbonDesignerNodeRow source)
        {
            return ResolveRibbonDesignerDropPlan(target, source)?.Parent;
        }

        private IEnumerable<RibbonDesignerNodeRow> RibbonDesignerDropTargetChain(RibbonDesignerNodeRow target)
        {
            RibbonDesignerNodeRow? current = target;
            while (current != null)
            {
                yield return current;
                current = FindRibbonDesignerParent(current);
            }
        }

        private RibbonDesignerNodeRow? FindRibbonDesignerParent(RibbonDesignerNodeRow child)
        {
            return FindRibbonDesignerParent(_viewModel.RibbonDesignerTabs, child);
        }

        private static RibbonDesignerNodeRow? FindRibbonDesignerParent(IEnumerable<RibbonDesignerNodeRow> roots, RibbonDesignerNodeRow child)
        {
            foreach (var root in roots ?? new List<RibbonDesignerNodeRow>())
            {
                if (root.Children.Any(item => ReferenceEquals(item, child)))
                {
                    return root;
                }

                var parent = FindRibbonDesignerParent(root.Children, child);
                if (parent != null)
                {
                    return parent;
                }
            }

            return null;
        }

        private void AddRibbonDesignerNode(string nodeType)
        {
            var parent = FindRibbonDesignerParentForNewNode(nodeType);
            if (parent == null)
            {
                RefreshStatus("当前布局没有可用放置位置。");
                return;
            }

            var node = RibbonDesignerMapper.CreateContainerNode(nodeType, RibbonNodeTypeDisplayName(nodeType), (parent.Children.Count + 1) * 100);
            parent.Children.Add(node);
            if (IsExpandableRibbonDesignerContainer(node))
            {
                _expandedRibbonDesignerNodeIds.Add(RibbonDesignerNodeKey(node));
            }

            SelectRibbonDesignerNode(node);
            RefreshRibbonDesignerAfterLayoutChange("已新增布局项，保存并重启 Revit 后生效。");
        }

        private RibbonDesignerNodeRow? FindRibbonDesignerParentForNewNode(string nodeType)
        {
            if (string.Equals(nodeType, RibbonDesignerNodeRow.Panel, StringComparison.OrdinalIgnoreCase))
            {
                return _viewModel.RibbonDesignerTabs.FirstOrDefault();
            }

            var selected = _viewModel.SelectedRibbonDesignerNode;
            var candidate = RibbonDesignerMapper.CreateContainerNode(nodeType, RibbonNodeTypeDisplayName(nodeType), 100);
            if (selected != null && _ribbonDesignerDropService.CanContainNode(selected, candidate))
            {
                return selected;
            }

            return _ribbonDesignerDropService
                .Flatten(_viewModel.RibbonDesignerTabs)
                .FirstOrDefault(row => _ribbonDesignerDropService.CanContainNode(row, candidate));
        }

        private void SelectRibbonDesignerNode(RibbonDesignerNodeRow? row, bool refreshCanvas = true)
        {
            _viewModel.SelectedRibbonDesignerNode = row;
            SyncSelectedRibbonDesignerEditor();
            if (refreshCanvas)
            {
                RefreshRibbonDesignerCanvas();
            }
        }

        private void SyncSelectedRibbonDesignerEditor()
        {
            _syncingSelectedRibbonDesignerEditor = true;
            try
            {
                var row = _viewModel.SelectedRibbonDesignerNode;
                var hasSelection = row != null;
                var canEditItemType = hasSelection
                    && !RibbonDesignerMapper.IsType(row!, RibbonDesignerNodeRow.Tab)
                    && !RibbonDesignerMapper.IsType(row!, RibbonDesignerNodeRow.Panel);
                var canEditItemProperties = canEditItemType || (hasSelection && RibbonDesignerMapper.IsType(row!, RibbonDesignerNodeRow.Panel));
                var typeOptions = RibbonDesignerNodeTypeOptions(row);

                _selectedRibbonDesignerName.Text = hasSelection ? RibbonNodeTypeDisplayName(row!.NodeType) : "请选择布局项";
                _selectedRibbonDesignerText.IsEnabled = CanEditRibbonDesignerDisplayName(row);
                _selectedRibbonDesignerType.ItemsSource = typeOptions;
                _selectedRibbonDesignerType.IsEnabled = canEditItemType && typeOptions.Count > 1;
                _selectedRibbonDesignerIcon.IsEnabled = CanEditRibbonDesignerIcon(row);
                _selectedRibbonDesignerDefaultFeature.IsEnabled = hasSelection && RibbonDesignerMapper.IsType(row!, RibbonDesignerNodeRow.SplitButton);

                _selectedRibbonDesignerText.Text = hasSelection ? row!.Text : string.Empty;
                _selectedRibbonDesignerType.SelectedValue = canEditItemType ? row!.NodeType : null;
                SetRibbonDesignerIconSelectorText(CanEditRibbonDesignerIcon(row) ? row!.IconPath : string.Empty);
                var defaultFeatureRows = hasSelection ? RibbonDesignerDefaultFeatureRows(row!).ToList() : new List<RibbonDesignerFeatureRow>();
                _selectedRibbonDesignerDefaultFeature.ItemsSource = defaultFeatureRows;
                _selectedRibbonDesignerDefaultFeature.SelectedValue = hasSelection ? row!.DefaultFeatureId : string.Empty;
            }
            finally
            {
                _syncingSelectedRibbonDesignerEditor = false;
            }
        }

        private static bool CanEditRibbonDesignerDisplayName(RibbonDesignerNodeRow? row)
        {
            return row != null
                && (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Panel)
                    || RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PushButton));
        }

        private static bool CanEditRibbonDesignerIcon(RibbonDesignerNodeRow? row)
        {
            return row != null && RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PushButton);
        }

        private IEnumerable<RibbonDesignerFeatureRow> RibbonDesignerDefaultFeatureRows(RibbonDesignerNodeRow row)
        {
            var featureIds = _ribbonDesignerDropService
                .Flatten(new[] { row })
                .Where(child => !ReferenceEquals(child, row) && !string.IsNullOrWhiteSpace(child.FeatureId))
                .Select(child => child.FeatureId)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var featureId in featureIds)
            {
                var feature = _viewModel.RibbonDesignerFeatures.FirstOrDefault(item => string.Equals(item.FeatureId, featureId, StringComparison.OrdinalIgnoreCase));
                if (feature != null)
                {
                    yield return feature;
                }
            }
        }

        private void SelectedRibbonDesignerTextLostFocus(object sender, RoutedEventArgs args)
        {
            CommitSelectedRibbonDesignerPropertiesFromEditor();
        }

        private void SelectedRibbonDesignerTextKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Enter) return;
            CommitSelectedRibbonDesignerPropertiesFromEditor();
            args.Handled = true;
        }

        private void SelectedRibbonDesignerPropertySelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            CommitSelectedRibbonDesignerPropertiesFromEditor();
        }

        private void CommitSelectedRibbonDesignerPropertiesFromEditor()
        {
            if (_syncingSelectedRibbonDesignerEditor) return;
            if (!CommitSelectedRibbonDesignerProperties(true)) return;
            RefreshRibbonDesignerAfterLayoutChange("已自动更新布局属性，保存并重启 Revit 后生效。");
        }

        private void CommitSelectedRibbonDesignerPropertiesBeforeCanvasInteraction()
        {
            if (_syncingSelectedRibbonDesignerEditor) return;
            CommitSelectedRibbonDesignerProperties(false);
        }

        private string SelectedRibbonDesignerIconValue()
        {
            var text = (_selectedRibbonDesignerIcon.Text ?? string.Empty).Trim();
            if (_selectedRibbonDesignerIcon.SelectedItem is IconOption selectedOption)
            {
                if (IsRibbonDesignerIconAction(selectedOption.Value)) return string.Empty;
                return string.Equals(text, selectedOption.DisplayText, StringComparison.OrdinalIgnoreCase)
                    ? selectedOption.Value
                    : text;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var selectedValue = Convert.ToString(_selectedRibbonDesignerIcon.SelectedValue);
            if (!string.IsNullOrWhiteSpace(selectedValue))
            {
                return selectedValue;
            }

            return string.Empty;
        }

        private void SetRibbonDesignerIconSelectorText(string iconPath)
        {
            if (IsBuiltinIconValue(iconPath))
            {
                _selectedRibbonDesignerIcon.SelectedValue = iconPath;
                return;
            }

            _selectedRibbonDesignerIcon.SelectedIndex = -1;
            _selectedRibbonDesignerIcon.Text = iconPath ?? string.Empty;
        }

        private void ApplySelectedRibbonDesignerProperties()
        {
            if (!CommitSelectedRibbonDesignerProperties(true))
            {
                return;
            }

            RefreshRibbonDesignerAfterLayoutChange("已更新布局属性，保存并重启 Revit 后生效。");
        }

        private bool CommitSelectedRibbonDesignerProperties(bool showStatus)
        {
            if (_syncingSelectedRibbonDesignerEditor) return false;
            var row = _viewModel.SelectedRibbonDesignerNode;
            if (row == null)
            {
                if (showStatus) RefreshStatus("请先选择一个布局项。");
                return false;
            }

            var requestedText = _selectedRibbonDesignerText.Text ?? string.Empty;
            if (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Tab) || RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Panel))
            {
                if (CanEditRibbonDesignerDisplayName(row))
                {
                    row.Text = requestedText;
                }

                return true;
            }

            var requestedType = Convert.ToString(_selectedRibbonDesignerType.SelectedValue) ?? row.NodeType;
            if (!ApplyRibbonDesignerNodeType(row, requestedType, showStatus))
            {
                return false;
            }

            if (CanEditRibbonDesignerDisplayName(row))
            {
                row.Text = requestedText;
            }
            else if (string.IsNullOrWhiteSpace(row.Text))
            {
                row.Text = RibbonNodeTypeDisplayName(row.NodeType);
            }

            row.Size = InferredRibbonDesignerButtonSize(FindRibbonDesignerParent(row) ?? row, row);
            row.IconPath = CanEditRibbonDesignerIcon(row) ? SelectedRibbonDesignerIconValue() : string.Empty;
            row.DefaultFeatureId = RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.SplitButton)
                ? ValidSplitDefaultFeatureId(row, Convert.ToString(_selectedRibbonDesignerDefaultFeature.SelectedValue) ?? row.DefaultFeatureId)
                : string.Empty;
            row.RequiresRestart = true;
            row.StatusText = "需重启";
            return true;
        }

        private bool ApplyRibbonDesignerNodeType(RibbonDesignerNodeRow row, string requestedType, bool showStatus)
        {
            if (string.Equals(row.NodeType, requestedType, StringComparison.OrdinalIgnoreCase)) return true;
            var previousExpansionKey = RibbonDesignerNodeKey(row);
            if (!CanConvertRibbonDesignerNodeType(row, requestedType, out var message))
            {
                if (showStatus) RefreshStatus(message);
                return false;
            }

            if (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PushButton)
                && !string.Equals(requestedType, RibbonDesignerNodeRow.PushButton, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(row.FeatureId))
            {
                var child = new RibbonDesignerNodeRow
                {
                    NodeType = RibbonDesignerNodeRow.PushButton,
                    Id = row.FeatureId,
                    Text = DisplayName(row.Text, row.FeatureId, row.FeatureId),
                    FeatureId = row.FeatureId,
                    Size = NormalizeButtonSize(row.Size),
                    IconPath = row.IconPath,
                    Order = 100,
                    RequiresRestart = true,
                    StatusText = "需重启"
                };
                row.Children.Add(child);
                row.FeatureId = string.Empty;
                row.NodeType = requestedType;
                row.IconPath = string.Empty;
                row.DefaultFeatureId = string.Equals(requestedType, RibbonDesignerNodeRow.SplitButton, StringComparison.OrdinalIgnoreCase) ? child.FeatureId : string.Empty;
                _expandedRibbonDesignerNodeIds.Remove(previousExpansionKey);
                if (IsExpandableRibbonDesignerContainer(row))
                {
                    _expandedRibbonDesignerNodeIds.Add(RibbonDesignerNodeKey(row));
                }

                return true;
            }

            if (string.Equals(requestedType, RibbonDesignerNodeRow.PushButton, StringComparison.OrdinalIgnoreCase) && row.Children.Count > 0)
            {
                if (showStatus) RefreshStatus("包含子项的复合控件不能直接改为常规按钮，请先移除或移动子项。");
                return false;
            }

            if (string.Equals(requestedType, RibbonDesignerNodeRow.PushButton, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(row.FeatureId))
            {
                if (showStatus) RefreshStatus("空容器不能直接改为常规按钮，请先把一个功能移动到该控件中。");
                return false;
            }

            if (!CanKeepDesignerChildren(row, requestedType))
            {
                if (showStatus) RefreshStatus("当前子项不符合目标控件类型，请先调整子项后再切换类型。");
                return false;
            }

            row.NodeType = requestedType;
            _expandedRibbonDesignerNodeIds.Remove(previousExpansionKey);
            if (IsExpandableRibbonDesignerContainer(row))
            {
                _expandedRibbonDesignerNodeIds.Add(RibbonDesignerNodeKey(row));
            }
            else
            {
                _expandedRibbonDesignerNodeIds.Remove(RibbonDesignerNodeKey(row));
            }

            return true;
        }

        private bool CanConvertRibbonDesignerNodeType(RibbonDesignerNodeRow row, string requestedType, out string message)
        {
            message = string.Empty;
            var parent = FindRibbonDesignerParent(row);
            if (parent != null
                && RibbonDesignerMapper.IsType(parent, RibbonDesignerNodeRow.Stack)
                && string.Equals(requestedType, RibbonDesignerNodeRow.Stack, StringComparison.OrdinalIgnoreCase))
            {
                message = "不能在堆叠中嵌套堆叠。堆叠只能包含常规按钮、下拉按钮或拆分按钮。";
                return false;
            }

            if (parent != null
                && (RibbonDesignerMapper.IsType(parent, RibbonDesignerNodeRow.PulldownButton)
                    || RibbonDesignerMapper.IsType(parent, RibbonDesignerNodeRow.SplitButton))
                && !string.Equals(requestedType, RibbonDesignerNodeRow.PushButton, StringComparison.OrdinalIgnoreCase))
            {
                message = "下拉按钮和拆分按钮内部只能放常规按钮，不能继续嵌套容器。";
                return false;
            }

            return true;
        }

        private static bool CanKeepDesignerChildren(RibbonDesignerNodeRow row, string requestedType)
        {
            if (string.Equals(requestedType, RibbonDesignerNodeRow.PushButton, StringComparison.OrdinalIgnoreCase))
            {
                return row.Children.Count == 0;
            }

            if (string.Equals(requestedType, RibbonDesignerNodeRow.Stack, StringComparison.OrdinalIgnoreCase))
            {
                return row.Children.Count <= 3
                    && row.Children.All(child =>
                        RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.PushButton)
                        || RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.PulldownButton)
                        || RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.SplitButton));
            }

            if (string.Equals(requestedType, RibbonDesignerNodeRow.PulldownButton, StringComparison.OrdinalIgnoreCase)
                || string.Equals(requestedType, RibbonDesignerNodeRow.SplitButton, StringComparison.OrdinalIgnoreCase))
            {
                return row.Children.All(child => RibbonDesignerMapper.IsType(child, RibbonDesignerNodeRow.PushButton));
            }

            return true;
        }

        private string ValidSplitDefaultFeatureId(RibbonDesignerNodeRow row, string candidate)
        {
            var featureIds = RibbonDesignerDefaultFeatureRows(row).Select(feature => feature.FeatureId).ToList();
            if (featureIds.Contains(candidate, StringComparer.OrdinalIgnoreCase)) return candidate;
            return featureIds.FirstOrDefault() ?? string.Empty;
        }

        private void RemoveSelectedRibbonDesignerNode()
        {
            var row = _viewModel.SelectedRibbonDesignerNode;
            if (row == null)
            {
                RefreshStatus("请先选择一个布局项。");
                return;
            }

            if (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Tab))
            {
                RefreshStatus("不能移除 Ribbon 页签。");
                return;
            }

            if (!CanRemoveRibbonDesignerNode(row))
            {
                RefreshStatus("常规按钮不能移除，只能拖动位置。");
                return;
            }

            if (IsDefaultRibbonDesignerPanel(row) && row.Children.Count > 0)
            {
                RefreshStatus("默认面板包含自动维护的功能，不能直接移除。");
                return;
            }

            MoveRibbonDesignerChildrenToDefaultPanel(row);
            if (RemoveRibbonDesignerNode(_viewModel.RibbonDesignerTabs, row))
            {
                SelectRibbonDesignerNode(_viewModel.RibbonDesignerTabs.FirstOrDefault()?.Children.FirstOrDefault());
                RefreshRibbonDesignerAfterLayoutChange("已移除布局项，保存并重启 Revit 后生效。");
            }
        }

        private bool CanRemoveRibbonDesignerNode(RibbonDesignerNodeRow row)
        {
            return row != null
                && !RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Tab)
                && !RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PushButton);
        }

        private bool IsDefaultRibbonDesignerPanel(RibbonDesignerNodeRow row)
        {
            return RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Panel)
                && (string.Equals(row.Id, DefaultRibbonDesignerPanelId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.Text, DefaultRibbonDesignerPanelName, StringComparison.OrdinalIgnoreCase));
        }

        private void MoveRibbonDesignerChildrenToDefaultPanel(RibbonDesignerNodeRow row)
        {
            var featureNodes = CollectRibbonDesignerFeatureNodes(row)
                .Where(node => !string.IsNullOrWhiteSpace(node.FeatureId))
                .ToList();
            if (featureNodes.Count == 0)
            {
                return;
            }

            var defaultPanel = EnsureDefaultRibbonDesignerPanel();
            foreach (var featureNode in featureNodes)
            {
                defaultPanel.Children.Add(CloneRibbonDesignerFeatureNode(featureNode, (defaultPanel.Children.Count + 1) * 100));
            }
        }

        private IEnumerable<RibbonDesignerNodeRow> CollectRibbonDesignerFeatureNodes(RibbonDesignerNodeRow row)
        {
            if (row == null)
            {
                yield break;
            }

            if (RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PushButton) && !string.IsNullOrWhiteSpace(row.FeatureId))
            {
                yield return row;
            }

            foreach (var child in row.Children)
            {
                foreach (var featureNode in CollectRibbonDesignerFeatureNodes(child))
                {
                    yield return featureNode;
                }
            }
        }

        private static RibbonDesignerNodeRow CloneRibbonDesignerFeatureNode(RibbonDesignerNodeRow source, int order)
        {
            return new RibbonDesignerNodeRow
            {
                NodeType = RibbonDesignerNodeRow.PushButton,
                Id = source.Id,
                Text = source.Text,
                FeatureId = source.FeatureId,
                Size = "large",
                IconPath = source.IconPath,
                Order = order,
                RequiresRestart = true,
                StatusText = "需重启"
            };
        }

        private static bool RemoveRibbonDesignerNode(IList<RibbonDesignerNodeRow> roots, RibbonDesignerNodeRow target)
        {
            if (roots.Remove(target)) return true;
            foreach (var root in roots)
            {
                if (RemoveRibbonDesignerNode(root.Children, target)) return true;
            }

            return false;
        }

        private void RefreshRibbonDesignerAfterLayoutChange(string status)
        {
            RefreshRibbonDesignerLayoutState();
            RefreshRibbonDesignerCanvas();
            SyncSelectedRibbonDesignerEditor();
            var message = RibbonDesignerChangeSummaryMessage();
            RefreshStatus(string.IsNullOrWhiteSpace(status) ? message : status + " " + message);
        }

        private void LoadRepositoryRows()
        {
            _repositoriesGrid.Columns.Clear();
            _repositoriesGrid.Columns.Add(CheckColumn(nameof(RepositoryRow.Enabled), "启用"));
            _repositoriesGrid.Columns.Add(ComboColumn(nameof(RepositoryRow.Provider), "类型", new[] { "github", "gitee" }, 0.8));
            _repositoriesGrid.Columns.Add(ComboColumn(nameof(RepositoryRow.Visibility), "可见性", new[] { "public", "private" }, 0.9));
            _repositoriesGrid.Columns.Add(TextColumn(nameof(RepositoryRow.Repository), "仓库", false, 1.7));
            _repositoriesGrid.Columns.Add(TextColumn(nameof(RepositoryRow.Ref), "分支", false, 0.7));
            _repositoriesGrid.Columns.Add(TextColumn(nameof(RepositoryRow.ApiKey), "私有 ApiKey", false, 1.1));
            _repositoriesGrid.Columns.Add(TextColumn(nameof(RepositoryRow.Status), "状态", true, 1.4));

            _viewModel.Repositories.Clear();
            foreach (var row in (_configuration.Modules.Repositories ?? new List<PackageRepositoryConfiguration>())
                .Select(repository => new RepositoryRow
                {
                    Id = repository.Id,
                    Enabled = repository.Enabled,
                    Provider = string.IsNullOrWhiteSpace(repository.Provider) ? DefaultRepositoryProvider : repository.Provider,
                    Visibility = string.Equals(repository.Visibility, "private", StringComparison.OrdinalIgnoreCase) ? "private" : "public",
                    Repository = repository.Repository,
                    Ref = string.IsNullOrWhiteSpace(repository.Ref) ? "main" : repository.Ref,
                    ManifestPath = string.IsNullOrWhiteSpace(repository.ManifestPath) ? DefaultPackageManifestName : repository.ManifestPath,
                    ApiKey = string.Empty,
                    PlainApiKey = repository.ApiKey,
                    EncryptedApiKey = repository.EncryptedApiKey,
                    ApiKeyProtection = repository.ApiKeyProtection,
                    Status = repository.Enabled ? "可浏览" : "停用"
                }))
            {
                _viewModel.Repositories.Add(row);
            }
            _repositoriesGrid.ItemsSource = _viewModel.Repositories;
        }

        private void LoadRepositoryPackageRows(IEnumerable<RepositoryPackageDescriptor> packages)
        {
            _repositoryPackagesGrid.Columns.Clear();
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.RepositoryId), "仓库", true, 1.0));
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.PackageId), "插件包 ID", true, 1.4));
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.DisplayName), "功能", true, 1.8));
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.Version), "版本", true, 0.8));
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.InstallState), "安装状态", true, 0.9));

            _viewModel.RepositoryPackages.Clear();
            foreach (var row in (packages ?? new List<RepositoryPackageDescriptor>())
                .Select(package => RepositoryPackageRow.FromDescriptor(package, IsLoadedInCurrentRuntime(package.PackageId, package.ModuleId))))
            {
                _viewModel.RepositoryPackages.Add(row);
            }
            _repositoryPackagesGrid.ItemsSource = _viewModel.RepositoryPackages;
        }

        private void LoadPendingPackageOperationRows()
        {
            _pendingPackageOperationsGrid.Columns.Clear();
            _pendingPackageOperationsGrid.Columns.Add(TextColumn(nameof(PendingPackageOperationRow.Operation), "操作", true, 0.6));
            _pendingPackageOperationsGrid.Columns.Add(TextColumn(nameof(PendingPackageOperationRow.PackageId), "插件包", true, 1.2));
            _pendingPackageOperationsGrid.Columns.Add(TextColumn(nameof(PendingPackageOperationRow.CreatedAtUtc), "创建时间", true, 1.0));

            _viewModel.PendingOperations.Clear();
            foreach (var row in _packageRepositoryService.ListPendingOperations(BaseDirectory()).Select(PendingPackageOperationRow.FromOperation))
            {
                _viewModel.PendingOperations.Add(row);
            }
            _pendingPackageOperationsGrid.ItemsSource = _viewModel.PendingOperations;
        }

        private void LoadCachedRepositoryPackages()
        {
            var repositories = EnabledRepositoriesWithCache().ToList();
            if (repositories.Count == 0) return;

            var packages = new List<RepositoryPackageDescriptor>();
            var messages = new List<DiagnosticMessage>();
            foreach (var repository in repositories)
            {
                var cachedPackages = _packageRepositoryService.BrowseCached(BaseDirectory(), repository.ToConfiguration(), out var diagnostics);
                packages.AddRange(cachedPackages);
                messages.AddRange(diagnostics);
                repository.Status = cachedPackages.Count > 0 ? "已从本地缓存加载 " + cachedPackages.Count + " 个插件" : "本地缓存无插件";
            }

            if (packages.Count > 0)
            {
                LoadRepositoryPackageRows(packages);
                RefreshStatus("已从本地仓库缓存加载 " + packages.Count + " 个插件，正在后台检查更新。");
            }

            if (messages.Count > 0)
            {
                LoadDiagnosticRowsFromMessages(messages);
            }

            SafeRefreshGrid(_repositoriesGrid);
        }

        private void StartRepositoryUpdateCheck()
        {
            var repositories = EnabledRepositoriesWithCache()
                .Select(row => row.ToConfiguration())
                .ToList();
            if (repositories.Count == 0) return;

            var baseDirectory = BaseDirectory();
            Task.Run(() =>
            {
                var packages = new List<RepositoryPackageDescriptor>();
                var messages = new List<DiagnosticMessage>();
                try
                {
                    foreach (var repository in repositories)
                    {
                        var repositoryPackages = _packageRepositoryService.Browse(baseDirectory, repository, out var diagnostics);
                        packages.AddRange(repositoryPackages);
                        messages.AddRange(diagnostics);
                    }
                }
                catch (Exception ex)
                {
                    messages.Add(new DiagnosticMessage
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Code = "PH-REPOSITORY-BACKGROUND",
                        ModuleId = "repository",
                        Message = ex.Message
                    });
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (packages.Count > 0)
                    {
                        LoadRepositoryPackageRows(packages);
                    }

                    if (messages.Count > 0)
                    {
                        LoadDiagnosticRowsFromMessages(messages);
                    }

                    foreach (var row in _viewModel.Repositories)
                    {
                        var count = packages.Count(package => string.Equals(package.RepositoryId, row.Id, StringComparison.OrdinalIgnoreCase));
                        if (count > 0)
                        {
                            row.Status = "后台检查完成，" + count + " 个插件";
                        }
                    }

                    SafeRefreshGrid(_repositoriesGrid);
                    RefreshStatus("仓库后台更新检查完成。");
                }));
            });
        }

        private IEnumerable<RepositoryRow> EnabledRepositoriesWithCache()
        {
            var baseDirectory = BaseDirectory();
            return _viewModel.Repositories
                .Where(row => row.Enabled)
                .Where(row => _packageRepositoryService.HasRepositoryCache(baseDirectory, row.ToConfiguration()))
                .ToList();
        }

        private void LoadDiagnosticRows(FrameworkRuntimeSnapshot? snapshot)
        {
            _diagnosticsGrid.Columns.Clear();
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Severity), "级别", true, 0.8));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Code), "代码", true, 1.1));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Scope), "对象", true, 1.4));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Message), "消息", true, 4.6));

            var rows = (snapshot?.Diagnostics ?? new List<DiagnosticMessage>())
                .Select(message => new DiagnosticRow
                {
                    Severity = message.Severity.ToString(),
                    Code = message.Code,
                    Scope = message.ModuleId,
                    Message = message.Message
                })
                .ToList();

            if (rows.Count == 0)
            {
                rows.Add(new DiagnosticRow
                {
                    Severity = "Info",
                    Code = "PH-OK",
                    Scope = "runtime",
                    Message = "当前没有日志消息。"
                });
            }

            SetDiagnosticRows(rows);
        }

        private void LoadDiagnosticRowsFromMessages(IReadOnlyList<DiagnosticMessage> messages)
        {
            _diagnosticsGrid.Columns.Clear();
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Severity), "级别", true, 0.8));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Code), "代码", true, 1.1));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Scope), "对象", true, 1.4));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Message), "消息", true, 4.6));

            var rows = (messages ?? new List<DiagnosticMessage>())
                .Select(message => new DiagnosticRow
                {
                    Severity = message.Severity.ToString(),
                    Code = message.Code,
                    Scope = message.ModuleId,
                    Message = message.Message
                })
                .ToList();

            if (rows.Count == 0)
            {
                rows.Add(new DiagnosticRow
                {
                    Severity = "Info",
                    Code = "PH-REPOSITORY-OK",
                    Scope = "repository",
                    Message = "仓库浏览完成。"
                });
            }

            SetDiagnosticRows(rows);
        }

        private void LoadPostSaveDiagnosticRows()
        {
            _diagnosticsGrid.Columns.Clear();
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Severity), "级别", true, 0.8));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Code), "代码", true, 1.1));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Scope), "对象", true, 1.4));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Message), "消息", true, 4.6));
            SetDiagnosticRows(new[]
            {
                new DiagnosticRow
                {
                    Severity = "Info",
                    Code = "PH-SAVED",
                    Scope = "settings",
                    Message = "配置已保存。仓库设置和布局会在重启 Revit 后重新加载；此处不显示保存前的运行时日志。"
                }
            });
        }

        private void SetDiagnosticRows(IEnumerable<DiagnosticRow> rows)
        {
            _viewModel.Diagnostics.Clear();
            foreach (var row in rows)
            {
                _viewModel.Diagnostics.Add(row);
            }

            _diagnosticsGrid.ItemsSource = _viewModel.Diagnostics;
        }

        private void TrySave()
        {
            try
            {
                Save();
            }
            catch (Exception ex)
            {
                ReportSettingsError("保存配置失败", ex);
            }
        }

        private void Save()
        {
            EndGridEdits();
            CommitSelectedRibbonDesignerProperties(false);
            RefreshRibbonDesignerChangeSummary();
            ApplyFeatureRows();
            ApplyRibbonLayoutRows();
            ApplyRepositoryRows();

            _configurationStore.Save(_configuration, _moduleDocuments);
            _originalRibbonDesignerTabs = RibbonDesignerMapper.CloneTabs(_viewModel.RibbonDesignerTabs);
            RefreshRibbonDesignerChangeSummary();

            LoadPostSaveDiagnosticRows();
            LoadRepositoryRows();
            RefreshStatus("已保存配置。布局和仓库设置已写回；布局和图标需重启 Revit 重绘。");
        }

        private void ReloadFromDisk()
        {
            try
            {
                _configuration = _configurationStore.LoadConfiguration();
                _moduleDocuments = _configurationStore.LoadModuleDocuments(_configuration);
                LoadRows();
                RefreshStatus("已从配置文件重新加载。");
            }
            catch (Exception ex)
            {
                ReportSettingsError("重新加载失败", ex);
            }
        }

        private void ApplyPluginPackageRows()
        {
            var rows = _viewModel.Modules.ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var module in EditableModules())
            {
                if (!rows.TryGetValue(module.Id, out var row)) continue;
                module.DisplayName = row.DisplayName ?? string.Empty;
                module.Enabled = row.Enabled;
                module.Visible = row.Visible;
                module.SourceId = row.SourceId ?? string.Empty;
                module.Order = row.Order;
            }
        }

        private void ApplyGroupRows()
        {
            var view = WorkspaceView();
            view.Groups = _viewModel.Groups
                .Where(row => !string.IsNullOrWhiteSpace(row.Id))
                .Select(row => new ViewGroupConfiguration
                {
                    Id = row.Id.Trim(),
                    Name = DisplayName(row.Name, row.Id, row.Id),
                    Description = string.Empty,
                    IncludeCategories = new List<string>(),
                    IncludeTags = new List<string>(),
                    Order = row.Order,
                    Presentation = "panel"
                })
                .ToList();
        }

        private void ApplyFeatureRows()
        {
            var modulesById = EditableModules()
                .GroupBy(module => module.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            foreach (var module in modulesById.Values)
            {
                module.Features = new List<FeatureConfiguration>();
            }

            foreach (var row in _viewModel.Features)
            {
                if (!modulesById.TryGetValue(row.ModuleId, out var module))
                {
                    continue;
                }

                var feature = row.ToConfiguration();
                EnsureViewGroupForFeature(module, feature);
                module.Features.Add(feature);
                row.OriginalModuleId = row.ModuleId;
            }

            foreach (var module in modulesById.Values)
            {
                module.Features = module.Features
                    .OrderBy(feature => feature.Group, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(feature => feature.Order)
                    .ThenBy(feature => DisplayName(feature.DisplayName, feature.Name, feature.Id), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(feature => feature.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        private void ApplyRibbonLayoutRows()
        {
            RefreshRibbonDesignerLayoutState();
            NormalizeInvalidRibbonDesignerStacksForSave(_viewModel.RibbonDesignerTabs);
            ValidateNoNestedRibbonDesignerStacks();
            ValidateUniqueRibbonFeaturePlacement();
            if (_viewModel.SelectedRibbonDesignerNode == null)
            {
                _viewModel.SelectedRibbonDesignerNode = _viewModel.RibbonDesignerTabs.FirstOrDefault()?.Children.FirstOrDefault();
            }

            var view = WorkspaceView();
            if (view.Ribbon == null)
            {
                view.Ribbon = new RibbonConfiguration { TabName = "PlugHub", FallbackPanelName = "其他工具" };
            }

            view.Ribbon.LayoutVersion = "1.0";
            view.Ribbon.Panels = _ribbonDesignerMapper.ToPanels(_viewModel.RibbonDesignerTabs);
            InitializeRibbonDesignerContainerExpansionState();
            RefreshRibbonDesignerCanvas();
            SyncSelectedRibbonDesignerEditor();
        }

        private void ApplyRepositoryRows()
        {
            _configuration.Modules.Repositories = _viewModel.Repositories
                .Where(row => !string.IsNullOrWhiteSpace(row.Id))
                .Select(row =>
                {
                    var repository = row.ToConfiguration();
                    _credentialService.ProtectForSave(repository);
                    return repository;
                })
                .ToList();
        }

        private List<RibbonLayoutNodeRow> CreateDefaultRibbonLayoutNodes()
        {
            var result = new List<RibbonLayoutNodeRow>();
            var placedFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var features = _viewModel.Features
                .Where(feature => feature.Visible && !string.IsNullOrWhiteSpace(feature.FeatureId))
                .OrderBy(feature => DisplayName(feature.ModuleName, feature.ModuleId, "默认工具"), StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.Order)
                .ThenBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var moduleGroup in features.GroupBy(DefaultRibbonPanelKey, StringComparer.OrdinalIgnoreCase))
            {
                var panelIndex = result.Count + 1;
                var firstFeature = moduleGroup.First();
                var panel = new RibbonLayoutNodeRow
                {
                    NodeType = "panel",
                    Id = "default-panel-" + panelIndex,
                    Text = DefaultRibbonPanelName(firstFeature),
                    Order = panelIndex * 100
                };

                foreach (var feature in moduleGroup
                    .OrderBy(feature => feature.Order)
                    .ThenBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase))
                {
                    if (!placedFeatureIds.Add(feature.FeatureId))
                    {
                        continue;
                    }

                    panel.Children.Add(CreateRibbonFeatureNode(feature, panel.Children.Count + 1));
                }

                if (panel.Children.Count > 0)
                {
                    result.Add(panel);
                }
            }

            return result;
        }

        private static string DefaultRibbonPanelKey(FeatureRow feature)
        {
            return DefaultRibbonPanelName(feature).Trim();
        }

        private static string DefaultRibbonPanelName(FeatureRow feature)
        {
            return DisplayName(feature.ModuleName, feature.ModuleId, "默认工具");
        }

        private static List<RibbonLayoutNodeRow> MergeRibbonPanelsByDisplayName(IEnumerable<RibbonLayoutNodeRow> panels)
        {
            var result = new List<RibbonLayoutNodeRow>();
            var panelsByName = new Dictionary<string, RibbonLayoutNodeRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var panel in panels ?? new List<RibbonLayoutNodeRow>())
            {
                var key = DisplayName(panel.Text, panel.Id, "默认工具").Trim();
                if (panelsByName.TryGetValue(key, out var existing))
                {
                    var existingFeatureIds = new HashSet<string>(CollectRibbonFeatureIds(existing.Children), StringComparer.OrdinalIgnoreCase);
                    foreach (var child in panel.Children)
                    {
                        if (!string.IsNullOrWhiteSpace(child.FeatureId) && !existingFeatureIds.Add(child.FeatureId))
                        {
                            continue;
                        }

                        existing.Children.Add(child);
                    }

                    continue;
                }

                panelsByName[key] = panel;
                result.Add(panel);
            }

            return result;
        }

        private static IEnumerable<string> CollectRibbonFeatureIds(IEnumerable<RibbonLayoutNodeRow> rows)
        {
            foreach (var row in rows ?? new List<RibbonLayoutNodeRow>())
            {
                if (!string.IsNullOrWhiteSpace(row.FeatureId))
                {
                    yield return row.FeatureId;
                }

                foreach (var childFeatureId in CollectRibbonFeatureIds(row.Children))
                {
                    yield return childFeatureId;
                }
            }
        }

        private void ResetDefaultRibbonLayout()
        {
            _viewModel.RibbonDesignerTabs.Clear();
            _viewModel.RibbonDesignerTabs.Add(new RibbonDesignerNodeRow
            {
                NodeType = RibbonDesignerNodeRow.Tab,
                Id = "tab",
                Text = DisplayName(WorkspaceView().Ribbon?.TabName ?? string.Empty, "PlugHub", "PlugHub"),
                Order = 100
            });
            RefreshRibbonDesignerLayoutState();

            SelectRibbonDesignerNode(_viewModel.RibbonDesignerTabs.FirstOrDefault()?.Children.FirstOrDefault());
            RefreshRibbonDesignerAfterLayoutChange("已按当前已安装功能重新生成默认布局，保存并重启 Revit 后生效。");
        }

        private void NormalizeRibbonLayoutFeatureBindings(IEnumerable<RibbonLayoutNodeRow> rows)
        {
            foreach (var row in rows ?? new List<RibbonLayoutNodeRow>())
            {
                NormalizeRibbonNodeFeatureBinding(row);
                NormalizeRibbonLayoutFeatureBindings(row.Children);
            }
        }

        private static void NormalizeDefaultRibbonFeatureIds(IEnumerable<RibbonLayoutNodeRow> rows)
        {
            foreach (var row in rows ?? new List<RibbonLayoutNodeRow>())
            {
                if (string.Equals(row.NodeType, "splitButton", StringComparison.OrdinalIgnoreCase))
                {
                    var childFeatureIds = row.Children
                        .Where(child => string.Equals(child.NodeType, "pushButton", StringComparison.OrdinalIgnoreCase))
                        .Select(child => child.FeatureId)
                        .Where(featureId => !string.IsNullOrWhiteSpace(featureId))
                        .ToList();
                    if (!childFeatureIds.Contains(row.DefaultFeatureId, StringComparer.OrdinalIgnoreCase))
                    {
                        row.DefaultFeatureId = childFeatureIds.FirstOrDefault() ?? string.Empty;
                    }
                }
                else
                {
                    row.DefaultFeatureId = string.Empty;
                }

                NormalizeDefaultRibbonFeatureIds(row.Children);
            }
        }

        private void NormalizeRibbonNodeFeatureBinding(RibbonLayoutNodeRow row)
        {
            if (row == null || IsRibbonNodeType(row, "pushButton") || string.IsNullOrWhiteSpace(row.FeatureId))
            {
                return;
            }

            ConvertFeatureNodeToRibbonContainer(row);
        }

        private void ConvertFeatureNodeToRibbonContainer(RibbonLayoutNodeRow row)
        {
            var featureId = row.FeatureId.Trim();
            if (string.IsNullOrWhiteSpace(featureId))
            {
                return;
            }

            if (FeatureIdExistsInRibbonLayout(row.Children, featureId, null))
            {
                row.FeatureId = string.Empty;
                return;
            }

            if (IsRibbonNodeType(row, "stack") && row.Children.Count >= 3)
            {
                RefreshStatus("堆叠控件最多包含 3 个子项，无法把原功能自动转为子按钮。");
                return;
            }

            var feature = _viewModel.Features.FirstOrDefault(item => string.Equals(item.FeatureId, featureId, StringComparison.OrdinalIgnoreCase));
            var child = feature == null
                ? new RibbonLayoutNodeRow
                {
                    NodeType = "pushButton",
                    Id = featureId,
                    Text = DisplayName(row.Text, featureId, featureId),
                    FeatureId = featureId,
                    Size = NormalizeButtonSize(row.Size),
                    IconPath = row.IconPath,
                    Order = 100
                }
                : CreateRibbonFeatureNode(feature, 1);

            child.Text = DisplayName(row.Text, child.Text, featureId);
            child.Size = NormalizeButtonSize(row.Size);
            if (!string.IsNullOrWhiteSpace(row.IconPath))
            {
                child.IconPath = row.IconPath;
            }

            row.FeatureId = string.Empty;
            row.Children.Insert(0, child);
            if (IsRibbonNodeType(row, "splitButton") && string.IsNullOrWhiteSpace(row.DefaultFeatureId))
            {
                row.DefaultFeatureId = featureId;
            }
        }

        private static bool IsRibbonNodeType(RibbonLayoutNodeRow row, string type)
        {
            return row != null && string.Equals(row.NodeType, type, StringComparison.OrdinalIgnoreCase);
        }

        private RibbonLayoutNodeRow CreateRibbonFeatureNode(FeatureRow feature, int index)
        {
            return new RibbonLayoutNodeRow
            {
                NodeType = "pushButton",
                Id = feature.FeatureId,
                Text = DisplayName(feature.DisplayName, feature.Name, feature.FeatureId),
                FeatureId = feature.FeatureId,
                Size = NormalizeButtonSize(feature.ButtonSize),
                IconPath = feature.IconPath,
                Order = index * 100
            };
        }

        private bool FeatureIdExistsInRibbonLayout(string featureId)
        {
            return FeatureIdExistsInRibbonLayout(featureId, null);
        }

        private bool FeatureIdExistsInRibbonLayout(string featureId, RibbonDesignerNodeRow? excludedNode)
        {
            if (string.IsNullOrWhiteSpace(featureId)) return false;
            return FeatureIdExistsInRibbonLayout(_viewModel.RibbonDesignerTabs, featureId, excludedNode);
        }

        private void NormalizeInvalidRibbonDesignerStacksForSave(IList<RibbonDesignerNodeRow> rows)
        {
            for (var index = rows.Count - 1; index >= 0; index--)
            {
                var row = rows[index];
                NormalizeInvalidRibbonDesignerStacksForSave(row.Children);
                if (!RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Stack))
                {
                    continue;
                }

                if (row.Children.Count == 0)
                {
                    if (ReferenceEquals(_viewModel.SelectedRibbonDesignerNode, row))
                    {
                        _viewModel.SelectedRibbonDesignerNode = null;
                    }

                    rows.RemoveAt(index);
                    continue;
                }

                if (row.Children.Count == 1)
                {
                    var onlyChild = row.Children[0];
                    onlyChild.Order = row.Order;
                    rows[index] = onlyChild;
                    if (ReferenceEquals(_viewModel.SelectedRibbonDesignerNode, row))
                    {
                        _viewModel.SelectedRibbonDesignerNode = onlyChild;
                    }
                }
            }
        }

        private void ValidateNoNestedRibbonDesignerStacks()
        {
            var nestedStack = FindNestedRibbonDesignerStack(_viewModel.RibbonDesignerTabs, false);
            if (nestedStack != null)
            {
                throw new InvalidOperationException("堆叠控件不能嵌套堆叠: " + DisplayName(nestedStack.Text, nestedStack.Id, "堆叠"));
            }

            var invalidStack = _ribbonDesignerDropService
                .Flatten(_viewModel.RibbonDesignerTabs)
                .FirstOrDefault(row => RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Stack)
                    && row.Children.Count != 2
                    && row.Children.Count != 3);
            if (invalidStack != null)
            {
                throw new InvalidOperationException("堆叠控件需要包含 2 或 3 个按钮: " + DisplayName(invalidStack.Text, invalidStack.Id, "堆叠"));
            }
        }

        private static RibbonDesignerNodeRow? FindNestedRibbonDesignerStack(IEnumerable<RibbonDesignerNodeRow> rows, bool insideStack)
        {
            foreach (var row in rows ?? new List<RibbonDesignerNodeRow>())
            {
                var isStack = RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Stack);
                if (insideStack && isStack)
                {
                    return row;
                }

                var nestedStack = FindNestedRibbonDesignerStack(row.Children, insideStack || isStack);
                if (nestedStack != null)
                {
                    return nestedStack;
                }
            }

            return null;
        }

        private static bool FeatureIdExistsInRibbonLayout(IEnumerable<RibbonDesignerNodeRow> rows, string featureId, RibbonDesignerNodeRow? excludedNode)
        {
            foreach (var row in rows ?? new List<RibbonDesignerNodeRow>())
            {
                if (!ReferenceEquals(row, excludedNode)
                    && !string.IsNullOrWhiteSpace(row.FeatureId)
                    && string.Equals(row.FeatureId, featureId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (FeatureIdExistsInRibbonLayout(row.Children, featureId, excludedNode)) return true;
            }

            return false;
        }

        private static bool FeatureIdExistsInRibbonLayout(IEnumerable<RibbonLayoutNodeRow> rows, string featureId, RibbonLayoutNodeRow? excludedNode)
        {
            foreach (var row in rows ?? new List<RibbonLayoutNodeRow>())
            {
                if (!ReferenceEquals(row, excludedNode)
                    && string.Equals(row.NodeType, "pushButton", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(row.FeatureId, featureId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (FeatureIdExistsInRibbonLayout(row.Children, featureId, excludedNode)) return true;
            }

            return false;
        }

        private void ValidateUniqueRibbonFeaturePlacement()
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            CountRibbonFeatureIds(_viewModel.RibbonDesignerTabs, counts);
            var duplicates = counts.Where(item => item.Value > 1).Select(item => item.Key).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException("布局中存在重复功能: " + string.Join(", ", duplicates));
            }
        }

        private static void CountRibbonFeatureIds(IEnumerable<RibbonDesignerNodeRow> rows, IDictionary<string, int> counts)
        {
            foreach (var row in rows ?? new List<RibbonDesignerNodeRow>())
            {
                if (!string.IsNullOrWhiteSpace(row.FeatureId))
                {
                    counts.TryGetValue(row.FeatureId, out var count);
                    counts[row.FeatureId] = count + 1;
                }

                CountRibbonFeatureIds(row.Children, counts);
            }
        }

        private static void CountRibbonFeatureIds(IEnumerable<RibbonLayoutNodeRow> rows, IDictionary<string, int> counts)
        {
            foreach (var row in rows ?? new List<RibbonLayoutNodeRow>())
            {
                if (string.Equals(row.NodeType, "pushButton", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(row.FeatureId))
                {
                    counts.TryGetValue(row.FeatureId, out var count);
                    counts[row.FeatureId] = count + 1;
                }

                CountRibbonFeatureIds(row.Children, counts);
            }
        }

        private static string RibbonNodeTypeDisplayName(string type)
        {
            if (string.Equals(type, "tab", StringComparison.OrdinalIgnoreCase)) return "页签";
            if (string.Equals(type, "panel", StringComparison.OrdinalIgnoreCase)) return "面板";
            if (string.Equals(type, "pulldownButton", StringComparison.OrdinalIgnoreCase)) return "下拉按钮";
            if (string.Equals(type, "splitButton", StringComparison.OrdinalIgnoreCase)) return "拆分按钮";
            if (string.Equals(type, "stack", StringComparison.OrdinalIgnoreCase)) return "堆叠";
            if (string.Equals(type, "pushButton", StringComparison.OrdinalIgnoreCase)) return "常规按钮";
            return "布局项";
        }

        private List<RibbonDisplayModeOption> RibbonDesignerNodeTypeOptions(RibbonDesignerNodeRow? row)
        {
            var allowedTypes = RibbonDesignerAllowedNodeTypes(row);
            return AllRibbonDesignerNodeTypeOptions()
                .Where(option => allowedTypes.Contains(option.Value, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        private List<string> RibbonDesignerAllowedNodeTypes(RibbonDesignerNodeRow? row)
        {
            var allTypes = new List<string>
            {
                RibbonDesignerNodeRow.PushButton,
                RibbonDesignerNodeRow.PulldownButton,
                RibbonDesignerNodeRow.SplitButton,
                RibbonDesignerNodeRow.Stack
            };

            if (row == null) return allTypes;
            var parent = FindRibbonDesignerParent(row);
            if (parent != null && RibbonDesignerMapper.IsType(parent, RibbonDesignerNodeRow.Stack))
            {
                return new List<string>
                {
                    RibbonDesignerNodeRow.PushButton,
                    RibbonDesignerNodeRow.PulldownButton,
                    RibbonDesignerNodeRow.SplitButton
                };
            }

            if (parent != null
                && (RibbonDesignerMapper.IsType(parent, RibbonDesignerNodeRow.PulldownButton)
                    || RibbonDesignerMapper.IsType(parent, RibbonDesignerNodeRow.SplitButton)))
            {
                return new List<string> { RibbonDesignerNodeRow.PushButton };
            }

            return allTypes;
        }

        private static List<RibbonDisplayModeOption> AllRibbonDesignerNodeTypeOptions()
        {
            return new List<RibbonDisplayModeOption>
            {
                new RibbonDisplayModeOption(RibbonDesignerNodeRow.PushButton, "常规按钮"),
                new RibbonDisplayModeOption(RibbonDesignerNodeRow.PulldownButton, "下拉按钮"),
                new RibbonDisplayModeOption(RibbonDesignerNodeRow.SplitButton, "拆分按钮"),
                new RibbonDisplayModeOption(RibbonDesignerNodeRow.Stack, "堆叠")
            };
        }

        private IEnumerable<ModuleConfiguration> EditableModules()
        {
            return _moduleDocuments.SelectMany(document => document.Modules.Modules ?? new List<ModuleConfiguration>());
        }

        private static DataGridTextColumn TextColumn(string propertyName, string header, bool readOnly, double starWidth)
        {
            return new DataGridTextColumn
            {
                Header = header,
                IsReadOnly = readOnly,
                Binding = new Binding(propertyName) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(starWidth, DataGridLengthUnitType.Star)
            };
        }

        private static DataGridCheckBoxColumn CheckColumn(string propertyName, string header)
        {
            return new DataGridCheckBoxColumn
            {
                Header = header,
                Binding = new Binding(propertyName) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(68)
            };
        }

        private static DataGridComboBoxColumn ComboColumn<T>(string propertyName, string header, IEnumerable<T> values, double starWidth, string displayMemberPath = "", string selectedValuePath = "")
        {
            var column = new DataGridComboBoxColumn
            {
                Header = header,
                ItemsSource = values,
                Width = new DataGridLength(starWidth, DataGridLengthUnitType.Star)
            };

            if (!string.IsNullOrWhiteSpace(displayMemberPath) && !string.IsNullOrWhiteSpace(selectedValuePath))
            {
                column.DisplayMemberPath = displayMemberPath;
                column.SelectedValuePath = selectedValuePath;
                column.SelectedValueBinding = new Binding(propertyName) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
                return column;
            }

            column.SelectedItemBinding = new Binding(propertyName) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            return column;
        }

        private static DataGridTemplateColumn ComboBoxTemplateColumn<T>(
            string propertyName,
            string header,
            IEnumerable<T> values,
            double starWidth,
            string displayMemberPath = "",
            string selectedValuePath = "",
            SelectionChangedEventHandler? selectionChanged = null)
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(ComboBox));
            factory.SetValue(ItemsControl.ItemsSourceProperty, values);
            factory.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch);
            factory.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 2, 0));
            factory.SetValue(ComboBox.IsSynchronizedWithCurrentItemProperty, false);

            if (!string.IsNullOrWhiteSpace(displayMemberPath) && !string.IsNullOrWhiteSpace(selectedValuePath))
            {
                factory.SetValue(ItemsControl.DisplayMemberPathProperty, displayMemberPath);
                factory.SetValue(Selector.SelectedValuePathProperty, selectedValuePath);
                factory.SetBinding(Selector.SelectedValueProperty, new Binding(propertyName) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            }
            else
            {
                factory.SetBinding(Selector.SelectedItemProperty, new Binding(propertyName) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            }

            if (selectionChanged != null)
            {
                factory.AddHandler(Selector.SelectionChangedEvent, selectionChanged);
            }

            template.VisualTree = factory;
            return new DataGridTemplateColumn
            {
                Header = header,
                CellTemplate = template,
                CellEditingTemplate = template,
                Width = new DataGridLength(starWidth, DataGridLengthUnitType.Star)
            };
        }

        private ContextMenu BuildPluginPackageMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("启用并显示", (sender, args) => SetSelectedModuleState(true, true)));
            menu.Items.Add(MenuItem("禁用", (sender, args) => SetSelectedModuleState(false, false)));
            menu.Items.Add(MenuItem("仅隐藏", (sender, args) => SetSelectedModuleState(true, false)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("上移", (sender, args) => MoveSelectedRow(_pluginPackagesGrid, -1)));
            menu.Items.Add(MenuItem("下移", (sender, args) => MoveSelectedRow(_pluginPackagesGrid, 1)));
            return menu;
        }

        private ContextMenu BuildFeatureMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("显示", (sender, args) => SetSelectedFeatureVisible(true)));
            menu.Items.Add(MenuItem("隐藏", (sender, args) => SetSelectedFeatureVisible(false)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("设为大按钮", (sender, args) => SetSelectedFeatureSize("large")));
            menu.Items.Add(MenuItem("设为小按钮", (sender, args) => SetSelectedFeatureSize("small")));
            menu.Items.Add(MenuItem("设置图标...", (sender, args) => SetSelectedFeatureIcon()));
            menu.Items.Add(BuildBuiltinIconMenu());
            menu.Items.Add(MenuItem("清空图标", (sender, args) => SetSelectedFeatureIcon(string.Empty)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("上移", (sender, args) => MoveSelectedRow(_featuresGrid, -1)));
            menu.Items.Add(MenuItem("下移", (sender, args) => MoveSelectedRow(_featuresGrid, 1)));
            return menu;
        }

        private ContextMenu BuildGroupMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("新增分组", (sender, args) => AddCustomGroup()));
            menu.Items.Add(MenuItem("删除分组", (sender, args) => RemoveSelectedGroup()));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("上移", (sender, args) => MoveSelectedRow(_groupsGrid, -1)));
            menu.Items.Add(MenuItem("下移", (sender, args) => MoveSelectedRow(_groupsGrid, 1)));
            return menu;
        }

        private MenuItem BuildBuiltinIconMenu()
        {
            var menu = new MenuItem { Header = "选择内置图标" };
            foreach (var option in BuiltinIconOptions())
            {
                var value = option.Value;
                menu.Items.Add(MenuItem(option.DisplayText, (sender, args) => SetSelectedFeatureBuiltinIcon(value)));
            }

            return menu;
        }

        private ContextMenu BuildRepositoryMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("启用", (sender, args) => SetSelectedRepositoryEnabled(true)));
            menu.Items.Add(MenuItem("禁用", (sender, args) => SetSelectedRepositoryEnabled(false)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("浏览仓库插件包", (sender, args) => BrowseSelectedRepository()));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("新增仓库", (sender, args) => AddRepository()));
            menu.Items.Add(MenuItem("删除仓库", (sender, args) => RemoveSelectedRepository()));
            return menu;
        }

        private ContextMenu BuildRepositoryPackageMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("安装插件包", (sender, args) => InstallSelectedRepositoryPackage()));
            menu.Items.Add(MenuItem("更新插件包", (sender, args) => UpdateSelectedRepositoryPackage()));
            menu.Items.Add(MenuItem("卸载插件包", (sender, args) => UninstallSelectedRepositoryPackage()));
            return menu;
        }

        private ContextMenu BuildPendingPackageOperationMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("取消待处理操作", (sender, args) => CancelSelectedPendingPackageOperation()));
            return menu;
        }

        private static MenuItem MenuItem(string text, RoutedEventHandler handler)
        {
            var item = new MenuItem { Header = text };
            item.Click += handler;
            return item;
        }

        private void SetSelectedModuleState(bool enabled, bool visible)
        {
            if (_pluginPackagesGrid.SelectedItem is ModuleRow row)
            {
                row.Enabled = enabled;
                row.Visible = visible;
                SafeRefreshGrid(_pluginPackagesGrid);
            }
        }

        private void SetSelectedFeatureVisible(bool visible)
        {
            if (_featuresGrid.SelectedItem is FeatureRow row)
            {
                row.Visible = visible;
                SafeRefreshGrid(_featuresGrid);
            }
        }

        private void SetSelectedFeatureSize(string size)
        {
            if (_featuresGrid.SelectedItem is FeatureRow row)
            {
                row.ButtonSize = NormalizeButtonSize(size);
                SafeRefreshGrid(_featuresGrid);
            }
        }

        private void SetSelectedFeatureIcon(string? iconPath = null)
        {
            if (!(_featuresGrid.SelectedItem is FeatureRow row)) return;

            if (iconPath == null)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择功能图标",
                    Filter = "图标和图片|*.png;*.jpg;*.jpeg;*.ico;*.bmp|所有文件|*.*"
                };
                if (dialog.ShowDialog(this) != true) return;
                iconPath = ToPluginRelativePath(dialog.FileName);
            }

            row.IconPath = iconPath;
            SafeRefreshGrid(_featuresGrid);
        }

        private void SetSelectedFeatureBuiltinIcon(string iconPath)
        {
            SetSelectedFeatureIcon(iconPath);
        }

        private void RibbonDesignerIconSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            if (_syncingSelectedRibbonDesignerEditor) return;
            var iconPath = Convert.ToString(_selectedRibbonDesignerIcon.SelectedValue) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(iconPath)) return;
            if (string.Equals(iconPath, RibbonDesignerBrowseIconAction, StringComparison.OrdinalIgnoreCase))
            {
                SetSelectedRibbonDesignerIcon();
                return;
            }

            if (string.Equals(iconPath, RibbonDesignerClearIconAction, StringComparison.OrdinalIgnoreCase))
            {
                SetSelectedRibbonDesignerIcon(string.Empty);
                return;
            }

            SetSelectedRibbonDesignerIcon(iconPath);
        }

        private void SetSelectedRibbonDesignerIcon(string? iconPath = null)
        {
            var row = _viewModel.SelectedRibbonDesignerNode;
            if (row == null)
            {
                RefreshStatus("请先选择一个布局项。");
                return;
            }

            if (!CanEditRibbonDesignerIcon(row))
            {
                RefreshStatus("只有常规按钮需要单独设置图标。");
                return;
            }

            if (iconPath == null)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择功能图标",
                    Filter = "图标和图片|*.png;*.jpg;*.jpeg;*.ico;*.bmp|所有文件|*.*"
                };
                if (dialog.ShowDialog(this) != true)
                {
                    SetRibbonDesignerIconSelectorText(row.IconPath);
                    return;
                }

                iconPath = ToPluginRelativePath(dialog.FileName);
            }

            row.IconPath = iconPath;
            SetRibbonDesignerIconSelectorText(iconPath);
            RefreshRibbonDesignerAfterLayoutChange("已更新图标，保存并重启 Revit 后生效。");
        }

        private void SetSelectedRepositoryEnabled(bool enabled)
        {
            if (_repositoriesGrid.SelectedItem is RepositoryRow row)
            {
                row.Enabled = enabled;
                row.Status = enabled ? "可浏览" : "停用";
                SafeRefreshGrid(_repositoriesGrid);
            }
        }

        private void AddRepository()
        {
            var id = UniqueRepositoryId(_viewModel.Repositories, "repository");
            var row = new RepositoryRow
            {
                Id = id,
                Enabled = true,
                Provider = DefaultRepositoryProvider,
                Visibility = "public",
                Repository = DefaultPublicRepository,
                Ref = "main",
                ManifestPath = DefaultPackageManifestName,
                ApiKey = string.Empty,
                Status = "待保存"
            };
            _viewModel.Repositories.Add(row);
            _repositoriesGrid.SelectedItem = row;
            SafeRefreshGrid(_repositoriesGrid);
        }

        private void BrowseSelectedRepository()
        {
            try
            {
                EndGridEdits();
                ApplyRepositoryRows();

                if (!(_repositoriesGrid.SelectedItem is RepositoryRow row)) return;
                var repository = row.ToConfiguration();
                var packages = _packageRepositoryService.Browse(BaseDirectory(), repository, out var diagnostics);

                row.Status = diagnostics.Any()
                    ? diagnostics.Last().Message
                    : "已浏览 " + packages.Count + " 个插件包";
                SafeRefreshGrid(_repositoriesGrid);

                LoadRepositoryPackageRows(packages);
                LoadDiagnosticRowsFromMessages(diagnostics);
                RefreshStatus(row.Status);
            }
            catch (Exception ex)
            {
                ReportSettingsError("浏览仓库失败", ex);
            }
        }

        private void InstallSelectedRepositoryPackage()
        {
            RunRepositoryPackageOperation(package => _packageRepositoryService.Install(BaseDirectory(), package));
        }

        private void UpdateSelectedRepositoryPackage()
        {
            RunRepositoryPackageOperation(package => _packageRepositoryService.Update(BaseDirectory(), package));
        }

        private void UninstallSelectedRepositoryPackage()
        {
            if (!(_repositoryPackagesGrid.SelectedItem is RepositoryPackageRow row)) return;
            RunRepositoryPackageOperation(package => _packageRepositoryService.Uninstall(BaseDirectory(), package));
        }

        private void RunRepositoryPackageOperation(Func<RepositoryPackageDescriptor, PackageRepositoryOperationResult> operation)
        {
            try
            {
                EndGridEdits();
                if (!(_repositoryPackagesGrid.SelectedItem is RepositoryPackageRow row)) return;

                var result = operation(row.ToDescriptor());
                RefreshRepositoryPackageInstallState(row.PackageId, row.InstallDirectory);
                LoadPendingPackageOperationRows();
                SafeRefreshGrid(_repositoryPackagesGrid);

                _moduleDocuments = _configurationStore.LoadModuleDocuments(_configuration);
                LoadGroupRows();
                LoadFeatureRows();
                LoadRibbonLayoutRows();
                RefreshStatus(result.Message);
            }
            catch (Exception ex)
            {
                ReportSettingsError("插件包操作失败", ex);
            }
        }

        private void CancelSelectedPendingPackageOperation()
        {
            if (!(_pendingPackageOperationsGrid.SelectedItem is PendingPackageOperationRow row)) return;
            var result = _packageRepositoryService.CancelPendingOperation(BaseDirectory(), row.PackageId, row.ModuleId);
            RefreshRepositoryPackageInstallState(row.PackageId, row.InstallDirectory);
            LoadPendingPackageOperationRows();
            SafeRefreshGrid(_repositoryPackagesGrid);
            RefreshStatus(result.Message);
        }

        private void RefreshRepositoryPackageInstallState(string packageId, string installDirectory)
        {
            foreach (var row in _viewModel.RepositoryPackages.Where(item =>
                string.Equals(item.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.InstallDirectory, installDirectory, StringComparison.OrdinalIgnoreCase)))
            {
                var refreshed = _packageRepositoryService.RefreshInstallState(BaseDirectory(), row.ToDescriptor());
                row.IsInstalled = refreshed.IsInstalled;
                row.InstalledVersion = refreshed.InstalledVersion;
                row.PendingOperation = refreshed.PendingOperation;
                row.InstallState = RepositoryPackageRow.InstallStateFor(row.IsInstalled, row.Version, row.InstalledVersion, row.PendingOperation, IsLoadedInCurrentRuntime(row.PackageId, row.ModuleId));
            }
        }

        private bool IsLoadedInCurrentRuntime(string packageId, string moduleId)
        {
            var id = string.IsNullOrWhiteSpace(moduleId) ? packageId : moduleId;
            if (string.IsNullOrWhiteSpace(id)) return false;

            return (FrameworkRuntimeState.Current?.Configuration.EffectiveModules.Modules ?? new List<ModuleConfiguration>())
                .Any(module => string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private void AddCustomGroup()
        {
            EndGridEdits();
            var id = UniqueGroupId(_viewModel.Groups, "custom-group");
            var index = _viewModel.Groups.Count + 1;
            var row = new GroupRow
            {
                Id = id,
                Name = "自定义分组 " + index,
                FeatureCount = 0,
                Order = (_viewModel.Groups.Count + 1) * 100
            };

            _viewModel.Groups.Add(row);
            RefreshGroupPositions();
            RefreshFeatureGroupOptions();
            _groupsGrid.SelectedItem = row;
            RefreshStatus("已新增自定义分组，可在布局页重置默认布局或手动添加功能。");
        }

        private void RemoveSelectedGroup()
        {
            if (!(_groupsGrid.SelectedItem is GroupRow row)) return;

            EndGridEdits();
            var isInUse = _viewModel.Features.Any(feature => string.Equals(feature.Group, row.Id, StringComparison.OrdinalIgnoreCase));
            if (isInUse)
            {
                RefreshStatus("该分组仍有功能使用。请先调整布局或功能来源后再删除。");
                return;
            }

            _viewModel.Groups.Remove(row);
            RefreshGroupPositions();
            RefreshFeatureGroupOptions();
            RefreshStatus("已删除未使用的自定义分组。");
        }

        private List<GroupOption> GroupOptionsForFeatureRows()
        {
            var rows = _viewModel.Groups.Any()
                ? _viewModel.Groups
                : new ObservableCollection<GroupRow>(EditableModules()
                    .SelectMany(module => (module.Features ?? new List<FeatureConfiguration>()).Select(feature => new GroupRow
                    {
                        Id = GroupIdForFeature(module, feature),
                        Name = GroupIdForFeature(module, feature)
                    }))
                    .Where(row => !string.IsNullOrWhiteSpace(row.Id))
                    .GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First()));

            return rows
                .Where(row => !string.IsNullOrWhiteSpace(row.Id))
                .Select(row => new GroupOption
                {
                    Id = row.Id,
                    DisplayText = DisplayName(row.Name, row.Id, row.Id)
                })
                .OrderBy(option => option.DisplayText, StringComparer.OrdinalIgnoreCase)
                .ThenBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<IconOption> BuiltinIconOptions()
        {
            return DefaultRibbonIconProvider.BuiltinIconKeys
                .Select(key => new IconOption
                {
                    Value = DefaultRibbonIconProvider.ToIconPath(key),
                    DisplayText = BuiltinIconDisplayName(key)
                })
                .ToList();
        }

        private static List<IconOption> RibbonDesignerIconOptions()
        {
            var options = new List<IconOption>
            {
                new IconOption { Value = RibbonDesignerBrowseIconAction, DisplayText = "选择图标..." },
                new IconOption { Value = RibbonDesignerClearIconAction, DisplayText = "清空图标" }
            };
            options.AddRange(BuiltinIconOptions());
            return options;
        }

        private static bool IsRibbonDesignerIconAction(string value)
        {
            return string.Equals(value, RibbonDesignerBrowseIconAction, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, RibbonDesignerClearIconAction, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBuiltinIconValue(string value)
        {
            return BuiltinIconOptions().Any(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));
        }

        private static string BuiltinIconDisplayName(string key)
        {
            if (string.Equals(key, "settings", StringComparison.OrdinalIgnoreCase)) return "设置";
            if (string.Equals(key, "tool", StringComparison.OrdinalIgnoreCase)) return "工具";
            if (string.Equals(key, "duct", StringComparison.OrdinalIgnoreCase)) return "风管";
            if (string.Equals(key, "family", StringComparison.OrdinalIgnoreCase)) return "族";
            if (string.Equals(key, "batch", StringComparison.OrdinalIgnoreCase)) return "批处理";
            if (string.Equals(key, "document", StringComparison.OrdinalIgnoreCase)) return "文档";
            if (string.Equals(key, "warning", StringComparison.OrdinalIgnoreCase)) return "提示";
            return "默认";
        }

        private void RefreshFeatureGroupOptions()
        {
            var options = GroupOptionsForFeatureRows();
            _groupOptions.Clear();
            foreach (var option in options)
            {
                _groupOptions.Add(option);
            }

            SafeRefreshGrid(_featuresGrid);
            SyncSelectedFeatureEditor();
        }

        private void SyncSelectedFeatureEditor()
        {
            _syncingSelectedFeatureEditor = true;
            try
            {
                var row = _featuresGrid.SelectedItem as FeatureRow;
                var hasSelection = row != null;
                _selectedFeatureName.Text = hasSelection ? row!.Name : "未选择功能";
                _selectedFeatureGroupCombo.IsEnabled = hasSelection;
                _selectedFeatureButtonSizeCombo.IsEnabled = hasSelection;
                _selectedFeatureGroupCombo.SelectedValue = hasSelection ? row!.Group : null;
                _selectedFeatureButtonSizeCombo.SelectedItem = hasSelection ? NormalizeButtonSize(row!.ButtonSize) : null;
            }
            finally
            {
                _syncingSelectedFeatureEditor = false;
            }
        }

        private void ApplySelectedFeatureGroup()
        {
            if (_syncingSelectedFeatureEditor) return;
            if (!(_featuresGrid.SelectedItem is FeatureRow row)) return;

            var groupId = Convert.ToString(_selectedFeatureGroupCombo.SelectedValue);
            if (string.IsNullOrWhiteSpace(groupId)) return;
            row.Group = groupId.Trim();
            UpdateFeatureDisplayFields(row);
            SortFeatureRowsForRuntimeOrder();
            RefreshFeaturePositionsByGroup();
            _featuresGrid.SelectedItem = row;
            _featuresGrid.ScrollIntoView(row);
            SyncSelectedFeatureEditor();
            RefreshStatus("已调整功能所属分组，保存并重启 Revit 后 Ribbon 分组生效。");
        }

        private void ApplySelectedFeatureButtonSize()
        {
            if (_syncingSelectedFeatureEditor) return;
            if (!(_featuresGrid.SelectedItem is FeatureRow row)) return;

            var buttonSize = Convert.ToString(_selectedFeatureButtonSizeCombo.SelectedItem);
            if (string.IsNullOrWhiteSpace(buttonSize)) return;
            row.ButtonSize = NormalizeButtonSize(buttonSize);
            UpdateFeatureDisplayFields(row);
            SafeRefreshGrid(_featuresGrid);
            RefreshStatus("已调整功能显示设置，保存并重启 Revit 后生效。");
        }

        private FeatureRow? SelectedFeatureRow()
        {
            return _featuresGrid.SelectedItem as FeatureRow;
        }

        private void SelectFeatureRow(FeatureRow row)
        {
            if (row == null) return;
            _featuresGrid.SelectedItem = row;
            _featuresGrid.ScrollIntoView(row);
            SyncSelectedFeatureEditor();
        }

        private void RefreshFeatureOrderAndSelection(FeatureRow? selectedRow)
        {
            RefreshFeaturePositionsByGroup();
            if (selectedRow != null)
            {
                SelectFeatureRow(selectedRow);
            }
        }

        private void EnsureViewGroupForFeature(ModuleConfiguration module, FeatureConfiguration feature)
        {
            if (string.IsNullOrWhiteSpace(feature.Group))
            {
                feature.Group = GroupIdForFeature(module, feature);
            }

            var groupId = feature.Group.Trim();
            var row = _viewModel.Groups.FirstOrDefault(item => string.Equals(item.Id, groupId, StringComparison.OrdinalIgnoreCase));
            EnsureViewGroup(
                groupId,
                DisplayName(row?.Name ?? string.Empty, groupId, groupId),
                row?.Order > 0 ? row.Order : feature.Order,
                feature.Category,
                feature.Tags);
        }

        private void EnsureViewGroup(string groupId, string displayName, int order, string category, IEnumerable<string> tags)
        {
            var view = WorkspaceView();
            if (view.Groups == null)
            {
                view.Groups = new List<ViewGroupConfiguration>();
            }

            groupId = string.IsNullOrWhiteSpace(groupId) ? "external" : groupId.Trim();
            var group = view.Groups.FirstOrDefault(item => string.Equals(item.Id, groupId, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                group = new ViewGroupConfiguration
                {
                    Id = groupId,
                    Name = displayName,
                    Description = string.Empty,
                    IncludeCategories = new List<string>(),
                    IncludeTags = new List<string>(),
                    Order = order > 0 ? order : (view.Groups.Count + 1) * 100,
                    Presentation = "panel"
                };
                view.Groups.Add(group);
            }

            group.Name = DisplayName(displayName, group.Name, groupId);
            group.Order = order > 0 ? order : group.Order;
            if (group.IncludeCategories == null)
            {
                group.IncludeCategories = new List<string>();
            }

            if (group.IncludeTags == null)
            {
                group.IncludeTags = new List<string>();
            }

            if (!string.IsNullOrWhiteSpace(category) && !group.IncludeCategories.Any(item => string.Equals(item, category, StringComparison.OrdinalIgnoreCase)))
            {
                group.IncludeCategories.Add(category);
            }

            foreach (var tag in tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
            {
                if (!group.IncludeTags.Any(item => string.Equals(item, tag, StringComparison.OrdinalIgnoreCase)))
                {
                    group.IncludeTags.Add(tag);
                }
            }
        }

        private ViewConfiguration WorkspaceView()
        {
            if (_configuration.Views == null)
            {
                _configuration.Views = new ViewsConfiguration();
            }

            if (_configuration.Views.Views == null)
            {
                _configuration.Views.Views = new List<ViewConfiguration>();
            }

            var viewId = string.IsNullOrWhiteSpace(_configuration.Views.DefaultView) ? "workspace" : _configuration.Views.DefaultView.Trim();
            var view = _configuration.Views.Views.FirstOrDefault(item => string.Equals(item.Id, viewId, StringComparison.OrdinalIgnoreCase));
            if (view == null)
            {
                view = new ViewConfiguration
                {
                    Id = viewId,
                    Name = "PlugHub 工作台",
                    Description = string.Empty,
                    Ribbon = new RibbonConfiguration { TabName = "PlugHub", FallbackPanelName = "其他工具" },
                    Groups = new List<ViewGroupConfiguration>(),
                    Sort = new List<string> { "group.order", "feature.order", "feature.name", "feature.id" },
                    EmptyStateText = "PlugHub 工作台没有可用功能。"
                };
                _configuration.Views.Views.Add(view);
            }

            if (view.Ribbon == null)
            {
                view.Ribbon = new RibbonConfiguration { TabName = "PlugHub", FallbackPanelName = "其他工具" };
            }

            if (view.Groups == null)
            {
                view.Groups = new List<ViewGroupConfiguration>();
            }

            if (view.Sort == null || view.Sort.Count == 0)
            {
                view.Sort = new List<string> { "group.order", "feature.order", "feature.name", "feature.id" };
            }

            _configuration.Views.DefaultView = viewId;
            return view;
        }

        private static string GroupIdForFeature(ModuleConfiguration module, FeatureConfiguration feature)
        {
            if (!string.IsNullOrWhiteSpace(feature.Group)) return feature.Group.Trim();
            if (!string.IsNullOrWhiteSpace(feature.Category)) return feature.Category.Trim();
            return module.Id ?? string.Empty;
        }

        private void RemoveSelectedRepository()
        {
            if (_repositoriesGrid.SelectedItem is RepositoryRow row)
            {
                _viewModel.Repositories.Remove(row);
            }
        }

        private void MoveSelectedRow(DataGrid grid, int direction)
        {
            var sourceIndex = grid.SelectedIndex;
            if (sourceIndex < 0) return;

            var targetIndex = sourceIndex + direction;
            if (grid == _pluginPackagesGrid)
            {
                MoveRow(_viewModel.Modules, sourceIndex, targetIndex);
                RecalculatePluginPackageOrders();
                _pluginPackagesGrid.SelectedIndex = targetIndex;
                return;
            }

            if (grid == _featuresGrid)
            {
                MoveSelectedFeature(direction);
                return;
            }

            if (grid == _groupsGrid)
            {
                MoveRow(_viewModel.Groups, sourceIndex, targetIndex);
                RecalculateGroupOrders();
                _groupsGrid.SelectedIndex = targetIndex;
            }
        }

        private static void MoveRow<T>(ObservableCollection<T> rows, int sourceIndex, int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= rows.Count) return;
            rows.Move(sourceIndex, targetIndex);
        }

        private void MoveSelectedFeature(int direction)
        {
            var row = SelectedFeatureRow();
            if (row == null) return;

            var sameGroupIndexes = _viewModel.Features
                .Select((feature, index) => new { Feature = feature, Index = index })
                .Where(item => string.Equals(item.Feature.Group, row.Group, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Index)
                .ToList();
            var groupPosition = sameGroupIndexes.IndexOf(_viewModel.Features.IndexOf(row));
            var targetGroupPosition = groupPosition + direction;
            if (groupPosition < 0 || targetGroupPosition < 0 || targetGroupPosition >= sameGroupIndexes.Count) return;

            MoveRow(_viewModel.Features, _viewModel.Features.IndexOf(row), sameGroupIndexes[targetGroupPosition]);
            RecalculateFeatureOrders();
            SelectFeatureRow(row);
        }

        private void RecalculatePluginPackageOrders()
        {
            RefreshPluginPackagePositions();
        }

        private void RecalculateFeatureOrders()
        {
            RefreshFeaturePositionsByGroup();
        }

        private void RecalculateGroupOrders()
        {
            RefreshGroupPositions();
        }

        private void RefreshPluginPackagePositions()
        {
            for (var index = 0; index < _viewModel.Modules.Count; index++)
            {
                _viewModel.Modules[index].Order = (index + 1) * 100;
                _viewModel.Modules[index].PositionText = "第 " + (index + 1) + " 项";
            }

            SafeRefreshGrid(_pluginPackagesGrid);
        }

        private void SortFeatureRowsForRuntimeOrder()
        {
            var sorted = _viewModel.Features
                .OrderBy(row => GroupOrderForFeature(row.Group))
                .ThenBy(row => GroupDisplayName(row.Group), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Order)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _viewModel.Features.Clear();
            foreach (var row in sorted)
            {
                _viewModel.Features.Add(row);
            }
        }

        private void RefreshFeaturePositionsByGroup()
        {
            var groupIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < _viewModel.Features.Count; index++)
            {
                var row = _viewModel.Features[index];
                var groupKey = string.IsNullOrWhiteSpace(row.Group) ? string.Empty : row.Group.Trim();
                groupIndexes.TryGetValue(groupKey, out var groupIndex);
                groupIndex++;
                groupIndexes[groupKey] = groupIndex;

                row.Order = groupIndex * 10;
                row.PositionText = GroupDisplayName(groupKey) + " 第 " + groupIndex + " 项";
                UpdateFeatureDisplayFields(row);
            }

            RefreshFeatureCounts();
            SafeRefreshGrid(_featuresGrid);
        }

        private void RefreshGroupPositions()
        {
            for (var index = 0; index < _viewModel.Groups.Count; index++)
            {
                _viewModel.Groups[index].Order = (index + 1) * 100;
                _viewModel.Groups[index].PositionText = "第 " + (index + 1) + " 项";
                _viewModel.Groups[index].FeatureCountText = _viewModel.Groups[index].FeatureCount + " 个";
            }

            SafeRefreshGrid(_groupsGrid);
        }

        private void RefreshFeatureCounts()
        {
            foreach (var group in _viewModel.Groups)
            {
                group.FeatureCount = _viewModel.Features.Count(feature => string.Equals(feature.Group, group.Id, StringComparison.OrdinalIgnoreCase));
                group.FeatureCountText = group.FeatureCount + " 个";
            }

            SafeRefreshGrid(_groupsGrid);
        }

        private void UpdateFeatureDisplayFields(FeatureRow row)
        {
            if (row == null) return;
            row.GroupDisplayText = GroupDisplayName(row.Group);
            row.ButtonSize = NormalizeButtonSize(row.ButtonSize);
            row.ButtonSizeDisplayText = ButtonSizeDisplayName(row.ButtonSize);
        }

        private void AttachGridBehaviors(DataGrid grid)
        {
            grid.AllowDrop = true;
            grid.PreviewMouseRightButtonDown += GridPreviewMouseRightButtonDown;
            grid.PreviewMouseLeftButtonDown += GridPreviewMouseLeftButtonDown;
            grid.MouseMove += GridMouseMove;
            grid.DragOver += GridDragOver;
            grid.Drop += GridDrop;
        }

        private static void GridPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is DataGrid grid)) return;
            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row == null) return;

            row.IsSelected = true;
            grid.CurrentItem = row.Item;
        }

        private void GridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is DataGrid grid)) return;
            if (IsInteractiveGridEditor(e.OriginalSource as DependencyObject))
            {
                ResetDragSource();
                return;
            }

            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            _dragSourceGrid = grid;
            _dragSourceRowIndex = row?.GetIndex() ?? -1;
        }

        private void GridMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragSourceRowIndex < 0) return;
            if (!(sender is DataGrid grid) || grid != _dragSourceGrid) return;
            if (_dragSourceRowIndex >= grid.Items.Count) return;

            try
            {
                DragDrop.DoDragDrop(grid, grid.Items[_dragSourceRowIndex], DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                ReportSettingsError("拖拽排序失败", ex);
                ResetDragSource();
            }
        }

        private static void GridDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void GridDrop(object sender, DragEventArgs e)
        {
            if (!(sender is DataGrid grid) || grid != _dragSourceGrid)
            {
                ResetDragSource();
                return;
            }

            if (_dragSourceRowIndex < 0)
            {
                ResetDragSource();
                return;
            }

            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            var targetIndex = row?.GetIndex() ?? grid.Items.Count - 1;
            if (targetIndex < 0 || targetIndex == _dragSourceRowIndex)
            {
                ResetDragSource();
                return;
            }

            if (grid == _pluginPackagesGrid)
            {
                MoveRow(_viewModel.Modules, _dragSourceRowIndex, targetIndex);
                RecalculatePluginPackageOrders();
            }
            else if (grid == _featuresGrid)
            {
                var dragged = _viewModel.Features[_dragSourceRowIndex];
                var target = _viewModel.Features[targetIndex];
                if (!string.Equals(dragged.Group, target.Group, StringComparison.OrdinalIgnoreCase))
                {
                    dragged.Group = target.Group;
                    UpdateFeatureDisplayFields(dragged);
                }

                MoveRow(_viewModel.Features, _dragSourceRowIndex, targetIndex);
                RecalculateFeatureOrders();
                SelectFeatureRow(dragged);
                ResetDragSource();
                return;
            }
            else if (grid == _groupsGrid)
            {
                MoveRow(_viewModel.Groups, _dragSourceRowIndex, targetIndex);
                RecalculateGroupOrders();
            }

            grid.SelectedIndex = targetIndex;
            ResetDragSource();
        }

        private void ResetDragSource()
        {
            _dragSourceRowIndex = -1;
            _dragSourceGrid = null;
        }

        private void SafeRefreshGrid(DataGrid grid)
        {
            if (grid == null) return;

            if (!grid.Dispatcher.CheckAccess())
            {
                grid.Dispatcher.BeginInvoke(new Action(() => SafeRefreshGrid(grid)), DispatcherPriority.Background);
                return;
            }

            if (TryRefreshGrid(grid)) return;
            grid.Dispatcher.BeginInvoke(new Action(() => TryRefreshGrid(grid)), DispatcherPriority.Background);
        }

        private static bool TryRefreshGrid(DataGrid grid)
        {
            try
            {
                CommitGrid(grid);
                grid.Items.Refresh();
                return true;
            }
            catch (InvalidOperationException ex) when (IsEditTransactionRefreshError(ex))
            {
                return false;
            }
        }

        private static bool IsEditTransactionRefreshError(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            return message.IndexOf("AddNew", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("EditItem", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EndGridEdits()
        {
            CommitGrid(_pluginPackagesGrid);
            CommitGrid(_featuresGrid);
            CommitGrid(_groupsGrid);
            CommitGrid(_repositoriesGrid);
            CommitGrid(_repositoryPackagesGrid);
        }

        private static void CommitGrid(DataGrid grid)
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void RefreshStatus(string text)
        {
            _statusText.Text = text;
        }

        private void ReportSettingsError(string title, Exception ex)
        {
            var message = title + "：" + ex.Message;
            LoadDiagnosticRowsFromMessages(new[]
            {
                new DiagnosticMessage
                {
                    Severity = DiagnosticSeverity.Warning,
                    Code = "PH-SETTINGS",
                    ModuleId = "settings",
                    Message = message
                }
            });
            RefreshStatus(message);
        }

        private void ExportLogs()
        {
            try
            {
                var targetPath = Path.Combine(BaseDirectory(), "exports", "plughub-logs.zip");
                new PlugHubLogExporter().Export(BaseDirectory(), targetPath);
                RefreshStatus("日志已导出: " + targetPath);
            }
            catch (Exception ex)
            {
                ReportSettingsError("导出日志失败", ex);
            }
        }

        private string ToPluginRelativePath(string path)
        {
            var baseDirectory = BaseDirectory();
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(baseDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return fullPath;
        }

        private string BaseDirectory()
        {
            return _configurationStore.BaseDirectory();
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static bool IsInteractiveGridEditor(DependencyObject? source)
        {
            return FindAncestor<ComboBox>(source) != null
                || FindAncestor<TextBox>(source) != null
                || FindAncestor<CheckBox>(source) != null
                || FindAncestor<ButtonBase>(source) != null
                || FindAncestor<Thumb>(source) != null;
        }

        private static Dictionary<string, string> DiagnosticsBySourceId(FrameworkRuntimeSnapshot? snapshot)
        {
            return (snapshot?.Diagnostics ?? new List<DiagnosticMessage>())
                .Where(message => !string.IsNullOrWhiteSpace(message.ModuleId))
                .GroupBy(message => message.ModuleId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Message, StringComparer.OrdinalIgnoreCase);
        }

        private static string DisplayName(string displayName, string name, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName.Trim();
            if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
            return fallback ?? string.Empty;
        }

        private int GroupOrderForFeature(string groupId)
        {
            var group = _viewModel.Groups.FirstOrDefault(row => string.Equals(row.Id, groupId, StringComparison.OrdinalIgnoreCase));
            return group?.Order > 0 ? group.Order : int.MaxValue;
        }

        private string GroupDisplayName(string groupId)
        {
            var group = _viewModel.Groups.FirstOrDefault(row => string.Equals(row.Id, groupId, StringComparison.OrdinalIgnoreCase));
            return DisplayName(group?.Name ?? string.Empty, groupId, "未分组");
        }

        private static string NormalizeButtonSize(string value)
        {
            return string.Equals(value, "small", StringComparison.OrdinalIgnoreCase) ? "small" : "large";
        }

        private static string ButtonSizeDisplayName(string value)
        {
            return string.Equals(NormalizeButtonSize(value), "small", StringComparison.OrdinalIgnoreCase) ? "小" : "大";
        }

        private static string UniqueRepositoryId(IEnumerable<RepositoryRow> rows, string prefix)
        {
            var existing = new HashSet<string>(rows.Select(row => row.Id), StringComparer.OrdinalIgnoreCase);
            var index = 1;
            string candidate;
            do
            {
                candidate = prefix + "-" + index++;
            }
            while (existing.Contains(candidate));

            return candidate;
        }

        private static string UniqueGroupId(IEnumerable<GroupRow> rows, string prefix)
        {
            var existing = new HashSet<string>(rows.Select(row => row.Id), StringComparer.OrdinalIgnoreCase);
            var index = 1;
            string candidate;
            do
            {
                candidate = prefix + "-" + index++;
            }
            while (existing.Contains(candidate));

            return candidate;
        }

        private sealed class GroupOption
        {
            public string Id { get; set; } = string.Empty;
            public string DisplayText { get; set; } = string.Empty;
        }

        private sealed class IconOption
        {
            public string Value { get; set; } = string.Empty;
            public string DisplayText { get; set; } = string.Empty;
        }

        private sealed class RibbonDisplayModeOption
        {
            public RibbonDisplayModeOption(string value, string displayText)
            {
                Value = value ?? string.Empty;
                DisplayText = displayText ?? string.Empty;
            }

            public string Value { get; }
            public string DisplayText { get; }
        }

    }
}
