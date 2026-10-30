// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation

/// User-selected public destinations. Constructing a URL never opens or sends it.
enum MacSupportLinks {
    static let sponsorURL = URL(string: "https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW")!
    static let feedbackAddress = "xiun1019@gmail.com"

    /// Only a recipient and a versioned subject; no body, attachments or diagnostics.
    static func feedbackURL(version: String) -> URL? {
        guard isSupportedVersion(version) else { return nil }
        var components = URLComponents()
        components.scheme = "mailto"
        components.path = feedbackAddress
        components.queryItems = [URLQueryItem(
            name: "subject", value: MacText.format("FeedbackSubjectFmt", version + " (Mac)"))]
        return components.url
    }

    private static func isSupportedVersion(_ version: String) -> Bool {
        guard version.utf8.count <= 40 else { return false }
        // Keep the release-number shape used by Windows and this repository.
        let release = #"\A(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.([0-9])\z"#
        if version.range(of: release, options: .regularExpression) != nil {
            let fields = version.split(separator: ".")
            guard let major = Int(fields[0]), let minor = Int(fields[1]) else { return false }
            return major <= 65534 && minor <= 65534
        }
        // Mac's historical alpha series starts at 1, unlike Windows' support UI.
        let alpha = #"\A0\.1\.0-alpha\.([1-9][0-9]{0,4})\z"#
        guard version.range(of: alpha, options: .regularExpression) != nil,
              let revision = Int(version.split(separator: ".").last!) else { return false }
        return revision <= 65534
    }
}
