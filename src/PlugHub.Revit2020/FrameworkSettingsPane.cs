using System;
using System.Windows.Forms.Integration;
using Autodesk.Revit.UI;
using PlugHub.Framework.Configuration;

namespace PlugHub.Revit2020
{
    internal sealed class FrameworkSettingsPane : IDockablePaneProvider
    {
        public static readonly DockablePaneId PaneId = new DockablePaneId(new Guid("4D6C932A-151E-4F31-92EE-F02D84CB16A0"));
        private readonly string _configDirectory;

        public FrameworkSettingsPane(string configDirectory)
        {
            _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            var configuration = FrameworkConfigurationLoader.LoadFromDirectory(_configDirectory);
            var form = new FrameworkSettingsForm(_configDirectory, configuration)
            {
                TopLevel = false,
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.None,
                Dock = System.Windows.Forms.DockStyle.Fill
            };

            data.FrameworkElement = new WindowsFormsHost { Child = form };
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };

            form.Show();
        }
    }
}
