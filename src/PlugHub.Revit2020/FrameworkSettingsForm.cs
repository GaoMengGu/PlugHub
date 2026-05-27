using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    internal sealed class FrameworkSettingsForm : Form
    {
        private readonly string _configDirectory;
        private FrameworkConfiguration _configuration;
        private readonly DataGridView _modulesGrid = new DataGridView();
        private readonly DataGridView _featuresGrid = new DataGridView();
        private readonly DataGridView _sourcesGrid = new DataGridView();
        private readonly DataGridView _diagnosticsGrid = new DataGridView();
        private readonly Label _statusLabel = new Label();
        private readonly TabControl _tabs = new TabControl();
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 128 };
        private int _dragSourceRowIndex = -1;

        public FrameworkSettingsForm(string configDirectory, FrameworkConfiguration configuration)
        {
            _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            Text = "PlugHub 设置";
            Width = 980;
            Height = 660;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(247, 249, 252);
            Font = new Font("Microsoft YaHei UI", 9F);

            BuildLayout();
            LoadRows();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            _tabs.Dock = DockStyle.Fill;
            _tabs.Padding = new Point(14, 5);
            _tabs.TabPages.Add(BuildModulesTab());
            _tabs.TabPages.Add(BuildFeaturesTab());
            _tabs.TabPages.Add(BuildSourcesTab());
            _tabs.TabPages.Add(BuildDiagnosticsTab());

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(_tabs, 0, 1);
            root.Controls.Add(BuildButtons(), 0, 2);
            Controls.Add(root);
        }

        private Control BuildHeader()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BackColor
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "PlugHub 设置",
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.FromArgb(27, 36, 48),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.ForeColor = Color.FromArgb(92, 102, 115);
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            panel.Controls.Add(_statusLabel, 0, 1);

            return panel;
        }

        private TabPage BuildModulesTab()
        {
            ConfigureGrid(_modulesGrid);
            _modulesGrid.ContextMenuStrip = BuildModuleMenu();
            AttachGridBehaviors(_modulesGrid);
            return BuildTabPage("模块", _modulesGrid);
        }

        private TabPage BuildFeaturesTab()
        {
            ConfigureGrid(_featuresGrid);
            _featuresGrid.ContextMenuStrip = BuildFeatureMenu();
            AttachGridBehaviors(_featuresGrid);
            return BuildTabPage("功能", _featuresGrid);
        }

        private TabPage BuildSourcesTab()
        {
            ConfigureGrid(_sourcesGrid);
            _sourcesGrid.ContextMenuStrip = BuildSourceMenu();
            return BuildTabPage("来源", _sourcesGrid);
        }

        private TabPage BuildDiagnosticsTab()
        {
            ConfigureGrid(_diagnosticsGrid);
            _diagnosticsGrid.ReadOnly = true;
            return BuildTabPage("诊断", _diagnosticsGrid);
        }

        private TabPage BuildTabPage(string title, DataGridView grid)
        {
            var page = new TabPage(title)
            {
                Padding = new Padding(10),
                BackColor = BackColor
            };

            var shell = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Padding = new Padding(1)
            };
            shell.Controls.Add(grid);
            page.Controls.Add(shell);
            return page;
        }

        private Control BuildButtons()
        {
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = BackColor
            };

            var saveButton = CreateButton("保存并刷新");
            saveButton.Width = 112;
            saveButton.Click += (sender, args) => Save();

            var reloadButton = CreateButton("重新加载");
            reloadButton.Width = 104;
            reloadButton.Click += (sender, args) => ReloadFromDisk();

            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(reloadButton);
            return buttons;
        }

        private Button CreateButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 88,
                Height = 30,
                FlatStyle = FlatStyle.System
            };
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 243, 247);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(39, 48, 62);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 243, 247);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 235, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(21, 31, 45);
            grid.GridColor = Color.FromArgb(229, 234, 241);
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = 28;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.DataError += (sender, args) => args.ThrowException = false;
        }

        private void LoadRows()
        {
            LoadModuleRows();
            LoadFeatureRows();
            LoadSourceRows();
            LoadDiagnosticRows(FrameworkRuntimeState.Current);
            RefreshStatus("已加载配置。再次点击 Ribbon 设置或使用 Revit 面板标题栏可收起此面板。");
        }

        private void LoadModuleRows()
        {
            _modulesGrid.Columns.Clear();
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.Name), "模块", true, 20));
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.DisplayName), "显示名", false, 24));
            _modulesGrid.Columns.Add(CheckColumn(nameof(ModuleRow.Enabled), "启用", 9));
            _modulesGrid.Columns.Add(CheckColumn(nameof(ModuleRow.Visible), "显示", 9));
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.SourceId), "来源", false, 14));
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.Order), "排序", false, 8));

            BindModules((_configuration.Modules.Modules ?? new List<ModuleConfiguration>())
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
                })
                .ToList());
        }

        private void LoadFeatureRows()
        {
            _featuresGrid.Columns.Clear();
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.Name), "功能", true, 20));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.DisplayName), "显示名", false, 20));
            _featuresGrid.Columns.Add(CheckColumn(nameof(FeatureRow.Visible), "显示", 8));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.ModuleName), "模块", true, 14));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.Panel), "面板", false, 12));
            _featuresGrid.Columns.Add(SizeColumn());
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.IconPath), "图标", false, 16));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.Order), "排序", false, 8));

            BindFeatures((_configuration.Modules.Modules ?? new List<ModuleConfiguration>())
                .SelectMany(module => (module.Features ?? new List<FeatureConfiguration>()).Select(feature => new FeatureRow
                {
                    ModuleId = module.Id,
                    FeatureId = feature.Id,
                    ModuleName = DisplayName(module.DisplayName, module.Name, module.Id),
                    Name = DisplayName(feature.DisplayName, feature.Name, feature.Id),
                    DisplayName = feature.DisplayName,
                    Visible = string.Equals(feature.DefaultState, "Visible", StringComparison.OrdinalIgnoreCase),
                    Panel = feature.Group,
                    IconPath = feature.IconPath,
                    Order = feature.Order,
                    ButtonSize = NormalizeButtonSize(feature.ButtonSize)
                }))
                .OrderBy(row => row.ModuleName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Order)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        private void LoadSourceRows()
        {
            _sourcesGrid.Columns.Clear();
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Id), "来源 ID", false, 18));
            _sourcesGrid.Columns.Add(CheckColumn(nameof(SourceRow.Enabled), "启用", 8));
            _sourcesGrid.Columns.Add(CheckColumn(nameof(SourceRow.AutoUpdate), "拉取", 8));
            _sourcesGrid.Columns.Add(SourceTypeColumn());
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Path), "文件夹/缓存", false, 20));
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Repository), "GitHub 仓库", false, 18));
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Ref), "分支", false, 10));
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.ManifestPath), "清单", false, 14));
            _sourcesGrid.Columns.Add(TextColumn(nameof(SourceRow.Status), "状态", true, 18));

            var diagnostics = DiagnosticsBySourceId(FrameworkRuntimeState.Current);
            BindSources((_configuration.Modules.ModuleSources ?? new List<ModuleSourceConfiguration>())
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
                })
                .ToList());
        }

        private void LoadDiagnosticRows(FrameworkRuntimeSnapshot? snapshot)
        {
            _diagnosticsGrid.Columns.Clear();
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Severity), "级别", true, 10));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Code), "代码", true, 14));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Scope), "对象", true, 18));
            _diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Message), "消息", true, 58));

            var rows = (snapshot?.Diagnostics ?? new List<PlugHub.Contracts.Modules.DiagnosticMessage>())
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

            _diagnosticsGrid.DataSource = rows;
        }

        private void Save()
        {
            EndGridEdits();
            ApplyModuleRows();
            ApplyFeatureRows();
            ApplySourceRows();

            Directory.CreateDirectory(_configDirectory);
            SaveJson(Path.Combine(_configDirectory, "modules.json"), _configuration.Modules);
            SaveJson(Path.Combine(_configDirectory, "views.json"), _configuration.Views);
            SaveJson(Path.Combine(_configDirectory, "feature-combinations.json"), _configuration.FeatureCombinations);

            FrameworkRuntimeSnapshot? snapshot = null;
            string refreshMessage;
            try
            {
                snapshot = FrameworkRuntimeState.Refresh();
                refreshMessage = "已保存并刷新运行时。";
            }
            catch (Exception ex)
            {
                refreshMessage = "已保存；运行时刷新失败：" + ex.Message;
            }

            LoadDiagnosticRows(snapshot ?? FrameworkRuntimeState.Current);
            LoadSourceRows();
            RefreshStatus(refreshMessage + " 开关会即时拦截执行；Ribbon 结构、图标和大小需重启 Revit 重绘。");
            MessageBox.Show(this, refreshMessage + "\n\n模块/功能开关会即时拦截执行；Ribbon 结构、图标、按钮大小和新增来源模块需重启 Revit 2020 重绘。", "PlugHub 设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ReloadFromDisk()
        {
            try
            {
                _configuration = FrameworkConfigurationLoader.LoadFromDirectory(_configDirectory);
                LoadRows();
                RefreshStatus("已从配置文件重新加载。");
            }
            catch (Exception ex)
            {
                RefreshStatus("重新加载失败：" + ex.Message);
                MessageBox.Show(this, ex.Message, "PlugHub 设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplyModuleRows()
        {
            var rows = Rows<ModuleRow>(_modulesGrid).ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);
            foreach (var module in _configuration.Modules.Modules ?? new List<ModuleConfiguration>())
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
            var rows = Rows<FeatureRow>(_featuresGrid).ToDictionary(row => row.ModuleId + "|" + row.FeatureId, StringComparer.OrdinalIgnoreCase);
            foreach (var module in _configuration.Modules.Modules ?? new List<ModuleConfiguration>())
            {
                foreach (var feature in module.Features ?? new List<FeatureConfiguration>())
                {
                    if (!rows.TryGetValue(module.Id + "|" + feature.Id, out var row)) continue;
                    feature.DisplayName = row.DisplayName ?? string.Empty;
                    feature.DefaultState = row.Visible ? "Visible" : "Hidden";
                    feature.Group = row.Panel ?? string.Empty;
                    feature.IconPath = row.IconPath ?? string.Empty;
                    feature.Order = row.Order;
                    feature.ButtonSize = NormalizeButtonSize(row.ButtonSize);
                }
            }
        }

        private void ApplySourceRows()
        {
            _configuration.Modules.ModuleSources = Rows<SourceRow>(_sourcesGrid)
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

        private static DataGridViewTextBoxColumn TextColumn(string propertyName, string header, bool readOnly, float fillWeight)
        {
            return new DataGridViewTextBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = header,
                ReadOnly = readOnly,
                FillWeight = fillWeight,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
        }

        private static DataGridViewCheckBoxColumn CheckColumn(string propertyName, string header, float fillWeight)
        {
            return new DataGridViewCheckBoxColumn
            {
                DataPropertyName = propertyName,
                HeaderText = header,
                FillWeight = fillWeight,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
        }

        private static DataGridViewComboBoxColumn SizeColumn()
        {
            return new DataGridViewComboBoxColumn
            {
                DataPropertyName = nameof(FeatureRow.ButtonSize),
                HeaderText = "大小",
                FillWeight = 9,
                FlatStyle = FlatStyle.Flat,
                Items = { "large", "small" }
            };
        }

        private static DataGridViewComboBoxColumn SourceTypeColumn()
        {
            return new DataGridViewComboBoxColumn
            {
                DataPropertyName = nameof(SourceRow.Type),
                HeaderText = "类型",
                FillWeight = 12,
                FlatStyle = FlatStyle.Flat,
                Items = { "localFolder", "github" }
            };
        }

        private void AttachGridBehaviors(DataGridView grid)
        {
            grid.MouseDown -= GridMouseDown;
            grid.MouseMove -= GridMouseMove;
            grid.DragOver -= GridDragOver;
            grid.DragDrop -= GridDragDrop;
            grid.MouseDown += GridMouseDown;
            grid.MouseMove += GridMouseMove;
            grid.DragOver += GridDragOver;
            grid.DragDrop += GridDragDrop;
            grid.AllowDrop = true;
        }

        private ContextMenuStrip BuildModuleMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("启用并显示", null, (sender, args) => SetSelectedModuleState(true, true));
            menu.Items.Add("禁用", null, (sender, args) => SetSelectedModuleState(false, false));
            menu.Items.Add("仅隐藏", null, (sender, args) => SetSelectedModuleState(true, false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("上移", null, (sender, args) => MoveSelectedRow(_modulesGrid, -1));
            menu.Items.Add("下移", null, (sender, args) => MoveSelectedRow(_modulesGrid, 1));
            return menu;
        }

        private ContextMenuStrip BuildFeatureMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示", null, (sender, args) => SetSelectedFeatureVisible(true));
            menu.Items.Add("隐藏", null, (sender, args) => SetSelectedFeatureVisible(false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("设为大按钮", null, (sender, args) => SetSelectedFeatureSize("large"));
            menu.Items.Add("设为小按钮", null, (sender, args) => SetSelectedFeatureSize("small"));
            menu.Items.Add("设置图标...", null, (sender, args) => SetSelectedFeatureIcon());
            menu.Items.Add("清空图标", null, (sender, args) => SetSelectedFeatureIcon(string.Empty));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("上移", null, (sender, args) => MoveSelectedRow(_featuresGrid, -1));
            menu.Items.Add("下移", null, (sender, args) => MoveSelectedRow(_featuresGrid, 1));
            return menu;
        }

        private ContextMenuStrip BuildSourceMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("启用", null, (sender, args) => SetSelectedSourceEnabled(true));
            menu.Items.Add("禁用", null, (sender, args) => SetSelectedSourceEnabled(false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("新增本地文件夹", null, (sender, args) => AddSource("localFolder"));
            menu.Items.Add("新增 GitHub 仓库", null, (sender, args) => AddSource("github"));
            menu.Items.Add("删除来源", null, (sender, args) => RemoveSelectedSource());
            return menu;
        }

        private void SetSelectedModuleState(bool enabled, bool visible)
        {
            if (_modulesGrid.CurrentRow?.DataBoundItem is ModuleRow row)
            {
                row.Enabled = enabled;
                row.Visible = visible;
                _modulesGrid.Refresh();
            }
        }

        private void SetSelectedFeatureVisible(bool visible)
        {
            if (_featuresGrid.CurrentRow?.DataBoundItem is FeatureRow row)
            {
                row.Visible = visible;
                _featuresGrid.Refresh();
            }
        }

        private void SetSelectedFeatureSize(string size)
        {
            if (_featuresGrid.CurrentRow?.DataBoundItem is FeatureRow row)
            {
                row.ButtonSize = NormalizeButtonSize(size);
                _featuresGrid.Refresh();
            }
        }

        private void SetSelectedFeatureIcon(string? iconPath = null)
        {
            if (!(_featuresGrid.CurrentRow?.DataBoundItem is FeatureRow row)) return;

            if (iconPath == null)
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择功能图标";
                    dialog.Filter = "图标和图片|*.png;*.jpg;*.jpeg;*.ico;*.bmp|所有文件|*.*";
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    iconPath = ToPluginRelativePath(dialog.FileName);
                }
            }

            row.IconPath = iconPath;
            _featuresGrid.Refresh();
        }

        private void SetSelectedSourceEnabled(bool enabled)
        {
            if (_sourcesGrid.CurrentRow?.DataBoundItem is SourceRow row)
            {
                row.Enabled = enabled;
                row.Status = enabled ? "待保存" : "停用";
                _sourcesGrid.Refresh();
            }
        }

        private void AddSource(string type)
        {
            var rows = Rows<SourceRow>(_sourcesGrid);
            var normalizedType = NormalizeSourceType(type);
            var id = UniqueSourceId(rows, normalizedType == "github" ? "github-source" : "local-source");
            rows.Add(new SourceRow
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

            BindSources(rows);
        }

        private void RemoveSelectedSource()
        {
            if (_sourcesGrid.CurrentRow == null) return;
            var rows = Rows<SourceRow>(_sourcesGrid);
            rows.RemoveAt(_sourcesGrid.CurrentRow.Index);
            BindSources(rows);
        }

        private void MoveSelectedRow(DataGridView grid, int direction)
        {
            if (grid.CurrentRow == null) return;
            var rows = grid.DataSource as System.Collections.IList;
            if (rows == null) return;

            var sourceIndex = grid.CurrentRow.Index;
            var targetIndex = sourceIndex + direction;
            if (targetIndex < 0 || targetIndex >= rows.Count) return;

            var item = rows[sourceIndex];
            rows.RemoveAt(sourceIndex);
            rows.Insert(targetIndex, item);
            RecalculateOrders(rows);
            grid.DataSource = null;
            grid.DataSource = rows;
            grid.CurrentCell = grid.Rows[targetIndex].Cells[0];
        }

        private static void RecalculateOrders(System.Collections.IList rows)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index] is ModuleRow module) module.Order = (index + 1) * 100;
                if (rows[index] is FeatureRow feature) feature.Order = (index + 1) * 10;
            }
        }

        private void GridMouseDown(object sender, MouseEventArgs e)
        {
            if (!(sender is DataGridView grid)) return;
            var hit = grid.HitTest(e.X, e.Y);
            _dragSourceRowIndex = hit.RowIndex;
            if (hit.RowIndex >= 0)
            {
                grid.CurrentCell = grid.Rows[hit.RowIndex].Cells[Math.Max(0, hit.ColumnIndex)];
            }
        }

        private void GridMouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _dragSourceRowIndex < 0) return;
            if (sender is DataGridView grid && _dragSourceRowIndex < grid.Rows.Count)
            {
                grid.DoDragDrop(grid.Rows[_dragSourceRowIndex], DragDropEffects.Move);
            }
        }

        private static void GridDragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void GridDragDrop(object sender, DragEventArgs e)
        {
            if (!(sender is DataGridView grid)) return;
            if (_dragSourceRowIndex < 0) return;

            var clientPoint = grid.PointToClient(new Point(e.X, e.Y));
            var targetIndex = grid.HitTest(clientPoint.X, clientPoint.Y).RowIndex;
            if (targetIndex < 0 || targetIndex == _dragSourceRowIndex) return;

            var rows = grid.DataSource as System.Collections.IList;
            if (rows == null) return;
            var item = rows[_dragSourceRowIndex];
            rows.RemoveAt(_dragSourceRowIndex);
            rows.Insert(targetIndex, item);
            RecalculateOrders(rows);
            grid.DataSource = null;
            grid.DataSource = rows;
            grid.CurrentCell = grid.Rows[targetIndex].Cells[0];
            _dragSourceRowIndex = -1;
        }

        private void EndGridEdits()
        {
            _modulesGrid.EndEdit();
            _featuresGrid.EndEdit();
            _sourcesGrid.EndEdit();
        }

        private void BindModules(List<ModuleRow> rows)
        {
            _modulesGrid.DataSource = rows;
        }

        private void BindFeatures(List<FeatureRow> rows)
        {
            _featuresGrid.DataSource = rows;
        }

        private void BindSources(List<SourceRow> rows)
        {
            _sourcesGrid.DataSource = rows;
        }

        private void RefreshStatus(string text)
        {
            _statusLabel.Text = text;
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

        private static Dictionary<string, string> DiagnosticsBySourceId(FrameworkRuntimeSnapshot? snapshot)
        {
            return (snapshot?.Diagnostics ?? new List<PlugHub.Contracts.Modules.DiagnosticMessage>())
                .Where(message => !string.IsNullOrWhiteSpace(message.ModuleId))
                .GroupBy(message => message.ModuleId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Message, StringComparer.OrdinalIgnoreCase);
        }

        private static List<T> Rows<T>(DataGridView grid)
        {
            return (grid.DataSource as IEnumerable<T>)?.ToList() ?? new List<T>();
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
            public string Panel { get; set; } = string.Empty;
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
    }
}
