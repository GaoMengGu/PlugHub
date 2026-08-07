using System;
using System.IO;
using System.Net;
using System.Net.Cache;

namespace PlugHub.Framework.Packages
{
    internal interface IRepositoryRemoteTransport
    {
        void Download(Uri uri, string targetPath, string authorizationHeader);
        string ReadText(Uri uri, string accept);
    }

    internal sealed class HttpRepositoryRemoteTransport : IRepositoryRemoteTransport
    {
        private const string UserAgent = "curl/8.0.1";

        public void Download(Uri uri, string targetPath, string authorizationHeader)
        {
            var request = CreateRequest(uri, "*/*");
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.Reload);
            if (!string.IsNullOrWhiteSpace(authorizationHeader))
            {
                request.Headers["Authorization"] = authorizationHeader;
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                EnsureHttpsResponse(response.ResponseUri);
                using (var source = response.GetResponseStream())
                using (var target = File.Create(targetPath))
                {
                    if (source == null) throw new InvalidOperationException("Repository archive response did not contain a body.");
                    source.CopyTo(target);
                }
            }
        }

        public string ReadText(Uri uri, string accept)
        {
            var request = CreateRequest(uri, accept);
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                EnsureHttpsResponse(response.ResponseUri);
                using (var source = response.GetResponseStream())
                using (var reader = new StreamReader(source ?? Stream.Null, System.Text.Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static HttpWebRequest CreateRequest(Uri uri, string accept)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            request.AllowAutoRedirect = true;
            request.UserAgent = UserAgent;
            request.Accept = accept;
            return request;
        }

        private static void EnsureHttpsResponse(Uri responseUri)
        {
            if (responseUri == null || !string.Equals(responseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Repository request redirected to a non-HTTPS URL.");
            }
        }
    }
}
