using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace Quadra;

/// <summary>
/// Quadra installs itself: opening the downloaded Quadra.exe copies it to %LocalAppData%\Programs\Quadra,
/// adds a Start menu shortcut and an entry in Settings → Apps (with Uninstall), turns on Start with Windows
/// the first time, and opens the installed copy. Opening a newer Quadra.exe the same way updates it.
/// Everything is per user, so no administrator rights are needed.
/// </summary>
internal static class Installer
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";

    /// <summary>True when this is the installed copy.</summary>
    public static bool IsRunningInstalled => SamePath(Environment.ProcessPath, AppPaths.InstalledExe);

    /// <summary>This copy's version. The self-test can pretend to be newer with QUADRA_TEST_VERSION.</summary>
    public static Version OwnVersion
    {
        get
        {
            if (AppPaths.IsTest && Version.TryParse(Environment.GetEnvironmentVariable("QUADRA_TEST_VERSION"), out var fake))
                return fake;
            var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    private static Version? InstalledVersion()
    {
        if (!File.Exists(AppPaths.InstalledExe)) return null;
        var info = FileVersionInfo.GetVersionInfo(AppPaths.InstalledExe);
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
    }

    /// <summary>The argument the installer gives the installed copy, so it doesn't repeat the news as a notification.</summary>
    public const string JustInstalledArgument = "--just-installed";

    /// <summary>
    /// Run when Quadra.exe is opened from anywhere but its install folder (Downloads, or an update in Temp).
    /// Installs or updates Quadra and opens the installed copy, then says so in a window (not when the update
    /// came from Quadra's own update button, which shows a notification instead). False when it couldn't
    /// install, so this copy keeps running from where it is.
    /// </summary>
    public static bool InstallAndOpen(bool fromUpdateButton)
    {
        string source = Environment.ProcessPath ?? "", target = AppPaths.InstalledExe;
        try
        {
            var installed = InstalledVersion();
            bool firstInstall = installed == null;
            var running = RunningCopy();
            if (installed == null || OwnVersion > installed)
            {
                Log.Write($"Installing Quadra {OwnVersion.ToString(3)} from {source} to {target}" +
                          (installed != null ? $" (replacing {installed.ToString(3)})" : ""));
                Quit(running);
                Directory.CreateDirectory(AppPaths.InstallFolder);
                CopyWithRetries(source, target);
                // The downloaded file is marked as coming from the internet; the installed copy must not be,
                // or Windows would ask again every time it starts with Windows.
                Native.DeleteFile(target + ":Zone.Identifier");
            }
            else if (running is { } copy && SamePath(copy.Path, target))
            {
                // The same or a newer version is installed and open: show its settings, as opening it again would.
                HotKeyCenter.AskRunningCopyToShowSettings();
                Register(firstInstall: false);
                return true;
            }
            else
            {
                Quit(running); // an old copy running from somewhere else
            }

            Register(firstInstall);
            // The self-test only asks for the window when it is ready to look at it and close it.
            bool tellHere = !fromUpdateButton &&
                            (!AppPaths.IsTest || Environment.GetEnvironmentVariable("QUADRA_TEST_SHOW_WINDOW") == "1");
            Process.Start(new ProcessStartInfo(target, tellHere ? JustInstalledArgument : "")
            {
                UseShellExecute = false,
                WorkingDirectory = AppPaths.InstallFolder,
            });
            if (tellHere) ShowInstalled(firstInstall || installed == null, installed);
            return true;
        }
        catch (Exception e)
        {
            Log.Write($"Couldn't install Quadra in {target}: {e}");
            return false;
        }
    }

    /// <summary>
    /// Says that Quadra was installed (or updated), where it is and that it opens with Windows, so the user
    /// knows something happened: the install itself takes a second and shows nothing.
    /// </summary>
    private static void ShowInstalled(bool firstInstall, Version? previous)
    {
        string version = OwnVersion.ToString(3);
        string text;
        if (previous == null || firstInstall)
        {
            var settings = new SettingsStore(System.IO.Path.Combine(AppPaths.DataFolder, "settings.json")).Current;
            var combo = settings.ShowGridHotKey ?? HotKeyCombo.ShowGridDefault;
            text = Loc.Format("Install_Done", version) + "\n\n" + Loc.Format("Install_DoneText", combo.Display);
        }
        else if (OwnVersion > previous)
        {
            text = Loc.Format("Updated_Title", version) + "\n\n" + Loc.Get("Install_UpdatedText");
        }
        else
        {
            text = Loc.Format("Install_Done", version) + "\n\n" + Loc.Get("Install_AlreadyText");
        }
        MessageBox.Show(text, "Quadra", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// The Start menu shortcut, the entry in Settings → Apps and, on the first install, Start with Windows.
    /// Updates keep the user's choice about starting with Windows, only pointing it to the installed copy.
    /// </summary>
    private static void Register(bool firstInstall)
    {
        string exe = AppPaths.InstalledExe;
        WriteUninstallEntry(exe);
        CreateShortcut(AppPaths.StartMenuShortcut, exe);
        if (firstInstall) StartWithWindows.Set(true, exe);
        else StartWithWindows.UpdatePath(exe);
    }

    private static void WriteUninstallEntry(string exe)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey + AppPaths.Name);
        key.SetValue("DisplayName", AppPaths.IsTest ? "Quadra (self-test)" : "Quadra");
        key.SetValue("DisplayVersion", OwnVersion.ToString(3));
        key.SetValue("Publisher", "Alan Ribeiro Michelon");
        key.SetValue("DisplayIcon", exe + ",0");
        key.SetValue("InstallLocation", AppPaths.InstallFolder);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        key.SetValue("UninstallString", $"\"{exe}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{exe}\" --uninstall --quiet");
        key.SetValue("URLInfoAbout", Links.Repository);
        key.SetValue("HelpLink", Links.Repository + "/issues");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
    }

    /// <summary>
    /// Run by Uninstall in Settings → Apps (Quadra.exe --uninstall). Closes Quadra and removes the app, its
    /// shortcut, its startup and uninstall entries, and its settings. --quiet skips the questions.
    /// </summary>
    public static int Uninstall(bool quiet)
    {
        if (!quiet && MessageBox.Show(Loc.Get("Uninstall_Confirm"), "Quadra", MessageBoxButton.YesNo,
                                      MessageBoxImage.Question) != MessageBoxResult.Yes)
            return 1;
        try
        {
            Quit(RunningCopy());
            StartWithWindows.Set(false);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey + AppPaths.Name, throwOnMissingSubKey: false);
            if (File.Exists(AppPaths.StartMenuShortcut)) File.Delete(AppPaths.StartMenuShortcut);
            if (Directory.Exists(AppPaths.DataFolder)) Directory.Delete(AppPaths.DataFolder, recursive: true);
            if (IsRunningInstalled)
            {
                // A running program can't delete itself: a hidden command prompt removes the folder
                // a couple of seconds after this copy closes.
                Process.Start(new ProcessStartInfo("cmd.exe",
                    $"/c ping 127.0.0.1 -n 3 > nul & rd /s /q \"{AppPaths.InstallFolder}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            }
            else if (Directory.Exists(AppPaths.InstallFolder))
            {
                Directory.Delete(AppPaths.InstallFolder, recursive: true);
            }
        }
        catch (Exception e)
        {
            if (!quiet)
                MessageBox.Show(Loc.Format("Uninstall_Failed", e.Message), "Quadra", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 2;
        }
        if (!quiet) MessageBox.Show(Loc.Get("Uninstall_Done"), "Quadra", MessageBoxButton.OK, MessageBoxImage.Information);
        return 0;
    }

    // Other copies of Quadra

    private readonly record struct Copy(IntPtr Window, int ProcessId, string? Path);

    /// <summary>The copy of Quadra that is open now (found by its hidden window), if any.</summary>
    private static Copy? RunningCopy()
    {
        var window = Native.FindWindowEx(Native.HWND_MESSAGE, IntPtr.Zero, null, HotKeyCenter.WindowName);
        if (window == IntPtr.Zero) return null;
        Native.GetWindowThreadProcessId(window, out uint pid);
        if (pid == 0 || pid == Environment.ProcessId) return null;
        return new Copy(window, (int)pid, ProcessPath(pid));
    }

    /// <summary>Asks the open copy to quit and waits for it. Versions before 1.2 don't know how, so they are closed.</summary>
    private static void Quit(Copy? copy)
    {
        if (copy is not { } c) return;
        Process? process = null;
        try { process = Process.GetProcessById(c.ProcessId); } catch (ArgumentException) { }
        Native.PostMessage(c.Window, HotKeyCenter.QuitMessage, IntPtr.Zero, IntPtr.Zero);
        if (process == null) return;
        using (process)
        {
            if (process.WaitForExit(4000)) return;
            Log.Write($"Quadra (process {c.ProcessId}) didn't quit when asked; closing it");
            try
            {
                process.Kill();
                process.WaitForExit(3000);
            }
            catch (Exception e)
            {
                Log.Write($"Couldn't close it: {e.Message}");
            }
        }
    }

    private static string? ProcessPath(uint pid)
    {
        var handle = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var path = new StringBuilder(1024);
            uint size = (uint)path.Capacity;
            return Native.QueryFullProcessImageName(handle, 0, path, ref size) ? path.ToString() : null;
        }
        finally
        {
            Native.CloseHandle(handle);
        }
    }

    private static void CopyWithRetries(string source, string target)
    {
        // The old copy may take a moment to let go of its file after quitting.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Copy(source, target, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(250);
            }
        }
    }

    private static bool SamePath(string? a, string? b) =>
        a != null && b != null &&
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    // Start menu shortcut (through the Windows Shell's IShellLink)

    public static void CreateShortcut(string shortcut, string exe)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcut)!);
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(exe);
            link.SetWorkingDirectory(Path.GetDirectoryName(exe)!);
            link.SetDescription(Loc.Get("Shortcut_Description"));
            link.SetIconLocation(exe, 0);
            ((IPersistFile)link).Save(shortcut, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink;

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxIconPath, out int icon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int icon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
