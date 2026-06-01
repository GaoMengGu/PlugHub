using System;
using System.Collections.Generic;
using System.Linq;
using PlugHub.Framework.Configuration;
using PlugHub.Revit2020.Settings.Rows;

namespace PlugHub.Revit2020.Settings.RibbonDesigner
{
    internal sealed class RibbonDesignerMapper
    {
        public List<RibbonDesignerNodeRow> FromConfiguration(RibbonConfiguration ribbon, IEnumerable<FeatureRow> features)
        {
            ribbon = ribbon ?? new RibbonConfiguration();
            var tab = new RibbonDesignerNodeRow
            {
                NodeType = RibbonDesignerNodeRow.Tab,
                Id = "tab",
                Text = SafeText(ribbon.TabName, "PlugHub"),
                Order = 100
            };

            var configuredPanels = ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>();
            var panels = configuredPanels.Count == 0
                ? CreateDefaultPanels(features)
                : configuredPanels
                    .OrderBy(panel => panel.Order)
                    .ThenBy(panel => SafeText(panel.Name, panel.Id), StringComparer.OrdinalIgnoreCase)
                    .Select(FromPanel)
                    .ToList();

            foreach (var panel in panels)
            {
                tab.Children.Add(panel);
            }

            return new List<RibbonDesignerNodeRow> { tab };
        }

        public List<RibbonPanelLayoutConfiguration> ToPanels(IEnumerable<RibbonDesignerNodeRow> tabs)
        {
            var index = 0;
            return (tabs ?? new List<RibbonDesignerNodeRow>())
                .Where(node => IsType(node, RibbonDesignerNodeRow.Tab))
                .SelectMany(tab => tab.Children)
                .Where(panel => IsType(panel, RibbonDesignerNodeRow.Panel))
                .Select(panel =>
                {
                    index++;
                    panel.Order = index * 100;
                    AssignOrders(panel.Children);
                    return ToPanel(panel);
                })
                .ToList();
        }

        public static List<RibbonDesignerNodeRow> CloneTabs(IEnumerable<RibbonDesignerNodeRow> tabs)
        {
            return (tabs ?? new List<RibbonDesignerNodeRow>())
                .Select(CloneNode)
                .ToList();
        }

        public static RibbonDesignerNodeRow CreateFeatureNode(RibbonDesignerFeatureRow feature, int order)
        {
            return new RibbonDesignerNodeRow
            {
                NodeType = RibbonDesignerNodeRow.PushButton,
                Id = SafeId(feature.FeatureId, feature.DisplayName),
                Text = SafeText(feature.DisplayName, feature.FeatureName),
                FeatureId = feature.FeatureId ?? string.Empty,
                Size = NormalizeButtonSize(feature.ButtonSize),
                IconPath = feature.IconPath ?? string.Empty,
                Order = order,
                RequiresRestart = true,
                StatusText = "需重启"
            };
        }

        public static RibbonDesignerNodeRow CreateContainerNode(string nodeType, string text, int order)
        {
            return new RibbonDesignerNodeRow
            {
                NodeType = nodeType ?? string.Empty,
                Id = SafeId(nodeType, text),
                Text = SafeText(text, NodeTypeDisplayName(nodeType)),
                Size = "large",
                Order = order,
                RequiresRestart = true,
                StatusText = "需重启"
            };
        }

        public static bool IsType(RibbonDesignerNodeRow node, string nodeType)
        {
            return node != null && string.Equals(node.NodeType, nodeType, StringComparison.OrdinalIgnoreCase);
        }

        private static List<RibbonDesignerNodeRow> CreateDefaultPanels(IEnumerable<FeatureRow> features)
        {
            var defaultPanel = new RibbonDesignerNodeRow
            {
                NodeType = RibbonDesignerNodeRow.Panel,
                Id = "default",
                Text = "默认",
                Order = 100,
                RequiresRestart = true,
                StatusText = "需重启"
            };

            var index = 0;
            foreach (var feature in (features ?? new List<FeatureRow>())
                .Where(feature => feature.Visible && !string.IsNullOrWhiteSpace(feature.FeatureId))
                .OrderBy(feature => SafeText(feature.ModuleName, feature.ModuleId), StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.Order)
                .ThenBy(feature => SafeText(feature.DisplayName, feature.Name), StringComparer.OrdinalIgnoreCase))
            {
                index++;
                defaultPanel.Children.Add(new RibbonDesignerNodeRow
                {
                    NodeType = RibbonDesignerNodeRow.PushButton,
                    Id = SafeId(feature.FeatureId, feature.Name),
                    Text = SafeText(feature.DisplayName, feature.Name),
                    FeatureId = feature.FeatureId,
                    Size = "large",
                    IconPath = feature.IconPath,
                    Order = index * 100,
                    RequiresRestart = true,
                    StatusText = "需重启"
                });
            }

            return defaultPanel.Children.Count == 0
                ? new List<RibbonDesignerNodeRow>()
                : new List<RibbonDesignerNodeRow> { defaultPanel };
        }

        private static RibbonDesignerNodeRow FromPanel(RibbonPanelLayoutConfiguration panel)
        {
            var row = new RibbonDesignerNodeRow
            {
                NodeType = RibbonDesignerNodeRow.Panel,
                Id = panel.Id ?? string.Empty,
                Text = SafeText(panel.Name, panel.Id),
                Order = panel.Order,
                RequiresRestart = true,
                StatusText = "需重启"
            };

            foreach (var child in (panel.Items ?? new List<RibbonItemLayoutConfiguration>())
                .OrderBy(item => item.Order)
                .ThenBy(item => SafeText(item.Text, item.Id), StringComparer.OrdinalIgnoreCase)
                .Select(FromItem))
            {
                row.Children.Add(child);
            }

            return row;
        }

        private static RibbonDesignerNodeRow FromItem(RibbonItemLayoutConfiguration item)
        {
            var row = new RibbonDesignerNodeRow
            {
                NodeType = string.IsNullOrWhiteSpace(item.Type) ? RibbonDesignerNodeRow.PushButton : item.Type,
                Id = item.Id ?? string.Empty,
                Text = SafeText(item.TextOverride, SafeText(item.Text, item.Id)),
                FeatureId = item.FeatureId ?? string.Empty,
                DefaultFeatureId = item.DefaultFeatureId ?? string.Empty,
                Size = NormalizeButtonSize(item.Size),
                IconPath = SafeText(item.IconPathOverride, item.IconPath),
                Order = item.Order,
                RequiresRestart = true,
                StatusText = "需重启"
            };

            foreach (var child in (item.Items ?? new List<RibbonItemLayoutConfiguration>())
                .OrderBy(child => child.Order)
                .ThenBy(child => SafeText(child.Text, child.Id), StringComparer.OrdinalIgnoreCase)
                .Select(FromItem))
            {
                row.Children.Add(child);
            }

            return row;
        }

        private static RibbonPanelLayoutConfiguration ToPanel(RibbonDesignerNodeRow panel)
        {
            return new RibbonPanelLayoutConfiguration
            {
                Id = panel.Id ?? string.Empty,
                Name = panel.Text ?? string.Empty,
                Order = panel.Order,
                Items = panel.Children.Select(ToItem).ToList()
            };
        }

        private static RibbonItemLayoutConfiguration ToItem(RibbonDesignerNodeRow item)
        {
            return new RibbonItemLayoutConfiguration
            {
                Type = item.NodeType ?? string.Empty,
                Id = item.Id ?? string.Empty,
                Text = item.Text ?? string.Empty,
                FeatureId = item.FeatureId ?? string.Empty,
                DefaultFeatureId = item.DefaultFeatureId ?? string.Empty,
                Size = item.Size ?? string.Empty,
                IconPath = item.IconPath ?? string.Empty,
                TextOverride = item.Text ?? string.Empty,
                IconPathOverride = item.IconPath ?? string.Empty,
                Order = item.Order,
                Items = item.Children.Select(ToItem).ToList()
            };
        }

        private static RibbonDesignerNodeRow CloneNode(RibbonDesignerNodeRow source)
        {
            var clone = new RibbonDesignerNodeRow
            {
                NodeType = source.NodeType,
                Id = source.Id,
                Text = source.Text,
                FeatureId = source.FeatureId,
                Size = source.Size,
                IconPath = source.IconPath,
                DefaultFeatureId = source.DefaultFeatureId,
                Order = source.Order,
                IsVisible = source.IsVisible,
                RequiresRestart = source.RequiresRestart,
                StatusText = source.StatusText
            };

            foreach (var child in source.Children)
            {
                clone.Children.Add(CloneNode(child));
            }

            return clone;
        }

        private static void AssignOrders(IEnumerable<RibbonDesignerNodeRow> rows)
        {
            var index = 0;
            foreach (var row in rows ?? new List<RibbonDesignerNodeRow>())
            {
                index++;
                row.Order = index * 100;
                AssignOrders(row.Children);
            }
        }

        private static string NormalizeButtonSize(string? value)
        {
            return string.Equals(value, "small", StringComparison.OrdinalIgnoreCase) ? "small" : "large";
        }

        private static string NodeTypeDisplayName(string? nodeType)
        {
            if (string.Equals(nodeType, RibbonDesignerNodeRow.PulldownButton, StringComparison.OrdinalIgnoreCase)) return "下拉按钮";
            if (string.Equals(nodeType, RibbonDesignerNodeRow.SplitButton, StringComparison.OrdinalIgnoreCase)) return "拆分按钮";
            if (string.Equals(nodeType, RibbonDesignerNodeRow.Stack, StringComparison.OrdinalIgnoreCase)) return "堆叠";
            return "功能按钮";
        }

        private static string SafeText(string? value, string? fallback)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value!.Trim();
            }

            return (fallback ?? string.Empty).Trim();
        }

        private static string SafeId(string? value, string? fallback)
        {
            var source = SafeText(value, fallback);
            return string.IsNullOrWhiteSpace(source) ? "ribbon-item" : source;
        }
    }
}
