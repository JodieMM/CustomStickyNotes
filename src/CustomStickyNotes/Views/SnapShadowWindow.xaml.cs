using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CustomStickyNotes.Views;

/// <summary>A non-interactive ghost outline showing where a dragged note will land when dropped.</summary>
public partial class SnapShadowWindow : Window
{
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    public SnapShadowWindow()
    {
        InitializeComponent();
    }

    public void UpdatePreview(Rect rect, Color noteColor)
    {
        Left = rect.X;
        Top = rect.Y;
        Width = rect.Width;
        Height = rect.Height;

        ShadowRect.Fill = new SolidColorBrush(Color.FromArgb(60, noteColor.R, noteColor.G, noteColor.B));
        ShadowRect.Stroke = new SolidColorBrush(Color.FromArgb(170, noteColor.R, noteColor.G, noteColor.B));

        if (!IsVisible) Show();

        // The window actively being dragged is also topmost, and OS z-ordering favors it —
        // re-assert our own topmost position on every update so the ghost stays visible above it.
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void HidePreview()
    {
        if (IsVisible) Hide();
    }
}
