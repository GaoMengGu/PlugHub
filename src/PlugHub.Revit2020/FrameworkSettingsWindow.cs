using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Packages;
using PlugHub.Framework.Runtime;
using PlugHub.Revit2020.Settings.Rows;

namespace PlugHub.Revit2020
{
    internal sealed class FrameworkSettingsWindow : Window
    {
        private const string SourcesFileName = "sources.json";
        private const string DefaultPackageManifestName = "package.json";
        private const string AdjacentPackageManifestPattern = "*.package.json";
        private const string DefaultRepositoryProvider = "gitee";
        private const string DefaultPublicRepository = "https://gitee.com/GaoMengGu/PlugHub_Packages";

        private readonly string _configDirectory;
        private FrameworkConfiguration _configuration;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };
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
        private List<ModuleManifestDocument> _moduleDocuments = new List<ModuleManifestDocument>();
        private ObservableCollection<ModuleRow> _moduleRows = new ObservableCollection<ModuleRow>();
        private ObservableCollection<FeatureRow> _featureRows = new ObservableCollection<FeatureRow>();
        private ObservableCollection<GroupRow> _groupRows = new ObservableCollection<GroupRow>();
        private readonly ObservableCollection<GroupOption> _groupOptions = new ObservableCollection<GroupOption>();
        private readonly IReadOnlyList<string> _buttonSizeOptions = new[] { "large", "small" };
        private readonly TextBlock _selectedFeatureName = new TextBlock();
        private readonly ComboBox _selectedFeatureGroupCombo = new ComboBox();
        private readonly ComboBox _selectedFeatureButtonSizeCombo = new ComboBox();
        private ObservableCollection<RepositoryRow> _repositoryRows = new ObservableCollection<RepositoryRow>();
        private ObservableCollection<RepositoryPackageRow> _repositoryPackageRows = new ObservableCollection<RepositoryPackageRow>();
        private ObservableCollection<PendingPackageOperationRow> _pendingPackageOperationRows = new ObservableCollection<PendingPackageOperationRow>();
        private int _dragSourceRowIndex = -1;
        private DataGrid? _dragSourceGrid;
        private bool _syncingSelectedFeatureEditor;

        public FrameworkSettingsWindow(string configDirectory, FrameworkConfiguration configuration)
        {
            _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _moduleDocuments = LoadModuleDocuments(_configuration);

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
            tabs.Items.Add(BuildFeaturesTab());
            tabs.Items.Add(BuildGroupsTab());
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

        private TabItem BuildFeaturesTab()
        {
            _featuresGrid.ContextMenu = BuildFeatureMenu();
            _featuresGrid.SelectionChanged += (sender, args) => SyncSelectedFeatureEditor();
            AttachGridBehaviors(_featuresGrid);

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var editor = BuildSelectedFeatureEditor();
            Grid.SetRow(editor, 0);
            layout.Children.Add(editor);
            Grid.SetRow(_featuresGrid, 1);
            layout.Children.Add(_featuresGrid);
            return BuildTab("功能", layout);
        }

        private UIElement BuildSelectedFeatureEditor()
        {
            _selectedFeatureName.Text = "未选择功能";
            _selectedFeatureName.MinWidth = 180;
            _selectedFeatureName.VerticalAlignment = VerticalAlignment.Center;
            _selectedFeatureName.Foreground = new SolidColorBrush(Color.FromRgb(45, 56, 72));

            _selectedFeatureGroupCombo.ItemsSource = _groupOptions;
            _selectedFeatureGroupCombo.DisplayMemberPath = nameof(GroupOption.DisplayText);
            _selectedFeatureGroupCombo.SelectedValuePath = nameof(GroupOption.Id);
            _selectedFeatureGroupCombo.MinWidth = 180;
            _selectedFeatureGroupCombo.Height = 26;
            _selectedFeatureGroupCombo.Margin = new Thickness(6, 0, 16, 0);
            _selectedFeatureGroupCombo.SelectionChanged += (sender, args) => ApplySelectedFeatureGroup();

            _selectedFeatureButtonSizeCombo.ItemsSource = _buttonSizeOptions;
            _selectedFeatureButtonSizeCombo.MinWidth = 96;
            _selectedFeatureButtonSizeCombo.Height = 26;
            _selectedFeatureButtonSizeCombo.Margin = new Thickness(6, 0, 0, 0);
            _selectedFeatureButtonSizeCombo.SelectionChanged += (sender, args) => ApplySelectedFeatureButtonSize();

            var editor = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 8, 8, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            editor.Children.Add(_selectedFeatureName);
            editor.Children.Add(EditorLabel("所属分组"));
            editor.Children.Add(_selectedFeatureGroupCombo);
            editor.Children.Add(EditorLabel("图标大小"));
            editor.Children.Add(_selectedFeatureButtonSizeCombo);

            SyncSelectedFeatureEditor();
            return editor;
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

        private TabItem BuildGroupsTab()
        {
            _groupsGrid.ContextMenu = BuildGroupMenu();
            _groupsGrid.CurrentCellChanged += (sender, args) =>
            {
                RefreshFeatureGroupOptions();
                RefreshFeatureCounts();
            };
            AttachGridBehaviors(_groupsGrid);
            return BuildTab("分组", _groupsGrid);
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
            return BuildTab("日志", _diagnosticsGrid);
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
            LoadRepositoryRows();
            LoadRepositoryPackageRows(new List<RepositoryPackageDescriptor>());
            LoadPendingPackageOperationRows();
            LoadDiagnosticRows(FrameworkRuntimeState.Current);
            RefreshStatus("已加载配置。设置窗口会保存根配置和独立模块清单；Ribbon 布局、图标和按钮大小需重启 Revit 重绘。");
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

            _moduleRows = new ObservableCollection<ModuleRow>(EditableModules()
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
                }));
            RefreshPluginPackagePositions();
            _pluginPackagesGrid.ItemsSource = _moduleRows;
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

            _groupRows = new ObservableCollection<GroupRow>(featureGroups
                .OrderBy(group => group.Order)
                .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Id, StringComparer.OrdinalIgnoreCase));
            RefreshGroupPositions();
            RefreshFeatureGroupOptions();
            _groupsGrid.ItemsSource = _groupRows;
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

            _featureRows = new ObservableCollection<FeatureRow>(EditableModules()
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
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase));
            SortFeatureRowsForRuntimeOrder();
            RefreshFeaturePositionsByGroup();
            _featuresGrid.ItemsSource = _featureRows;
            if (_featureRows.Count > 0)
            {
                _featuresGrid.SelectedIndex = 0;
            }

            SyncSelectedFeatureEditor();
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

            _repositoryRows = new ObservableCollection<RepositoryRow>((_configuration.Modules.Repositories ?? new List<PackageRepositoryConfiguration>())
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
                }));
            _repositoriesGrid.ItemsSource = _repositoryRows;
        }

        private void LoadRepositoryPackageRows(IEnumerable<RepositoryPackageDescriptor> packages)
        {
            _repositoryPackagesGrid.Columns.Clear();
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.RepositoryId), "仓库", true, 1.0));
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.PackageId), "插件包 ID", true, 1.4));
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.DisplayName), "功能", true, 1.8));
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.Version), "版本", true, 0.8));
            _repositoryPackagesGrid.Columns.Add(TextColumn(nameof(RepositoryPackageRow.InstallState), "安装状态", true, 0.9));

            _repositoryPackageRows = new ObservableCollection<RepositoryPackageRow>((packages ?? new List<RepositoryPackageDescriptor>())
                .Select(package => RepositoryPackageRow.FromDescriptor(package, IsLoadedInCurrentRuntime(package.PackageId, package.ModuleId))));
            _repositoryPackagesGrid.ItemsSource = _repositoryPackageRows;
        }

        private void LoadPendingPackageOperationRows()
        {
            _pendingPackageOperationsGrid.Columns.Clear();
            _pendingPackageOperationsGrid.Columns.Add(TextColumn(nameof(PendingPackageOperationRow.Operation), "操作", true, 0.6));
            _pendingPackageOperationsGrid.Columns.Add(TextColumn(nameof(PendingPackageOperationRow.PackageId), "插件包", true, 1.2));
            _pendingPackageOperationsGrid.Columns.Add(TextColumn(nameof(PendingPackageOperationRow.CreatedAtUtc), "创建时间", true, 1.0));

            _pendingPackageOperationRows = new ObservableCollection<PendingPackageOperationRow>(
                _packageRepositoryService.ListPendingOperations(BaseDirectory()).Select(PendingPackageOperationRow.FromOperation));
            _pendingPackageOperationsGrid.ItemsSource = _pendingPackageOperationRows;
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

                    foreach (var row in _repositoryRows)
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
            return _repositoryRows
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

            _diagnosticsGrid.ItemsSource = rows;
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

            _diagnosticsGrid.ItemsSource = rows;
        }

        private void LoadPostSaveDiagnosticRows()
        {
            _diagnosticsGrid.Columns.Clear();
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Severity), "级别", true, 0.8));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Code), "代码", true, 1.1));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Scope), "对象", true, 1.4));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Message), "消息", true, 4.6));
            _diagnosticsGrid.ItemsSource = new[]
            {
                new DiagnosticRow
                {
                    Severity = "Info",
                    Code = "PH-SAVED",
                    Scope = "settings",
                    Message = "配置已保存。仓库设置和 Ribbon 布局会在重启 Revit 后重新加载；此处不显示保存前的运行时日志。"
                }
            };
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
            ApplyGroupRows();
            ApplyFeatureRows();
            ApplyRepositoryRows();

            Directory.CreateDirectory(_configDirectory);
            SaveModuleDocuments();
            SaveJson(Path.Combine(_configDirectory, "views.json"), _configuration.Views);
            SaveJson(Path.Combine(_configDirectory, "feature-combinations.json"), _configuration.FeatureCombinations);

            LoadPostSaveDiagnosticRows();
            LoadRepositoryRows();
            RefreshStatus("已保存配置。插件包、分组、功能和仓库设置已写回对应清单；Ribbon 布局、图标、按钮大小需重启 Revit 重绘。");
        }

        private void ReloadFromDisk()
        {
            try
            {
                _configuration = FrameworkConfigurationLoader.LoadFromDirectory(_configDirectory);
                _moduleDocuments = LoadModuleDocuments(_configuration);
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
            var rows = _moduleRows.ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);
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
            view.Groups = _groupRows
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

            foreach (var row in _featureRows)
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

        private void ApplyRepositoryRows()
        {
            _configuration.Modules.Repositories = _repositoryRows
                .Where(row => !string.IsNullOrWhiteSpace(row.Id))
                .Select(row =>
                {
                    var repository = row.ToConfiguration();
                    _credentialService.ProtectForSave(repository);
                    return repository;
                })
                .ToList();
        }

        private void SaveJson(string path, object value)
        {
            File.WriteAllText(path, _serializer.Serialize(value));
        }

        private List<ModuleManifestDocument> LoadModuleDocuments(FrameworkConfiguration configuration)
        {
            var documents = new List<ModuleManifestDocument>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddModuleDocument(documents, seenPaths, Path.Combine(_configDirectory, SourcesFileName), configuration.Modules);

            var baseDirectory = Directory.GetParent(_configDirectory)?.FullName ?? _configDirectory;
            foreach (var packageDirectory in configuration.Modules.PackageDirectories ?? new List<string>())
            {
                // Configured packageDirectories are editable manifests too.
                foreach (var manifestPath in FindModuleManifests(ResolvePath(baseDirectory, packageDirectory)))
                {
                    var manifest = TryReadModulesConfiguration(manifestPath);
                    if (manifest != null)
                    {
                        AddModuleDocument(documents, seenPaths, manifestPath, manifest);
                    }
                }
            }

            foreach (var source in (configuration.Modules.ModuleSources ?? new List<ModuleSourceConfiguration>())
                .Where(source => source.Enabled && string.Equals(source.Type, "localFolder", StringComparison.OrdinalIgnoreCase)))
            {
                var sourceDirectory = ResolveSourceDirectory(baseDirectory, source);
                if (IsDefaultManifestPath(source.ManifestPath))
                {
                    foreach (var manifestPath in FindModuleManifests(sourceDirectory))
                    {
                        var manifest = TryReadModulesConfiguration(manifestPath);
                        if (manifest != null)
                        {
                            AddModuleDocument(documents, seenPaths, manifestPath, manifest);
                        }
                    }

                    continue;
                }

                var explicitManifestPath = Path.Combine(sourceDirectory, source.ManifestPath.Trim());
                var explicitManifest = TryReadModulesConfiguration(explicitManifestPath);
                if (explicitManifest != null)
                {
                    AddModuleDocument(documents, seenPaths, explicitManifestPath, explicitManifest);
                }
            }

            return documents;
        }

        private void SaveModuleDocuments()
        {
            foreach (var document in _moduleDocuments)
            {
                SaveJson(document.Path, document.Modules);
            }
        }

        private IEnumerable<ModuleConfiguration> EditableModules()
        {
            return _moduleDocuments.SelectMany(document => document.Modules.Modules ?? new List<ModuleConfiguration>());
        }

        private ModulesConfiguration? TryReadModulesConfiguration(string path)
        {
            if (!File.Exists(path)) return null;

            Dictionary<string, object>? root;
            try
            {
                root = _serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }

            if (root == null || !ContainsKey(root, "schemaVersion") || !ContainsKey(root, "modules")) return null;

            return _serializer.Deserialize<ModulesConfiguration>(File.ReadAllText(path));
        }

        private static bool ContainsKey(Dictionary<string, object> source, string key)
        {
            return source.Keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddModuleDocument(ICollection<ModuleManifestDocument> documents, ISet<string> seenPaths, string path, ModulesConfiguration modules)
        {
            if (string.IsNullOrWhiteSpace(path) || modules == null) return;
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) && !IsPackageManifestFileName(Path.GetFileName(fullPath))) return;
            if (!seenPaths.Add(fullPath)) return;
            documents.Add(new ModuleManifestDocument(fullPath, modules));
        }

        private static IEnumerable<string> FindModuleManifests(string sourceDirectory)
        {
            if (!Directory.Exists(sourceDirectory)) yield break;

            var rootManifest = Path.Combine(sourceDirectory, DefaultPackageManifestName);
            if (File.Exists(rootManifest))
            {
                yield return rootManifest;
            }

            var manifests = Directory.GetFiles(sourceDirectory, DefaultPackageManifestName, SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(sourceDirectory, AdjacentPackageManifestPattern, SearchOption.AllDirectories))
                .Where(path => !string.Equals(path, rootManifest, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var manifest in manifests)
            {
                yield return manifest;
            }
        }

        private static bool IsPackageManifestFileName(string fileName)
        {
            return string.Equals(fileName, DefaultPackageManifestName, StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".package.json", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolvePath(string baseDirectory, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return baseDirectory;
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
        }

        private static string ResolveSourceDirectory(string baseDirectory, ModuleSourceConfiguration source)
        {
            if (source == null) return baseDirectory;
            if (!string.IsNullOrWhiteSpace(source.Path)) return ResolvePath(baseDirectory, source.Path);

            return baseDirectory;
        }

        private static bool IsDefaultManifestPath(string manifestPath)
        {
            return string.IsNullOrWhiteSpace(manifestPath)
                || string.Equals(manifestPath.Trim(), DefaultPackageManifestName, StringComparison.OrdinalIgnoreCase);
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
            var id = UniqueRepositoryId(_repositoryRows, "repository");
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
            _repositoryRows.Add(row);
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
                var baseDirectory = Directory.GetParent(_configDirectory)?.FullName ?? _configDirectory;
                var packages = _packageRepositoryService.Browse(baseDirectory, repository, out var diagnostics);

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

                _moduleDocuments = LoadModuleDocuments(_configuration);
                LoadGroupRows();
                LoadFeatureRows();
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
            LoadPendingPackageOperationRows();
            RefreshStatus(result.Message);
        }

        private void RefreshRepositoryPackageInstallState(string packageId, string installDirectory)
        {
            foreach (var row in _repositoryPackageRows.Where(item =>
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
            var id = UniqueGroupId(_groupRows, "custom-group");
            var index = _groupRows.Count + 1;
            var row = new GroupRow
            {
                Id = id,
                Name = "自定义分组 " + index,
                FeatureCount = 0,
                Order = (_groupRows.Count + 1) * 100
            };

            _groupRows.Add(row);
            RefreshGroupPositions();
            RefreshFeatureGroupOptions();
            _groupsGrid.SelectedItem = row;
            RefreshStatus("已新增自定义分组，可在功能页把功能移动到该分组。");
        }

        private void RemoveSelectedGroup()
        {
            if (!(_groupsGrid.SelectedItem is GroupRow row)) return;

            EndGridEdits();
            var isInUse = _featureRows.Any(feature => string.Equals(feature.Group, row.Id, StringComparison.OrdinalIgnoreCase));
            if (isInUse)
            {
                RefreshStatus("该分组仍有功能使用。请先在功能页把功能移动到其他分组。");
                return;
            }

            _groupRows.Remove(row);
            RefreshGroupPositions();
            RefreshFeatureGroupOptions();
            RefreshStatus("已删除未使用的自定义分组。");
        }

        private List<GroupOption> GroupOptionsForFeatureRows()
        {
            var rows = _groupRows.Any()
                ? _groupRows
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
            var row = _groupRows.FirstOrDefault(item => string.Equals(item.Id, groupId, StringComparison.OrdinalIgnoreCase));
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
                _repositoryRows.Remove(row);
            }
        }

        private void MoveSelectedRow(DataGrid grid, int direction)
        {
            var sourceIndex = grid.SelectedIndex;
            if (sourceIndex < 0) return;

            var targetIndex = sourceIndex + direction;
            if (grid == _pluginPackagesGrid)
            {
                MoveRow(_moduleRows, sourceIndex, targetIndex);
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
                MoveRow(_groupRows, sourceIndex, targetIndex);
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

            var sameGroupIndexes = _featureRows
                .Select((feature, index) => new { Feature = feature, Index = index })
                .Where(item => string.Equals(item.Feature.Group, row.Group, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Index)
                .ToList();
            var groupPosition = sameGroupIndexes.IndexOf(_featureRows.IndexOf(row));
            var targetGroupPosition = groupPosition + direction;
            if (groupPosition < 0 || targetGroupPosition < 0 || targetGroupPosition >= sameGroupIndexes.Count) return;

            MoveRow(_featureRows, _featureRows.IndexOf(row), sameGroupIndexes[targetGroupPosition]);
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
            for (var index = 0; index < _moduleRows.Count; index++)
            {
                _moduleRows[index].Order = (index + 1) * 100;
                _moduleRows[index].PositionText = "第 " + (index + 1) + " 项";
            }

            SafeRefreshGrid(_pluginPackagesGrid);
        }

        private void SortFeatureRowsForRuntimeOrder()
        {
            var sorted = _featureRows
                .OrderBy(row => GroupOrderForFeature(row.Group))
                .ThenBy(row => GroupDisplayName(row.Group), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Order)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _featureRows.Clear();
            foreach (var row in sorted)
            {
                _featureRows.Add(row);
            }
        }

        private void RefreshFeaturePositionsByGroup()
        {
            var groupIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < _featureRows.Count; index++)
            {
                var row = _featureRows[index];
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
            for (var index = 0; index < _groupRows.Count; index++)
            {
                _groupRows[index].Order = (index + 1) * 100;
                _groupRows[index].PositionText = "第 " + (index + 1) + " 项";
                _groupRows[index].FeatureCountText = _groupRows[index].FeatureCount + " 个";
            }

            SafeRefreshGrid(_groupsGrid);
        }

        private void RefreshFeatureCounts()
        {
            foreach (var group in _groupRows)
            {
                group.FeatureCount = _featureRows.Count(feature => string.Equals(feature.Group, group.Id, StringComparison.OrdinalIgnoreCase));
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
                MoveRow(_moduleRows, _dragSourceRowIndex, targetIndex);
                RecalculatePluginPackageOrders();
            }
            else if (grid == _featuresGrid)
            {
                var dragged = _featureRows[_dragSourceRowIndex];
                var target = _featureRows[targetIndex];
                if (!string.Equals(dragged.Group, target.Group, StringComparison.OrdinalIgnoreCase))
                {
                    dragged.Group = target.Group;
                    UpdateFeatureDisplayFields(dragged);
                }

                MoveRow(_featureRows, _dragSourceRowIndex, targetIndex);
                RecalculateFeatureOrders();
                SelectFeatureRow(dragged);
                ResetDragSource();
                return;
            }
            else if (grid == _groupsGrid)
            {
                MoveRow(_groupRows, _dragSourceRowIndex, targetIndex);
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
            return Directory.GetParent(_configDirectory)?.FullName ?? _configDirectory;
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
            var group = _groupRows.FirstOrDefault(row => string.Equals(row.Id, groupId, StringComparison.OrdinalIgnoreCase));
            return group?.Order > 0 ? group.Order : int.MaxValue;
        }

        private string GroupDisplayName(string groupId)
        {
            var group = _groupRows.FirstOrDefault(row => string.Equals(row.Id, groupId, StringComparison.OrdinalIgnoreCase));
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

        private sealed class ModuleManifestDocument
        {
            public ModuleManifestDocument(string path, ModulesConfiguration modules)
            {
                Path = path ?? string.Empty;
                Modules = modules ?? new ModulesConfiguration();
            }

            public string Path { get; }
            public ModulesConfiguration Modules { get; }
        }
    }
}
