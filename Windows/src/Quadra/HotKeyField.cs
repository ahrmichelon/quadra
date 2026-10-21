using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Quadra;

/// <summary>Button that records a shortcut: click it and press the keys. Esc cancels.</summary>
public sealed class HotKeyField : StackPanel
{
    private readonly Button button = new() { MinWidth = 150, Padding = new Thickness(10, 3, 10, 3) };
    private readonly Button clear = new()
    {
        Content = "",
        FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        FontSize = 10,
        Width = 24,
        Height = 24,
        Margin = new Thickness(4, 0, 0, 0),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Foreground = Brushes.Gray,
        Focusable = false,
    };
    private HotKeyCombo? combo;
    private bool recording;

    /// <summary>The app turns its shortcuts off while one is being recorded (otherwise the old one would fire).</summary>
    public static Action? RecordingStarted { get; set; }
    public static Action? RecordingStopped { get; set; }

    /// <summary>The user recorded a new shortcut or removed it (null).</summary>
    public event Action<HotKeyCombo?>? Changed;

    public HotKeyField()
    {
        Orientation = Orientation.Horizontal;
        button.ToolTip = Loc.Get("HotKey_RecordTip");
        clear.ToolTip = Loc.Get("HotKey_Remove");
        Children.Add(button);
        Children.Add(clear);

        button.Click += (_, _) => { if (recording) Stop(); else Start(); };
        button.PreviewKeyDown += OnKey;
        button.LostKeyboardFocus += (_, _) => Stop();
        clear.Click += (_, _) =>
        {
            Stop();
            Combo = null;
            Changed?.Invoke(null);
        };
        Unloaded += (_, _) => Stop();
        Update();
    }

    public HotKeyCombo? Combo
    {
        get => combo;
        set { combo = value; Update(); }
    }

    private void Update(string? message = null)
    {
        button.Content = message ?? (recording ? Loc.Get("HotKey_Press") : combo?.Display ?? Loc.Get("HotKey_Record"));
        button.FontWeight = recording ? FontWeights.SemiBold : FontWeights.Normal;
        clear.Visibility = combo != null && !recording ? Visibility.Visible : Visibility.Hidden;
    }

    private void Start()
    {
        recording = true;
        RecordingStarted?.Invoke();
        button.Focus();
        Update();
    }

    private void Stop()
    {
        if (!recording) return;
        recording = false;
        RecordingStopped?.Invoke();
        Update();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (!recording) return;
        e.Handled = true; // while recording, keys are only for the shortcut (Tab and Alt included)
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0 || Keys.IsModifier(vk)) return; // wait for the main key

        var held = Keyboard.Modifiers;
        int modifiers = 0;
        if (held.HasFlag(ModifierKeys.Control)) modifiers |= HotKeyCombo.Control;
        if (held.HasFlag(ModifierKeys.Alt)) modifiers |= HotKeyCombo.Alt;
        if (held.HasFlag(ModifierKeys.Shift)) modifiers |= HotKeyCombo.Shift;
        if (held.HasFlag(ModifierKeys.Windows)) modifiers |= HotKeyCombo.Win;

        if (vk == Keys.Escape && modifiers == 0)
        {
            Stop();
            return;
        }
        var recorded = new HotKeyCombo(vk, modifiers);
        if (!recorded.IsUsable)
        {
            WindowMover.Beep();
            Update(Loc.Get("HotKey_NeedsModifier"));
            return;
        }
        combo = recorded;
        Stop();
        Changed?.Invoke(recorded);
    }
}
