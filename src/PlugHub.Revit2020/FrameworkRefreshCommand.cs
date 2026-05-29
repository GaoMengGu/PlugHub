using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    [Transaction(TransactionMode.Manual)]
    public sealed class FrameworkRefreshCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var snapshot = FrameworkRuntimeState.Refresh();
                FrameworkStatusWindow.ShowRefreshResult(snapshot);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                FrameworkStatusWindow.ShowLogs(
                    "PlugHub 刷新配置",
                    "刷新失败：" + ex.Message,
                    new[]
                    {
                        new DiagnosticMessage
                        {
                            Severity = DiagnosticSeverity.Error,
                            Code = "PH-REFRESH",
                            ModuleId = "runtime",
                            Message = ex.Message
                        }
                    });
                return Result.Failed;
            }
        }
    }
}
