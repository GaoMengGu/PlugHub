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
    internal sealed class LegacyStateMigrationValidator
    {
        private readonly ValidationSource _source;

        public LegacyStateMigrationValidator(ValidationSource source) { _source = source; }

        public void Validate()
        {
            Require(typeof(ModuleConfiguration).GetProperty("Enabled") == null
                && typeof(ModuleConfiguration).GetProperty("Visible") == null
                && typeof(ModuleOverrideConfiguration).GetProperty("Enabled") == null
                && typeof(ModuleOverrideConfiguration).GetProperty("Visible") == null
                && typeof(FeatureConfiguration).GetProperty("DefaultState") == null,
                "legacy configuration state fields must be removed, including compatibility placeholders.");
            var contracts = typeof(PlugHub.Contracts.Modules.ModuleDescriptor).Assembly;
            Require(contracts.GetType("PlugHub.Contracts.Modules.ModuleState") == null
                && contracts.GetType("PlugHub.Contracts.Features.FeatureState") == null
                && typeof(PlugHub.Contracts.Modules.ModuleDescriptor).GetProperty("State") == null
                && typeof(PlugHub.Contracts.Features.FeatureDescriptor).GetProperty("DefaultState") == null,
                "legacy public Contracts state members and enums must be removed.");
            var directory = _source.FullPath(".tmp/legacy-state-" + Guid.NewGuid().ToString("N"));
            try
            {
                var config = Path.Combine(directory, "config");
                Directory.CreateDirectory(config);
                var packageDirectory = Path.Combine(directory, "packages", "legacy");
                Directory.CreateDirectory(packageDirectory);
                var manifest = Path.Combine(packageDirectory, "packages.json");
                var untouched = Path.Combine(directory, "unscanned.packages.json");
                var json = new JavaScriptSerializer();
                var modules = new[]
                {
                    Module("legacy-hidden", "Hidden", 20),
                    Module("legacy-disabled", "Disabled", 10),
                    Module("legacy-hidden", "Visible", 30),
                    Module("wrong-revit", "Hidden", 40, "2024")
                };
                var original = json.Serialize(new { schemaVersion = "1.0", customRoot = "keep", modules });
                File.WriteAllText(manifest, original);
                File.WriteAllText(untouched, original);
                File.WriteAllText(Path.Combine(config, "sources.json"),
                    "{\"schemaVersion\":\"1.0\",\"repositories\":[{\"id\":\"private\",\"enabled\":false,\"visibility\":\"private\"}]} ");
                File.WriteAllText(Path.Combine(config, "views.json"),
                    "{\"defaultView\":\"workspace\",\"views\":[{\"id\":\"workspace\",\"excludeTags\":[\"layout-hidden\"]}]}");
                var presetsPath = Path.Combine(config, "feature-combinations.json");
                File.WriteAllText(presetsPath,
                    "{\"presets\":[{\"id\":\"legacy\",\"viewId\":\"workspace\",\"moduleOverrides\":[{\"moduleId\":\"legacy-hidden\",\"enabled\":false,\"visible\":false,\"order\":1}]}]}");

                var runtime = new FrameworkRuntime().Load(directory, config, false);
                Require(runtime.Features.Count == 2, "legacy disabled/hidden values must not skip installed features; duplicate IDs and incompatible modules must still be filtered.");
                Require(runtime.Diagnostics.Any(item => item.Code == "RT-MODULE-DUPLICATE"), "duplicate module diagnosis must survive state removal.");
                Require(runtime.Diagnostics.Any(item => item.Code == "RT-MODULE-COMPATIBILITY"), "Revit compatibility diagnosis must survive state removal.");
                Require(runtime.Configuration.EffectiveModules.Modules.First(item => item.Id == "legacy-hidden").Order == 1, "preset sorting must remain effective.");
                Require(runtime.Composition.Features.Select(item => item.FeatureId).SequenceEqual(new[] { "legacy-hidden.run" }), "layout tags must still filter features independently from legacy state.");
                var gate = new FeatureExecutionGate();
                Require(gate.CanExecuteFeatureId("legacy-hidden.run").Allowed, "legacy Hidden feature must execute when registered and in the workspace.");
                Require(!gate.CanExecuteFeatureId("legacy-disabled.run").Allowed, "workspace exclusion must still block execution.");
                Require(!gate.CanExecuteFeatureId("not-registered").Allowed, "unregistered feature must remain blocked.");
                Require(File.ReadAllText(manifest) == original && File.ReadAllText(untouched) == original, "discovery must not rewrite old JSON.");
                File.WriteAllText(Path.Combine(config, "views.json"),
                    "{\"defaultView\":\"workspace\",\"views\":[{\"id\":\"workspace\"}]}");
                var unfiltered = new FrameworkRuntime().Load(directory, config, false);
                Require(unfiltered.Composition.Features.Count == 2
                    && gate.CanExecuteFeatureId("legacy-hidden.run").Allowed
                    && gate.CanExecuteFeatureId("legacy-disabled.run").Allowed,
                    "both legacy Hidden and Disabled features must compose and execute when layout permits them.");


                var store = new SettingsConfigurationStore(config);
                var configuration = store.LoadConfiguration();
                store.Save(configuration, store.LoadModuleDocuments(configuration));
                Require(!HasLegacyFields(File.ReadAllText(manifest)) && !HasLegacyFields(File.ReadAllText(presetsPath)), "normal settings save must omit legacy module, feature and preset state.");
                Require(File.ReadAllText(untouched) == original, "normal save must not rewrite unscanned files.");
                var repository = new FrameworkConfigurationLoader().Load(config).Modules.Repositories.Single();
                Require(!repository.Enabled && repository.Visibility == "private", "repository enabled and private visibility must remain intact.");

                var raw = json.Deserialize<Dictionary<string, object>>(original);
                raw["modules"] = PackageManifestReader.ArrayValue(raw, "modules").ToArray();
                var reader = new PackageManifestReader();
                Require(reader.TryWriteManifest(manifest, raw, out var error), "raw manifest update must succeed: " + error);
                var rewritten = File.ReadAllText(manifest);
                Require(!HasLegacyFields(rewritten) && rewritten.Contains("customRoot") && rewritten.Contains("customModule"), "raw manifest updates must remove state while preserving unrelated extension fields.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static object Module(string id, string state, int order, string revit = "2020")
        {
            return new
            {
                id, enabled = false, visible = false, order, assembly = "Plugin.dll", customModule = "keep", revitVersions = new[] { revit },
                features = new[] { new { id = id + ".run", displayName = id, commandType = "Example.Command", defaultState = state,
                    tags = id == "legacy-disabled" ? new[] { "layout-hidden" } : new string[0] } }
            };
        }

        private static bool HasLegacyFields(string text)
        {
            return new[] { "\"enabled\"", "\"visible\"", "\"defaultState\"" }.Any(key => text.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
