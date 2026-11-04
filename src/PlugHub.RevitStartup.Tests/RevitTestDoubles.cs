using System;
using System.Collections.Generic;
using PlugHub.Framework.Composition;
using PlugHub.Framework.Configuration;

// These substitutes exercise managed dispatch only; the adapter is also compiled against the real API references.
namespace Autodesk.Revit.UI
{
    public enum Result { Succeeded, Cancelled, Failed }
    public interface IExternalCommand { }
    public interface IExternalApplication
    {
        Result OnStartup(UIControlledApplication application);
        Result OnShutdown(UIControlledApplication application);
    }
    public sealed class UIControlledApplication
    {
        public readonly List<string> Calls = new List<string>();
        public readonly List<int> ThreadIds = new List<int>();
    }
}

namespace PlugHub.Revit2020
{
    internal sealed class FrameworkFeatureCommand { }
    internal sealed class FeatureRibbonBuilder
    {
        internal static Action? OnBuild;
        internal FeatureRibbonBuilder(string assemblyPath, string assemblyDirectory) { }
        internal void Build(Autodesk.Revit.UI.UIControlledApplication application, ViewConfiguration view, FeatureViewCompositionResult composition)
        {
            OnBuild?.Invoke();
        }
    }
}
