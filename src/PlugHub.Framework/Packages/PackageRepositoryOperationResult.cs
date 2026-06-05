namespace PlugHub.Framework.Packages
{
    public sealed class PackageRepositoryOperationResult
    {
        private PackageRepositoryOperationResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }

        public static PackageRepositoryOperationResult Succeeded(string message)
        {
            return new PackageRepositoryOperationResult(true, message);
        }

        public static PackageRepositoryOperationResult Failed(string message)
        {
            return new PackageRepositoryOperationResult(false, message);
        }
    }
}
