using System.Collections.Generic;

namespace PlugHub.Framework.Packages
{
    public sealed class PendingPackageOperationsDocument
    {
        public List<PendingPackageOperation> Operations { get; set; } = new List<PendingPackageOperation>();
    }
}
