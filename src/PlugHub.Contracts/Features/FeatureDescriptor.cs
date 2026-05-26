using System.Collections.Generic;

namespace PlugHub.Contracts.Features
{
    public sealed class FeatureDescriptor
    {
        public string Id { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public IReadOnlyList<string> Tags { get; set; } = new List<string>();
        public int Order { get; set; }
        public FeatureState DefaultState { get; set; } = FeatureState.Visible;
        public string CommandKey { get; set; } = string.Empty;
        public string CommandAssembly { get; set; } = string.Empty;
        public string CommandType { get; set; } = string.Empty;
        public string ButtonSize { get; set; } = "large";
        public string IconPath { get; set; } = string.Empty;
    }

    public enum FeatureState { Visible, Disabled, Hidden }
}
