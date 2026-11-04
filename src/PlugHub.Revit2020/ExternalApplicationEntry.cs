using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.UI;
using PlugHub.Framework.Runtime;

namespace PlugHub.Revit2020
{
    /// <summary>
    /// Revit 2020 外部应用入口。这里只做框架启动、关闭和 Ribbon 渲染，不实现具体 Revit 业务操作。
    /// </summary>
    public sealed class ExternalApplicationEntry : IExternalApplication
    {
        private static readonly PackageStartupSession StartupSession = new PackageStartupSession();

        public Result OnStartup(UIControlledApplication application)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));

            var assemblyPath = typeof(ExternalApplicationEntry).Assembly.Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var configDirectory = Path.Combine(assemblyDirectory, "config");
            try
            {
                var runtimeSnapshot = new FrameworkRuntime().Load(assemblyDirectory, configDirectory);
                StartupSession.Start(application, runtimeSnapshot.StartupModules, assemblyDirectory);
                runtimeSnapshot = new FrameworkRuntimeSnapshot(runtimeSnapshot.Configuration, runtimeSnapshot.Features,
                    runtimeSnapshot.Composition, runtimeSnapshot.Diagnostics.Concat(StartupSession.Diagnostics).ToList(), runtimeSnapshot.StartupModules);
                FrameworkRuntimeState.SetCurrent(assemblyDirectory, configDirectory, runtimeSnapshot);
                new FeatureRibbonBuilder(assemblyPath, assemblyDirectory).Build(
                    application,
                    runtimeSnapshot.Configuration.ActiveView,
                    runtimeSnapshot.Composition);
                return Result.Succeeded;
            }
            catch
            {
                StartupSession.Stop(application);
                throw;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));

            StartupSession.Stop(application);
            return Result.Succeeded;
        }
    }
}
