using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web.Script.Serialization;
using PlugHub.Framework.Diagnostics;

namespace PlugHub.Framework.Updates
{
    public sealed class ReleaseClient
    {
        private const string UserAgent = "PlugHub-Framework-Updater/1.0";
        private const SecurityProtocolType Tls12 = (SecurityProtocolType)3072;
        private const SecurityProtocolType Tls11 = (SecurityProtocolType)768;

        public ReleaseInfo GetLatest(Uri releaseUri)
        {
            return GetLatestRelease(releaseUri);
        }

        public ReleaseInfo GetLatestRelease(Uri releaseUri)
        {
            return ParseReleaseJson(ReadHttpsText(releaseUri, "application/vnd.github+json"));
        }

        public ReleaseInfo GetGiteeTags(Uri tagsUri, string downloadUrlTemplate)
        {
            return ParseGiteeTagsJson(ReadHttpsText(tagsUri, "application/json"), downloadUrlTemplate);
        }

        private static string ReadHttpsText(Uri releaseUri, string accept)
        {
            if (releaseUri == null) throw new ArgumentNullException(nameof(releaseUri));
            if (!string.Equals(releaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Release API must use HTTPS.");
            }

            EnsureSecureTransport();
            var request = (HttpWebRequest)WebRequest.Create(releaseUri);
            request.Method = "GET";
            request.UserAgent = UserAgent;
            request.Accept = accept;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.KeepAlive = false;
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                EnsureHttpsResponse(response.ResponseUri);
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream ?? Stream.Null))
                {
                    return reader.ReadToEnd();
                }
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
                    var parsed = ParseAsset(asset);
                    if (!string.IsNullOrWhiteSpace(parsed.Name) && !string.IsNullOrWhiteSpace(parsed.DownloadUrl))
                    {
                        release.Assets.Add(parsed);
                    }
                }
            }

            return release;
        }

        public ReleaseInfo ParseGiteeTagsJson(string json, string downloadUrlTemplate)
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json ?? string.Empty);
            var tags = AssetObjects(root)
                .Select(tag => StringValue(tag, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Where(IsReleaseTag)
                .OrderByDescending(TagVersion)
                .ToList();

            var latestTag = tags.FirstOrDefault() ?? string.Empty;
            var release = new ReleaseInfo
            {
                TagName = latestTag,
                Body = string.Empty
            };
            if (!string.IsNullOrWhiteSpace(latestTag))
            {
                var assetName = "PlugHub-Revit2020-" + latestTag + ".zip";
                release.Assets.Add(new ReleaseAssetInfo
                {
                    Name = assetName,
                    DownloadUrl = CreateGiteeReleaseDownloadUrl(downloadUrlTemplate, latestTag, assetName)
                });
            }

            return release;
        }

        public static string CreateGiteeReleaseDownloadUrl(string template, string tag, string asset)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("Gitee download URL template is required.", nameof(template));
            }

            return template
                .Replace("{tag}", Uri.EscapeDataString(tag ?? string.Empty))
                .Replace("{asset}", Uri.EscapeDataString(asset ?? string.Empty));
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
                throw new InvalidOperationException("Release API redirected to a non-HTTPS URL.");
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

        private static ReleaseAssetInfo ParseAsset(Dictionary<string, object> asset)
        {
            return new ReleaseAssetInfo
            {
                Name = FirstStringValue(asset, "name", "filename", "file_name", "fileName"),
                DownloadUrl = FirstStringValue(asset, "browser_download_url", "download_url", "url", "html_url")
            };
        }

        private static string FirstStringValue(Dictionary<string, object> source, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = StringValue(source, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool IsReleaseTag(string value)
        {
            return Version.TryParse((value ?? string.Empty).Trim().TrimStart('v', 'V'), out _);
        }

        private static Version TagVersion(string value)
        {
            return Version.TryParse((value ?? string.Empty).Trim().TrimStart('v', 'V'), out var version)
                ? version
                : new Version(0, 0, 0);
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value)
                ? SensitiveTextRedactor.Redact(Convert.ToString(value) ?? string.Empty)
                : string.Empty;
        }
    }
}
