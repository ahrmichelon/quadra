using System;
using System.IO;

namespace Quadra;

/// <summary>
/// Where Quadra installs itself and keeps its data, and the names it registers with Windows.
/// The self-test sets QUADRA_TEST_ROOT to a temporary folder: everything then goes there, under test names,
/// so a test never touches the real Quadra.
/// </summary>
internal static class AppPaths
{
    private static readonly string? TestRoot =
        Environment.GetEnvironmentVariable("QUADRA_TEST_ROOT") is { Length: > 0 } root ? root : null;

    public static bool IsTest => TestRoot != null;

    /// <summary>"Quadra": the name of the startup entry, the uninstall entry and the Start menu shortcut.</summary>
    public static string Name => IsTest ? "QuadraTest" : "Quadra";

    /// <summary>Settings and log: %AppData%\Quadra.</summary>
    public static string DataFolder => TestRoot is { } root
        ? Path.Combine(root, "Data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Quadra");

    /// <summary>
    /// The installed app: %LocalAppData%\Programs\Quadra, the usual place for apps installed for one user
    /// (no administrator rights needed).
    /// </summary>
    public static string InstallFolder => TestRoot is { } root
        ? Path.Combine(root, "Programs", "Quadra")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Quadra");

    public static string InstalledExe => Path.Combine(InstallFolder, "Quadra.exe");

    /// <summary>The shortcut in the Start menu.</summary>
    public static string StartMenuShortcut => Path.Combine(
        TestRoot is { } root ? Path.Combine(root, "Start Menu") : Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Name + ".lnk");
}
