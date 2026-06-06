using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PlugHub.Framework.Configuration;

namespace PlugHub.Manager.Settings.Rows
{
    internal sealed class RibbonLayoutNodeRow
    {
        public string NodeType { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string DefaultFeatureId { get; set; } = string.Empty;
        public string Size { get; set; } = "large";
        public string IconPath { get; set; } = string.Empty;
        public int Order { get; set; }
        public ObservableCollection<RibbonLayoutNodeRow> Children { get; } = new ObservableCollection<RibbonLayoutNodeRow>();

        public RibbonPanelLayoutConfiguration ToPanelConfiguration()
        {
            return new RibbonPanelLayoutConfiguration
            {
                Id = Id ?? string.Empty,
                Name = Text ?? string.Empty,
                Order = Order,
                Items = Children.Select(child => child.ToItemConfiguration()).ToList()
            };
        }

        public RibbonItemLayoutConfiguration ToItemConfiguration()
        {
            return new RibbonItemLayoutConfiguration
            {
                Type = NodeType ?? string.Empty,
                Id = Id ?? string.Empty,
                Text = Text ?? string.Empty,
                FeatureId = FeatureId ?? string.Empty,
                DefaultFeatureId = DefaultFeatureId ?? string.Empty,
                Size = Size ?? string.Empty,
                IconPath = IconPath ?? string.Empty,
                Order = Order,
                Items = Children.Select(child => child.ToItemConfiguration()).ToList()
            };
        }

        public static RibbonLayoutNodeRow FromPanel(RibbonPanelLayoutConfiguration panel)
        {
            var row = new RibbonLayoutNodeRow
            {
                NodeType = "panel",
                Id = panel.Id ?? string.Empty,
                Text = panel.Name ?? string.Empty,
                Order = panel.Order
            };

            foreach (var item in panel.Items ?? new List<RibbonItemLayoutConfiguration>())
            {
                row.Children.Add(FromItem(item));
            }

            return row;
        }

        public static RibbonLayoutNodeRow FromItem(RibbonItemLayoutConfiguration item)
        {
            var row = new RibbonLayoutNodeRow
            {
                NodeType = item.Type ?? string.Empty,
                Id = item.Id ?? string.Empty,
                Text = string.IsNullOrWhiteSpace(item.TextOverride) ? item.Text ?? string.Empty : item.TextOverride,
                FeatureId = item.FeatureId ?? string.Empty,
                DefaultFeatureId = item.DefaultFeatureId ?? string.Empty,
                Size = string.IsNullOrWhiteSpace(item.Size) ? "large" : item.Size,
                IconPath = string.IsNullOrWhiteSpace(item.IconPathOverride) ? item.IconPath ?? string.Empty : item.IconPathOverride,
                Order = item.Order
            };

            foreach (var child in item.Items ?? new List<RibbonItemLayoutConfiguration>())
            {
                row.Children.Add(FromItem(child));
            }

            return row;
        }
    }
}
