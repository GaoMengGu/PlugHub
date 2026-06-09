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

        public ReleaseInfo GetLatestTestPrerelease(Uri releasesUri)
        {
            return ParseLatestTestPrereleaseJson(ReadHttpsText(releasesUri, "application/vnd.github+json"));
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

            return ParseReleaseObject(root);
        }

        public ReleaseInfo ParseLatestTestPrereleaseJson(string json)
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json ?? string.Empty);
            var release = AssetObjects(root)
                .Where(item => IsTestReleaseTag(StringValue(item, "tag_name")))
                .Where(item => BoolValue(item, "prerelease"))
                .Where(item => !BoolValue(item, "draft"))
                .OrderByDescending(item => TagVersion(StringValue(item, "tag_name")))
                .FirstOrDefault();

            return release == null ? new ReleaseInfo() : ParseReleaseObject(release);
        }

        public ReleaseInfo ParseGiteeTagsJson(string json, string downloadUrlTemplate)
        {
            var serializer = new JavaScriptSerializer();
            var root = serializer.DeserializeObject(json ?? string.Empty);
            var tags = AssetObjects(root)
                .Select(tag => StringValue(tag, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Where(IsStableReleaseTag)
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

        private static ReleaseInfo ParseReleaseObject(Dictionary<string, object> root)
        {
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

        private static bool IsStableReleaseTag(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.StartsWith("V", StringComparison.OrdinalIgnoreCase)
                && !IsTestReleaseTag(text)
                && Version.TryParse(ComparableVersionText(text), out _);
        }

        private static bool IsTestReleaseTag(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.StartsWith("TV", StringComparison.OrdinalIgnoreCase)
                && Version.TryParse(ComparableVersionText(text), out _);
        }

        private static string ComparableVersionText(string value)
        {
            var text = (value ?? string.Empty).Trim();
            var start = text.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
            return start >= 0 ? text.Substring(start) : text.TrimStart('v', 'V');
        }

        private static Version TagVersion(string value)
        {
            return Version.TryParse(ComparableVersionText(value), out var version)
                ? version
                : new Version(0, 0, 0);
        }

        private static bool BoolValue(Dictionary<string, object> source, string key)
        {
            if (!source.TryGetValue(key, out var value) || value == null) return false;
            if (value is bool boolean) return boolean;
            return bool.TryParse(Convert.ToString(value), out var parsed) && parsed;
        }

        private static string StringValue(Dictionary<string, object> source, string key)
        {
            return source.TryGetValue(key, out var value)
                ? SensitiveTextRedactor.Redact(Convert.ToString(value) ?? string.Empty)
                : string.Empty;
        }
    }
}
