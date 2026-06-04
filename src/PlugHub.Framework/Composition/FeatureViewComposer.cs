using System;
using System.Collections.Generic;
using System.Linq;
using PlugHub.Contracts.Features;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Composition
{
    public sealed class FeatureViewComposer
    {
        private const string ViewIncludeFilterSkipReason = "feature not included by view filters";

        public IReadOnlyList<FeatureViewModel> Compose(IReadOnlyList<FeatureDescriptor> features, ViewConfiguration view)
        {
            return ComposeDetailed(features, view).Features;
        }

        public FeatureViewCompositionResult ComposeDetailed(IReadOnlyList<FeatureDescriptor> features, ViewConfiguration view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));

            var sourceFeatures = features ?? new List<FeatureDescriptor>();
            var included = ProjectAll(sourceFeatures, view);
            if (ShouldRetryWithoutStaleIncludeFilters(included, view))
            {
                included = ProjectAll(sourceFeatures, CreateIncludeFilterFallbackView(view));
            }

            var visible = included
                .Where(result => result.Model != null)
                .Select(result => result.Model!)
                .OrderBy(vm => vm, new FeatureViewComparer(view.Sort))
                .ToList();

            var skipped = included
                .Where(result => result.Model == null)
                .Select(result => result.SkipReason)
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .ToList();

            return new FeatureViewCompositionResult(visible, skipped, view.EmptyStateText);
        }

        private static List<ProjectedFeature> ProjectAll(IReadOnlyList<FeatureDescriptor> features, ViewConfiguration view)
        {
            return (features ?? new List<FeatureDescriptor>())
                .Select(feature => Project(feature, view))
                .ToList();
        }

        private static bool ShouldRetryWithoutStaleIncludeFilters(IReadOnlyList<ProjectedFeature> included, ViewConfiguration view)
        {
            return HasIncludeFilters(view)
                && included.Any()
                && included.All(result => result.Model == null)
                && included.Any(result => result.SkipReason.IndexOf(ViewIncludeFilterSkipReason, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool HasIncludeFilters(ViewConfiguration view)
        {
            return (view.IncludeTags ?? new List<string>()).Any()
                || (view.IncludeCategories ?? new List<string>()).Any();
        }

        private static ViewConfiguration CreateIncludeFilterFallbackView(ViewConfiguration view)
        {
            return new ViewConfiguration
            {
                Id = view.Id,
                Name = view.Name,
                Description = view.Description,
                Ribbon = view.Ribbon,
                IncludeTags = new List<string>(),
                ExcludeTags = new List<string>(view.ExcludeTags ?? new List<string>()),
                IncludeCategories = new List<string>(),
                ExcludeCategories = new List<string>(view.ExcludeCategories ?? new List<string>()),
                Groups = new List<ViewGroupConfiguration>(view.Groups ?? new List<ViewGroupConfiguration>()),
                Sort = new List<string>(view.Sort ?? new List<string>()),
                EmptyStateText = view.EmptyStateText
            };
        }

        private static ProjectedFeature Project(FeatureDescriptor feature, ViewConfiguration view)
        {
            var excludeTags = view.ExcludeTags ?? new List<string>();
            var excludeCategories = view.ExcludeCategories ?? new List<string>();
            var includeTags = view.IncludeTags ?? new List<string>();
            var includeCategories = view.IncludeCategories ?? new List<string>();
            var groups = view.Groups ?? new List<ViewGroupConfiguration>();

            if (feature.DefaultState != FeatureState.Visible)
            {
                return ProjectedFeature.Hidden(feature.Id, "feature is not visible by default");
            }

            if (excludeTags.Any(tag => Contains(feature.Tags, tag)))
            {
                return ProjectedFeature.Hidden(feature.Id, "feature excluded by tag");
            }

            if (Contains(excludeCategories, feature.Category))
            {
                return ProjectedFeature.Hidden(feature.Id, "feature excluded by category");
            }

            var hasIncludeTags = includeTags.Any();
            var hasIncludeCategories = includeCategories.Any();
            if (hasIncludeTags || hasIncludeCategories)
            {
                var includedByView = includeTags.Any(tag => Contains(feature.Tags, tag)) || Contains(includeCategories, feature.Category);
                if (!includedByView)
                {
                    return ProjectedFeature.Hidden(feature.Id, ViewIncludeFilterSkipReason);
                }
            }

            var group = SelectGroup(feature, groups) ?? CreateFallbackGroup(feature);
            if (group == null)
            {
                return ProjectedFeature.Hidden(feature.Id, "feature does not match any view group");
            }

            return ProjectedFeature.Visible(new FeatureViewModel
            {
                FeatureId = feature.Id,
                ModuleId = feature.ModuleId,
                DisplayName = feature.Name,
                GroupId = group.Id,
                GroupName = group.Name,
                GroupOrder = group.Order,
                Category = feature.Category,
                DisplayOrder = feature.Order,
                IsEnabled = true,
                Tags = (feature.Tags ?? new List<string>()).ToList(),
                Description = feature.Description,
                CommandKey = feature.CommandKey,
                CommandAssembly = feature.CommandAssembly,
                CommandType = feature.CommandType,
                ButtonSize = string.IsNullOrWhiteSpace(feature.ButtonSize) ? "large" : feature.ButtonSize,
                IconPath = feature.IconPath
            });
        }

        private static bool MatchesGroup(FeatureDescriptor feature, ViewGroupConfiguration group)
        {
            return string.Equals(group.Id, feature.Group, System.StringComparison.OrdinalIgnoreCase)
                || Contains(group.IncludeCategories, feature.Category)
                || group.IncludeTags.Any(t => Contains(feature.Tags, t));
        }

        private static ViewGroupConfiguration? SelectGroup(FeatureDescriptor feature, IReadOnlyList<ViewGroupConfiguration> groups)
        {
            return groups
                .Where(group => MatchesGroup(feature, group))
                .OrderBy(group => string.Equals(group.Id, feature.Group, System.StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(group => group.Order)
                .ThenBy(group => group.Name)
                .FirstOrDefault();
        }

        private static ViewGroupConfiguration? CreateFallbackGroup(FeatureDescriptor feature)
        {
            var groupId = FirstNonEmpty(feature.Group, feature.Category, feature.ModuleId);
            if (string.IsNullOrWhiteSpace(groupId)) return null;
            var groupName = FirstNonEmpty(feature.Group, feature.ModuleName, feature.Category, feature.ModuleId);

            return new ViewGroupConfiguration
            {
                Id = groupId,
                Name = groupName,
                Order = feature.Order,
                IncludeTags = new List<string>(),
                IncludeCategories = new List<string>(),
                Presentation = "panel"
            };
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static bool Contains(IEnumerable<string> values, string candidate)
        {
            return values != null && values.Any(value => string.Equals(value, candidate, System.StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class FeatureViewModel
    {
        public string FeatureId { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public int GroupOrder { get; set; }
        public string Category { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsEnabled { get; set; }
        public IReadOnlyList<string> Tags { get; set; } = new List<string>();
        public string Description { get; set; } = string.Empty;
        public string CommandKey { get; set; } = string.Empty;
        public string CommandAssembly { get; set; } = string.Empty;
        public string CommandType { get; set; } = string.Empty;
        public string ButtonSize { get; set; } = "large";
        public string IconPath { get; set; } = string.Empty;
    }

    public sealed class FeatureViewCompositionResult
    {
        public FeatureViewCompositionResult(IReadOnlyList<FeatureViewModel> features, IReadOnlyList<string> skippedFeatures, string emptyStateText)
        {
            Features = features ?? new List<FeatureViewModel>();
            SkippedFeatures = skippedFeatures ?? new List<string>();
            EmptyStateText = emptyStateText ?? string.Empty;
        }

        public IReadOnlyList<FeatureViewModel> Features { get; }
        public IReadOnlyList<string> SkippedFeatures { get; }
        public string EmptyStateText { get; }
    }

    internal sealed class FeatureViewComparer : IComparer<FeatureViewModel>
    {
        private readonly IReadOnlyList<string> _sortKeys;

        public FeatureViewComparer(IReadOnlyList<string> sortKeys)
        {
            _sortKeys = sortKeys != null && sortKeys.Count > 0
                ? sortKeys
                : new[] { "group.order", "feature.order", "feature.name", "feature.id" };
        }

        public int Compare(FeatureViewModel x, FeatureViewModel y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            foreach (var sortKey in _sortKeys)
            {
                var comparison = CompareByKey(sortKey, x, y);
                if (comparison != 0) return comparison;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(x.FeatureId, y.FeatureId);
        }

        private static int CompareByKey(string sortKey, FeatureViewModel x, FeatureViewModel y)
        {
            switch ((sortKey ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "group.order":
                    return x.GroupOrder.CompareTo(y.GroupOrder);
                case "group.name":
                    return StringComparer.OrdinalIgnoreCase.Compare(x.GroupName, y.GroupName);
                case "feature.order":
                    return x.DisplayOrder.CompareTo(y.DisplayOrder);
                case "feature.category":
                    return StringComparer.OrdinalIgnoreCase.Compare(x.Category, y.Category);
                case "feature.name":
                    return StringComparer.OrdinalIgnoreCase.Compare(x.DisplayName, y.DisplayName);
                case "feature.tag":
                case "feature.tags":
                    return StringComparer.OrdinalIgnoreCase.Compare(FirstTag(x.Tags), FirstTag(y.Tags));
                case "feature.module":
                case "module.id":
                    return StringComparer.OrdinalIgnoreCase.Compare(x.ModuleId, y.ModuleId);
                case "feature.id":
                    return StringComparer.OrdinalIgnoreCase.Compare(x.FeatureId, y.FeatureId);
                default:
                    return 0;
            }
        }

        private static string FirstTag(IReadOnlyList<string> tags)
        {
            return tags != null && tags.Count > 0 ? tags[0] : string.Empty;
        }
    }

    internal sealed class ProjectedFeature
    {
        private ProjectedFeature(FeatureViewModel? model, string skipReason)
        {
            Model = model;
            SkipReason = skipReason;
        }

        public FeatureViewModel? Model { get; }
        public string SkipReason { get; }

        public static ProjectedFeature Visible(FeatureViewModel model) => new ProjectedFeature(model, string.Empty);
        public static ProjectedFeature Hidden(string featureId, string reason) => new ProjectedFeature(null, $"{featureId}: {reason}");
    }
}
