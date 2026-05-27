using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    internal sealed class FrameworkStatusWindow : Window
    {
        public FrameworkStatusWindow(string title, string summary, IEnumerable<DiagnosticMessage> diagnostics)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "PlugHub 状态" : title;
            Width = 760;
            Height = 560;
            MinWidth = 620;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 252));

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            header.Children.Add(new TextBlock
            {
                Text = Title,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(24, 34, 48))
            });
            header.Children.Add(new TextBlock
            {
                Text = summary ?? string.Empty,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 83, 101)),
                LineHeight = 20
            });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var diagnosticsGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = true,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 226, 234)),
                RowHeight = 30,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(250, 252, 255)),
                ItemsSource = BuildDiagnosticRows(diagnostics)
            };
            diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Severity), "级别", 90));
            diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Code), "代码", 130));
            diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Scope), "对象", 170));
            diagnosticsGrid.Columns.Add(TextColumn(nameof(DiagnosticRow.Message), "消息", 1, true));
            Grid.SetRow(diagnosticsGrid, 1);
            root.Children.Add(diagnosticsGrid);

            var closeButton = new Button
            {
                Content = "关闭",
                Width = 88,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            closeButton.Click += (sender, args) => Close();
            Grid.SetRow(closeButton, 2);
            root.Children.Add(closeButton);

            Content = root;
        }

        public static void ShowDialog(string title, string summary, IEnumerable<DiagnosticMessage> diagnostics)
        {
            RevitWindowOwner.ShowDialog(new FrameworkStatusWindow(title, summary, diagnostics));
        }

        public static string BuildRuntimeSummary(FrameworkRuntimeSnapshot? snapshot)
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
                "Diagnostics: " + snapshot.Diagnostics.Count + "\n\n" +
                "模块/功能开关可通过 Ribbon 的「刷新配置」热重载到运行时；Ribbon 结构、图标和按钮大小变更仍需重启 Revit 重绘。";
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
                    Message = "当前没有诊断消息。"
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
