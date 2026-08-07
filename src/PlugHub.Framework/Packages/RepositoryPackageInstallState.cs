using System;

namespace PlugHub.Framework.Packages
{
    public static class RepositoryPackageInstallState
    {
        public static string Resolve(
            bool isInstalled,
            string version,
            string installedVersion,
            string pendingOperation,
            bool isRevitHostRunning,
            bool isLoadedInCurrentRuntime)
        {
            if (string.Equals(pendingOperation, "delete", StringComparison.OrdinalIgnoreCase)) return "待重启卸载";
            if (string.Equals(pendingOperation, "update", StringComparison.OrdinalIgnoreCase)) return "待重启更新";
            if (string.Equals(pendingOperation, "restart", StringComparison.OrdinalIgnoreCase) && isInstalled) return "已安装待重启";
            if (isRevitHostRunning && !isInstalled && isLoadedInCurrentRuntime) return "待重启卸载";
            if (!isInstalled) return "未安装";
            if (isRevitHostRunning && !isLoadedInCurrentRuntime) return "已安装待重启";
            if (!string.IsNullOrWhiteSpace(version)
                && !string.IsNullOrWhiteSpace(installedVersion)
                && !string.Equals(version, installedVersion, StringComparison.OrdinalIgnoreCase))
            {
                return "可更新";
            }

            return "已安装";
        }
    }
}
