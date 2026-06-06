namespace PlugHub.Manager.Settings.Rows
{
    internal sealed class GroupRow
    {
        public string Id { get; set; } = string.Empty;
        public string PositionText { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int FeatureCount { get; set; }
        public string FeatureCountText { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
