using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    [Transaction(TransactionMode.Manual)]
    public sealed class FrameworkStatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            FrameworkStatusWindow.ShowRuntimeStatus(FrameworkRuntimeState.Current);
            return Result.Succeeded;
        }
    }
}
