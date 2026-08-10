using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class ValidationSource
    {
        private readonly string _root;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();

        public ValidationSource(string root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public string ReadText(string relativePath)
        {
            return File.ReadAllText(FullPath(relativePath));
        }

        public Dictionary<string, object> ReadObject(string relativePath)
        {
            return _json.Deserialize<Dictionary<string, object>>(ReadText(relativePath));
        }

        public string ReadAllCSharp(string relativeDirectory)
        {
            return string.Join("\n", Directory.GetFiles(FullPath(relativeDirectory), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        }

        public string ReadProductionCSharp()
        {
            return string.Join(
                "\n",
                Directory.GetFiles(FullPath("src"), "*.cs", SearchOption.AllDirectories)
                    .Where(path => !RelativePath(path).StartsWith("src" + Path.DirectorySeparatorChar + "PlugHub.StaticValidation", StringComparison.OrdinalIgnoreCase))
                    .Select(File.ReadAllText));
        }

        public string MethodBody(string source, string methodName)
        {
            var token = methodName + "(";
            var start = -1;
            var search = 0;
            while (search < source.Length)
            {
                var candidate = source.IndexOf(token, search, StringComparison.Ordinal);
                if (candidate < 0) break;
                var lineStart = source.LastIndexOf('\n', candidate);
                var line = source.Substring(lineStart + 1, candidate - lineStart - 1);
                if (line.Contains("private ") && !line.Contains("="))
                {
                    start = lineStart + 1;
                    break;
                }

                search = candidate + token.Length;
            }

            if (start < 0) throw new InvalidOperationException("missing method: " + methodName);
            var next = source.IndexOf("\n        private ", start + methodName.Length, StringComparison.Ordinal);
            return next >= 0 ? source.Substring(start, next - start) : source.Substring(start);
        }

        public string FullPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private string RelativePath(string path)
        {
            return path.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
