using System.Collections.Generic;
using System.Linq;
using PlugHub.Contracts.Modules;

namespace PlugHub.Framework.Diagnostics
{
    public sealed class DiagnosticsSink
    {
        private readonly List<DiagnosticMessage> _messages = new List<DiagnosticMessage>();

        public void Add(DiagnosticMessage message)
        {
            if (message != null) _messages.Add(message);
        }

        public void AddRange(IEnumerable<DiagnosticMessage> messages)
        {
            foreach (var message in messages ?? Enumerable.Empty<DiagnosticMessage>())
            {
                Add(message);
            }
        }

        public void Info(string moduleId, string code, string message) => Add(DiagnosticSeverity.Info, moduleId, code, message);
        public void Warning(string moduleId, string code, string message) => Add(DiagnosticSeverity.Warning, moduleId, code, message);
        public void Error(string moduleId, string code, string message) => Add(DiagnosticSeverity.Error, moduleId, code, message);

        public bool HasErrors => _messages.Any(message => message.Severity == DiagnosticSeverity.Error);
        public IReadOnlyList<DiagnosticMessage> Messages => _messages.ToList();

        private void Add(DiagnosticSeverity severity, string moduleId, string code, string message)
        {
            Add(new DiagnosticMessage
            {
                Severity = severity,
                ModuleId = moduleId ?? string.Empty,
                Code = code ?? string.Empty,
                Message = message ?? string.Empty
            });
        }
    }
}
