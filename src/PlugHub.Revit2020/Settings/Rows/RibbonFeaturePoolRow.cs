namespace PlugHub.Revit2020.Settings.Rows
{
    internal sealed class RibbonFeaturePoolRow
    {
        public string ModuleId { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string FeatureName { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public bool IsPlaced { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}
