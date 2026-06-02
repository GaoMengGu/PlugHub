using System;
using System.IO;
using System.Xml;

namespace PlugHub.Installer
{
    internal static class AddinManifestWriter
    {
        public static string Install(string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                throw new ArgumentException("Install directory is required.", nameof(installDirectory));
            }

            var fullInstallDirectory = Path.GetFullPath(installDirectory);
            var dllPath = Path.Combine(fullInstallDirectory, "PlugHub.Revit2020.dll");
            var addinPath = Path.Combine(fullInstallDirectory, "PlugHub.addin");
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException("PlugHub.Revit2020.dll was not found in the install directory.", dllPath);
            }

            if (!File.Exists(addinPath))
            {
                throw new FileNotFoundException("PlugHub.addin was not found in the install directory.", addinPath);
            }

            RewriteAssemblyPath(addinPath, dllPath);

            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(programData))
            {
                throw new InvalidOperationException("ProgramData could not be resolved for machine-wide Revit addin registration.");
            }

            var addinsDirectory = Path.Combine(programData, "Autodesk", "Revit", "Addins", "2020");
            Directory.CreateDirectory(addinsDirectory);
            var targetAddin = Path.Combine(addinsDirectory, "PlugHub.addin");
            var backup = BackupExistingAddin(targetAddin);
            try
            {
                File.Copy(addinPath, targetAddin, true);
                return targetAddin;
            }
            catch
            {
                RestoreBackup(targetAddin, backup);
                throw;
            }
        }

        private static void RewriteAssemblyPath(string addinPath, string dllPath)
        {
            var document = new XmlDocument();
            document.Load(addinPath);
            var assemblyNode = document.SelectSingleNode("//RevitAddIns/AddIn/Assembly");
            if (assemblyNode == null)
            {
                throw new InvalidOperationException("Missing Assembly node in " + addinPath);
            }

            assemblyNode.InnerText = dllPath;
            document.Save(addinPath);
        }

        private static string BackupExistingAddin(string targetAddin)
        {
            if (!File.Exists(targetAddin))
            {
                return string.Empty;
            }

            var backup = targetAddin + ".bak";
            File.Copy(targetAddin, backup, true);
            return backup;
        }

        private static void RestoreBackup(string targetAddin, string backup)
        {
            if (!string.IsNullOrWhiteSpace(backup) && File.Exists(backup))
            {
                File.Copy(backup, targetAddin, true);
            }
        }
    }
}
