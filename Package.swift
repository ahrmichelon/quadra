// swift-tools-version: 5.10
import PackageDescription

// Quadra is a menu bar app. `swift build` compiles the executable and `./build.sh`
// wraps it into Quadra.app (Info.plist, icon and translations).
let package = Package(
    name: "Quadra",
    platforms: [.macOS(.v14)],
    targets: [
        .executableTarget(
            name: "Quadra",
            path: "Sources/Quadra"
        ),
        .testTarget(
            name: "QuadraTests",
            dependencies: ["Quadra"],
            path: "Tests/QuadraTests"
        ),
    ]
)
