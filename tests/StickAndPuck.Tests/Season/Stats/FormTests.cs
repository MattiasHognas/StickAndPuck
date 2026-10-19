using System.Globalization;
using System.Text.Json;
using StickAndPuck.App.Core;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Stats
{
    /// <summary>
    /// Carry-over form (W123): the plus/minus and ice time the tracker reads from a match, form rising
    /// and falling with a skater's and a goalie's games and drifting back when they sit out, the trait
    /// nudge it gives under each strength and with the rule off, the presets, and a season that plays
    /// its matches with the nudge, keeps the form in its save (an older save reads with none) and
    /// stores the nudges in the replay setup so the replay builds the same clubs.
    /// </summary>
    public sealed class FormTests : CheckedTest
    {
        private const string Club = "home";

        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-formtest-{Environment.ProcessId}-{Guid.NewGuid():N}"
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

        /// <summary>A two-line game: our skater (number 9), a teammate at the same position, and our goalie (number 1).</summary>
        private static MatchStatSheet Game(
            int goals,
            int assists,
            int plusMinus,
            float ice,
            int saves,
            int against,
            bool played = true
        )
        {
            MatchStatSheet sheet = new()
            {
                Home = Club,
                Away = "away",
                HomeGoals = goals,
                AwayGoals = against,
            };
            sheet.Lines.Add(
                new MatchStatLine
                {
                    Club = Club,
                    Side = Side.Home,
                    Number = 9,
                    Position = PlayingPosition.C,
                    Played = played,
                    Goals = goals,
                    Assists = assists,
                    PlusMinus = plusMinus,
                    IceTime = ice,
                }
            );
            sheet.Lines.Add(
                new MatchStatLine
                {
                    Club = Club,
                    Side = Side.Home,
                    Number = 10,
                    Position = PlayingPosition.C,
                    Played = true,
                    IceTime = 900,
                }
            );
            sheet.Lines.Add(
                new MatchStatLine
                {
                    Club = Club,
                    Side = Side.Home,
                    Number = 1,
                    Position = PlayingPosition.G,
                    Played = played,
                    Saves = saves,
                }
            );
            return sheet;
        }

        [Test]
        public void TrackerReadsPlusMinusAndIceTime()
        {
            MatchStatTracker tracker = new();
            World w = new WorldBuilder().NotBegun().Build();
            w.Match.Phase = MatchPhase.Play;
            tracker.Observe(w);
            w.Time += 30;
            tracker.Observe(w);
            Player home = w.OnIce(Side.Home).First(p => !p.IsGoalie);
            Player away = w.OnIce(Side.Away).First(p => !p.IsGoalie);
            MatchStatLine Line(Player p)
            {
                return tracker.Sheet.Lines.First(l =>
                    l.Number == p.Data.Number && l.Side == p.Side
                );
            }

            Check(
                Math.Abs(Line(home).IceTime - 30) < 0.01f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"thirty seconds of play are thirty seconds of ice time ({Line(home).IceTime})"
                )
            );
            w.Match.Phase = MatchPhase.Stopped;
            w.Time += 30;
            tracker.Observe(w);
            Check(Math.Abs(Line(home).IceTime - 30) < 0.01f, "a stoppage adds no ice time");

            // an even-strength goal: +1 to every home skater on the ice, -1 to every away skater
            w.Events.Clear();
            w.Events.Publish(new GoalScored(w.Time, Side.Home, home.Id, default, 0));
            tracker.Observe(w);
            Check(
                w.OnIce(Side.Home).Where(p => !p.IsGoalie).All(p => Line(p).PlusMinus == 1)
                    && w.OnIce(Side.Away).Where(p => !p.IsGoalie).All(p => Line(p).PlusMinus == -1)
                    && w.Players.Where(p => p.IsGoalie).All(p => Line(p).PlusMinus == 0),
                "an even-strength goal is +1 for the scorers' skaters and -1 for the others', goalies aside"
            );

            // a power-play goal (an away skater in the box) counts for nobody's plus/minus
            w.Events.Clear();
            away.OnIce = false;
            w.Events.Publish(new PuckDropped(w.Time, default));
            tracker.Observe(w);
            w.Events.Clear();
            w.Events.Publish(new GoalScored(w.Time, Side.Home, home.Id, default, 0));
            tracker.Observe(w);
            Check(
                Line(home).PlusMinus == 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a power-play goal leaves the plus/minus alone ({Line(home).PlusMinus})"
                )
            );

            // short-handed the other way: the away club scores a man down, which counts
            w.Events.Clear();
            w.Events.Publish(new GoalScored(w.Time, Side.Away, -1, default, 0));
            tracker.Observe(w);
            Check(
                Line(home).PlusMinus == 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a short-handed goal against counts ({Line(home).PlusMinus})"
                )
            );
        }

        [Test]
        public void FormRisesAndFalls()
        {
            FormTable hot = new();
            List<double> climb = [];
            for (int i = 0; i < 5; i++)
            {
                hot.Add(Game(goals: 1, assists: 0, plusMinus: 1, ice: 1000, saves: 30, against: 0));
                climb.Add(hot.Of(Club, 9));
            }
            Check(
                climb.Zip(climb.Skip(1)).All(x => x.Second > x.First) && climb[^1] > 3,
                $"a skater scoring every night climbs towards +5 ({string.Join(", ", climb.Select(F))})"
            );
            Check(climb[^1] <= FormTable.Max, "form stops at +5");
            Check(
                hot.Of(Club, 1) > 2,
                $"a goalie keeping shutouts heats up ({F(hot.Of(Club, 1))})"
            );
            Check(
                hot.Of(Club, 10) is < 0 and > -3,
                $"a teammate without a point all week cools a little ({F(hot.Of(Club, 10))})"
            );

            FormTable cold = new();
            for (int i = 0; i < 5; i++)
            {
                cold.Add(
                    Game(goals: 0, assists: 0, plusMinus: -2, ice: 600, saves: 20, against: 5)
                );
            }
            Check(
                cold.Of(Club, 9) < -2,
                $"a pointless minus player with little ice time goes cold ({F(cold.Of(Club, 9))})"
            );
            Check(
                cold.Of(Club, 1) < -2,
                $"a goalie letting in five a night goes cold ({F(cold.Of(Club, 1))})"
            );
            Check(cold.Of(Club, 9) >= -FormTable.Max, "form stops at -5");

            double before = hot.Of(Club, 9);
            hot.Add(Game(0, 0, 0, 0, 0, 0, played: false));
            Check(
                hot.Of(Club, 9) < before && hot.Of(Club, 9) > 0,
                $"a night out drifts the form back towards level ({F(before)} to {F(hot.Of(Club, 9))})"
            );
            Check(new FormTable().Of(Club, 9) == 0, "a player with no games is level");

            // the ice time counts: the same game with more minutes than the teammate rates higher
            double more = FormTable.Rating(
                Game(0, 0, 0, 1200, 0, 0).Lines.First(),
                Game(0, 0, 0, 1200, 0, 0)
            );
            double less = FormTable.Rating(
                Game(0, 0, 0, 600, 0, 0).Lines.First(),
                Game(0, 0, 0, 600, 0, 0)
            );
            Check(more > less, $"more ice time rates higher ({F(more)} vs {F(less)})");
        }

        [Test]
        public void NudgesTraits()
        {
            FormTable table = new()
            {
                Players =
                [
                    new PlayerForm
                    {
                        Club = Club,
                        Number = 9,
                        Value = 4,
                    },
                    new PlayerForm
                    {
                        Club = Club,
                        Number = 1,
                        Value = -3,
                    },
                ],
            };
            Check(
                table.Nudge(Club, 9, RuleSet.Shl) == 4
                    && table.Nudge(Club, 9, RuleSet.Shl with { FormStrength = FormStrength.Mild })
                        == 2
                    && table.Nudge(Club, 9, RuleSet.Shl with { FormStrength = FormStrength.Strong })
                        == 5
                    && table.Nudge(Club, 1, RuleSet.Shl) == -3
                    && table.Nudge(Club, 9, RuleSet.Shl with { Form = false }) == 0,
                "the nudge is the form, halved when mild, half again when strong (capped at 5), none with form off"
            );

            ClubData club = new()
            {
                Id = Club,
                Roster =
                [
                    new PlayerData
                    {
                        Number = 9,
                        Position = PlayingPosition.C,
                        Traits = new Traits
                        {
                            TopSpeed = 98,
                            ShotAccuracy = 60,
                            HockeyIq = 40,
                        },
                    },
                    new PlayerData
                    {
                        Number = 1,
                        Position = PlayingPosition.G,
                        Traits = new Traits { GoalieReflexes = 70 },
                    },
                    new PlayerData { Number = 20, Position = PlayingPosition.D },
                ],
            };
            ClubData inForm = table.Apply(club, RuleSet.Shl);
            Traits nine = inForm.Roster[0].Traits;
            Check(
                nine is { TopSpeed: 100, ShotAccuracy: 64, HockeyIq: 44, Checking: 54 },
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a hot skater's traits go up by the nudge, kept under 100 ({nine.TopSpeed}, {nine.ShotAccuracy}, {nine.HockeyIq})"
                )
            );
            Check(
                inForm.Roster[1].Traits.GoalieReflexes == 67,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a cold goalie's reflexes go down ({inForm.Roster[1].Traits.GoalieReflexes})"
                )
            );
            Check(
                inForm.Roster[2].Traits == club.Roster[2].Traits,
                "a player with no form keeps their traits"
            );
            Check(
                ReferenceEquals(table.Apply(club, RuleSet.Shl with { Form = false }), club),
                "with form off the club plays as in its file"
            );
        }

        [Test]
        public void Presets()
        {
            Check(
                RuleSet.Shl is { Form: true, FormStrength: FormStrength.Normal }
                    && RuleSet.Nhl is { Form: true, FormStrength: FormStrength.Normal }
                    && RuleSet.Arcade is { Form: true, FormStrength: FormStrength.Strong },
                "SHL and NHL play normal form, Arcade strong"
            );
            RuleChoice off = RuleChoice
                .Of(RulePreset.Shl)
                .With(
                    RuleRows.All.First(r =>
                        string.Equals(r.Key, nameof(RuleSet.Form), StringComparison.Ordinal)
                    ),
                    "Off"
                );
            Check(!off.Resolve().Form && off.Custom, "form can be turned off on the rules page");
            Check(
                string.Equals(
                    RuleRow.Show("Mild") + RuleRow.Show("Strong"),
                    Text.Get(Text.Keys.RuleValueMild) + Text.Get(Text.Keys.RuleValueStrong),
                    StringComparison.Ordinal
                ),
                "the strengths show their strings"
            );
        }

        [Test]
        public void SeasonPlaysWithForm()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            string path = Path.Combine(folder, "season.json");
            _ = Directory.CreateDirectory(folder);
            SeasonState season = app.Season.StartNew(clubs, clubs[0].Id, Maybe.Some(1234), path);
            for (int i = 0; i < 6; i++)
            {
                _ = app.Season.SimCurrentGame();
            }

            Check(
                season.Stats.Form.Players.Count > 0
                    && season.Stats.Form.Players.Any(p => p.Value > 0.5)
                    && season.Stats.Form.Players.Any(p => p.Value < -0.5),
                $"the simmed games give players form both ways ({season.Stats.Form.Players.Count} players)"
            );

            Check(
                app.Season.NextSetup(InputDevice.None) is NextMatch.Play,
                "the season has a next game"
            );
            Fixture game = season.NextGame.TryGet(out Fixture g)
                ? g
                : throw new InvalidOperationException("expected a next game");
            MatchSetup setup = season.SetupFor(game, clubs);
            ClubData file = clubs.First(c =>
                string.Equals(c.Id, game.Home, StringComparison.Ordinal)
            );
            List<(PlayerData Played, PlayerData File)> pairs =
            [
                .. setup.Home.Roster.Zip(file.Roster),
            ];
            Check(
                pairs.TrueForAll(p =>
                    p.Played.Traits.HockeyIq
                    == Math.Clamp(
                        p.File.Traits.HockeyIq + season.FormNudge(file.Id, p.File.Number),
                        0,
                        100
                    )
                ),
                "the season match plays every player with their form's nudge"
            );
            Check(
                setup.Form.Count > 0
                    && setup.Form.All(kv => kv.Value != 0 && Math.Abs(kv.Value) <= FormTable.Max),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the setup lists the nudges it plays ({setup.Form.Count})"
                )
            );
            ReplayKeepsForm(setup, clubs);
            SaveKeepsForm(season, clubs, path);
        }

        private void ReplayKeepsForm(MatchSetup setup, IReadOnlyList<ClubData> clubs)
        {
            // the replay keeps the nudges and builds the same clubs from the data files
            string json = JsonSerializer.Serialize(
                ReplaySetup.Of(setup),
                ReplayJsonContext.Default.ReplaySetup
            );
            ReplaySetup read =
                JsonSerializer.Deserialize(json, ReplayJsonContext.Default.ReplaySetup)
                ?? throw new InvalidOperationException("replay setup did not read");
            MatchSetup rebuilt = read.ToSetup(clubs);
            Check(
                string.Equals(
                    Clubs.ToJson(rebuilt.Home) + Clubs.ToJson(rebuilt.Away),
                    Clubs.ToJson(setup.Home) + Clubs.ToJson(setup.Away),
                    StringComparison.Ordinal
                ),
                "the replay rebuilds the clubs with the same form"
            );
            ReplaySetup old =
                JsonSerializer.Deserialize(
                    json.Replace("\"form\":", "\"unused\":", StringComparison.Ordinal),
                    ReplayJsonContext.Default.ReplaySetup
                ) ?? throw new InvalidOperationException("old replay setup did not read");
            Check(old.Form.Count == 0, "an older replay without form reads with none");
        }

        private void SaveKeepsForm(SeasonState season, IReadOnlyList<ClubData> clubs, string path)
        {
            // the form survives the save, and an older save without it reads with everyone level
            int count = season.Stats.Form.Players.Count;
            SeasonSave.Write(season, path);
            app.Season.End();
            SeasonState again = app.Season.Continue(clubs, path).TryGet(out SeasonState s)
                ? s
                : throw new InvalidOperationException("the save did not read");
            Check(again.Stats.Form.Players.Count == count, "the form reads back from the save");
            string saved = File.ReadAllText(path);
            int at = saved.IndexOf("\"form\":", StringComparison.Ordinal);
            Check(at > 0, "the save carries the form");
            File.WriteAllText(
                path,
                saved.Replace("\"form\":", "\"oldUnused\":", StringComparison.Ordinal)
            );
            app.Season.End();
            Maybe<SeasonState> old2 = SeasonSave.Read(path);
            Check(
                old2.Is(x => x.Stats.Form.Players.Count == 0 && x.Games.Count > 0),
                "a save from before form reads, with everyone level"
            );
        }
    }
}
