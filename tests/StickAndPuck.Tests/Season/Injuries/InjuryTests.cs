using System.Globalization;
using System.Numerics;
using System.Text.Json;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Injuries;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Injuries
{
    /// <summary>
    /// Injuries (W128): the presets and rates, the chance of each event type (a hard check, a blocked
    /// slap shot, a punch, a game on paper) as the injury system draws it, the 1 to 6 games, the lines
    /// of the next game built without the injured player (the players behind moving up), the recovery
    /// as his club plays its games, the save (an older save reads with everyone fit), the replay setup
    /// and the dressing room's injury list.
    /// </summary>
    public sealed class InjuryTests : CheckedTest
    {
        private const int Trials = 4000;

        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-injurytest-{Environment.ProcessId}-{Guid.NewGuid():N}"
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
            return v.ToString("0.000", CultureInfo.InvariantCulture);
        }

        [Test]
        public void PresetsAndRates()
        {
            Check(
                RuleSet.Shl.Injuries
                    && RuleSet.Nhl.Injuries
                    && !RuleSet.Arcade.Injuries
                    && RuleSet.Shl.InjuryRate == InjuryRate.Normal
                    && RuleSet.Nhl.InjuryRate == InjuryRate.Normal,
                "SHL and NHL injure players at the normal rate, Arcade never"
            );
            Check(
                Same(RuleSet.Arcade.InjuryScale, 0)
                    && Same((RuleSet.Shl with { InjuryRate = InjuryRate.Rare }).InjuryScale, 0.5)
                    && Same(RuleSet.Shl.InjuryScale, 1)
                    && Same((RuleSet.Shl with { InjuryRate = InjuryRate.Frequent }).InjuryScale, 2)
                    && Same(
                        (
                            RuleSet.Shl with
                            {
                                Injuries = false,
                                InjuryRate = InjuryRate.Frequent,
                            }
                        ).InjuryScale,
                        0
                    ),
                "the rate scales every chance by half, one or two, and off is none"
            );
            Check(
                RuleRows.All.Any(r =>
                    string.Equals(r.Key, nameof(RuleSet.Injuries), StringComparison.Ordinal)
                    && r.Plays
                )
                    && RuleRows.All.Any(r =>
                        string.Equals(r.Key, nameof(RuleSet.InjuryRate), StringComparison.Ordinal)
                        && r.Plays
                    ),
                "both rules are on the rules page"
            );
        }

        private static bool Same(double a, double b)
        {
            return Math.Abs(a - b) < 1e-6;
        }

        [Test]
        public void ChanceByEventType()
        {
            Check(
                Same(InjuryModel.Check(CheckingModel.HardHit - 0.01f, 1), 0)
                    && Same(InjuryModel.Check(CheckingModel.HardHit, 1), InjuryModel.CheckChance)
                    && Same(
                        InjuryModel.Check(CheckingModel.HardHit * 5, 1),
                        InjuryModel.CheckChance * 2
                    ),
                "only a hard check can injure, a harder one up to twice as likely"
            );
            Check(
                Same(InjuryModel.Block(slap: false, 40, 1), 0)
                    && Same(
                        InjuryModel.Block(slap: true, InjuryModel.BlockSpeed, 1),
                        InjuryModel.BlockChance
                    ),
                "a blocked wrist shot never injures, a blocked slap shot can"
            );
        }

        [Test]
        public void SystemDrawsEachEventType()
        {
            // the system's draws, each trial a fresh match stream
            double check = Rate(
                InjuryCause.Check,
                (w, victim, checker) =>
                    new CheckLanded(w.Time, checker.Side, checker.Id, victim.Position, 2f)
            );
            double soft = Rate(
                InjuryCause.Check,
                (w, victim, checker) =>
                    new CheckLanded(w.Time, checker.Side, checker.Id, victim.Position, 1f)
            );
            double slap = Rate(
                InjuryCause.BlockedShot,
                (w, victim, _) =>
                    new ShotBlocked(
                        w.Time,
                        victim.Side,
                        victim.Id,
                        victim.Position,
                        30f,
                        Slap: true
                    )
            );
            double wrist = Rate(
                InjuryCause.BlockedShot,
                (w, victim, _) =>
                    new ShotBlocked(
                        w.Time,
                        victim.Side,
                        victim.Id,
                        victim.Position,
                        30f,
                        Slap: false
                    )
            );
            double punch = Rate(
                InjuryCause.Fight,
                (w, victim, checker) =>
                {
                    checker.Begin(PlayerAction.Fight);
                    return new CheckLanded(w.Time, checker.Side, checker.Id, victim.Position, 1f);
                }
            );
            Near(check, InjuryModel.Check(2f, 1), "a hard check");
            Near(slap, InjuryModel.Block(slap: true, 30f, 1), "a blocked slap shot");
            Near(punch, InjuryModel.Punch(1f, 1), "a landed punch");
            Check(
                Same(soft, 0) && Same(wrist, 0),
                "an ordinary check and a blocked wrist shot never injure"
            );

            double frequent = Rate(
                InjuryCause.Check,
                (w, victim, checker) =>
                    new CheckLanded(w.Time, checker.Side, checker.Id, victim.Position, 2f),
                RuleSet.Shl with
                {
                    InjuryRate = InjuryRate.Frequent,
                }
            );
            double off = Rate(
                InjuryCause.Check,
                (w, victim, checker) =>
                    new CheckLanded(w.Time, checker.Side, checker.Id, victim.Position, 2f),
                RuleSet.Shl with
                {
                    Injuries = false,
                }
            );
            Near(frequent, InjuryModel.Check(2f, 2), "a hard check at the frequent rate");
            Check(Same(off, 0), "with injuries off nobody is hurt");
        }

        [Test]
        public void GoaliesAreNeverHurtAndOnceAMatch()
        {
            World w = new WorldBuilder().Build();
            Player goalie = w.OnIce(Side.Away).First(p => p.IsGoalie);
            Player victim = w.OnIce(Side.Away).First(p => !p.IsGoalie);
            int hurt = 0;
            InjurySystem system = new(Seeds.Dice(1));
            for (int i = 0; i < 2000; i++)
            {
                w.Events.Clear();
                w.Events.Publish(
                    new ShotBlocked(w.Time, Side.Away, goalie.Id, goalie.Position, 40f, Slap: true)
                );
                w.Events.Publish(
                    new ShotBlocked(w.Time, Side.Away, victim.Id, victim.Position, 40f, Slap: true)
                );
                system.Update(w, Game.Step);
                hurt += w.Events.Of<PlayerInjured>().Count(e => e.PlayerId == goalie.Id) * 1000;
                hurt += w.Events.Of<PlayerInjured>().Count(e => e.PlayerId == victim.Id);
            }

            Check(
                hurt == 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the goalie is never hurt and a skater only once a match ({hurt})"
                )
            );
        }

        /// <summary>The share of fresh matches in which one event injures the player it hits.</summary>
        private static double Rate(InjuryCause cause, Func<World, Player, Player, MatchEvent> make)
        {
            return Rate(cause, make, RuleSet.Shl);
        }

        /// <summary>As above, under some rules.</summary>
        private static double Rate(
            InjuryCause cause,
            Func<World, Player, Player, MatchEvent> make,
            RuleSet rules
        )
        {
            World w = new WorldBuilder().Rules(rules).Build();
            Player victim = w.OnIce(Side.Away).First(p => !p.IsGoalie);
            Player checker = w.OnIce(Side.Home).First(p => !p.IsGoalie);
            int hurt = 0;
            for (int i = 0; i < Trials; i++)
            {
                w.Events.Clear();
                checker.Begin(PlayerAction.None);
                w.Events.Publish(make(w, victim, checker));
                new InjurySystem(Seeds.Dice(Seeds.For(i + 1, "injuries", 0))).Update(w, Game.Step);
                foreach (PlayerInjured e in w.Events.Of<PlayerInjured>())
                {
                    if (e.PlayerId == victim.Id && e.Cause == cause && e.Side == victim.Side)
                    {
                        hurt++;
                    }
                }
            }

            return (double)hurt / Trials;
        }

        private void Near(double seen, double chance, string what)
        {
            Check(
                seen > chance * 0.6 && seen < chance * 1.4,
                $"{what} injures about {F(chance)} of the time ({F(seen)})"
            );
        }

        [Test]
        public void OneToSixGames()
        {
            Random dice = Seeds.Dice(7);
            int[] counts = new int[8];
            for (int i = 0; i < 20000; i++)
            {
                counts[InjuryModel.Games(dice)]++;
            }

            Check(
                counts[0] == 0 && counts[7] == 0 && counts.Skip(1).Take(6).All(c => c > 0),
                $"every injury is 1 to 6 games, and every length happens ({string.Join(", ", counts)})"
            );
            Check(
                counts[1] > counts[3] && counts[3] > counts[6],
                "short injuries are the most common"
            );
        }

        [Test]
        public void PaperGamesInjureRarely()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            FixtureResult result = new(3, 2, ResultKind.Regulation);
            int injuries = 0,
                games = 0;
            for (int seed = 1; seed <= 2000; seed++)
            {
                MatchStatSheet sheet = SyntheticStats.Sheet(clubs[0], clubs[1], result, seed);
                InjuryModel.DrawPaper(sheet, 1f, seed);
                injuries += sheet.Absences.Count;
                games++;
                foreach (Absence a in sheet.Absences)
                {
                    MatchStatLine line = sheet.Lines.First(l =>
                        string.Equals(l.Club, a.Club, StringComparison.Ordinal)
                        && l.Number == a.Number
                    );
                    if (line.IsGoalie || !line.Played || a.Games is < 1 or > 6)
                    {
                        Check(
                            ok: false,
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"a paper injury to a goalie or a player who sat out ({a.Number})"
                            )
                        );
                    }
                }
            }

            double perClub = injuries / (games * 2.0);
            Check(
                perClub is > (InjuryModel.PaperChance * 0.6) and < (InjuryModel.PaperChance * 1.4),
                $"about {F(InjuryModel.PaperChance)} of clubs lose a skater in a game on paper ({F(perClub)})"
            );
            MatchStatSheet none = SyntheticStats.Sheet(clubs[0], clubs[1], result, 3);
            InjuryModel.DrawPaper(none, 0f, 3);
            Check(none.Absences.Count == 0, "with injuries off a game on paper hurts nobody");
        }

        [Test]
        public void LinesWithoutTheInjuredPlayer()
        {
            // a club without spares (W86, SpareTests): the players behind move up
            ClubData club = WorldBuilder.DefaultSetup.Home with
            {
                Spares = [],
            };
            PlayerData firstLeft = club.Roster.First(p => p.Position == PlayingPosition.LW);
            PlayerData secondLeft = club
                .Roster.Where(p => p.Position == PlayingPosition.LW)
                .Skip(1)
                .First();
            AbsenceTable table = new();
            table.Add(Injury(club.Id, firstLeft.Number, 3));
            ClubData dressed = table.Without(club);
            Check(
                dressed.Roster.Count == club.Roster.Count - 1
                    && !dressed.Roster.Contains(firstLeft),
                "the injured player is left out of the club's next game"
            );

            World w = new WorldBuilder()
                .Setup(s => s with { Home = dressed })
                .Then(x => new LinesSystem().Start(x))
                .Build();
            Squad squad = Bench.SquadOf(w, Side.Home).TryGet(out Squad s)
                ? s
                : throw new InvalidOperationException("no squad");
            Check(
                !w.Players.Exists(p => p.Side == Side.Home && p.Data.Number == firstLeft.Number),
                "he is not in the match"
            );
            Check(
                squad.Lines[0].Members.Count == 3
                    && squad.Lines[0].Members.Any(p => p.Data == secondLeft)
                    && squad.Lines[0].Members.All(p => p.OnIce),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the next left wing moves up to the first line (#{secondLeft.Number})"
                )
            );
            Check(
                squad.Lines.Count == 4
                    && squad.Lines.Sum(l => l.Members.Count) == 11
                    && squad.Pairs.Count == 3
                    && squad.Goalies.Count == 2,
                $"four lines still go out, the fourth a man short ({string.Join(' ', squad.Lines.Select(l => l.Members.Count))})"
            );

            // a club never dresses fewer than twelve skaters or no goalie
            AbsenceTable many = new();
            foreach (PlayerData p in club.Everyone())
            {
                many.Add(Injury(club.Id, p.Number, 2));
            }

            ClubData thin = many.Without(club);
            Check(
                thin.Roster.Where(p => p.Position != PlayingPosition.G).Take(13).Count()
                    == AbsenceTable.MinSkaters
                    && thin.Roster.Where(p => p.Position == PlayingPosition.G).Take(2).Count() == 1,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"with everyone hurt a club still dresses {AbsenceTable.MinSkaters} skaters and a goalie ({thin.Roster.Count})"
                )
            );
        }

        [Test]
        public void RecoveryCountsTheClubsGames()
        {
            AbsenceTable table = new();
            table.Add(Injury("home", 9, 2));
            table.Play(Sheet("other", "third"));
            Check(table.GamesLeft("home", 9) == 2, "another club's game does not count down");
            table.Play(Sheet("home", "away"));
            Check(
                table.IsOut("home", 9) && table.GamesLeft("home", 9) == 1,
                "his club's game counts one down"
            );

            MatchStatSheet hurtAgain = Sheet("away", "home");
            hurtAgain.Absences.Add(Injury("home", 11, 1));
            table.Play(hurtAgain);
            Check(
                !table.IsOut("home", 9) && table.Players.Count == 1,
                "after his club's second game he is back"
            );
            Check(
                table.IsOut("home", 11) && table.GamesLeft("home", 11) == 1,
                "a player hurt in a game misses the next one"
            );
        }

        [Test]
        public void MatchInjuryReachesTheSeason()
        {
            MatchStatTracker tracker = new();
            World w = new WorldBuilder().NotBegun().Build();
            tracker.Observe(w);
            Player victim = w.OnIce(Side.Away).First(p => !p.IsGoalie);
            w.Events.Clear();
            w.Events.Publish(
                new PlayerInjured(w.Time, Side.Away, victim.Id, Vector3.Zero, 4, InjuryCause.Fight)
            );
            tracker.Observe(w);
            MatchStatSheet sheet = tracker.Finish();
            Check(
                sheet.Absences.Count == 1
                    && sheet.Absences.First().Number == victim.Data.Number
                    && string.Equals(
                        sheet.Absences.First().Club,
                        w.Setup.Away.Id,
                        StringComparison.Ordinal
                    )
                    && sheet.Absences.First().GamesLeft == 4,
                "the injury is on the match sheet"
            );

            SeasonStats stats = new();
            _ = stats.Credit("g0", sheet);
            Check(
                stats.Absences.GamesLeft(w.Setup.Away.Id, victim.Data.Number) == 4,
                "crediting the game puts him on the season's list"
            );
        }

        [Test]
        public void SeasonSetupSaveAndReplay()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            string path = Path.Combine(folder, "season.json");
            SeasonState season = app.Season.StartNew(clubs, clubs[0].Id, seed: 11, path: path);
            Fixture game = season.NextGame.TryGet(out Fixture g)
                ? g
                : throw new InvalidOperationException("expected a next game");
            ClubData user = clubs.First(c =>
                string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)
            );
            PlayerData hurt = user.Roster.First(p => p.Position == PlayingPosition.C);
            season.Stats.Absences.Add(Injury(user.Id, hurt.Number, 3));

            MatchSetup setup = season.SetupFor(game, clubs);
            ClubData played = setup.Club(
                string.Equals(game.Home, user.Id, StringComparison.Ordinal) ? Side.Home : Side.Away
            );
            Check(
                !played.Roster.Any(p => p.Number == hurt.Number)
                    && played.Roster.Count == user.Roster.Count
                    && played.Roster.Any(p => user.Spares.Any(s => s.Number == p.Number))
                    && setup.DressSpares
                    && setup.Absent.Contains(
                        FormTable.Key(user.Id, hurt.Number),
                        StringComparer.Ordinal
                    ),
                "the season match is set up without him, a spare in his slot, and says so"
            );
            Check(
                !season.InForm(user).Roster.Any(p => p.Number == hurt.Number),
                "a game on paper plays without him too"
            );

            string json = JsonSerializer.Serialize(
                ReplaySetup.Of(setup),
                ReplayJsonContext.Default.ReplaySetup
            );
            MatchSetup rebuilt = (
                JsonSerializer.Deserialize(json, ReplayJsonContext.Default.ReplaySetup)
                ?? throw new InvalidOperationException("replay setup did not read")
            ).ToSetup(clubs);
            Check(
                string.Equals(
                    Clubs.ToJson(rebuilt.Home) + Clubs.ToJson(rebuilt.Away),
                    Clubs.ToJson(setup.Home) + Clubs.ToJson(setup.Away),
                    StringComparison.Ordinal
                ),
                "the replay rebuilds the clubs without him"
            );
            ReplaySetup old =
                JsonSerializer.Deserialize(
                    json.Replace("\"absent\":", "\"unused\":", StringComparison.Ordinal),
                    ReplayJsonContext.Default.ReplaySetup
                ) ?? throw new InvalidOperationException("old replay setup did not read");
            Check(old.Absent.Count == 0, "an older replay reads with everyone dressed");

            SeasonSave.Write(season, path);
            app.Season.End();
            SeasonState again = app.Season.Continue(clubs, path).TryGet(out SeasonState s)
                ? s
                : throw new InvalidOperationException("the save did not read");
            Check(
                again.Stats.Absences.GamesLeft(user.Id, hurt.Number) == 3
                    && again.Stats.Absences.Players.First().Cause == InjuryCause.Check,
                "the injury reads back from the save"
            );
            string saved = File.ReadAllText(path);
            Check(saved.Contains("\"absences\":", StringComparison.Ordinal), "the save carries it");
            File.WriteAllText(
                path,
                saved.Replace("\"absences\":", "\"oldUnused\":", StringComparison.Ordinal)
            );
            Check(
                SeasonSave
                    .Read(path)
                    .Is(x => x.Stats.Absences.Players.Count == 0 && x.Games.Count > 0),
                "a save from before injuries reads, with everyone fit"
            );
        }

        [Test]
        public void SimmedSeasonInjuresAndHeals()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            SeasonState season = app.Season.StartNew(
                clubs,
                clubs[0].Id,
                seed: 3,
                path: Path.Combine(folder, "sim.json")
            );
            season.Rules = RuleSet.Shl with { InjuryRate = InjuryRate.Frequent };
            int seen = 0;
            int maxOut = 0;
            for (int i = 0; i < 20 && app.Season.SimCurrentGame().HasValue; i++)
            {
                seen += season.Stats.Absences.Players.Count(a => a.GamesLeft == a.Games);
                maxOut = Math.Max(maxOut, season.Stats.Absences.Players.Count);
                Check(
                    season.Stats.Absences.Players.All(a => a.GamesLeft is >= 1 and <= 6),
                    "every absence on the list has 1 to 6 games left"
                );
            }

            Check(
                seen > 0,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a season settled on paper has injuries ({seen} new)"
                )
            );
            Check(
                maxOut < clubs.Count * 3,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"players come back as their clubs play (at most {maxOut} out at once)"
                )
            );
        }

        [Test]
        public void DressingRoomListsInjuries()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            SeasonState season = app.Season.StartNew(
                clubs,
                clubs[0].Id,
                seed: 5,
                path: Path.Combine(folder, "room.json")
            );
            ClubData user = clubs.First(c =>
                string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)
            );
            PlayerData hurt = user.Roster.First(p => p.Position == PlayingPosition.D);
            season.Stats.Absences.Add(Injury(user.Id, hurt.Number, 3, InjuryCause.BlockedShot));
            season.Stats.Absences.Add(Injury(clubs[1].Id, 9, 2));

            DressingRoomReport report = DressingRoomSources.Build(
                DressingRoomContext.Of(season, clubs, RuleSet.Shl)
            );
            string expected = InjuryReport.Line(
                season.Stats.Absences.Of(user.Id)[0],
                hurt.LastName
            );
            Check(
                report.Injuries.Count == 2
                    && string.Equals(report.Injuries[0].Text, expected, StringComparison.Ordinal)
                    && report.Injuries[0].Highlight
                    && report
                        .Injuries[0]
                        .Text.Contains(hurt.LastName.ToUpperInvariant(), StringComparison.Ordinal),
                $"the dressing room lists the club's own injury ({(report.Injuries.Count > 0 ? report.Injuries[0].Text : "none")})"
            );
            PlayerData seventh = user.Spares.First(p => p.Position == PlayingPosition.D);
            Check(
                string.Equals(
                    report.Injuries[^1].Text,
                    SpareReport.Line([new SpareIn(seventh, hurt)]),
                    StringComparison.Ordinal
                ),
                $"under it the seventh defenceman who dresses in his slot ({report.Injuries[^1].Text})"
            );
            Check(
                DressingRoomSources
                    .Build(
                        DressingRoomContext.Of(season, clubs, RuleSet.Shl with { Injuries = false })
                    )
                    .Injuries.Count == 0,
                "with injuries off the list stays empty"
            );
        }

        [Test]
        public void DressingRoomListsBothSpares()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            SeasonState season = app.Season.StartNew(
                clubs,
                clubs[0].Id,
                seed: 5,
                path: Path.Combine(folder, "spares.json")
            );
            ClubData user = clubs.First(c =>
                string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)
            );
            PlayerData back = user.Roster.First(p => p.Position == PlayingPosition.D);
            PlayerData centre = user.Roster.First(p => p.Position == PlayingPosition.C);
            season.Stats.Absences.Add(Injury(user.Id, back.Number, 3));
            season.Stats.Absences.Add(Injury(user.Id, centre.Number, 2));

            DressingRoomReport report = DressingRoomSources.Build(
                DressingRoomContext.Of(season, clubs, RuleSet.Shl)
            );
            string line = report.Injuries.Count == 3 ? report.Injuries[2].Text : "";
            Check(
                user.Spares.All(s =>
                    line.Contains(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"#{s.Number} {s.LastName.ToUpperInvariant()}"
                        ),
                        StringComparison.Ordinal
                    )
                ),
                $"two players out, both spares on one line under them ({line})"
            );
        }

        private static Absence Injury(
            string club,
            int number,
            int games,
            InjuryCause cause = InjuryCause.Check
        )
        {
            return new Absence
            {
                Club = club,
                Number = number,
                Reason = AbsenceReason.Injury,
                Cause = cause,
                Games = games,
                GamesLeft = games,
            };
        }

        private static MatchStatSheet Sheet(string home, string away)
        {
            return new MatchStatSheet { Home = home, Away = away };
        }
    }
}
