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
using System.Windows.Threading;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;
using PlugHub.Framework.Packages;
using PlugHub.Framework.Runtime;
using PlugHub.Revit2020.Settings;
using PlugHub.Revit2020.Settings.Rows;

namespace PlugHub.Revit2020
{
    internal sealed class FrameworkSettingsWindow : Window
    {
        private const string DefaultPackageManifestName = "package.json";
        private const string DefaultRepositoryProvider = "gitee";
        private const string DefaultPublicRepository = "https://gitee.com/GaoMengGu/PlugHub_Packages";

        private readonly SettingsConfigurationStore _configurationStore;
        private readonly FrameworkSettingsViewModel _viewModel = new FrameworkSettingsViewModel();
        private FrameworkConfiguration _configuration;
        private readonly PackageRepositoryService _packageRepositoryService = new PackageRepositoryService();
        private readonly RepositoryCredentialService _credentialService = new RepositoryCredentialService();
        private readonly DataGrid _pluginPackagesGrid = CreateGrid();
        private readonly DataGrid _featuresGrid = CreateGrid();
        private readonly DataGrid _groupsGrid = CreateGrid();
        private readonly DataGrid _repositoriesGrid = CreateGrid();
        private readonly DataGrid _repositoryPackagesGrid = CreateGrid();
        private readonly DataGrid _pendingPackageOperationsGrid = CreateGrid();
        private readonly DataGrid _diagnosticsGrid = CreateGrid();
        private readonly TextBlock _statusText = new TextBlock();
        private List<SettingsConfigurationStore.ModuleManifestDocument> _moduleDocuments = new List<SettingsConfigurationStore.ModuleManifestDocument>();
        private readonly ObservableCollection<GroupOption> _groupOptions = new ObservableCollection<GroupOption>();
        private readonly IReadOnlyList<string> _buttonSizeOptions = new[] { "large", "small" };
        private readonly TextBlock _selectedFeatureName = new TextBlock();
        private readonly ComboBox _selectedFeatureGroupCombo = new ComboBox();
        private readonly ComboBox _selectedFeatureButtonSizeCombo = new ComboBox();
        private readonly WrapPanel _ribbonLayoutCanvas = new WrapPanel();
        private readonly ListBox _ribbonFeaturePoolList = new ListBox();
        private readonly TextBox _selectedRibbonNodeIdText = new TextBox();
        private readonly TextBox _selectedRibbonNodeText = new TextBox();
        private readonly TextBox _selectedRibbonNodeFeatureIdText = new TextBox();
        private readonly TextBox _selectedRibbonNodeDefaultFeatureIdText = new TextBox();
        private readonly ComboBox _selectedRibbonNodeTypeCombo = new ComboBox();
        private readonly ComboBox _selectedRibbonNodeSizeCombo = new ComboBox();
        private int _dragSourceRowIndex = -1;
        private DataGrid? _dragSourceGrid;
        private bool _syncingSelectedFeatureEditor;
        private bool _syncingSelectedRibbonNodeEditor;
        private RibbonLayoutNodeRow? _selectedRibbonLayoutNode;

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
            _statusText.Margin = new Thickness(0, 8, 0, 0);
            _statusText.TextWrapping = TextWrapping.Wrap;
            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(72, 84, 101));
            header.Children.Add(_statusText);
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

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            buttons.Children.Add(CreateButton("重新加载", (sender, args) => ReloadFromDisk()));
            buttons.Children.Add(CreateButton("保存配置", (sender, args) => TrySave()));
            buttons.Children.Add(CreateButton("关闭", (sender, args) => Close()));
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            return root;
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
            _selectedRibbonNodeTypeCombo.ItemsSource = new[] { "panel", "pushButton", "pulldownButton", "splitButton", "stack" };
            _selectedRibbonNodeSizeCombo.ItemsSource = _buttonSizeOptions;

            _ribbonLayoutCanvas.Orientation = Orientation.Horizontal;
            _ribbonLayoutCanvas.Margin = new Thickness(8);

            _ribbonFeaturePoolList.ItemsSource = _viewModel.RibbonFeaturePool;
            _ribbonFeaturePoolList.DisplayMemberPath = nameof(RibbonFeaturePoolRow.DisplayText);
            _ribbonFeaturePoolList.Margin = new Thickness(8);

            var root = new DockPanel();
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };
            actions.Children.Add(CreateButton("重置默认布局", (sender, args) => ResetDefaultRibbonLayout()));
            actions.Children.Add(CreateButton("新增面板", (sender, args) => AddRibbonPanelNode()));
            actions.Children.Add(CreateButton("添加功能", (sender, args) => AddSelectedFeatureToRibbonLayout()));
            actions.Children.Add(CreateButton("新增下拉", (sender, args) => AddRibbonContainerNode("pulldownButton")));
            actions.Children.Add(CreateButton("新增拆分", (sender, args) => AddRibbonContainerNode("splitButton")));
            actions.Children.Add(CreateButton("新增堆叠", (sender, args) => AddRibbonContainerNode("stack")));
            actions.Children.Add(CreateButton("删除布局项", (sender, args) => RemoveSelectedRibbonLayoutNode()));
            DockPanel.SetDock(actions, Dock.Top);
            root.Children.Add(actions);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });

            var poolColumn = BuildRibbonLayoutColumn("功能池", _ribbonFeaturePoolList);
            var layoutColumn = BuildRibbonLayoutColumn("布局画布", BuildRibbonLayoutCanvasHost());
            var propertyColumn = BuildRibbonLayoutColumn("属性", BuildRibbonNodePropertyPanel());
            Grid.SetColumn(poolColumn, 0);
            Grid.SetColumn(layoutColumn, 1);
            Grid.SetColumn(propertyColumn, 2);
            grid.Children.Add(poolColumn);
            grid.Children.Add(layoutColumn);
            grid.Children.Add(propertyColumn);
            root.Children.Add(grid);

            SyncSelectedRibbonNodeEditor();
            return BuildTab("布局", root);
        }

        private UIElement BuildRibbonLayoutCanvasHost()
        {
            return new ScrollViewer
            {
                Content = _ribbonLayoutCanvas,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
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

        private UIElement BuildRibbonNodePropertyPanel()
        {
            var panel = new StackPanel { Margin = new Thickness(8) };

            _selectedRibbonNodeTypeCombo.Height = 26;
            _selectedRibbonNodeTypeCombo.Margin = new Thickness(0, 2, 0, 8);
            _selectedRibbonNodeIdText.Height = 26;
            _selectedRibbonNodeIdText.Margin = new Thickness(0, 2, 0, 8);
            _selectedRibbonNodeText.Height = 26;
            _selectedRibbonNodeText.Margin = new Thickness(0, 2, 0, 8);
            _selectedRibbonNodeFeatureIdText.Height = 26;
            _selectedRibbonNodeFeatureIdText.Margin = new Thickness(0, 2, 0, 8);
            _selectedRibbonNodeDefaultFeatureIdText.Height = 26;
            _selectedRibbonNodeDefaultFeatureIdText.Margin = new Thickness(0, 2, 0, 8);
            _selectedRibbonNodeSizeCombo.Height = 26;
            _selectedRibbonNodeSizeCombo.Margin = new Thickness(0, 2, 0, 10);

            panel.Children.Add(EditorLabel("类型"));
            panel.Children.Add(_selectedRibbonNodeTypeCombo);
            panel.Children.Add(EditorLabel("ID"));
            panel.Children.Add(_selectedRibbonNodeIdText);
            panel.Children.Add(EditorLabel("显示名"));
            panel.Children.Add(_selectedRibbonNodeText);
            panel.Children.Add(EditorLabel("功能 ID"));
            panel.Children.Add(_selectedRibbonNodeFeatureIdText);
            panel.Children.Add(EditorLabel("默认功能 ID"));
            panel.Children.Add(_selectedRibbonNodeDefaultFeatureIdText);
            panel.Children.Add(EditorLabel("按钮大小"));
            panel.Children.Add(_selectedRibbonNodeSizeCombo);
            panel.Children.Add(CreateButton("应用属性", (sender, args) => ApplySelectedRibbonNodeEditor()));

            return panel;
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
            RefreshStatus("已加载配置。布局、图标和按钮大小需重启 Revit 重绘。");
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

            var ribbon = WorkspaceView().Ribbon ?? new RibbonConfiguration();
            var panels = (ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>())
                .OrderBy(panel => panel.Order)
                .ThenBy(panel => DisplayName(panel.Name, panel.Id, panel.Id), StringComparer.OrdinalIgnoreCase)
                .ToList();
            var rows = panels.Any()
                ? panels.Select(RibbonLayoutNodeRow.FromPanel).ToList()
                : CreateDefaultRibbonLayoutNodes();

            foreach (var row in rows)
            {
                _viewModel.RibbonLayoutNodes.Add(row);
            }

            foreach (var row in _viewModel.Features
                .OrderBy(feature => feature.ModuleName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase))
            {
                _viewModel.RibbonFeaturePool.Add(new RibbonFeaturePoolRow
                {
                    ModuleId = row.ModuleId,
                    ModuleName = row.ModuleName,
                    FeatureId = row.FeatureId,
                    FeatureName = row.Name,
                    DisplayText = row.Name,
                    Group = row.Group,
                    IconPath = row.IconPath
                });
            }

            RefreshRibbonLayoutCanvas();
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
            ApplyRibbonLayoutRows();
            ApplyRepositoryRows();

            _configurationStore.Save(_configuration, _moduleDocuments);

            LoadPostSaveDiagnosticRows();
            LoadRepositoryRows();
            RefreshStatus("已保存配置。布局和仓库设置已写回；布局、图标、按钮大小需重启 Revit 重绘。");
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
            ValidateUniqueRibbonFeaturePlacement();

            var view = WorkspaceView();
            if (view.Ribbon == null)
            {
                view.Ribbon = new RibbonConfiguration { TabName = "PlugHub", FallbackPanelName = "其他工具" };
            }

            view.Ribbon.LayoutVersion = _viewModel.RibbonLayoutNodes.Any() ? "1.0" : string.Empty;
            view.Ribbon.Panels = _viewModel.RibbonLayoutNodes
                .Where(row => string.Equals(row.NodeType, "panel", StringComparison.OrdinalIgnoreCase))
                .Select((row, index) =>
                {
                    row.Order = (index + 1) * 100;
                    AssignRibbonNodeOrders(row.Children);
                    return row.ToPanelConfiguration();
                })
                .ToList();
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

        private static void AssignRibbonNodeOrders(IEnumerable<RibbonLayoutNodeRow> rows)
        {
            var index = 0;
            foreach (var row in rows ?? new List<RibbonLayoutNodeRow>())
            {
                index++;
                row.Order = index * 100;
                AssignRibbonNodeOrders(row.Children);
            }
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

            foreach (var moduleGroup in features.GroupBy(feature => string.IsNullOrWhiteSpace(feature.ModuleId) ? "default" : feature.ModuleId, StringComparer.OrdinalIgnoreCase))
            {
                var panelIndex = result.Count + 1;
                var firstFeature = moduleGroup.First();
                var panel = new RibbonLayoutNodeRow
                {
                    NodeType = "panel",
                    Id = "default-panel-" + panelIndex,
                    Text = DisplayName(firstFeature.ModuleName, firstFeature.ModuleId, "默认工具"),
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

        private void ResetDefaultRibbonLayout()
        {
            _viewModel.RibbonLayoutNodes.Clear();
            foreach (var row in CreateDefaultRibbonLayoutNodes())
            {
                _viewModel.RibbonLayoutNodes.Add(row);
            }

            RefreshRibbonLayoutCanvas();
            RefreshStatus("已按当前已安装功能生成默认布局，保存并重启 Revit 后生效。");
        }

        private void AddRibbonPanelNode()
        {
            var panel = CreateRibbonPanelNode();
            _viewModel.RibbonLayoutNodes.Add(panel);
            SelectRibbonLayoutNode(panel);
            RefreshStatus("已新增 Ribbon 面板，保存并重启 Revit 后生效。");
        }

        private void AddRibbonContainerNode(string type)
        {
            var parent = SelectedRibbonContainerOrPanel();
            if (parent == null)
            {
                RefreshStatus("请先选择一个 Ribbon 面板。");
                return;
            }

            if (string.Equals(parent.NodeType, "pulldownButton", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parent.NodeType, "splitButton", StringComparison.OrdinalIgnoreCase))
            {
                RefreshStatus("下拉和拆分按钮内只放置功能按钮。");
                return;
            }

            if (string.Equals(parent.NodeType, "stack", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(type, "pulldownButton", StringComparison.OrdinalIgnoreCase))
            {
                RefreshStatus("堆叠控件内只能放置功能按钮或下拉按钮。");
                return;
            }

            var index = parent.Children.Count + 1;
            var row = new RibbonLayoutNodeRow
            {
                NodeType = type,
                Id = type + "-" + index,
                Text = RibbonNodeTypeDisplayName(type) + " " + index,
                Order = index * 100
            };
            parent.Children.Add(row);
            SelectRibbonLayoutNode(row);
            RefreshStatus("已新增 Ribbon 容器，保存并重启 Revit 后生效。");
        }

        private void AddSelectedFeatureToRibbonLayout()
        {
            var poolRow = _ribbonFeaturePoolList.SelectedItem as RibbonFeaturePoolRow;
            if (poolRow == null)
            {
                RefreshStatus("请先在功能池选择一个功能。");
                return;
            }

            if (FeatureIdExistsInRibbonLayout(poolRow.FeatureId))
            {
                RefreshRibbonFeaturePoolPlacementState();
                RefreshStatus("该功能已在布局中，不能重复添加。");
                return;
            }

            var parent = SelectedRibbonContainerOrPanel();
            if (parent == null)
            {
                parent = CreateRibbonPanelNode();
                _viewModel.RibbonLayoutNodes.Add(parent);
            }

            if (string.Equals(parent.NodeType, "stack", StringComparison.OrdinalIgnoreCase) && parent.Children.Count >= 3)
            {
                RefreshStatus("堆叠控件最多包含 3 个子项。");
                return;
            }

            var feature = _viewModel.Features.FirstOrDefault(row => string.Equals(row.FeatureId, poolRow.FeatureId, StringComparison.OrdinalIgnoreCase));
            var node = feature == null
                ? CreateRibbonFeatureNode(poolRow, parent.Children.Count + 1)
                : CreateRibbonFeatureNode(feature, parent.Children.Count + 1);
            parent.Children.Add(node);

            if (string.Equals(parent.NodeType, "splitButton", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(parent.DefaultFeatureId))
            {
                parent.DefaultFeatureId = node.FeatureId;
            }

            SelectRibbonLayoutNode(node);
            RefreshStatus("已添加功能到布局，保存并重启 Revit 后生效。");
        }

        private void RemoveSelectedRibbonLayoutNode()
        {
            var row = _selectedRibbonLayoutNode;
            if (row == null)
            {
                RefreshStatus("请先选择要删除的布局项。");
                return;
            }

            if (_viewModel.RibbonLayoutNodes.Remove(row))
            {
                _selectedRibbonLayoutNode = null;
                RefreshRibbonLayoutCanvas();
                RefreshStatus("已删除 Ribbon 面板，保存并重启 Revit 后生效。");
                return;
            }

            foreach (var panel in _viewModel.RibbonLayoutNodes)
            {
                if (RemoveRibbonNode(panel, row))
                {
                    _selectedRibbonLayoutNode = null;
                    RefreshRibbonLayoutCanvas();
                    RefreshStatus("已删除布局项，保存并重启 Revit 后生效。");
                    return;
                }
            }
        }

        private void SyncSelectedRibbonNodeEditor()
        {
            _syncingSelectedRibbonNodeEditor = true;
            var row = _selectedRibbonLayoutNode;
            var hasSelection = row != null;
            _selectedRibbonNodeTypeCombo.IsEnabled = hasSelection;
            _selectedRibbonNodeIdText.IsEnabled = hasSelection;
            _selectedRibbonNodeText.IsEnabled = hasSelection;
            _selectedRibbonNodeFeatureIdText.IsEnabled = hasSelection;
            _selectedRibbonNodeDefaultFeatureIdText.IsEnabled = hasSelection;
            _selectedRibbonNodeSizeCombo.IsEnabled = hasSelection;

            _selectedRibbonNodeTypeCombo.SelectedItem = hasSelection ? row!.NodeType : null;
            _selectedRibbonNodeIdText.Text = hasSelection ? row!.Id : string.Empty;
            _selectedRibbonNodeText.Text = hasSelection ? row!.Text : string.Empty;
            _selectedRibbonNodeFeatureIdText.Text = hasSelection ? row!.FeatureId : string.Empty;
            _selectedRibbonNodeDefaultFeatureIdText.Text = hasSelection ? row!.DefaultFeatureId : string.Empty;
            _selectedRibbonNodeSizeCombo.SelectedItem = hasSelection ? NormalizeButtonSize(row!.Size) : null;
            _syncingSelectedRibbonNodeEditor = false;
        }

        private void ApplySelectedRibbonNodeEditor()
        {
            if (_syncingSelectedRibbonNodeEditor) return;
            var row = _selectedRibbonLayoutNode;
            if (row == null)
            {
                RefreshStatus("请先选择一个布局项。");
                return;
            }

            var featureId = _selectedRibbonNodeFeatureIdText.Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(featureId) && FeatureIdExistsInRibbonLayout(featureId, row))
            {
                RefreshStatus("该功能已在布局中，不能重复添加。");
                return;
            }

            row.NodeType = Convert.ToString(_selectedRibbonNodeTypeCombo.SelectedItem) ?? row.NodeType;
            row.Id = _selectedRibbonNodeIdText.Text ?? string.Empty;
            row.Text = _selectedRibbonNodeText.Text ?? string.Empty;
            row.FeatureId = featureId;
            row.DefaultFeatureId = _selectedRibbonNodeDefaultFeatureIdText.Text ?? string.Empty;
            row.Size = NormalizeButtonSize(Convert.ToString(_selectedRibbonNodeSizeCombo.SelectedItem) ?? row.Size);
            RefreshRibbonLayoutCanvas();
            RefreshStatus("已更新布局项属性，保存并重启 Revit 后生效。");
        }

        private RibbonLayoutNodeRow? SelectedRibbonContainerOrPanel()
        {
            var row = _selectedRibbonLayoutNode;
            if (row == null)
            {
                return _viewModel.RibbonLayoutNodes.FirstOrDefault();
            }

            if (CanContainRibbonChildren(row))
            {
                return row;
            }

            return FindParentRibbonNode(row);
        }

        private static bool CanContainRibbonChildren(RibbonLayoutNodeRow row)
        {
            return row != null
                && (string.Equals(row.NodeType, "panel", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.NodeType, "pulldownButton", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.NodeType, "splitButton", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(row.NodeType, "stack", StringComparison.OrdinalIgnoreCase));
        }

        private RibbonLayoutNodeRow? FindParentRibbonNode(RibbonLayoutNodeRow target)
        {
            foreach (var panel in _viewModel.RibbonLayoutNodes)
            {
                var parent = FindParentRibbonNode(panel, target);
                if (parent != null) return parent;
            }

            return null;
        }

        private static RibbonLayoutNodeRow? FindParentRibbonNode(RibbonLayoutNodeRow current, RibbonLayoutNodeRow target)
        {
            foreach (var child in current.Children)
            {
                if (ReferenceEquals(child, target)) return current;
                var parent = FindParentRibbonNode(child, target);
                if (parent != null) return parent;
            }

            return null;
        }

        private static bool RemoveRibbonNode(RibbonLayoutNodeRow parent, RibbonLayoutNodeRow target)
        {
            if (parent.Children.Remove(target)) return true;
            foreach (var child in parent.Children)
            {
                if (RemoveRibbonNode(child, target)) return true;
            }

            return false;
        }

        private RibbonLayoutNodeRow CreateRibbonPanelNode()
        {
            var index = _viewModel.RibbonLayoutNodes.Count + 1;
            return new RibbonLayoutNodeRow
            {
                NodeType = "panel",
                Id = "custom-panel-" + index,
                Text = "自定义面板 " + index,
                Order = index * 100
            };
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

        private static RibbonLayoutNodeRow CreateRibbonFeatureNode(RibbonFeaturePoolRow feature, int index)
        {
            return new RibbonLayoutNodeRow
            {
                NodeType = "pushButton",
                Id = feature.FeatureId,
                Text = feature.FeatureName,
                FeatureId = feature.FeatureId,
                Size = "large",
                IconPath = feature.IconPath,
                Order = index * 100
            };
        }

        private void SelectRibbonLayoutNode(RibbonLayoutNodeRow row)
        {
            _selectedRibbonLayoutNode = row;
            RefreshRibbonLayoutCanvas();
        }

        private void RefreshRibbonLayoutCanvas()
        {
            if (_selectedRibbonLayoutNode != null && !ContainsRibbonNode(_viewModel.RibbonLayoutNodes, _selectedRibbonLayoutNode))
            {
                _selectedRibbonLayoutNode = null;
            }

            _ribbonLayoutCanvas.Children.Clear();
            foreach (var panel in _viewModel.RibbonLayoutNodes)
            {
                _ribbonLayoutCanvas.Children.Add(BuildRibbonPanelPreview(panel));
            }

            RefreshRibbonFeaturePoolPlacementState();
            SyncSelectedRibbonNodeEditor();
        }

        private UIElement BuildRibbonPanelPreview(RibbonLayoutNodeRow panel)
        {
            var body = new StackPanel();
            body.Children.Add(BuildRibbonPreviewButton(panel, DisplayName(panel.Text, panel.Id, "面板"), 156, 30));

            var items = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            foreach (var child in panel.Children)
            {
                items.Children.Add(BuildRibbonItemPreview(child));
            }

            body.Children.Add(items);
            return new Border
            {
                MinWidth = 186,
                MaxWidth = 260,
                Margin = new Thickness(0, 0, 12, 12),
                Padding = new Thickness(10),
                Background = Brushes.White,
                BorderBrush = IsSelectedRibbonNode(panel) ? new SolidColorBrush(Color.FromRgb(25, 118, 210)) : new SolidColorBrush(Color.FromRgb(210, 218, 229)),
                BorderThickness = new Thickness(IsSelectedRibbonNode(panel) ? 2 : 1),
                Child = body
            };
        }

        private UIElement BuildRibbonItemPreview(RibbonLayoutNodeRow row)
        {
            if (row.Children.Count == 0)
            {
                var height = string.Equals(NormalizeButtonSize(row.Size), "small", StringComparison.OrdinalIgnoreCase) ? 28 : 58;
                return BuildRibbonPreviewButton(row, DisplayName(row.Text, row.FeatureId, RibbonNodeTypeDisplayName(row.NodeType)), 88, height);
            }

            var body = new StackPanel { Margin = new Thickness(0, 0, 8, 8) };
            body.Children.Add(BuildRibbonPreviewButton(row, DisplayName(row.Text, row.Id, RibbonNodeTypeDisplayName(row.NodeType)), 116, 28));

            var children = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            foreach (var child in row.Children)
            {
                children.Children.Add(BuildRibbonItemPreview(child));
            }

            body.Children.Add(children);
            return new Border
            {
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(6),
                Background = new SolidColorBrush(Color.FromRgb(247, 249, 252)),
                BorderBrush = IsSelectedRibbonNode(row) ? new SolidColorBrush(Color.FromRgb(25, 118, 210)) : new SolidColorBrush(Color.FromRgb(220, 226, 234)),
                BorderThickness = new Thickness(IsSelectedRibbonNode(row) ? 2 : 1),
                Child = body
            };
        }

        private Button BuildRibbonPreviewButton(RibbonLayoutNodeRow row, string text, double width, double height)
        {
            var button = new Button
            {
                Content = text,
                Width = width,
                Height = height,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(6, 2, 6, 2),
                Background = IsSelectedRibbonNode(row) ? new SolidColorBrush(Color.FromRgb(227, 242, 253)) : new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = IsSelectedRibbonNode(row) ? new SolidColorBrush(Color.FromRgb(25, 118, 210)) : new SolidColorBrush(Color.FromRgb(198, 210, 225)),
                BorderThickness = new Thickness(IsSelectedRibbonNode(row) ? 2 : 1)
            };
            button.Click += (sender, args) => SelectRibbonLayoutNode(row);
            return button;
        }

        private bool IsSelectedRibbonNode(RibbonLayoutNodeRow row)
        {
            return ReferenceEquals(row, _selectedRibbonLayoutNode);
        }

        private static bool ContainsRibbonNode(IEnumerable<RibbonLayoutNodeRow> rows, RibbonLayoutNodeRow target)
        {
            foreach (var row in rows ?? new List<RibbonLayoutNodeRow>())
            {
                if (ReferenceEquals(row, target)) return true;
                if (ContainsRibbonNode(row.Children, target)) return true;
            }

            return false;
        }

        private void RefreshRibbonFeaturePoolPlacementState()
        {
            var selectedFeatureId = (_ribbonFeaturePoolList.SelectedItem as RibbonFeaturePoolRow)?.FeatureId ?? string.Empty;
            var placedFeatureIds = PlacedFeatureIds();
            foreach (var row in _viewModel.RibbonFeaturePool)
            {
                row.IsPlaced = !string.IsNullOrWhiteSpace(row.FeatureId) && placedFeatureIds.Contains(row.FeatureId);
                row.DisplayText = row.FeatureName + (row.IsPlaced ? "（已放置）" : string.Empty);
            }

            _ribbonFeaturePoolList.ItemsSource = null;
            _ribbonFeaturePoolList.ItemsSource = _viewModel.RibbonFeaturePool;
            if (!string.IsNullOrWhiteSpace(selectedFeatureId))
            {
                _ribbonFeaturePoolList.SelectedItem = _viewModel.RibbonFeaturePool.FirstOrDefault(row => string.Equals(row.FeatureId, selectedFeatureId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private HashSet<string> PlacedFeatureIds()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectPlacedFeatureIds(_viewModel.RibbonLayoutNodes, ids);
            return ids;
        }

        private static void CollectPlacedFeatureIds(IEnumerable<RibbonLayoutNodeRow> rows, ISet<string> ids)
        {
            foreach (var row in rows ?? new List<RibbonLayoutNodeRow>())
            {
                if (string.Equals(row.NodeType, "pushButton", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(row.FeatureId))
                {
                    ids.Add(row.FeatureId);
                }

                CollectPlacedFeatureIds(row.Children, ids);
            }
        }

        private bool FeatureIdExistsInRibbonLayout(string featureId)
        {
            return FeatureIdExistsInRibbonLayout(featureId, null);
        }

        private bool FeatureIdExistsInRibbonLayout(string featureId, RibbonLayoutNodeRow? excludedNode)
        {
            if (string.IsNullOrWhiteSpace(featureId)) return false;
            return FeatureIdExistsInRibbonLayout(_viewModel.RibbonLayoutNodes, featureId, excludedNode);
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
            CountRibbonFeatureIds(_viewModel.RibbonLayoutNodes, counts);
            var duplicates = counts.Where(item => item.Value > 1).Select(item => item.Key).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException("布局中存在重复功能: " + string.Join(", ", duplicates));
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
            if (string.Equals(type, "pulldownButton", StringComparison.OrdinalIgnoreCase)) return "下拉按钮";
            if (string.Equals(type, "splitButton", StringComparison.OrdinalIgnoreCase)) return "拆分按钮";
            if (string.Equals(type, "stack", StringComparison.OrdinalIgnoreCase)) return "堆叠";
            if (string.Equals(type, "pushButton", StringComparison.OrdinalIgnoreCase)) return "功能按钮";
            return "布局项";
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
            RefreshStatus("已调整功能图标大小，保存并重启 Revit 后 Ribbon 按钮大小生效。");
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

    }
}
