namespace CustomStickyNotes.Models;

public class AppSettings
{
    /// <summary>Consistent gap kept between a note and its neighbors, and between a note and the screen edge, in DIPs.</summary>
    public double NoteGutter { get; set; } = 16;

    /// <summary>How close (in DIPs) a note's edge must get to a neighbor/screen edge before it snaps.</summary>
    public double SnapThreshold { get; set; } = 28;

    public double DefaultNoteWidth { get; set; } = 260;

    public double DefaultNoteHeight { get; set; } = 260;

    public string DefaultFontFamily { get; set; } = "Segoe UI";

    public double DefaultFontSize { get; set; } = 14;

    public bool LaunchOnStartup { get; set; }

    public bool NotesVisible { get; set; } = true;

    public string DefaultColorHex { get; set; } = "#9A93C6";
}
