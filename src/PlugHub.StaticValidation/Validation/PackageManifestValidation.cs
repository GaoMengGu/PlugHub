using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation.Validation
{
    internal static class PackageManifestValidation
    {
        public static IEnumerable<ValidationIssue> ValidateFile(string path)
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            if (root == null || !root.ContainsKey("schemaVersion") || !root.ContainsKey("modules"))
            {
                yield return Error(path, "PH-PACKAGE-SCHEMA", "Packages manifest must contain schemaVersion and modules.", "Add schemaVersion and a modules array.");
                yield break;
            }

            var modules = root["modules"] as IEnumerable;
            if (modules == null || root["modules"] is string || !modules.Cast<object>().Any())
            {
                yield return Error(path, "PH-PACKAGE-MODULES", "Packages manifest must declare at least one module.", "Add one module entry.");
            }

            foreach (var module in (modules ?? new System.Collections.ArrayList()).Cast<object>().OfType<Dictionary<string, object>>())
            {
                var id = module.TryGetValue("id", out var moduleId) ? moduleId?.ToString() ?? "<unknown>" : "<unknown>";
                if (module.TryGetValue("autoStart", out var autoStart) && !(autoStart is bool))
                    yield return Error(path, "PH-PACKAGE-STARTUP", id + ": autoStart must be a JSON boolean.", "Use true or false without quotes.");
                if (module.TryGetValue("startupType", out var startupType) && !(startupType is string))
                    yield return Error(path, "PH-PACKAGE-STARTUP", id + ": startupType must be a JSON string.", "Use the public startup type's full name.");
                if (autoStart is bool enabled && enabled
                    && (!module.TryGetValue("assembly", out var assembly) || !(assembly is string assemblyName) || string.IsNullOrWhiteSpace(assemblyName)
                        || !(startupType is string typeName) || string.IsNullOrWhiteSpace(typeName)))
                    yield return Error(path, "PH-PACKAGE-STARTUP", id + ": autoStart requires non-empty assembly and startupType.", "Declare both fields for this module.");
            }
        }

        private static ValidationIssue Error(string file, string code, string message, string suggestion)
        {
            return new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Code = code,
                File = file,
                Message = message,
                Suggestion = suggestion
            };
        }
    }
}
