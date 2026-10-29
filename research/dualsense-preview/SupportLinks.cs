using System;
using System.Text.RegularExpressions;

namespace PSKeyboard
{
    // Fixed public destinations only. No account, profile, input, or diagnostic data.
    internal static class SupportLinks
    {
        // Awaiting the owner's corrected public PayPal destination.
        internal const string SponsorUrl = "";
        internal const string FeedbackAddress = "xiun1019@gmail.com";

        internal static bool SponsorAvailable { get { return IsAllowedPayPalUrl(SponsorUrl); } }

        internal static string FeedbackUrl(string version)
        {
            if (version == null || version.Length > 40 || !Regex.IsMatch(version, @"\A[0-9]+\.[0-9]+\.[0-9]+-alpha\.[0-9]+\z"))
                throw new ArgumentException("Expected a product version.", "version");
            return "mailto:" + FeedbackAddress + "?subject=" + Uri.EscapeDataString("上班不要玩 " + version + " 意見回饋");
        }

        internal static bool IsAllowedPayPalUrl(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 2048 || value != value.Trim()) return false;
            foreach (char character in value) if (Char.IsControl(character) || character == '\\') return false;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps ||
                !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0) return false;
            string host = uri.DnsSafeHost;
            bool paypal = String.Equals(host, "www.paypal.com", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(host, "paypal.com", StringComparison.OrdinalIgnoreCase);
            bool paypalMe = String.Equals(host, "paypal.me", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(host, "www.paypal.me", StringComparison.OrdinalIgnoreCase);
            return (paypal && (uri.AbsolutePath == "/donate" || uri.AbsolutePath == "/donate/")) ||
                (paypalMe && Regex.IsMatch(uri.AbsolutePath, @"\A/[A-Za-z0-9_.-]+/1USD/?\z", RegexOptions.IgnoreCase));
        }

        internal static bool IsAllowedLaunchUrl(string value, string version)
        {
            return String.Equals(value, FeedbackUrl(version), StringComparison.Ordinal) ||
                (SponsorAvailable && String.Equals(value, SponsorUrl, StringComparison.Ordinal));
        }
    }
}
