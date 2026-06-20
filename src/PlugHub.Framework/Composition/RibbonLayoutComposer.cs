using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Composition
{
    public sealed class RibbonLayoutComposer
    {
        public RibbonLayoutViewModel Compose(ViewConfiguration view, IReadOnlyList<FeatureViewModel> features)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));

            IReadOnlyList<FeatureViewModel> visibleFeatures = features ?? new List<FeatureViewModel>();
            var ribbon = view.Ribbon ?? new RibbonConfiguration();
            var configuredPanels = ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>();

            return configuredPanels.Any()
                ? BuildConfiguredLayout(ribbon, visibleFeatures)
                : BuildLegacyLayout(view, visibleFeatures);
        }

        private static RibbonLayoutViewModel BuildLegacyLayout(ViewConfiguration view, IReadOnlyList<FeatureViewModel> features)
        {
            var fallbackPanelName = view.Ribbon == null ? "Framework" : view.Ribbon.FallbackPanelName;
            var panels = (features ?? new List<FeatureViewModel>())
                .GroupBy(feature => LegacyPanelDisplayKey(feature, fallbackPanelName), StringComparer.OrdinalIgnoreCase)
                .Select(group => BuildLegacyPanel(group.Key, group))
                .OrderBy(panel => panel.Order)
                .ThenBy(panel => panel.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var clickable = panels.SelectMany(panel => panel.Items).SelectMany(item => item.ClickableFeatures()).ToList();
            return new RibbonLayoutViewModel(panels, clickable);
        }

        private static RibbonPanelViewModel BuildLegacyPanel(string panelName, IEnumerable<FeatureViewModel> features)
        {
            var orderedFeatures = (features ?? new List<FeatureViewModel>())
                .OrderBy(feature => feature.DisplayOrder)
                .ThenBy(feature => feature.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var firstFeature = orderedFeatures.FirstOrDefault();
            var order = orderedFeatures.Any() ? orderedFeatures.Min(feature => feature.GroupOrder) : 0;

            return new RibbonPanelViewModel(
                SafeId(firstFeature == null ? string.Empty : firstFeature.GroupId, panelName),
                panelName,
                order,
                orderedFeatures
                    .Select(feature => PushItem(feature, feature.ButtonSize, string.Empty, string.Empty))
                    .ToList());
        }

        private static string LegacyPanelDisplayKey(FeatureViewModel feature, string? fallbackPanelName)
        {
            return SafeText(feature == null ? string.Empty : feature.GroupName, fallbackPanelName);
        }

        private static RibbonLayoutViewModel BuildConfiguredLayout(RibbonConfiguration ribbon, IReadOnlyList<FeatureViewModel> features)
        {
            IReadOnlyList<FeatureViewModel> sourceFeatures = features ?? new List<FeatureViewModel>();
            var featuresById = sourceFeatures
                .Where(feature => !string.IsNullOrWhiteSpace(feature.FeatureId))
                .GroupBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var placedFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var panels = MergeConfiguredPanelsByDisplayName(
                ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>(),
                featuresById,
                placedFeatureIds);

            AppendUnplacedFeatures(ribbon, panels, sourceFeatures, placedFeatureIds);

            var clickable = panels.SelectMany(panel => panel.Items).SelectMany(item => item.ClickableFeatures()).ToList();
            return new RibbonLayoutViewModel(panels, clickable);
        }

        private static List<RibbonPanelViewModel> MergeConfiguredPanelsByDisplayName(
            IEnumerable<RibbonPanelLayoutConfiguration> panels,
            IReadOnlyDictionary<string, FeatureViewModel> featuresById,
            ISet<string> placedFeatureIds)
        {
            return (panels ?? new List<RibbonPanelLayoutConfiguration>())
                .OrderBy(panel => panel.Order)
                .ThenBy(panel => SafeText(panel.Name, panel.Id), StringComparer.OrdinalIgnoreCase)
                .GroupBy(panel => SafeText(panel.Name, panel.Id), StringComparer.OrdinalIgnoreCase)
                .Select(group => BuildConfiguredPanel(group.Key, group, featuresById, placedFeatureIds))
                .OrderBy(panel => panel.Order)
                .ThenBy(panel => panel.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static RibbonPanelViewModel BuildConfiguredPanel(
            string panelName,
            IEnumerable<RibbonPanelLayoutConfiguration> panels,
            IReadOnlyDictionary<string, FeatureViewModel> featuresById,
            ISet<string> placedFeatureIds)
        {
            var orderedPanels = (panels ?? new List<RibbonPanelLayoutConfiguration>())
                .OrderBy(panel => panel.Order)
                .ThenBy(panel => SafeText(panel.Name, panel.Id), StringComparer.OrdinalIgnoreCase)
                .ThenBy(panel => panel.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var firstPanel = orderedPanels.FirstOrDefault();
            var order = orderedPanels.Any() ? orderedPanels.Min(panel => panel.Order) : 0;
            var items = orderedPanels
                .SelectMany(panel => panel.Items ?? new List<RibbonItemLayoutConfiguration>())
                .ToList();

            return new RibbonPanelViewModel(
                SafeId(firstPanel == null ? string.Empty : firstPanel.Id, panelName),
                panelName,
                order,
                BuildConfiguredItems(string.Empty, items, featuresById, placedFeatureIds));
        }

        private static List<RibbonItemViewModel> BuildConfiguredItems(
            string parentType,
            IEnumerable<RibbonItemLayoutConfiguration>? items,
            IReadOnlyDictionary<string, FeatureViewModel> featuresById,
            ISet<string> placedFeatureIds)
        {
            var orderedItems = (items ?? new List<RibbonItemLayoutConfiguration>())
                .OrderBy(item => item.Order)
                .ThenBy(item => SafeText(item.Text, item.Id), StringComparer.OrdinalIgnoreCase)
                .Where(item => CanContainItem(parentType, item));

            if (IsRibbonItemType(parentType, RibbonItemViewModel.Stack))
            {
                orderedItems = orderedItems.Take(3);
            }

            return orderedItems
                .Select(item => BuildConfiguredItem(item, featuresById, placedFeatureIds))
                .Where(item => item != null)
                .Cast<RibbonItemViewModel>()
                .ToList();
        }

        private static RibbonItemViewModel? BuildConfiguredItem(
            RibbonItemLayoutConfiguration item,
            IReadOnlyDictionary<string, FeatureViewModel> featuresById,
            ISet<string> placedFeatureIds)
        {
            var type = NormalizedItemType(item.Type);
            if (IsRibbonItemType(type, RibbonItemViewModel.PushButton))
            {
                if (!featuresById.TryGetValue(item.FeatureId ?? string.Empty, out var feature)) return null;
                if (!placedFeatureIds.Add(feature.FeatureId)) return null;
                return PushItem(feature, item.Size, item.TextOverride, item.IconPathOverride);
            }

            if (!IsContainerType(type)) return null;

            var children = BuildConfiguredItems(type, item.Items, featuresById, placedFeatureIds);
            return new RibbonItemViewModel(
                type,
                SafeId(item.Id, item.Text),
                SafeText(item.Text, item.Id),
                item.IconPath,
                item.Size,
                null,
                item.DefaultFeatureId,
                OrderDefaultFeatureFirst(type, item.DefaultFeatureId, children));
        }

        private static bool CanContainItem(string parentType, RibbonItemLayoutConfiguration item)
        {
            var itemType = NormalizedItemType(item == null ? string.Empty : item.Type);
            if (IsRibbonItemType(itemType, RibbonItemViewModel.PushButton))
            {
                return true;
            }

            if (!IsContainerType(itemType))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(parentType))
            {
                return true;
            }

            if (IsRibbonItemType(parentType, RibbonItemViewModel.Stack))
            {
                return IsRibbonItemType(itemType, RibbonItemViewModel.PulldownButton)
                    || IsRibbonItemType(itemType, RibbonItemViewModel.SplitButton);
            }

            return false;
        }

        private static bool IsContainerType(string type)
        {
            return IsRibbonItemType(type, RibbonItemViewModel.PulldownButton)
                || IsRibbonItemType(type, RibbonItemViewModel.SplitButton)
                || IsRibbonItemType(type, RibbonItemViewModel.Stack);
        }

        private static string NormalizedItemType(string? type)
        {
            return string.IsNullOrWhiteSpace(type) ? RibbonItemViewModel.PushButton : type!.Trim();
        }

        private static bool IsRibbonItemType(string? actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static List<RibbonItemViewModel> OrderDefaultFeatureFirst(string type, string? defaultFeatureId, List<RibbonItemViewModel> children)
        {
            if (!string.Equals(type, RibbonItemViewModel.SplitButton, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(defaultFeatureId))
            {
                return children;
            }

            var defaultChild = children.FirstOrDefault(child => string.Equals(child.FeatureId, defaultFeatureId, StringComparison.OrdinalIgnoreCase));
            if (defaultChild == null) return children;

            return new[] { defaultChild }
                .Concat(children.Where(child => !ReferenceEquals(child, defaultChild)))
                .ToList();
        }

        private static void AppendUnplacedFeatures(
            RibbonConfiguration ribbon,
            List<RibbonPanelViewModel> panels,
            IReadOnlyList<FeatureViewModel> features,
            ISet<string> placedFeatureIds)
        {
            var unplaced = (features ?? new List<FeatureViewModel>())
                .Where(feature => !string.IsNullOrWhiteSpace(feature.FeatureId))
                .Where(feature => !placedFeatureIds.Contains(feature.FeatureId))
                .OrderBy(feature => feature.GroupOrder)
                .ThenBy(feature => feature.DisplayOrder)
                .ThenBy(feature => feature.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!unplaced.Any()) return;

            foreach (var group in unplaced.GroupBy(feature => SafeText(feature.GroupName, ribbon.FallbackPanelName), StringComparer.OrdinalIgnoreCase))
            {
                var panelName = SafeText(group.Key, ribbon.FallbackPanelName);
                var items = group
                    .Select(feature => PushItem(feature, "large", string.Empty, string.Empty))
                    .ToList();
                var existingPanel = panels.FirstOrDefault(panel => string.Equals(panel.Name, panelName, StringComparison.OrdinalIgnoreCase));
                if (existingPanel == null)
                {
                    panels.Add(new RibbonPanelViewModel(
                        SafeId(group.FirstOrDefault()?.GroupId, panelName),
                        panelName,
                        group.Min(feature => feature.GroupOrder),
                        items));
                    continue;
                }

                panels.Remove(existingPanel);
                panels.Add(new RibbonPanelViewModel(
                    existingPanel.Id,
                    existingPanel.Name,
                    existingPanel.Order,
                    existingPanel.Items.Concat(items).ToList()));
            }

            panels.Sort((left, right) =>
            {
                var order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            });
        }

        private static RibbonItemViewModel PushItem(FeatureViewModel feature, string? size, string? textOverride, string? iconPathOverride)
        {
            return new RibbonItemViewModel(
                RibbonItemViewModel.PushButton,
                SafeId(feature.FeatureId, feature.DisplayName),
                SafeText(textOverride, feature.DisplayName),
                SelectFeatureIconPath(feature, iconPathOverride),
                string.IsNullOrWhiteSpace(size) ? feature.ButtonSize : size,
                feature,
                string.Empty,
                new List<RibbonItemViewModel>());
        }

        private static string SelectFeatureIconPath(FeatureViewModel feature, string? iconPathOverride)
        {
            if (feature == null) return iconPathOverride ?? string.Empty;
            if (string.IsNullOrWhiteSpace(iconPathOverride)) return feature.IconPath;
            if (IsManifestRelativeDefaultIcon(iconPathOverride!, feature.IconPath)) return feature.IconPath;
            return iconPathOverride!;
        }

        private static bool IsManifestRelativeDefaultIcon(string iconPathOverride, string featureIconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPathOverride) || string.IsNullOrWhiteSpace(featureIconPath)) return false;
            if (Path.IsPathRooted(iconPathOverride) || !Path.IsPathRooted(featureIconPath)) return false;
            if (iconPathOverride.IndexOf(':') >= 0) return false;

            var normalizedOverride = NormalizePath(iconPathOverride).TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedOverride)) return false;

            var normalizedFeatureIcon = NormalizePath(featureIconPath);
            return normalizedFeatureIcon.EndsWith("/" + normalizedOverride, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string SafeText(string? value, string? fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? (fallback ?? string.Empty).Trim() : value!.Trim();
        }

        private static string SafeId(string? value, string? fallback)
        {
            var source = SafeText(value, fallback);
            return string.IsNullOrWhiteSpace(source) ? "ribbon-item" : source;
        }
    }
}
