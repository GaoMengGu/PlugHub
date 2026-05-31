using System.Text.RegularExpressions;

namespace PlugHub.Framework.Diagnostics
{
    public static class SensitiveTextRedactor
    {
        public static string Redact(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var redacted = Regex.Replace(value, "(https?://)([^\\s/@:]+):([^\\s/@]+)@", "$1$2:***@", RegexOptions.IgnoreCase);
            redacted = Regex.Replace(redacted, "(https?://oauth2:)[^@]+@", "$1***@", RegexOptions.IgnoreCase);
            redacted = Regex.Replace(redacted, "(https?://x-access-token:)[^@]+@", "$1***@", RegexOptions.IgnoreCase);
            redacted = Regex.Replace(redacted, "(apiKey\"?\\s*[:=]\\s*\"?)[^\"\\s,]+", "$1***", RegexOptions.IgnoreCase);
            return redacted;
        }
    }
}
