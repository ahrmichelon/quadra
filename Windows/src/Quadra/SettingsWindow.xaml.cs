using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Quadra;

/// <summary>Settings window: the app's shortcuts, the panel's look and the list of layouts.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore store;
    private Guid? selectedId;
    /// <summary>True while the window fills its controls, so their change events don't edit the settings.</summary>
    private bool updating;

    public SettingsWindow(SettingsStore store)
    {
        InitializeComponent();
        this.store = store;
        // On small laptop screens the window scrolls instead of going past the bottom of the screen.
        MaxHeight = SystemParameters.WorkArea.Height;

        Title = Loc.Get("Settings_Title");
        Icon = AppIcons.Window;
        AppIcon.Source = AppIcons.Large;
        RenderOptions.SetBitmapScalingMode(AppIcon, BitmapScalingMode.HighQuality);
        Tagline.Text = Loc.Format("Settings_Tagline", Links.Version);
        SupportText.Text = Loc.Get("Settings_Support");
        ShowGridLabel.Text = Loc.Get("Settings_ShowGrid");
        NextDisplayLabel.Text = Loc.Get("Settings_NextDisplay");
        PreviousDisplayLabel.Text = Loc.Get("Settings_PreviousDisplay");
        ShapeLabel.Text = Loc.Get("Settings_Shape");
        WidthLabel.Text = Loc.Get("Settings_Width");
        OpacityLabel.Text = Loc.Get("Settings_Opacity");
        OpacityHint.Text = Loc.Get("Settings_OpacityHint");
        SpacingLabel.Text = Loc.Get("Settings_Spacing");
        StartWithWindowsBox.Content = Loc.Get("Settings_StartWithWindows");
        LayoutsTitle.Text = Loc.Get("Settings_Layouts");
        LayoutsHint.Text = Loc.Get("Settings_LayoutsHint");
        AddButton.ToolTip = Loc.Get("Settings_NewLayout");
        RemoveButton.ToolTip = Loc.Get("Settings_DeleteLayout");
        UpButton.ToolTip = Loc.Get("Settings_MoveUp");
        DownButton.ToolTip = Loc.Get("Settings_MoveDown");
        EmptyText.Text = Loc.Get("Settings_ChooseLayout");
        NameLabel.Text = Loc.Get("Settings_Name");
        ShortcutLabel.Text = Loc.Get("Settings_Shortcut");
        AreaLabel.Text = Loc.Get("Settings_Area");
        AreaHint.Text = Loc.Get("Settings_AreaHint");
        FewerColumns.ToolTip = Loc.Get("Panel_FewerColumns");
        MoreColumns.ToolTip = Loc.Get("Panel_MoreColumns");
        FewerRows.ToolTip = Loc.Get("Panel_FewerRows");
        MoreRows.ToolTip = Loc.Get("Panel_MoreRows");
        AreaGrid.SetColors(Theme.Accent, Color.FromRgb(0x1A, 0x1A, 0x1A));

        ShapeBox.ItemsSource = PanelShapes.All.Select(s => s.Title()).ToList();
        SpacingBox.ItemsSource = AppSettings.GapChoices
            .Select(g => g == 0 ? Loc.Get("Menu_SpacingNone") : Loc.Format("Menu_SpacingPixels", g)).ToList();

        // App shortcuts and the panel's look
        ShowGridKey.Changed += combo => store.Edit(s => s.ShowGridHotKey = combo);
        NextDisplayKey.Changed += combo => store.Edit(s => s.NextScreenHotKey = combo);
        PreviousDisplayKey.Changed += combo => store.Edit(s => s.PreviousScreenHotKey = combo);
        ShapeBox.SelectionChanged += (_, _) =>
        {
            if (!updating && ShapeBox.SelectedIndex >= 0)
                store.Edit(s => s.PanelShape = PanelShapes.All[ShapeBox.SelectedIndex]);
        };
        WidthSlider.ValueChanged += (_, e) =>
        {
            WidthValue.Text = Loc.Format("Settings_Pixels", (int)e.NewValue);
            if (!updating) store.Edit(s => s.PanelSize = e.NewValue);
        };
        OpacitySlider.ValueChanged += (_, e) =>
        {
            OpacityValue.Text = $"{Math.Round(e.NewValue * 100)}%";
            if (!updating) store.Edit(s => s.PanelOpacity = e.NewValue);
        };
        SpacingBox.SelectionChanged += (_, _) =>
        {
            if (!updating && SpacingBox.SelectedIndex >= 0)
                store.Edit(s => s.Gap = AppSettings.GapChoices[SpacingBox.SelectedIndex]);
        };
        StartWithWindowsBox.Click += (_, _) =>
        {
            QuadraApp.SetStartWithWindows(StartWithWindowsBox.IsChecked == true);
            StartWithWindowsBox.IsChecked = StartWithWindows.IsEnabled;
        };
        SupportLink.Click += (_, _) => Links.Open(Links.Support);

        // Layouts
        LayoutList.SelectionChanged += (_, _) =>
        {
            if (updating) return;
            selectedId = (LayoutList.SelectedItem as ListBoxItem)?.Tag as Guid?;
            updating = true;
            try
            {
                RefreshButtons();
                RefreshEditor();
            }
            finally
            {
                updating = false;
            }
        };
        AddButton.Click += (_, _) => AddLayout();
        RemoveButton.Click += (_, _) => RemoveLayout();
        UpButton.Click += (_, _) => MoveLayout(-1);
        DownButton.Click += (_, _) => MoveLayout(1);
        NameBox.TextChanged += (_, _) =>
        {
            if (!updating) EditSelected(l => l.Name = NameBox.Text);
        };
        LayoutKey.Changed += combo => EditSelected(l => l.HotKey = combo);
        AreaGrid.Picked += selection => EditSelected(l => l.Selection = selection);
        FewerColumns.Click += (_, _) => EditSelected(l => l.Resize(AppSettings.ClampSize(l.Cols - 1), l.Rows));
        MoreColumns.Click += (_, _) => EditSelected(l => l.Resize(AppSettings.ClampSize(l.Cols + 1), l.Rows));
        FewerRows.Click += (_, _) => EditSelected(l => l.Resize(l.Cols, AppSettings.ClampSize(l.Rows - 1)));
        MoreRows.Click += (_, _) => EditSelected(l => l.Resize(l.Cols, AppSettings.ClampSize(l.Rows + 1)));

        store.Changed += Refresh;
        Closed += (_, _) => store.Changed -= Refresh;

        selectedId = store.Current.Layouts.FirstOrDefault()?.Id;
        Refresh();
    }

    /// <summary>Fills every control from the settings.</summary>
    private void Refresh()
    {
        updating = true;
        try
        {
            var s = store.Current;
            ShowGridKey.Combo = s.ShowGridHotKey;
            ShowGridWarning.Text = WarningText(s.ShowGridHotKey);
            NextDisplayKey.Combo = s.NextScreenHotKey;
            NextDisplayWarning.Text = WarningText(s.NextScreenHotKey);
            PreviousDisplayKey.Combo = s.PreviousScreenHotKey;
            PreviousDisplayWarning.Text = WarningText(s.PreviousScreenHotKey);
            ShapeBox.SelectedIndex = Array.IndexOf(PanelShapes.All, s.PanelShape);
            WidthSlider.Value = s.PanelSize;
            WidthValue.Text = Loc.Format("Settings_Pixels", (int)s.PanelSize);
            OpacitySlider.Value = s.PanelOpacity;
            OpacityValue.Text = $"{Math.Round(s.PanelOpacity * 100)}%";
            SpacingBox.SelectedIndex = Array.IndexOf(AppSettings.GapChoices, s.Gap);
            StartWithWindowsBox.IsChecked = StartWithWindows.IsEnabled;
            RefreshList();
            RefreshEditor();
        }
        finally
        {
            updating = false;
        }
    }

    private string WarningText(HotKeyCombo? combo) => store.Warning(combo) is { } w ? "⚠ " + w : "";

    private void RefreshList()
    {
        var layouts = store.Current.Layouts;
        if (selectedId is { } id && layouts.All(l => l.Id != id)) selectedId = null;
        LayoutList.Items.Clear();
        foreach (var layout in layouts)
        {
            var item = new ListBoxItem { Tag = layout.Id, Content = Row(layout), Padding = new Thickness(6, 5, 6, 5) };
            LayoutList.Items.Add(item);
            if (layout.Id == selectedId) LayoutList.SelectedItem = item;
        }
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        var layouts = store.Current.Layouts;
        int index = layouts.FindIndex(l => l.Id == selectedId);
        RemoveButton.IsEnabled = index >= 0;
        UpButton.IsEnabled = index > 0;
        DownButton.IsEnabled = index >= 0 && index < layouts.Count - 1;
    }

    /// <summary>A row of the list: thumbnail of the area, name and shortcut.</summary>
    private UIElement Row(SavedLayout layout)
    {
        var row = new DockPanel();
        var preview = new AreaPreview { Unit = layout.UnitRect, Width = 30, Height = 19, Margin = new Thickness(0, 0, 8, 0) };
        DockPanel.SetDock(preview, Dock.Left);
        row.Children.Add(preview);

        var shortcut = new TextBlock
        {
            Text = layout.HotKey?.Display ?? "—",
            Foreground = Brushes.Gray,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(shortcut, Dock.Right);
        row.Children.Add(shortcut);

        if (store.Warning(layout.HotKey) is { } warning)
        {
            var sign = new TextBlock
            {
                Text = "⚠",
                ToolTip = warning,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0x53, 0x09)),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(sign, Dock.Right);
            row.Children.Add(sign);
        }

        row.Children.Add(new TextBlock
        {
            Text = layout.Name.Length > 0 ? layout.Name : Loc.Get("Menu_Untitled"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    private void RefreshEditor()
    {
        var layout = selectedId is { } id ? store.Layout(id) : null;
        Editor.Visibility = layout != null ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = layout != null ? Visibility.Collapsed : Visibility.Visible;
        if (layout == null) return;

        if (NameBox.Text != layout.Name) NameBox.Text = layout.Name;
        LayoutKey.Combo = layout.HotKey;
        LayoutWarning.Text = WarningText(layout.HotKey);

        var (width, height) = EditorGridSize();
        AreaGrid.Width = width;
        AreaGrid.Height = height;
        NameBox.Width = width + 14;
        AreaGrid.Cols = layout.Cols;
        AreaGrid.Rows = layout.Rows;
        AreaGrid.Selection = layout.Selection;
        ColumnsText.Text = Loc.Format("Panel_Columns", layout.Cols);
        RowsText.Text = Loc.Format("Panel_Rows", layout.Rows);
        FewerColumns.IsEnabled = layout.Cols > AppSettings.MinSize;
        MoreColumns.IsEnabled = layout.Cols < AppSettings.MaxSize;
        FewerRows.IsEnabled = layout.Rows > AppSettings.MinSize;
        MoreRows.IsEnabled = layout.Rows < AppSettings.MaxSize;
    }

    /// <summary>
    /// Size of the editing grid, with the main display's proportion
    /// (a bit taller on ultrawide displays, so it is easy to drag).
    /// </summary>
    private static (double Width, double Height) EditorGridSize()
    {
        var screen = SystemParameters.WorkArea;
        double ratio = Math.Max(screen.Height / Math.Max(screen.Width, 1), 0.4);
        double width = Math.Min(360, 270 / ratio);
        return (Math.Round(width), Math.Round(width * ratio));
    }

    private void EditSelected(Action<SavedLayout> change)
    {
        if (selectedId is not { } id) return;
        store.Edit(s =>
        {
            if (s.Layouts.FirstOrDefault(l => l.Id == id) is { } layout) change(layout);
        });
    }

    private void AddLayout()
    {
        var layout = new SavedLayout { Name = Loc.Get("Settings_NewLayout"), Cols = 6, Rows = 6, C0 = 0, R0 = 0, C1 = 2, R1 = 5 };
        selectedId = layout.Id;
        store.Edit(s => s.Layouts.Add(layout));
        NameBox.Focus();
        NameBox.SelectAll();
    }

    private void RemoveLayout()
    {
        var layouts = store.Current.Layouts;
        int i = layouts.FindIndex(l => l.Id == selectedId);
        if (i < 0) return;
        var next = i + 1 < layouts.Count ? layouts[i + 1] : i > 0 ? layouts[i - 1] : null;
        selectedId = next?.Id;
        store.Edit(s => s.Layouts.RemoveAt(i));
    }

    private void MoveLayout(int step)
    {
        var layouts = store.Current.Layouts;
        int i = layouts.FindIndex(l => l.Id == selectedId), j = i + step;
        if (i < 0 || j < 0 || j >= layouts.Count) return;
        store.Edit(s => (s.Layouts[i], s.Layouts[j]) = (s.Layouts[j], s.Layouts[i]));
    }
}

/// <summary>The Quadra icon, for windows.</summary>
internal static class AppIcons
{
    private static readonly Uri Source = new("pack://application:,,,/Assets/Quadra.ico", UriKind.Absolute);

    private static BitmapDecoder Decoder =>
        BitmapDecoder.Create(Source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

    /// <summary>The icon with all its sizes, for a window's title bar and taskbar button.</summary>
    public static ImageSource Window => BitmapFrame.Create(Source);

    /// <summary>The largest size in the icon, for showing big.</summary>
    public static ImageSource Large => Decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
}
