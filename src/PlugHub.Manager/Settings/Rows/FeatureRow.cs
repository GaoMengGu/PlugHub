using System;
using System.Collections.Generic;
using PlugHub.Framework.Configuration;

namespace PlugHub.Manager.Settings.Rows
{
    internal sealed class FeatureRow
    {
        public string ModuleId { get; set; } = string.Empty;
        public string OriginalModuleId { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string PositionText { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ConfigName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string GroupDisplayText { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public bool Visible { get; set; }
        public string IconPath { get; set; } = string.Empty;
        public string ModuleBaseDirectory { get; set; } = string.Empty;
        public int Order { get; set; }
        public string ButtonSize { get; set; } = "large";
        public string ButtonSizeDisplayText { get; set; } = "大";
        public string CommandKey { get; set; } = string.Empty;
        public string CommandAssembly { get; set; } = string.Empty;
        public string CommandType { get; set; } = string.Empty;

        public FeatureConfiguration ToConfiguration()
        {
            return new FeatureConfiguration
            {
                Id = FeatureId ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(ConfigName) ? Name ?? string.Empty : ConfigName,
                DisplayName = DisplayName ?? string.Empty,
                Description = Description ?? string.Empty,
                Category = Category ?? string.Empty,
                Group = Group ?? string.Empty,
                Tags = new List<string>(Tags ?? new List<string>()),
                Order = Order,
                DefaultState = Visible ? "Visible" : "Hidden",
                CommandKey = CommandKey ?? string.Empty,
                CommandAssembly = CommandAssembly ?? string.Empty,
                CommandType = CommandType ?? string.Empty,
                ButtonSize = NormalizeButtonSize(ButtonSize),
                IconPath = IconPath ?? string.Empty
            };
        }

        private static string NormalizeButtonSize(string value)
        {
            return string.Equals(value, "small", StringComparison.OrdinalIgnoreCase) ? "small" : "large";
        }
    }
}
