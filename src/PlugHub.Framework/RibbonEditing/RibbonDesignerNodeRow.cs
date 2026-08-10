using System.Collections.ObjectModel;

namespace PlugHub.Framework.RibbonEditing
{
    public sealed class RibbonDesignerNodeRow
    {
        public const string Tab = "tab";
        public const string Panel = "panel";
        public const string PushButton = "pushButton";
        public const string PulldownButton = "pulldownButton";
        public const string SplitButton = "splitButton";
        public const string Stack = "stack";

        public string NodeType { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string Size { get; set; } = "large";
        public string IconPath { get; set; } = string.Empty;
        public string DefaultFeatureId { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool RequiresRestart { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public ObservableCollection<RibbonDesignerNodeRow> Children { get; } = new ObservableCollection<RibbonDesignerNodeRow>();
    }
}
