using System;
using System.Collections.Generic;
using System.Linq;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.RibbonEditing
{
    public sealed class RibbonDesignerMapper
    {
        public List<RibbonDesignerNodeRow> FromConfiguration(RibbonConfiguration ribbon, IEnumerable<RibbonDesignerFeatureRow> features)
        {
            ribbon = ribbon ?? new RibbonConfiguration();
            var featureRows = (features ?? new List<RibbonDesignerFeatureRow>()).ToList();
            var featuresById = featureRows
                .Where(feature => !string.IsNullOrWhiteSpace(feature.FeatureId))
                .GroupBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var tab = new RibbonDesignerNodeRow
            {
                NodeType = RibbonDesignerNodeRow.Tab,
                Id = "tab",
                Text = SafeText(ribbon.TabName, "PlugHub"),
                Order = 100
            };

            var configuredPanels = ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>();
            var panels = configuredPanels.Count == 0
                ? CreateDefaultPanels(featureRows)
                : configuredPanels
                    .OrderBy(panel => panel.Order)
                    .ThenBy(panel => SafeText(panel.Name, panel.Id), StringComparer.OrdinalIgnoreCase)
                    .Select(panel => FromPanel(panel, featuresById))
                    .ToList();

            foreach (var panel in panels)
            {
                tab.Children.Add(panel);
            }

            return new List<RibbonDesignerNodeRow> { tab };
        }

        public List<RibbonPanelLayoutConfiguration> ToPanels(IEnumerable<RibbonDesignerNodeRow> tabs)
        {
            return ToPanels(tabs, new List<RibbonDesignerFeatureRow>());
        }

        public List<RibbonPanelLayoutConfiguration> ToPanels(IEnumerable<RibbonDesignerNodeRow> tabs, IEnumerable<RibbonDesignerFeatureRow> features)
        {
            var index = 0;
            var featuresById = (features ?? new List<RibbonDesignerFeatureRow>())
                .Where(feature => !string.IsNullOrWhiteSpace(feature.FeatureId))
                .GroupBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            return (tabs ?? new List<RibbonDesignerNodeRow>())
                .Where(node => IsType(node, RibbonDesignerNodeRow.Tab))
                .SelectMany(tab => tab.Children)
                .Where(panel => IsType(panel, RibbonDesignerNodeRow.Panel))
                .Select(panel =>
                {
                    index++;
                    panel.Order = index * 100;
                    AssignOrders(panel.Children);
                    return ToPanel(panel, featuresById);
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

        private static List<RibbonDesignerNodeRow> CreateDefaultPanels(IEnumerable<RibbonDesignerFeatureRow> features)
        {
            var panels = new List<RibbonDesignerNodeRow>();
            foreach (var featureGroup in (features ?? new List<RibbonDesignerFeatureRow>())
                .Where(feature => feature.Visible && !string.IsNullOrWhiteSpace(feature.FeatureId))
                .OrderBy(feature => DefaultPanelName(feature), StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.Order)
                .ThenBy(feature => SafeText(feature.DisplayName, feature.Name), StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .GroupBy(DefaultPanelKey, StringComparer.OrdinalIgnoreCase))
            {
                var firstFeature = featureGroup.First();
                var panel = new RibbonDesignerNodeRow
                {
                    NodeType = RibbonDesignerNodeRow.Panel,
                    Id = SafeId(firstFeature.Group, DefaultPanelName(firstFeature)),
                    Text = DefaultPanelName(firstFeature),
                    Order = (panels.Count + 1) * 100,
                    RequiresRestart = true,
                    StatusText = "需重启"
                };

                var index = 0;
                foreach (var feature in featureGroup
                    .OrderBy(feature => feature.Order)
                    .ThenBy(feature => SafeText(feature.DisplayName, feature.Name), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase))
                {
                    index++;
                    panel.Children.Add(new RibbonDesignerNodeRow
                    {
                        NodeType = RibbonDesignerNodeRow.PushButton,
                        Id = SafeId(feature.FeatureId, feature.Name),
                        Text = SafeText(feature.DisplayName, feature.Name),
                        FeatureId = feature.FeatureId,
                        Size = NormalizeButtonSize(feature.ButtonSize),
                        IconPath = feature.IconPath,
                        Order = index * 100,
                        RequiresRestart = true,
                        StatusText = "需重启"
                    });
                }

                if (panel.Children.Count > 0)
                {
                    panels.Add(panel);
                }
            }

            return panels;
        }

        private static string DefaultPanelKey(RibbonDesignerFeatureRow feature)
        {
            return DefaultPanelName(feature).Trim();
        }

        private static string DefaultPanelName(RibbonDesignerFeatureRow feature)
        {
            return SafeText(
                feature == null ? string.Empty : feature.GroupDisplayText,
                SafeText(
                    feature == null ? string.Empty : feature.ModuleName,
                    SafeText(
                        feature == null ? string.Empty : feature.Group,
                        SafeText(feature == null ? string.Empty : feature.ModuleId, "默认工具"))));
        }

        private static RibbonDesignerNodeRow FromPanel(RibbonPanelLayoutConfiguration panel, IReadOnlyDictionary<string, RibbonDesignerFeatureRow> featuresById)
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
                .Select(item => FromItem(item, featuresById)))
            {
                row.Children.Add(child);
            }

            return row;
        }

        private static RibbonDesignerNodeRow FromItem(RibbonItemLayoutConfiguration item, IReadOnlyDictionary<string, RibbonDesignerFeatureRow> featuresById)
        {
            featuresById.TryGetValue(item.FeatureId ?? string.Empty, out var feature);
            var row = new RibbonDesignerNodeRow
            {
                NodeType = string.IsNullOrWhiteSpace(item.Type) ? RibbonDesignerNodeRow.PushButton : item.Type,
                Id = item.Id ?? string.Empty,
                Text = SafeText(item.TextOverride, SafeText(item.Text, item.Id)),
                FeatureId = item.FeatureId ?? string.Empty,
                DefaultFeatureId = item.DefaultFeatureId ?? string.Empty,
                Size = NormalizeButtonSize(item.Size),
                IconPath = IconPathForDisplay(item, feature),
                Order = item.Order,
                RequiresRestart = true,
                StatusText = "需重启"
            };

            foreach (var child in (item.Items ?? new List<RibbonItemLayoutConfiguration>())
                .OrderBy(child => child.Order)
                .ThenBy(child => SafeText(child.Text, child.Id), StringComparer.OrdinalIgnoreCase)
                .Select(child => FromItem(child, featuresById)))
            {
                row.Children.Add(child);
            }

            return row;
        }

        private static string IconPathForDisplay(RibbonItemLayoutConfiguration item, RibbonDesignerFeatureRow? feature)
        {
            return SafeText(
                item == null ? string.Empty : item.IconPathOverride,
                SafeText(
                    item == null ? string.Empty : item.IconPath,
                    feature == null ? string.Empty : feature.IconPath));
        }

        private static RibbonPanelLayoutConfiguration ToPanel(RibbonDesignerNodeRow panel, IReadOnlyDictionary<string, RibbonDesignerFeatureRow> featuresById)
        {
            return new RibbonPanelLayoutConfiguration
            {
                Id = panel.Id ?? string.Empty,
                Name = panel.Text ?? string.Empty,
                Order = panel.Order,
                Items = panel.Children.Select(child => ToItem(child, featuresById)).ToList()
            };
        }

        private static RibbonItemLayoutConfiguration ToItem(RibbonDesignerNodeRow item, IReadOnlyDictionary<string, RibbonDesignerFeatureRow> featuresById)
        {
            var iconPath = IconPathForSave(item, featuresById);
            return new RibbonItemLayoutConfiguration
            {
                Type = item.NodeType ?? string.Empty,
                Id = item.Id ?? string.Empty,
                Text = item.Text ?? string.Empty,
                FeatureId = item.FeatureId ?? string.Empty,
                DefaultFeatureId = item.DefaultFeatureId ?? string.Empty,
                Size = item.Size ?? string.Empty,
                IconPath = iconPath,
                TextOverride = item.Text ?? string.Empty,
                IconPathOverride = IsType(item, RibbonDesignerNodeRow.PushButton) ? iconPath : string.Empty,
                Order = item.Order,
                Items = item.Children.Select(child => ToItem(child, featuresById)).ToList()
            };
        }

        private static string IconPathForSave(RibbonDesignerNodeRow item, IReadOnlyDictionary<string, RibbonDesignerFeatureRow> featuresById)
        {
            var iconPath = item.IconPath ?? string.Empty;
            if (!IsType(item, RibbonDesignerNodeRow.PushButton) || string.IsNullOrWhiteSpace(item.FeatureId))
            {
                return iconPath;
            }

            return featuresById.TryGetValue(item.FeatureId, out var feature)
                && SameIconPath(iconPath, feature.IconPath)
                ? string.Empty
                : iconPath;
        }

        private static bool SameIconPath(string left, string right)
        {
            return string.Equals(NormalizeIconPath(left), NormalizeIconPath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeIconPath(string value)
        {
            return (value ?? string.Empty).Trim().Replace('\\', '/');
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
