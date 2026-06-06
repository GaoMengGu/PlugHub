using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Runtime;

namespace PlugHub.Wpf
{
    public sealed class FrameworkStatusWindow : Window
    {
        public FrameworkStatusWindow(string title, string summary, IEnumerable<DiagnosticMessage> diagnostics, bool showLogs)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "PlugHub 状态" : title;
            Width = 760;
            Height = showLogs ? 560 : 340;
            MinWidth = 620;
            MinHeight = showLogs ? 420 : 260;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            RevitUiTheme.Apply(this);
            var theme = RevitUiTheme.Current;

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (showLogs)
            {
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            header.Children.Add(new TextBlock
            {
                Text = Title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextBrush
            });
            header.Children.Add(new TextBlock
            {
                Text = summary ?? string.Empty,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = theme.MutedTextBrush,
                LineHeight = 20
            });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var buttonRow = 1;
            if (showLogs)
            {
                var diagnosticsGrid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    CanUserAddRows = false,
                    CanUserDeleteRows = false,
                    IsReadOnly = true,
                    HeadersVisibility = DataGridHeadersVisibility.Column,
                    GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                    Background = theme.PanelBackground,
                    Foreground = theme.TextBrush,
                    BorderBrush = theme.BorderBrush,
                    RowHeight = 30,
                    AlternatingRowBackground = theme.AlternatingRowBrush,
                    ItemsSource = BuildDiagnosticRows(diagnostics)
                };
                diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Severity), "级别", 90));
                diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Code), "代码", 130));
                diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Scope), "对象", 170));
                diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Message), "消息", 1, true));
                Grid.SetRow(diagnosticsGrid, 1);
                root.Children.Add(diagnosticsGrid);
                buttonRow = 2;
            }

            var closeButton = new Button
            {
                Content = "关闭",
                Width = 88,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            closeButton.Click += (sender, args) => Close();
            Grid.SetRow(closeButton, buttonRow);
            root.Children.Add(closeButton);

            Content = root;
        }

        public static void ShowDialog(string title, string summary, IEnumerable<DiagnosticMessage> diagnostics)
        {
            ShowLogs(title, summary, diagnostics);
        }

        public static void ShowRuntimeStatus(FrameworkRuntimeSnapshot? snapshot)
        {
            RevitWindowOwner.ShowDialog(new FrameworkStatusWindow("PlugHub 状态", BuildRuntimeStatus(snapshot), Array.Empty<DiagnosticMessage>(), false));
        }

        public static void ShowLogs(string title, string summary, IEnumerable<DiagnosticMessage> diagnostics)
        {
            RevitWindowOwner.ShowDialog(new FrameworkStatusWindow(title, summary, diagnostics, true));
        }

        public static string BuildRuntimeStatus(FrameworkRuntimeSnapshot? snapshot)
        {
            if (snapshot == null)
            {
                return "PlugHub 框架已加载，但当前还没有可用的运行时状态。";
            }

            var activeView = snapshot.Configuration.ActiveView;
            var activePreset = snapshot.Configuration.ActivePreset;
            return
                "当前加载的是 PlugHub 单工作台。\n\n" +
                "Workspace: " + activeView.Id + " / " + activeView.Name + "\n" +
                "Active preset: " + (activePreset == null ? "(none)" : activePreset.Id) + "\n" +
                "Config: " + FrameworkRuntimeState.ConfigDirectory + "\n" +
                "Modules: " + snapshot.Configuration.EffectiveModules.Modules.Count + "\n" +
                "Features in workspace: " + snapshot.Composition.Features.Count + "\n" +
                "Logs: " + snapshot.Diagnostics.Count + "\n\n" +
                "PlugHub Manager 写入的仓库、插件和布局配置会在下次启动时重新读取；Ribbon 布局和图标变更需要重启 Revit 后重绘。\n" +
                "需要排障时，请在 PlugHub Manager 中打开日志目录或导出日志。";
        }

        private static DataGridTextColumn TextColumn(string propertyName, string header, double width, bool star = false)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(propertyName),
                Width = star ? new DataGridLength(width, DataGridLengthUnitType.Star) : new DataGridLength(width)
            };
        }

        private static List<DiagnosticRow> BuildDiagnosticRows(IEnumerable<DiagnosticMessage> diagnostics)
        {
            var rows = (diagnostics ?? Enumerable.Empty<DiagnosticMessage>())
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

            return rows;
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
