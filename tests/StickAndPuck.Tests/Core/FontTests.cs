using System.Globalization;
using System.Text.Json;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Core
{
    /// <summary>
    /// The pixel font (art/ui/font.fnt) draws every letter the data hands it: every string in the club
    /// files (player, club and arena names), upper-cased as the game draws them (the language files are
    /// checked by TextTests against the same glyphs), so <see cref="Letters.Plain"/> never swaps one for its plain form.
    /// </summary>
    public sealed class FontTests : CheckedTest
    {
        /// <summary>The code points font.fnt has a glyph for.</summary>
        internal static HashSet<char> Glyphs()
        {
            return
            [
                .. ResourceLoader
                    .ReadText("art/ui/font.fnt")
                    .Split('\n')
                    .Where(line => line.StartsWith("char id=", StringComparison.Ordinal))
                    .Select(line =>
                        (char)
                            int.Parse(
                                line["char id=".Length..].Split(' ')[0],
                                CultureInfo.InvariantCulture
                            )
                    ),
            ];
        }

        private static IEnumerable<string> Strings(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Strings(p.Value)),
                JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
                JsonValueKind.String => [element.GetString() ?? ""],
                JsonValueKind.Undefined
                or JsonValueKind.Number
                or JsonValueKind.True
                or JsonValueKind.False
                or JsonValueKind.Null => [],
                _ => [],
            };
        }

        [Test]
        public void EveryClubStringIsInTheFont()
        {
            HashSet<char> glyphs = Glyphs();
            List<string> files = [.. ResourceLoader.List("data/clubs", ".json")];
            Check(files.Count >= 46, $"{files.Count} club files are bundled (SHL and NHL)");
            int names = 0;
            foreach (string file in files)
            {
                using JsonDocument doc = JsonDocument.Parse(ResourceLoader.ReadText(file));
                foreach (string text in Strings(doc.RootElement))
                {
                    string upper = text.ToUpperInvariant();
                    string lacking = new([.. upper.Where(c => !glyphs.Contains(c)).Distinct()]);
                    Check(
                        lacking.Length == 0,
                        $"{file}: \"{text}\" is in the font (lacks: {lacking})"
                    );
                    string shown = Letters.Plain(upper, glyphs.Contains);
                    Check(
                        string.Equals(shown, upper, StringComparison.Ordinal),
                        $"{file}: \"{text}\" draws as {upper}, not plain {shown}"
                    );
                    names++;
                }
            }
            Check(
                names > 1000,
                string.Create(CultureInfo.InvariantCulture, $"{names} club strings checked")
            );
        }
    }
}
