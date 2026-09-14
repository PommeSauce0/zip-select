using System.Text.Json;

namespace ZipSelect;

/// <summary>Shared UI and engine messages. File names and external exception messages are never translated.</summary>
public static class Texts
{
    public sealed record Translation(string fr, string en);
    static readonly Dictionary<string, Translation> Catalog = Load();
    public static string Language { get; private set; } = "fr";
    public static IReadOnlyDictionary<string, Translation> Entries => Catalog;

    static Dictionary<string, Translation> Load()
    {
        using var stream = typeof(Texts).Assembly.GetManifestResourceStream("ZipSelect.Translations.json")
            ?? throw new InvalidOperationException("Missing translation catalog.");
        return JsonSerializer.Deserialize<Dictionary<string, Translation>>(stream)
            ?? throw new InvalidOperationException("Invalid translation catalog.");
    }

    public static void SetLanguage(string language) => Language = language == "en" ? "en" : "fr";
    public static string Get(string key)
    {
        var entry = Catalog[key];
        return Language == "en" ? entry.en : entry.fr;
    }
    public static string Format(string key, params object[] values) => string.Format(Get(key), values);
}
