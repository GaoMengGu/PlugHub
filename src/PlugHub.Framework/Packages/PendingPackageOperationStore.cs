using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace PlugHub.Framework.Packages
{
    public sealed class PendingPackageOperationStore
    {
        private const string PendingOperationsFileName = "pending-operations.json";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public IReadOnlyList<PendingPackageOperation> Read(string baseDirectory)
        {
            var path = PathFor(baseDirectory);
            if (!File.Exists(path))
            {
                return new List<PendingPackageOperation>();
            }

            try
            {
                var document = _serializer.Deserialize<PendingPackageOperationsDocument>(File.ReadAllText(path));
                return (document?.Operations ?? new List<PendingPackageOperation>())
                    .Where(operation => operation != null)
                    .ToList();
            }
            catch (Exception)
            {
                return new List<PendingPackageOperation>();
            }
        }

        public PendingPackageOperation? Find(string baseDirectory, string packageId, string moduleId)
        {
            return Read(baseDirectory)
                .FirstOrDefault(operation => MatchesExact(operation, packageId, moduleId));
        }

        public void AddOrReplace(string baseDirectory, PendingPackageOperation operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            var operations = Read(baseDirectory)
                .Where(item => !MatchesReplacementKey(item, operation.PackageId, operation.ModuleId))
                .ToList();
            operations.Add(operation);
            Write(baseDirectory, operations);
        }

        public void Remove(string baseDirectory, string packageId, string moduleId)
        {
            var operations = Read(baseDirectory)
                .Where(item => !MatchesExact(item, packageId, moduleId))
                .ToList();
            Write(baseDirectory, operations);
        }

        public void Write(string baseDirectory, IReadOnlyList<PendingPackageOperation> operations)
        {
            var path = PathFor(baseDirectory);
            if (operations == null || operations.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Path.GetFullPath(baseDirectory));
            File.WriteAllText(path, _serializer.Serialize(new PendingPackageOperationsDocument
            {
                Operations = operations.Where(operation => operation != null).ToList()
            }));
        }

        public string PathFor(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentException("Base directory is required.", nameof(baseDirectory));

            return Path.GetFullPath(Path.Combine(baseDirectory, "repository-cache", ".package-install", PendingOperationsFileName));
        }

        internal static bool MatchesExact(PendingPackageOperation operation, string packageId, string moduleId)
        {
            if (operation == null) return false;
            if (!string.Equals(operation.PackageId, packageId, StringComparison.OrdinalIgnoreCase)) return false;

            return string.Equals(operation.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesReplacementKey(PendingPackageOperation operation, string packageId, string moduleId)
        {
            if (operation == null) return false;
            if (!string.Equals(operation.PackageId, packageId, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrWhiteSpace(moduleId)) return true;

            return string.Equals(operation.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
