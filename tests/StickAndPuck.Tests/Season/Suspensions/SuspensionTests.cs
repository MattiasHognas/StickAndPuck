using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Fights;
using StickAndPuck.App.Lines;
using StickAndPuck.App.Rules.Penalties;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Injuries;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.App.Season.Suspensions;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Suspensions
{
    /// <summary>
    /// Suspensions (W129): the presets and the rules page, the 1 to 3 games by offence and strength, a
    /// second fight and a match penalty (a game misconduct, a fight's major and game misconduct, a hit's
    /// major and game misconduct) suspending the player, the suspension reaching the season through the
    /// match sheet, the next game's lines without him, the games served counting down, the save (an older
    /// save reads with nobody suspended) and the dressing room's list.
    /// </summary>
    public sealed class SuspensionTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-suspensiontest-{Environment.ProcessId}-{Guid.NewGuid():N}"
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

        [Test]
        public void PresetsAndRows()
        {
            Check(
                RuleSet.Shl.Suspensions
                    && RuleSet.Nhl.Suspensions
                    && !RuleSet.Arcade.Suspensions
                    && RuleSet.Shl.SuspensionStrength == SuspensionStrength.Normal
                    && RuleSet.Nhl.SuspensionStrength == SuspensionStrength.Normal,
                "SHL and NHL suspend players at normal length, Arcade never"
            );
            Check(
                RuleRows.All.Any(r =>
                    string.Equals(r.Key, nameof(RuleSet.Suspensions), StringComparison.Ordinal)
                    && r.Plays
                    && string.Equals(r.Group, Text.Keys.RuleGroupSeason, StringComparison.Ordinal)
                )
                    && RuleRows.All.Any(r =>
                        string.Equals(
                            r.Key,
                            nameof(RuleSet.SuspensionStrength),
                            StringComparison.Ordinal
                        ) && r.Plays
                    ),
                "both rules are in the rules page's season group"
            );
        }

        [Test]
        public void GamesByOffenceAndStrength()
        {
            RuleSet mild = RuleSet.Shl with { SuspensionStrength = SuspensionStrength.Mild };
            RuleSet strong = RuleSet.Shl with { SuspensionStrength = SuspensionStrength.Strong };
            int one = SuspensionModel.Offence(SuspensionCause.SecondFight, majorForAHit: false);
            int hit = SuspensionModel.Offence(SuspensionCause.MatchPenalty, majorForAHit: true);
            int gm = SuspensionModel.Offence(SuspensionCause.MatchPenalty, majorForAHit: false);
            Check(
                one == 1 && gm == 1 && hit == 2,
                "a hit's major and game misconduct is worth two"
            );
            Check(
                SuspensionModel.Games([one], RuleSet.Shl) == 1
                    && SuspensionModel.Games([hit], RuleSet.Shl) == 2
                    && SuspensionModel.Games([one, gm], RuleSet.Shl) == 2
                    && SuspensionModel.Games([hit, one], RuleSet.Shl) == 3,
                "normal: one game, two for a hit or two offences"
            );
            Check(
                SuspensionModel.Games([one], mild) == 1
                    && SuspensionModel.Games([hit], mild) == 1
                    && SuspensionModel.Games([one], strong) == 2
                    && SuspensionModel.Games([hit, one, gm], strong) == 3,
                "mild takes one off, strong adds one, always 1 to 3 games"
            );
            Check(
                SuspensionModel.Games([hit], RuleSet.Shl with { Suspensions = false }) == 0
                    && SuspensionModel.Games([], RuleSet.Shl) == 0,
                "off, or no offence, suspends nobody"
            );
        }

        [Test]
        public void SecondFightSuspends()
        {
            World w = new WorldBuilder().Rules(RuleSet.Nhl).Build();
            Player a = w.OnIce(Side.Home).First(p => !p.IsGoalie);
            Player b = w.OnIce(Side.Away).First(p => !p.IsGoalie);
            SuspensionSystem system = new();
            system.Start(w);

            Fight(w, system, a, b);
            Check(Suspended(w).Count == 0, "a first fight under NHL rules suspends nobody");

            Fight(w, system, a, b);
            List<PlayerSuspended> second = Suspended(w);
            Check(
                second.Count == 2
                    && second.TrueForAll(s =>
                        s.Games == 1 && s.Cause == SuspensionCause.SecondFight
                    )
                    && second.Exists(s => s.PlayerId == a.Id && s.Side == Side.Home)
                    && second.Exists(s => s.PlayerId == b.Id && s.Side == Side.Away),
                $"a second fight suspends both fighters one game ({Describe(second)})"
            );

            Player c = w.OnIce(Side.Away).Where(p => !p.IsGoalie).Skip(1).First();
            Fight(w, system, a, c);
            Check(
                Suspended(w).Count == 0,
                "a third fight adds nothing for the player already suspended, nor for a first-time fighter"
            );

            World off = new WorldBuilder().Rules(RuleSet.Nhl with { Suspensions = false }).Build();
            SuspensionSystem none = new();
            none.Start(off);
            Player x = off.OnIce(Side.Home).First(p => !p.IsGoalie);
            Player y = off.OnIce(Side.Away).First(p => !p.IsGoalie);
            Fight(off, none, x, y);
            Fight(off, none, x, y);
            Check(Suspended(off).Count == 0, "with suspensions off a second fight costs nothing");
        }

        [Test]
        public void MatchPenaltySuspends()
        {
            World w = new WorldBuilder().Build();
            SuspensionSystem system = new();
            system.Start(w);
            PenaltyBook book = PenaltyCalls.Book(w);
            Player gm = w.OnIce(Side.Home).First(p => !p.IsGoalie);
            Player hitter = w.OnIce(Side.Away).First(p => !p.IsGoalie);
            Player minor = w.OnIce(Side.Away).Where(p => !p.IsGoalie).Skip(1).First();

            w.Events.Clear();
            PenaltyCalls.Assess(
                w,
                book,
                new DelayedPenalty(minor.Side, minor.Id, Infraction.Tripping, w.Time)
            );
            system.Update(w, Game.Step);
            Check(Suspended(w).Count == 0, "a minor suspends nobody");

            w.Events.Clear();
            PenaltyCalls.Assess(
                w,
                book,
                new DelayedPenalty(
                    gm.Side,
                    gm.Id,
                    Infraction.Roughing,
                    w.Time,
                    Severity.GameMisconduct
                )
            );
            PenaltyCalls.Assess(
                w,
                book,
                new DelayedPenalty(
                    hitter.Side,
                    hitter.Id,
                    Infraction.Boarding,
                    w.Time,
                    Severity.MajorAndGameMisconduct
                )
            );
            system.Update(w, Game.Step);
            List<PlayerSuspended> seen = Suspended(w);
            Check(
                seen.Count == 2
                    && seen.TrueForAll(s => s.Cause == SuspensionCause.MatchPenalty)
                    && seen.Exists(s => s.PlayerId == gm.Id && s.Games == 1)
                    && seen.Exists(s => s.PlayerId == hitter.Id && s.Games == 2),
                $"a game misconduct suspends one game, a hit's major and game misconduct two ({Describe(seen)})"
            );
        }

        [Test]
        public void ShlFightAndStrongHit()
        {
            // SHL rules: a fight is a major and game misconduct for both, a match penalty of one game each
            World fight = new WorldBuilder().Build();
            SuspensionSystem fights = new();
            fights.Start(fight);
            Player a = fight.OnIce(Side.Home).First(p => !p.IsGoalie);
            Player b = fight.OnIce(Side.Away).First(p => !p.IsGoalie);
            Fight(fight, fights, a, b);
            Check(Suspended(fight).Count == 0, "dropping the gloves is not yet a suspension");
            FightState s = Fighting.State(fight);
            s.Last = s.Fight;
            s.Fight = Maybe.None<Fight>();
            s.LastEnded = fight.Time;
            fight.Events.Clear();
            foreach (Player p in new[] { a, b })
            {
                PenaltyCalls.Assess(
                    fight,
                    PenaltyCalls.Book(fight),
                    new DelayedPenalty(
                        p.Side,
                        p.Id,
                        FightTuning.Infraction,
                        fight.Time,
                        Severity.MajorAndGameMisconduct
                    ),
                    substitute: true
                );
            }

            fights.Update(fight, Game.Step);
            List<PlayerSuspended> both = Suspended(fight);
            Check(
                both.Count == 2
                    && both.TrueForAll(x =>
                        x.Games == 1 && x.Cause == SuspensionCause.MatchPenalty
                    ),
                $"an SHL fight's ejection suspends both fighters one game ({Describe(both)})"
            );

            World strict = new WorldBuilder()
                .Rules(RuleSet.Shl with { SuspensionStrength = SuspensionStrength.Strong })
                .Build();
            SuspensionSystem strong = new();
            strong.Start(strict);
            Player h = strict.OnIce(Side.Away).First(p => !p.IsGoalie);
            strict.Events.Clear();
            PenaltyCalls.Assess(
                strict,
                PenaltyCalls.Book(strict),
                new DelayedPenalty(
                    h.Side,
                    h.Id,
                    Infraction.Boarding,
                    strict.Time,
                    Severity.MajorAndGameMisconduct
                )
            );
            strong.Update(strict, Game.Step);
            Check(
                Suspended(strict).Count == 1 && Suspended(strict)[0].Games == 3,
                "strong suspensions sit a dangerous hit out three games"
            );
        }

        [Test]
        public void SuspensionReachesTheSeasonAndCountsDown()
        {
            MatchStatTracker tracker = new();
            World w = new WorldBuilder().NotBegun().Build();
            tracker.Observe(w);
            Player p = w.OnIce(Side.Away).First(x => !x.IsGoalie);
            w.Events.Clear();
            w.Events.Publish(
                new PlayerSuspended(
                    w.Time,
                    Side.Away,
                    p.Id,
                    p.Position,
                    1,
                    SuspensionCause.SecondFight
                )
            );
            w.Events.Publish(
                new PlayerSuspended(
                    w.Time,
                    Side.Away,
                    p.Id,
                    p.Position,
                    2,
                    SuspensionCause.MatchPenalty
                )
            );
            tracker.Observe(w);
            MatchStatSheet sheet = tracker.Finish();
            string club = w.Setup.Away.Id;
            int number = p.Data.Number;
            Check(
                sheet.Absences.Count == 1
                    && sheet.Absences.First().Reason == AbsenceReason.Suspension
                    && sheet.Absences.First().Is(club, number)
                    && sheet.Absences.First().GamesLeft == 2,
                "the sheet carries one suspension, the longer"
            );

            SeasonStats stats = new();
            _ = stats.Credit("g0", sheet);
            Check(
                stats.Absences.GamesLeft(club, number) == 2 && stats.Absences.IsOut(club, number),
                "crediting the game suspends him for his club's next two games"
            );
            _ = stats.Credit("g1", new MatchStatSheet { Home = "x", Away = "y" });
            Check(
                stats.Absences.GamesLeft(club, number) == 2,
                "another club's game serves nothing"
            );
            _ = stats.Credit("g2", new MatchStatSheet { Home = club, Away = "y" });
            Check(stats.Absences.GamesLeft(club, number) == 1, "his club's game serves one");
            _ = stats.Credit("g3", new MatchStatSheet { Home = "y", Away = club });
            Check(
                !stats.Absences.IsOut(club, number) && stats.Absences.Players.Count == 0,
                "after two of his club's games he is back"
            );
        }

        [Test]
        public void LinesWithoutTheSuspendedPlayerAndSave()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            string path = Path.Combine(folder, "season.json");
            SeasonState season = app.Season.StartNew(clubs, clubs[0].Id, seed: 13, path: path);
            Fixture game = season.NextGame.TryGet(out Fixture g)
                ? g
                : throw new InvalidOperationException("expected a next game");
            ClubData user = clubs.First(c =>
                string.Equals(c.Id, season.UserClub, StringComparison.Ordinal)
            );
            PlayerData out1 = user.Roster.First(p => p.Position == PlayingPosition.C);
            season.Stats.Absences.Add(Suspension(user.Id, out1.Number, 2));

            MatchSetup setup = season.SetupFor(game, clubs);
            Side side = string.Equals(game.Home, user.Id, StringComparison.Ordinal)
                ? Side.Home
                : Side.Away;
            Check(
                !setup.Club(side).Roster.Any(p => p.Number == out1.Number)
                    && setup.Absent.Contains(
                        FormTable.Key(user.Id, out1.Number),
                        StringComparer.Ordinal
                    ),
                "the season match is set up without the suspended player"
            );
            World w = new WorldBuilder().Setup(setup).Then(x => new LinesSystem().Start(x)).Build();
            Squad squad = Bench.SquadOf(w, side).TryGet(out Squad s)
                ? s
                : throw new InvalidOperationException("no squad");
            Check(
                !w.Players.Exists(p => p.Side == side && p.Data.Number == out1.Number)
                    && squad.Lines.Count == 4
                    && squad.Lines.All(l => l.Members.All(m => m.Data.Number != out1.Number)),
                "his club's lines go out without him"
            );

            SeasonSave.Write(season, path);
            app.Season.End();
            SeasonState again = app.Season.Continue(clubs, path).TryGet(out SeasonState r)
                ? r
                : throw new InvalidOperationException("the save did not read");
            Check(
                again.Stats.Absences.GamesLeft(user.Id, out1.Number) == 2
                    && again.Stats.Absences.Players.First().Reason == AbsenceReason.Suspension,
                "the suspension reads back from the save"
            );
            string saved = File.ReadAllText(path);
            File.WriteAllText(
                path,
                saved.Replace("\"absences\":", "\"oldUnused\":", StringComparison.Ordinal)
            );
            Check(
                SeasonSave
                    .Read(path)
                    .Is(x => x.Stats.Absences.Players.Count == 0 && x.Games.Count > 0),
                "a save from before suspensions reads, with nobody suspended"
            );
        }

        [Test]
        public void DressingRoomListsSuspensions()
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
            PlayerData banned = user.Roster.First(p => p.Position == PlayingPosition.RW);
            season.Stats.Absences.Add(Suspension(user.Id, banned.Number, 2));
            season.Stats.Absences.Add(Suspension(clubs[1].Id, 9, 3));

            DressingRoomReport report = DressingRoomSources.Build(
                DressingRoomContext.Of(season, clubs, RuleSet.Shl)
            );
            string expected = SuspensionReport.Line(
                season.Stats.Absences.Of(user.Id)[0],
                banned.LastName
            );
            Check(
                report.Injuries.Count == 1
                    && string.Equals(report.Injuries[0].Text, expected, StringComparison.Ordinal)
                    && report.Injuries[0].Highlight
                    && report
                        .Injuries[0]
                        .Text.Contains(Text.Get(Text.Keys.Suspended), StringComparison.Ordinal),
                $"the dressing room lists the club's own suspension ({(report.Injuries.Count > 0 ? report.Injuries[0].Text : "none")})"
            );
            Check(
                DressingRoomSources
                    .Build(
                        DressingRoomContext.Of(
                            season,
                            clubs,
                            RuleSet.Shl with
                            {
                                Suspensions = false,
                            }
                        )
                    )
                    .Injuries.Count == 0,
                "with suspensions off the list stays empty"
            );
        }

        /// <summary>One fight between two skaters as the fight system starts it: the fight on the state and a
        /// <see cref="FightStarted"/> event, then a step of the suspension system.</summary>
        private static void Fight(World w, SuspensionSystem system, Player a, Player b)
        {
            FightState s = Fighting.State(w);
            s.Fight = new Fight(new Fighter(a.Id, -1), new Fighter(b.Id, -1), a.Position);
            w.Events.Clear();
            w.Events.Publish(new FightStarted(w.Time, b.Side, b.Id, a.Position));
            system.Update(w, Game.Step);
        }

        private static List<PlayerSuspended> Suspended(World w)
        {
            return [.. w.Events.Of<PlayerSuspended>()];
        }

        private static string Describe(IEnumerable<PlayerSuspended> seen)
        {
            return string.Join(
                ", ",
                seen.Select(s =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{s.Side} {s.PlayerId} {s.Games} {s.Cause}"
                    )
                )
            );
        }

        private static Absence Suspension(string club, int number, int games)
        {
            return new Absence
            {
                Club = club,
                Number = number,
                Reason = AbsenceReason.Suspension,
                Games = games,
                GamesLeft = games,
            };
        }
    }
}
