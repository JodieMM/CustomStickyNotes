using CustomStickyNotes.Models;

namespace CustomStickyNotes.Services;

public class SettingsStore
{
    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        Settings = JsonStore.Load<AppSettings>(AppPaths.SettingsFile) ?? new AppSettings();
    }

    public void Save()
    {
        JsonStore.Save(AppPaths.SettingsFile, Settings);
    }
}
