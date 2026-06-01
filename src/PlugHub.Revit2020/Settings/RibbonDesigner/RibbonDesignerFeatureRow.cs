namespace PlugHub.Revit2020.Settings.RibbonDesigner
{
    internal sealed class RibbonDesignerFeatureRow
    {
        public string ModuleId { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string FeatureName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string ButtonSize { get; set; } = "large";
        public bool IsPlaced { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}
