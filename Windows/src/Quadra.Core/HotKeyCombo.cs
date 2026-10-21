using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Quadra;

/// <summary>
/// A key combination: a Windows virtual-key code plus modifiers, in the format RegisterHotKey uses.
/// </summary>
public readonly record struct HotKeyCombo(
    [property: JsonPropertyName("key")] int Key,
    [property: JsonPropertyName("modifiers")] int Modifiers)
{
    // Modifier flags (MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN).
    public const int Alt = 0x1;
    public const int Control = 0x2;
    public const int Shift = 0x4;
    public const int Win = 0x8;

    public static readonly HotKeyCombo ShowGridDefault = new(Keys.G, Control | Alt);
    public static readonly HotKeyCombo NextScreenDefault = new(Keys.Right, Control | Alt | Shift);
    public static readonly HotKeyCombo PreviousScreenDefault = new(Keys.Left, Control | Alt | Shift);

    /// <summary>
    /// Global shortcuts need Ctrl, Alt or Win (or an F key), otherwise they get in the way of typing.
    /// </summary>
    [JsonIgnore]
    public bool IsUsable => (Modifiers & (Control | Alt | Win)) != 0 || Keys.IsFunctionKey(Key);

    /// <summary>Text such as "Ctrl+Alt+←".</summary>
    [JsonIgnore]
    public string Display
    {
        get
        {
            var parts = new List<string>();
            if ((Modifiers & Win) != 0) parts.Add("Win");
            if ((Modifiers & Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & Alt) != 0) parts.Add("Alt");
            if ((Modifiers & Shift) != 0) parts.Add("Shift");
            parts.Add(Keys.Name(Key));
            return string.Join("+", parts);
        }
    }
}

/// <summary>Windows virtual-key codes used by Quadra, and their names.</summary>
public static class Keys
{
    public const int Back = 0x08, Tab = 0x09, Return = 0x0D, Escape = 0x1B, Space = 0x20;
    public const int PageUp = 0x21, PageDown = 0x22, End = 0x23, Home = 0x24;
    public const int Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28;
    public const int Insert = 0x2D, Delete = 0x2E;
    public const int D0 = 0x30, A = 0x41, C = 0x43, G = 0x47, Z = 0x5A;
    public const int NumPad0 = 0x60, NumPad9 = 0x69;
    public const int Multiply = 0x6A, Add = 0x6B, Subtract = 0x6D, Decimal = 0x6E, Divide = 0x6F;
    public const int F1 = 0x70, F24 = 0x87;

    /// <summary>
    /// Names keys that depend on the keyboard layout (such as ç or ;). Set by the Windows app;
    /// returns null when it doesn't know the key.
    /// </summary>
    public static Func<int, string?>? LayoutKeyName { get; set; }

    public static bool IsFunctionKey(int key) => key >= F1 && key <= F24;

    /// <summary>
    /// The modifier keys themselves (Shift, Ctrl, Alt, Win in all their variants). They can't be
    /// the main key of a shortcut.
    /// </summary>
    public static bool IsModifier(int key) =>
        key is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or (>= 0xA0 and <= 0xA5);

    /// <summary>Name of a key, such as "G", "←", "F5" or "Num 4".</summary>
    public static string Name(int key)
    {
        switch (key)
        {
            case Left: return "←";
            case Right: return "→";
            case Up: return "↑";
            case Down: return "↓";
            case Return: return "Enter";
            case Space: return Loc.Get("Key_Space");
            case Tab: return "Tab";
            case Escape: return "Esc";
            case Back: return "Backspace";
            case Delete: return "Del";
            case Insert: return "Ins";
            case Home: return "Home";
            case End: return "End";
            case PageUp: return "PgUp";
            case PageDown: return "PgDn";
            case Multiply: return "Num *";
            case Add: return "Num +";
            case Subtract: return "Num -";
            case Decimal: return "Num .";
            case Divide: return "Num /";
        }
        if (IsFunctionKey(key)) return $"F{key - F1 + 1}";
        if (key >= A && key <= Z) return ((char)key).ToString();
        if (key >= D0 && key <= D0 + 9) return ((char)key).ToString();
        if (key >= NumPad0 && key <= NumPad9) return $"Num {key - NumPad0}";
        return LayoutKeyName?.Invoke(key) ?? $"#{key}";
    }
}
