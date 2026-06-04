using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Contracts.Modules;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Packages
{
    public sealed class PackageInstallService
    {
        private const string DefaultPackageManifestName = "packages.json";

        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private readonly PackageManifestReader _manifestReader;

        public PackageInstallService(PackageManifestReader manifestReader)
        {
            _manifestReader = manifestReader ?? throw new ArgumentNullException(nameof(manifestReader));
        }

        public PackageRepositoryOperationResult InstallPackagePayload(RepositoryPackageDescriptor package, string stagingDirectory)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (string.IsNullOrWhiteSpace(stagingDirectory)) throw new ArgumentException("Staging directory is required.", nameof(stagingDirectory));

            return CopyPackagePayload(package, stagingDirectory);
        }

        private PackageRepositoryOperationResult CopyPackagePayload(RepositoryPackageDescriptor package, string installDirectory)
        {
            if (!_manifestReader.TryReadManifest(package.ManifestPath, out var root, out var modules))
            {
                return PackageRepositoryOperationResult.Failed("Packages manifest could not be read: " + package.ManifestPath);
            }

            var module = _manifestReader.FindModule(modules, package);
            if (module == null)
            {
                return PackageRepositoryOperationResult.Failed("Package module was not found in manifest: " + package.ModuleId);
            }

            var moduleObject = _manifestReader.FindModuleObject(root, module.Id);
            if (moduleObject == null)
            {
                return PackageRepositoryOperationResult.Failed("Package module was not found in manifest: " + package.ModuleId);
            }

            Directory.CreateDirectory(installDirectory);
            WriteSingleModuleManifest(root, moduleObject, Path.Combine(installDirectory, DefaultPackageManifestName));
            foreach (var relativePath in PayloadPaths(module))
            {
                if (!CopyPayloadFile(package.SourceDirectory, installDirectory, relativePath, out var error))
                {
                    return PackageRepositoryOperationResult.Failed(error);
                }
            }

            return PackageRepositoryOperationResult.Succeeded("Package payload installed.");
        }

        private void WriteSingleModuleManifest(Dictionary<string, object> root, Dictionary<string, object> moduleObject, string targetManifestPath)
        {
            var manifest = new Dictionary<string, object>
            {
                ["schemaVersion"] = FirstNonEmpty(PackageManifestReader.StringValue(root, "schemaVersion"), "1.0"),
                ["modules"] = new object[] { moduleObject }
            };
            CopyOptionalManifestValue(root, manifest, "revitVersions");
            CopyOptionalManifestValue(root, manifest, "frameworkVersionRange");

            File.WriteAllText(targetManifestPath, _serializer.Serialize(manifest));
        }

        private static void CopyOptionalManifestValue(Dictionary<string, object> source, Dictionary<string, object> target, string key)
        {
            if (PackageManifestReader.TryGetValue(source, key, out var value))
            {
                target[key] = value;
            }
        }

        private static IEnumerable<string> PayloadPaths(ModuleConfiguration module)
        {
            var paths = new List<string>();
            AddPayloadPath(paths, module.Assembly);
            foreach (var feature in module.Features ?? new List<FeatureConfiguration>())
            {
                AddPayloadPath(paths, feature.CommandAssembly);
                AddPayloadPath(paths, feature.IconPath);
            }

            return paths.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void AddPayloadPath(ICollection<string> paths, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (Path.IsPathRooted(path)) return;
            paths.Add(path.Trim());
        }

        private static bool CopyPayloadFile(string sourceDirectory, string installDirectory, string relativePath, out string error)
        {
            error = string.Empty;
            var sourceFile = Path.GetFullPath(Path.Combine(sourceDirectory, relativePath));
            var targetFile = Path.GetFullPath(Path.Combine(installDirectory, relativePath));
            if (!IsUnderDirectory(sourceDirectory, sourceFile))
            {
                error = "Package payload path is outside the repository package directory: " + relativePath;
                return false;
            }

            if (!IsUnderDirectory(installDirectory, targetFile))
            {
                error = "Package payload path is outside the install directory: " + relativePath;
                return false;
            }

            if (!File.Exists(sourceFile))
            {
                error = "Package payload file was not found: " + sourceFile;
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? installDirectory);
            File.Copy(sourceFile, targetFile, true);
            return true;
        }

        private static bool IsUnderDirectory(string parentDirectory, string childPath)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
