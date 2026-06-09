using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Runtime;
using PlugHub.Manager.Maintenance;

namespace PlugHub.Manager
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                var maintenance = ManagerMaintenanceArguments.Parse(args);
                if (maintenance.Mode != ManagerMaintenanceMode.None)
                {
                    return new ManagerMaintenanceRunner().Run(maintenance);
                }

                var configDirectory = ResolveConfigDirectory(args);
                var singleInstance = TryAcquireSingleInstance(configDirectory);
                if (singleInstance == null)
                {
                    return 0;
                }

                try
                {
                    var hostProcessId = ReadIntOption(args, "--hostProcessId");
                    var snapshot = new FrameworkRuntime().Load(configDirectory, ShouldApplyPendingPackageOperations(hostProcessId));
                    var configuration = snapshot.Configuration.Configuration;
                    var application = new Application();
                    application.Run(new FrameworkSettingsWindow(configDirectory, configuration, hostProcessId));
                }
                finally
                {
                    singleInstance.ReleaseMutex();
                    singleInstance.Dispose();
                }

                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "PlugHub Manager", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }

        private static string ResolveConfigDirectory(string[] args)
        {
            var config = ReadOption(args, "--config");
            if (!string.IsNullOrWhiteSpace(config))
            {
                return Path.GetFullPath(config);
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
        }

        private static Mutex? TryAcquireSingleInstance(string configDirectory)
        {
            bool createdNew;
            var mutex = new Mutex(true, SingleInstanceMutexName(configDirectory), out createdNew);
            if (createdNew)
            {
                return mutex;
            }

            mutex.Dispose();
            return null;
        }

        private static string SingleInstanceMutexName(string configDirectory)
        {
            var fullConfigDirectory = Path.GetFullPath(configDirectory ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
            return @"Local\PlugHub.Manager." + StableHash(fullConfigDirectory);
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                var hash = offset;
                foreach (var ch in value ?? string.Empty)
                {
                    hash ^= ch;
                    hash *= prime;
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        private static int ReadIntOption(string[] args, string name)
        {
            var value = ReadOption(args, name);
            return int.TryParse(value, out var parsed) ? parsed : 0;
        }

        private static bool ShouldApplyPendingPackageOperations(int hostProcessId)
        {
            if (hostProcessId <= 0 || hostProcessId == Process.GetCurrentProcess().Id) return true;

            try
            {
                using (var process = Process.GetProcessById(hostProcessId))
                {
                    return process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private static string ReadOption(string[] args, string name)
        {
            if (args == null) return string.Empty;

            for (var index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index + 1 >= args.Length)
                {
                    return string.Empty;
                }

                return args[index + 1] ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
