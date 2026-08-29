using System;

namespace CustomStickyNotes.Models;

public class NoteModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ContentXaml { get; set; } = string.Empty;

    public string ColorHex { get; set; } = "#FFF6E5";

    public double Width { get; set; } = 260;

    public double Height { get; set; } = 260;

    /// <summary>Stable identity of the monitor this note is saved on (resolution + relative position).</summary>
    public string? MonitorId { get; set; }

    /// <summary>Position relative to the saved monitor's working-area top-left corner, in DIPs.</summary>
    public double OffsetX { get; set; }

    public double OffsetY { get; set; }

    public bool IsArchived { get; set; }

    public bool IsTopmost { get; set; } = true;

    public string FontFamily { get; set; } = "Segoe UI";

    public double FontSize { get; set; } = 14;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Plain-text snapshot of the content, kept in sync for fast search without parsing XAML.</summary>
    public string PlainText { get; set; } = string.Empty;
}
