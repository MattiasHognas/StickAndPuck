using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Board;
using StickAndPuck.App.Season.DressingRoom;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Board
{
    /// <summary>
    /// The board's expectation and the hot seat (W133): the goal picked from a club's strength rank in the SHL
    /// and the NHL, the dressing room's board line as the table moves, the hot seat firing a club far below its
    /// goal late in the season (and never a club meeting it, or with the rule off), the fired season's end, the
    /// verdict, and the goal and the sack surviving the save (an older save gets its goal on load).
    /// </summary>
    public sealed class BoardExpectationTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-board-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        private static IReadOnlyList<ClubData> Shl => WorldBuilder.Clubs;

        private static string S(FormattableString s)
        {
            return s.ToString(CultureInfo.InvariantCulture);
        }

        [After(Test)]
        public void EndSession()
        {
            app.Season.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static string Ranked(IReadOnlyList<ClubData> clubs, int rank)
        {
            return clubs.First(c => BoardExpectation.Rank(clubs, c.Id) == rank).Id;
        }

        private SeasonState Start(string club, RuleSet rules)
        {
            SeasonState season = app.Season.StartNew(
                Shl,
                club,
                Maybe.Some(7),
                Path.Combine(folder, "season.json")
            );
            season.Rules = rules;
            return season;
        }

        /// <summary>Plays the human's next game as a win or a loss, through the session as a played match is.</summary>
        private bool Play(SeasonState season, bool win)
        {
            if (app.Season.NextSetup(InputDevice.None) is not NextMatch.Play)
            {
                return false;
            }

            bool home = app.Season.Playing.Is(g =>
                string.Equals(g.Home, season.UserClub, StringComparison.Ordinal)
            );
            int mine = win ? 4 : 1,
                theirs = win ? 1 : 4;
            return app.Season.Finish(
                home
                    ? new FixtureResult(mine, theirs, ResultKind.Regulation)
                    : new FixtureResult(theirs, mine, ResultKind.Regulation)
            );
        }

        /// <summary>Plays the human's games one by one, all won or all lost, until the season ends for them or (with
        /// <paramref name="untilFired"/>) the board fires them.</summary>
        private int PlayOut(SeasonState season, bool win, bool untilFired)
        {
            int played = 0;
            while (!(untilFired && season.Fired) && Play(season, win))
            {
                played++;
            }

            return played;
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        [Test]
        public void Presets()
        {
            Check(
                RuleSet.Shl.HotSeat == HotSeat.Lenient
                    && RuleSet.Nhl.HotSeat == HotSeat.Strict
                    && RuleSet.Arcade.HotSeat == HotSeat.Off,
                "the hot seat is lenient in the SHL, strict in the NHL and off in Arcade"
            );
            Check(
                RuleRows.All.Any(r =>
                    string.Equals(r.Key, nameof(RuleSet.HotSeat), StringComparison.Ordinal)
                    && r.Plays
                    && string.Equals(r.Group, Text.Keys.RuleGroupSeason, StringComparison.Ordinal)
                ),
                "HOT SEAT is a rule of the SEASON group"
            );
        }

        [Test]
        public void ShlGoalsByStrength()
        {
            IReadOnlyList<ClubData> clubs = Shl;
            Dictionary<BoardGoal, int> count = [];
            for (int rank = 1; rank <= clubs.Count; rank++)
            {
                BoardGoal goal = BoardExpectation.Pick(clubs, Ranked(clubs, rank));
                count[goal] = count.GetValueOrDefault(goal) + 1;
                BoardGoal expected =
                    rank == 1 ? BoardGoal.WinLeague
                    : rank <= 4 ? BoardGoal.Top
                    : rank <= 10 ? BoardGoal.MakePlayoffs
                    : BoardGoal.AvoidBottom;
                Check(goal == expected, S($"SHL strength rank {rank} gets {goal} ({expected})"));
            }

            Check(clubs.Count == 14, S($"the SHL has 14 clubs ({clubs.Count})"));
            Check(
                BoardExpectation.Line(BoardGoal.Top, League.Shl, 14) == 4
                    && BoardExpectation.Line(BoardGoal.MakePlayoffs, League.Shl, 14) == 10
                    && BoardExpectation.Line(BoardGoal.AvoidBottom, League.Shl, 14) == 12,
                "SHL lines: top 4, the playoffs down to the play-in's 10th, out of the bottom 2"
            );
            Check(
                BoardExpectation.Margin(BoardGoal.WinLeague, League.Shl, 14) == 3
                    && BoardExpectation.Margin(BoardGoal.AvoidBottom, League.Shl, 14) == 2,
                "SHL margin: three places, two for the bottom goal so last always misses badly"
            );
            Check(
                Same(Text.Format(Text.Keys.BoardGoalTop, 4), "FINISH IN THE TOP 4")
                    && Same(
                        BoardExpectation.Describe(BoardGoal.AvoidBottom, League.Shl, 14),
                        "STAY OUT OF THE BOTTOM 2"
                    ),
                "the goals in words"
            );

            SeasonState season = Start(Ranked(clubs, 1), RuleSet.Shl);
            Check(
                season.Goal.Is(g => g == BoardGoal.WinLeague),
                "a new season for the strongest club sets the goal to win the league"
            );
        }

        [Test]
        public void NhlGoalsByStrength()
        {
            IReadOnlyList<ClubData> clubs = Clubs.LoadNhl();
            Dictionary<BoardGoal, int> count = [];
            foreach (ClubData c in clubs)
            {
                BoardGoal goal = BoardExpectation.Pick(clubs, c.Id);
                count[goal] = count.GetValueOrDefault(goal) + 1;
            }

            Check(
                clubs.Count == 32
                    && count.GetValueOrDefault(BoardGoal.WinLeague) == 2
                    && count.GetValueOrDefault(BoardGoal.Top) == 6
                    && count.GetValueOrDefault(BoardGoal.MakePlayoffs) == 8
                    && count.GetValueOrDefault(BoardGoal.AvoidBottom) == 16,
                S(
                    $"NHL goals: 2 win the league, 6 top 8, 8 make the playoffs, 16 stay out of the bottom 4 ({string.Join(", ", count.Select(kv => S($"{kv.Key} {kv.Value}")))})"
                )
            );
            Check(
                BoardExpectation.Line(BoardGoal.Top, League.Nhl, 32) == 8
                    && BoardExpectation.Line(BoardGoal.MakePlayoffs, League.Nhl, 32) == 16
                    && BoardExpectation.Line(BoardGoal.AvoidBottom, League.Nhl, 32) == 28
                    && BoardExpectation.Margin(BoardGoal.MakePlayoffs, League.Nhl, 32) == 6,
                "NHL lines: top 8, 16 playoff spots, out of the bottom 4; six places of margin"
            );

            // the NHL playoff goal counts the division and wild card spots, not the overall place
            SeasonState season = SeasonState.New(clubs, Ranked(clubs, 12), seed: 3);
            season.Rules = RuleSet.Nhl;
            _ = season.SimulateUpTo(season.Rounds - 1, clubs);
            Check(
                season.Goal.Is(g => g == BoardGoal.MakePlayoffs),
                "rank 12 must make the playoffs"
            );
            IReadOnlyList<StandingRow> table = season.Table;
            HashSet<string> field =
            [
                with(StringComparer.Ordinal),
                .. App.Season.Playoffs.NhlSeeding.Field(season, table).Tags.Keys,
            ];
            List<string> wrong = [];
            foreach (string id in clubs.Select(c => c.Id))
            {
                season.UserClub = id;
                if (
                    BoardExpectation.MeetsGoal(season, goal: BoardGoal.MakePlayoffs)
                    != field.Contains(id)
                )
                {
                    wrong.Add(id);
                }
            }

            Check(
                wrong.Count == 0,
                S(
                    $"an NHL club meets the playoff goal exactly when it is in the field ({string.Join(", ", wrong)})"
                )
            );

            Check(field.Count == 16, S($"the NHL field has 16 clubs ({field.Count})"));
        }

        [Test]
        public void TracksThePlace()
        {
            IReadOnlyList<ClubData> clubs = Shl;
            SeasonState season = Start(Ranked(clubs, 6), RuleSet.Shl);
            Check(
                season.Goal.Is(g => g == BoardGoal.MakePlayoffs),
                "rank 6 must make the playoffs"
            );
            DressingRoomReport before = DressingRoomSources.Build(
                DressingRoomContext.Of(season, clubs, RuleSet.Shl)
            );
            Check(
                before.Board.Count == 1 && Same(before.Board[0].Text, "TARGET: MAKE THE PLAYOFFS"),
                S(
                    $"before the first game the board gives its target ({string.Join(" | ", before.Board.Select(b => b.Text))})"
                )
            );

            for (int i = 0; i < 12; i++)
            {
                _ = Play(season, win: false);
            }

            int place = BoardExpectation.Place(season);
            Check(place == 14, S($"twelve losses put the club last ({place})"));
            Check(
                BoardExpectation.Standing(season) == BoardStanding.HotSeat,
                S(
                    $"four places below the playoffs is the hot seat ({BoardExpectation.Standing(season)})"
                )
            );
            Check(!season.Fired, "but nobody is fired before the last quarter of the season");
            DressingRoomReport report = DressingRoomSources.Build(
                DressingRoomContext.Of(season, clubs, RuleSet.Shl)
            );
            Check(
                report.Board.Count == 1
                    && Same(
                        report.Board[0].Text,
                        "TARGET: MAKE THE PLAYOFFS. PLACE 14 OF 14, ON THE HOT SEAT"
                    )
                    && report.Board[0].Highlight,
                S(
                    $"the dressing room's board line ({string.Join(" | ", report.Board.Select(b => b.Text))})"
                )
            );

            season.Rules = RuleSet.Shl with { HotSeat = HotSeat.Off };
            Check(
                BoardExpectation.Standing(season) == BoardStanding.UnderPressure,
                "with the hot seat off the same place is only pressure"
            );

            season.Goal = BoardGoal.WinLeague;
            Check(
                BoardExpectation.Standing(season) == BoardStanding.UnderPressure
                    && BoardExpectation.Deficit(season) == 13,
                "against a title goal last place is thirteen places short"
            );
        }

        [Test]
        public void TracksAWinner()
        {
            IReadOnlyList<ClubData> clubs = Shl;
            SeasonState winner = Start(Ranked(clubs, 6), RuleSet.Shl);
            for (int i = 0; i < 12; i++)
            {
                _ = Play(winner, win: true);
            }

            Check(BoardExpectation.Place(winner) == 1, "twelve wins put the club top");
            Check(
                BoardExpectation.Standing(winner) == BoardStanding.Ahead,
                "top of the table is ahead of a playoff target"
            );
            winner.Goal = BoardGoal.WinLeague;
            Check(
                BoardExpectation.Standing(winner) == BoardStanding.OnTrack,
                "top of the table is on track for the title"
            );
            string line = DressingRoomSources
                .Build(DressingRoomContext.Of(winner, clubs, RuleSet.Shl))
                .Board[0]
                .Text;
            Check(
                Same(line, "TARGET: WIN THE REGULAR SEASON. PLACE 1 OF 14, ON TRACK"),
                S($"on track in words ({line})")
            );
            Check(
                !DressingRoomSources
                    .Build(DressingRoomContext.Of(winner, clubs, RuleSet.Shl))
                    .Board[0]
                    .Highlight,
                "on track is not highlighted"
            );
        }

        [Test]
        public void HotSeatFiresLate()
        {
            SeasonState season = Start(Ranked(Shl, 1), RuleSet.Shl);
            int total = season.Games.Count(g => g.Involves(season.UserClub));
            int played = 0;
            bool spared = false;
            while (!season.Fired && Play(season, win: false))
            {
                played++;
                spared |=
                    !season.Fired
                    && BoardExpectation.Progress(season) >= BoardExpectation.LenientFrom;
            }

            Check(!spared, "far below the goal in the last quarter the board always fires");

            int from = (int)Math.Ceiling(total * BoardExpectation.LenientFrom);
            Check(season.Fired, "losing every game gets the title favourite fired");
            Check(
                played == from,
                S($"fired at game {played}, the first of the last quarter ({from} of {total})")
            );
            Check(
                app.Season.NextSetup(InputDevice.None) is not NextMatch.Play
                    && !app.Season.SimCurrentGame().HasValue
                    && !app.Season.Playoffs.Open().HasValue,
                "a fired season plays and sims no more games and has no playoffs"
            );
            Check(
                !app.Screens.SeasonHasNextGame()
                    && app.Screens.Overview() is FiredScreen
                    && app.Screens.AfterSeasonGame() is FiredScreen,
                "the screens show the fired screen in place of the table"
            );
            Check(
                BoardExpectation.Verdict(season).Is(v => v == BoardVerdict.Missed),
                "the verdict on a fired season is missed"
            );
            Check(!BoardExpectation.CheckHotSeat(season), "a fired human is not fired twice");
        }

        [Test]
        public void StrictFiresAtHalfway()
        {
            SeasonState season = Start(
                Ranked(Shl, 1),
                RuleSet.Shl with
                {
                    HotSeat = HotSeat.Strict,
                }
            );
            int total = season.Games.Count(g => g.Involves(season.UserClub));
            int played = PlayOut(season, win: false, untilFired: true);
            Check(
                season.Fired && played == (int)Math.Ceiling(total * BoardExpectation.StrictFrom),
                S($"a strict board fires at halfway ({played} of {total})")
            );
        }

        [Test]
        public void NotFiredWhenOffOrOnTarget()
        {
            SeasonState off = Start(Ranked(Shl, 1), RuleSet.Shl with { HotSeat = HotSeat.Off });
            _ = PlayOut(off, win: false, untilFired: false);

            Check(
                !off.Fired && off.IsOver,
                "with the hot seat off a losing season runs to its end"
            );
            Check(
                BoardExpectation.Verdict(off).Is(v => v == BoardVerdict.Missed),
                "and the board's verdict is missed"
            );
            Check(app.Season.Playoffs.Open().HasValue, "and the playoffs start as usual");

            SeasonState good = Start(Ranked(Shl, 14), RuleSet.Shl);
            Check(
                good.Goal.Is(g => g == BoardGoal.AvoidBottom),
                "the weakest club must stay out of the bottom"
            );
            _ = PlayOut(good, win: true, untilFired: false);

            Check(!good.Fired && good.IsOver, "a winning season is never fired");
            Check(
                BoardExpectation.Verdict(good).Is(v => v == BoardVerdict.Exceeded),
                "winning the league with a bottom goal exceeds it"
            );
            good.Goal = BoardGoal.WinLeague;
            Check(
                BoardExpectation.Verdict(good).Is(v => v == BoardVerdict.Met),
                "winning the league with a title goal meets it"
            );
            Check(
                !BoardExpectation.Verdict(Start(Ranked(Shl, 3), RuleSet.Shl)).HasValue,
                "no verdict while the season runs"
            );
        }

        [Test]
        public void SaveAndLoad()
        {
            string path = Path.Combine(folder, "season.json");
            SeasonState season = Start(
                Ranked(Shl, 1),
                RuleSet.Shl with
                {
                    HotSeat = HotSeat.Strict,
                }
            );
            _ = PlayOut(season, win: false, untilFired: true);

            SeasonSave.Write(season, path);
            app.Season.End();
            Check(
                SeasonSave
                    .Read(path)
                    .Is(s =>
                        s.Fired
                        && s.Goal.Is(g => g == BoardGoal.WinLeague)
                        && s.Rules.Is(r => r.HotSeat == HotSeat.Strict)
                    ),
                "the goal, the sack and the hot seat rule read back from the save"
            );

            string saved = File.ReadAllText(path);
            Check(
                saved.Contains("\"goal\": \"WinLeague\"", StringComparison.Ordinal),
                "the save names the goal"
            );
            File.WriteAllText(
                path,
                saved.Replace("\"goal\":", "\"oldUnused\":", StringComparison.Ordinal)
            );
            Check(
                SeasonSave.Read(path).Is(s => s.Goal.Is(g => g == BoardGoal.WinLeague)),
                "a save from before the board gets its goal on load"
            );

            Maybe<SeasonState> continued = app.Season.Continue(Shl, path);
            Check(
                continued.Is(s => s.Fired) && app.Screens.Overview() is FiredScreen,
                "continuing a fired save opens the fired screen"
            );
        }
    }
}
