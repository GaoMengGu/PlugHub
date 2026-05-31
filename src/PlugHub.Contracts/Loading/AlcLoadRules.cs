using System;
using System.Collections.Generic;

namespace PlugHub.Contracts.Loading
{
    public static class AlcLoadRules
    {
        private static readonly HashSet<string> DefaultContextAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RevitAPI",
            "RevitAPIUI",
            "PlugHub.Contracts"
        };

        public static bool MustUseDefaultContext(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName)) return false;

            var simpleName = SimpleName(assemblyName);
            return DefaultContextAssemblyNames.Contains(simpleName)
                || simpleName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                || simpleName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);
        }

        public static IReadOnlyCollection<string> SharedAssemblyNames => DefaultContextAssemblyNames;

        private static string SimpleName(string assemblyName)
        {
            var commaIndex = assemblyName.IndexOf(',');
            return (commaIndex >= 0 ? assemblyName.Substring(0, commaIndex) : assemblyName).Trim();
        }
    }
}
