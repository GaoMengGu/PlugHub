using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using PlugHub.Framework.Updates;

namespace PlugHub.Manager.Maintenance
{
    internal static class ManagerMaintenanceLauncher
    {
        private const string ManagerFileName = "PlugHub.Manager.exe";

        public static FrameworkUpdateOperationResult StartUpdate(
            string installDirectory,
            string packagePath,
            string targetVersion,
            IEnumerable<int> waitProcessIds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                {
                    throw new FileNotFoundException("框架更新包不存在。", packagePath);
                }

                var temporaryManagerPath = CreateTemporaryManagerCopy(installDirectory);
                var arguments = BuildArguments(
                    "/update",
                    new Dictionary<string, string>
                    {
                        { "/payloadZipBase64", ToBase64(packagePath) },
                        { "/installDirBase64", ToBase64(installDirectory) },
                        { "/targetVersionBase64", ToBase64(targetVersion) }
                    },
                    waitProcessIds);

                Process.Start(new ProcessStartInfo
                {
                    FileName = temporaryManagerPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(temporaryManagerPath) ?? string.Empty
                });

                return new FrameworkUpdateOperationResult
                {
                    Success = true,
                    Message = "框架更新已准备好。PlugHub Manager 将关闭；关闭 Revit 后会自动完成覆盖。"
                };
            }
            catch (Exception ex)
            {
                return new FrameworkUpdateOperationResult
                {
                    Success = false,
                    Message = "启动框架更新失败：" + ex.Message
                };
            }
        }

        public static FrameworkUpdateOperationResult StartUninstall(string installDirectory, IEnumerable<int> waitProcessIds)
        {
            try
            {
                var temporaryManagerPath = CreateTemporaryManagerCopy(installDirectory);
                var arguments = BuildArguments(
                    "/uninstall",
                    new Dictionary<string, string>
                    {
                        { "/installDirBase64", ToBase64(installDirectory) }
                    },
                    waitProcessIds);

                Process.Start(new ProcessStartInfo
                {
                    FileName = temporaryManagerPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(temporaryManagerPath) ?? string.Empty
                });

                return new FrameworkUpdateOperationResult
                {
                    Success = true,
                    Message = "已打开 PlugHub Manager 卸载维护流程。当前 Manager 将关闭。"
                };
            }
            catch (Exception ex)
            {
                return new FrameworkUpdateOperationResult
                {
                    Success = false,
                    Message = "启动卸载维护流程失败：" + ex.Message
                };
            }
        }

        private static string CreateTemporaryManagerCopy(string installDirectory)
        {
            var sourceDirectory = Path.GetFullPath(installDirectory ?? string.Empty);
            var currentExe = Assembly.GetExecutingAssembly().Location;
            var managerSource = Path.Combine(sourceDirectory, ManagerFileName);
            if (!File.Exists(managerSource))
            {
                managerSource = currentExe;
            }

            if (!File.Exists(managerSource))
            {
                throw new FileNotFoundException("PlugHub Manager was not found.", managerSource);
            }

            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "PlugHub", "manager-maintenance", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);

            CopyIfExists(managerSource, Path.Combine(temporaryDirectory, ManagerFileName));
            CopyIfExists(managerSource + ".config", Path.Combine(temporaryDirectory, ManagerFileName + ".config"));

            if (Directory.Exists(sourceDirectory))
            {
                foreach (var file in Directory.GetFiles(sourceDirectory, "PlugHub.*.dll"))
                {
                    CopyIfExists(file, Path.Combine(temporaryDirectory, Path.GetFileName(file)));
                }
            }

            return Path.Combine(temporaryDirectory, ManagerFileName);
        }

        private static string BuildArguments(string mode, IDictionary<string, string> values, IEnumerable<int> waitProcessIds)
        {
            var parts = new List<string> { Quote(mode) };
            foreach (var pair in values)
            {
                parts.Add(Quote(pair.Key));
                parts.Add(Quote(pair.Value));
            }

            foreach (var processId in (waitProcessIds ?? Enumerable.Empty<int>()).Distinct())
            {
                if (processId <= 0) continue;
                parts.Add(Quote("/waitProcessId"));
                parts.Add(Quote(processId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            return string.Join(" ", parts);
        }

        private static void CopyIfExists(string source, string target)
        {
            if (!File.Exists(source)) return;
            File.Copy(source, target, true);
        }

        private static string ToBase64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
