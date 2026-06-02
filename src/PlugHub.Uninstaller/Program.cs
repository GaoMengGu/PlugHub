using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace PlugHub.Uninstaller
{
    internal static class Program
    {
        private const string RunFromTempArgument = "/run-from-temp";
        private const string InstallDirArgument = "/installDir";
        private const string EncodedInstallDirArgument = "/installDirBase64";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                var installDirectory = DecodeArgumentValue(args, EncodedInstallDirArgument);
                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    installDirectory = ArgumentValue(args, InstallDirArgument);
                }

                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    installDirectory = AppDomain.CurrentDomain.BaseDirectory;
                }

                installDirectory = Path.GetFullPath(NormalizePathArgument(installDirectory));
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
                Arguments = Quote(RunFromTempArgument) + " " + Quote(EncodedInstallDirArgument) + " " + Quote(EncodeArgument(installDirectory)),
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

        private static string DecodeArgumentValue(string[] args, string name)
        {
            var value = ArgumentValue(args, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(NormalizePathArgument(value)));
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("Invalid encoded install directory argument.", ex);
            }
        }

        private static string EncodeArgument(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string NormalizePathArgument(string value)
        {
            return TrimWrappingQuotes((value ?? string.Empty).Trim());
        }

        private static string TrimWrappingQuotes(string value)
        {
            while (value.Length >= 2
                && ((value[0] == '"' && value[value.Length - 1] == '"')
                    || (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                value = value.Substring(1, value.Length - 2).Trim();
            }

            return value;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
