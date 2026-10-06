using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace ETSOverlay
{
    public enum UrlPolicy { StripePortal, OfficialWebsite, GitHubRelease, Markdown }

    public static class SafeUrlLauncher
    {
        public static bool TryValidate(string? value, UrlPolicy policy, out Uri? uri)
        {
            uri = null;
            // Reject characters that parsers or shell handlers could reinterpret.
            if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace) ||
                value.Any(char.IsControl) || value.Contains('\\') ||
                !Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
                parsed.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(parsed.Host) ||
                !string.IsNullOrEmpty(parsed.UserInfo) ||
                parsed.GetComponents(UriComponents.StrongAuthority, UriFormat.UriEscaped).Contains('@')) return false;

            bool allowed = policy switch
            {
                UrlPolicy.StripePortal => parsed.IdnHost.Equals("billing.stripe.com", StringComparison.OrdinalIgnoreCase),
                UrlPolicy.OfficialWebsite => parsed.IdnHost.Equals("trucksim.uk", StringComparison.OrdinalIgnoreCase),
                UrlPolicy.GitHubRelease => parsed.IdnHost.Equals("github.com", StringComparison.OrdinalIgnoreCase),
                UrlPolicy.Markdown => true,
                _ => false
            };
            if (!allowed) return false;
            uri = parsed;
            return true;
        }

        public static bool TryOpen(string? value, UrlPolicy policy, Action<string> log,
            Action<ProcessStartInfo>? start = null)
        {
            if (!TryValidate(value, policy, out var uri))
            {
                log("[URL] Blocked invalid or untrusted link.");
                return false;
            }
            try
            {
                var info = new ProcessStartInfo(uri!.AbsoluteUri) { UseShellExecute = true };
                if (start != null) start(info);
                else Process.Start(info);
                return true;
            }
            catch (Exception)
            {
                // Do not log session URLs, credentials, or exception details.
                log("[URL] Failed to open browser.");
                return false;
            }
        }

        public static bool OpenForUser(string? value, UrlPolicy policy, Window owner, string language)
        {
            if (TryOpen(value, policy, message =>
                (Application.Current?.MainWindow as MainWindow)?.WriteLog(message))) return true;
            CustomMessageBox.Show(owner,
                language == "uk" ? "Не вдалося відкрити посилання. Спробуйте пізніше." : "Unable to open this link. Please try again later.",
                language == "uk" ? "Посилання" : "Link", "OK", string.Empty);
            return false;
        }
    }
}
