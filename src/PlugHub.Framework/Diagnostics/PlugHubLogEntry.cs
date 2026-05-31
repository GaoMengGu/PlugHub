using System;
using PlugHub.Contracts.Modules;

namespace PlugHub.Framework.Diagnostics
{
    public sealed class PlugHubLogEntry
    {
        public DateTime TimestampUtc { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string FeatureId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Exception { get; set; } = string.Empty;
    }
}
