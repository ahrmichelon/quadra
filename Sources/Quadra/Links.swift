import Foundation

/// Web pages the app opens.
enum Links {
    static let repository = URL(string: "https://github.com/ahrmichelon/quadra")!

    /// The "support Quadra" section of the README, in the app's language.
    static var support: URL {
        let portuguese = Bundle.main.preferredLocalizations.first?.hasPrefix("pt") == true
        return URL(string: portuguese
            ? "https://github.com/ahrmichelon/quadra/blob/main/README.pt-BR.md#apoie-o-quadra"
            : "https://github.com/ahrmichelon/quadra#support-quadra")!
    }
}
