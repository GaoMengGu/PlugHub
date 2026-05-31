using System.Collections.Generic;
using System.Linq;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class ValidationReport
    {
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();
        public bool HasErrors => Issues.Any(issue => issue.Severity == ValidationSeverity.Error);

        public void Error(string code, string file, string message, string suggestion)
        {
            Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Code = code,
                File = file,
                Message = message,
                Suggestion = suggestion
            });
        }
    }
}
