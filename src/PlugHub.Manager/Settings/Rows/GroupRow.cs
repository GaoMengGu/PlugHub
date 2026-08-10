namespace PlugHub.Manager.Settings.Rows
{
    internal sealed class GroupRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int FeatureCount { get; set; }
        public int Order { get; set; }
    }
}
