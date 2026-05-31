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
                yield return Error(path, "PH-PACKAGE-SCHEMA", "Package manifest must contain schemaVersion and modules.", "Add schemaVersion and a modules array.");
                yield break;
            }

            var modules = root["modules"] as IEnumerable;
            if (modules == null || root["modules"] is string || !modules.Cast<object>().Any())
            {
                yield return Error(path, "PH-PACKAGE-MODULES", "Package manifest must declare at least one module.", "Add one module entry.");
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
