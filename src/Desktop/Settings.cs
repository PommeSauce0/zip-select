using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZipSelect.Desktop;

public sealed class Settings
{
    public bool Dark { get; set; }
    public string Language { get; set; } = "fr";
    public ExtractionOptions Options { get; set; } = new();
    public string Source { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    public string Destination { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Extractions");
    public string Extensions { get; set; } = "";
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZipSelect-Avalonia", "settings.json");
    public static Settings Load(string path)
    {
        try { var value = File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJson.Default.Settings) : null; if (value != null) { value.Options ??= new(); return value; } }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return new();
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, SettingsJson.Default.Settings));
        File.Move(temporary, path, true);
    }
}

// Generated at compile time so the trimmed executable needs no reflection.
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Settings))]
internal partial class SettingsJson : JsonSerializerContext;
