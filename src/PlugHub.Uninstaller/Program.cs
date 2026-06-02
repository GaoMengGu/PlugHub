using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace PlugHub.Uninstaller
{
    internal static class Program
    {
        private const string RunFromTempArgument = "/run-from-temp";
        private const string InstallDirArgument = "/installDir";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                var installDirectory = ArgumentValue(args, InstallDirArgument);
                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    installDirectory = AppDomain.CurrentDomain.BaseDirectory;
                }

                installDirectory = Path.GetFullPath(installDirectory);
                if (!HasArgument(args, RunFromTempArgument))
                {
                    StartTemporaryCopy(installDirectory);
                    return;
                }

                Application.Run(new UninstallerForm(installDirectory));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PlugHub Uninstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void StartTemporaryCopy(string installDirectory)
        {
            var currentExe = Assembly.GetExecutingAssembly().Location;
            var tempDirectory = Path.Combine(Path.GetTempPath(), "PlugHubUninstaller", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            var tempExe = Path.Combine(tempDirectory, Path.GetFileName(currentExe));
            File.Copy(currentExe, tempExe, true);

            var startInfo = new ProcessStartInfo
            {
                FileName = tempExe,
                Arguments = Quote(RunFromTempArgument) + " " + Quote(InstallDirArgument) + " " + Quote(installDirectory),
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }

        private static bool HasArgument(string[] args, string name)
        {
            return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string ArgumentValue(string[] args, string name)
        {
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return string.Empty;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
