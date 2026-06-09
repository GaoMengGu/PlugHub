using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PlugHub.Wpf;

namespace PlugHub.Manager.Maintenance
{
    internal sealed class ManagerMaintenanceRunner
    {
        private const string UpdateCompletedMessage = "PlugHub was updated successfully.";

        public int Run(ManagerMaintenanceArguments args)
        {
            var logger = new ManagerMaintenanceLogger(args.InstallDirectory);
            try
            {
                if (args.Mode == ManagerMaintenanceMode.Update)
                {
                    new ManagerFrameworkUpdater(logger).Run(args);
                    ShowThemedUpdateCompletedDialog();
                    return 0;
                }

                if (args.Mode == ManagerMaintenanceMode.Uninstall)
                {
                    return RunUninstall(args, logger);
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.Error("PlugHub Manager maintenance failed.", ex);
                if (args.Mode == ManagerMaintenanceMode.Update)
                {
                    MessageBox.Show(ex.Message, "PlugHub Manager - Update", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (args.Mode == ManagerMaintenanceMode.Uninstall)
                {
                    MessageBox.Show(ex.Message, "PlugHub Manager - Uninstall", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return 1;
            }
        }

        private static void ShowThemedUpdateCompletedDialog()
        {
            EnsureWpfApplication();

            var theme = RevitUiTheme.Current;
            var dialog = new Window
            {
                Title = "PlugHub Manager - Update",
                Width = 420,
                Height = 190,
                MinWidth = 380,
                MinHeight = 160,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                SizeToContent = SizeToContent.Manual,
                Tag = UpdateCompletedMessage
            };
            RevitUiTheme.Apply(dialog);

            var root = new Grid
            {
                Margin = new Thickness(16),
                Background = theme.WindowBackground
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new DockPanel { LastChildFill = true };
            var statusMark = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = theme.SuccessBrush,
                Margin = new Thickness(0, 0, 10, 0),
                Child = new Viewbox
                {
                    Width = 13,
                    Height = 13,
                    Child = new Path
                    {
                        Data = Geometry.Parse("M1,7 L5,11 L12,2"),
                        Stroke = theme.AccentForegroundBrush,
                        StrokeThickness = 2,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeLineJoin = PenLineJoin.Round
                    }
                }
            };
            DockPanel.SetDock(statusMark, Dock.Left);
            header.Children.Add(statusMark);
            header.Children.Add(new TextBlock
            {
                Text = "更新完成",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = theme.TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            root.Children.Add(header);

            var message = new TextBlock
            {
                Text = "PlugHub 已更新完成。关闭并重新打开 Revit 后将使用新版本。",
                Margin = new Thickness(38, 12, 0, 0),
                Foreground = theme.MutedTextBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };
            Grid.SetRow(message, 1);
            root.Children.Add(message);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var ok = new Button
            {
                Content = "确定",
                MinWidth = 78,
                Height = 28
            };
            ok.Click += (sender, args) => dialog.DialogResult = true;
            buttons.Children.Add(ok);
            Grid.SetRow(buttons, 3);
            root.Children.Add(buttons);

            dialog.Content = root;
            dialog.ShowDialog();
        }

        private static void EnsureWpfApplication()
        {
            if (Application.Current != null) return;
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }

        private static int RunUninstall(ManagerMaintenanceArguments args, ManagerMaintenanceLogger logger)
        {
            var confirmation = MessageBox.Show(
                "Uninstall PlugHub from this computer?\n\nInstall directory: " + args.InstallDirectory,
                "PlugHub Manager - Uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
            {
                return 0;
            }

            WaitForProcesses(args.WaitProcessIds, logger);
            new ManagerUninstaller(logger).Run(args.InstallDirectory);
            MessageBox.Show("PlugHub was uninstalled successfully.", "PlugHub Manager - Uninstall", MessageBoxButton.OK, MessageBoxImage.Information);
            return 0;
        }

        private static void WaitForProcesses(System.Collections.Generic.IEnumerable<int> processIds, ManagerMaintenanceLogger logger)
        {
            var currentProcessId = Process.GetCurrentProcess().Id;
            foreach (var processId in (processIds ?? Enumerable.Empty<int>()).Distinct())
            {
                if (processId <= 0 || processId == currentProcessId) continue;
                try
                {
                    var process = Process.GetProcessById(processId);
                    logger.Info("Waiting for process to exit before uninstall: " + processId);
                    process.WaitForExit();
                }
                catch (ArgumentException)
                {
                    logger.Info("Process already exited before uninstall: " + processId);
                }
            }
        }
    }
}
