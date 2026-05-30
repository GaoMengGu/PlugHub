using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    internal static class FeatureCommandDispatcher
    {
        private static readonly ICommandAssemblyLoader CommandAssemblyLoader = new Net48DirectCommandAssemblyLoader();

        public static Result ExecuteSlot(int slotId, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!FeatureSlotRegistry.TryGetFeatureId(slotId, out var featureId))
            {
                message = "PlugHub feature slot is not assigned: " + slotId;
                ShowFailure("PlugHub 功能执行失败", message, "PH-FEATURE-SLOT", string.Empty, DiagnosticSeverity.Error);
                return Result.Cancelled;
            }

            return ExecuteFeature(featureId, commandData, ref message, elements);
        }

        public static Result ExecuteFeature(string featureKey, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var decision = new FeatureExecutionGate().CanExecuteFeatureId(featureKey);
            if (!decision.Allowed)
            {
                message = decision.Message;
                ShowFailure("PlugHub 功能已禁用", decision.Message, "PH-FEATURE-GATE", decision.FeatureId, DiagnosticSeverity.Warning);
                return Result.Cancelled;
            }

            var snapshot = FrameworkRuntimeState.Current;
            var feature = snapshot?.Features.FirstOrDefault(item =>
                string.Equals(item.Id, decision.FeatureId, StringComparison.OrdinalIgnoreCase));
            if (feature == null || string.IsNullOrWhiteSpace(feature.CommandType))
            {
                FrameworkStatusWindow.ShowRuntimeStatus(snapshot);
                return Result.Succeeded;
            }

            var assemblyPath = ResolveAssemblyPath(feature.CommandAssembly);
            if (!File.Exists(assemblyPath))
            {
                message = "Command assembly was not found: " + assemblyPath;
                ShowFailure("PlugHub 功能执行失败", message, "PH-COMMAND-ASSEMBLY", feature.ModuleId, DiagnosticSeverity.Error);
                return Result.Failed;
            }

            IExternalCommand command;
            try
            {
                command = CommandAssemblyLoader.Create(assemblyPath, feature.CommandType);
            }
            catch (Exception ex)
            {
                message = ex.Message;
                ShowFailure("PlugHub 功能执行失败", message, "PH-COMMAND-TYPE", feature.ModuleId, DiagnosticSeverity.Error);
                return Result.Failed;
            }

            return command.Execute(commandData, ref message, elements);
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
    }
}
