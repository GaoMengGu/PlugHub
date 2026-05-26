using System;
using System.IO;
using Autodesk.Revit.UI;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    /// <summary>
    /// Revit 2020 外部应用入口。这里只做框架启动/关闭占位，不实现具体 Revit 业务操作。
    /// </summary>
    public sealed class ExternalApplicationEntry : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));

            var assemblyPath = typeof(ExternalApplicationEntry).Assembly.Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var configDirectory = Path.Combine(assemblyDirectory, "config");
            var runtimeSnapshot = new FrameworkRuntime().Load(assemblyDirectory, configDirectory);

            application.RegisterDockablePane(
                FrameworkSettingsPane.PaneId,
                "PlugHub 设置",
                new FrameworkSettingsPane(configDirectory));

            new FeatureRibbonBuilder(assemblyPath, assemblyDirectory).Build(
                application,
                runtimeSnapshot.Configuration.ActiveView,
                runtimeSnapshot.Composition);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));

            return Result.Succeeded;
        }
    }
}
