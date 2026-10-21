using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Quadra;

/// <summary>A small log in %AppData%\Quadra\quadra.log, to help when something goes wrong.</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string Folder => AppPaths.DataFolder;

    public static string FilePath => Path.Combine(Folder, "quadra.log");

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > 512 * 1024)
                    File.Move(FilePath, Path.Combine(Folder, "quadra.old.log"), overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // Logging must never break the app.
            }
        }
    }
}

/// <summary>Web pages the app opens, and the app's version.</summary>
internal static class Links
{
    public const string Repository = "https://github.com/ahrmichelon/quadra";

    /// <summary>The "support Quadra" section of the README, in the app's language.</summary>
    public static string Support => Loc.IsPortuguese
        ? "https://github.com/ahrmichelon/quadra/blob/main/README.pt-BR.md#apoie-o-quadra"
        : "https://github.com/ahrmichelon/quadra#support-quadra";

    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Write($"Couldn't open {url}: {e.Message}");
        }
    }

    public static string Version
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
            return $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
        }
    }
}

/// <summary>"Start with Windows": an entry in the current user's Run key. Needs no administrator rights.</summary>
internal static class StartWithWindows
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private static string Name => AppPaths.Name;

    /// <summary>The installed copy when there is one, otherwise this copy.</summary>
    private static string Command => $"\"{(Installer.IsRunningInstalled || !File.Exists(AppPaths.InstalledExe) ? Environment.ProcessPath : AppPaths.InstalledExe)}\"";

    public static bool IsEnabled
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            if (run?.GetValue(Name) is not string) return false;
            // Task Manager's Startup page can turn the entry off without removing it.
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return approved?.GetValue(Name) is not byte[] { Length: > 0 } state || (state[0] & 1) == 0;
        }
    }

    /// <param name="exe">The Quadra.exe to start (the installer passes the installed one).</param>
    public static void Set(bool on, string? exe = null)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on)
        {
            run.SetValue(Name, exe != null ? $"\"{exe}\"" : Command);
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            approved?.DeleteValue(Name, throwOnMissingValue: false);
        }
        else
        {
            run.DeleteValue(Name, throwOnMissingValue: false);
        }
    }

    /// <summary>If Quadra.exe was moved after turning this on, point the entry to the new place.</summary>
    public static void UpdatePathIfMoved() => UpdatePath(null);

    /// <summary>When the entry is on, points it to <paramref name="exe"/> (or to this copy).</summary>
    public static void UpdatePath(string? exe)
    {
        try
        {
            string command = exe != null ? $"\"{exe}\"" : Command;
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(Name) is string current && current != command) run.SetValue(Name, command);
        }
        catch (Exception e)
        {
            Log.Write($"Couldn't update the startup entry: {e.Message}");
        }
    }
}
