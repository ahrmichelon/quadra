using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Quadra;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Quadra.exe --self-test <folder>: checks the parts that talk to Windows (see SelfTest.cs).
        if (args.Length > 0 && args[0] == "--self-test")
            return SelfTest.Main(args.Length > 1 ? args[1] : "self-test");

        // Uninstall in Settings → Apps runs Quadra.exe --uninstall.
        if (args.Contains("--uninstall")) return Installer.Uninstall(quiet: args.Contains("--quiet"));

        // Opened from Downloads (or an update downloaded to Temp): install or update Quadra, then open the
        // installed copy. --portable keeps it running from where it is (handy while working on the code).
        if (!args.Contains("--portable") && !Installer.IsRunningInstalled && Installer.InstallAndOpen()) return 0;

        // Only one Quadra at a time. Opening Quadra.exe again shows the settings of the one already running
        // (handy when its icon is hidden under the ^ arrow next to the clock).
        using var single = new Mutex(initiallyOwned: true, $@"Local\{AppPaths.Name}.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            HotKeyCenter.AskRunningCopyToShowSettings();
            return 0;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var quadra = new QuadraApp(app);

        // An unexpected error is written to the log instead of closing Quadra.
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log.Write($"Error: {e.Exception}");
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write($"Fatal error: {e.ExceptionObject}");

        app.Startup += (_, _) =>
        {
            try
            {
                quadra.Start();
            }
            catch (Exception e)
            {
                Log.Write($"Quadra couldn't start: {e}");
                MessageBox.Show($"Quadra couldn't start.\n\n{e.Message}\n\n{Log.FilePath}", "Quadra",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                quadra.Dispose();
                app.Shutdown(1);
            }
        };
        app.Exit += (_, _) => quadra.Dispose();
        return app.Run();
    }
}
