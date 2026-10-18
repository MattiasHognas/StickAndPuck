using System.Text.Json;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Core
{
    /// <summary>The 32 NHL club files (W115) load through the club loader with full lines, record their
    /// conference and division, and stay out of the SHL club list until league choice (W117).</summary>
    public sealed class NhlClubsTests : CheckedTest
    {
        /// <summary>The line order every SHL club file uses: two goalies, then C, LW, RW, D, D per line
        /// (line 4 has no D).</summary>
        private static readonly PlayingPosition[] LineOrder =
        [
            PlayingPosition.G,
            PlayingPosition.G,
            .. Enumerable
                .Repeat<PlayingPosition[]>(
                    [
                        PlayingPosition.C,
                        PlayingPosition.LW,
                        PlayingPosition.RW,
                        PlayingPosition.D,
                        PlayingPosition.D,
                    ],
                    3
                )
                .SelectMany(line => line),
            PlayingPosition.C,
            PlayingPosition.LW,
            PlayingPosition.RW,
        ];

        [Test]
        public void AllClubsLoadWithFullLines()
        {
            IReadOnlyList<ClubData> nhl = Clubs.LoadNhl();
            Check(nhl.Count == 32, $"all 32 NHL clubs load ({nhl.Count})");
            Check(
                nhl.DistinctBy(c => c.Id, StringComparer.Ordinal).Take(nhl.Count + 1).Count()
                    == nhl.Count,
                "NHL club ids are unique"
            );
            Check(
                nhl.All(c => c.ShortName.Length == 3 && c.Name.Length > 0),
                "every NHL club has a name and a three-letter code"
            );
            foreach (ClubData club in nhl)
            {
                Check(
                    club.Roster.Select(p => p.Position).SequenceEqual(LineOrder),
                    $"{club.Id} has two goalies, four forward lines and three defence pairs in line order"
                );
                Check(
                    club.Roster.DistinctBy(p => p.Number).Take(club.Roster.Count + 1).Count()
                        == club.Roster.Count
                        && club.Roster.All(p => p.Number > 0 && p.LastName.Length > 0),
                    $"{club.Id} has named players with unique numbers"
                );
                Check(
                    club.Roster.All(p => Values(p.Traits).All(v => v is >= 0 and <= 100)),
                    $"{club.Id} traits are 0 to 100"
                );
                Check(
                    club.Home.Primary.StartsWith('#', StringComparison.Ordinal)
                        && club.Away.Primary.StartsWith('#', StringComparison.Ordinal),
                    $"{club.Id} has home and away colours"
                );
            }
        }

        [Test]
        public void ConferencesAndDivisionsAreRecorded()
        {
            List<(string Conference, string Division)> places = [];
            foreach (string file in ResourceLoader.List(Assets.NhlClubFolder, ".json"))
            {
                using JsonDocument doc = JsonDocument.Parse(ResourceLoader.ReadText(file));
                JsonElement root = doc.RootElement;
                string league = root.GetProperty("league").GetString() ?? "";
                Check(string.Equals(league, "NHL", StringComparison.Ordinal), $"{file} is NHL");
                places.Add(
                    (
                        root.GetProperty("conference").GetString() ?? "",
                        root.GetProperty("division").GetString() ?? ""
                    )
                );
            }
            int[] conferences =
            [
                .. places.GroupBy(p => p.Conference, StringComparer.Ordinal).Select(g => g.Count()),
            ];
            int[] divisions =
            [
                .. places
                    .GroupBy(p => p.Conference + "/" + p.Division, StringComparer.Ordinal)
                    .Select(g => g.Count()),
            ];
            Check(
                conferences.Length == 2 && conferences.All(n => n == 16),
                "two conferences of 16 clubs"
            );
            Check(divisions.Length == 4 && divisions.All(n => n == 8), "four divisions of 8 clubs");
        }

        [Test]
        public void ShlListStaysShlOnly()
        {
            IReadOnlyList<ClubData> shl = Clubs.LoadAll();
            HashSet<string> nhl = new(Clubs.LoadNhl().Select(c => c.Id), StringComparer.Ordinal);
            Check(shl.Count == 14, $"the SHL list still has 14 clubs ({shl.Count})");
            Check(shl.All(c => !nhl.Contains(c.Id)), "no NHL club is in the SHL list");
        }

        [Test]
        public void LeagueConferenceAndDivisionLoad()
        {
            IReadOnlyList<ClubData> nhl = Clubs.Load(League.Nhl);
            Check(nhl.Count == 32, $"Load(Nhl) reads the 32 NHL clubs ({nhl.Count})");
            Check(nhl.All(c => c.League == League.Nhl), "every NHL club loads with League = Nhl");
            HashSet<string> conferences = new(["Eastern", "Western"], StringComparer.Ordinal);
            HashSet<string> divisions = new(
                ["Atlantic", "Metropolitan", "Central", "Pacific"],
                StringComparer.Ordinal
            );
            Check(
                nhl.All(c =>
                    c.Conference.Is(conferences.Contains) && c.Division.Is(divisions.Contains)
                ),
                "every NHL club loads its conference and division"
            );
            ClubData avalanche = nhl.Single(c =>
                string.Equals(c.Id, "avalanche", StringComparison.Ordinal)
            );
            Check(
                string.Equals(avalanche.Conference.Or(""), "Western", StringComparison.Ordinal)
                    && string.Equals(
                        avalanche.Division.Or(""),
                        "Central",
                        StringComparison.Ordinal
                    ),
                "Colorado is in the Western conference, Central division"
            );
            string json = Clubs.ToJson(avalanche);
            Check(
                json.Contains("\"league\": \"NHL\"", StringComparison.Ordinal)
                    && json.Contains("\"conference\": \"Western\"", StringComparison.Ordinal),
                "NHL clubs write their league and conference back"
            );

            IReadOnlyList<ClubData> shl = Clubs.Load(League.Shl);
            Check(shl.Count == 14, $"Load(Shl) reads the 14 SHL clubs ({shl.Count})");
            Check(
                shl.All(c =>
                    c.League == League.Shl
                    && c.Conference == Maybe.None<string>()
                    && c.Division == Maybe.None<string>()
                ),
                "SHL clubs default to League = Shl with no conference or division"
            );
            foreach (ClubData club in shl)
            {
                string written = Clubs.ToJson(club);
                Check(
                    !written.Contains("\"league\"", StringComparison.Ordinal)
                        && !written.Contains("\"conference\"", StringComparison.Ordinal)
                        && !written.Contains("\"division\"", StringComparison.Ordinal),
                    $"{club.Id} JSON leaves the default league fields out"
                );
                Check(
                    string.Equals(
                        written,
                        Clubs.ToJson(
                            JsonSerializer.Deserialize(written, ClubJsonContext.Default.ClubData)
                        ),
                        StringComparison.Ordinal
                    ),
                    $"{club.Id} JSON round-trips byte-identical"
                );
            }
        }

        private static int[] Values(Traits t)
        {
            return
            [
                t.Acceleration,
                t.TopSpeed,
                t.ShotAccuracy,
                t.ShotPower,
                t.Checking,
                t.PuckControl,
                t.HockeyIq,
                t.GoalieReflexes,
                t.GoalieGlove,
                t.GoalieBlocker,
                t.GoalieReboundControl,
                t.GoaliePuckHandling,
            ];
        }
    }
}
