using System.Collections.ObjectModel;
using PlugHub.Framework.RibbonEditing;
using PlugHub.Manager.Settings.Rows;

namespace PlugHub.Manager.Settings
{
    internal sealed class FrameworkSettingsViewModel
    {
        public ObservableCollection<FeatureRow> Features { get; } = new ObservableCollection<FeatureRow>();
        public ObservableCollection<GroupRow> Groups { get; } = new ObservableCollection<GroupRow>();
        public ObservableCollection<RepositoryRow> Repositories { get; } = new ObservableCollection<RepositoryRow>();
        public ObservableCollection<RepositoryPackageRow> RepositoryPackages { get; } = new ObservableCollection<RepositoryPackageRow>();
        public ObservableCollection<RibbonDesignerFeatureRow> RibbonDesignerFeatures { get; } = new ObservableCollection<RibbonDesignerFeatureRow>();
        public ObservableCollection<RibbonDesignerNodeRow> RibbonDesignerTabs { get; } = new ObservableCollection<RibbonDesignerNodeRow>();
        public RibbonDesignerNodeRow? SelectedRibbonDesignerNode { get; set; }
    }
}
