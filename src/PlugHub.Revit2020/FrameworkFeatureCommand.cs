using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    [Transaction(TransactionMode.Manual)]
    public sealed class FrameworkFeatureCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var featureKey = FeatureKeyFromJournal(commandData);
            if (!string.IsNullOrWhiteSpace(featureKey))
            {
                return ExecuteFeature(featureKey, commandData, ref message, elements);
            }

            var snapshot = FrameworkRuntimeState.Current;
            FrameworkStatusWindow.ShowDialog("PlugHub 框架状态", FrameworkStatusWindow.BuildRuntimeSummary(snapshot), snapshot?.Diagnostics ?? Array.Empty<DiagnosticMessage>());
            return Result.Succeeded;
        }

        private static Result ExecuteFeature(string featureKey, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var decision = new FeatureExecutionGate().CanExecute(featureKey);
            if (!decision.Allowed)
            {
                message = decision.Message;
                FrameworkStatusWindow.ShowDialog(
                    "PlugHub 功能已禁用",
                    decision.Message,
                    new[]
                    {
                        new DiagnosticMessage
                        {
                            Severity = DiagnosticSeverity.Warning,
                            Code = "PH-FEATURE-GATE",
                            ModuleId = decision.FeatureId,
                            Message = decision.Message
                        }
                    });
                return Result.Cancelled;
            }

            var snapshot = FrameworkRuntimeState.Current;
            var feature = snapshot?.Features.FirstOrDefault(item =>
                string.Equals(item.Id, decision.FeatureId, StringComparison.OrdinalIgnoreCase));
            if (feature == null || string.IsNullOrWhiteSpace(feature.CommandType))
            {
                FrameworkStatusWindow.ShowDialog("PlugHub 功能状态", FrameworkStatusWindow.BuildRuntimeSummary(snapshot), snapshot?.Diagnostics ?? Array.Empty<DiagnosticMessage>());
                return Result.Succeeded;
            }

            var assemblyPath = ResolveAssemblyPath(feature.CommandAssembly);
            if (!File.Exists(assemblyPath))
            {
                message = "Command assembly was not found: " + assemblyPath;
                FrameworkStatusWindow.ShowDialog(
                    "PlugHub 功能执行失败",
                    message,
                    new[]
                    {
                        new DiagnosticMessage
                        {
                            Severity = DiagnosticSeverity.Error,
                            Code = "PH-COMMAND-ASSEMBLY",
                            ModuleId = feature.ModuleId,
                            Message = message
                        }
                    });
                return Result.Failed;
            }

            var commandType = Assembly.LoadFrom(assemblyPath).GetType(feature.CommandType, throwOnError: false);
            if (commandType == null || !typeof(IExternalCommand).IsAssignableFrom(commandType))
            {
                message = "Command type was not found or does not implement IExternalCommand: " + feature.CommandType;
                FrameworkStatusWindow.ShowDialog(
                    "PlugHub 功能执行失败",
                    message,
                    new[]
                    {
                        new DiagnosticMessage
                        {
                            Severity = DiagnosticSeverity.Error,
                            Code = "PH-COMMAND-TYPE",
                            ModuleId = feature.ModuleId,
                            Message = message
                        }
                    });
                return Result.Failed;
            }

            var command = (IExternalCommand)Activator.CreateInstance(commandType)!;
            return command.Execute(commandData, ref message, elements);
        }

        private static string FeatureKeyFromJournal(ExternalCommandData commandData)
        {
            try
            {
                if (commandData?.JournalData != null && commandData.JournalData.ContainsKey("PlugHubFeature"))
                {
                    return commandData.JournalData["PlugHubFeature"];
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static string ResolveAssemblyPath(string commandAssembly)
        {
            if (string.IsNullOrWhiteSpace(commandAssembly)) return typeof(FrameworkFeatureCommand).Assembly.Location;
            return Path.IsPathRooted(commandAssembly)
                ? commandAssembly
                : Path.GetFullPath(Path.Combine(FrameworkRuntimeState.BaseDirectory, commandAssembly));
        }

    }
}
