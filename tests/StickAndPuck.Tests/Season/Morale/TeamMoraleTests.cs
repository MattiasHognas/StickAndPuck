using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StickAndPuck.App.Core;
using StickAndPuck.App.Momentum;
using StickAndPuck.App.Season.Morale;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Morale
{
    /// <summary>
    /// Team morale: results move it (wins up, losses down, streaks and margins more, overtime less),
    /// it drifts back toward neutral and stays within 0 to 100, the rules turn it off and scale it, it
    /// starts a season game's momentum meter and tilts the simulated games, and a save from before
    /// morale still loads, with morale worked out from its results.
    /// </summary>
    public sealed class TeamMoraleTests : CheckedTest
    {
        private static IReadOnlyList<ClubData> Clubs => WorldBuilder.Clubs;

        private static SeasonState NewSeason(RuleSet rules)
        {
            SeasonState season = SeasonState.New(Clubs, Clubs[0].Id, 7);
            season.Rules = rules;
            return season;
        }

        private static string S(FormattableString s)
        {
            return s.ToString(CultureInfo.InvariantCulture);
        }

        // records the user's club's games of rounds from..rounds-1 with one outcome, the user at home or away
        private static void Play(
            SeasonState season,
            int from,
            int rounds,
            bool win,
            ResultKind kind
        )
        {
            foreach (
                Fixture g in season
                    .Games.Where(g =>
                        g.Round >= from && g.Round < rounds && g.Involves(season.UserClub)
                    )
                    .ToList()
            )
            {
                bool home = string.Equals(g.Home, season.UserClub, StringComparison.Ordinal);
                int mine = win ? 3 : 1,
                    theirs = win ? 1 : 3;
                season.Record(
                    g.Number,
                    home
                        ? new FixtureResult(mine, theirs, kind)
                        : new FixtureResult(theirs, mine, kind)
                );
            }
        }

        [Test]
        public void ResultsMoveMorale()
        {
            SeasonState season = NewSeason(RuleSet.Shl);
            string club = season.UserClub;
            Check(TeamMorale.Of(season, club) == TeamMorale.Neutral, "a new season starts at 50");

            Play(season, 0, 1, win: true, ResultKind.Regulation);
            int afterWin = TeamMorale.Of(season, club);
            Check(afterWin > TeamMorale.Neutral, S($"a regulation win lifts morale ({afterWin})"));

            Play(season, 1, 5, win: true, ResultKind.Regulation);
            int afterStreak = TeamMorale.Of(season, club);
            Check(
                afterStreak > afterWin + 10,
                S($"a five-game win streak lifts it a lot ({afterWin} -> {afterStreak})")
            );

            SeasonState losing = NewSeason(RuleSet.Shl);
            Play(losing, 0, 5, win: false, ResultKind.Regulation);
            int lost = TeamMorale.Of(losing, club);
            Check(
                lost < TeamMorale.Neutral - 10,
                S($"a five-game losing streak sinks it ({lost})")
            );

            SeasonState overtime = NewSeason(RuleSet.Shl);
            Play(overtime, 0, 1, win: true, ResultKind.Overtime);
            int otWin = TeamMorale.Of(overtime, club);
            Check(
                otWin > TeamMorale.Neutral && otWin < afterWin,
                S($"an overtime win lifts it less than a regulation win ({otWin} < {afterWin})")
            );

            SeasonState hot = NewSeason(RuleSet.Shl);
            Play(hot, 0, 52, win: true, ResultKind.Regulation);
            int top = TeamMorale.Of(hot, club);
            Check(
                top is > 75 and <= TeamMorale.Max,
                S($"a perfect season stays high but within 100 ({top})")
            );

            SeasonState drift = NewSeason(RuleSet.Shl);
            drift.Morale[club] = 90;
            Fixture next = drift.Games.First(g => g.Involves(club));
            drift.Record(next.Number, new FixtureResult(2, 2, ResultKind.Regulation));
            Check(
                TeamMorale.Of(drift, club) < 90,
                S($"a tie lets high morale drift back toward 50 ({TeamMorale.Of(drift, club)})")
            );
        }

        [Test]
        public void RulesTurnMoraleOffAndScaleIt()
        {
            SeasonState off = NewSeason(RuleSet.Shl with { Morale = false });
            Play(off, 0, 5, win: true, ResultKind.Regulation);
            Check(
                TeamMorale.Of(off, off.UserClub) == TeamMorale.Neutral,
                "morale off: results leave it at 50"
            );
            Check(
                TeamMorale.Nudge(off, off.UserClub, 10) == TeamMorale.Neutral,
                "morale off: a nudge does nothing"
            );
            Check(
                !off.SetupFor(off.Games[0], Clubs).Morale.HasValue,
                "morale off: a game's setup carries no morale"
            );

            SeasonState mild = NewSeason(RuleSet.Shl with { MoraleStrength = MoraleStrength.Mild });
            SeasonState strong = NewSeason(
                RuleSet.Shl with
                {
                    MoraleStrength = MoraleStrength.Strong,
                }
            );
            Play(mild, 0, 3, win: true, ResultKind.Regulation);
            Play(strong, 0, 3, win: true, ResultKind.Regulation);
            int m = TeamMorale.Of(mild, mild.UserClub),
                s = TeamMorale.Of(strong, strong.UserClub);
            Check(m > TeamMorale.Neutral && s > m, S($"strong swings more than mild ({m} < {s})"));

            Check(!RuleSet.Arcade.Morale, "the Arcade preset plays without morale");
            Check(
                RuleSet.Shl.Morale && RuleSet.Shl.MoraleStrength == MoraleStrength.Normal,
                "SHL: morale on, normal"
            );
            Check(
                RuleSet.Nhl.Morale && RuleSet.Nhl.MoraleStrength == MoraleStrength.Strong,
                "NHL: morale on, strong"
            );
        }

        [Test]
        public void NudgeMovesMoraleWithinBounds()
        {
            SeasonState season = NewSeason(RuleSet.Shl);
            string club = season.UserClub;
            Check(
                TeamMorale.Nudge(season, club, 8) == 58,
                S($"a nudge of 8 at normal strength gives 58 ({TeamMorale.Of(season, club)})")
            );
            Check(
                TeamMorale.Nudge(season, club, 500) == TeamMorale.Max,
                "a huge nudge stops at 100"
            );
            Check(TeamMorale.Nudge(season, club, -500) == TeamMorale.Min, "a huge drop stops at 0");
            Check(
                TeamMorale.Nudge(season, "nobody", 10) == TeamMorale.Neutral
                    && !season.Morale.ContainsKey("nobody"),
                "a club outside the season is ignored"
            );

            SeasonState strong = NewSeason(
                RuleSet.Shl with
                {
                    MoraleStrength = MoraleStrength.Strong,
                }
            );
            Check(TeamMorale.Nudge(strong, club, 8) == 62, "strong scales a nudge by 1.5");
        }

        [Test]
        public void MoraleStartsTheMomentumMeter()
        {
            SeasonState season = NewSeason(RuleSet.Shl);
            Fixture game = season.Games.First(g => g.Involves(season.UserClub));
            season.Morale[game.Home] = 90;
            season.Morale[game.Away] = 30;
            MatchSetup setup = season.SetupFor(game, Clubs).WithRules(RuleSet.Shl);
            Check(
                setup.Morale == new MatchMorale(90, 30),
                S($"the season game's setup carries both clubs' morale ({setup.Morale})")
            );

            World world = new WorldBuilder().Setup(_ => setup).Build();
            _ = MomentumMeter.State(world); // as MomentumSystem.Start makes it
            float home = MomentumMeter.Of(world, Side.Home),
                away = MomentumMeter.Of(world, Side.Away);
            Check(
                Math.Abs(home - (0.8f * MomentumTuning.MoraleStart)) < 0.001f,
                S($"morale 90 starts the home boost at 0.4 ({home:0.000})")
            );
            Check(away == 0f, S($"morale 30 starts without a boost ({away:0.000})"));

            World plain = new WorldBuilder().Build();
            _ = MomentumMeter.State(plain);
            Check(
                MomentumMeter.Of(plain, Side.Home) == 0f
                    && MomentumMeter.Of(plain, Side.Away) == 0f,
                "a match outside a season starts with no boost"
            );

            World off = new WorldBuilder()
                .Setup(_ => setup with { Rules = RuleSet.Shl with { Morale = false } })
                .Build();
            _ = MomentumMeter.State(off);
            Check(MomentumMeter.Of(off, Side.Home) == 0f, "with morale off the meter starts empty");
            Check(
                MomentumMeter.StartLevel(100, RuleSet.Nhl)
                    > MomentumMeter.StartLevel(100, RuleSet.Shl),
                "NHL's stronger morale starts a bigger boost"
            );
        }

        [Test]
        public void MoraleTiltsQuickSim()
        {
            ClubData home = Clubs[0],
                away = Clubs[1];
            SeasonState season = NewSeason(RuleSet.Shl);
            int neutralWins = 0,
                highWins = 0,
                lowWins = 0,
                same = 0;
            for (int seed = 0; seed < 600; seed++)
            {
                season.Morale.Clear();
                FixtureResult neutral = TeamMorale.Sim(season, home, away, seed);
                same += neutral == QuickSim.Play(home, away, seed) ? 1 : 0;
                season.Morale[home.Id] = 100;
                FixtureResult high = TeamMorale.Sim(season, home, away, seed);
                season.Morale[home.Id] = 0;
                FixtureResult low = TeamMorale.Sim(season, home, away, seed);
                neutralWins += neutral.HomeGoals > neutral.AwayGoals ? 1 : 0;
                highWins += high.HomeGoals > high.AwayGoals ? 1 : 0;
                lowWins += low.HomeGoals > low.AwayGoals ? 1 : 0;
            }

            Check(
                same == 600,
                S($"neutral morale leaves every simulated score as before ({same} of 600)")
            );
            Check(
                highWins > neutralWins + 15 && lowWins < neutralWins - 15,
                S(
                    $"high morale wins more and low morale fewer (low {lowWins}, neutral {neutralWins}, high {highWins} of 600)"
                )
            );

            SeasonState off = NewSeason(RuleSet.Shl with { Morale = false });
            off.Morale[home.Id] = 100;
            Check(
                Math.Abs(TeamMorale.GoalLift(off, home.Id) - 1.0) < 1e-12,
                "with morale off the sim is untouched"
            );
        }

        [Test]
        public void SimulatedRoundsMoveEveryClub()
        {
            SeasonState season = NewSeason(RuleSet.Shl);
            _ = season.SimulateUpTo(9, Clubs);
            int moved = season.Clubs.Count(c => TeamMorale.Of(season, c) != TeamMorale.Neutral);
            Check(
                moved >= 10,
                S($"ten simulated rounds move most clubs' morale ({moved} of {season.Clubs.Count})")
            );
        }

        [Test]
        public void OldSaveLoadsAndRebuildsMorale()
        {
            string folder = Path.Combine(
                Path.GetTempPath(),
                S($"sap-moraletest-{Environment.ProcessId}-{Guid.NewGuid():N}")
            );
            string path = Path.Combine(folder, "season.json");
            try
            {
                SeasonState season = NewSeason(RuleSet.Shl);
                for (int round = 0; round < 6; round++)
                {
                    Play(season, round, round + 1, win: true, ResultKind.Regulation);
                    _ = season.SimulateUpTo(round, Clubs); // the rest of the round, as after a played game
                }
                Dictionary<string, int> expected = new(season.Morale, StringComparer.Ordinal);
                Check(expected.Count > 0, "the season has morale before saving");

                JsonObject json = JsonNode
                    .Parse(JsonSerializer.Serialize(season, SaveJsonContext.Default.SeasonState))
                    .AsObject();
                Check(
                    json.Remove("morale"),
                    "the save holds a morale entry, removed to make an older save"
                );
                _ = Directory.CreateDirectory(folder);
                File.WriteAllText(path, json.ToJsonString());

                Check(
                    SeasonSave.Read(path).TryGet(out SeasonState read),
                    "a save without morale loads"
                );
                Check(
                    read.Games.Where(g => g.Played)
                        .SequenceEqual(season.Games.Where(g => g.Played)),
                    "its results are kept"
                );
                Check(
                    expected.All(kv => TeamMorale.Of(read, kv.Key) == kv.Value),
                    "morale is worked out again from its results, as it was played"
                );

                SeasonSave.Write(read, path);
                Check(
                    SeasonSave.Read(path).TryGet(out SeasonState again)
                        && again.Morale.Count == read.Morale.Count
                        && again.Morale.All(kv => read.Morale[kv.Key] == kv.Value),
                    "morale round-trips through the save"
                );
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
        }
    }
}
