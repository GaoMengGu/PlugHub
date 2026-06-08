using System.Collections.Generic;

namespace PlugHub.Framework.Configuration
{
    public sealed class FrameworkConfiguration
    {
        public ModulesConfiguration Modules { get; set; } = new ModulesConfiguration();
        public ViewsConfiguration Views { get; set; } = new ViewsConfiguration();
        public FeatureCombinationsConfiguration FeatureCombinations { get; set; } = new FeatureCombinationsConfiguration();
    }

    public sealed class ModulesConfiguration
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string IndexVersion { get; set; } = string.Empty;
        public List<string> RevitVersions { get; set; } = new List<string>();
        public string FrameworkVersionRange { get; set; } = string.Empty;
        public List<string> PackageDirectories { get; set; } = new List<string>();
        public List<ModuleSourceConfiguration> ModuleSources { get; set; } = new List<ModuleSourceConfiguration>();
        public List<PackageRepositoryConfiguration> Repositories { get; set; } = new List<PackageRepositoryConfiguration>();
        public ConflictPolicyConfiguration ConflictPolicy { get; set; } = new ConflictPolicyConfiguration();
        public List<ModuleConfiguration> Modules { get; set; } = new List<ModuleConfiguration>();
    }

    public sealed class ModuleSourceConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Repository { get; set; } = string.Empty;
        public string Ref { get; set; } = string.Empty;
        public string ManifestPath { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool AutoUpdate { get; set; }
    }

    public sealed class PackageRepositoryConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Provider { get; set; } = "gitee";
        public string Visibility { get; set; } = "public";
        public string Repository { get; set; } = string.Empty;
        public string Ref { get; set; } = "main";
        public string ManifestPath { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string EncryptedApiKey { get; set; } = string.Empty;
        public string ApiKeyProtection { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }

    public sealed class ConflictPolicyConfiguration
    {
        public string DuplicateFeatureId { get; set; } = "fail-feature";
        public string DuplicateModuleId { get; set; } = "fail-module";
        public string MissingModuleType { get; set; } = "warn";
    }

    public sealed class ModuleConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Assembly { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string ResolvedBaseDirectory { get; set; } = string.Empty;
        public List<string> RevitVersions { get; set; } = new List<string>();
        public string FrameworkVersionRange { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool Visible { get; set; }
        public int Order { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> DependsOn { get; set; } = new List<string>();
        public List<FeatureConfiguration> Features { get; set; } = new List<FeatureConfiguration>();
    }

    public sealed class FeatureConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public int Order { get; set; }
        public string DefaultState { get; set; } = "Visible";
        public string CommandKey { get; set; } = string.Empty;
        public string CommandAssembly { get; set; } = string.Empty;
        public string CommandType { get; set; } = string.Empty;
        public string ButtonSize { get; set; } = "large";
        public string IconPath { get; set; } = string.Empty;
    }

    public sealed class ViewsConfiguration
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string DefaultView { get; set; } = string.Empty;
        public List<ViewConfiguration> Views { get; set; } = new List<ViewConfiguration>();
    }

    public sealed class FeatureCombinationsConfiguration
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string DefaultPreset { get; set; } = string.Empty;
        public List<FeatureCombinationPresetConfiguration> Presets { get; set; } = new List<FeatureCombinationPresetConfiguration>();
    }

    public sealed class FeatureCombinationPresetConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ViewId { get; set; } = string.Empty;
        public List<ModuleOverrideConfiguration> ModuleOverrides { get; set; } = new List<ModuleOverrideConfiguration>();
    }

    public sealed class ModuleOverrideConfiguration
    {
        public string ModuleId { get; set; } = string.Empty;
        public bool? Enabled { get; set; }
        public bool? Visible { get; set; }
        public int? Order { get; set; }
    }

    public sealed class ViewConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RibbonConfiguration Ribbon { get; set; } = new RibbonConfiguration();
        public List<string> IncludeTags { get; set; } = new List<string>();
        public List<string> ExcludeTags { get; set; } = new List<string>();
        public List<string> IncludeCategories { get; set; } = new List<string>();
        public List<string> ExcludeCategories { get; set; } = new List<string>();
        public List<ViewGroupConfiguration> Groups { get; set; } = new List<ViewGroupConfiguration>();
        public List<string> Sort { get; set; } = new List<string>();
        public string EmptyStateText { get; set; } = string.Empty;
    }

    public sealed class RibbonConfiguration
    {
        public string TabName { get; set; } = "PlugHub";
        public string FallbackPanelName { get; set; } = "Framework";
        public string LayoutVersion { get; set; } = string.Empty;
        public List<RibbonPanelLayoutConfiguration> Panels { get; set; } = new List<RibbonPanelLayoutConfiguration>();
    }

    public sealed class RibbonPanelLayoutConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<RibbonItemLayoutConfiguration> Items { get; set; } = new List<RibbonItemLayoutConfiguration>();
    }

    public sealed class RibbonItemLayoutConfiguration
    {
        public string Type { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string DefaultFeatureId { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty;
        public string TextOverride { get; set; } = string.Empty;
        public string IconPathOverride { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<RibbonItemLayoutConfiguration> Items { get; set; } = new List<RibbonItemLayoutConfiguration>();
    }

    public sealed class ViewGroupConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> IncludeTags { get; set; } = new List<string>();
        public List<string> IncludeCategories { get; set; } = new List<string>();
        public int Order { get; set; }
        public string Presentation { get; set; } = "panel";
    }
}
