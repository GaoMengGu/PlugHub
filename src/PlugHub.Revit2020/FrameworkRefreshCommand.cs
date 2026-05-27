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
                FrameworkStatusWindow.ShowDialog(
                    "PlugHub 刷新配置",
                    "运行时配置已重新读取。模块/功能开关、模块来源和执行拦截会使用最新配置；Ribbon 布局、图标和按钮大小仍需重启 Revit 重绘。\n\n" +
                    FrameworkStatusWindow.BuildRuntimeSummary(snapshot),
                    snapshot.Diagnostics);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                FrameworkStatusWindow.ShowDialog(
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
