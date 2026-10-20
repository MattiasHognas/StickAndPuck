using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Stats
{
    /// <summary>
    /// Young player development (W132): every club player's age loads from the club files; young players
    /// with ice time and form gain trait points over the season, veterans decline; the rule's switch,
    /// strengths and presets; a season plays its matches with the development (through the setup's
    /// nudges, so replays rebuild the same clubs), keeps it in its save (an older save reads with none)
    /// and puts a level-up in the dressing room news; ages leave the replay club fingerprint alone.
    /// </summary>
    public sealed class DevelopmentTests : CheckedTest
    {
        private const string Club = "home";

        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-devtest-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        [After(Test)]
        public void CleanUp()
        {
            app.Season.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static string F(double v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static MatchStatLine Line(int number, int age, float ice, bool played = true)
        {
            return new MatchStatLine
            {
                Club = Club,
                Side = Side.Home,
                Number = number,
                Position = PlayingPosition.C,
                Played = played,
                IceTime = ice,
                Age = age,
            };
        }

        /// <summary>One game: a teenager on the top line (9), a teenager in the press box of minutes (10),
        /// a 23-year-old regular (11), a player in his prime (12), two veterans (13, 14) and a player with
        /// no age in his file (15).</summary>
        private static MatchStatSheet Game()
        {
            MatchStatSheet sheet = new() { Home = Club, Away = "away" };
            sheet.Lines.Add(Line(9, 19, 1200));
            sheet.Lines.Add(Line(10, 19, 300));
            sheet.Lines.Add(Line(11, 23, 900));
            sheet.Lines.Add(Line(12, 27, 900));
            sheet.Lines.Add(Line(13, 33, 900));
            sheet.Lines.Add(Line(14, 37, 900, played: false));
            sheet.Lines.Add(Line(15, 0, 900));
            return sheet;
        }

        [Test]
        public void AgesLoadForEveryClubPlayer()
        {
            foreach (League league in new[] { League.Shl, League.Nhl })
            {
                IReadOnlyList<ClubData> clubs = Clubs.Load(league);
                List<string> missing =
                [
                    .. clubs.SelectMany(c =>
                        c.Everyone()
                            .Where(p => p.Age is < 16 or > 45)
                            .Select(p =>
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"{c.Id} #{p.Number} {p.LastName} ({p.Age})"
                                )
                            )
                    ),
                ];
                Check(
                    clubs.Count > 0 && missing.Count == 0,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"every {league} club player has an age ({clubs.Sum(c => c.Roster.Count)} players, missing: {string.Join(", ", missing)})"
                    )
                );
                Check(
                    clubs.SelectMany(c => c.Roster).Any(p => p.Age <= DevelopmentTable.YoungAge)
                        && clubs
                            .SelectMany(c => c.Roster)
                            .Any(p => p.Age >= DevelopmentTable.VeteranAge),
                    $"the {league} has young players and veterans"
                );
            }

            ClubData written = Clubs.Load(League.Shl)[0];
            string json = Clubs.ToJson(written);
            Check(
                json.Contains("\"age\":", StringComparison.Ordinal),
                "a club writes its ages back"
            );
            ClubData noAges = written with
            {
                Roster = [.. written.Roster.Select(p => p with { Age = 0 })],
                Spares = [],
            };
            Check(
                !Clubs.ToJson(noAges).Contains("\"age\":", StringComparison.Ordinal),
                "a club with no ages writes none, as before"
            );
            Check(
                string.Equals(
                    ReplayRecorder.ClubsHash(written, written),
                    ReplayRecorder.ClubsHash(noAges, noAges),
                    StringComparison.Ordinal
                ),
                "ages leave the replay club fingerprint as it was"
            );
            string path = Path.Combine(folder, "club.json");
            _ = Directory.CreateDirectory(folder);
            File.WriteAllText(path, Clubs.ToJson(noAges));
            Check(Clubs.Load(path).Roster.All(p => p.Age == 0), "a club file without ages loads");
        }

        [Test]
        public void YoungGainVeteransDecline()
        {
            DevelopmentTable table = new();
            FormTable form = new();
            form.Players.Add(
                new PlayerForm
                {
                    Club = Club,
                    Number = 11,
                    Value = 4,
                }
            );
            for (int i = 0; i < 52; i++)
            {
                table.Add(SeasonStats.GameKey(i), Game(), form);
            }

            double Of(int n)
            {
                return table.Of(Club, n);
            }

            Check(
                Of(9) is > 3 and <= DevelopmentTable.MaxGain,
                $"a teenager with top-line ice time gains about four points in a season ({F(Of(9))})"
            );
            Check(
                Of(10) > 0 && Of(10) < Of(9) / 2,
                $"a teenager with little ice time gains much less ({F(Of(10))} against {F(Of(9))})"
            );
            Check(
                Of(11) > 0 && Of(11) < Of(9),
                $"a 23-year-old in form still gains, less than a teenager ({F(Of(11))})"
            );
            Check(
                table.Players.All(p => p.Number != 12) && Of(15) == 0,
                "a player in his prime, or with no age, does not move"
            );
            Check(
                Of(13) < 0 && Of(14) < Of(13) && Of(14) >= -DevelopmentTable.MaxLoss,
                $"veterans decline, the older faster, even out of the line-up ({F(Of(13))}, {F(Of(14))})"
            );
            Check(
                table.Points(Club, 9, RuleSet.Shl) > 0
                    && table.Points(Club, 14, RuleSet.Shl) < 0
                    && table.Points(Club, 12, RuleSet.Shl) == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the whole points: {table.Points(Club, 9, RuleSet.Shl)} young, {table.Points(Club, 14, RuleSet.Shl)} old"
                )
            );

            // form: the same game with the teenager hot or cold
            MatchStatLine kid = Line(9, 19, 900);
            MatchStatSheet sheet = Game();
            Check(
                DevelopmentTable.Step(kid, sheet, 5) > DevelopmentTable.Step(kid, sheet, 0)
                    && DevelopmentTable.Step(kid, sheet, 0) > DevelopmentTable.Step(kid, sheet, -5)
                    && DevelopmentTable.Step(kid, sheet, -5) > 0,
                "form speeds a young player's development, a slump slows it"
            );
            MatchStatLine old = Line(13, 33, 900);
            Check(
                DevelopmentTable.Step(old, sheet, 5) > DevelopmentTable.Step(old, sheet, -5),
                "a veteran in form declines slower"
            );
            Check(
                DevelopmentTable.Step(Line(9, 19, 0, played: false), sheet, 0) == 0,
                "a young player who did not play does not develop"
            );

            // a long career caps out
            for (int i = 52; i < 300; i++)
            {
                table.Add(SeasonStats.GameKey(i), Game(), form);
            }

            Check(
                Math.Abs(Of(9) - DevelopmentTable.MaxGain) < 1e-9
                    && Math.Abs(Of(14) + DevelopmentTable.MaxLoss) < 1e-9,
                "development stops at +5 and -4"
            );
        }

        [Test]
        public void StrengthsAndPresets()
        {
            DevelopmentTable table = new();
            table.Players.Add(
                new PlayerDevelopment
                {
                    Club = Club,
                    Number = 9,
                    Value = 3.4,
                }
            );
            table.Players.Add(
                new PlayerDevelopment
                {
                    Club = Club,
                    Number = 13,
                    Value = -2.2,
                }
            );
            int P(int n, RuleSet r)
            {
                return table.Points(Club, n, r);
            }

            RuleSet mild = RuleSet.Shl with { DevelopmentStrength = DevelopmentStrength.Mild };
            RuleSet strong = RuleSet.Shl with { DevelopmentStrength = DevelopmentStrength.Strong };
            RuleSet off = RuleSet.Shl with { Development = false };
            Check(
                P(9, mild) == 1 && P(9, RuleSet.Shl) == 3 && P(9, strong) == 5,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"mild, normal and strong scale the gain ({P(9, mild)}, {P(9, RuleSet.Shl)}, {P(9, strong)})"
                )
            );
            Check(
                P(13, mild) == -1 && P(13, RuleSet.Shl) == -2 && P(13, strong) == -3,
                "and the decline"
            );
            Check(P(9, off) == 0 && P(13, off) == 0, "with development off nobody moves");
            Check(
                RuleSet.Shl
                    is { Development: true, DevelopmentStrength: DevelopmentStrength.Normal }
                    && RuleSet.Nhl
                        is { Development: true, DevelopmentStrength: DevelopmentStrength.Mild }
                    && RuleSet.Arcade is { Development: false },
                "SHL develops normally, the NHL's longer season mildly, Arcade not at all"
            );
            RuleChoice turnedOff = RuleChoice
                .Of(RulePreset.Shl)
                .With(
                    RuleRows.All.First(r =>
                        string.Equals(r.Key, nameof(RuleSet.Development), StringComparison.Ordinal)
                    ),
                    "Off"
                );
            Check(
                !turnedOff.Resolve().Development && turnedOff.Custom,
                "development can be turned off on the rules page"
            );
            Check(
                RuleRows.All.Any(r =>
                    string.Equals(
                        r.Key,
                        nameof(RuleSet.DevelopmentStrength),
                        StringComparison.Ordinal
                    ) && string.Equals(r.Group, Text.Keys.RuleGroupSeason, StringComparison.Ordinal)
                ),
                "its strength is a SEASON rule"
            );
        }

        [Test]
        public void SeasonDevelopsItsPlayers()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            string path = Path.Combine(folder, "season.json");
            _ = Directory.CreateDirectory(folder);
            SeasonState season = app.Season.StartNew(clubs, clubs[0].Id, Maybe.Some(77), path);
            for (int i = 0; i < 30; i++)
            {
                _ = app.Season.SimCurrentGame();
            }

            RuleSet rules = season.Rules.Or(RuleSet.Shl);
            List<PlayerDevelopment> grown =
            [
                .. season.Stats.Development.Players.Where(p =>
                    season.Stats.Development.Points(p.Club, p.Number, rules) > 0
                ),
            ];
            List<PlayerDevelopment> declined =
            [
                .. season.Stats.Development.Players.Where(p =>
                    season.Stats.Development.Points(p.Club, p.Number, rules) < 0
                ),
            ];
            Check(
                grown.Count > 0 && declined.Count > 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"thirty rounds in, young players have gained and veterans lost ({grown.Count} up, {declined.Count} down)"
                )
            );
            Check(
                grown.TrueForAll(d => Age(clubs, d) <= DevelopmentTable.YoungAge)
                    && declined.TrueForAll(d => Age(clubs, d) >= DevelopmentTable.VeteranAge),
                "only the young gain and only veterans lose"
            );

            Fixture game = season.NextGame.TryGet(out Fixture g)
                ? g
                : throw new InvalidOperationException("expected a next game");
            MatchSetup setup = season.SetupFor(game, clubs);
            ClubData file = clubs.First(c =>
                string.Equals(c.Id, game.Home, StringComparison.Ordinal)
            );
            Check(
                setup.Home.Roster.All(p =>
                    p.Traits.HockeyIq
                    == Math.Clamp(
                        file.Everyone().First(f => f.Number == p.Number).Traits.HockeyIq
                            + season.FormNudge(file.Id, p.Number)
                            + season.DevelopmentPoints(file.Id, p.Number),
                        0,
                        100
                    )
                ),
                "the season match plays every player with form and development"
            );
            Check(
                setup.Home.Roster.All(p =>
                    setup.Form.GetValueOrDefault(FormTable.Key(file.Id, p.Number))
                    == season.FormNudge(file.Id, p.Number)
                        + season.DevelopmentPoints(file.Id, p.Number)
                ),
                "the setup's nudges carry the development, so the replay rebuilds the same clubs"
            );

            ClubData user = clubs[0];
            Check(
                season
                    .InForm(user)
                    .Roster.All(p =>
                        p.Traits.TopSpeed
                        == Math.Clamp(
                            user.Everyone().First(f => f.Number == p.Number).Traits.TopSpeed
                                + season.FormNudge(user.Id, p.Number)
                                + season.DevelopmentPoints(user.Id, p.Number),
                            0,
                            100
                        )
                    ),
                "simmed games play with the development too"
            );

            SaveKeepsDevelopment(season, clubs, path);
        }

        private static int Age(IReadOnlyList<ClubData> clubs, PlayerDevelopment d)
        {
            return clubs
                .First(c => string.Equals(c.Id, d.Club, StringComparison.Ordinal))
                .Everyone()
                .First(p => p.Number == d.Number)
                .Age;
        }

        private void SaveKeepsDevelopment(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            string path
        )
        {
            int count = season.Stats.Development.Players.Count;
            double sum = season.Stats.Development.Players.Sum(p => p.Value);
            SeasonSave.Write(season, path);
            app.Season.End();
            SeasonState again = app.Season.Continue(clubs, path).TryGet(out SeasonState s)
                ? s
                : throw new InvalidOperationException("the save did not read");
            Check(
                again.Stats.Development.Players.Count == count
                    && Math.Abs(again.Stats.Development.Players.Sum(p => p.Value) - sum) < 1e-9,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the development reads back from the save ({count} players)"
                )
            );
            string saved = File.ReadAllText(path);
            Check(
                saved.Contains("\"development\":", StringComparison.Ordinal),
                "the save carries the development"
            );
            File.WriteAllText(
                path,
                saved.Replace("\"development\":", "\"oldUnused\":", StringComparison.Ordinal)
            );
            app.Season.End();
            Check(
                SeasonSave
                    .Read(path)
                    .Is(x => x.Stats.Development.Players.Count == 0 && x.Games.Count > 0),
                "a save from before development reads, with nobody developed"
            );
        }

        [Test]
        public void LevelUpMakesTheNews()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            _ = Directory.CreateDirectory(folder);
            SeasonState season = app.Season.StartNew(
                clubs,
                clubs[1].Id,
                Maybe.Some(11),
                Path.Combine(folder, "season.json")
            );
            _ = app.Season.SimCurrentGame();
            LastGame last = LastGame.Of(season).TryGet(out LastGame l)
                ? l
                : throw new InvalidOperationException("expected a last game");
            ClubData club = clubs[1];
            PlayerData kid = club.Roster.OrderBy(p => p.Age).First();
            PlayerData veteran = club.Roster.OrderByDescending(p => p.Age).First();
            season.Stats.Development.Players.Clear();
            season.Stats.Development.Players.Add(
                new PlayerDevelopment
                {
                    Club = club.Id,
                    Number = kid.Number,
                    Before = 2.9,
                    Value = 3.05,
                    Game = last.Key,
                }
            );
            season.Stats.Development.Players.Add(
                new PlayerDevelopment
                {
                    Club = club.Id,
                    Number = veteran.Number,
                    Before = -0.98,
                    Value = -1.01,
                    Game = last.Key,
                }
            );
            DressingRoomReport report = DressingRoomSources.Build(
                DressingRoomContext.Of(season, clubs, RuleSet.Shl)
            );
            string up = Text.Format(Text.Keys.NewsDevelopUp, kid.LastName, kid.Age, "+3");
            string down = Text.Format(
                Text.Keys.NewsDevelopDown,
                veteran.LastName,
                veteran.Age,
                "-1"
            );
            Check(
                report.News.Any(n =>
                    n.Highlight && string.Equals(n.Text, up, StringComparison.Ordinal)
                ),
                $"the young player's level-up is in the news: {up}"
            );
            Check(
                report.News.Any(n => string.Equals(n.Text, down, StringComparison.Ordinal)),
                $"so is the veteran slowing down: {down}"
            );
            NoNews(season, clubs, kid, up, down);
        }

        private void NoNews(
            SeasonState season,
            IReadOnlyList<ClubData> clubs,
            PlayerData kid,
            string up,
            string down
        )
        {
            DressingRoomReport mild = DressingRoomSources.Build(
                DressingRoomContext.Of(
                    season,
                    clubs,
                    RuleSet.Shl with
                    {
                        DevelopmentStrength = DevelopmentStrength.Mild,
                    }
                )
            );
            Check(
                !mild.News.Any(n => n.Text.Contains(kid.LastName + " (", StringComparison.Ordinal)),
                "under mild development the same game reaches no new point, so no news"
            );
            DressingRoomReport off = DressingRoomSources.Build(
                DressingRoomContext.Of(season, clubs, RuleSet.Shl with { Development = false })
            );
            Check(
                !off.News.Any(n =>
                    string.Equals(n.Text, up, StringComparison.Ordinal)
                    || string.Equals(n.Text, down, StringComparison.Ordinal)
                ),
                "with development off there is no development news"
            );

            season.Stats.Development.Players.First().Game = "g999";
            Check(
                !DressingRoomSources
                    .Build(DressingRoomContext.Of(season, clubs, RuleSet.Shl))
                    .News.Any(n => string.Equals(n.Text, up, StringComparison.Ordinal)),
                "a step from an earlier game is old news"
            );
        }
    }
}
