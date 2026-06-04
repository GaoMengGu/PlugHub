using System.Collections.Generic;
using System.Linq;
using PlugHub.Contracts.Features;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Registry
{
    public sealed class FeatureRegistry
    {
        private readonly List<FeatureDescriptor> _features = new List<FeatureDescriptor>();
        private readonly List<DiagnosticMessage> _diagnostics = new List<DiagnosticMessage>();
        private readonly HashSet<string> _moduleIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _featureIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public void Register(ModuleDescriptor module, ConflictPolicyConfiguration? conflictPolicy = null)
        {
            if (module == null) return;
            var policy = conflictPolicy ?? new ConflictPolicyConfiguration();

            if (!_moduleIds.Add(module.Id))
            {
                AddDiagnostic(SeverityFor(policy.DuplicateModuleId), module.Id, "RT-MODULE-DUPLICATE", "Duplicate module id was skipped.");
                return;
            }

            if (module.State != ModuleState.Enabled)
            {
                AddDiagnostic(DiagnosticSeverity.Info, module.Id, "RT-MODULE-SKIPPED", "Module is not enabled and was skipped.");
                return;
            }

            foreach (var feature in module.Features ?? new List<FeatureDescriptor>())
            {
                if (!_featureIds.Add(feature.Id))
                {
                    AddDiagnostic(SeverityFor(policy.DuplicateFeatureId), module.Id, "RT-FEATURE-DUPLICATE", "Duplicate feature id was skipped: " + feature.Id);
                    continue;
                }

                _features.Add(CloneFeature(module, feature));
            }
        }

        public IReadOnlyList<FeatureDescriptor> All() => _features.OrderBy(f => f.Order).ThenBy(f => f.Id).ToList();
        public IReadOnlyList<DiagnosticMessage> Diagnostics => _diagnostics;

        private static FeatureDescriptor CloneFeature(ModuleDescriptor module, FeatureDescriptor feature)
        {
            return new FeatureDescriptor
            {
                Id = feature.Id,
                ModuleId = string.IsNullOrWhiteSpace(feature.ModuleId) ? module.Id : feature.ModuleId,
                ModuleName = feature.ModuleName,
                Name = feature.Name,
                Description = feature.Description,
                Category = feature.Category,
                Group = feature.Group,
                Tags = feature.Tags?.ToList() ?? new List<string>(),
                Order = feature.Order,
                DefaultState = feature.DefaultState,
                CommandKey = feature.CommandKey,
                CommandAssembly = feature.CommandAssembly,
                CommandType = feature.CommandType,
                ButtonSize = string.IsNullOrWhiteSpace(feature.ButtonSize) ? "large" : feature.ButtonSize,
                IconPath = feature.IconPath
            };
        }

        private void AddDiagnostic(DiagnosticSeverity severity, string moduleId, string code, string message)
        {
            _diagnostics.Add(new DiagnosticMessage
            {
                Severity = severity,
                ModuleId = moduleId,
                Code = code,
                Message = message
            });
        }

        private static DiagnosticSeverity SeverityFor(string policy)
        {
            return string.Equals(policy, "fail-feature", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(policy, "fail-module", System.StringComparison.OrdinalIgnoreCase)
                ? DiagnosticSeverity.Error
                : DiagnosticSeverity.Warning;
        }
    }
}
