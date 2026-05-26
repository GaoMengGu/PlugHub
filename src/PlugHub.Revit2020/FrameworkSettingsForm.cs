using System;
using System.Collections.Generic;
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
        private readonly FrameworkConfiguration _configuration;
        private readonly DataGridView _modulesGrid = new DataGridView();
        private readonly DataGridView _featuresGrid = new DataGridView();
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

            BuildLayout();
            LoadRows();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Text = "PlugHub 工作台设置：开关模块和功能，编辑显示名称、图标、按钮大小与排序。Ribbon 结构类变更保存后需重启 Revit 生效。"
            }, 0, 0);

            root.Controls.Add(BuildGroup("模块", _modulesGrid), 0, 1);
            root.Controls.Add(BuildGroup("功能", _featuresGrid), 0, 2);
            root.Controls.Add(BuildButtons(), 0, 3);

            Controls.Add(root);
        }

        private static GroupBox BuildGroup(string title, DataGridView grid)
        {
            ConfigureGrid(grid);
            var group = new GroupBox { Dock = DockStyle.Fill, Text = title, Padding = new Padding(8) };
            group.Controls.Add(grid);
            return group;
        }

        private Control BuildButtons()
        {
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var closeButton = new Button { Text = "关闭", Width = 90, Height = 30 };
            closeButton.Click += (sender, args) => Close();

            var saveButton = new Button { Text = "保存", Width = 90, Height = 30 };
            saveButton.Click += (sender, args) => Save();

            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(saveButton);
            return buttons;
        }

        private static void ConfigureGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
        }

        private void LoadRows()
        {
            LoadModuleRows();
            LoadFeatureRows();
        }

        private void LoadModuleRows()
        {
            _modulesGrid.Columns.Clear();
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.Name), "模块", true, 24));
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.DisplayName), "显示名", false, 24));
            _modulesGrid.Columns.Add(CheckColumn(nameof(ModuleRow.Enabled), "启用", 12));
            _modulesGrid.Columns.Add(CheckColumn(nameof(ModuleRow.Visible), "显示", 12));
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.SourceId), "来源", true, 12));
            _modulesGrid.Columns.Add(TextColumn(nameof(ModuleRow.Order), "顺序", false, 12));

            _modulesGrid.DataSource = (_configuration.Modules.Modules ?? new List<ModuleConfiguration>())
                .Select(module => new ModuleRow
                {
                    Id = module.Id,
                    Name = string.IsNullOrWhiteSpace(module.Name) ? module.Id : module.Name,
                    DisplayName = module.DisplayName,
                    Enabled = module.Enabled,
                    Visible = module.Visible,
                    SourceId = string.IsNullOrWhiteSpace(module.SourceId) ? "builtin" : module.SourceId,
                    Order = module.Order
                })
                .ToList();

            AttachGridBehaviors(_modulesGrid, BuildModuleMenu());
        }

        private void LoadFeatureRows()
        {
            _featuresGrid.Columns.Clear();
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.ModuleName), "模块", true, 14));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.Name), "功能", true, 18));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.DisplayName), "显示名", false, 18));
            _featuresGrid.Columns.Add(CheckColumn(nameof(FeatureRow.Visible), "显示", 8));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.Panel), "面板", false, 12));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.IconPath), "图标", false, 14));
            _featuresGrid.Columns.Add(TextColumn(nameof(FeatureRow.Order), "顺序", false, 8));
            _featuresGrid.Columns.Add(SizeColumn());

            _featuresGrid.DataSource = (_configuration.Modules.Modules ?? new List<ModuleConfiguration>())
                .SelectMany(module => (module.Features ?? new List<FeatureConfiguration>()).Select(feature => new FeatureRow
                {
                    ModuleId = module.Id,
                    FeatureId = feature.Id,
                    ModuleName = string.IsNullOrWhiteSpace(module.Name) ? module.Id : module.Name,
                    Name = string.IsNullOrWhiteSpace(feature.Name) ? feature.Id : feature.Name,
                    DisplayName = feature.DisplayName,
                    Visible = string.Equals(feature.DefaultState, "Visible", StringComparison.OrdinalIgnoreCase),
                    Panel = feature.Group,
                    IconPath = feature.IconPath,
                    Order = feature.Order,
                    ButtonSize = NormalizeButtonSize(feature.ButtonSize)
                }))
                .OrderBy(row => row.ModuleName)
                .ThenBy(row => row.Order)
                .ThenBy(row => row.Name)
                .ToList();

            AttachGridBehaviors(_featuresGrid, BuildFeatureMenu());
        }

        private void Save()
        {
            _modulesGrid.EndEdit();
            _featuresGrid.EndEdit();

            ApplyModuleRows();
            ApplyFeatureRows();

            Directory.CreateDirectory(_configDirectory);
            SaveJson(Path.Combine(_configDirectory, "modules.json"), _configuration.Modules);
            SaveJson(Path.Combine(_configDirectory, "views.json"), _configuration.Views);
            SaveJson(Path.Combine(_configDirectory, "feature-combinations.json"), _configuration.FeatureCombinations);

            try
            {
                FrameworkRuntimeState.Refresh();
            }
            catch (Exception)
            {
                // The settings file is still saved. Runtime refresh can fail in design-time or partial startup contexts.
            }

            MessageBox.Show(this, "已保存。模块/功能开关会尽量即时生效；图标、按钮大小、面板和 Ribbon 结构调整需重启 Revit 2020。", "PlugHub 设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                FillWeight = 12,
                FlatStyle = FlatStyle.Flat,
                Items = { "large", "small" }
            };
        }

        private void AttachGridBehaviors(DataGridView grid, ContextMenuStrip menu)
        {
            grid.ContextMenuStrip = menu;
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
            menu.Items.Add("启用", null, (sender, args) => SetSelectedModuleState(true, true));
            menu.Items.Add("禁用", null, (sender, args) => SetSelectedModuleState(false, true));
            menu.Items.Add("显示", null, (sender, args) => SetSelectedModuleState(true, true));
            menu.Items.Add("隐藏", null, (sender, args) => SetSelectedModuleState(true, false));
            menu.Items.Add("上移", null, (sender, args) => MoveSelectedRow(_modulesGrid, -1));
            menu.Items.Add("下移", null, (sender, args) => MoveSelectedRow(_modulesGrid, 1));
            return menu;
        }

        private ContextMenuStrip BuildFeatureMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("启用/显示", null, (sender, args) => SetSelectedFeatureVisible(true));
            menu.Items.Add("禁用/隐藏", null, (sender, args) => SetSelectedFeatureVisible(false));
            menu.Items.Add("设为大按钮", null, (sender, args) => SetSelectedFeatureSize("large"));
            menu.Items.Add("设为小按钮", null, (sender, args) => SetSelectedFeatureSize("small"));
            menu.Items.Add("上移", null, (sender, args) => MoveSelectedRow(_featuresGrid, -1));
            menu.Items.Add("下移", null, (sender, args) => MoveSelectedRow(_featuresGrid, 1));
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

            var clientPoint = grid.PointToClient(new System.Drawing.Point(e.X, e.Y));
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

        private static List<T> Rows<T>(DataGridView grid)
        {
            return (grid.DataSource as IEnumerable<T>)?.ToList() ?? new List<T>();
        }

        private static string NormalizeButtonSize(string value)
        {
            return string.Equals(value, "small", StringComparison.OrdinalIgnoreCase) ? "small" : "large";
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
    }
}
