namespace PlugHub.Framework.RibbonEditing
{
    public sealed class RibbonDesignerFeatureRow
    {
        public string ModuleId { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FeatureName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string GroupDisplayText { get; set; } = string.Empty;
        public string SearchText { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string ModuleBaseDirectory { get; set; } = string.Empty;
        public string ButtonSize { get; set; } = "large";
        public int Order { get; set; }
        public bool Visible { get; set; }
        public bool IsPlaced { get; set; }
        public string DisplayText { get; set; } = string.Empty;
    }
}
