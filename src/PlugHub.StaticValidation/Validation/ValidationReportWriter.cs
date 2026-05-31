using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation.Validation
{
    internal static class ValidationReportWriter
    {
        public static void WriteJson(string path, ValidationReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(path, serializer.Serialize(new
            {
                issues = report.Issues.Select(issue => new
                {
                    severity = issue.Severity.ToString(),
                    code = issue.Code,
                    file = issue.File,
                    message = issue.Message,
                    suggestion = issue.Suggestion
                }).ToList()
            }));
        }

        public static void WriteHtml(string path, ValidationReport report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            var rows = string.Join("\n", report.Issues.Select(issue =>
                "<tr><td>" + Escape(issue.Severity.ToString()) + "</td><td>" +
                Escape(issue.Code) + "</td><td>" + Escape(issue.File) + "</td><td>" +
                Escape(issue.Message) + "</td><td>" + Escape(issue.Suggestion) + "</td></tr>"));
            File.WriteAllText(path,
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>PlugHub Validation</title></head><body><table>" +
                "<thead><tr><th>severity</th><th>code</th><th>file</th><th>message</th><th>suggestion</th></tr></thead><tbody>" +
                rows +
                "</tbody></table></body></html>",
                Encoding.UTF8);
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
