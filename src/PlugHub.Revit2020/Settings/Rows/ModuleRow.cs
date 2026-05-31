namespace PlugHub.Revit2020.Settings.Rows
{
    internal sealed class ModuleRow
    {
        public string Id { get; set; } = string.Empty;
        public string PositionText { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool Visible { get; set; }
        public string SourceId { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
