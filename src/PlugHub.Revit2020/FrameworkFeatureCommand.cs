using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
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
                return FeatureCommandDispatcher.ExecuteFeature(featureKey, commandData, ref message, elements);
            }

            var snapshot = FrameworkRuntimeState.Current;
            FrameworkStatusWindow.ShowRuntimeStatus(snapshot);
            return Result.Succeeded;
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
    }
}
