using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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
using PlugHub.Framework.Settings;
using PlugHub.Framework.Updates;
using PlugHub.Manager.Maintenance;
using PlugHub.Manager.Settings;
using PlugHub.Manager.Settings.RibbonDesigner;
using PlugHub.Manager.Settings.Rows;
using PlugHub.Wpf;

namespace PlugHub.Manager
{
    public sealed class FrameworkSettingsWindow : Window
    {
        private const string DefaultPackageManifestName = "packages.json";
        private const string DefaultRepositoryProvider = "gitee";
        private const string DefaultPublicRepository = "https://gitee.com/GaoMengGu/PlugHub_Packages";
        private const string DefaultRibbonDesignerPanelId = "default";
        private const string DefaultRibbonDesignerPanelName = "默认";
        private const string RibbonDesignerBrowseIconAction = "__browse_icon__";
        private const string RibbonDesignerClearIconAction = "__clear_icon__";
        private const double RepositorySettingsDefaultWidth = 1140.0;
        private const double RepositorySettingsDefaultHeight = 600.0;
        private const double SettingsWindowOuterMargin = 12.0;
        private const double SettingsWindowOuterMarginWidth = SettingsWindowOuterMargin * 2.0;
        private const double RepositoryCardRowChromeReserve = 60.0;
        private const double RepositoryCardRowWidth = RepositorySettingsDefaultWidth - SettingsWindowOuterMarginWidth - RepositoryCardRowChromeReserve;
        private const double RepositorySourceColumns = 4.0;
        private const double RepositoryPackageColumns = 3.0;
        private const double RepositoryPackageCardVerticalMargin = 4.0;
        private const double RepositorySourceCardBottomMargin = RepositoryPackageCardVerticalMargin * 2.0;
        private const double RepositoryCardHorizontalMargin = RepositoryPackageCardVerticalMargin;
        private const double RepositoryCardHorizontalMarginWidth = RepositoryCardHorizontalMargin * 2.0;
        private const double RepositorySourceScrollbarSafetyReserve = 16.0;
        private const double RepositorySourceCardRowWidth = RepositoryCardRowWidth - RepositorySourceScrollbarSafetyReserve;
        private const double RepositorySourceCardSlotWidth = RepositorySourceCardRowWidth / RepositorySourceColumns;
        private const double RepositorySourceCardWidth = RepositorySourceCardSlotWidth - RepositoryCardHorizontalMarginWidth;
        private const double RepositoryPackageCardSlotWidth = RepositoryCardRowWidth / RepositoryPackageColumns;
        private const double RepositoryPackageCardWidth = RepositoryPackageCardSlotWidth - RepositoryCardHorizontalMarginWidth;
        private const double RepositoryPackageActionWidth = 72.0;
        private const double RepositoryPackageActionHeight = 26.0;
        private static readonly string[] SameNameIconExtensions = { ".png", ".ico", ".jpg", ".jpeg", ".bmp" };

        private readonly SettingsConfigurationStore _configurationStore;
        private readonly FrameworkSettingsViewModel _viewModel = new FrameworkSettingsViewModel();
        private FrameworkConfiguration _configuration;
        private readonly PackageRepositoryService _packageRepositoryService = new PackageRepositoryService();
        private readonly RepositoryCredentialService _credentialService = new RepositoryCredentialService();
        private readonly FrameworkUpdateService _frameworkUpdateService = new FrameworkUpdateService();
        private readonly int _hostProcessId;
        private readonly RepositorySettingsController _repositorySettingsController = new RepositorySettingsController();
        private readonly RibbonDesignerMapper _ribbonDesignerMapper = new RibbonDesignerMapper();
        private readonly RibbonDesignerDropService _ribbonDesignerDropService = new RibbonDesignerDropService();
        private readonly RibbonLayoutDiffService _ribbonLayoutDiffService = new RibbonLayoutDiffService();
        private readonly DataGrid _pluginPackagesGrid = CreateGrid();
        private readonly DataGrid _featuresGrid = CreateGrid();
        private readonly DataGrid _groupsGrid = CreateGrid();
        private readonly DataGrid _diagnosticsGrid = CreateGrid();
        private readonly ListBox _repositorySourcesList = new ListBox();
        private readonly ListBox _warehousePackageList = new ListBox();
        private readonly TextBox _repositoryPackageSearchText = new TextBox();
        private readonly ComboBox _repositoryPackageStateFilter = new ComboBox();
        private readonly ComboBox _repositoryPackageRepositoryFilter = new ComboBox();
        private readonly ComboBox _repositoryPackageTagFilter = new ComboBox();
        private readonly TextBlock _statusText = new TextBlock();
        private List<SettingsConfigurationStore.ModuleManifestDocument> _moduleDocuments = new List<SettingsConfigurationStore.ModuleManifestDocument>();
        private readonly List<RepositoryPackageRow> _repositoryPackageRows = new List<RepositoryPackageRow>();
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
        private Button? _checkFrameworkIconButton;
        private Button? _uninstallIconButton;

        public FrameworkSettingsWindow(string configDirectory, FrameworkConfiguration configuration, int hostProcessId)
        {
            _hostProcessId = hostProcessId;
            _configurationStore = new SettingsConfigurationStore(configDirectory ?? throw new ArgumentNullException(nameof(configDirectory)));
            _configuration = _configurationStore.Load(configuration ?? throw new ArgumentNullException(nameof(configuration)));
            _moduleDocuments = _configurationStore.LoadModuleDocuments(_configuration);

            Title = "PlugHub Manager";
            Width = RepositorySettingsDefaultWidth;
            Height = RepositorySettingsDefaultHeight;
            MinWidth = 1000;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            RevitUiTheme.Apply(this);

            Content = BuildLayout();
            LoadRows();
        }

        private UIElement BuildLayout()
        {
            var theme = RevitUiTheme.Current;
            var root = new Grid
            {
                Margin = new Thickness(SettingsWindowOuterMargin),
                Background = theme.WindowBackground
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var tabs = new TabControl
            {
                Background = Brushes.Transparent,
                BorderBrush = theme.BorderBrush
            };
            tabs.Items.Add(BuildRibbonLayoutTab());
            tabs.Items.Add(BuildRepositoriesTab());
            tabs.Items.Add(BuildAboutTab());
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            return root;
        }

        private UIElement BuildHeader()
        {
            var theme = RevitUiTheme.Current;
            var header = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Vertical };
            titleStack.Children.Add(new TextBlock
            {
                Text = "PlugHub Manager",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextBrush
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "Revit 2020 模块化插件框架",
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = theme.MutedTextBrush
            });
            Grid.SetColumn(titleStack, 0);
            header.Children.Add(titleStack);

            var metrics = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            metrics.Children.Add(BuildHeaderMetric("模块", ConfiguredModuleCount().ToString(CultureInfo.InvariantCulture)));
            metrics.Children.Add(BuildHeaderMetric("功能", ConfiguredFeatureCount().ToString(CultureInfo.InvariantCulture)));
            metrics.Children.Add(BuildHeaderMetric("仓库", ConfiguredRepositoryCount().ToString(CultureInfo.InvariantCulture)));
            Grid.SetColumn(metrics, 1);
            header.Children.Add(metrics);

            return header;
        }

        private static UIElement BuildHeaderMetric(string label, string value)
        {
            var theme = RevitUiTheme.Current;
            var text = new TextBlock
            {
                Text = label + " " + value,
                Foreground = theme.MutedTextBrush,
                FontSize = 11
            };

            return new Border
            {
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(7, 3, 7, 3),
                Background = theme.SurfaceBackground,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                Child = text
            };
        }

        private UIElement BuildFooter()
        {
            var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusText.VerticalAlignment = VerticalAlignment.Center;
            _statusText.TextWrapping = TextWrapping.Wrap;
            _statusText.Foreground = RevitUiTheme.Current.MutedTextBrush;
            Grid.SetColumn(_statusText, 0);
            footer.Children.Add(_statusText);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttons.Children.Add(BuildRepositoryDiagnosticsMenu());
            buttons.Children.Add(CreateButton("重新加载", (sender, args) => ReloadFromDisk()));
            buttons.Children.Add(CreateButton("保存配置", (sender, args) => TrySave()));
            buttons.Children.Add(CreateButton("关闭", (sender, args) => Close()));
            Grid.SetColumn(buttons, 1);
            footer.Children.Add(buttons);

            return footer;
        }

        private Button BuildRepositoryDiagnosticsMenu()
        {
            var button = CreateButton("诊断", OpenRepositoryDiagnosticsMenu);
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("打开日志目录", (sender, args) => OpenLogsDirectory()));
            menu.Items.Add(MenuItem("导出日志", (sender, args) => ExportLogs()));
            menu.Items.Add(MenuItem("查看诊断", (sender, args) => ShowRepositoryDiagnostics()));
            button.ContextMenu = menu;
            return button;
        }

        private void OpenRepositoryDiagnosticsMenu(object sender, RoutedEventArgs args)
        {
            if (!(sender is Button button) || button.ContextMenu == null) return;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Top;
            button.ContextMenu.IsOpen = true;
        }

        private void ShowRepositoryDiagnostics()
        {
            FrameworkStatusWindow.ShowLogs(
                "PlugHub 诊断",
                "当前运行诊断。日志默认写入本地 logs 目录。",
                FrameworkRuntimeState.Current?.Diagnostics ?? Array.Empty<DiagnosticMessage>());
        }

        private static TextBlock EditorLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = RevitUiTheme.Current.MutedTextBrush
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
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var repositoriesToolbar = BuildRepositoryToolbar();
            Grid.SetRow(repositoriesToolbar, 0);
            layout.Children.Add(repositoriesToolbar);
            var sources = BuildRepositorySourceCards();
            Grid.SetRow(sources, 1);
            layout.Children.Add(sources);

            var packagesToolbar = BuildRepositoryPackageToolbar();
            Grid.SetRow(packagesToolbar, 2);
            layout.Children.Add(packagesToolbar);
            var packages = BuildRepositoryPackageList();
            Grid.SetRow(packages, 3);
            layout.Children.Add(packages);

            return BuildTab("仓库", layout);
        }

        private TabItem BuildAboutTab()
        {
            var theme = RevitUiTheme.Current;
            var root = new Grid { Margin = new Thickness(14) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var summary = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
            summary.Children.Add(BuildAboutHeader());
            summary.Children.Add(new TextBlock
            {
                Text = "面向 Revit 2020 的模块化插件框架。",
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = theme.MutedTextBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            });
            summary.Children.Add(new TextBlock
            {
                Text = "框架层负责模块契约、发现、启用/禁用、排序组合、诊断和 Revit 2020 入口适配；具体业务命令由外部插件包提供。",
                Margin = new Thickness(0, 10, 0, 0),
                Foreground = theme.MutedTextBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            });

            var metrics = new UniformGrid
            {
                Columns = 2,
                Margin = new Thickness(0, 18, 0, 0)
            };
            metrics.Children.Add(BuildAboutMetric("模块数", ConfiguredModuleCount().ToString(CultureInfo.InvariantCulture)));
            metrics.Children.Add(BuildAboutMetric("功能数", ConfiguredFeatureCount().ToString(CultureInfo.InvariantCulture)));
            metrics.Children.Add(BuildAboutMetric("仓库数", ConfiguredRepositoryCount().ToString(CultureInfo.InvariantCulture)));
            metrics.Children.Add(BuildAboutMetric("目标版本", "Revit 2020"));
            summary.Children.Add(metrics);
            Grid.SetColumn(summary, 0);
            root.Children.Add(summary);

            var details = new StackPanel();
            details.Children.Add(BuildAboutSection(
                "运行环境",
                BuildAboutInfoRow("版本", AssemblyVersionText()),
                BuildAboutInfoRow("主题", RevitUiTheme.Current.IsDark ? "深色" : "浅色"),
                BuildAboutInfoRow("适配层", "PlugHub.Revit2020 / .NET Framework 4.8")));
            details.Children.Add(BuildAboutSection(
                "路径",
                BuildAboutInfoRow("根目录", BaseDirectory()),
                BuildAboutInfoRow("配置", FrameworkRuntimeState.ConfigDirectory ?? string.Empty),
                BuildAboutInfoRow("日志", PlugHubLogger.LogsDirectory(BaseDirectory()))));
            details.Children.Add(BuildAboutSection(
                "诊断",
                BuildAboutInfoRow("日志消息", (FrameworkRuntimeState.Current?.Diagnostics.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
                BuildAboutInfoRow("待重启操作", _packageRepositoryService.ListPendingOperations(BaseDirectory()).Count.ToString(CultureInfo.InvariantCulture)),
                BuildAboutInfoRow("静态验证", "dotnet run --project src/PlugHub.StaticValidation/PlugHub.StaticValidation.csproj")));
            Grid.SetColumn(details, 1);
            root.Children.Add(details);

            return BuildTab("关于", new ScrollViewer
            {
                Content = root,
                Background = theme.PanelBackground,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });
        }

        private static UIElement BuildAboutMetric(string label, string value)
        {
            var theme = RevitUiTheme.Current;
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextBrush
            });
            panel.Children.Add(new TextBlock
            {
                Text = label,
                Margin = new Thickness(0, 3, 0, 0),
                Foreground = theme.MutedTextBrush
            });

            return new Border
            {
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(10, 8, 10, 8),
                Background = theme.SurfaceBackground,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                Child = panel
            };
        }

        private static UIElement BuildAboutSection(string title, params UIElement[] rows)
        {
            var theme = RevitUiTheme.Current;
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextBrush,
                Margin = new Thickness(0, 0, 0, 8)
            });
            foreach (var row in rows)
            {
                panel.Children.Add(row);
            }

            return new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10),
                Background = theme.SurfaceBackground,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(1),
                Child = panel
            };
        }

        private UIElement BuildAboutHeader()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = "PlugHub",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = RevitUiTheme.Current.TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = AssemblyVersionText(),
                Margin = new Thickness(8, 4, 0, 0),
                FontSize = 13,
                Foreground = RevitUiTheme.Current.MutedTextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            _checkFrameworkIconButton = CreateIconButton("refresh", "检查更新", (sender, args) => CheckFrameworkUpdate());
            _checkFrameworkIconButton.Margin = new Thickness(10, 0, 0, 0);
            panel.Children.Add(_checkFrameworkIconButton);

            _uninstallIconButton = CreateIconButton("uninstall", "卸载 PlugHub", (sender, args) => LaunchUninstaller());
            _uninstallIconButton.Margin = new Thickness(4, 0, 0, 0);
            panel.Children.Add(_uninstallIconButton);

            return panel;
        }

        private static UIElement BuildAboutInfoRow(string label, string value)
        {
            var theme = RevitUiTheme.Current;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label,
                Foreground = theme.SubtleTextBrush
            };
            Grid.SetColumn(labelBlock, 0);
            row.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value ?? string.Empty,
                Foreground = theme.MutedTextBrush,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(valueBlock, 1);
            row.Children.Add(valueBlock);
            return row;
        }

        private UIElement BuildRepositorySourceCards()
        {
            _repositorySourcesList.BorderThickness = new Thickness(0);
            _repositorySourcesList.Background = Brushes.Transparent;
            _repositorySourcesList.ContextMenu = BuildRepositoryMenu();
            _repositorySourcesList.Height = 74;
            _repositorySourcesList.SelectionMode = SelectionMode.Single;
            _repositorySourcesList.ItemTemplate = BuildRepositorySourceCardTemplate();
            _repositorySourcesList.PreviewMouseRightButtonDown += ListBoxPreviewMouseRightButtonDown;
            _repositorySourcesList.SelectionChanged += RepositorySourceSelectionChanged;
            _repositorySourcesList.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            _repositorySourcesList.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            _repositorySourcesList.SetValue(ScrollViewer.CanContentScrollProperty, false);

            var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
            panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            _repositorySourcesList.ItemsPanel = new ItemsPanelTemplate(panelFactory);

            return BuildRepositorySourceScrollViewer(_repositorySourcesList);
        }

        private static ScrollViewer BuildRepositorySourceScrollViewer(UIElement content)
        {
            var scroll = new ScrollViewer
            {
                Height = 94,
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, false);
            return scroll;
        }

        private DataTemplate BuildRepositorySourceCardTemplate()
        {
            var theme = RevitUiTheme.Current;
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.WidthProperty, RepositorySourceCardWidth);
            border.SetValue(Border.MarginProperty, new Thickness(RepositoryCardHorizontalMargin, RepositoryPackageCardVerticalMargin, RepositoryCardHorizontalMargin, RepositorySourceCardBottomMargin));
            border.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
            border.SetValue(Border.BackgroundProperty, theme.PanelBackground);
            border.SetValue(Border.BorderBrushProperty, theme.BorderBrush);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
            border.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(BrowseRepositorySourceCacheFromCard));

            var root = new FrameworkElementFactory(typeof(DockPanel));
            border.AppendChild(root);

            var toggle = new FrameworkElementFactory(typeof(CheckBox));
            toggle.SetValue(DockPanel.DockProperty, Dock.Left);
            toggle.SetValue(CheckBox.WidthProperty, 22.0);
            toggle.SetValue(CheckBox.HeightProperty, 26.0);
            toggle.SetValue(CheckBox.MarginProperty, new Thickness(0, 0, 8, 0));
            toggle.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Top);
            toggle.SetValue(FrameworkElement.ToolTipProperty, "启用仓库源");
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(RepositoryRow.Enabled)));
            toggle.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(ToggleRepositorySourceFromCard));
            root.AppendChild(toggle);

            var actions = new FrameworkElementFactory(typeof(StackPanel));
            actions.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            actions.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Top);
            actions.SetValue(DockPanel.DockProperty, Dock.Right);
            root.AppendChild(actions);

            actions.AppendChild(BuildRepositorySourceMoreGlyph());

            var text = new FrameworkElementFactory(typeof(StackPanel));
            text.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            root.AppendChild(text);

            var title = new FrameworkElementFactory(typeof(TextBlock));
            title.SetBinding(TextBlock.TextProperty, new Binding(nameof(RepositoryRow.DisplayName)));
            title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            title.SetValue(TextBlock.ForegroundProperty, theme.TextBrush);
            title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            title.SetBinding(FrameworkElement.ToolTipProperty, new Binding(nameof(RepositoryRow.DisplayName)));
            text.AppendChild(title);

            var meta = new FrameworkElementFactory(typeof(TextBlock));
            meta.SetBinding(TextBlock.TextProperty, new Binding(".") { Converter = new RepositoryMetaLabelConverter() });
            meta.SetValue(TextBlock.MarginProperty, new Thickness(0, 4, 0, 0));
            meta.SetValue(TextBlock.ForegroundProperty, theme.MutedTextBrush);
            text.AppendChild(meta);

            return new DataTemplate { VisualTree = border };
        }

        private FrameworkElementFactory BuildRepositorySourceMoreGlyph()
        {
            var theme = RevitUiTheme.Current;
            var button = new FrameworkElementFactory(typeof(TextBlock));
            button.SetValue(TextBlock.TextProperty, "...");
            button.SetValue(TextBlock.FontSizeProperty, 18.0);
            button.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            button.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            button.SetValue(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight);
            button.SetValue(TextBlock.LineHeightProperty, 14.0);
            button.SetValue(TextBlock.ForegroundProperty, theme.MutedTextBrush);
            button.SetValue(FrameworkElement.WidthProperty, 22.0);
            button.SetValue(FrameworkElement.HeightProperty, 16.0);
            button.SetValue(FrameworkElement.MarginProperty, new Thickness(2, -5, 0, 0));
            button.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
            button.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
            button.SetValue(FrameworkElement.ToolTipProperty, "更多操作");
            button.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OpenRepositorySourceMenuFromCard));
            return button;
        }

        private UIElement BuildRepositoryToolbar()
        {
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 8, 8, 6)
            };
            actions.Children.Add(CreateButton("一键同步", (sender, args) => CheckRepositoryUpdates()));
            actions.Children.Add(CreateButton("新增仓库", (sender, args) => AddRepository()));
            return BuildToolbarHeader("仓库源", actions);
        }

        private UIElement BuildRepositoryPackageToolbar()
        {
            _repositoryPackageSearchText.Width = 220;
            _repositoryPackageSearchText.Height = 26;
            _repositoryPackageSearchText.Margin = new Thickness(8, 0, 0, 0);
            _repositoryPackageSearchText.VerticalContentAlignment = VerticalAlignment.Center;
            _repositoryPackageSearchText.TextChanged += RepositoryPackageFilterChanged;

            _repositoryPackageStateFilter.Width = 110;
            _repositoryPackageStateFilter.Height = 26;
            _repositoryPackageStateFilter.Margin = new Thickness(8, 0, 0, 0);
            _repositoryPackageStateFilter.ItemsSource = new[] { "全部", "未安装", "可更新", "已安装", "待重启" };
            _repositoryPackageStateFilter.SelectedIndex = 0;
            _repositoryPackageStateFilter.SelectionChanged += RepositoryPackageFilterChanged;

            _repositoryPackageTagFilter.Width = 140;
            _repositoryPackageTagFilter.Height = 26;
            _repositoryPackageTagFilter.Margin = new Thickness(8, 0, 0, 0);
            _repositoryPackageTagFilter.SelectionChanged += RepositoryPackageFilterChanged;

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 8, 8, 6)
            };
            actions.Children.Add(EditorLabel("搜索"));
            actions.Children.Add(_repositoryPackageSearchText);
            actions.Children.Add(EditorLabel("状态"));
            actions.Children.Add(_repositoryPackageStateFilter);
            actions.Children.Add(EditorLabel("分类"));
            actions.Children.Add(_repositoryPackageTagFilter);
            return BuildToolbarHeader("仓库插件包", actions);
        }

        private UIElement BuildRepositoryPackageList()
        {
            _warehousePackageList.BorderThickness = new Thickness(0);
            _warehousePackageList.Background = RevitUiTheme.Current.PanelBackground;
            _warehousePackageList.ContextMenu = BuildRepositoryPackageMenu();
            _warehousePackageList.ItemTemplate = BuildRepositoryPackageTemplate();
            _warehousePackageList.ItemsPanel = BuildRepositoryPackageItemsPanel();
            _warehousePackageList.SelectionMode = SelectionMode.Single;
            _warehousePackageList.HorizontalContentAlignment = HorizontalAlignment.Center;
            _warehousePackageList.PreviewMouseRightButtonDown += ListBoxPreviewMouseRightButtonDown;
            _warehousePackageList.SetValue(ScrollViewer.CanContentScrollProperty, false);
            _warehousePackageList.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            _warehousePackageList.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            return _warehousePackageList;
        }

        private static ItemsPanelTemplate BuildRepositoryPackageItemsPanel()
        {
            var panelFactory = new FrameworkElementFactory(typeof(WrapPanel));
            panelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            panelFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            return new ItemsPanelTemplate(panelFactory);
        }

        private DataTemplate BuildRepositoryPackageTemplate()
        {
            var theme = RevitUiTheme.Current;
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.WidthProperty, RepositoryPackageCardWidth);
            border.SetValue(Border.MarginProperty, new Thickness(RepositoryCardHorizontalMargin, RepositoryPackageCardVerticalMargin, RepositoryCardHorizontalMargin, RepositoryPackageCardVerticalMargin));
            border.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
            border.SetValue(Border.BackgroundProperty, theme.PanelBackground);
            border.SetValue(Border.BorderBrushProperty, theme.BorderBrush);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.MinHeightProperty, 78.0);
            border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
            border.SetValue(FrameworkElement.UseLayoutRoundingProperty, true);

            var row = new FrameworkElementFactory(typeof(DockPanel));
            row.SetValue(FrameworkElement.MinHeightProperty, 58.0);
            row.SetValue(DockPanel.LastChildFillProperty, true);
            border.AppendChild(row);

            var actionRail = new FrameworkElementFactory(typeof(Border));
            actionRail.SetValue(DockPanel.DockProperty, Dock.Right);
            actionRail.SetValue(FrameworkElement.WidthProperty, RepositoryPackageActionWidth);
            actionRail.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
            row.AppendChild(actionRail);

            var actions = new FrameworkElementFactory(typeof(StackPanel));
            actions.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            actions.SetValue(FrameworkElement.WidthProperty, RepositoryPackageActionWidth);
            actions.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            actions.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            actionRail.AppendChild(actions);

            var body = new FrameworkElementFactory(typeof(StackPanel));
            body.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);
            body.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 10, 0));
            row.AppendChild(body);

            var title = new FrameworkElementFactory(typeof(TextBlock));
            title.SetBinding(TextBlock.TextProperty, new Binding(nameof(RepositoryPackageRow.DisplayName)));
            title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            title.SetValue(TextBlock.ForegroundProperty, theme.TextBrush);
            title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            body.AppendChild(title);

            var packageId = new FrameworkElementFactory(typeof(TextBlock));
            packageId.SetBinding(TextBlock.TextProperty, new Binding(nameof(RepositoryPackageRow.PackageId)));
            packageId.SetValue(TextBlock.MarginProperty, new Thickness(0, 3, 0, 0));
            packageId.SetValue(TextBlock.FontSizeProperty, 11.0);
            packageId.SetValue(TextBlock.ForegroundProperty, theme.SubtleTextBrush);
            packageId.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            body.AppendChild(packageId);

            var meta = new FrameworkElementFactory(typeof(TextBlock));
            meta.SetBinding(TextBlock.TextProperty, new Binding(".") { Converter = new RepositoryPackageMetaLabelConverter() });
            meta.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 0));
            meta.SetValue(TextBlock.FontSizeProperty, 11.0);
            meta.SetValue(TextBlock.ForegroundProperty, theme.MutedTextBrush);
            meta.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            body.AppendChild(meta);

            body.AppendChild(BuildRepositoryPackageTagsControl());

            actions.AppendChild(BuildRepositoryPackagePrimaryActionButton());
            actions.AppendChild(BuildRepositoryPackageUninstallButton());

            return new DataTemplate { VisualTree = border };
        }

        private FrameworkElementFactory BuildRepositoryPackagePrimaryActionButton()
        {
            var action = new FrameworkElementFactory(typeof(Button));
            action.SetValue(Button.StyleProperty, RepositoryPackageActionButtonStyle());
            action.SetBinding(ContentControl.ContentProperty, RepositoryPackagePrimaryActionLabelBinding());
            action.SetValue(FrameworkElement.WidthProperty, RepositoryPackageActionWidth);
            action.SetValue(Button.MinWidthProperty, RepositoryPackageActionWidth);
            action.SetValue(Button.HeightProperty, RepositoryPackageActionHeight);
            action.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 6));
            action.SetValue(Button.BorderThicknessProperty, new Thickness(1));
            action.SetBinding(Button.BackgroundProperty, RepositoryPackagePrimaryActionBinding(new RepositoryPackageActionBrushConverter()));
            action.SetBinding(Button.ForegroundProperty, RepositoryPackagePrimaryActionBinding(new RepositoryPackageActionForegroundConverter()));
            action.SetBinding(Button.BorderBrushProperty, RepositoryPackagePrimaryActionBinding(new RepositoryPackageActionBorderConverter()));
            action.AddHandler(Button.ClickEvent, new RoutedEventHandler(RunRepositoryPackagePrimaryAction));
            return action;
        }

        private static MultiBinding RepositoryPackagePrimaryActionLabelBinding()
        {
            return RepositoryPackagePrimaryActionBinding(new RepositoryPackagePrimaryActionLabelConverter());
        }

        private static MultiBinding RepositoryPackagePrimaryActionBinding(IMultiValueConverter converter)
        {
            var binding = new MultiBinding { Converter = converter };
            binding.Bindings.Add(new Binding(nameof(RepositoryPackageRow.PrimaryAction)));
            binding.Bindings.Add(new Binding("IsMouseOver") { RelativeSource = RelativeSource.Self });
            return binding;
        }

        private FrameworkElementFactory BuildRepositoryPackageUninstallButton()
        {
            var action = new FrameworkElementFactory(typeof(Button));
            action.SetValue(Button.StyleProperty, RepositoryPackageUninstallButtonStyle());
            action.SetValue(ContentControl.ContentProperty, "卸载");
            action.SetValue(FrameworkElement.WidthProperty, RepositoryPackageActionWidth);
            action.SetValue(Button.MinWidthProperty, RepositoryPackageActionWidth);
            action.SetValue(Button.HeightProperty, RepositoryPackageActionHeight);
            action.AddHandler(Button.ClickEvent, new RoutedEventHandler(RunRepositoryPackageUninstallAction));
            return action;
        }

        private static Style RepositoryPackageActionButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 2, 6, 2)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(Control.TemplateProperty, RepositoryPackageButtonTemplate()));

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.65));
            style.Triggers.Add(disabled);
            return style;
        }

        private static Style RepositoryPackageUninstallButtonStyle()
        {
            var style = RepositoryPackageActionButtonStyle();
            style.Setters.Add(new Setter(Control.BackgroundProperty, RepositoryPackageUninstallBackground()));
            style.Setters.Add(new Setter(Control.ForegroundProperty, RepositoryPackageUninstallForeground()));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, RepositoryPackageUninstallBorder()));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, RevitUiTheme.Current.DangerBrush));
            hover.Setters.Add(new Setter(Control.ForegroundProperty, RevitUiTheme.Current.AccentForegroundBrush));
            hover.Setters.Add(new Setter(Control.BorderBrushProperty, RevitUiTheme.Current.DangerBrush));
            style.Triggers.Add(hover);
            return style;
        }

        private static ControlTemplate RepositoryPackageButtonTemplate()
        {
            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.Name = "Chrome";
            chrome.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            chrome.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            chrome.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            content.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            chrome.AppendChild(content);
            return new ControlTemplate(typeof(Button)) { VisualTree = chrome };
        }

        private static FrameworkElementFactory BuildRepositoryPackageTagsControl()
        {
            var tags = new FrameworkElementFactory(typeof(ItemsControl));
            tags.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 6, 0, 0));
            tags.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(RepositoryPackageRow.TagBadges)));

            var panelFactory = new FrameworkElementFactory(typeof(WrapPanel));
            panelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            tags.SetValue(ItemsControl.ItemsPanelProperty, new ItemsPanelTemplate(panelFactory));
            tags.SetValue(ItemsControl.ItemTemplateProperty, BuildRepositoryTagChipTemplate());
            return tags;
        }

        private static DataTemplate BuildRepositoryTagChipTemplate()
        {
            var theme = RevitUiTheme.Current;
            var chip = new FrameworkElementFactory(typeof(Border));
            chip.SetValue(Border.BackgroundProperty, theme.ChipBackground);
            chip.SetValue(Border.BorderBrushProperty, theme.BorderBrush);
            chip.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            chip.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            chip.SetValue(Border.PaddingProperty, new Thickness(4, 2, 4, 2));
            chip.SetValue(Border.MarginProperty, new Thickness(0, 0, 4, 4));

            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetBinding(TextBlock.TextProperty, new Binding("."));
            label.SetValue(TextBlock.FontSizeProperty, 10.0);
            label.SetValue(TextBlock.ForegroundProperty, theme.MutedTextBrush);
            chip.AppendChild(label);

            return new DataTemplate { VisualTree = chip };
        }

        private static UIElement BuildToolbarHeader(string title, UIElement actions)
        {
            var header = new DockPanel();
            var titleBlock = SectionHeader(title);
            DockPanel.SetDock(titleBlock, Dock.Left);
            header.Children.Add(titleBlock);
            DockPanel.SetDock(actions, Dock.Right);
            header.Children.Add(actions);
            return header;
        }

        private static TabItem BuildTab(string title, UIElement content)
        {
            var theme = RevitUiTheme.Current;
            return new TabItem
            {
                Header = title,
                Padding = new Thickness(12, 6, 12, 6),
                Content = new Border
                {
                    Background = theme.PanelBackground,
                    BorderBrush = theme.BorderBrush,
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
                Content = BuildButtonContent(text),
                MinWidth = 92,
                Height = 30,
                Margin = new Thickness(8, 0, 0, 0)
            };
            button.Click += handler;
            return button;
        }

        private static Button CreateIconButton(string iconKey, string tooltip, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = new Image
                {
                    Source = DefaultRibbonIconProvider.CreateSmallIcon(iconKey),
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Uniform
                },
                Width = 28,
                MinWidth = 28,
                Height = 28,
                Padding = new Thickness(4),
                ToolTip = tooltip,
                Style = CreateBorderlessIconButtonStyle()
            };
            button.Click += handler;
            return button;
        }

        private static Style CreateBorderlessIconButtonStyle()
        {
            var theme = RevitUiTheme.Current;
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 0.0));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, theme.ControlHoverBackground));
            style.Triggers.Add(hover);

            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(Control.BackgroundProperty, theme.ControlPressedBackground));
            style.Triggers.Add(pressed);

            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
            style.Triggers.Add(disabled);
            return style;
        }

        private static object BuildButtonContent(string text)
        {
            var iconKey = IconKeyForButtonText(text);
            if (string.IsNullOrWhiteSpace(iconKey))
            {
                return text;
            }

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(new Image
            {
                Source = DefaultRibbonIconProvider.CreateSmallIcon(iconKey),
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 5, 0),
                Stretch = Stretch.Uniform
            });
            panel.Children.Add(new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = RevitUiTheme.Current.TextBrush
            });
            return panel;
        }

        private static string IconKeyForButtonText(string text)
        {
            var value = text ?? string.Empty;
            if (value.IndexOf("诊断", StringComparison.OrdinalIgnoreCase) >= 0) return "diagnostics";
            if (value.IndexOf("重新", StringComparison.OrdinalIgnoreCase) >= 0) return "refresh";
            if (value.IndexOf("同步", StringComparison.OrdinalIgnoreCase) >= 0) return "refresh";
            if (value.IndexOf("保存", StringComparison.OrdinalIgnoreCase) >= 0) return "save";
            if (value.IndexOf("关闭", StringComparison.OrdinalIgnoreCase) >= 0) return "close";
            if (value.IndexOf("取消", StringComparison.OrdinalIgnoreCase) >= 0) return "close";
            if (value.IndexOf("仓库", StringComparison.OrdinalIgnoreCase) >= 0) return "repository";
            if (value.IndexOf("安装", StringComparison.OrdinalIgnoreCase) >= 0) return "install";
            if (value.IndexOf("更新", StringComparison.OrdinalIgnoreCase) >= 0) return "update";
            if (value.IndexOf("卸载", StringComparison.OrdinalIgnoreCase) >= 0) return "uninstall";
            return string.Empty;
        }

        private static TextBlock SectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 8, 8, 6),
                Foreground = RevitUiTheme.Current.TextBrush
            };
        }

        private static DataGrid CreateGrid()
        {
            var theme = RevitUiTheme.Current;
            return new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                Background = theme.PanelBackground,
                Foreground = theme.TextBrush,
                BorderBrush = theme.BorderBrush,
                BorderThickness = new Thickness(0),
                RowHeight = 30,
                AlternatingRowBackground = theme.AlternatingRowBrush
            };
        }

        private void LoadRows()
        {
            LoadGroupRows();
            LoadFeatureRows();
            LoadRibbonLayoutRows();
            LoadRepositoryRows();
            LoadRepositoryPackageRows(new List<RepositoryPackageDescriptor>());
            LoadDiagnosticRows(FrameworkRuntimeState.Current);
            RefreshStatusWithPendingPackageOperations("已加载配置。请选择启用的仓库源并手动一键同步。布局和图标需重启 Revit 重绘。");
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
                    Name = DefaultGroupDisplayName(module, feature),
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
                        Name = DisplayName(viewGroup?.Name ?? string.Empty, group.Select(item => item.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty, group.Key),
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

            var panels = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var panel in tab.Children)
            {
                panels.Children.Add(BuildRibbonDesignerPanelPreview(panel));
            }

            body.Children.Add(panels);
            return BuildRibbonDesignerDropBorder(tab, body, new Thickness(0, 0, 0, 12), new Thickness(8), Brushes.Transparent);
        }

        private UIElement BuildRibbonDesignerPanelPreview(RibbonDesignerNodeRow panel)
        {
            var theme = RevitUiTheme.Current;
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
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = theme.MutedTextBrush
            };
            Grid.SetRow(title, 1);
            body.Children.Add(title);

            return BuildRibbonDesignerDropBorder(panel, body, new Thickness(0, 0, 10, 10), new Thickness(8), theme.PanelBackground);
        }

        private UIElement BuildRibbonDesignerPanelDropSurface(RibbonDesignerNodeRow panel)
        {
            var theme = RevitUiTheme.Current;
            var items = new WrapPanel { Orientation = Orientation.Horizontal, MinHeight = 72 };
            items.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            foreach (var child in panel.Children)
            {
                items.Children.Add(BuildRibbonDesignerItemPreview(child));
            }

            var surface = new Border
            {
                Tag = panel,
                MinHeight = 76,
                Background = panel.Children.Count == 0 ? theme.SurfaceBackground : Brushes.Transparent,
                BorderBrush = panel.Children.Count == 0 ? theme.BorderBrush : Brushes.Transparent,
                BorderThickness = new Thickness(panel.Children.Count == 0 ? 1 : 0),
                Child = items,
                AllowDrop = true
            };
            surface.PreviewMouseLeftButtonDown += RibbonDesignerNodeMouseLeftButtonDown;
            surface.PreviewMouseLeftButtonUp += RibbonDesignerNodeMouseLeftButtonUp;
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
            var theme = RevitUiTheme.Current;
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

            return BuildRibbonDesignerDropBorder(row, body, new Thickness(0, 0, 8, 8), new Thickness(4), theme.SurfaceBackground);
        }

        private UIElement BuildRibbonDesignerStackPreview(RibbonDesignerNodeRow row)
        {
            var theme = RevitUiTheme.Current;
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
                    Foreground = theme.SubtleTextBrush,
                    TextAlignment = TextAlignment.Center
                });
            }

            return BuildRibbonDesignerDropBorder(row, body, new Thickness(0, 0, 8, 8), new Thickness(4), theme.SurfaceBackground);
        }

        private UIElement BuildRibbonDesignerContainerMenuPreview(RibbonDesignerNodeRow row, Thickness margin)
        {
            var theme = RevitUiTheme.Current;
            var menuItems = new StackPanel();
            foreach (var child in row.Children)
            {
                menuItems.Children.Add(BuildRibbonDesignerSmallButtonPreview(child));
            }

            return new Border
            {
                Margin = margin,
                Padding = new Thickness(4),
                Background = theme.PanelBackground,
                BorderBrush = theme.BorderBrush,
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
            var theme = RevitUiTheme.Current;
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(2),
                Background = theme.AccentSoftBrush,
                BorderBrush = theme.BorderBrush,
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
            var iconPath = RibbonDesignerIconPath(row);
            return LoadConfiguredRibbonDesignerIcon(iconPath, large)
                ?? LoadConfiguredRibbonDesignerIcon(ResolveRibbonDesignerPackageIconPath(row, iconPath), large)
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

        private string ResolveRibbonDesignerPackageIconPath(RibbonDesignerNodeRow row, string iconPath)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.FeatureId) || string.IsNullOrWhiteSpace(iconPath)) return string.Empty;
            if (!RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.PushButton)) return string.Empty;
            if (Path.IsPathRooted(iconPath) || iconPath.IndexOf(':') >= 0) return string.Empty;

            var feature = _viewModel.Features
                .FirstOrDefault(item => string.Equals(item.FeatureId, row.FeatureId, StringComparison.OrdinalIgnoreCase));
            if (feature == null || string.IsNullOrWhiteSpace(feature.ModuleId)) return string.Empty;

            var moduleDirectory = ModuleManifestDirectory(feature.ModuleId);
            if (string.IsNullOrWhiteSpace(moduleDirectory)) return string.Empty;

            var resolvedPath = Path.GetFullPath(Path.Combine(moduleDirectory, iconPath));
            return File.Exists(resolvedPath) ? resolvedPath : string.Empty;
        }

        private string ModuleManifestDirectory(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) return string.Empty;
            foreach (var document in _moduleDocuments)
            {
                if ((document.Modules.Modules ?? new List<ModuleConfiguration>())
                    .Any(module => string.Equals(module.Id, moduleId, StringComparison.OrdinalIgnoreCase)))
                {
                    return Path.GetDirectoryName(document.Path) ?? string.Empty;
                }
            }

            return string.Empty;
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
                Foreground = RevitUiTheme.Current.TextBrush,
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
                Foreground = RevitUiTheme.Current.MutedTextBrush,
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
            var border = BuildRibbonDesignerSelectionChrome(row, child, margin, padding, background);
            border.AllowDrop = true;
            border.PreviewMouseLeftButtonDown += RibbonDesignerNodeMouseLeftButtonDown;
            border.PreviewMouseLeftButtonUp += RibbonDesignerNodeMouseLeftButtonUp;
            border.PreviewMouseMove += RibbonDesignerNodeMouseMove;
            border.DragOver += RibbonDesignerNodeDragOver;
            border.Drop += RibbonDesignerNodeDrop;
            return border;
        }

        private Border BuildRibbonDesignerSelectionChrome(RibbonDesignerNodeRow row, UIElement child, Thickness margin, Thickness padding, Brush background)
        {
            var theme = RevitUiTheme.Current;
            var selected = ReferenceEquals(row, _viewModel.SelectedRibbonDesignerNode);
            return new Border
            {
                Tag = row,
                MinWidth = RibbonDesignerPanelPreviewMinWidth(row),
                Margin = margin,
                Padding = padding,
                Background = selected ? theme.SelectionBrush : background,
                BorderBrush = selected ? theme.AccentBrush : theme.BorderBrush,
                BorderThickness = new Thickness(selected ? 2 : 1),
                Child = child
            };
        }

        private static double RibbonDesignerPanelPreviewMinWidth(RibbonDesignerNodeRow row)
        {
            if (!RibbonDesignerMapper.IsType(row, RibbonDesignerNodeRow.Panel)) return 0;
            return IsSinglePushButtonRibbonDesignerPanel(row) ? 112 : 180;
        }

        private static bool IsSinglePushButtonRibbonDesignerPanel(RibbonDesignerNodeRow row)
        {
            return row != null
                && row.Children.Count == 1
                && RibbonDesignerMapper.IsType(row.Children[0], RibbonDesignerNodeRow.PushButton);
        }

        private Button BuildRibbonDesignerPreviewButton(RibbonDesignerNodeRow row, object content, double width, double height)
        {
            var theme = RevitUiTheme.Current;
            var selected = ReferenceEquals(row, _viewModel.SelectedRibbonDesignerNode);
            var button = new Button
            {
                Tag = row,
                Content = content,
                Width = width,
                Height = height,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(6, 2, 6, 2),
                Background = selected ? theme.SelectionBrush : theme.PanelBackground,
                BorderBrush = selected ? theme.AccentBrush : theme.BorderBrush,
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
            button.PreviewMouseLeftButtonUp += RibbonDesignerNodeMouseLeftButtonUp;
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

        private void RibbonDesignerNodeMouseLeftButtonUp(object sender, MouseButtonEventArgs args)
        {
            if (!IsRibbonDesignerDirectEventNode(sender, args.OriginalSource)) return;
            if (!(ResolveRibbonDesignerEventNode(args.OriginalSource, sender) is RibbonDesignerNodeRow row)) return;
            if (!ReferenceEquals(row, _viewModel.SelectedRibbonDesignerNode)) return;

            RefreshRibbonDesignerCanvas();
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
            _viewModel.Repositories.Clear();
            foreach (var row in (_configuration.Modules.Repositories ?? new List<PackageRepositoryConfiguration>())
                .Select(CreateRepositoryRow))
            {
                _viewModel.Repositories.Add(row);
            }

            _repositorySourcesList.ItemsSource = _viewModel.Repositories;
            if (_repositorySourcesList.SelectedItem == null && _viewModel.Repositories.Count > 0)
            {
                _repositorySourcesList.SelectedItem = _viewModel.Repositories[0];
            }
            RefreshRepositorySourceCards();
            RefreshRepositoryFilterOptions();
        }

        private void LoadRepositoryPackageRows(IEnumerable<RepositoryPackageDescriptor> packages)
        {
            _repositoryPackageRows.Clear();
            _repositoryPackageRows.AddRange((packages ?? new List<RepositoryPackageDescriptor>())
                .Select(package =>
                {
                    var row = RepositoryPackageRow.FromDescriptor(package, IsLoadedInCurrentRuntime(package.PackageId, package.ModuleId));
                    _repositorySettingsController.PreparePackageRow(row, _viewModel.Repositories);
                    return row;
                }));
            RefreshRepositoryFilterOptions();
            ApplyRepositoryPackageFilter();
        }

        private void RepositoryPackageFilterChanged(object sender, RoutedEventArgs args)
        {
            ApplyRepositoryPackageFilter();
        }

        private void ApplyRepositoryPackageFilter()
        {
            var source = SelectedRepositorySourceRow();
            if (source == null || !source.Enabled || string.IsNullOrWhiteSpace(source.Id))
            {
                _viewModel.RepositoryPackages.Clear();
                _warehousePackageList.ItemsSource = _viewModel.RepositoryPackages;
                RefreshItems(_warehousePackageList);
                return;
            }

            var filter = new RepositoryPackageFilterState
            {
                SearchText = (_repositoryPackageSearchText.Text ?? string.Empty).Trim(),
                Status = _repositoryPackageStateFilter.SelectedItem as string ?? "全部",
                RepositoryId = source.Id,
                TagOrCategory = _repositoryPackageTagFilter.SelectedItem as string ?? "全部"
            };

            var filtered = _repositorySettingsController.ApplyPackageFilters(_repositoryPackageRows, filter);
            _viewModel.RepositoryPackages.Clear();
            foreach (var row in filtered)
            {
                _viewModel.RepositoryPackages.Add(row);
            }

            _warehousePackageList.ItemsSource = _viewModel.RepositoryPackages;
            RefreshItems(_warehousePackageList);
        }

        private static bool ContainsText(string value, string search)
        {
            return (value ?? string.Empty).IndexOf(search ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private RepositoryRow CreateRepositoryRow(PackageRepositoryConfiguration repository)
        {
            var row = new RepositoryRow
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
            };
            row.DisplayName = _repositorySettingsController.RepositoryDisplayName(row);
            return row;
        }

        private void RefreshRepositorySourceCards()
        {
            foreach (var row in _viewModel.Repositories)
            {
                row.DisplayName = _repositorySettingsController.RepositoryDisplayName(row);
            }

            _repositorySourcesList.ItemsSource = _viewModel.Repositories;
            RefreshItems(_repositorySourcesList);
        }

        private void RefreshRepositoryFilterOptions()
        {
            ReplaceComboItems(
                _repositoryPackageTagFilter,
                new[] { "全部" }
                    .Concat(_repositoryPackageRows.SelectMany(row => new[] { row.CategoryText, row.TagsText })
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .SelectMany(value => value.Split(new[] { ',', '，', ';', '；', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)));
        }

        private static void ReplaceComboItems(ComboBox combo, IEnumerable<string> values)
        {
            var selected = combo.SelectedItem as string ?? "全部";
            var items = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            combo.ItemsSource = items;
            combo.SelectedItem = items.Any(item => string.Equals(item, selected, StringComparison.OrdinalIgnoreCase))
                ? selected
                : "全部";
            if (combo.SelectedItem == null && combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        private static void RefreshItems(ItemsControl control)
        {
            control.Items.Refresh();
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
                RefreshStatus("已从本地仓库缓存加载 " + packages.Count + " 个插件。需要远端状态时请手动一键同步。");
            }

            if (messages.Count > 0)
            {
                LoadDiagnosticRowsFromMessages(messages);
            }

            RefreshRepositorySourceCards();
        }

        private void LoadCachedRepositoryPackages(RepositoryRow row)
        {
            if (row == null) return;

            if (!row.Enabled)
            {
                LoadRepositoryPackageRows(new List<RepositoryPackageDescriptor>());
                row.Status = "停用";
                RefreshRepositorySourceCards();
                RefreshStatus(row.DisplayName + " 已停用，未加载本地缓存。");
                return;
            }

            var repository = row.ToConfiguration();
            if (!_packageRepositoryService.HasRepositoryCache(BaseDirectory(), repository))
            {
                LoadRepositoryPackageRows(new List<RepositoryPackageDescriptor>());
                row.Status = "本地缓存无插件";
                RefreshRepositorySourceCards();
                RefreshStatus(row.DisplayName + " 暂无本地缓存。使用一键同步后可在此浏览。");
                return;
            }

            var packages = _packageRepositoryService.BrowseCached(BaseDirectory(), row.ToConfiguration(), out var diagnostics);
            LoadRepositoryPackageRows(packages);
            if (diagnostics.Count > 0)
            {
                LoadDiagnosticRowsFromMessages(diagnostics);
            }

            row.Status = packages.Count > 0 ? "已从本地缓存加载 " + packages.Count + " 个插件" : "本地缓存无插件";
            RefreshRepositorySourceCards();
            RefreshStatus(row.DisplayName + " 已从本地缓存加载 " + packages.Count + " 个插件。需要远端最新状态时请手动一键同步。");
        }

        private void CheckRepositoryUpdates()
        {
            EndGridEdits();
            ApplyRepositoryRows();

            var repositories = _viewModel.Repositories
                .Where(row => row.Enabled)
                .Select(row => row.ToConfiguration())
                .ToList();
            if (repositories.Count == 0)
            {
                RefreshStatus("没有启用的仓库可检查。");
                return;
            }

            var baseDirectory = BaseDirectory();
            RefreshStatus("正在检查 " + repositories.Count + " 个仓库，请稍候...");
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

                    RefreshRepositorySourceCards();
                    RefreshStatus("仓库检查完成，找到 " + packages.Count + " 个插件。");
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
            view.Ribbon.Panels = _ribbonDesignerMapper.ToPanels(_viewModel.RibbonDesignerTabs, _viewModel.Features);
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
            menu.Items.Add(MenuItem("编辑仓库源", (sender, args) => EditSelectedRepository()));
            menu.Items.Add(MenuItem("同步仓库源", (sender, args) => BrowseSelectedRepository()));
            menu.Items.Add(new Separator());
            var delete = MenuItem("删除仓库", (sender, args) => RemoveSelectedRepository());
            delete.Foreground = RevitUiTheme.Current.DangerBrush;
            menu.Items.Add(delete);
            return menu;
        }

        private ContextMenu BuildRepositoryPackageMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("安装插件包", (sender, args) => InstallSelectedRepositoryPackage()));
            menu.Items.Add(MenuItem("更新插件包", (sender, args) => UpdateSelectedRepositoryPackage()));
            menu.Items.Add(MenuItem("卸载插件包", (sender, args) => UninstallSelectedRepositoryPackage()));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("复制插件 ID", (sender, args) => CopySelectedRepositoryPackageId()));
            menu.Items.Add(MenuItem("打开来源目录", (sender, args) => OpenSelectedRepositoryPackageSource()));
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
            if (SelectedRepositorySourceRow() is RepositoryRow row)
            {
                row.Enabled = enabled;
                row.Status = enabled ? "可浏览" : "停用";
                RefreshRepositorySourceCards();
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
            row.DisplayName = _repositorySettingsController.RepositoryDisplayName(row);
            _viewModel.Repositories.Add(row);
            _repositorySourcesList.SelectedItem = row;
            RefreshRepositorySourceCards();
            RefreshRepositoryFilterOptions();
            EditRepository(row);
        }

        private void ToggleRepositorySourceFromCard(object sender, RoutedEventArgs args)
        {
            if (!(RowFromSender<RepositoryRow>(sender) is RepositoryRow row)) return;
            _repositorySourcesList.SelectedItem = row;
            row.Enabled = sender is CheckBox box ? box.IsChecked == true : !row.Enabled;
            row.Status = row.Enabled ? "可浏览" : "停用";
            RefreshRepositorySourceCards();
            ApplyRepositoryPackageFilter();
            RefreshStatus(row.DisplayName + (row.Enabled ? " 已启用。" : " 已停用。"));
        }

        private void RepositorySourceSelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ApplyRepositoryPackageFilter();
        }

        private void BrowseRepositorySourceCacheFromCard(object sender, MouseButtonEventArgs args)
        {
            if (!(RowFromSender<RepositoryRow>(sender) is RepositoryRow row)) return;
            _repositorySourcesList.SelectedItem = row;
            LoadCachedRepositoryPackages(row);
            args.Handled = true;
        }

        private void OpenRepositorySourceMenuFromCard(object sender, RoutedEventArgs args)
        {
            if (!(RowFromSender<RepositoryRow>(sender) is RepositoryRow row)) return;
            _repositorySourcesList.SelectedItem = row;
            var menu = BuildRepositoryMenu();
            menu.PlacementTarget = sender as UIElement;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
            if (args is MouseButtonEventArgs mouseArgs)
            {
                mouseArgs.Handled = true;
            }
        }

        private void BrowseRepositorySourceFromCard(object sender, RoutedEventArgs args)
        {
            if (!(RowFromSender<RepositoryRow>(sender) is RepositoryRow row)) return;
            _repositorySourcesList.SelectedItem = row;
            BrowseSelectedRepository();
        }

        private void EditRepositorySourceFromCard(object sender, RoutedEventArgs args)
        {
            if (!(RowFromSender<RepositoryRow>(sender) is RepositoryRow row)) return;
            _repositorySourcesList.SelectedItem = row;
            EditRepository(row);
        }

        private void EditSelectedRepository()
        {
            if (!(SelectedRepositorySourceRow() is RepositoryRow row)) return;
            EditRepository(row);
        }

        private void EditRepository(RepositoryRow row)
        {
            var dialog = new Window
            {
                Owner = this,
                Title = "编辑仓库源",
                Width = 520,
                Height = 430,
                MinWidth = 480,
                MinHeight = 390,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            RevitUiTheme.Apply(dialog);

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var form = new Grid();
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < 7; i++)
            {
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var enabled = new CheckBox { IsChecked = row.Enabled, VerticalAlignment = VerticalAlignment.Center };
            var provider = new ComboBox { ItemsSource = new[] { "gitee", "github" }, SelectedItem = string.IsNullOrWhiteSpace(row.Provider) ? DefaultRepositoryProvider : row.Provider, Height = 26 };
            var visibility = new ComboBox { ItemsSource = new[] { "public", "private" }, SelectedItem = string.IsNullOrWhiteSpace(row.Visibility) ? "public" : row.Visibility, Height = 26 };
            var repository = new TextBox { Text = row.Repository, Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            var gitRef = new TextBox { Text = row.Ref, Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            var manifestPath = new TextBox { Text = row.ManifestPath, Height = 26, VerticalContentAlignment = VerticalAlignment.Center };
            var apiKey = new TextBox { Text = string.Empty, Height = 26, VerticalContentAlignment = VerticalAlignment.Center };

            AddRepositoryEditorRow(form, 0, "启用", enabled);
            AddRepositoryEditorRow(form, 1, "类型", provider);
            AddRepositoryEditorRow(form, 2, "可见性", visibility);
            AddRepositoryEditorRow(form, 3, "仓库", repository);
            AddRepositoryEditorRow(form, 4, "分支", gitRef);
            AddRepositoryEditorRow(form, 5, "清单", manifestPath);
            AddRepositoryEditorRow(form, 6, "Token", apiKey);
            Grid.SetRow(form, 0);
            root.Children.Add(form);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            var cancel = CreateButton("取消", (sender, args) => dialog.DialogResult = false);
            var save = CreateButton("保存", (sender, args) =>
            {
                row.Enabled = enabled.IsChecked == true;
                row.Provider = Convert.ToString(provider.SelectedItem) ?? DefaultRepositoryProvider;
                row.Visibility = Convert.ToString(visibility.SelectedItem) ?? "public";
                row.Repository = repository.Text ?? string.Empty;
                row.Ref = string.IsNullOrWhiteSpace(gitRef.Text) ? "main" : gitRef.Text.Trim();
                row.ManifestPath = string.IsNullOrWhiteSpace(manifestPath.Text) ? DefaultPackageManifestName : manifestPath.Text.Trim();
                if (!string.IsNullOrWhiteSpace(apiKey.Text))
                {
                    row.ApiKey = apiKey.Text;
                }

                row.Status = row.Enabled ? "可浏览" : "停用";
                row.DisplayName = _repositorySettingsController.RepositoryDisplayName(row);
                dialog.DialogResult = true;
            });
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            Grid.SetRow(buttons, 1);
            root.Children.Add(buttons);

            dialog.Content = root;
            if (dialog.ShowDialog() == true)
            {
                RefreshRepositorySourceCards();
                RefreshRepositoryFilterOptions();
                RefreshStatus("已更新仓库源设置，保存配置后持久化。");
            }
        }

        private static void AddRepositoryEditorRow(Grid form, int rowIndex, string label, UIElement editor)
        {
            var text = EditorLabel(label);
            text.Margin = new Thickness(0, 0, 10, 10);
            Grid.SetRow(text, rowIndex);
            Grid.SetColumn(text, 0);
            form.Children.Add(text);

            if (editor is FrameworkElement element)
            {
                element.Margin = new Thickness(0, 0, 0, 10);
            }

            Grid.SetRow(editor, rowIndex);
            Grid.SetColumn(editor, 1);
            form.Children.Add(editor);
        }

        private void BrowseSelectedRepository()
        {
            try
            {
                EndGridEdits();
                ApplyRepositoryRows();

                if (!(SelectedRepositorySourceRow() is RepositoryRow row)) return;
                if (!row.Enabled)
                {
                    RefreshStatus("仓库源已停用，启用后才能同步。");
                    ApplyRepositoryPackageFilter();
                    return;
                }

                var repository = row.ToConfiguration();
                var packages = _packageRepositoryService.Browse(BaseDirectory(), repository, out var diagnostics);

                row.Status = diagnostics.Any()
                    ? diagnostics.Last().Message
                    : "已浏览 " + packages.Count + " 个插件包";
                RefreshRepositorySourceCards();

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
            if (!(SelectedRepositoryPackageRow() is RepositoryPackageRow row)) return;
            RunRepositoryPackageOperation(row, package => _packageRepositoryService.Uninstall(BaseDirectory(), package));
        }

        private void RunRepositoryPackageOperation(Func<RepositoryPackageDescriptor, PackageRepositoryOperationResult> operation)
        {
            if (!(SelectedRepositoryPackageRow() is RepositoryPackageRow row)) return;
            RunRepositoryPackageOperation(row, operation);
        }

        private void RunRepositoryPackageOperation(RepositoryPackageRow row, Func<RepositoryPackageDescriptor, PackageRepositoryOperationResult> operation)
        {
            try
            {
                EndGridEdits();

                var result = operation(row.ToDescriptor());
                RefreshRepositoryPackageInstallState(row.PackageId, row.InstallDirectory);
                ApplyRepositoryPackageFilter();
                RefreshItems(_warehousePackageList);

                _moduleDocuments = _configurationStore.LoadModuleDocuments(_configuration);
                LoadGroupRows();
                LoadFeatureRows();
                LoadRibbonLayoutRows();
                RefreshStatusWithPendingPackageOperations(result.Message);
            }
            catch (Exception ex)
            {
                ReportSettingsError("插件包操作失败", ex);
            }
        }

        private void RunRepositoryPackagePrimaryAction(object sender, RoutedEventArgs args)
        {
            if (!(RowFromSender<RepositoryPackageRow>(sender) is RepositoryPackageRow row)) return;
            _warehousePackageList.SelectedItem = row;

            if (string.Equals(row.PrimaryAction, RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                RunRepositoryPackageOperation(row, package => _packageRepositoryService.Install(BaseDirectory(), package));
                return;
            }

            if (string.Equals(row.PrimaryAction, RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                RunRepositoryPackageOperation(row, package => _packageRepositoryService.Update(BaseDirectory(), package));
                return;
            }

            if (string.Equals(row.PrimaryAction, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                RunRepositoryPackageOperation(row, package => _packageRepositoryService.Update(BaseDirectory(), package));
                return;
            }

            if (string.Equals(row.PrimaryAction, RepositoryPackageAction.Uninstall.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                RefreshStatus(row.DisplayName + " 已安装，可使用下方卸载按钮。");
                return;
            }

            RefreshStatusWithPendingPackageOperations("该插件包已有待重启操作。");
        }

        private void RunRepositoryPackageUninstallAction(object sender, RoutedEventArgs args)
        {
            if (!(RowFromSender<RepositoryPackageRow>(sender) is RepositoryPackageRow row)) return;
            _warehousePackageList.SelectedItem = row;

            if (!string.Equals(row.PrimaryAction, RepositoryPackageAction.Uninstall.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(row.PrimaryAction, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                RefreshStatus("未安装插件无需卸载。");
                return;
            }

            RunRepositoryPackageOperation(row, package => _packageRepositoryService.Uninstall(BaseDirectory(), package));
        }

        private void RepositoryPackageUninstallHoverEnter(object sender, MouseEventArgs args)
        {
            if (!(sender is Button button)) return;
            button.Background = RepositoryPackageUninstallHoverBackground();
            button.Foreground = RepositoryPackageUninstallHoverForeground();
            button.BorderBrush = RepositoryPackageUninstallHoverBorder();
        }

        private void RepositoryPackageUninstallHoverLeave(object sender, MouseEventArgs args)
        {
            if (!(sender is Button button)) return;
            button.Background = RepositoryPackageUninstallBackground();
            button.Foreground = RepositoryPackageUninstallForeground();
            button.BorderBrush = RepositoryPackageUninstallBorder();
        }

        private RepositoryRow? SelectedRepositorySourceRow()
        {
            return _repositorySourcesList.SelectedItem as RepositoryRow;
        }

        private RepositoryPackageRow? SelectedRepositoryPackageRow()
        {
            return _warehousePackageList.SelectedItem as RepositoryPackageRow;
        }

        private static T? RowFromSender<T>(object sender) where T : class
        {
            return (sender as FrameworkElement)?.DataContext as T;
        }

        private void CopySelectedRepositoryPackageId()
        {
            if (!(SelectedRepositoryPackageRow() is RepositoryPackageRow row)) return;
            Clipboard.SetText(row.PackageId ?? string.Empty);
            RefreshStatus("已复制插件 ID: " + row.PackageId);
        }

        private void OpenSelectedRepositoryPackageSource()
        {
            if (!(SelectedRepositoryPackageRow() is RepositoryPackageRow row)) return;
            if (string.IsNullOrWhiteSpace(row.SourceDirectory) || !Directory.Exists(row.SourceDirectory))
            {
                RefreshStatus("来源目录不存在。");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = row.SourceDirectory,
                UseShellExecute = true
            });
        }

        private void RefreshRepositoryPackageInstallState(string packageId, string installDirectory)
        {
            var rows = _repositoryPackageRows.Count > 0 ? _repositoryPackageRows : _viewModel.RepositoryPackages.AsEnumerable();
            foreach (var row in rows.Where(item =>
                string.Equals(item.PackageId, packageId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.InstallDirectory, installDirectory, StringComparison.OrdinalIgnoreCase)))
            {
                var refreshed = _packageRepositoryService.RefreshInstallState(BaseDirectory(), row.ToDescriptor());
                row.IsInstalled = refreshed.IsInstalled;
                row.InstalledVersion = refreshed.InstalledVersion;
                row.PendingOperation = refreshed.PendingOperation;
                row.InstallState = RepositoryPackageRow.InstallStateFor(row.IsInstalled, row.Version, row.InstalledVersion, row.PendingOperation, IsLoadedInCurrentRuntime(row.PackageId, row.ModuleId));
                _repositorySettingsController.PreparePackageRow(row, _viewModel.Repositories);
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
                        Name = DefaultGroupDisplayName(module, feature)
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
            return DefaultRibbonIconProvider.FeatureIconKeys
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
            if (!string.IsNullOrWhiteSpace(module.Category)) return module.Category.Trim();
            return module.Id ?? string.Empty;
        }

        private static string DefaultGroupDisplayName(ModuleConfiguration module, FeatureConfiguration feature)
        {
            if (!string.IsNullOrWhiteSpace(feature.Group)) return feature.Group.Trim();
            return DisplayName(module.DisplayName, module.Name, module.Id);
        }

        private void RemoveSelectedRepository()
        {
            if (SelectedRepositorySourceRow() is RepositoryRow row)
            {
                _viewModel.Repositories.Remove(row);
                RefreshRepositorySourceCards();
                RefreshRepositoryFilterOptions();
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

        private static void ListBoxPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ListBox list)) return;
            var row = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (row == null) return;

            row.IsSelected = true;
            list.SelectedItem = row.DataContext;
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
        }

        private static void CommitGrid(DataGrid grid)
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void CheckFrameworkUpdate()
        {
            RefreshStatus("正在检查框架更新，请稍候。");
            if (_checkFrameworkIconButton != null)
            {
                _checkFrameworkIconButton.IsEnabled = false;
            }

            Task.Run(() =>
            {
                try
                {
                    return _frameworkUpdateService.Check(AssemblyVersionText());
                }
                catch (Exception ex)
                {
                    return new FrameworkUpdateCheckResult { Success = false, Message = "检查更新失败：" + ex.Message };
                }
            }).ContinueWith(task => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_checkFrameworkIconButton != null)
                {
                    _checkFrameworkIconButton.IsEnabled = true;
                }

                RefreshStatus(task.Result.Message);

                if (task.Result.Success && task.Result.HasUpdate)
                {
                    if (!ShowFrameworkUpdateDialog(task.Result))
                    {
                        RefreshStatus("已取消框架更新。");
                        return;
                    }

                    UpdateFramework(task.Result);
                }
            })));
        }

        private void UpdateFramework(FrameworkUpdateCheckResult checkResult)
        {
            if (_checkFrameworkIconButton != null)
            {
                _checkFrameworkIconButton.IsEnabled = false;
            }

            RefreshStatus("正在下载框架更新，请稍候。");
            var baseDirectory = BaseDirectory();
            Task.Run(() =>
            {
                try
                {
                    var download = _frameworkUpdateService.Download(checkResult);
                    if (!download.Success)
                    {
                        return new FrameworkUpdateOperationResult { Success = false, Message = download.Message };
                    }

                    return ManagerMaintenanceLauncher.StartUpdate(baseDirectory, download.PackagePath, download.LatestVersion, MaintenanceWaitProcessIds());
                }
                catch (Exception ex)
                {
                    return new FrameworkUpdateOperationResult { Success = false, Message = "更新框架失败：" + ex.Message };
                }
            }).ContinueWith(task => Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshStatus(task.Result.Message);
                if (_checkFrameworkIconButton != null)
                {
                    _checkFrameworkIconButton.IsEnabled = !task.Result.Success;
                }

                if (task.Result.Success)
                {
                    Close();
                }
            })));
        }

        private void LaunchUninstaller()
        {
            var baseDirectory = BaseDirectory();
            if (_uninstallIconButton != null)
            {
                _uninstallIconButton.IsEnabled = false;
            }

            var launch = ManagerMaintenanceLauncher.StartUninstall(baseDirectory, MaintenanceWaitProcessIds());
            RefreshStatus(launch.Message);
            if (launch.Success)
            {
                Close();
                return;
            }

            if (_uninstallIconButton != null)
            {
                _uninstallIconButton.IsEnabled = true;
            }
        }

        private IEnumerable<int> MaintenanceWaitProcessIds()
        {
            yield return Process.GetCurrentProcess().Id;
            if (_hostProcessId > 0 && _hostProcessId != Process.GetCurrentProcess().Id)
            {
                yield return _hostProcessId;
            }
        }

        private bool ShowFrameworkUpdateDialog(FrameworkUpdateCheckResult update)
        {
            var theme = RevitUiTheme.Current;
            var dialog = new Window
            {
                Title = "升级框架",
                Owner = this,
                Width = 520,
                Height = 360,
                MinWidth = 460,
                MinHeight = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize
            };
            RevitUiTheme.Apply(dialog);

            var root = new Grid { Margin = new Thickness(14), Background = theme.WindowBackground };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = "更新版本 " + update.LatestVersion,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextBrush,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var notes = new TextBox
            {
                Text = ReleaseNotesText(update.ReleaseNotes),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderBrush = theme.BorderBrush,
                Background = theme.SurfaceBackground,
                Foreground = theme.MutedTextBrush,
                Padding = new Thickness(8)
            };
            Grid.SetRow(notes, 1);
            root.Children.Add(notes);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var close = CreateButton("关闭", (sender, args) => dialog.DialogResult = false);
            var confirm = CreateButton("确认", (sender, args) => dialog.DialogResult = true);
            buttons.Children.Add(close);
            buttons.Children.Add(confirm);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            dialog.Content = root;
            return dialog.ShowDialog() == true;
        }

        private static string ReleaseNotesText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "暂无更新信息。" : value.Trim();
        }

        private void RefreshStatus(string text)
        {
            _statusText.Text = text;
        }

        private string PendingPackageOperationsStatusText()
        {
            var pendingCount = _packageRepositoryService.ListPendingOperations(BaseDirectory()).Count;
            return pendingCount == 0
                ? string.Empty
                : "待重启操作 " + pendingCount + " 项，重启 Revit 后生效。";
        }

        private void RefreshStatusWithPendingPackageOperations(string message)
        {
            var pendingStatus = PendingPackageOperationsStatusText();
            if (string.IsNullOrWhiteSpace(pendingStatus))
            {
                RefreshStatus(message);
                return;
            }

            RefreshStatus(string.IsNullOrWhiteSpace(message)
                ? pendingStatus
                : message + " " + pendingStatus);
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

        private void OpenLogsDirectory()
        {
            try
            {
                var logsDirectory = PlugHubLogger.LogsDirectory(BaseDirectory());
                Process.Start(new ProcessStartInfo
                {
                    FileName = logsDirectory,
                    UseShellExecute = true
                });
                RefreshStatus("已打开日志目录: " + logsDirectory);
            }
            catch (Exception ex)
            {
                ReportSettingsError("打开日志目录失败", ex);
            }
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

        private int ConfiguredModuleCount()
        {
            return EditableModules().Count();
        }

        private int ConfiguredFeatureCount()
        {
            return EditableModules().Sum(module => (module.Features ?? new List<FeatureConfiguration>()).Count);
        }

        private int ConfiguredRepositoryCount()
        {
            return (_configuration.Modules.Repositories ?? new List<PackageRepositoryConfiguration>()).Count;
        }

        private static string AssemblyVersionText()
        {
            var assembly = typeof(FrameworkSettingsWindow).Assembly;
            var informationalVersion = ((System.Reflection.AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(
                assembly,
                typeof(System.Reflection.AssemblyInformationalVersionAttribute)))?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                var text = informationalVersion!.Split('+')[0].Trim();
                if (!string.Equals(text, "dev", StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            var version = assembly.GetName().Version;
            return version == null || version.Major == 0 ? "开发构建" : version.ToString();
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

        private static string RepositoryPackagePrimaryActionLabel(string action, bool isMouseOver)
        {
            if (string.Equals(action, RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase)) return "安装";
            if (string.Equals(action, RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase)) return "有更新";
            if (string.Equals(action, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase) && isMouseOver) return "重安装";
            if (string.Equals(action, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return "已安装";
            if (string.Equals(action, RepositoryPackageAction.Uninstall.ToString(), StringComparison.OrdinalIgnoreCase)) return "已安装";
            return "需重启";
        }

        private static Brush RepositoryPackageActionBackground(string action, bool isMouseOver)
        {
            var theme = RevitUiTheme.Current;
            if (string.Equals(action, RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.SuccessBrush;
            if (string.Equals(action, RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.UpdateBrush;
            if (string.Equals(action, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase) && isMouseOver) return theme.SuccessBrush;
            if (string.Equals(action, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.ControlBackground;
            if (string.Equals(action, RepositoryPackageAction.Uninstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.ControlBackground;
            return theme.SurfaceBackground;
        }

        private static Brush RepositoryPackageActionForeground(string action, bool isMouseOver)
        {
            var theme = RevitUiTheme.Current;
            if (string.Equals(action, RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.AccentForegroundBrush;
            if (string.Equals(action, RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.AccentForegroundBrush;
            if (string.Equals(action, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase) && isMouseOver) return theme.AccentForegroundBrush;
            if (string.Equals(action, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.TextBrush;
            if (string.Equals(action, RepositoryPackageAction.Uninstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.TextBrush;
            return theme.MutedTextBrush;
        }

        private static Brush RepositoryPackageActionBorder(string action, bool isMouseOver)
        {
            var theme = RevitUiTheme.Current;
            if (string.Equals(action, RepositoryPackageAction.Install.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.SuccessBrush;
            if (string.Equals(action, RepositoryPackageAction.Update.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.UpdateBrush;
            if (string.Equals(action, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase) && isMouseOver) return theme.SuccessBrush;
            if (string.Equals(action, RepositoryPackageAction.Reinstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.BorderBrush;
            if (string.Equals(action, RepositoryPackageAction.Uninstall.ToString(), StringComparison.OrdinalIgnoreCase)) return theme.BorderBrush;
            return theme.BorderBrush;
        }

        private static Brush RepositoryPackageUninstallBackground()
        {
            return RevitUiTheme.Current.SurfaceBackground;
        }

        private static Brush RepositoryPackageUninstallForeground()
        {
            return RevitUiTheme.Current.MutedTextBrush;
        }

        private static Brush RepositoryPackageUninstallBorder()
        {
            return RevitUiTheme.Current.BorderBrush;
        }

        private static Brush RepositoryPackageUninstallHoverBackground()
        {
            return RevitUiTheme.Current.DangerBrush;
        }

        private static Brush RepositoryPackageUninstallHoverForeground()
        {
            return RevitUiTheme.Current.AccentForegroundBrush;
        }

        private static Brush RepositoryPackageUninstallHoverBorder()
        {
            return RevitUiTheme.Current.DangerBrush;
        }

        private sealed class RepositoryMetaLabelConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (!(value is RepositoryRow row)) return string.Empty;
                var provider = string.IsNullOrWhiteSpace(row.Provider) ? DefaultRepositoryProvider : row.Provider;
                var visibility = string.Equals(row.Visibility, "private", StringComparison.OrdinalIgnoreCase) ? "私有" : "公开";
                return provider + " · " + visibility;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        }

        private sealed class RepositoryPackageMetaLabelConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (!(value is RepositoryPackageRow row)) return string.Empty;
                var localVersion = string.IsNullOrWhiteSpace(row.InstalledVersion) ? "-" : row.InstalledVersion.Trim();
                var repositoryVersion = string.IsNullOrWhiteSpace(row.Version) ? "?" : row.Version.Trim();
                return "本 " + localVersion + " · 仓 " + repositoryVersion;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Binding.DoNothing;
            }
        }

        private sealed class RepositoryPackagePrimaryActionLabelConverter : IMultiValueConverter
        {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                return RepositoryPackagePrimaryActionLabel(ActionValue(values), IsMouseOverValue(values));
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                return NoMultiBindingWriteback(targetTypes);
            }
        }

        private sealed class RepositoryPackageActionBrushConverter : IMultiValueConverter
        {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                return RepositoryPackageActionBackground(ActionValue(values), IsMouseOverValue(values));
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                return NoMultiBindingWriteback(targetTypes);
            }
        }

        private sealed class RepositoryPackageActionForegroundConverter : IMultiValueConverter
        {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                return RepositoryPackageActionForeground(ActionValue(values), IsMouseOverValue(values));
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                return NoMultiBindingWriteback(targetTypes);
            }
        }

        private sealed class RepositoryPackageActionBorderConverter : IMultiValueConverter
        {
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                return RepositoryPackageActionBorder(ActionValue(values), IsMouseOverValue(values));
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                return NoMultiBindingWriteback(targetTypes);
            }
        }

        private static string ActionValue(object[] values)
        {
            return values != null && values.Length > 0
                ? System.Convert.ToString(values[0]) ?? string.Empty
                : string.Empty;
        }

        private static bool IsMouseOverValue(object[] values)
        {
            return values != null && values.Length > 1 && values[1] is bool isMouseOver && isMouseOver;
        }

        private static object[] NoMultiBindingWriteback(Type[] targetTypes)
        {
            return Enumerable.Repeat(Binding.DoNothing, targetTypes == null ? 0 : targetTypes.Length).ToArray();
        }

    }
}
