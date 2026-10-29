using System;
using System.Globalization;
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
            if (!IsSupportedVersion(version))
                throw new ArgumentException("Expected a product version.", "version");
            return "mailto:" + FeedbackAddress + "?subject=" + Uri.EscapeDataString(PreviewText.F("FeedbackSubjectFmt", version));
        }

        private static bool IsSupportedVersion(string version)
        {
            if (version == null || version.Length > 40) return false;
            Match release = Regex.Match(version, @"\A(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.([0-9])\z");
            if (release.Success)
                return Int32.Parse(release.Groups[1].Value, CultureInfo.InvariantCulture) <= 65534 &&
                    Int32.Parse(release.Groups[2].Value, CultureInfo.InvariantCulture) <= 65534;
            Match alpha = Regex.Match(version, @"\A0\.1\.0-alpha\.([0-9]+)\z");
            int revision;
            return alpha.Success && Int32.TryParse(alpha.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out revision) &&
                revision >= 11 && revision <= 65534;
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
