namespace PlugHub.Framework.Configuration
{
    internal static class DisplayNameResolver
    {
        public static string Resolve(string displayName, string name, string descriptorName, string id)
        {
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName.Trim();
            if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
            if (!string.IsNullOrWhiteSpace(descriptorName)) return descriptorName.Trim();
            return id ?? string.Empty;
        }
    }
}
