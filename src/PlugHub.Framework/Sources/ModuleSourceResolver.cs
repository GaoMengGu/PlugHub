using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Sources
{
    public sealed class ModuleSourceResolver
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public ModuleSourceResolutionResult Resolve(string baseDirectory, ModulesConfiguration modules)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            if (modules == null) throw new ArgumentNullException(nameof(modules));

            var diagnostics = new List<DiagnosticMessage>();
            var resolved = CloneModules(modules);

            foreach (var source in modules.ModuleSources ?? new List<ModuleSourceConfiguration>())
            {
                if (!source.Enabled) continue;

                if (string.Equals(source.Type, "localFolder", StringComparison.OrdinalIgnoreCase))
                {
                    AddLocalFolderModules(baseDirectory, source, resolved, diagnostics);
                    continue;
                }

                if (string.Equals(source.Type, "github", StringComparison.OrdinalIgnoreCase))
                {
                    AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MISSING", "GitHub module source requires a local cache/update step before modules can be loaded.");
                    continue;
                }

                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", "Unknown module source type: " + source.Type);
            }

            return new ModuleSourceResolutionResult(resolved, diagnostics);
        }

        private void AddLocalFolderModules(string baseDirectory, ModuleSourceConfiguration source, ModulesConfiguration resolved, ICollection<DiagnosticMessage> diagnostics)
        {
            var sourceDirectory = ResolvePath(baseDirectory, source.Path);
            if (!Directory.Exists(sourceDirectory))
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MISSING", "Module source folder was not found: " + sourceDirectory);
                return;
            }

            var manifestPath = Path.Combine(sourceDirectory, string.IsNullOrWhiteSpace(source.ManifestPath) ? "modules.json" : source.ManifestPath);
            if (!File.Exists(manifestPath))
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", "Module source manifest was not found: " + manifestPath);
                return;
            }

            try
            {
                var sourceModules = _serializer.Deserialize<ModulesConfiguration>(File.ReadAllText(manifestPath));
                foreach (var module in sourceModules.Modules ?? new List<ModuleConfiguration>())
                {
                    module.SourceId = string.IsNullOrWhiteSpace(module.SourceId) ? source.Id : module.SourceId;
                    module.ResolvedBaseDirectory = sourceDirectory;
                    resolved.Modules.Add(module);
                }
            }
            catch (Exception ex)
            {
                AddSourceDiagnostic(diagnostics, source.Id, "PH-SOURCE-MANIFEST", ex.Message);
            }
        }

        private static ModulesConfiguration CloneModules(ModulesConfiguration modules)
        {
            return new ModulesConfiguration
            {
                SchemaVersion = modules.SchemaVersion,
                ModuleDirectories = new List<string>(modules.ModuleDirectories ?? new List<string>()),
                ModuleSources = (modules.ModuleSources ?? new List<ModuleSourceConfiguration>()).Select(source => new ModuleSourceConfiguration
                {
                    Id = source.Id,
                    Type = source.Type,
                    Path = source.Path,
                    Repository = source.Repository,
                    Ref = source.Ref,
                    ManifestPath = source.ManifestPath,
                    Enabled = source.Enabled
                }).ToList(),
                ConflictPolicy = modules.ConflictPolicy ?? new ConflictPolicyConfiguration(),
                Modules = new List<ModuleConfiguration>(modules.Modules ?? new List<ModuleConfiguration>())
            };
        }

        private static string ResolvePath(string baseDirectory, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return baseDirectory;
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
        }

        private static void AddSourceDiagnostic(ICollection<DiagnosticMessage> diagnostics, string sourceId, string code, string message)
        {
            diagnostics.Add(new DiagnosticMessage
            {
                ModuleId = sourceId ?? string.Empty,
                Severity = DiagnosticSeverity.Warning,
                Code = code ?? string.Empty,
                Message = message ?? string.Empty
            });
        }
    }

    public sealed class ModuleSourceResolutionResult
    {
        public ModuleSourceResolutionResult(ModulesConfiguration modules, IReadOnlyList<DiagnosticMessage> diagnostics)
        {
            Modules = modules ?? throw new ArgumentNullException(nameof(modules));
            Diagnostics = diagnostics ?? new List<DiagnosticMessage>();
        }

        public ModulesConfiguration Modules { get; }
        public IReadOnlyList<DiagnosticMessage> Diagnostics { get; }
    }
}
