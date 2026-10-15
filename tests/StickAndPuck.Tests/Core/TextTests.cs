using System.Reflection;
using System.Text.RegularExpressions;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests;

/// <summary>
/// The string table: every language file has exactly English's keys, every key constant is in
/// every file, each text's {0} placeholders match English's, Swedish uses only letters the pixel
/// font has, and no text drawing call in the app's source is handed a literal.
/// </summary>
public sealed partial class TextTests : CheckedTest
{
    /// <summary>Characters the pixel font draws (art/ui/font.fnt); text is upper-cased before drawing.</summary>
    const string FontGlyphs = " !\"#%&'()*+,-./0123456789:;<=>?ABCDEFGHIJKLMNOPQRSTUVWXYZ_ÅÄÖÉÜ";

    static IReadOnlyDictionary<string, string> Table(Language language) =>
        Text.Read(Path.Combine(AppContext.BaseDirectory, Text.FileOf(language)));

    [Test]
    public void EveryLanguageHasEnglishsKeys()
    {
        var english = Table(Language.English);
        foreach (var language in Enum.GetValues<Language>())
        {
            var table = Table(language);
            var missing = english.Keys.Except(table.Keys, StringComparer.Ordinal).ToList();
            var extra = table.Keys.Except(english.Keys, StringComparer.Ordinal).ToList();
            Check(missing.Count == 0, $"{language} has every English key (missing: {string.Join(", ", missing)})");
            Check(extra.Count == 0, $"{language} has no key English lacks (extra: {string.Join(", ", extra)})");
        }
    }

    [Test]
    public void EveryKeyConstantIsInEveryFile()
    {
        var keys = typeof(Text.Keys).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()).ToList();
        Check(keys.Count > 0, $"{keys.Count} key constants");
        foreach (var language in Enum.GetValues<Language>())
        {
            var table = Table(language);
            var missing = keys.Where(k => !table.ContainsKey(k)).ToList();
            Check(missing.Count == 0, $"{language} has every key constant (missing: {string.Join(", ", missing)})");
        }
    }

    [Test]
    public void PlaceholdersMatchEnglish()
    {
        var english = Table(Language.English);
        foreach (var language in Enum.GetValues<Language>())
        {
            var table = Table(language);
            foreach (var (key, text) in table)
            {
                if (english.GetValueOrDefault(key) is not { } en) continue;
                Check(string.Equals(Holes(text), Holes(en), StringComparison.Ordinal), $"{language} {key} has English's placeholders {Holes(en)}, not {Holes(text)}");
            }
        }
    }

    [Test]
    public void EveryTextIsInTheFont()
    {
        foreach (var language in Enum.GetValues<Language>())
        {
            foreach (var (key, text) in Table(language))
            {
                string lacking = new([.. PlaceholderPattern().Replace(text, "").ToUpperInvariant().Where(c => !FontGlyphs.Contains(c, StringComparison.Ordinal)).Distinct()]);
                Check(lacking.Length == 0, $"{language} {key} uses only letters the font has (lacks: {lacking})");
            }
        }
    }

    [Test]
    public void LanguageSwitchAndFallback()
    {
        Check(string.Equals(Text.NameOf(Language.English), "ENGLISH", StringComparison.Ordinal), "English names itself ENGLISH");
        Check(string.Equals(Text.NameOf(Language.Swedish), "SVENSKA", StringComparison.Ordinal), "Swedish names itself SVENSKA");
        Check(string.Equals(Text.Get("NoSuchKey"), "NoSuchKey", StringComparison.Ordinal), "a missing key shows as itself");
    }

    [Test]
    public void NoLiteralReachesATextDrawCall()
    {
        var found = RepoRoot();
        Check(found.HasValue, "the repository root (src.slnx) is found above the test output");
        if (!found.TryGet(out string root)) return;
        var offenders = new List<string>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src", "StickAndPuck.App"), "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            if (name.EndsWith("Scenes.cs", StringComparison.Ordinal) || name.EndsWith("Scene.cs", StringComparison.Ordinal) || DebugOnly.Contains(name)) continue;
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
                if (DrawnLiteral().Match(lines[i]) is { Success: true } m && HasWords(m.Groups["text"].Value)) offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
        }
        Check(offenders.Count == 0, $"text draw calls get their text from Text (literals at: {string.Join(", ", offenders)})");
    }

    /// <summary>Files whose drawn text is a debug overlay only (the fps line, the debug panels).</summary>
    static readonly HashSet<string> DebugOnly = new(StringComparer.Ordinal) { "Game.cs", "MatchRules.cs", "LinesSystem.cs", "TacticsSystem.cs", "SpecialTeamsSystem.cs", "StandInPlay.cs" };

    /// <summary>A string literal as the text of a draw call, a panel, a hint, a menu row or a banner.</summary>
    [GeneratedRegex(@"(\b(DrawText|DrawInSlot|Left|Right|Centred|Clip|Panel|Hint|Row)\(|new MenuItem\(|new\(""panel\w*"",\s*)\s*\$?""(?<text>[^""]*)""")]
    private static partial Regex DrawnLiteral();

    /// <summary>Whether a literal has words outside its interpolation holes; a score like {a}-{b} has none.</summary>
    static bool HasWords(string literal) => Hole().Replace(literal, "").Any(char.IsLetter);

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex Hole();

    static string Holes(string text) => string.Join(",", PlaceholderPattern().Matches(text).Select(m => m.Value).Distinct().Order(StringComparer.Ordinal));

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    static Maybe<string> RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is { }; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "src.slnx"))) return dir.FullName;
        return Maybe<string>.None;
    }
}
