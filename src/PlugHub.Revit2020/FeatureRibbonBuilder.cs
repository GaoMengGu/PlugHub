using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using PlugHub.Framework.Composition;
using PlugHub.Framework.Configuration;

namespace PlugHub.Revit2020
{
    internal sealed class FeatureRibbonBuilder
    {
        private readonly string _assemblyPath;
        private readonly string _baseDirectory;

        public FeatureRibbonBuilder(string assemblyPath, string baseDirectory)
        {
            _assemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        }

        public void Build(UIControlledApplication application, ViewConfiguration view, FeatureViewCompositionResult composition)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (composition == null) throw new ArgumentNullException(nameof(composition));

            var tabName = SafeDisplayName(view.Ribbon?.TabName, "PlugHub");
            var fallbackPanelName = SafeDisplayName(view.Ribbon?.FallbackPanelName, "Framework");
            EnsureRibbonTab(application, tabName);
            AddFrameworkButtons(GetOrCreatePanel(application, tabName, "框架"));

            if (!composition.Features.Any())
            {
                return;
            }

            foreach (var group in composition.Features.GroupBy(f => new { f.GroupId, f.GroupName, f.GroupOrder }).OrderBy(group => group.Key.GroupOrder).ThenBy(group => group.Key.GroupName))
            {
                var panelName = SafeDisplayName(group.Key.GroupName, fallbackPanelName);
                var panel = GetOrCreatePanel(application, tabName, panelName);
                AddFeatureButtons(panel, group);
            }
        }

        private void AddFrameworkButtons(RibbonPanel panel)
        {
            AddFrameworkButton(
                panel,
                "PlugHub_Framework_Settings",
                "设置",
                typeof(FrameworkSettingsCommand),
                "打开 PlugHub WPF 设置窗口。",
                "用于开关模块、重命名模块和功能，以及调整功能按钮的显示、面板、顺序、图标和大小。保存只写入配置文件；运行时更新请点击「刷新配置」。");
            AddFrameworkButton(
                panel,
                "PlugHub_Framework_Refresh",
                "刷新配置",
                typeof(FrameworkRefreshCommand),
                "重新读取 PlugHub 配置和模块来源。",
                "在 Revit 命令上下文中刷新模块/功能开关、模块来源和执行拦截。Ribbon 结构、图标和按钮大小变更仍需重启 Revit 重绘。");
            AddFrameworkButton(
                panel,
                "PlugHub_Framework_Status",
                "状态",
                typeof(FrameworkFeatureCommand),
                "查看 PlugHub 框架运行状态。",
                "以 WPF 窗口显示当前工作台、模块、功能和诊断数量。诊断明细请在设置窗口的诊断页查看。");
        }

        private void AddFrameworkButton(RibbonPanel panel, string buttonName, string text, Type commandType, string tooltip, string longDescription)
        {
            if (panel.GetItems().Any(item => string.Equals(item.Name, buttonName, StringComparison.OrdinalIgnoreCase))) return;

            var data = new PushButtonData(
                buttonName,
                text,
                _assemblyPath,
                commandType.FullName);

            data.ToolTip = tooltip;
            data.LongDescription = longDescription;
            panel.AddItem(data);
        }

        private void AddFeatureButtons(RibbonPanel panel, IEnumerable<FeatureViewModel> features)
        {
            var smallBuffer = new List<PushButtonData>();
            foreach (var feature in features)
            {
                var data = CreateFeatureButtonData(feature);
                if (IsSmall(feature.ButtonSize))
                {
                    smallBuffer.Add(data);
                    if (smallBuffer.Count == 3)
                    {
                        AddStackedButtons(panel, smallBuffer);
                        smallBuffer.Clear();
                    }

                    continue;
                }

                AddStackedButtons(panel, smallBuffer);
                smallBuffer.Clear();
                AddFeatureButton(panel, data);
            }

            AddStackedButtons(panel, smallBuffer);
        }

        private void AddFeatureButton(RibbonPanel panel, FeatureViewModel feature)
        {
            AddFeatureButton(panel, CreateFeatureButtonData(feature));
        }

        private void AddFeatureButton(RibbonPanel panel, PushButtonData data)
        {
            if (panel.GetItems().Any(item => string.Equals(item.Name, data.Name, StringComparison.OrdinalIgnoreCase))) return;
            panel.AddItem(data);
        }

        private PushButtonData CreateFeatureButtonData(FeatureViewModel feature)
        {
            var buttonName = SafeInternalName(feature.FeatureId);
            var commandTarget = ResolveCommandTarget(feature);
            var data = new PushButtonData(
                buttonName,
                SafeDisplayName(feature.DisplayName, "Feature"),
                commandTarget.AssemblyPath,
                commandTarget.TypeName);

            data.ToolTip = BuildToolTip(feature);
            data.LongDescription = feature.Description;
            var icon = LoadFeatureIcon(feature.IconPath);
            if (icon != null)
            {
                data.Image = icon;
                data.LargeImage = icon;
            }

            return data;
        }

        private static void AddStackedButtons(RibbonPanel panel, List<PushButtonData> data)
        {
            if (data.Count == 0) return;
            if (data.Count == 1)
            {
                if (!panel.GetItems().Any(item => string.Equals(item.Name, data[0].Name, StringComparison.OrdinalIgnoreCase)))
                {
                    panel.AddItem(data[0]);
                }

                return;
            }

            var filtered = data
                .Where(item => !panel.GetItems().Any(existing => string.Equals(existing.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (filtered.Count == 0) return;
            if (filtered.Count == 1)
            {
                panel.AddItem(filtered[0]);
                return;
            }

            if (filtered.Count == 2)
            {
                panel.AddStackedItems(filtered[0], filtered[1]);
                return;
            }

            panel.AddStackedItems(filtered[0], filtered[1], filtered[2]);
        }

        private static void EnsureRibbonTab(UIControlledApplication application, string tabName)
        {
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                // Revit throws when a tab already exists. Existing tabs are acceptable during startup.
            }
        }

        private static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string tabName, string panelName)
        {
            var existing = application.GetRibbonPanels(tabName).FirstOrDefault(panel => string.Equals(panel.Name, panelName, StringComparison.OrdinalIgnoreCase));
            return existing ?? application.CreateRibbonPanel(tabName, panelName);
        }

        private static string SafeDisplayName(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value!.Trim();
        }

        private static string SafeInternalName(string value)
        {
            var builder = new StringBuilder("PlugHub_");
            foreach (var ch in value ?? string.Empty)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return builder.Length == "PlugHub_".Length ? "PlugHub_Feature" : builder.ToString();
        }

        private static string BuildToolTip(FeatureViewModel feature)
        {
            var lines = new List<string>
            {
                feature.DisplayName,
                feature.GroupName,
                "Module: " + feature.ModuleId,
                "Feature: " + feature.FeatureId
            };

            if (!string.IsNullOrWhiteSpace(feature.Category))
            {
                lines.Add("Category: " + feature.Category);
            }

            if (!string.IsNullOrWhiteSpace(feature.CommandKey))
            {
                lines.Add("Command: " + feature.CommandKey);
            }

            if (!string.IsNullOrWhiteSpace(feature.CommandType))
            {
                lines.Add("Command type: " + feature.CommandType);
            }

            lines.Add("Button size: " + SafeDisplayName(feature.ButtonSize, "large"));

            if (!string.IsNullOrWhiteSpace(feature.Description))
            {
                lines.Add(feature.Description);
            }

            return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private static bool IsSmall(string value)
        {
            return string.Equals(value, "small", StringComparison.OrdinalIgnoreCase);
        }

        private CommandTarget ResolveCommandTarget(FeatureViewModel feature)
        {
            if (feature == null) throw new ArgumentNullException(nameof(feature));

            if (!string.IsNullOrWhiteSpace(feature.CommandType))
            {
                var assemblyPath = ResolveAssemblyPath(feature.CommandAssembly);
                if (File.Exists(assemblyPath))
                {
                    return new CommandTarget(assemblyPath, feature.CommandType);
                }
            }

            return new CommandTarget(_assemblyPath, typeof(FrameworkFeatureCommand).FullName);
        }

        private string ResolveAssemblyPath(string configuredAssembly)
        {
            if (string.IsNullOrWhiteSpace(configuredAssembly))
            {
                return _assemblyPath;
            }

            return Path.IsPathRooted(configuredAssembly)
                ? configuredAssembly
                : Path.GetFullPath(Path.Combine(_baseDirectory, configuredAssembly));
        }

        private ImageSource? LoadFeatureIcon(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath)) return null;

            var resolvedPath = Path.IsPathRooted(iconPath)
                ? iconPath
                : Path.GetFullPath(Path.Combine(_baseDirectory, iconPath));
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

        private sealed class CommandTarget
        {
            public CommandTarget(string assemblyPath, string? typeName)
            {
                AssemblyPath = assemblyPath ?? throw new ArgumentNullException(nameof(assemblyPath));
                TypeName = typeName ?? string.Empty;
            }

            public string AssemblyPath { get; }
            public string TypeName { get; }
        }
    }
}
