using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using CustomStickyNotes.Models;
using CustomStickyNotes.Services;

namespace CustomStickyNotes.Views;

public partial class NoteWindow : Window
{
    private const int WM_MOVING = 0x0216;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private const string HeartPrefix = "♡ ";

    private readonly PaletteService _palette;
    private readonly SnapService _snapService;
    private readonly MonitorService _monitorService;
    private readonly Func<NoteWindow, string?, IEnumerable<Rect>> _siblingRectsProvider;
    private readonly SnapShadowWindow _shadow = new();
    private Popup _colorPopup = new();
    private bool _suppressChangeEvents;
    private bool _chromeHover;

    public NoteModel Model { get; }

    /// <summary>True while this note is displaced onto a fallback monitor because its saved monitor is disconnected.</summary>
    public bool IsTemporaryPlacement { get; set; }

    /// <summary>Stable id of the monitor this note is actually displayed on right now (may differ from Model's saved target while temporarily displaced).</summary>
    public string? CurrentMonitorId { get; private set; }

    public double CurrentOffsetX { get; private set; }

    public double CurrentOffsetY { get; private set; }

    /// <summary>Raised whenever content, color, pin state, position, or size changes and should be persisted.</summary>
    public event Action<NoteWindow>? Changed;
    public event Action<NoteWindow>? ArchiveRequested;
    public event Action<NoteWindow>? DeleteRequested;
    public event Action<NoteWindow>? DuplicateRequested;
    public event Action<NoteWindow>? NewNoteRequested;

    public NoteWindow(
        NoteModel model,
        PaletteService palette,
        SnapService snapService,
        MonitorService monitorService,
        Func<NoteWindow, string?, IEnumerable<Rect>> siblingRectsProvider)
    {
        InitializeComponent();

        Model = model;
        _palette = palette;
        _snapService = snapService;
        _monitorService = monitorService;
        _siblingRectsProvider = siblingRectsProvider;

        Width = model.Width;
        Height = model.Height;
        Topmost = model.IsTopmost;

        _suppressChangeEvents = true;
        ApplyColor(model.ColorHex);
        Rtb.FontFamily = new FontFamily(model.FontFamily);
        Rtb.FontSize = model.FontSize;
        DeserializeContent(model.ContentXaml);
        UpdateToggleStates();
        _suppressChangeEvents = false;

        PinButton.Opacity = model.IsTopmost ? 1.0 : 0.45;

        ChromePopup.PlacementTarget = RootBorder;
        BuildColorPopup();

        RootBorder.MouseEnter += (_, _) => UpdateChromeVisibility();
        RootBorder.MouseLeave += (_, _) => UpdateChromeVisibility();
        Activated += (_, _) => UpdateChromeVisibility();
        Deactivated += (_, _) => UpdateChromeVisibility();
        LocationChanged += (_, _) => NudgeChromePopup();
        Closed += (_, _) => _shadow.Close();
    }

    /// <summary>The floating chrome popup is only shown while the note is hovered, focused, or the popup itself is hovered.</summary>
    private void UpdateChromeVisibility()
    {
        ChromePopup.IsOpen = IsActive || RootBorder.IsMouseOver || _chromeHover;
    }

    private void ChromeBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        _chromeHover = true;
        UpdateChromeVisibility();
    }

    private void ChromeBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        _chromeHover = false;
        UpdateChromeVisibility();
    }

    /// <summary>WPF popups don't automatically follow their placement target's window while it's being dragged; nudging an offset forces a reposition.</summary>
    private void NudgeChromePopup()
    {
        if (!ChromePopup.IsOpen) return;
        var offset = ChromePopup.HorizontalOffset;
        ChromePopup.HorizontalOffset = offset + 1;
        ChromePopup.HorizontalOffset = offset;
    }

    public void PlaceOnMonitor(MonitorRecord monitor, double offsetX, double offsetY, bool temporary)
    {
        var point = _snapService.OffsetToScreenPoint(monitor.WorkingArea, offsetX, offsetY);
        Left = point.X;
        Top = point.Y;
        IsTemporaryPlacement = temporary;
        CurrentMonitorId = monitor.StableId;
        CurrentOffsetX = offsetX;
        CurrentOffsetY = offsetY;
    }

    public void BringToFront()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        var wasTopmost = Topmost;
        Topmost = true;
        Topmost = wasTopmost || Model.IsTopmost;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WndProc);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_MOVING:
                UpdateDragPreview(lParam);
                break;
            case WM_EXITSIZEMOVE:
                _shadow.HidePreview();
                SnapAndPersistPosition();
                break;
        }
        return IntPtr.Zero;
    }

    /// <summary>While the note is being dragged (not resized), shows a ghost of where it will land if dropped now.</summary>
    private void UpdateDragPreview(IntPtr lParam)
    {
        var r = Marshal.PtrToStructure<RECT>(lParam);
        var proposed = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

        var monitor = FindMonitorForRect(proposed);
        if (monitor is null) return;

        var siblings = _siblingRectsProvider(this, monitor.StableId).ToList();
        var snapped = _snapService.ComputeSnappedRect(proposed, monitor.WorkingArea, siblings);

        var color = (Color)ColorConverter.ConvertFromString(Model.ColorHex);
        _shadow.UpdatePreview(snapped, color);
    }

    private void SnapAndPersistPosition()
    {
        var proposed = new Rect(Left, Top, Width, Height);
        var monitor = FindMonitorForRect(proposed);
        if (monitor is null) return;

        var siblings = _siblingRectsProvider(this, monitor.StableId).ToList();
        var snapped = _snapService.ComputeSnappedRect(proposed, monitor.WorkingArea, siblings);

        Left = snapped.X;
        Top = snapped.Y;

        var (offsetX, offsetY) = _snapService.ScreenPointToOffset(monitor.WorkingArea, snapped.X, snapped.Y);

        Model.MonitorId = monitor.StableId;
        Model.OffsetX = offsetX;
        Model.OffsetY = offsetY;
        Model.Width = Width;
        Model.Height = Height;
        IsTemporaryPlacement = false;
        CurrentMonitorId = monitor.StableId;
        CurrentOffsetX = offsetX;
        CurrentOffsetY = offsetY;

        Changed?.Invoke(this);
    }

    private MonitorRecord? FindMonitorForRect(Rect rect)
    {
        var monitors = _monitorService.GetMonitors();
        if (monitors.Count == 0) return null;

        var centerX = (int)(rect.X + rect.Width / 2);
        var centerY = (int)(rect.Y + rect.Height / 2);

        return monitors.FirstOrDefault(m => m.Bounds.Contains(centerX, centerY))
               ?? monitors.OrderBy(m => DistanceSquared(m.Bounds, centerX, centerY)).First();
    }

    private static double DistanceSquared(System.Drawing.Rectangle rect, int x, int y)
    {
        var dx = Math.Max(rect.Left - x, Math.Max(0, x - rect.Right));
        var dy = Math.Max(rect.Top - y, Math.Max(0, y - rect.Bottom));
        return (double)dx * dx + (double)dy * dy;
    }

    private void ApplyColor(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        RootBorder.Background = new SolidColorBrush(color);
        ChromeBorder.Background = new SolidColorBrush(color);
        ColorDotBrush.Color = color;
        Model.ColorHex = hex;
    }

    private void BuildColorPopup()
    {
        var panel = new WrapPanel { Width = 150, Margin = new Thickness(6) };
        foreach (var c in _palette.Colors)
        {
            var color = (Color)ColorConverter.ConvertFromString(c.Hex);
            var swatch = new Border
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(color),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = c.Name,
                Tag = c.Hex,
            };
            swatch.MouseLeftButtonUp += (_, _) =>
            {
                ApplyColor((string)swatch.Tag);
                _colorPopup.IsOpen = false;
                Changed?.Invoke(this);
            };
            panel.Children.Add(swatch);
        }

        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.3 },
            Child = panel,
        };

        _colorPopup = new Popup
        {
            PlacementTarget = ColorButton,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            Child = border,
        };
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e) => _colorPopup.IsOpen = true;

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Model.IsTopmost = !Model.IsTopmost;
        Topmost = Model.IsTopmost;
        PinButton.Opacity = Model.IsTopmost ? 1.0 : 0.45;
        Changed?.Invoke(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => ArchiveRequested?.Invoke(this);

    private void DeleteNote_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this, "Delete this note permanently? This can't be undone.",
            "Delete Note", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
            DeleteRequested?.Invoke(this);
    }

    private void DuplicateNote_Click(object sender, RoutedEventArgs e) => DuplicateRequested?.Invoke(this);

    private void NewNote_Click(object sender, RoutedEventArgs e) => NewNoteRequested?.Invoke(this);

    private void BoldButton_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleBold.Execute(null, Rtb);
        Rtb.Focus();
    }

    private void ItalicButton_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleItalic.Execute(null, Rtb);
        Rtb.Focus();
    }

    private void StrikeButton_Click(object sender, RoutedEventArgs e)
    {
        var selection = Rtb.Selection;
        if (!selection.IsEmpty)
        {
            var current = selection.GetPropertyValue(Inline.TextDecorationsProperty);
            var isStrike = current is TextDecorationCollection tdc && tdc.Count > 0;
            selection.ApplyPropertyValue(Inline.TextDecorationsProperty, isStrike ? null : TextDecorations.Strikethrough);
        }
        Rtb.Focus();
    }

    /// <summary>
    /// WPF has no built-in support for a custom bullet glyph, so bullet lists here are native
    /// <see cref="List"/>/<see cref="ListItem"/> structures with MarkerStyle=None and a literal
    /// heart-character Run standing in for the marker. Numbered lists remain fully native.
    /// </summary>
    private void BulletsButton_Click(object sender, RoutedEventArgs e)
    {
        var currentList = FindAncestor<List>(Rtb.Selection.Start.Paragraph);
        var isHeartList = currentList != null && currentList.MarkerStyle == TextMarkerStyle.None;

        if (isHeartList)
        {
            // Strip our text markers and temporarily restore a real bullet marker style so the
            // native command recognizes this as a bulleted list and removes it correctly.
            StripHeartPrefixes(currentList!);
            currentList!.MarkerStyle = TextMarkerStyle.Disc;
        }

        EditingCommands.ToggleBullets.Execute(null, Rtb);

        if (!isHeartList)
            ApplyHeartStyleToNewBulletLists();

        UpdateToggleStates();
        Rtb.Focus();
    }

    private void NumberingButton_Click(object sender, RoutedEventArgs e)
    {
        var currentList = FindAncestor<List>(Rtb.Selection.Start.Paragraph);
        if (currentList != null && currentList.MarkerStyle == TextMarkerStyle.None)
            StripHeartPrefixes(currentList);

        EditingCommands.ToggleNumbering.Execute(null, Rtb);

        UpdateToggleStates();
        Rtb.Focus();
    }

    /// <summary>Runs after a bullet toggle turns a selection into a (native, Disc-marker) list, swapping in the heart-marker convention.</summary>
    private void ApplyHeartStyleToNewBulletLists()
    {
        // Snapshot before mutating: WPF's TextElement enumerators are invalidated by ANY document
        // edit, not just structural changes to the collection being walked.
        var lists = EnumerateLists(Rtb.Document.Blocks).ToList();
        foreach (var list in lists)
        {
            if (list.MarkerStyle != TextMarkerStyle.Disc) continue;
            list.MarkerStyle = TextMarkerStyle.None;
            foreach (var item in list.ListItems.ToList())
                if (item.Blocks.FirstBlock is Paragraph p)
                    AddHeartPrefixIfMissing(p);
        }
    }

    private static void StripHeartPrefixes(List list)
    {
        foreach (var item in list.ListItems.ToList())
        {
            if (item.Blocks.FirstBlock is Paragraph p && p.Inlines.FirstInline is Run r && r.Text.StartsWith(HeartPrefix))
                r.Text = r.Text.Substring(HeartPrefix.Length);
        }
    }

    private static void AddHeartPrefixIfMissing(Paragraph p)
    {
        var first = p.Inlines.FirstInline;
        if (first is Run r && r.Text.StartsWith(HeartPrefix)) return;
        if (first == null) p.Inlines.Add(new Run(HeartPrefix));
        else p.Inlines.InsertBefore(first, new Run(HeartPrefix));
    }

    private static IEnumerable<List> EnumerateLists(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            if (block is List list)
            {
                yield return list;
                foreach (var item in list.ListItems)
                    foreach (var nested in EnumerateLists(item.Blocks))
                        yield return nested;
            }
            else if (block is Section section)
            {
                foreach (var nested in EnumerateLists(section.Blocks))
                    yield return nested;
            }
        }
    }

    /// <summary>Continues the heart-bullet convention onto the new list item created when Enter splits a bulleted paragraph.</summary>
    private void Rtb_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Dispatcher.BeginInvoke(new Action(EnsureHeartPrefixAtCaret), DispatcherPriority.Input);
    }

    private void EnsureHeartPrefixAtCaret()
    {
        var para = Rtb.CaretPosition?.Paragraph;
        if (para == null) return;

        var list = FindAncestor<List>(para);
        if (list == null || list.MarkerStyle != TextMarkerStyle.None) return;

        var first = para.Inlines.FirstInline;
        if (first is Run r && r.Text.StartsWith(HeartPrefix)) return;

        _suppressChangeEvents = true;
        if (first == null) para.Inlines.Add(new Run(HeartPrefix));
        else para.Inlines.InsertBefore(first, new Run(HeartPrefix));
        _suppressChangeEvents = false;

        Rtb.CaretPosition = para.ContentEnd;
        PersistContent();
        Changed?.Invoke(this);
    }

    private void Rtb_SelectionChanged(object sender, RoutedEventArgs e) => UpdateToggleStates();

    private void UpdateToggleStates()
    {
        var selection = Rtb.Selection;

        BoldButton.IsChecked = selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight fw
                                && fw == FontWeights.Bold;

        ItalicButton.IsChecked = selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle fsy
                                  && fsy == FontStyles.Italic;

        StrikeButton.IsChecked = selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection tdc
                                  && tdc.Count > 0;

        var list = FindAncestor<List>(selection.Start.Paragraph);
        if (list != null)
        {
            var isBullet = list.MarkerStyle == TextMarkerStyle.None;
            BulletsButton.IsChecked = isBullet;
            NumberingButton.IsChecked = !isBullet;
        }
        else
        {
            BulletsButton.IsChecked = false;
            NumberingButton.IsChecked = false;
        }
    }

    private static T? FindAncestor<T>(TextElement? element) where T : TextElement
    {
        var parent = element?.Parent;
        while (parent != null)
        {
            if (parent is T match) return match;
            parent = (parent as TextElement)?.Parent;
        }
        return null;
    }

    private void Rtb_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressChangeEvents) return;
        PersistContent();
        Changed?.Invoke(this);
    }

    private void PersistContent()
    {
        Model.ContentXaml = SerializeContent();
        Model.PlainText = new TextRange(Rtb.Document.ContentStart, Rtb.Document.ContentEnd).Text.Trim();
        Model.ModifiedAt = DateTime.UtcNow;
    }

    private string SerializeContent()
    {
        var range = new TextRange(Rtb.Document.ContentStart, Rtb.Document.ContentEnd);
        using var stream = new System.IO.MemoryStream();
        range.Save(stream, DataFormats.Xaml);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void DeserializeContent(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return;
        try
        {
            var range = new TextRange(Rtb.Document.ContentStart, Rtb.Document.ContentEnd);
            using var stream = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(xaml));
            range.Load(stream, DataFormats.Xaml);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or ArgumentException or FormatException)
        {
            // Malformed/legacy content — leave the note blank rather than crash.
        }
    }
}
