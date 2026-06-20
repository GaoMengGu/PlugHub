using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Diagnostics;
using PlugHub.Framework.Runtime;
using PlugHub.Wpf;

namespace PlugHub.Revit2020
{
    internal static class FeatureCommandDispatcher
    {
        private static readonly ICommandAssemblyLoader CommandAssemblyLoader = new Net48ShadowCopyCommandAssemblyLoader();

        public static Result ExecuteSlot(int slotId, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!FeatureSlotRegistry.TryGetFeatureId(slotId, out var featureId))
            {
                message = "PlugHub feature slot is not assigned: " + slotId;
                LogCommand(DiagnosticSeverity.Error, "PH-FEATURE-SLOT", string.Empty, string.Empty, "FeatureCommandDispatcher.ExecuteSlot", message);
                ShowFailure("PlugHub 功能执行失败", message, "PH-FEATURE-SLOT", string.Empty, DiagnosticSeverity.Error);
                return Result.Cancelled;
            }

            var decision = new FeatureExecutionGate().CanExecuteFeatureId(featureId);
            return ExecuteFeatureByDecision(decision, commandData, ref message, elements);
        }

        public static Result ExecuteFeature(string featureKey, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var decision = new FeatureExecutionGate().CanExecute(featureKey);
            return ExecuteFeatureByDecision(decision, commandData, ref message, elements);
        }

        private static Result ExecuteFeatureByDecision(FeatureExecutionDecision decision, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!decision.Allowed)
            {
                message = decision.Message;
                LogCommand(DiagnosticSeverity.Warning, "PH-FEATURE-GATE", string.Empty, decision.FeatureId, "FeatureCommandDispatcher.Execute", decision.Message);
                ShowFailure("PlugHub 功能已禁用", decision.Message, "PH-FEATURE-GATE", decision.FeatureId, DiagnosticSeverity.Warning);
                return Result.Cancelled;
            }

            var snapshot = FrameworkRuntimeState.Current;
            var feature = snapshot?.Features.FirstOrDefault(item =>
                string.Equals(item.Id, decision.FeatureId, StringComparison.OrdinalIgnoreCase));
            if (feature == null || string.IsNullOrWhiteSpace(feature.CommandType))
            {
                LogCommand(DiagnosticSeverity.Info, "PH-COMMAND-NOOP", string.Empty, decision.FeatureId, "FeatureCommandDispatcher.Execute", "Feature has no command type. Showing runtime status.");
                FrameworkStatusWindow.ShowRuntimeStatus(snapshot);
                return Result.Succeeded;
            }

            var assemblyPath = ResolveAssemblyPath(feature.CommandAssembly);
            if (!File.Exists(assemblyPath))
            {
                message = "Command assembly was not found: " + assemblyPath;
                LogCommand(DiagnosticSeverity.Error, "PH-COMMAND-ASSEMBLY", feature.ModuleId, feature.Id, "FeatureCommandDispatcher.ResolveAssembly", message);
                ShowFailure("PlugHub 功能执行失败", message, "PH-COMMAND-ASSEMBLY", feature.ModuleId, DiagnosticSeverity.Error);
                return Result.Failed;
            }

            IExternalCommand command;
            try
            {
                command = CommandAssemblyLoader.Create(assemblyPath, feature.CommandType, FrameworkRuntimeState.BaseDirectory);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                new PlugHubLogger().Error(FrameworkRuntimeState.BaseDirectory, "PH-COMMAND-TYPE", feature.ModuleId, feature.Id, "FeatureCommandDispatcher.CreateCommand", message, ex);
                ShowFailure("PlugHub 功能执行失败", message, "PH-COMMAND-TYPE", feature.ModuleId, DiagnosticSeverity.Error);
                return Result.Failed;
            }

            try
            {
                LogCommand(DiagnosticSeverity.Info, "PH-COMMAND-START", feature.ModuleId, feature.Id, "FeatureCommandDispatcher.Execute", "Starting feature command: " + feature.CommandType);
                var result = command.Execute(commandData, ref message, elements);
                LogCommand(
                    result == Result.Succeeded ? DiagnosticSeverity.Info : DiagnosticSeverity.Warning,
                    "PH-COMMAND-RESULT",
                    feature.ModuleId,
                    feature.Id,
                    "FeatureCommandDispatcher.Execute",
                    "Feature command completed with result: " + result + ". " + (message ?? string.Empty));
                return result;
            }
            catch (Exception ex)
            {
                message = "插件功能执行时发生异常，已记录到 PlugHub 日志。";
                new PlugHubLogger().Error(FrameworkRuntimeState.BaseDirectory, "PH-COMMAND-EXECUTE", feature.ModuleId, feature.Id, "FeatureCommandDispatcher.Execute", message, ex);
                try
                {
                    ShowFailure("PlugHub 功能执行失败", message, "PH-COMMAND-EXECUTE", feature.ModuleId, DiagnosticSeverity.Error);
                }
                catch
                {
                }

                return Result.Failed;
            }
        }

        private static string ResolveAssemblyPath(string commandAssembly)
        {
            if (string.IsNullOrWhiteSpace(commandAssembly)) return typeof(FrameworkFeatureCommand).Assembly.Location;
            return Path.IsPathRooted(commandAssembly)
                ? commandAssembly
                : Path.GetFullPath(Path.Combine(FrameworkRuntimeState.BaseDirectory, commandAssembly));
        }

        private static void ShowFailure(string title, string failureMessage, string code, string moduleId, DiagnosticSeverity severity)
        {
            FrameworkStatusWindow.ShowLogs(
                title,
                failureMessage,
                new[]
                {
                    new DiagnosticMessage
                    {
                        Severity = severity,
                        Code = code,
                        ModuleId = moduleId ?? string.Empty,
                        Message = failureMessage ?? string.Empty
                    }
                });
        }

        private static void LogCommand(DiagnosticSeverity severity, string code, string moduleId, string featureId, string operation, string commandMessage)
        {
            new PlugHubLogger().Write(FrameworkRuntimeState.BaseDirectory, new PlugHubLogEntry
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ModuleId = moduleId ?? string.Empty,
                FeatureId = featureId ?? string.Empty,
                Operation = operation ?? string.Empty,
                Message = commandMessage ?? string.Empty
            });
        }
    }
}
