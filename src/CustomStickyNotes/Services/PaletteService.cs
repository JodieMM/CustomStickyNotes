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
        new PaletteColor { Name = "Blush Pink", Hex = "#F7D6E0" },
        new PaletteColor { Name = "Lavender", Hex = "#E3D9F7" },
        new PaletteColor { Name = "Mint", Hex = "#D6F0E0" },
        new PaletteColor { Name = "Peach", Hex = "#FBE3D0" },
        new PaletteColor { Name = "Sky Blue", Hex = "#D6EAF7" },
        new PaletteColor { Name = "Cream", Hex = "#FFF6E5" },
    };
}
