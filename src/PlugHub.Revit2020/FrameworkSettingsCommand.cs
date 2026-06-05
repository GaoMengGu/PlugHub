using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    [Transaction(TransactionMode.Manual)]
    public sealed class FrameworkSettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var configDirectory = FrameworkRuntimeState.ConfigDirectory;
            if (string.IsNullOrWhiteSpace(configDirectory))
            {
                FrameworkStatusWindow.ShowLogs(
                    "PlugHub 设置",
                    "未找到运行时配置目录，请确认框架已正常启动。",
                    new[]
                    {
                        new DiagnosticMessage
                        {
                            Severity = DiagnosticSeverity.Warning,
                            Code = "PH-SETTINGS",
                            ModuleId = "runtime",
                            Message = "PlugHub runtime configuration directory is empty."
                        }
                    });
                return Result.Cancelled;
            }

            try
            {
                if (new ExternalSettingsAppLauncher().TryLaunch(configDirectory, out var diagnostic))
                {
                    return Result.Succeeded;
                }

                message = diagnostic;
                FrameworkStatusWindow.ShowLogs(
                    "PlugHub 设置",
                    "打开 Windows 设置程序失败：" + diagnostic,
                    new[]
                    {
                        new DiagnosticMessage
                        {
                            Severity = DiagnosticSeverity.Error,
                            Code = "PH-SETTINGS-APP",
                            ModuleId = "runtime",
                            Message = diagnostic
                        }
                    });
                return Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                FrameworkStatusWindow.ShowLogs(
                    "PlugHub 设置",
                    "打开设置失败：" + ex.Message,
                    new[]
                    {
                        new DiagnosticMessage
                        {
                            Severity = DiagnosticSeverity.Error,
                            Code = "PH-SETTINGS",
                            ModuleId = "runtime",
                            Message = ex.Message
                        }
                    });
                return Result.Failed;
            }
        }
    }
}
