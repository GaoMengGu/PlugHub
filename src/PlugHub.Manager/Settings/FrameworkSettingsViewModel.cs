using System.Collections.ObjectModel;
using PlugHub.Manager.Settings.RibbonDesigner;
using PlugHub.Manager.Settings.Rows;

namespace PlugHub.Manager.Settings
{
    internal sealed class FrameworkSettingsViewModel
    {
        public ObservableCollection<ModuleRow> Modules { get; } = new ObservableCollection<ModuleRow>();
        public ObservableCollection<FeatureRow> Features { get; } = new ObservableCollection<FeatureRow>();
        public ObservableCollection<GroupRow> Groups { get; } = new ObservableCollection<GroupRow>();
        public ObservableCollection<RepositoryRow> Repositories { get; } = new ObservableCollection<RepositoryRow>();
        public ObservableCollection<RepositoryPackageRow> RepositoryPackages { get; } = new ObservableCollection<RepositoryPackageRow>();
        public ObservableCollection<PendingPackageOperationRow> PendingOperations { get; } = new ObservableCollection<PendingPackageOperationRow>();
        public ObservableCollection<DiagnosticRow> Diagnostics { get; } = new ObservableCollection<DiagnosticRow>();
        public ObservableCollection<RibbonLayoutNodeRow> RibbonLayoutNodes { get; } = new ObservableCollection<RibbonLayoutNodeRow>();
        public ObservableCollection<RibbonFeaturePoolRow> RibbonFeaturePool { get; } = new ObservableCollection<RibbonFeaturePoolRow>();
        public ObservableCollection<RibbonDesignerFeatureRow> RibbonDesignerFeatures { get; } = new ObservableCollection<RibbonDesignerFeatureRow>();
        public ObservableCollection<RibbonDesignerNodeRow> RibbonDesignerTabs { get; } = new ObservableCollection<RibbonDesignerNodeRow>();
        public RibbonDesignerNodeRow? SelectedRibbonDesignerNode { get; set; }
    }
}
