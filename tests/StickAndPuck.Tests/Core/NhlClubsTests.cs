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
