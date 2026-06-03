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
        private const SecurityProtocolType Tls12 = (SecurityProtocolType)3072;

        public ReleaseInfo GetLatest(Uri releaseUri)
        {
            if (releaseUri == null) throw new ArgumentNullException(nameof(releaseUri));
            if (!string.Equals(releaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Release API must use HTTPS.");
            }

            EnsureTls12();
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

            var release = new ReleaseInfo
            {
                TagName = StringValue(root, "tag_name"),
                Body = StringValue(root, "body")
            };
            if (root.TryGetValue("assets", out var assetsValue))
            {
                foreach (var asset in AssetObjects(assetsValue))
                {
                    release.Assets.Add(new ReleaseAssetInfo
                    {
                        Name = StringValue(asset, "name"),
                        DownloadUrl = StringValue(asset, "browser_download_url")
                    });
                }
            }

            return release;
        }

        private static void EnsureTls12()
        {
            if ((ServicePointManager.SecurityProtocol & Tls12) != Tls12)
            {
                ServicePointManager.SecurityProtocol |= Tls12;
            }
        }

        private static IEnumerable<Dictionary<string, object>> AssetObjects(object value)
        {
            if (!(value is IEnumerable items) || value is string)
            {
                yield break;
            }

            foreach (var item in items)
            {
                if (item is Dictionary<string, object> asset)
                {
                    yield return asset;
                }
            }
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value)
                ? SensitiveTextRedactor.Redact(Convert.ToString(value) ?? string.Empty)
                : string.Empty;
        }
    }
}
