using System;
using System.Diagnostics;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PlugHub.Framework.Configuration;
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
                TaskDialog.Show("PlugHub 设置", "未找到运行时配置目录，请确认框架已正常启动。");
                return Result.Cancelled;
            }

            try
            {
                try
                {
                    var pane = commandData.Application.GetDockablePane(FrameworkSettingsPane.PaneId);
                    if (pane != null)
                    {
                        if (pane.IsShown())
                        {
                            pane.Hide();
                        }
                        else
                        {
                            pane.Show();
                        }

                        return Result.Succeeded;
                    }
                }
                catch (Exception)
                {
                    // Older or partially loaded sessions can fall back to the modal form.
                }

                var configuration = FrameworkConfigurationLoader.LoadFromDirectory(configDirectory);
                using (var form = new FrameworkSettingsForm(configDirectory, configuration))
                {
                    form.ShowDialog(new WindowHandle(Process.GetCurrentProcess().MainWindowHandle));
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("PlugHub 设置", ex.Message);
                return Result.Failed;
            }
        }

        private sealed class WindowHandle : IWin32Window
        {
            public WindowHandle(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; }
        }
    }
}
