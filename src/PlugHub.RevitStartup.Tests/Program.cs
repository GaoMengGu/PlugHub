using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using Autodesk.Revit.UI;
using PlugHub.Framework.Configuration;
using PlugHub.Framework.Runtime;
using PlugHub.Revit2020;

namespace PlugHub.RevitStartup.Tests
{
    internal static class Program
    {
        private static readonly string Root = Path.Combine(RepositoryRoot(), ".tmp", "startup-tests-" + Guid.NewGuid().ToString("N"));
        private static string Fixture => Path.Combine(Root, "packages", "fixture", "dist", "Fixture.dll");

        private static int Main()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Fixture)!);
            File.Copy(typeof(Program).Assembly.Location, Fixture);
            try
            {
                DefaultsAndExplicitEntry();
                OrderAndOncePerSession();
                FailureMatrixAndCleanup();
                ShadowCopyAndEntryIntegration();
                Console.WriteLine("passed: 4 startup lifecycle groups (managed Revit substitutes)");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally
            {
                Directory.Delete(Root, true);
            }
        }

        private static ModuleConfiguration Module(string id, Type? type = null, int order = 0)
        {
            return new ModuleConfiguration { Id = id, AutoStart = true, Assembly = Fixture,
                StartupType = (type ?? typeof(SuccessA)).FullName!, Order = order };
        }

        private static PackageStartupSession Session()
        {
            return new PackageStartupSession((path, directory) => typeof(Program).Assembly);
        }

        private static void DefaultsAndExplicitEntry()
        {
            var application = new UIControlledApplication();
            var loads = 0;
            var session = new PackageStartupSession((path, directory) => { loads++; return typeof(Program).Assembly; });
            var missing = new ModuleConfiguration { Id = "default", Assembly = Fixture, StartupType = typeof(SuccessA).FullName! };
            var disabled = Module("false"); disabled.AutoStart = false;
            var legacyType = Module("legacy-type"); legacyType.Type = legacyType.StartupType; legacyType.StartupType = string.Empty;
            session.Start(application, new[] { missing, disabled, legacyType }, Root);
            Require(loads == 0 && application.Calls.Count == 0, "default/false or a legacy type must never initialize.");
            Require(session.Diagnostics.Single().Message.Contains("stage=configuration"), "true without startupType must report configuration failure.");
            session.Stop(application);
        }

        private static void OrderAndOncePerSession()
        {
            var application = new UIControlledApplication();
            var session = Session();
            session.Start(application, new[] { Module("z", typeof(SuccessB), 2), Module("b", typeof(SuccessC), 1), Module("A", typeof(SuccessA), 1), Module("a", typeof(SuccessB), 1) }, Root);
            Require(application.Calls.SequenceEqual(new[] { "+SuccessA", "+SuccessC", "+SuccessB" }), "startup must follow Order then case insensitive ID, attempting duplicates once.");
            session.Start(application, new[] { Module("new", typeof(SuccessA)) }, Root);
            Require(application.Calls.Count == 3, "a later refresh or new install must not initialize in the existing session.");
            Require(application.ThreadIds.All(id => id == Thread.CurrentThread.ManagedThreadId), "initialization must remain synchronous on the caller thread.");
            session.Stop(application); session.Stop(application);
            session.Start(application, new[] { Module("after-close") }, Root);
            Require(application.Calls.SequenceEqual(new[] { "+SuccessA", "+SuccessC", "+SuccessB", "-SuccessB", "-SuccessC", "-SuccessA" }), "shutdown must happen once in reverse order, including after repeated calls.");
        }

        private static void FailureMatrixAndCleanup()
        {
            var application = new UIControlledApplication();
            var session = Session();
            var noAssembly = Module("no-assembly"); noAssembly.Assembly = string.Empty;
            var missingFile = Module("missing-file"); missingFile.Assembly = Path.Combine(Root, "missing.dll");
            var missingType = Module("missing-type"); missingType.StartupType = "Missing.Type";
            var badBoolean = Module("bad-boolean"); badBoolean.AutoStart = false; badBoolean.StartupConfigurationError = "autoStart must be a boolean.";
            session.Start(application, new[] { noAssembly, missingFile, missingType, badBoolean, Module("interface", typeof(string)),
                Module("abstract", typeof(AbstractApplication)), Module("private", typeof(PrivateApplication)), Module("generic", typeof(GenericApplication<>)),
                Module("constructor-missing", typeof(NoDefaultConstructor)), Module("constructor-throws", typeof(ThrowingConstructor)),
                Module("failed", typeof(FailedStartup)), Module("cancelled", typeof(CancelledStartup)), Module("throws", typeof(ThrowingStartup)),
                Module("success-a", typeof(SuccessA)), Module("success-b", typeof(FailedShutdown)), Module("success-c", typeof(ThrowingShutdown)) }, Root);
            foreach (var type in new[] { "FailedStartup", "CancelledStartup", "ThrowingStartup" })
                Require(application.Calls.Count(call => call == "-" + type) == 1, "failed initialization must clean its own partial instance once: " + type);
            Require(!application.Calls.Any(call => call.Contains("ThrowingConstructor")), "a failed constructor must have no lifecycle cleanup.");
            session.Stop(application);
            Require(application.Calls.Skip(application.Calls.Count - 3).SequenceEqual(new[] { "-ThrowingShutdown", "-FailedShutdown", "-SuccessA" }), "a shutdown exception/non-success must not stop remaining cleanup.");
            Require(session.Diagnostics.All(diagnostic => diagnostic.Message.Contains("stage=") && diagnostic.Message.Contains("result=") && diagnostic.Message.Contains("elapsedMs=")), "all startup/cleanup results must carry stage, outcome and duration.");
            var log = string.Join("\n", Directory.GetFiles(Path.Combine(Root, "logs"), "*.log").Select(File.ReadAllText));
            Require(!log.Contains("private-token") && !string.Join("\n", session.Diagnostics.Select(diagnostic => diagnostic.Message)).Contains("private-token"), "exception credentials must be redacted in logs and diagnostics.");
            var loaderFailure = new PackageStartupSession((path, directory) => throw new BadImageFormatException("bad payload"));
            loaderFailure.Start(application, new[] { Module("bad-image") }, Root);
            Require(loaderFailure.Diagnostics.Single().Message.Contains("stage=load"), "assembly loading failures must be isolated and diagnosed.");
        }

        private static void ShadowCopyAndEntryIntegration()
        {
            Net48ShadowCopyCommandAssemblyLoader.LoadAssembly(Fixture, Root);
            Require(Directory.GetFiles(Path.Combine(Root, "runtime-cache"), "Fixture.dll", SearchOption.AllDirectories).Length == 1, "the production assembly loader must stage the startup payload in the existing hashed cache.");

            // The linked production entry uses this test binary's isolated output directory as its installation root.
            var installation = Path.GetDirectoryName(typeof(ExternalApplicationEntry).Assembly.Location)!;
            var config = Path.Combine(installation, "config");
            var packages = Path.Combine(installation, "packages", "entry-test");
            Directory.CreateDirectory(config); Directory.CreateDirectory(packages);
            try
            {
                var manifest = new { schemaVersion = "1.0", modules = new object[] {
                    new { id = "entry", assembly = Fixture, autoStart = true, startupType = typeof(SuccessA).FullName,
                        enabled = false, visible = false, features = new[] { new { id = "entry.run", commandType = "Example.Command", defaultState = "Hidden", tags = new[] { "filtered" } } } },
                    new { id = "no-buttons", assembly = Fixture, autoStart = true, startupType = typeof(SuccessB).FullName, features = new object[0] } } };
                File.WriteAllText(Path.Combine(packages, "packages.json"), new JavaScriptSerializer().Serialize(manifest));
                File.WriteAllText(Path.Combine(config, "views.json"), "{\"views\":[{\"id\":\"workspace\",\"excludeTags\":[\"filtered\"]}]}");
                var application = new UIControlledApplication();
                FeatureRibbonBuilder.OnBuild = () => Require(application.Calls.SequenceEqual(new[] { "+SuccessA", "+SuccessB" }), "startup must finish before Ribbon construction independently of layout or legacy state.");
                var entry = new ExternalApplicationEntry();
                Require(entry.OnStartup(application) == Result.Succeeded, "framework entry must return success after synchronous initialization.");
                FrameworkRuntimeState.Refresh();
                Require(application.Calls.Count == 2 && FrameworkRuntimeState.Current!.Composition.Features.Count == 0, "metadata refresh must not invoke the existing lifecycle again, including layout-filtered and no-button modules.");
                FeatureRibbonBuilder.OnBuild = () =>
                {
                    Require(application.Calls.SequenceEqual(new[] { "+SuccessA", "+SuccessB" }), "the package must initialize before Ribbon construction and entry return, including with no features.");
                    throw new InvalidOperationException("simulated ribbon failure");
                };
                try { entry.OnStartup(application); throw new Exception("Ribbon failure should propagate."); }
                catch (InvalidOperationException exception) { Require(exception.Message == "simulated ribbon failure", "original framework failure must be preserved: " + exception.Message + "; diagnostics: " + string.Join(" / ", FrameworkRuntimeState.Current!.Diagnostics.Select(item => item.Message))); }
                Require(application.Calls.SequenceEqual(new[] { "+SuccessA", "+SuccessB", "-SuccessB", "-SuccessA" }), "a later Ribbon failure must clean started packages in reverse order.");
                new ExternalApplicationEntry().OnShutdown(application);
                Require(application.Calls.Count == 4, "the failure cleanup must not be repeated at shutdown.");
                Require(FrameworkRuntimeState.Current!.Diagnostics.Any(item => item.Code == "PH-STARTUP-RESULT"), "startup diagnostic must be visible in the runtime snapshot.");
            }
            finally
            {
                FeatureRibbonBuilder.OnBuild = null;
                Directory.Delete(packages, true);
                Directory.Delete(config, true);
                foreach (var directory in new[] { "logs", "runtime-cache" })
                    if (Directory.Exists(Path.Combine(installation, directory))) Directory.Delete(Path.Combine(installation, directory), true);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("PlugHub repository root not found.");
        }

        private sealed class PrivateApplication : TestApplication { }
    }

    public abstract class TestApplication : IExternalApplication
    {
        protected virtual Result StartupResult => Result.Succeeded;
        protected virtual Result ShutdownResult => Result.Succeeded;
        public virtual Result OnStartup(UIControlledApplication application) { Record(application, "+"); return StartupResult; }
        public virtual Result OnShutdown(UIControlledApplication application) { Record(application, "-"); return ShutdownResult; }
        protected void Record(UIControlledApplication application, string prefix)
        {
            application.Calls.Add(prefix + GetType().Name);
            application.ThreadIds.Add(Thread.CurrentThread.ManagedThreadId);
        }
    }
    public sealed class SuccessA : TestApplication { }
    public sealed class SuccessB : TestApplication { }
    public sealed class SuccessC : TestApplication { }
    public abstract class AbstractApplication : TestApplication { }
    public sealed class GenericApplication<T> : TestApplication { }
    public sealed class NoDefaultConstructor : TestApplication { public NoDefaultConstructor(int value) { } }
    public sealed class ThrowingConstructor : TestApplication { public ThrowingConstructor() { throw new Exception("apiKey=private-token"); } }
    public sealed class FailedStartup : TestApplication { protected override Result StartupResult => Result.Failed; }
    public sealed class CancelledStartup : TestApplication { protected override Result StartupResult => Result.Cancelled; }
    public sealed class ThrowingStartup : TestApplication
    {
        public override Result OnStartup(UIControlledApplication application) { Record(application, "+"); throw new Exception("apiKey=private-token"); }
    }
    public sealed class FailedShutdown : TestApplication { protected override Result ShutdownResult => Result.Failed; }
    public sealed class ThrowingShutdown : TestApplication
    {
        public override Result OnShutdown(UIControlledApplication application) { Record(application, "-"); throw new Exception("cleanup"); }
    }
}
