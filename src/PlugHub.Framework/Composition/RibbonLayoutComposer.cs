using System;
using System.Collections.Generic;
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
                .GroupBy(feature => new { feature.GroupId, feature.GroupName, feature.GroupOrder })
                .OrderBy(group => group.Key.GroupOrder)
                .ThenBy(group => group.Key.GroupName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new RibbonPanelViewModel(
                    SafeId(group.Key.GroupId, group.Key.GroupName),
                    SafeText(group.Key.GroupName, fallbackPanelName),
                    group.Key.GroupOrder,
                    group
                        .OrderBy(feature => feature.DisplayOrder)
                        .ThenBy(feature => feature.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                        .Select(feature => PushItem(feature, feature.ButtonSize, string.Empty, string.Empty))
                        .ToList()))
                .ToList();

            var clickable = panels.SelectMany(panel => panel.Items).SelectMany(item => item.ClickableFeatures()).ToList();
            return new RibbonLayoutViewModel(panels, clickable);
        }

        private static RibbonLayoutViewModel BuildConfiguredLayout(RibbonConfiguration ribbon, IReadOnlyList<FeatureViewModel> features)
        {
            IReadOnlyList<FeatureViewModel> sourceFeatures = features ?? new List<FeatureViewModel>();
            var featuresById = sourceFeatures
                .Where(feature => !string.IsNullOrWhiteSpace(feature.FeatureId))
                .GroupBy(feature => feature.FeatureId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var placedFeatureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var panels = (ribbon.Panels ?? new List<RibbonPanelLayoutConfiguration>())
                .OrderBy(panel => panel.Order)
                .ThenBy(panel => SafeText(panel.Name, panel.Id), StringComparer.OrdinalIgnoreCase)
                .Select(panel => new RibbonPanelViewModel(
                    SafeId(panel.Id, panel.Name),
                    SafeText(panel.Name, panel.Id),
                    panel.Order,
                    BuildConfiguredItems(panel.Items, featuresById, placedFeatureIds)))
                .ToList();

            AppendUnplacedFeatures(ribbon, panels, sourceFeatures, placedFeatureIds);

            var clickable = panels.SelectMany(panel => panel.Items).SelectMany(item => item.ClickableFeatures()).ToList();
            return new RibbonLayoutViewModel(panels, clickable);
        }

        private static List<RibbonItemViewModel> BuildConfiguredItems(
            IEnumerable<RibbonItemLayoutConfiguration>? items,
            IReadOnlyDictionary<string, FeatureViewModel> featuresById,
            ISet<string> placedFeatureIds)
        {
            return (items ?? new List<RibbonItemLayoutConfiguration>())
                .OrderBy(item => item.Order)
                .ThenBy(item => SafeText(item.Text, item.Id), StringComparer.OrdinalIgnoreCase)
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
            var type = string.IsNullOrWhiteSpace(item.Type) ? RibbonItemViewModel.PushButton : item.Type.Trim();
            if (string.Equals(type, RibbonItemViewModel.PushButton, StringComparison.OrdinalIgnoreCase))
            {
                if (!featuresById.TryGetValue(item.FeatureId ?? string.Empty, out var feature)) return null;
                if (!placedFeatureIds.Add(feature.FeatureId)) return null;
                return PushItem(feature, item.Size, item.TextOverride, item.IconPathOverride);
            }

            var children = BuildConfiguredItems(item.Items, featuresById, placedFeatureIds);
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

            panels.Add(new RibbonPanelViewModel(
                "fallback",
                SafeText(ribbon.FallbackPanelName, "Framework"),
                int.MaxValue,
                unplaced.Select(feature => PushItem(feature, feature.ButtonSize, string.Empty, string.Empty)).ToList()));
        }

        private static RibbonItemViewModel PushItem(FeatureViewModel feature, string? size, string? textOverride, string? iconPathOverride)
        {
            return new RibbonItemViewModel(
                RibbonItemViewModel.PushButton,
                SafeId(feature.FeatureId, feature.DisplayName),
                SafeText(textOverride, feature.DisplayName),
                string.IsNullOrWhiteSpace(iconPathOverride) ? feature.IconPath : iconPathOverride,
                string.IsNullOrWhiteSpace(size) ? feature.ButtonSize : size,
                feature,
                string.Empty,
                new List<RibbonItemViewModel>());
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
