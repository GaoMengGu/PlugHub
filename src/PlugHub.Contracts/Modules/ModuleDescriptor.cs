using System.Collections.Generic;
using PlugHub.Contracts.Features;

namespace PlugHub.Contracts.Modules
{
    public sealed class ModuleDescriptor
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ModuleState State { get; set; } = ModuleState.Enabled;
        public int Order { get; set; }
        public IReadOnlyList<string> Tags { get; set; } = new List<string>();
        public IReadOnlyList<FeatureDescriptor> Features { get; set; } = new List<FeatureDescriptor>();
    }

    public enum ModuleState { Enabled, Disabled, Hidden, Failed }
}
