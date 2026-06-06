using PlugHub.Framework.Packages;

namespace PlugHub.Manager.Settings.Rows
{
    internal sealed class PendingPackageOperationRow
    {
        public string Operation { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string ModuleId { get; set; } = string.Empty;
        public string InstallDirectory { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;

        public static PendingPackageOperationRow FromOperation(PendingPackageOperation operation)
        {
            return new PendingPackageOperationRow
            {
                Operation = operation.Operation ?? string.Empty,
                PackageId = operation.PackageId ?? string.Empty,
                ModuleId = operation.ModuleId ?? string.Empty,
                InstallDirectory = operation.InstallDirectory ?? string.Empty,
                CreatedAtUtc = operation.CreatedAtUtc ?? string.Empty
            };
        }
    }
}
