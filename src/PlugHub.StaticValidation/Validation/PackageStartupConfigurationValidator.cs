using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Packages;
using PlugHub.Framework.Runtime;
using PlugHub.Framework.Settings;

namespace PlugHub.StaticValidation.Validation
{
    internal sealed class PackageStartupConfigurationValidator
    {
        private readonly ValidationSource _source;
        public PackageStartupConfigurationValidator(ValidationSource source) { _source = source; }

        public void Validate()
        {
            var directory = _source.FullPath(".tmp/startup-configuration-" + Guid.NewGuid().ToString("N"));
            try
            {
                var config = Path.Combine(directory, "config");
                var installed = Path.Combine(directory, "packages", "startup");
                Directory.CreateDirectory(config);
                Directory.CreateDirectory(installed);
                var manifest = Path.Combine(installed, "packages.json");
                var json = new JavaScriptSerializer();
                var document = new { schemaVersion = "1.0", modules = new object[] {
                    Module("no-declaration", 8),
                    Module("false", 7, false),
                    Module("b", 2, true),
                    Module("a", 2, true),
                    Module("a", 4, true, "Duplicate.Startup"),
                    Module("incompatible", 1, true, revit: "2024"),
                    Module("invalid-string", 5, "true"),
                    Module("invalid-number", 6, 1),
                    Module("invalid-object", 9, new { value = true }),
                    Module("missing-entry", 10, true, ""),
                    Module("invalid-type", 11, true, 1),
                    new { id = "no-button", order = 12, autoStart = true, startupType = "Example.Startup", assembly = "Plugin.dll", features = new object[0] }
                }};
                File.WriteAllText(manifest, json.Serialize(document));
                File.WriteAllText(Path.Combine(config, "views.json"), "{\"views\":[{\"id\":\"workspace\",\"excludeTags\":[\"layout-filter\"]}]}");
                // Remote indexes are not installed sources and must not become startup candidates.
                Directory.CreateDirectory(Path.Combine(directory, "repository-cache", "remote"));
                File.WriteAllText(Path.Combine(directory, "repository-cache", "remote", "packages.json"),
                    json.Serialize(new { schemaVersion = "1.0", modules = new[] { Module("remote", 0, true) } }));

                var snapshot = new FrameworkRuntime().Load(directory, config, false);
                Require(snapshot.StartupModules.Select(module => module.Id).SequenceEqual(new[] {
                    "a", "b", "invalid-string", "invalid-number", "false", "no-declaration", "invalid-object", "missing-entry", "invalid-type", "no-button"
                }), "startup candidates must use compatible registry winners in Order/ID order, independent from Ribbon.");
                Require(snapshot.Composition.Features.Count == 0 && snapshot.StartupModules.Count > 0, "layout-filtered and no-button modules must remain startup candidates.");
                Require(snapshot.StartupModules.Single(module => module.Id == "a").StartupType == "Example.Startup", "duplicate ID must not replace the first registered startup declaration.");
                Require(snapshot.StartupModules.Where(module => module.Id.StartsWith("invalid-")).All(module => !string.IsNullOrWhiteSpace(module.StartupConfigurationError)), "invalid startup JSON types must retain per-module errors without rejecting healthy neighbors.");
                Require(!snapshot.StartupModules.Single(module => module.Id == "no-declaration").AutoStart && !snapshot.StartupModules.Single(module => module.Id == "false").AutoStart, "omitted and false autoStart must stay false.");
                Require(snapshot.StartupModules.Single(module => module.Id == "missing-entry").AutoStart, "missing entry must be diagnosed per startup attempt rather than discard its neighbors.");
                Require(snapshot.Diagnostics.Any(item => item.Code == "RT-MODULE-COMPATIBILITY") && snapshot.Diagnostics.Any(item => item.Code == "RT-MODULE-DUPLICATE"), "startup qualification must retain compatibility and duplicate diagnostics.");
                Require(PackageManifestValidation.ValidateFile(manifest).Count() == 6, "package validator must reject non-boolean declarations and missing or invalid startup types.");
                Require(!PackageManifestValidation.ValidateFile(_source.FullPath("config/startup-package.example.json")).Any(), "startup manifest example must validate.");

                File.WriteAllText(manifest, "{\"schemaVersion\":\"1.0\",\"modules\":[{\"id\":null,\"autoStart\":\"true\"}]}");
                Require(PackageManifestValidation.ValidateFile(manifest).Single().Code == "PH-PACKAGE-STARTUP", "null module IDs must not interrupt startup declaration diagnostics.");

                File.WriteAllText(manifest, json.Serialize(new { schemaVersion = "1.0", modules = new[] { Module("roundtrip", 0, true) } }));
                var reader = new PackageManifestReader();
                Require(reader.TryReadManifest(manifest, out _, out var parsed), "package startup manifest must read.");
                Require(parsed.Modules.Single().AutoStart && parsed.Modules.Single().StartupType == "Example.Startup", "manifest reader must preserve startup declaration.");
                var store = new SettingsConfigurationStore(config);
                var configuration = store.LoadConfiguration();
                store.Save(configuration, store.LoadModuleDocuments(configuration));
                Require(reader.TryReadManifest(manifest, out _, out parsed) && parsed.Modules.Single().AutoStart && parsed.Modules.Single().StartupType == "Example.Startup", "Manager save must preserve startup declaration.");
                Require(!File.ReadAllText(manifest).Contains("StartupConfigurationError"), "runtime parsing errors must never be serialized.");

                // Inline sources use the same strict parser as installed package manifests.
                File.WriteAllText(Path.Combine(config, "sources.json"), json.Serialize(new { schemaVersion = "1.0", modules = new[] { Module("inline", 0, "true") } }));
                var inline = new FrameworkConfigurationLoader().Load(config).Modules.Modules.Single();
                Require(!inline.AutoStart && inline.StartupConfigurationError.Length > 0, "sources.json must not coerce string true into a boolean.");
                VerifyInstallationRoundTrip(directory, json, reader);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static void VerifyInstallationRoundTrip(string directory, JavaScriptSerializer json, PackageManifestReader reader)
        {
            var repository = Path.Combine(directory, "repository");
            Directory.CreateDirectory(repository);
            var manifest = Path.Combine(repository, "packages.json");
            File.WriteAllText(Path.Combine(repository, "Plugin.dll"), "v1");
            Action<string, string> write = (version, type) => File.WriteAllText(manifest, json.Serialize(new {
                schemaVersion = "1.0", modules = new[] { new { id = "roundtrip", version, assembly = "Plugin.dll", autoStart = true, startupType = type, features = new object[0] } }
            }));
            write("1.0.0", "Example.Startup");
            var package = new RepositoryPackageDescriptor { PackageId = "roundtrip", ModuleId = "roundtrip", ManifestPath = manifest, SourceDirectory = repository };
            var target = Path.Combine(directory, "installation");
            var service = new PackageRepositoryService();
            Require(service.Install(target, package).Success, "startup package must install.");
            var installedManifest = Path.Combine(target, "packages", "roundtrip", "packages.json");
            Require(reader.TryReadManifest(installedManifest, out _, out var installed) && installed.Modules.Single().AutoStart, "installation must retain autoStart.");
            write("2.0.0", "Example.NewStartup");
            Require(service.Update(target, package).Success, "startup package must update.");
            Require(reader.TryReadManifest(installedManifest, out _, out installed) && installed.Modules.Single().AutoStart && installed.Modules.Single().StartupType == "Example.NewStartup", "update must retain the new startup entry.");
        }

        private static object Module(string id, int order, object? autoStart = null, object? type = null, string revit = "2020")
        {
            var module = new Dictionary<string, object> { ["id"] = id, ["version"] = "1.0.0", ["assembly"] = "Plugin.dll", ["order"] = order,
                ["revitVersions"] = new[] { revit }, ["startupType"] = type ?? "Example.Startup", ["features"] = new[] { new { id = id + ".run", commandType = "Example.Command", tags = new[] { "layout-filter" } } } };
            if (autoStart != null) module["autoStart"] = autoStart;
            return module;
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
