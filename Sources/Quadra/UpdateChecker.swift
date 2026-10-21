import Foundation

/// Asks GitHub whether a newer Quadra release exists. Runs at most once a day, and is the app's
/// only network request. It can be turned off in Settings.
final class UpdateChecker {
    struct Release: Decodable, Equatable {
        let tagName: String
        let htmlURL: URL

        enum CodingKeys: String, CodingKey {
            case tagName = "tag_name"
            case htmlURL = "html_url"
        }

        /// "v1.2.0" → "1.2.0"
        var version: String { tagName.hasPrefix("v") ? String(tagName.dropFirst()) : tagName }
    }

    static let latestReleaseAPI = URL(string: "https://api.github.com/repos/ahrmichelon/quadra/releases/latest")!

    /// The newer release found by the last check, if any.
    private(set) var available: Release?
    /// Called after an automatic check finds a version the user hasn't been told about yet.
    var onNewVersion: ((Release) -> Void)?

    private var timer: Timer?
    private let defaults = UserDefaults.standard

    static var currentVersion: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0"
    }

    /// Automatic checks (on by default, can be turned off in Settings).
    static var isAutomatic: Bool {
        UserDefaults.standard.object(forKey: "checkForUpdates") as? Bool ?? true
    }

    /// Installed with Homebrew: updating is `brew upgrade`, not a download.
    static var installedWithHomebrew: Bool {
        ["/opt/homebrew/Caskroom/quadra", "/usr/local/Caskroom/quadra"]
            .contains { FileManager.default.fileExists(atPath: $0) }
    }

    static let homebrewUpgradeCommand = "brew upgrade --cask quadra"

    func start() {
        // First look shortly after launch, then every hour see whether a day has passed.
        DispatchQueue.main.asyncAfter(deadline: .now() + 15) { [weak self] in self?.checkIfDue() }
        timer = Timer.scheduledTimer(withTimeInterval: 3600, repeats: true) { [weak self] _ in self?.checkIfDue() }
    }

    private func checkIfDue() {
        guard Self.isAutomatic else { return }
        let last = defaults.object(forKey: "lastUpdateCheck") as? Date ?? .distantPast
        guard Date().timeIntervalSince(last) > 24 * 3600 else { return }
        check { [weak self] result in
            guard let self, case .success(let release?) = result,
                  self.defaults.string(forKey: "announcedVersion") != release.version else { return }
            self.markAnnounced(release)
            self.onNewVersion?(release)
        }
    }

    /// Remembers that the user has seen this version, so the automatic check doesn't repeat it.
    func markAnnounced(_ release: Release) {
        defaults.set(release.version, forKey: "announcedVersion")
    }

    /// Asks GitHub for the latest release. The result is the newer release, nil when Quadra is
    /// up to date, or an error. Always called on the main thread.
    func check(completion: @escaping (Result<Release?, Error>) -> Void) {
        var request = URLRequest(url: Self.latestReleaseAPI, timeoutInterval: 20)
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")
        request.setValue("Quadra/\(Self.currentVersion)", forHTTPHeaderField: "User-Agent")
        URLSession.shared.dataTask(with: request) { data, _, error in
            DispatchQueue.main.async {
                if let error { return completion(.failure(error)) }
                guard let data, let release = try? JSONDecoder().decode(Release.self, from: data) else {
                    return completion(.failure(URLError(.cannotParseResponse)))
                }
                self.defaults.set(Date(), forKey: "lastUpdateCheck")
                self.available = Self.isNewer(release.version, than: Self.currentVersion) ? release : nil
                completion(.success(self.available))
            }
        }.resume()
    }

    /// Compares versions number by number, so "1.10.0" is newer than "1.9.2".
    static func isNewer(_ a: String, than b: String) -> Bool {
        let x = numbers(a), y = numbers(b)
        for i in 0..<max(x.count, y.count) {
            let l = i < x.count ? x[i] : 0, r = i < y.count ? y[i] : 0
            if l != r { return l > r }
        }
        return false
    }

    private static func numbers(_ version: String) -> [Int] {
        version.split(separator: ".").map { Int($0.prefix { $0.isNumber }) ?? 0 }
    }
}
