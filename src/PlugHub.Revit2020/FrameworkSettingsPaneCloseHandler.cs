using System;
using Autodesk.Revit.UI;

namespace PlugHub.Revit2020
{
    internal sealed class FrameworkSettingsPaneCloseHandler : IExternalEventHandler
    {
        private readonly DockablePaneId _paneId;

        public FrameworkSettingsPaneCloseHandler(DockablePaneId paneId)
        {
            _paneId = paneId ?? throw new ArgumentNullException(nameof(paneId));
        }

        public void Execute(UIApplication app)
        {
            var pane = app.GetDockablePane(_paneId);
            pane.Hide();
        }

        public string GetName()
        {
            return "PlugHub Settings Pane Close";
        }
    }
}
