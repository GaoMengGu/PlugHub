using System.Collections.ObjectModel;
using PlugHub.Revit2020.Settings.Rows;

namespace PlugHub.Revit2020.Settings
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
    }
}
