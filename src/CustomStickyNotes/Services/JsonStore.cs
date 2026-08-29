using System.IO;
using System.Text.Json;

namespace CustomStickyNotes.Services;

public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Save<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(value, Options);
        File.WriteAllText(path, json);
    }
}
