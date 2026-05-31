using System;
using System.Collections.Generic;

namespace PlugHub.Framework.Composition
{
    public sealed class RibbonLayoutViewModel
    {
        public RibbonLayoutViewModel(IReadOnlyList<RibbonPanelViewModel>? panels, IReadOnlyList<FeatureViewModel>? clickableFeatures)
        {
            Panels = panels ?? new List<RibbonPanelViewModel>();
            ClickableFeatures = clickableFeatures ?? new List<FeatureViewModel>();
        }

        public IReadOnlyList<RibbonPanelViewModel> Panels { get; }
        public IReadOnlyList<FeatureViewModel> ClickableFeatures { get; }
    }

    public sealed class RibbonPanelViewModel
    {
        public RibbonPanelViewModel(string? id, string? name, int order, IReadOnlyList<RibbonItemViewModel>? items)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Order = order;
            Items = items ?? new List<RibbonItemViewModel>();
        }

        public string Id { get; }
        public string Name { get; }
        public int Order { get; }
        public IReadOnlyList<RibbonItemViewModel> Items { get; }
    }

    public sealed class RibbonItemViewModel
    {
        public const string PushButton = "pushButton";
        public const string PulldownButton = "pulldownButton";
        public const string SplitButton = "splitButton";
        public const string Stack = "stack";

        public RibbonItemViewModel(
            string? type,
            string? id,
            string? text,
            string? iconPath,
            string? size,
            FeatureViewModel? feature,
            string? defaultFeatureId,
            IReadOnlyList<RibbonItemViewModel>? items)
        {
            Type = string.IsNullOrWhiteSpace(type) ? PushButton : type!.Trim();
            Id = id ?? string.Empty;
            Text = text ?? string.Empty;
            IconPath = iconPath ?? string.Empty;
            Size = size ?? string.Empty;
            Feature = feature;
            DefaultFeatureId = defaultFeatureId ?? string.Empty;
            Items = items ?? new List<RibbonItemViewModel>();
        }

        public string Type { get; }
        public string Id { get; }
        public string Text { get; }
        public string IconPath { get; }
        public string Size { get; }
        public FeatureViewModel? Feature { get; }
        public string FeatureId => Feature == null ? string.Empty : Feature.FeatureId;
        public string DefaultFeatureId { get; }
        public IReadOnlyList<RibbonItemViewModel> Items { get; }

        public IReadOnlyList<FeatureViewModel> ClickableFeatures()
        {
            var result = new List<FeatureViewModel>();
            CollectClickableFeatures(this, result);
            return result;
        }

        private static void CollectClickableFeatures(RibbonItemViewModel? item, ICollection<FeatureViewModel> result)
        {
            if (item == null) return;
            if (string.Equals(item.Type, PushButton, StringComparison.OrdinalIgnoreCase) && item.Feature != null)
            {
                result.Add(item.Feature);
            }

            foreach (var child in item.Items ?? new List<RibbonItemViewModel>())
            {
                CollectClickableFeatures(child, result);
            }
        }
    }
}
