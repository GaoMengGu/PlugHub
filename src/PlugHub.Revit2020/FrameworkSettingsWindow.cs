using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    internal sealed class FrameworkSettingsWindow : Window
    {
        private readonly string _configDirectory;
        private FrameworkConfiguration _configuration;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };
        private readonly DataGrid _modulesGrid = CreateGrid();
        private readonly DataGrid _featuresGrid = CreateGrid();
        private readonly DataGrid _sourcesGrid = CreateGrid();
        private readonly DataGrid _diagnosticsGrid = CreateGrid();
        private readonly TextBlock _statusText = new TextBlock();
        private List<ModuleManifestDocument> _moduleDocuments = new List<ModuleManifestDocument>();
        private ObservableCollection<ModuleRow> _moduleRows = new ObservableCollection<ModuleRow>();
        private ObservableCollection<FeatureRow> _featureRows = new ObservableCollection<FeatureRow>();
        private ObservableCollection<SourceRow> _sourceRows = new ObservableCollection<SourceRow>();
        private int _dragSourceRowIndex = -1;
        private DataGrid? _dragSourceGrid;

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
            tabs.Items.Add(BuildModulesTab());
            tabs.Items.Add(BuildFeaturesTab());
            tabs.Items.Add(BuildSourcesTab());
            tabs.Items.Add(BuildDiagnosticsTab());
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            buttons.Children.Add(CreateButton("重新加载", (sender, args) => ReloadFromDisk()));
            buttons.Children.Add(CreateButton("保存配置", (sender, args) => Save()));
            buttons.Children.Add(CreateButton("关闭", (sender, args) => Close()));
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            return root;
        }

        private TabItem BuildModulesTab()
        {
            _modulesGrid.ContextMenu = BuildModuleMenu();
            AttachGridBehaviors(_modulesGrid);
            return BuildTab("模块", _modulesGrid);
        }

        private TabItem BuildFeaturesTab()
        {
            _featuresGrid.ContextMenu = BuildFeatureMenu();
            AttachGridBehaviors(_featuresGrid);
            return BuildTab("功能", _featuresGrid);
        }

        private TabItem BuildSourcesTab()
        {
            _sourcesGrid.ContextMenu = BuildSourceMenu();
            return BuildTab("来源", _sourcesGrid);
        }

        private TabItem BuildDiagnosticsTab()
        {
            _diagnosticsGrid.IsReadOnly = true;
            return BuildTab("诊断", _diagnosticsGrid);
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
            LoadModuleRows();
            LoadFeatureRows();
            LoadSourceRows();
            LoadDiagnosticRows(FrameworkRuntimeState.Current);
            RefreshStatus("已加载配置。设置窗口会保存根配置和独立模块清单；Ribbon 布局、图标和按钮大小需重启 Revit 重绘。");
        }

        private void LoadModuleRows()
        {
            _modulesGrid.Columns.Clear();
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.Name), "模块", true, 2.1));
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.DisplayName), "显示名", false, 2.2));
            _modulesGrid.Columns.Add(CheckColumn(nameof(ModuleRow.Enabled), "启用"));
            _modulesGrid.Columns.Add(CheckColumn(nameof(ModuleRow.Visible), "显示"));
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.SourceId), "来源", false, 1.2));
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.Order), "排序", false, 0.7));

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
                    SourceId = string.IsNullOrWhiteSpace(module.SourceId) ? "builtin" : module.SourceId,
                    Order = module.Order
                }));
            _modulesGrid.ItemsSource = _moduleRows;
        }

        private void LoadFeatureRows()
        {
            _featuresGrid.Columns.Clear();
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.Name), "功能", true, 2.0));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.DisplayName), "显示名", false, 1.8));
            _featuresGrid.Columns.Add(CheckColumn(nameof(FeatureRow.Visible), "显示"));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.ModuleName), "模块", true, 1.3));
            _featuresGrid.Columns.Add(ComboColumn(nameof(FeatureRow.ButtonSize), "大小", new[] { "large", "small" }, 0.8));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.IconPath), "图标", false, 1.5));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.Order), "排序", false, 0.7));

            _featureRows = new ObservableCollection<FeatureRow>(EditableModules()
                .SelectMany(module => (module.Features ?? new List<FeatureConfiguration>()).Select(feature => new FeatureRow
                {
                    ModuleId = module.Id,
                    FeatureId = feature.Id,
                    ModuleName = DisplayName(module.DisplayName, module.Name, module.Id),
                    Name = DisplayName(feature.DisplayName, feature.Name, feature.Id),
                    DisplayName = feature.DisplayName,
                    Visible = string.Equals(feature.DefaultState, "Visible", StringComparison.OrdinalIgnoreCase),
                    IconPath = feature.IconPath,
                    Order = feature.Order,
                    ButtonSize = NormalizeButtonSize(feature.ButtonSize)
                }))
                .OrderBy(row => row.ModuleName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Order)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase));
            _featuresGrid.ItemsSource = _featureRows;
        }

        private void LoadSourceRows()
        {
            _sourcesGrid.Columns.Clear();
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Id), "来源 ID", false, 1.4));
            _sourcesGrid.Columns.Add(CheckColumn(nameof(SourceRow.Enabled), "启用"));
            _sourcesGrid.Columns.Add(CheckColumn(nameof(SourceRow.AutoUpdate), "拉取"));
            _sourcesGrid.Columns.Add(ComboColumn(nameof(SourceRow.Type), "类型", new[] { "localFolder", "github" }, 1.0));
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Path), "文件夹/缓存", false, 1.7));
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Repository), "GitHub 仓库", false, 1.5));
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Ref), "分支", false, 0.8));
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.ManifestPath), "清单", false, 1.1));
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Status), "状态", true, 1.4));

            var diagnostics = DiagnosticsBySourceId(FrameworkRuntimeState.Current);
            _sourceRows = new ObservableCollection<SourceRow>((_configuration.Modules.ModuleSources ?? new List<ModuleSourceConfiguration>())
                .Select(source => new SourceRow
                {
                    Id = source.Id,
                    Enabled = source.Enabled,
                    AutoUpdate = source.AutoUpdate,
                    Type = NormalizeSourceType(source.Type),
                    Path = source.Path,
                    Repository = source.Repository,
                    Ref = string.IsNullOrWhiteSpace(source.Ref) ? "main" : source.Ref,
                    ManifestPath = string.IsNullOrWhiteSpace(source.ManifestPath) ? "modules.json" : source.ManifestPath,
                    Status = diagnostics.TryGetValue(source.Id ?? string.Empty, out var diagnostic)
                        ? diagnostic
                        : source.Enabled ? "就绪" : "停用"
                }));
            _sourcesGrid.ItemsSource = _sourceRows;
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
                    Message = "当前没有诊断消息。"
                });
            }

            _diagnosticsGrid.ItemsSource = rows;
        }

        private void Save()
        {
            EndGridEdits();
            ApplyModuleRows();
            ApplyFeatureRows();
            ApplySourceRows();

            Directory.CreateDirectory(_configDirectory);
            SaveModuleDocuments();
            SaveJson(Path.Combine(_configDirectory, "views.json"), _configuration.Views);
            SaveJson(Path.Combine(_configDirectory, "feature-combinations.json"), _configuration.FeatureCombinations);

            LoadDiagnosticRows(FrameworkRuntimeState.Current);
            LoadSourceRows();
            RefreshStatus("已保存配置。模块、功能和来源设置已写回对应清单；Ribbon 布局、图标、按钮大小需重启 Revit 重绘。");
            MessageBox.Show(
                this,
                "配置已保存。\n\n模块、功能和来源设置已写回对应清单。Ribbon 布局、图标、按钮大小仍需重启 Revit 重绘。",
                "PlugHub 设置",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
                RefreshStatus("重新加载失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "PlugHub 设置", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ApplyModuleRows()
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

        private void ApplyFeatureRows()
        {
            var rows = _featureRows.ToDictionary(row => row.ModuleId + "|" + row.FeatureId, StringComparer.OrdinalIgnoreCase);
            foreach (var module in EditableModules())
            {
                foreach (var feature in module.Features ?? new List<FeatureConfiguration>())
                {
                    if (!rows.TryGetValue(module.Id + "|" + feature.Id, out var row)) continue;
                    feature.DisplayName = row.DisplayName ?? string.Empty;
                    feature.DefaultState = row.Visible ? "Visible" : "Hidden";
                    feature.IconPath = row.IconPath ?? string.Empty;
                    feature.Order = row.Order;
                    feature.ButtonSize = NormalizeButtonSize(row.ButtonSize);
                }
            }
        }

        private void ApplySourceRows()
        {
            _configuration.Modules.ModuleSources = _sourceRows
                .Where(row => !string.IsNullOrWhiteSpace(row.Id))
                .Select(row => new ModuleSourceConfiguration
                {
                    Id = row.Id.Trim(),
                    Type = NormalizeSourceType(row.Type),
                    Path = row.Path ?? string.Empty,
                    Repository = row.Repository ?? string.Empty,
                    Ref = string.IsNullOrWhiteSpace(row.Ref) ? "main" : row.Ref.Trim(),
                    ManifestPath = string.IsNullOrWhiteSpace(row.ManifestPath) ? "modules.json" : row.ManifestPath.Trim(),
                    Enabled = row.Enabled,
                    AutoUpdate = row.AutoUpdate
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
            AddModuleDocument(documents, seenPaths, Path.Combine(_configDirectory, "modules.json"), configuration.Modules);

            var baseDirectory = Directory.GetParent(_configDirectory)?.FullName ?? _configDirectory;
            foreach (var moduleDirectory in configuration.Modules.ModuleDirectories ?? new List<string>())
            {
                // moduleDirectories such as modules/samples are editable manifests too.
                foreach (var manifestPath in FindModuleManifests(ResolvePath(baseDirectory, moduleDirectory)))
                {
                    AddModuleDocument(documents, seenPaths, manifestPath, ReadModulesConfiguration(manifestPath));
                }
            }

            foreach (var source in configuration.Modules.ModuleSources ?? new List<ModuleSourceConfiguration>())
            {
                var sourceDirectory = ResolvePath(baseDirectory, source.Path);
                var manifestPath = Path.Combine(sourceDirectory, string.IsNullOrWhiteSpace(source.ManifestPath) ? "modules.json" : source.ManifestPath);
                if (File.Exists(manifestPath))
                {
                    AddModuleDocument(documents, seenPaths, manifestPath, ReadModulesConfiguration(manifestPath));
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

        private ModulesConfiguration ReadModulesConfiguration(string path)
        {
            return _serializer.Deserialize<ModulesConfiguration>(File.ReadAllText(path));
        }

        private static void AddModuleDocument(ICollection<ModuleManifestDocument> documents, ISet<string> seenPaths, string path, ModulesConfiguration modules)
        {
            if (string.IsNullOrWhiteSpace(path) || modules == null) return;
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath) && !string.Equals(Path.GetFileName(fullPath), "modules.json", StringComparison.OrdinalIgnoreCase)) return;
            if (!seenPaths.Add(fullPath)) return;
            documents.Add(new ModuleManifestDocument(fullPath, modules));
        }

        private static IEnumerable<string> FindModuleManifests(string sourceDirectory)
        {
            if (!Directory.Exists(sourceDirectory)) yield break;

            var rootManifest = Path.Combine(sourceDirectory, "modules.json");
            if (File.Exists(rootManifest))
            {
                yield return rootManifest;
            }

            foreach (var manifest in Directory.GetFiles(sourceDirectory, "modules.json", SearchOption.AllDirectories)
                         .Where(path => !string.Equals(path, rootManifest, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                yield return manifest;
            }
        }

        private static string ResolvePath(string baseDirectory, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return baseDirectory;
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
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

        private static DataGridComboBoxColumn ComboColumn(string propertyName, string header, IEnumerable<string> values, double starWidth)
        {
            return new DataGridComboBoxColumn
            {
                Header = header,
                ItemsSource = values,
                SelectedItemBinding = new Binding(propertyName) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
                Width = new DataGridLength(starWidth, DataGridLengthUnitType.Star)
            };
        }

        private ContextMenu BuildModuleMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("启用并显示", (sender, args) => SetSelectedModuleState(true, true)));
            menu.Items.Add(MenuItem("禁用", (sender, args) => SetSelectedModuleState(false, false)));
            menu.Items.Add(MenuItem("仅隐藏", (sender, args) => SetSelectedModuleState(true, false)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("上移", (sender, args) => MoveSelectedRow(_modulesGrid, -1)));
            menu.Items.Add(MenuItem("下移", (sender, args) => MoveSelectedRow(_modulesGrid, 1)));
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
            menu.Items.Add(MenuItem("清空图标", (sender, args) => SetSelectedFeatureIcon(string.Empty)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("上移", (sender, args) => MoveSelectedRow(_featuresGrid, -1)));
            menu.Items.Add(MenuItem("下移", (sender, args) => MoveSelectedRow(_featuresGrid, 1)));
            return menu;
        }

        private ContextMenu BuildSourceMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(MenuItem("启用", (sender, args) => SetSelectedSourceEnabled(true)));
            menu.Items.Add(MenuItem("禁用", (sender, args) => SetSelectedSourceEnabled(false)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("新增本地文件夹", (sender, args) => AddSource("localFolder")));
            menu.Items.Add(MenuItem("新增 GitHub 仓库", (sender, args) => AddSource("github")));
            menu.Items.Add(MenuItem("删除来源", (sender, args) => RemoveSelectedSource()));
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
            if (_modulesGrid.SelectedItem is ModuleRow row)
            {
                row.Enabled = enabled;
                row.Visible = visible;
                _modulesGrid.Items.Refresh();
            }
        }

        private void SetSelectedFeatureVisible(bool visible)
        {
            if (_featuresGrid.SelectedItem is FeatureRow row)
            {
                row.Visible = visible;
                _featuresGrid.Items.Refresh();
            }
        }

        private void SetSelectedFeatureSize(string size)
        {
            if (_featuresGrid.SelectedItem is FeatureRow row)
            {
                row.ButtonSize = NormalizeButtonSize(size);
                _featuresGrid.Items.Refresh();
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
            _featuresGrid.Items.Refresh();
        }

        private void SetSelectedSourceEnabled(bool enabled)
        {
            if (_sourcesGrid.SelectedItem is SourceRow row)
            {
                row.Enabled = enabled;
                row.Status = enabled ? "待保存" : "停用";
                _sourcesGrid.Items.Refresh();
            }
        }

        private void AddSource(string type)
        {
            var normalizedType = NormalizeSourceType(type);
            var id = UniqueSourceId(_sourceRows, normalizedType == "github" ? "github-source" : "local-source");
            _sourceRows.Add(new SourceRow
            {
                Id = id,
                Enabled = true,
                AutoUpdate = normalizedType == "github",
                Type = normalizedType,
                Path = normalizedType == "github" ? "modules/github/" + id : "modules/" + id,
                Repository = normalizedType == "github" ? "owner/repository" : string.Empty,
                Ref = "main",
                ManifestPath = "modules.json",
                Status = "待保存"
            });
        }

        private void RemoveSelectedSource()
        {
            if (_sourcesGrid.SelectedItem is SourceRow row)
            {
                _sourceRows.Remove(row);
            }
        }

        private void MoveSelectedRow(DataGrid grid, int direction)
        {
            var sourceIndex = grid.SelectedIndex;
            if (sourceIndex < 0) return;

            var targetIndex = sourceIndex + direction;
            if (grid == _modulesGrid)
            {
                MoveRow(_moduleRows, sourceIndex, targetIndex);
                RecalculateModuleOrders();
                _modulesGrid.SelectedIndex = targetIndex;
                return;
            }

            if (grid == _featuresGrid)
            {
                MoveRow(_featureRows, sourceIndex, targetIndex);
                RecalculateFeatureOrders();
                _featuresGrid.SelectedIndex = targetIndex;
            }
        }

        private static void MoveRow<T>(ObservableCollection<T> rows, int sourceIndex, int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= rows.Count) return;
            rows.Move(sourceIndex, targetIndex);
        }

        private void RecalculateModuleOrders()
        {
            for (var index = 0; index < _moduleRows.Count; index++)
            {
                _moduleRows[index].Order = (index + 1) * 100;
            }

            _modulesGrid.Items.Refresh();
        }

        private void RecalculateFeatureOrders()
        {
            for (var index = 0; index < _featureRows.Count; index++)
            {
                _featureRows[index].Order = (index + 1) * 10;
            }

            _featuresGrid.Items.Refresh();
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
            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            _dragSourceGrid = grid;
            _dragSourceRowIndex = row?.GetIndex() ?? -1;
        }

        private void GridMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragSourceRowIndex < 0) return;
            if (!(sender is DataGrid grid) || grid != _dragSourceGrid) return;
            if (_dragSourceRowIndex >= grid.Items.Count) return;

            DragDrop.DoDragDrop(grid, grid.Items[_dragSourceRowIndex], DragDropEffects.Move);
        }

        private static void GridDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void GridDrop(object sender, DragEventArgs e)
        {
            if (!(sender is DataGrid grid) || grid != _dragSourceGrid) return;
            if (_dragSourceRowIndex < 0) return;

            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            var targetIndex = row?.GetIndex() ?? grid.Items.Count - 1;
            if (targetIndex < 0 || targetIndex == _dragSourceRowIndex) return;

            if (grid == _modulesGrid)
            {
                MoveRow(_moduleRows, _dragSourceRowIndex, targetIndex);
                RecalculateModuleOrders();
            }
            else if (grid == _featuresGrid)
            {
                MoveRow(_featureRows, _dragSourceRowIndex, targetIndex);
                RecalculateFeatureOrders();
            }

            grid.SelectedIndex = targetIndex;
            _dragSourceRowIndex = -1;
            _dragSourceGrid = null;
        }

        private void EndGridEdits()
        {
            CommitGrid(_modulesGrid);
            CommitGrid(_featuresGrid);
            CommitGrid(_sourcesGrid);
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

        private string ToPluginRelativePath(string path)
        {
            var baseDirectory = Directory.GetParent(_configDirectory)?.FullName ?? _configDirectory;
            var fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(baseDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return fullPath;
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

        private static string NormalizeButtonSize(string value)
        {
            return string.Equals(value, "small", StringComparison.OrdinalIgnoreCase) ? "small" : "large";
        }

        private static string NormalizeSourceType(string value)
        {
            return string.Equals(value, "github", StringComparison.OrdinalIgnoreCase) ? "github" : "localFolder";
        }

        private static string UniqueSourceId(IEnumerable<SourceRow> rows, string prefix)
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

        private sealed class ModuleRow
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool Enabled { get; set; }
            public bool Visible { get; set; }
            public string SourceId { get; set; } = string.Empty;
            public int Order { get; set; }
        }

        private sealed class FeatureRow
        {
            public string ModuleId { get; set; } = string.Empty;
            public string FeatureId { get; set; } = string.Empty;
            public string ModuleName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool Visible { get; set; }
            public string IconPath { get; set; } = string.Empty;
            public int Order { get; set; }
            public string ButtonSize { get; set; } = "large";
        }

        private sealed class SourceRow
        {
            public string Id { get; set; } = string.Empty;
            public bool Enabled { get; set; }
            public bool AutoUpdate { get; set; }
            public string Type { get; set; } = "localFolder";
            public string Path { get; set; } = string.Empty;
            public string Repository { get; set; } = string.Empty;
            public string Ref { get; set; } = "main";
            public string ManifestPath { get; set; } = "modules.json";
            public string Status { get; set; } = string.Empty;
        }

        private sealed class DiagnosticRow
        {
            public string Severity { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
            public string Scope { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
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
