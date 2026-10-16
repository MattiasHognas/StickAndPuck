using System.Globalization;
using System.Text.Json;

namespace StickAndPuck.App.Core;

/// <summary>A language the game's text comes in.</summary>
public enum Language
{
    /// <summary>Default: English, data/lang/en.json.</summary>
    English,

    /// <summary>Swedish, data/lang/sv.json.</summary>
    Swedish,
}

/// <summary>The string table: every player-visible text by key, one flat JSON file per language in
/// data/lang (en.json, sv.json), placeholders as {0}, {1}. <see cref="Text.Keys"/> holds the keys.
/// The settings pick the language (<see cref="Use"/>); a key a language lacks falls back to English,
/// and a key missing there too shows as itself. Numbers are formatted with the invariant culture.</summary>
public static partial class Text
{
    /// <summary>The folder the language files are read from.</summary>
    public const string Folder = "data/lang";

    static readonly Dictionary<Language, IReadOnlyDictionary<string, string>> Tables = [];
    static readonly Lock Gate = new();

    /// <summary>Gets the language the game shows.</summary>
    public static Language Current { get; private set; } = Language.English;

    /// <summary>Shows the game in a language from now on.</summary>
    public static void Use(Language language)
    {
        Current = language;
    }

    /// <summary>Gets a text in the current language.</summary>
    public static string Get(string key) => Lookup(Current, key);

    /// <summary>Gets a text in the current language with its {0}, {1} placeholders filled.</summary>
    public static string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Get(key), args);

    /// <summary>Gets a language's own name for itself, as the settings screen shows it.</summary>
    public static string NameOf(Language language) => Lookup(language, Keys.LanguageName);

    /// <summary>Gets the file a language is read from.</summary>
    public static string FileOf(Language language) => Path.Combine(Folder, (language == Language.Swedish ? "sv" : "en") + ".json");

    /// <summary>Reads a language file: a flat JSON object of key to text.</summary>
    public static IReadOnlyDictionary<string, string> Read(string path)
    {
        return JsonSerializer.Deserialize(File.ReadAllText(path), TextJsonContext.Default.DictionaryStringString)
            ?? throw new InvalidDataException($"{path} is empty");
    }

    static string Lookup(Language language, string key)
    {
        if (Table(language).GetValueOrDefault(key) is { } text) return text;
        if (language != Language.English && Table(Language.English).GetValueOrDefault(key) is { } english) return english;
        return key;
    }

    /// <summary>A language's table, read once; an unreadable file is logged and reads as empty.</summary>
    static IReadOnlyDictionary<string, string> Table(Language language)
    {
        lock (Gate)
        {
            if (Tables.TryGetValue(language, out var table)) return table;
            string path = FileOf(language);
            try
            {
                table = Read(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                Console.WriteLine($"SAP: text: {path} unreadable ({e.Message})");
                table = new Dictionary<string, string>(StringComparer.Ordinal);
            }
            Tables[language] = table;
            return table;
        }
    }
}
