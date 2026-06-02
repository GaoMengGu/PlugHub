using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PlugHub.Framework.Updates
{
    public sealed class FrameworkUpdatePackageValidator
    {
        private static readonly string[] RequiredDlls =
        {
            "PlugHub.Revit2020.dll",
            "PlugHub.Framework.dll",
            "PlugHub.Contracts.dll"
        };

        public void Validate(string zipPath)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                throw new FileNotFoundException("Framework update package was not found.", zipPath);
            }

            ValidateZipHeader(zipPath);
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var rootDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries)
                {
                    if (!IsSafeZipEntry(entry.FullName))
                    {
                        throw new InvalidDataException("Update package contains an unsafe path: " + entry.FullName);
                    }

                    if (IsRootDllEntry(entry.FullName))
                    {
                        rootDlls.Add(Path.GetFileName(entry.FullName));
                    }
                }

                foreach (var dll in RequiredDlls)
                {
                    if (!rootDlls.Contains(dll))
                    {
                        throw new InvalidDataException("Update package is missing framework DLL: " + dll);
                    }
                }
            }
        }

        private static void ValidateZipHeader(string zipPath)
        {
            var header = new byte[4];
            using (var source = File.OpenRead(zipPath))
            {
                if (source.Read(header, 0, header.Length) != header.Length)
                {
                    throw new InvalidDataException("Update package is not a valid zip file.");
                }
            }

            if (header[0] != 0x50 || header[1] != 0x4B)
            {
                throw new InvalidDataException("Update package is not a zip file.");
            }
        }

        public static bool IsSafeZipEntry(string entryName)
        {
            var normalized = (entryName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return !string.IsNullOrWhiteSpace(normalized)
                && !Path.IsPathRooted(normalized)
                && normalized.Split(Path.DirectorySeparatorChar).All(part => part != "..");
        }

        public static bool IsRootDllEntry(string entryName)
        {
            var normalized = (entryName ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return string.Equals(Path.GetExtension(normalized), ".dll", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(normalized), normalized, StringComparison.OrdinalIgnoreCase);
        }
    }
}
