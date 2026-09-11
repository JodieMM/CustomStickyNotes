using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using CustomStickyNotes.Models;
using CustomStickyNotes.Services;
using CustomStickyNotes.Views;

namespace CustomStickyNotes;

public partial class App : System.Windows.Application
{
    private readonly SettingsStore _settingsStore = new();
    private readonly NoteStore _noteStore = new();
    private readonly PaletteService _paletteService = new();
    private readonly MonitorService _monitorService = new();
    private readonly Dictionary<Guid, NoteWindow> _noteWindows = new();

    private SnapService? _snapService;
    private DispatcherTimer? _autosaveTimer;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private OverviewWindow? _overviewWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settingsStore.Load();
        _paletteService.Load();
        _noteStore.Load();
        _snapService = new SnapService(_settingsStore.Settings);

        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _autosaveTimer.Tick += (_, _) =>
        {
            _autosaveTimer!.Stop();
            PersistAll();
        };

        var monitors = _monitorService.GetMonitors();
        var primary = _monitorService.GetPrimary(monitors);
        var occupied = new Dictionary<string, List<Rect>>();

        foreach (var note in _noteStore.Notes)
        {
            var window = CreateNoteWindow(note, monitors, primary, occupied);
            if (!note.IsArchived && _settingsStore.Settings.NotesVisible)
                window.Show();
        }

        BuildTrayIcon();

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        PersistAll();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.OnExit(e);
    }

    // ---- Note lifecycle -------------------------------------------------

    private NoteWindow CreateNoteWindow(
        NoteModel note,
        List<MonitorRecord> monitors,
        MonitorRecord primary,
        Dictionary<string, List<Rect>> occupied)
    {
        var window = new NoteWindow(note, _paletteService, _snapService!, _monitorService, GetSiblingRects);
        window.Changed += OnNoteChanged;
        window.ArchiveRequested += OnNoteArchiveRequested;
        window.DeleteRequested += OnNoteDeleteRequested;
        window.DuplicateRequested += OnNoteDuplicateRequested;
        window.NewNoteRequested += OnNoteNewRequested;

        var savedMonitor = _monitorService.FindByStableId(monitors, note.MonitorId);
        MonitorRecord targetMonitor;
        double offsetX, offsetY;
        bool temporary;

        if (savedMonitor != null)
        {
            targetMonitor = savedMonitor;
            offsetX = note.OffsetX;
            offsetY = note.OffsetY;
            temporary = false;
        }
        else
        {
            targetMonitor = primary;
            var point = _snapService!.FindOpenPosition(primary.WorkingArea, note.Width, note.Height, GetOccupiedList(occupied, primary.StableId));
            (offsetX, offsetY) = _snapService.ScreenPointToOffset(primary.WorkingArea, point.X, point.Y);
            // Only "temporary" if the note had a saved monitor that's now missing — a brand-new note isn't displaced, it's just new.
            temporary = note.MonitorId != null;
        }

        window.PlaceOnMonitor(targetMonitor, offsetX, offsetY, temporary);
        if (!note.IsArchived)
            GetOccupiedList(occupied, targetMonitor.StableId).Add(new Rect(
                targetMonitor.WorkingArea.X + offsetX, targetMonitor.WorkingArea.Y + offsetY, note.Width, note.Height));

        _noteWindows[note.Id] = window;
        return window;
    }

    private static List<Rect> GetOccupiedList(Dictionary<string, List<Rect>> dict, string monitorId)
    {
        if (!dict.TryGetValue(monitorId, out var list))
        {
            list = new List<Rect>();
            dict[monitorId] = list;
        }
        return list;
    }

    private Dictionary<string, List<Rect>> BuildOccupiedMap()
    {
        var dict = new Dictionary<string, List<Rect>>();
        foreach (var w in _noteWindows.Values)
        {
            if (w.Model.IsArchived || w.CurrentMonitorId is null) continue;
            GetOccupiedList(dict, w.CurrentMonitorId).Add(new Rect(w.Left, w.Top, w.Width, w.Height));
        }
        return dict;
    }

    /// <summary>Live rects of other visible, active notes on the given monitor — used for drag-time and drop-time gutter snapping.</summary>
    private IEnumerable<Rect> GetSiblingRects(NoteWindow self, string? monitorId)
    {
        if (monitorId is null) yield break;
        foreach (var w in _noteWindows.Values)
        {
            if (ReferenceEquals(w, self) || w.Model.IsArchived || !w.IsVisible) continue;
            if (w.CurrentMonitorId != monitorId) continue;
            yield return new Rect(w.Left, w.Top, w.Width, w.Height);
        }
    }

    private void OnNoteChanged(NoteWindow window)
    {
        _autosaveTimer?.Stop();
        _autosaveTimer?.Start();
        _overviewWindow?.RefreshList();
    }

    private void OnNoteArchiveRequested(NoteWindow window)
    {
        window.Model.IsArchived = true;
        window.Hide();
        PersistAll();
        _overviewWindow?.RefreshList();
    }

    private void OnNoteDeleteRequested(NoteWindow window)
    {
        UnhookAndRemove(window);
        _noteStore.Notes.RemoveAll(n => n.Id == window.Model.Id);
        window.Close();
        PersistAll();
        _overviewWindow?.RefreshList();
    }

    private void OnNoteDuplicateRequested(NoteWindow source)
    {
        var copy = new NoteModel
        {
            ContentXaml = source.Model.ContentXaml,
            PlainText = source.Model.PlainText,
            ColorHex = source.Model.ColorHex,
            Width = source.Model.Width,
            Height = source.Model.Height,
            FontFamily = source.Model.FontFamily,
            FontSize = source.Model.FontSize,
            IsTopmost = source.Model.IsTopmost,
        };
        _noteStore.Notes.Add(copy);

        var monitors = _monitorService.GetMonitors();
        var primary = _monitorService.GetPrimary(monitors);
        var window = CreateNoteWindow(copy, monitors, primary, BuildOccupiedMap());
        window.Show();
        window.Activate();

        PersistAll();
        _overviewWindow?.RefreshList();
    }

    private void OnNoteNewRequested(NoteWindow window) => CreateNewNote();

    private void UnhookAndRemove(NoteWindow window)
    {
        window.Changed -= OnNoteChanged;
        window.ArchiveRequested -= OnNoteArchiveRequested;
        window.DeleteRequested -= OnNoteDeleteRequested;
        window.DuplicateRequested -= OnNoteDuplicateRequested;
        window.NewNoteRequested -= OnNoteNewRequested;
        _noteWindows.Remove(window.Model.Id);
    }

    // ---- Public API for tray menu / overview window ---------------------

    public IReadOnlyList<NoteModel> GetAllNotesSnapshot() => _noteStore.Notes.ToList();

    public void CreateNewNote()
    {
        var settings = _settingsStore.Settings;
        var note = new NoteModel
        {
            ColorHex = settings.DefaultColorHex,
            Width = settings.DefaultNoteWidth,
            Height = settings.DefaultNoteHeight,
            FontFamily = settings.DefaultFontFamily,
            FontSize = settings.DefaultFontSize,
        };
        _noteStore.Notes.Add(note);

        var monitors = _monitorService.GetMonitors();
        var primary = _monitorService.GetPrimary(monitors);
        var window = CreateNoteWindow(note, monitors, primary, BuildOccupiedMap());
        window.Show();
        window.Activate();

        PersistAll();
        _overviewWindow?.RefreshList();
    }

    public void ShowNoteWindow(Guid id)
    {
        if (!_noteWindows.TryGetValue(id, out var window)) return;
        if (window.Model.IsArchived)
        {
            RestoreNote(id);
            return;
        }
        window.BringToFront();
    }

    public void ArchiveNote(Guid id)
    {
        if (_noteWindows.TryGetValue(id, out var window))
            OnNoteArchiveRequested(window);
    }

    public void RestoreNote(Guid id)
    {
        if (!_noteWindows.TryGetValue(id, out var window)) return;

        window.Model.IsArchived = false;

        var monitors = _monitorService.GetMonitors();
        var primary = _monitorService.GetPrimary(monitors);
        var savedMonitor = _monitorService.FindByStableId(monitors, window.Model.MonitorId);
        var occupied = BuildOccupiedMap();

        if (savedMonitor != null)
        {
            window.PlaceOnMonitor(savedMonitor, window.Model.OffsetX, window.Model.OffsetY, temporary: false);
        }
        else
        {
            var point = _snapService!.FindOpenPosition(primary.WorkingArea, window.Model.Width, window.Model.Height, GetOccupiedList(occupied, primary.StableId));
            var (offsetX, offsetY) = _snapService.ScreenPointToOffset(primary.WorkingArea, point.X, point.Y);
            window.PlaceOnMonitor(primary, offsetX, offsetY, temporary: true);
        }

        window.Show();
        window.Activate();

        PersistAll();
        _overviewWindow?.RefreshList();
    }

    public void DeleteNote(Guid id)
    {
        if (_noteWindows.TryGetValue(id, out var window))
            OnNoteDeleteRequested(window);
    }

    public void ShowOverview()
    {
        if (_overviewWindow is null)
        {
            _overviewWindow = new OverviewWindow(this);
            _overviewWindow.Closed += (_, _) => _overviewWindow = null;
        }
        else
        {
            _overviewWindow.RefreshList();
        }
        _overviewWindow.Show();
        _overviewWindow.Activate();
    }

    public void ToggleNotesVisible()
    {
        _settingsStore.Settings.NotesVisible = !_settingsStore.Settings.NotesVisible;
        foreach (var window in _noteWindows.Values.Where(w => !w.Model.IsArchived))
        {
            if (_settingsStore.Settings.NotesVisible) window.Show();
            else window.Hide();
        }
        PersistAll();
    }

    private void PersistAll()
    {
        _noteStore.Save();
        _settingsStore.Save();
    }

    // ---- Monitor change handling -----------------------------------------

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RemapMonitors);
    }

    private void RemapMonitors()
    {
        var monitors = _monitorService.GetMonitors();
        var primary = _monitorService.GetPrimary(monitors);
        var occupied = BuildOccupiedMap();

        foreach (var window in _noteWindows.Values.Where(w => !w.Model.IsArchived))
        {
            if (window.IsTemporaryPlacement)
            {
                var savedMonitor = _monitorService.FindByStableId(monitors, window.Model.MonitorId);
                if (savedMonitor != null)
                {
                    window.PlaceOnMonitor(savedMonitor, window.Model.OffsetX, window.Model.OffsetY, temporary: false);
                    GetOccupiedList(occupied, savedMonitor.StableId).Add(new Rect(
                        savedMonitor.WorkingArea.X + window.Model.OffsetX,
                        savedMonitor.WorkingArea.Y + window.Model.OffsetY,
                        window.Model.Width, window.Model.Height));
                }
            }
            else if (_monitorService.FindByStableId(monitors, window.CurrentMonitorId) is null)
            {
                var point = _snapService!.FindOpenPosition(primary.WorkingArea, window.Model.Width, window.Model.Height, GetOccupiedList(occupied, primary.StableId));
                var (offsetX, offsetY) = _snapService.ScreenPointToOffset(primary.WorkingArea, point.X, point.Y);
                window.PlaceOnMonitor(primary, offsetX, offsetY, temporary: true);
                GetOccupiedList(occupied, primary.StableId).Add(new Rect(point.X, point.Y, window.Model.Width, window.Model.Height));
            }
        }

        PersistAll();
    }

    // ---- Tray icon ---------------------------------------------------------

    private void BuildTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Visible = true,
            Text = "Custom Sticky Notes",
        };
        _trayIcon.DoubleClick += (_, _) => ShowOverview();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("New Note", null, (_, _) => CreateNewNote());
        menu.Items.Add("Show All Notes", null, (_, _) => ShowOverview());

        var toggleItem = new System.Windows.Forms.ToolStripMenuItem("Notes Visible")
        {
            Checked = _settingsStore.Settings.NotesVisible,
            CheckOnClick = true,
        };
        toggleItem.Click += (_, _) => ToggleNotesVisible();
        menu.Items.Add(toggleItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var startupItem = new System.Windows.Forms.ToolStripMenuItem("Launch on Startup")
        {
            Checked = StartupService.IsEnabled(),
            CheckOnClick = true,
        };
        startupItem.Click += (_, _) =>
        {
            StartupService.SetEnabled(startupItem.Checked);
            _settingsStore.Settings.LaunchOnStartup = startupItem.Checked;
            PersistAll();
        };
        menu.Items.Add(startupItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon.ContextMenuStrip = menu;
    }

    /// <summary>Draws a simple sticky-note-with-heart glyph at runtime so the app doesn't need a shipped .ico asset.</summary>
    private static System.Drawing.Icon CreateTrayIcon()
    {
        const int size = 32;
        using var bitmap = new System.Drawing.Bitmap(size, size);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.Clear(System.Drawing.Color.Transparent);

            var noteRect = new System.Drawing.Rectangle(2, 2, size - 4, size - 4);
            using (var path = RoundedRectPath(noteRect, 6))
            using (var noteBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 255, 255, 255)))
            using (var notePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillPath(noteBrush, path);
                g.DrawPath(notePen, path);
            }

            using var heartFont = new System.Drawing.Font("Segoe UI Symbol", 16f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            using var heartBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(255, 217, 169, 207));
            const string heart = "♥";
            var textSize = g.MeasureString(heart, heartFont);
            g.DrawString(heart, heartFont, heartBrush, (size - textSize.Width) / 2, (size - textSize.Height) / 2 - 1);
        }

        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRectPath(System.Drawing.Rectangle rect, int radius)
    {
        var diameter = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void ExitApplication()
    {
        PersistAll();
        Shutdown();
    }
}
