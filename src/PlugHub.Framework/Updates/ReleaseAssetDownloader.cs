using System;
using System.IO;
using System.Linq;
using System.Net;

namespace PlugHub.Framework.Updates
{
    public sealed class ReleaseAssetDownloader
    {
        private const string UserAgent = "PlugHub-Framework-Updater/1.0";
        private const SecurityProtocolType Tls12 = (SecurityProtocolType)3072;
        private const SecurityProtocolType Tls11 = (SecurityProtocolType)768;

        public string Download(string downloadUrl, string targetDirectory, string fileName)
        {
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Release asset URL must be HTTPS.");
            }

            var targetPath = ResolveTargetPath(targetDirectory, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? targetDirectory);

            EnsureSecureTransport();
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;
            request.KeepAlive = false;
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                EnsureHttpsResponse(response.ResponseUri);
                using (var stream = response.GetResponseStream())
                using (var target = File.Create(targetPath))
                {
                    if (stream == null)
                    {
                        throw new InvalidDataException("Release asset response did not contain a body.");
                    }

                    stream.CopyTo(target);
                }
            }

            return targetPath;
        }

        private static string ResolveTargetPath(string targetDirectory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory)) throw new ArgumentException("Target directory is required.", nameof(targetDirectory));

            var fullTargetDirectory = Path.GetFullPath(targetDirectory);
            var targetPath = Path.GetFullPath(Path.Combine(fullTargetDirectory, SafeFileName(fileName)));
            if (!IsUnderDirectory(fullTargetDirectory, targetPath))
            {
                throw new InvalidOperationException("Release asset target path must stay inside the target directory.");
            }

            return targetPath;
        }

        private static void EnsureSecureTransport()
        {
            var protocols = ServicePointManager.SecurityProtocol | Tls12 | Tls11;
            if (ServicePointManager.SecurityProtocol != protocols)
            {
                ServicePointManager.SecurityProtocol = protocols;
            }

            ServicePointManager.Expect100Continue = false;
        }

        private static void EnsureHttpsResponse(Uri responseUri)
        {
            if (responseUri == null || !string.Equals(responseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Release asset download redirected to a non-HTTPS URL.");
            }
        }

        private static bool IsUnderDirectory(string parentDirectory, string childPath)
        {
            var parent = Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeFileName(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = (value ?? string.Empty).Replace(invalid, '_');
            }

            var segment = (value ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(segment) || segment.All(ch => ch == '.') ? "PlugHub-update.zip" : segment;
        }
    }
}
