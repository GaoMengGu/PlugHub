using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
            var text = snapshot == null
                ? "PlugHub framework is loaded, but runtime state is not available yet."
                : BuildRuntimeSummary(snapshot);

            TaskDialog.Show("PlugHub 框架状态", text);

            return Result.Succeeded;
        }

        private static Result ExecuteFeature(string featureKey, ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var decision = new FeatureExecutionGate().CanExecute(featureKey);
            if (!decision.Allowed)
            {
                message = decision.Message;
                TaskDialog.Show("PlugHub 功能已禁用", decision.Message);
                return Result.Cancelled;
            }

            var snapshot = FrameworkRuntimeState.Current;
            var feature = snapshot?.Features.FirstOrDefault(item =>
                string.Equals(item.Id, decision.FeatureId, StringComparison.OrdinalIgnoreCase));
            if (feature == null || string.IsNullOrWhiteSpace(feature.CommandType))
            {
                TaskDialog.Show("PlugHub 功能状态", BuildRuntimeSummary(snapshot!));
                return Result.Succeeded;
            }

            var assemblyPath = ResolveAssemblyPath(feature.CommandAssembly);
            if (!File.Exists(assemblyPath))
            {
                message = "Command assembly was not found: " + assemblyPath;
                return Result.Failed;
            }

            var commandType = Assembly.LoadFrom(assemblyPath).GetType(feature.CommandType, throwOnError: false);
            if (commandType == null || !typeof(IExternalCommand).IsAssignableFrom(commandType))
            {
                message = "Command type was not found or does not implement IExternalCommand: " + feature.CommandType;
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

        private static string BuildRuntimeSummary(FrameworkRuntimeSnapshot snapshot)
        {
            var activeView = snapshot.Configuration.ActiveView;
            var activePreset = snapshot.Configuration.ActivePreset;

            return
                "当前加载的是 PlugHub 单工作台。\n\n" +
                $"Workspace: {activeView.Id} / {activeView.Name}\n" +
                $"Active preset: {(activePreset == null ? "(none)" : activePreset.Id)}\n" +
                $"Config: {FrameworkRuntimeState.ConfigDirectory}\n" +
                $"Modules: {snapshot.Configuration.EffectiveModules.Modules.Count}\n" +
                $"Features in workspace: {snapshot.Composition.Features.Count}\n" +
                $"Diagnostics: {snapshot.Diagnostics.Count}\n\n" +
                "模块/功能开关会在执行前读取最新配置；Ribbon 结构、图标和按钮大小变更需要重启 Revit。\n\n" +
                "编写功能：创建一个 DLL，引用 PlugHub.Contracts，实现 IPlugHubModule，并在 Describe() 返回 ModuleDescriptor/FeatureDescriptor。\n\n" +
                "加载功能：把 DLL 放到 PlugHub.Revit2020.dll 同目录、配置的模块目录，或在 moduleSources 中声明来源。";
        }
    }
}
