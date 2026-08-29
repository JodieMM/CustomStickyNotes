using System;
using System.IO;

namespace CustomStickyNotes.Services;

public static class AppPaths
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CustomStickyNotes");

    public static string NotesFile => Path.Combine(DataDir, "notes.json");

    public static string SettingsFile => Path.Combine(DataDir, "settings.json");

    public static string PaletteFile => Path.Combine(DataDir, "palette.json");

    public static string BundledPaletteFile => Path.Combine(AppContext.BaseDirectory, "Themes", "palette.json");
}
