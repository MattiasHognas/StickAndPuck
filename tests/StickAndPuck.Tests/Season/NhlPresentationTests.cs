using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season
{
    /// <summary>
    /// The NHL presentation (W118): each league's screens name only their own league (standings, playoff
    /// and leaders headings, round names, the champions and their trophy) in English and Swedish, no
    /// screen picks an SHL heading key around <see cref="LeagueText"/>, the wild card view picks the
    /// same clubs as the playoff field, and every NHL club and player name draws with the pixel font.
    /// </summary>
    public sealed class NhlPresentationTests : CheckedTest
    {
        /// <summary>Characters the pixel font draws (art/ui/font.fnt); text is upper-cased before drawing.</summary>
        private const string FontGlyphs =
            " !\"#%&'()*+,-./0123456789:;<=>?ABCDEFGHIJKLMNOPQRSTUVWXYZ_ÅÄÖÉÜ";

        private static readonly Lazy<IReadOnlyList<ClubData>> Nhl = new(Clubs.LoadNhl);

        private static IEnumerable<string> Keys(LeagueText t)
        {
            return
            [
                t.FinalTable,
                t.Round,
                t.Playoffs,
                t.ChampionsOf,
                t.Champions,
                t.Trophy,
                t.LeadersSeason,
                t.LeadersPlayoff,
                .. t.Rounds,
            ];
        }

        [Test]
        public void EachLeaguesTextNamesOnlyItsOwnLeague()
        {
            foreach (Language language in Enum.GetValues<Language>())
            {
                IReadOnlyDictionary<string, string> table = Text.Read(Text.FileOf(language));
                foreach (
                    (LeagueText t, string own, string other) in new[]
                    {
                        (LeagueText.Nhl, "NHL", "SHL"),
                        (LeagueText.Shl, "SHL", "NHL"),
                    }
                )
                {
                    foreach (string key in Keys(t))
                    {
                        string text = table.TryGetValue(key, out string found) ? found : "";
                        Check(text.Length > 0, $"{language} {own}: {key} is in the table");
                        Check(
                            !text.Contains(other, StringComparison.OrdinalIgnoreCase),
                            $"{language} {own}: {key} = \"{text}\" does not say {other}"
                        );
                        Check(
                            !text.Contains("LE MAT", StringComparison.OrdinalIgnoreCase)
                                || ReferenceEquals(t, LeagueText.Shl),
                            $"{language} {own}: {key} = \"{text}\" names no SHL trophy"
                        );
                        Check(
                            !text.Contains("STANLEY", StringComparison.OrdinalIgnoreCase)
                                || ReferenceEquals(t, LeagueText.Nhl),
                            $"{language} {own}: {key} = \"{text}\" names no NHL trophy"
                        );
                    }
                }

                Check(
                    table[LeagueText.Nhl.Trophy].Contains("STANLEY CUP", StringComparison.Ordinal)
                        && table[LeagueText.Nhl.RoundKey(3)]
                            .Contains("STANLEY CUP", StringComparison.Ordinal),
                    $"{language}: the NHL plays the Stanley Cup final for the Stanley Cup"
                );
                Check(
                    table[LeagueText.Shl.Trophy].Contains("LE MAT", StringComparison.Ordinal),
                    $"{language}: the SHL champion lifts the Le Mat trophy"
                );
                Check(
                    table[LeagueText.Nhl.LeadersPlayoff].Contains("NHL", StringComparison.Ordinal)
                        && table[LeagueText.Nhl.LeadersSeason]
                            .Contains("NHL", StringComparison.Ordinal),
                    $"{language}: the NHL stats headings say NHL"
                );
            }
        }

        [Test]
        public void LeaguesPickTheirOwnKeys()
        {
            Check(ReferenceEquals(LeagueText.Of(League.Nhl), LeagueText.Nhl), "the NHL's keys");
            Check(ReferenceEquals(LeagueText.Of(League.Shl), LeagueText.Shl), "the SHL's keys");
            Check(
                !Keys(LeagueText.Nhl).Intersect(Keys(LeagueText.Shl), StringComparer.Ordinal).Any(),
                "the leagues share no heading, trophy or round key"
            );
            Check(
                string.Equals(
                    LeagueText.Nhl.RoundKey(-1),
                    Text.Keys.RoundFirst,
                    StringComparison.Ordinal
                )
                    && string.Equals(
                        LeagueText.Nhl.RoundKey(9),
                        Text.Keys.RoundStanleyCupFinal,
                        StringComparison.Ordinal
                    ),
                "NHL round names clamp to the first round and the Stanley Cup final"
            );
        }

        [Test]
        public void NoScreenPicksAnShlHeadingAroundLeagueText()
        {
            Maybe<string> found = RepoRoot();
            Check(found.HasValue, "the repository root (src.slnx) is found above the test output");
            if (!found.TryGet(out string root))
            {
                return;
            }

            string[] shlOnly =
            [
                "K.ShlFinalTable",
                "K.ShlRound",
                "K.ShlPlayoffs",
                "K.ShlChampions",
                "K.LeadersHeadingSeason",
                "K.LeadersHeadingPlayoff",
                "K.RoundPlayIn",
                "K.RoundQuarterFinals",
                "K.RoundSemiFinals",
            ];
            List<string> offenders = [];
            foreach (
                string file in Directory.EnumerateFiles(
                    Path.Combine(root, "src", "StickAndPuck.App"),
                    "*.cs",
                    SearchOption.AllDirectories
                )
            )
            {
                if (
                    string.Equals(Path.GetFileName(file), "LeagueText.cs", StringComparison.Ordinal)
                )
                {
                    continue;
                }

                string source = File.ReadAllText(file);
                offenders.AddRange(
                    shlOnly
                        .Where(k =>
                            source.Contains(k + ")", StringComparison.Ordinal)
                            || source.Contains(k + ",", StringComparison.Ordinal)
                            || source.Contains(k + " ", StringComparison.Ordinal)
                            || source.Contains(k + ";", StringComparison.Ordinal)
                        )
                        .Select(k => $"{Path.GetFileName(file)}: {k}")
                );
            }

            Check(
                offenders.Count == 0,
                $"SHL heading keys are only picked through LeagueText ({string.Join(", ", offenders)})"
            );
        }

        [Test]
        public void WildCardViewPicksTheFieldsWildCards()
        {
            IReadOnlyList<ClubData> clubs = Nhl.Value;
            SeasonState season = SeasonState.New(clubs, clubs[0].Id, 1);
            // the Atlantic's 4th and 5th beat the Metropolitan's 4th; the Pacific's 4th beats the Central's
            List<string> atlantic = Ids(season, "Atlantic"),
                metro = Ids(season, "Metropolitan"),
                central = Ids(season, "Central"),
                pacific = Ids(season, "Pacific");
            List<string> order =
            [
                .. metro.Take(3),
                .. atlantic.Take(5),
                .. pacific.Take(4),
                .. central.Take(3),
                .. metro.Skip(3),
                .. central.Skip(3),
                .. atlantic.Skip(5),
                .. pacific.Skip(4),
            ];
            List<StandingRow> table =
            [
                .. order.Select((id, i) => new StandingRow(id, 82, 40, 0, 0, 42, 0, 0, 200 - i)),
            ];
            IReadOnlyList<WildCardTable> view = WildCardStandings.Of(season, table);
            NhlField field = NhlSeeding.Field(season, table);

            Check(
                view.Select(c => c.Conference)
                    .SequenceEqual(["Eastern", "Western"], StringComparer.Ordinal),
                "two conferences, Eastern first"
            );
            WildCardTable east = view[0],
                west = view[1];
            Check(
                Same(east.Leaders[0].Division, "Metropolitan")
                    && Same(west.Leaders[0].Division, "Pacific"),
                "the division with the better leader comes first"
            );
            Check(
                east.Leaders.All(d => d.Rows.Count == NhlSeeding.PerDivision)
                    && east.Leaders[0]
                        .Rows.Select(r => r.ClubId)
                        .SequenceEqual(metro.Take(3), StringComparer.Ordinal)
                    && east.Leaders[1]
                        .Rows.Select(r => r.ClubId)
                        .SequenceEqual(atlantic.Take(3), StringComparer.Ordinal),
                "each division shows its top three"
            );
            Check(
                Same(east.Race[0].ClubId, atlantic[3])
                    && Same(east.Race[1].ClubId, atlantic[4])
                    && Same(east.Race[2].ClubId, metro[3]),
                "the East's race: the Atlantic's 4th and 5th hold the wild cards over the Metropolitan's 4th"
            );
            Check(
                Same(west.Race[0].ClubId, pacific[3]) && Same(west.Race[1].ClubId, central[3]),
                "the West's race: the Pacific's 4th, then the Central's 4th"
            );
            foreach (WildCardTable c in view)
            {
                for (int i = 0; i < NhlSeeding.WildCards; i++)
                {
                    string id = c.Race[i].ClubId,
                        wc = "WC" + (i + 1).ToString(CultureInfo.InvariantCulture);
                    Check(
                        field.Tags.TryGetValue(id, out string tag) && Same(tag, wc),
                        $"{c.Conference}: race place {wc} ({id}) is the field's {wc}"
                    );
                }

                Check(
                    c.Race.Skip(NhlSeeding.WildCards).All(r => !field.Tags.ContainsKey(r.ClubId)),
                    $"{c.Conference}: the clubs under the cut line miss the playoffs"
                );
                List<string> race = [.. c.Race.Select(r => r.ClubId)];
                Check(
                    race.SequenceEqual(race.OrderBy(order.IndexOf), StringComparer.Ordinal),
                    $"{c.Conference}: the race is in table order"
                );
                Check(
                    c.Leaders.Sum(d => d.Rows.Count) + c.Race.Count == 16,
                    $"{c.Conference}: all sixteen clubs shown once"
                );
            }
        }

        [Test]
        public void EveryNhlNameDrawsWithThePixelFont()
        {
            static bool Has(char c)
            {
                return FontGlyphs.Contains(c, StringComparison.Ordinal);
            }

            Check(
                string.Equals(
                    Letters.Plain("SLAFKOVSKÝ", Has),
                    "SLAFKOVSKY",
                    StringComparison.Ordinal
                )
                    && string.Equals(
                        Letters.Plain("JØRGENSEN", Has),
                        "JORGENSEN",
                        StringComparison.Ordinal
                    )
                    && string.Equals(Letters.Plain("HÁŠEK", Has), "HASEK", StringComparison.Ordinal)
                    && string.Equals(
                        Letters.Plain("MONTRÉAL", Has),
                        "MONTRÉAL",
                        StringComparison.Ordinal
                    ),
                "letters the font lacks draw plain, the ones it has stay"
            );
            Check(
                string.Equals(
                    Letters.Plain("SLAFKOVSKÝ", _ => true),
                    "SLAFKOVSKÝ",
                    StringComparison.Ordinal
                ),
                "a glyph added to the font is drawn as it is"
            );

            List<string> bad = [];
            foreach (ClubData club in Nhl.Value)
            {
                foreach (
                    string name in (string[])
                        [
                            club.Name,
                            club.ShortName,
                            .. club.Roster.Select(p => p.FirstName + " " + p.LastName),
                        ]
                )
                {
                    string shown = Letters.Plain(name.ToUpperInvariant(), Has);
                    if (!shown.All(Has))
                    {
                        bad.Add($"{name} -> {shown}");
                    }
                }
            }

            Check(bad.Count == 0, $"every NHL name draws in the font ({string.Join(", ", bad)})");
            Check(
                Nhl.Value.All(c =>
                    c.ShortName.Length == 3 && c.ShortName.All(char.IsAsciiLetterUpper)
                ),
                "every NHL club has a three-letter scorebug code"
            );
        }

        private static List<string> Ids(SeasonState season, string division)
        {
            return
            [
                .. season
                    .Clubs.Where(id => Same(season.Divisions[id], division))
                    .Order(StringComparer.Ordinal),
            ];
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        private static Maybe<string> RepoRoot()
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir is { }; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "src.slnx")))
                {
                    return dir.FullName;
                }
            }

            return Maybe.None<string>();
        }
    }
}
