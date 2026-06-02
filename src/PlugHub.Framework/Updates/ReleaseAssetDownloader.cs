using System;
using System.IO;
using System.Net;

namespace PlugHub.Framework.Updates
{
    public sealed class ReleaseAssetDownloader
    {
        private const string UserAgent = "PlugHub-Framework-Updater/1.0";

        public string Download(string downloadUrl, string targetDirectory, string fileName)
        {
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Release asset URL must be HTTPS.");
            }

            Directory.CreateDirectory(targetDirectory);
            var targetPath = Path.Combine(targetDirectory, SafeFileName(fileName));

            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var target = File.Create(targetPath))
            {
                if (stream == null)
                {
                    throw new InvalidDataException("Release asset response did not contain a body.");
                }

                stream.CopyTo(target);
            }

            return targetPath;
        }

        private static string SafeFileName(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = (value ?? string.Empty).Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "PlugHub-update.zip" : value;
        }
    }
}
