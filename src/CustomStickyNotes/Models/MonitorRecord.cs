using System.Drawing;

namespace CustomStickyNotes.Models;

public class MonitorRecord
{
    /// <summary>Stable across reconnects: derived from resolution + position rather than OS index/device name.</summary>
    public required string StableId { get; init; }

    public required Rectangle Bounds { get; init; }

    public required Rectangle WorkingArea { get; init; }

    public bool IsPrimary { get; init; }
}
