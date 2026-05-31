using System;
using System.Security.Cryptography;
using System.Text;
using PlugHub.Framework.Configuration;

namespace PlugHub.Framework.Packages
{
    public sealed class RepositoryCredentialService
    {
        private const string DpapiCurrentUserProtection = "dpapi-current-user";

        public string ResolveApiKey(PackageRepositoryConfiguration repository)
        {
            if (repository == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(repository.ApiKey)) return repository.ApiKey;
            if (string.IsNullOrWhiteSpace(repository.EncryptedApiKey)
                || !string.Equals(repository.ApiKeyProtection, DpapiCurrentUserProtection, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            try
            {
                var bytes = Convert.FromBase64String(repository.EncryptedApiKey);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
            }
            catch (FormatException)
            {
                return string.Empty;
            }
            catch (CryptographicException)
            {
                return string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        public void ProtectForSave(PackageRepositoryConfiguration repository)
        {
            if (repository == null || string.IsNullOrWhiteSpace(repository.ApiKey)) return;

            var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(repository.ApiKey), null, DataProtectionScope.CurrentUser);
            repository.EncryptedApiKey = Convert.ToBase64String(protectedBytes);
            repository.ApiKeyProtection = DpapiCurrentUserProtection;
            repository.ApiKey = string.Empty;
        }
    }
}
