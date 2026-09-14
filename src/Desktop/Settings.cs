using System.Text.Json;

namespace ZipSelect.Desktop;

public sealed class Settings
{
    public bool Dark { get; set; }
    public string Language { get; set; } = "fr";
    public ExtractionOptions Options { get; set; } = new();
    public string Source { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    public string Destination { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Extractions");
    public string Extensions { get; set; } = "";
    static readonly JsonSerializerOptions Format = new() { IncludeFields = true, WriteIndented = true };
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZipSelect-Avalonia", "settings.json");
    public static Settings Load(string path)
    {
        try { var value = File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Format) : null; if (value != null) { value.Options ??= new(); return value; } }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return new();
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, Format));
        File.Move(temporary, path, true);
    }
    public static ExtractionOptions Copy(ExtractionOptions o) => new() {
        SearchSubfolders = o.SearchSubfolders, IncludeArchiveSubfolders = o.IncludeArchiveSubfolders,
        PreserveFolders = o.PreserveFolders, RenameConflicts = o.RenameConflicts, FolderPerArchive = o.FolderPerArchive
    };
}
