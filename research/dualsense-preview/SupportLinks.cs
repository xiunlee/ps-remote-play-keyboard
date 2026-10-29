using System;
using System.Text.RegularExpressions;

namespace PSKeyboard
{
    // Fixed public destinations only. No account, profile, input, or diagnostic data.
    internal static class SupportLinks
    {
        // User-designated public link. Any payment is handled on PayPal's page.
        internal const string SponsorUrl = "https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW";
        internal const string FeedbackAddress = "xiun1019@gmail.com";

        internal static bool SponsorAvailable { get { return IsAllowedSponsorUrl(SponsorUrl); } }

        internal static string FeedbackUrl(string version)
        {
            if (version == null || version.Length > 40 || !Regex.IsMatch(version, @"\A[0-9]+\.[0-9]+\.[0-9]+-alpha\.[0-9]+\z"))
                throw new ArgumentException("Expected a product version.", "version");
            return "mailto:" + FeedbackAddress + "?subject=" + Uri.EscapeDataString(PreviewText.F("FeedbackSubjectFmt", version));
        }

        internal static bool IsAllowedSponsorUrl(string value)
        {
            // Match the entire raw URL: no credentials, ports, query, fragment,
            // escaping, path normalization, backslashes or control characters.
            return value != null && Regex.IsMatch(value, @"\Ahttps://www\.paypal\.com/ncp/payment/[A-Z0-9]{13}\z", RegexOptions.CultureInvariant);
        }

        internal static bool IsAllowedLaunchUrl(string value, string version)
        {
            return String.Equals(value, FeedbackUrl(version), StringComparison.Ordinal) ||
                (SponsorAvailable && String.Equals(value, SponsorUrl, StringComparison.Ordinal));
        }
    }
}
