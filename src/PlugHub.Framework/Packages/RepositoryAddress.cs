using System;
using System.IO;
using System.Linq;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Packages
{
    public sealed class RepositoryAddress
    {
        public RepositoryAddress(string provider, string owner, string name)
        {
            Provider = provider ?? string.Empty;
            Owner = owner ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public string Provider { get; }
        public string Owner { get; }
        public string Name { get; }
        public string Slug => string.IsNullOrWhiteSpace(Owner) || string.IsNullOrWhiteSpace(Name) ? string.Empty : Owner + "/" + Name;

        public static bool IsLocal(PackageRepositoryConfiguration repository)
        {
            return repository != null && string.Equals(repository.Provider, "local", StringComparison.OrdinalIgnoreCase);
        }

        public static RepositoryAddress? From(PackageRepositoryConfiguration repository)
        {
            if (repository == null || IsLocal(repository)) return null;

            var provider = NormalizeCloudProvider(repository.Provider);
            var value = StripRepositorySuffix(StripUrlUserInfo(repository.Repository ?? string.Empty).Trim().TrimEnd('/'));

            if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                {
                    return null;
                }

                var hostProvider = ProviderFromHost(uri.Host);
                if (string.IsNullOrWhiteSpace(hostProvider))
                {
                    return null;
                }

                var segments = uri.AbsolutePath
                    .Trim('/')
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                return segments.Length >= 2
                    ? new RepositoryAddress(hostProvider, segments[0], StripRepositorySuffix(segments[1]))
                    : null;
            }

            var shorthand = value
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return shorthand.Length >= 2
                ? new RepositoryAddress(provider, shorthand[0], StripRepositorySuffix(shorthand[1]))
                : null;
        }

        public static string NormalizeCloudProvider(string provider)
        {
            return string.Equals(provider, "gitee", StringComparison.OrdinalIgnoreCase) ? "gitee" : "github";
        }

        public static string NormalizeDisplayName(string customName, string id, string repository)
        {
            if (!string.IsNullOrWhiteSpace(customName)) return customName.Trim();
            var slug = SlugFromRepository(repository);
            if (!string.IsNullOrWhiteSpace(slug)) return slug;
            if (!string.IsNullOrWhiteSpace(repository))
            {
                return repository.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\')
                    .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault() ?? repository.Trim();
            }

            if (!string.IsNullOrWhiteSpace(id)) return id.Trim();

            return "未命名仓库";
        }

        public static string SlugFromRepository(string repository)
        {
            var value = StripRepositorySuffix(StripUrlUserInfo(repository ?? string.Empty).Trim().TrimEnd('/'));
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(ProviderFromHost(uri.Host)))
                {
                    return string.Empty;
                }

                var segments = uri.AbsolutePath
                    .Trim('/')
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                return segments.Length >= 2 ? segments[0] + "/" + StripRepositorySuffix(segments[1]) : string.Empty;
            }

            if (IsLikelyLocalPath(value)) return string.Empty;

            var shorthand = value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return shorthand.Length >= 2 ? shorthand[0] + "/" + StripRepositorySuffix(shorthand[1]) : string.Empty;
        }

        private static bool IsLikelyLocalPath(string value)
        {
            return Path.IsPathRooted(value)
                || value.IndexOf('\\') >= 0
                || value.IndexOf(':') >= 0;
        }

        public static string StripRepositorySuffix(string value)
        {
            return value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? value.Substring(0, value.Length - ".git".Length)
                : value;
        }

        public static string StripUrlUserInfo(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
            {
                return url;
            }

            var builder = new UriBuilder(uri)
            {
                UserName = string.Empty,
                Password = string.Empty
            };
            return builder.Uri.AbsoluteUri;
        }

        private static string ProviderFromHost(string host)
        {
            var normalized = (host ?? string.Empty).Trim().TrimEnd('.');
            if (string.Equals(normalized, "github.com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "www.github.com", StringComparison.OrdinalIgnoreCase))
            {
                return "github";
            }

            if (string.Equals(normalized, "gitee.com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "www.gitee.com", StringComparison.OrdinalIgnoreCase))
            {
                return "gitee";
            }

            return string.Empty;
        }
    }
}
