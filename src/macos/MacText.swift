// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation

/// Nine-language product text. The resource is generated from the existing
/// Windows catalogues plus explicit Mac overrides; no system setting is changed.
enum MacText {
    static let supportedLanguages = ["en", "zh-Hant", "zh-Hans", "ja", "ko", "de", "fr", "es", "pt-BR"]
    static let languageTag = resolveLanguage(Locale.preferredLanguages)

    private struct Catalogue: Decodable, Sendable {
        let schemaVersion: Int
        let languages: [String]
        let aliases: [String: String]
        let strings: [String: [String: String]]
    }

    private static let catalogue: Catalogue? = {
        guard let url = Bundle.main.url(forResource: "strings", withExtension: "json"),
              let data = try? Data(contentsOf: url), data.count <= 1_048_576,
              let value = try? JSONDecoder().decode(Catalogue.self, from: data),
              value.schemaVersion == 1, value.languages == supportedLanguages,
              let english = value.strings["en"], !english.isEmpty,
              supportedLanguages.allSatisfy({ Set(value.strings[$0]?.keys.map { $0 } ?? []) == Set(english.keys) })
        else { return nil }
        return value
    }()

    static func resolveLanguage(_ preferred: [String]) -> String {
        // Match the first preferred locale. Unsupported primary languages use
        // English, as the Windows UI does, rather than silently picking a lower
        // priority language that the user may not want for this application.
        guard let first = preferred.first else { return "en" }
        let fields = first.replacingOccurrences(of: "_", with: "-").lowercased().split(separator: "-")
        guard let primary = fields.first else { return "en" }
        if primary == "zh" {
            if fields.contains("hant") || fields.contains("tw") || fields.contains("hk") || fields.contains("mo") {
                return "zh-Hant"
            }
            return "zh-Hans"
        }
        if primary == "pt" { return "pt-BR" }
        let language = String(primary)
        return supportedLanguages.contains(language) ? language : "en"
    }

    static func t(_ key: String) -> String {
        guard let catalogue else { return key }
        let resolved = catalogue.strings["en"]?[key] != nil ? key : catalogue.aliases[key] ?? key
        return catalogue.strings[languageTag]?[resolved] ?? catalogue.strings["en"]?[resolved] ?? key
    }

    static func format(_ key: String, _ args: CVarArg...) -> String {
        let template = t(key)
        // Catalogue formats use only numbered %@ placeholders. Convert values
        // to strings so callers may pass either a version string or a count.
        let values = args.map { String(describing: $0) }
        guard let expression = try? NSRegularExpression(pattern: #"%%|%([1-9][0-9]*)\$@"#) else { return template }
        let range = NSRange(template.startIndex..<template.endIndex, in: template)
        let matches = expression.matches(in: template, range: range)
        guard !matches.isEmpty else { return template }
        let valid = matches.allSatisfy { match in
            if match.range(at: 1).location == NSNotFound { return true }
            guard let argumentRange = Range(match.range(at: 1), in: template),
                  let index = Int(template[argumentRange]) else { return false }
            return index <= values.count
        }
        guard valid else { return template }
        // Substitute only our explicit object tokens. Do not pass unknown keys
        // or translated percent signs into a C variadic format interpreter.
        var result = template
        for match in matches.reversed() {
            guard let target = Range(match.range, in: result) else { return template }
            if match.range(at: 1).location == NSNotFound {
                result.replaceSubrange(target, with: "%")
            } else {
                guard let argumentRange = Range(match.range(at: 1), in: template),
                      let index = Int(template[argumentRange]) else { return template }
                result.replaceSubrange(target, with: values[index - 1])
            }
        }
        return result
    }
}
