using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quadra;

/// <summary>
/// Holds the settings, saves them after every change and tells the app about it.
/// The file is %AppData%\Quadra\settings.json on Windows.
/// </summary>
public sealed class SettingsStore
{
    private readonly string? path;
    private HashSet<HotKeyCombo> unavailable = [];

    public AppSettings Current { get; private set; }

    /// <summary>Called after any change.</summary>
    public event Action? Changed;

    /// <summary>Messages about problems reading or writing the file (the app writes them to its log).</summary>
    public static Action<string>? Log { get; set; }

    /// <summary>With a null path it only uses the defaults and saves nothing (for tests).</summary>
    public SettingsStore(string? path)
    {
        this.path = path;
        Current = Load(path);
    }

    /// <summary>Changes the settings, then saves and tells the app.</summary>
    public void Edit(Action<AppSettings> change)
    {
        change(Current);
        Current.Normalize();
        Save();
        Changed?.Invoke();
    }

    public SavedLayout? Layout(Guid id) => Current.Layouts.FirstOrDefault(l => l.Id == id);

    /// <summary>Shortcuts Windows refused to register (usually because another app already uses them).</summary>
    public IReadOnlySet<HotKeyCombo> Unavailable => unavailable;

    public void SetUnavailable(IEnumerable<HotKeyCombo> combos)
    {
        var set = combos.ToHashSet();
        if (set.SetEquals(unavailable)) return;
        unavailable = set;
        Changed?.Invoke();
    }

    /// <summary>Every shortcut in use: the app's own ones and each layout's.</summary>
    public IEnumerable<HotKeyCombo?> AllHotKeys =>
        new[] { Current.ShowGridHotKey, Current.NextScreenHotKey, Current.PreviousScreenHotKey }
            .Concat(Current.Layouts.Select(l => l.HotKey));

    /// <summary>Warning to show next to a shortcut, or null when all is well.</summary>
    public string? Warning(HotKeyCombo? combo)
    {
        if (combo is not { } c) return null;
        if (AllHotKeys.Count(h => h == c) > 1) return Loc.Get("Warn_Duplicate");
        if (unavailable.Contains(c)) return Loc.Get("Warn_InUse");
        return null;
    }

    private static AppSettings Load(string? path)
    {
        if (path == null || !File.Exists(path)) return new AppSettings();
        try
        {
            return AppSettings.FromJson(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            // Keep the broken file for reference and start over with the defaults.
            Log?.Invoke($"Couldn't read {path}: {e.Message}");
            try { File.Copy(path, path + ".broken", overwrite: true); } catch (IOException) { }
            return new AppSettings();
        }
    }

    private void Save()
    {
        if (path == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Write to a temporary file first, so a crash never leaves a half-written file.
            string temp = path + ".tmp";
            File.WriteAllText(temp, Current.ToJson());
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log?.Invoke($"Couldn't save {path}: {e.Message}");
        }
    }
}
