namespace PlugHub.Contracts.Modules
{
    public sealed class DiagnosticMessage
    {
        public DiagnosticSeverity Severity { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
    }

    public enum DiagnosticSeverity { Info, Warning, Error }
}
