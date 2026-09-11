using System.Collections.Generic;
using System.IO;
using CustomStickyNotes.Models;

namespace CustomStickyNotes.Services;

/// <summary>
/// Loads the color palette from an editable JSON file in AppData, seeded from the
/// bundled default on first run so it can be tweaked later without touching the install.
/// </summary>
public class PaletteService
{
    public List<PaletteColor> Colors { get; private set; } = new();

    public void Load()
    {
        if (!File.Exists(AppPaths.PaletteFile) && File.Exists(AppPaths.BundledPaletteFile))
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.Copy(AppPaths.BundledPaletteFile, AppPaths.PaletteFile);
        }

        Colors = JsonStore.Load<List<PaletteColor>>(AppPaths.PaletteFile)
                 ?? JsonStore.Load<List<PaletteColor>>(AppPaths.BundledPaletteFile)
                 ?? DefaultPalette();
    }

    private static List<PaletteColor> DefaultPalette() => new()
    {
        new PaletteColor { Name = "Periwinkle", Hex = "#ADC8F5" },
        new PaletteColor { Name = "Cornflower", Hex = "#8EA8E2" },
        new PaletteColor { Name = "Iris", Hex = "#8A96D0" },
        new PaletteColor { Name = "Thistle", Hex = "#9A93C6" },
        new PaletteColor { Name = "Lilac", Hex = "#C2ADD9" },
        new PaletteColor { Name = "Orchid", Hex = "#BE93C6" },
        new PaletteColor { Name = "Mauve Pink", Hex = "#D9A9CF" },
    };
}
