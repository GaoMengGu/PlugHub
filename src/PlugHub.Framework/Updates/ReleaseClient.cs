using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using PlugHub.Framework.Diagnostics;

namespace PlugHub.Framework.Updates
{
    public sealed class ReleaseClient
    {
        private const string UserAgent = "PlugHub-Framework-Updater/1.0";

        public ReleaseInfo GetLatest(Uri releaseUri)
        {
            if (releaseUri == null) throw new ArgumentNullException(nameof(releaseUri));
            if (!string.Equals(releaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Release API must use HTTPS.");
            }

            var request = (HttpWebRequest)WebRequest.Create(releaseUri);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Accept = "application/vnd.github+json";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream ?? Stream.Null))
            {
                return ParseReleaseJson(reader.ReadToEnd());
            }
        }

        public ReleaseInfo ParseReleaseJson(string json)
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json ?? string.Empty) as Dictionary<string, object>;
            if (root == null)
            {
                throw new InvalidDataException("Release response is not a JSON object.");
            }

            var release = new ReleaseInfo { TagName = StringValue(root, "tag_name") };
            if (root.TryGetValue("assets", out var assetsValue) && assetsValue is ArrayList assets)
            {
                foreach (var item in assets)
                {
                    if (!(item is Dictionary<string, object> asset)) continue;
                    release.Assets.Add(new ReleaseAssetInfo
                    {
                        Name = StringValue(asset, "name"),
                        DownloadUrl = StringValue(asset, "browser_download_url")
                    });
                }
            }

            return release;
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value)
                ? SensitiveTextRedactor.Redact(Convert.ToString(value) ?? string.Empty)
                : string.Empty;
        }
    }
}
