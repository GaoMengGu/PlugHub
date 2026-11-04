using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Diagnostics;

namespace PlugHub.Revit2020
{
    internal sealed class PackageStartupSession
    {
        private readonly Func<string, string, Assembly> _loadAssembly;
        private readonly HashSet<string> _attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<KeyValuePair<string, IExternalApplication>> _started = new List<KeyValuePair<string, IExternalApplication>>();
        private readonly List<DiagnosticMessage> _diagnostics = new List<DiagnosticMessage>();
        private readonly PlugHubLogger _logger = new PlugHubLogger();
        private bool _startupAttempted;
        private bool _closed;
        private string _baseDirectory = string.Empty;

        internal PackageStartupSession(Func<string, string, Assembly>? loadAssembly = null)
        {
            _loadAssembly = loadAssembly ?? Net48ShadowCopyCommandAssemblyLoader.LoadAssembly;
        }

        internal IReadOnlyList<DiagnosticMessage> Diagnostics => _diagnostics;

        internal void Start(UIControlledApplication application, IEnumerable<ModuleConfiguration> modules, string baseDirectory)
        {
            if (_startupAttempted || _closed) return;
            _startupAttempted = true;
            _baseDirectory = baseDirectory;

            foreach (var module in modules.Where(module => module != null && (module.AutoStart || !string.IsNullOrWhiteSpace(module.StartupConfigurationError)))
                .OrderBy(module => module.Order).ThenBy(module => module.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (!_attempted.Add(module.Id)) continue;
                StartModule(application, module);
            }
        }

        internal void Stop(UIControlledApplication application)
        {
            if (_closed) return;
            _closed = true;
            for (var index = _started.Count - 1; index >= 0; index--)
            {
                Cleanup(application, _started[index].Key, _started[index].Value);
            }
            _started.Clear();
        }

        private void StartModule(UIControlledApplication application, ModuleConfiguration module)
        {
            var timer = Stopwatch.StartNew();
            var stage = "configuration";
            IExternalApplication? instance = null;
            var enteredStartup = false;
            try
            {
                if (!string.IsNullOrWhiteSpace(module.StartupConfigurationError)) throw new InvalidOperationException(module.StartupConfigurationError);
                if (string.IsNullOrWhiteSpace(module.Assembly) || string.IsNullOrWhiteSpace(module.StartupType))
                    throw new InvalidOperationException("autoStart requires assembly and startupType.");

                stage = "load";
                var sourceDirectory = string.IsNullOrWhiteSpace(module.ResolvedBaseDirectory) ? _baseDirectory : module.ResolvedBaseDirectory;
                var assemblyPath = Path.GetFullPath(Path.IsPathRooted(module.Assembly) ? module.Assembly : Path.Combine(sourceDirectory, module.Assembly));
                if (!File.Exists(assemblyPath)) throw new FileNotFoundException("Startup assembly was not found.", assemblyPath);
                var assembly = _loadAssembly(assemblyPath, _baseDirectory);

                stage = "type";
                var type = assembly.GetType(module.StartupType, throwOnError: false);
                if (type == null || !type.IsVisible || type.IsAbstract || type.ContainsGenericParameters
                    || !typeof(IExternalApplication).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
                    throw new InvalidOperationException("startupType must be a public, concrete IExternalApplication with a public parameterless constructor: " + module.StartupType);

                stage = "construct";
                instance = (IExternalApplication)Activator.CreateInstance(type)!;
                stage = "OnStartup";
                enteredStartup = true;
                var result = instance.OnStartup(application);
                Report(module.Id, stage, result.ToString(), timer, result == Result.Succeeded ? DiagnosticSeverity.Info : DiagnosticSeverity.Error);
                if (result == Result.Succeeded)
                    _started.Add(new KeyValuePair<string, IExternalApplication>(module.Id, instance));
                else
                    Cleanup(application, module.Id, instance);
            }
            catch (Exception exception)
            {
                Report(module.Id, stage, "Exception", timer, DiagnosticSeverity.Error, exception);
                if (enteredStartup && instance != null) Cleanup(application, module.Id, instance);
            }
        }

        private void Cleanup(UIControlledApplication application, string moduleId, IExternalApplication instance)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var result = instance.OnShutdown(application);
                Report(moduleId, "OnShutdown", result.ToString(), timer, result == Result.Succeeded ? DiagnosticSeverity.Info : DiagnosticSeverity.Error);
            }
            catch (Exception exception)
            {
                Report(moduleId, "OnShutdown", "Exception", timer, DiagnosticSeverity.Error, exception);
            }
        }

        private void Report(string moduleId, string stage, string result, Stopwatch timer, DiagnosticSeverity severity, Exception? exception = null)
        {
            var code = stage == "OnShutdown" ? "PH-SHUTDOWN-RESULT" : "PH-STARTUP-RESULT";
            var message = SensitiveTextRedactor.Redact("stage=" + stage + "; result=" + result + "; elapsedMs=" + timer.ElapsedMilliseconds
                + (exception == null ? string.Empty : "; " + exception.GetBaseException().Message));
            _diagnostics.Add(new DiagnosticMessage { ModuleId = moduleId, Code = code, Severity = severity, Message = message });
            _logger.Write(_baseDirectory, new PlugHubLogEntry
            {
                ModuleId = moduleId, Code = code, Severity = severity, Operation = "Package." + stage,
                Message = message, Exception = exception?.ToString() ?? string.Empty
            });
        }
    }
}
