using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Board;
using StickAndPuck.App.Season.Challenges;
using StickAndPuck.App.Season.Injuries;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Recap;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Season.Stats;
using StickAndPuck.App.Season.Trades;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Recap
{
    /// <summary>
    /// The season recap (W136): the regular season split into month blocks, the best game, the club's top scorer,
    /// the injuries kept in the season's injury log (and its save), the board's verdict, the headlines picked for
    /// each way a season ends (champions, final lost, semi-final, playoff exit, missed playoffs, stayed up,
    /// relegated, fired) and for what happened in it, and the recap as the last step of the season's end screens.
    /// </summary>
    public sealed class SeasonRecapTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-recap-{Environment.ProcessId}-{Guid.NewGuid():N}"
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

        private SeasonState Start(string club)
        {
            SeasonState season = app.Season.StartNew(
                Shl,
                club,
                Maybe.Some(11),
                Path.Combine(folder, "season.json")
            );
            season.Rules = RuleSet.Shl with { HotSeat = HotSeat.Off };
            return season;
        }

        /// <summary>Plays the human's next game with this score (own goals first), through the session as a played match is.</summary>
        private bool Play(
            SeasonState season,
            int own,
            int other,
            ResultKind kind = ResultKind.Regulation
        )
        {
            if (app.Season.NextSetup(InputDevice.None) is not NextMatch.Play)
            {
                return false;
            }

            bool home = app.Season.Playing.Is(g => Same(g.Home, season.UserClub));
            return app.Season.Finish(
                home ? new FixtureResult(own, other, kind) : new FixtureResult(other, own, kind)
            );
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        private static string NameOf(string id)
        {
            return Shl.First(c => Same(c.Id, id)).Name;
        }

        private static IReadOnlyList<HeadlineKind> Kinds(SeasonRecap recap)
        {
            return [.. recap.Headlines.Select(h => h.Kind)];
        }

        private static string Listed(SeasonRecap recap)
        {
            return string.Join(", ", Kinds(recap));
        }

        [Test]
        public void MonthBlocks()
        {
            SeasonState season = Start(Shl[0].Id);
            // 10 games: 6 regulation wins, 2 overtime losses, 2 regulation losses
            (int Own, int Other, ResultKind Kind)[] results =
            [
                (3, 1, ResultKind.Regulation),
                (4, 2, ResultKind.Regulation),
                (2, 3, ResultKind.Overtime),
                (5, 0, ResultKind.Regulation),
                (1, 4, ResultKind.Regulation),
                (2, 1, ResultKind.Regulation),
                (3, 4, ResultKind.Shootout),
                (6, 2, ResultKind.Regulation),
                (0, 2, ResultKind.Regulation),
                (4, 3, ResultKind.Regulation),
            ];
            foreach ((int own, int other, ResultKind kind) in results)
            {
                Check(Play(season, own, other, kind), "the human's game is played");
            }

            SeasonRecap recap = SeasonRecap.Build(season, app.Season.Clubs);
            int total = season.Games.Count(g => g.Involves(season.UserClub));
            Check(
                recap.Blocks.Count == SeasonRecap.Months,
                S($"{recap.Blocks.Count} month blocks")
            );
            Check(
                recap.Blocks.Sum(b => b.Scheduled) == total && total == 52,
                S($"the blocks hold all {total} of the club's games")
            );
            Check(
                recap.Blocks.All(b => b.Scheduled is 7 or 8),
                S(
                    $"the blocks are equal to a game ({string.Join(',', recap.Blocks.Select(b => b.Scheduled))})"
                )
            );
            Check(
                recap.Blocks.Select(b => b.Month).SequenceEqual(Enumerable.Range(0, 7)),
                "the SHL's blocks run September to March"
            );
            RecapBlock first = recap.Blocks[0],
                second = recap.Blocks[1];
            Check(
                first.Played == 7
                    && second.Played == 3
                    && recap.Blocks.Skip(2).All(b => b.Played == 0),
                S(
                    $"10 games played: {first.Played} in the first block, {second.Played} in the second"
                )
            );
            RecapBlock t = recap.Total;
            Check(
                t.Wins == 6 && t.Losses == 2 && t.OvertimeLosses == 2 && t.Points == (6 * 3) + 2,
                S(
                    $"the season so far is 6-2-2 with 20 points ({t.Wins}-{t.Losses}-{t.OvertimeLosses}, {t.Points})"
                )
            );
            Check(
                first.Wins == 4 && first.Losses == 1 && first.OvertimeLosses == 2,
                S($"September is 4-1-2 ({first.Wins}-{first.Losses}-{first.OvertimeLosses})")
            );
            Check(
                Same(RecapText.Record(first), "4-1-2")
                    && Same(RecapText.Record(recap.Blocks[3]), "-"),
                "a block's record reads W-L-OTL, a block not played a dash"
            );
        }

        [Test]
        public void NhlMonths()
        {
            IReadOnlyList<ClubData> nhl = Clubs.LoadNhl();
            SeasonRecap recap = SeasonRecap.Build(SeasonState.New(nhl, nhl[0].Id, 3), nhl);
            Check(
                recap.Blocks[0].Month == 1
                    && recap.Blocks[^1].Month == 7
                    && recap.Blocks.Sum(b => b.Scheduled) == 82,
                "the NHL's 82 games run October to April"
            );
        }

        [Test]
        public void BestGame()
        {
            SeasonState season = Start(Shl[1].Id);
            Check(!SeasonRecap.Build(season, Shl).BestGame.HasValue, "no best game before a game");
            _ = Play(season, 1, 3);
            _ = Play(season, 2, 3);
            Check(
                SeasonRecap.Build(season, Shl).BestGame.Is(g => g.Margin == -1 && g.For == 2),
                "without a win the best game is the narrowest defeat"
            );

            _ = Play(season, 3, 1);
            _ = Play(season, 6, 1);
            _ = Play(season, 7, 2);
            _ = Play(season, 2, 0);
            SeasonRecap recap = SeasonRecap.Build(season, Shl);
            Check(
                recap.BestGame.Is(g => g.For == 7 && g.Against == 2 && !g.Playoff),
                "the biggest win is the best game, more goals breaking a tie (7-2 over 6-1)"
            );
            Fixture fifth = season.PlayedBy(season.UserClub).ElementAt(4);
            bool home = Same(fifth.Home, season.UserClub);
            string opponent = home ? fifth.Away : fifth.Home;
            Check(
                recap.BestGame.Is(g => Same(g.Opponent, opponent) && g.Home == home),
                "the best game names its opponent and where it was played"
            );
            Check(
                Same(
                    RecapText.BestGame(recap, Shl),
                    Text.Format(
                        home ? Text.Keys.RecapGameHome : Text.Keys.RecapGameAway,
                        7,
                        2,
                        NameOf(opponent)
                    )
                ),
                S($"the best game's line ({RecapText.BestGame(recap, Shl)})")
            );
        }

        [Test]
        public void TopScorer()
        {
            SeasonState season = Start(Shl[2].Id);
            string user = season.UserClub,
                other = Shl[3].Id;
            season.Stats.Regular.Players.Add(Stats(user, 10, "ALPHA", 10, 5));
            season.Stats.Playoffs.Players.Add(Stats(user, 10, "ALPHA", 2, 0));
            season.Stats.Regular.Players.Add(Stats(user, 22, "BRAVO", 6, 10));
            season.Stats.Regular.Players.Add(Stats(other, 9, "OTHER", 30, 30));
            PlayerStats goalie = Stats(user, 30, "GOALIE", 0, 20);
            goalie.Position = PlayingPosition.G;
            season.Stats.Regular.Players.Add(goalie);

            SeasonRecap recap = SeasonRecap.Build(season, Shl);
            Check(
                recap.TopScorer.Is(p => p.Number == 10 && p.Goals == 12 && p.Assists == 5),
                "the top scorer counts the regular season and the playoffs together (17 over 16) and only the club's skaters"
            );
            Check(
                Same(
                    RecapText.TopScorer(recap),
                    Text.Format(Text.Keys.RecapScorerLine, "#10 ALPHA", 17, 12, 5)
                ),
                S($"the scorer line ({RecapText.TopScorer(recap)})")
            );
            Check(
                !SeasonRecap.Build(Start(Shl[4].Id), Shl).TopScorer.HasValue,
                "no top scorer before anybody scored"
            );
        }

        private static PlayerStats Stats(
            string club,
            int number,
            string name,
            int goals,
            int assists
        )
        {
            return new PlayerStats
            {
                Club = club,
                Number = number,
                Name = name,
                Position = PlayingPosition.C,
                Games = 1,
                Goals = goals,
                Assists = assists,
            };
        }

        [Test]
        public void InjuriesAreLogged()
        {
            SeasonState season = Injured();
            string user = season.UserClub;
            Check(
                season.Stats.Injuries.Count == 3,
                S(
                    $"every injury of the season is logged, suspensions not ({season.Stats.Injuries.Count})"
                )
            );
            InjuryRecord first = season.Stats.Injuries[0];
            Check(
                Same(first.Name, "HURT")
                    && first.Number == 17
                    && first.Games == 4
                    && first.Cause == InjuryCause.Check
                    && Same(first.Game, "g900"),
                "the log keeps the player's name, the games and the cause"
            );
            SeasonRecap recap = SeasonRecap.Build(season, Shl);
            Check(
                recap.Injuries.Count == 2
                    && recap.Injuries.All(i => Same(i.Club, user))
                    && recap.GamesLost == 10
                    && recap.LongestInjury.Is(i => i.Number == 8),
                S(
                    $"the recap counts the club's own injuries: {recap.Injuries.Count}, {recap.GamesLost} games"
                )
            );
            Check(
                Same(RecapText.Injuries(recap), Text.Format(Text.Keys.RecapInjuryMany, 2, 10))
                    && Same(
                        RecapText.Injuries(SeasonRecap.Build(Start(Shl[6].Id), Shl)),
                        Text.Get(Text.Keys.RecapInjuryNone)
                    ),
                "the injuries line"
            );
        }

        [Test]
        public void InjuryLogSaveAndLoad()
        {
            SeasonState season = Injured();
            string path = Path.Combine(folder, "season.json");
            SeasonSave.Write(season, path);
            Check(
                SeasonSave
                    .Read(path)
                    .Is(s =>
                        s.Stats.Injuries.Count == 3
                        && s.Stats.Injuries[2].Number == 8
                        && s.Stats.Injuries[2].Games == 6
                        && Same(s.Stats.Injuries[2].Club, season.UserClub)
                        && Same(s.Stats.Injuries[0].Name, "HURT")
                    ),
                "the injury log reads back from the save"
            );
            string saved = File.ReadAllText(path);
            Check(
                saved.Contains("\"injuries\": [", StringComparison.Ordinal),
                "the save holds the log"
            );
            File.WriteAllText(
                path,
                saved.Replace("\"injuries\": [", "\"oldUnused\": [", StringComparison.Ordinal)
            );
            Check(
                SeasonSave.Read(path).Is(s => s.Stats.Injuries.Count == 0),
                "a save from before the log reads with none"
            );
        }

        /// <summary>A season whose stats credited two sheets: the human's club two injuries (4 and 6 games) and a
        /// suspension, the other club one injury.</summary>
        private SeasonState Injured()
        {
            SeasonState season = Start(Shl[0].Id);
            string user = season.UserClub,
                other = Shl[5].Id;
            MatchStatSheet sheet = new() { Home = user, Away = other };
            sheet.Lines.Add(
                new MatchStatLine
                {
                    Club = user,
                    Number = 17,
                    Name = "HURT",
                    Played = true,
                }
            );
            sheet.Absences.Add(Absence(user, 17, AbsenceReason.Injury, 4));
            sheet.Absences.Add(Absence(user, 21, AbsenceReason.Suspension, 2));
            sheet.Absences.Add(Absence(other, 5, AbsenceReason.Injury, 3));
            Check(season.Stats.Credit("g900", sheet), "the sheet is credited");
            MatchStatSheet later = new() { Home = other, Away = user };
            later.Absences.Add(Absence(user, 8, AbsenceReason.Injury, 6));
            _ = season.Stats.Credit("g901", later);
            return season;
        }

        private static Absence Absence(string club, int number, AbsenceReason reason, int games)
        {
            return new Absence
            {
                Club = club,
                Number = number,
                Reason = reason,
                Cause = reason == AbsenceReason.Injury ? InjuryCause.Check : InjuryCause.None,
                Games = games,
                GamesLeft = games,
            };
        }

        [Test]
        public void FiredSeason()
        {
            string club = Shl.First(c => BoardExpectation.Rank(Shl, c.Id) == 1).Id;
            SeasonState season = Start(club);
            season.Rules = RuleSet.Shl with { HotSeat = HotSeat.Strict };
            int played = 0;
            while (!season.Fired && Play(season, 1, 4))
            {
                played++;
            }

            Check(season.Fired, S($"losing every game gets the human fired ({played} games)"));
            SeasonRecap recap = SeasonRecap.Build(season, Shl);
            Check(recap.Ending == SeasonEnding.Fired, "the season ended with the sack");
            Check(
                recap.Verdict.Is(v => v == BoardVerdict.Missed)
                    && recap.Goal.Is(g => g == BoardGoal.WinLeague),
                "the board's verdict: the goal missed"
            );
            Check(
                Same(
                    RecapText.Verdict(recap),
                    Text.Format(Text.Keys.RecapVerdictMissed, Text.Get(Text.Keys.BoardGoalWin))
                ),
                S($"the verdict line ({RecapText.Verdict(recap)})")
            );
            Check(
                Kinds(recap)[0] == HeadlineKind.Fired
                    && Kinds(recap).Contains(HeadlineKind.LosingStreak)
                    && !Kinds(recap).Contains(HeadlineKind.RegularSeasonWinner),
                S($"the sack leads the headlines, the losing run follows ({Listed(recap)})")
            );
            Check(
                recap.Blocks.Any(b => b.Played < b.Scheduled),
                "the months after the sack have games never played"
            );
        }

        [Test]
        public void RecapEndsTheSeason()
        {
            string club = Shl.First(c => BoardExpectation.Rank(Shl, c.Id) == 1).Id;
            SeasonState season = Start(club);
            season.Rules = RuleSet.Shl with { HotSeat = HotSeat.Strict };
            int played = 0;
            while (!season.Fired && Play(season, 0, 3))
            {
                played++;
            }

            FiredScreen fired = new(app);
            app.Screens.Push(fired);
            Check(
                Same(fired.Items()[0].Label, Text.Get(Text.Keys.SeasonRecap)),
                S($"the fired screen offers the season recap first (after {played} games)")
            );
            fired.Items()[0].Pick();
            Check(
                app.Screens.Top.Is(t => t is SeasonRecapScreen) && app.Screens.Screens.Count == 1,
                "the recap takes the fired screen's place"
            );
            IReadOnlyList<MenuItem> items = new SeasonRecapScreen(app).Items();
            Check(
                items.Count == 2
                    && Same(items[0].Label, Text.Get(Text.Keys.NewSeason))
                    && Same(items[1].Label, Text.Get(Text.Keys.MainMenu)),
                "the recap ends the season: new season or the main menu"
            );
        }

        /// <summary>One season settled on paper to the end of the playoffs and the playout, the human then put on
        /// each club in turn: every way a season ends gets its leading headline.</summary>
        [Test]
        public void EndingsLeadTheHeadlines()
        {
            SeasonState season = Start(Shl[0].Id);
            int simmed = 0;
            while (app.Season.SimCurrentGame().HasValue)
            {
                simmed++;
            }

            Check(
                season.Playoffs.TryGet(out PlayoffState playoffs) && playoffs.IsDone,
                S($"the paper season reaches a champion and settles the playout ({simmed} games)")
            );
            if (!season.Playoffs.TryGet(out playoffs))
            {
                return;
            }

            _ = season.Stats.CatchUp(season, Shl);
            foreach ((string club, SeasonEnding ending, HeadlineKind lead) in Endings(playoffs))
            {
                season.UserClub = club;
                playoffs.UserClub = club;
                CheckEnding(season, playoffs, club, ending, lead);
            }

            season.UserClub = playoffs.Champion.Or("");
            playoffs.UserClub = season.UserClub;
            Check(
                Kinds(SeasonRecap.Build(season, Shl)).Contains(HeadlineKind.RegularSeasonWinner)
                    == (BoardExpectation.Place(season) == 1),
                "topping the table makes a headline"
            );
            Check(
                Same(
                    RecapText.Headline(new RecapHeadline(HeadlineKind.Champions, "X"), League.Nhl),
                    Text.Format(Text.Keys.HeadChampionsNhl, "X", "", 0, 0)
                ),
                "an NHL title is the Stanley Cup"
            );
        }

        /// <summary>A club for each way the season ended, with how it ended and the headline that leads.</summary>
        private static List<(string Club, SeasonEnding Ending, HeadlineKind Lead)> Endings(
            PlayoffState playoffs
        )
        {
            string champion = playoffs.Champion.Or("");
            PlayoffSeries final = playoffs.Rounds[^1].Series[0];
            PlayoffSeries playout = playoffs.Playout.Or(new PlayoffSeries());
            string relegated = playoffs.Relegated.Or("");
            return
            [
                (champion, SeasonEnding.Champions, HeadlineKind.Champions),
                (Loser(final), SeasonEnding.RunnersUp, HeadlineKind.RunnersUp),
                (
                    Loser(playoffs.Rounds[^2].Series[0]),
                    SeasonEnding.Semifinal,
                    HeadlineKind.Semifinal
                ),
                (
                    Loser(playoffs.Rounds[^3].Series[0]),
                    SeasonEnding.SeasonOver,
                    HeadlineKind.PlayoffExit
                ),
                (
                    playoffs.Seeds[PlayoffState.LastPlayIn], // 11th: no playoffs, no playout
                    SeasonEnding.SeasonOver,
                    HeadlineKind.MissedPlayoffs
                ),
                (Loser(playout), SeasonEnding.Relegated, HeadlineKind.Relegated),
                (
                    Same(playout.High, relegated) ? playout.Low : playout.High,
                    SeasonEnding.SeasonOver,
                    HeadlineKind.StayedUp
                ),
            ];
        }

        private static string Loser(PlayoffSeries series)
        {
            return Same(series.Winner.Or(""), series.High) ? series.Low : series.High;
        }

        private void CheckEnding(
            SeasonState season,
            PlayoffState playoffs,
            string club,
            SeasonEnding ending,
            HeadlineKind lead
        )
        {
            SeasonRecap recap = SeasonRecap.Build(season, Shl);
            Check(
                recap.Ending == ending && SeasonRecap.EndingOf(season) == ending,
                S($"{club} ends as {ending} ({recap.Ending})")
            );
            Check(
                recap.Headlines.Count is > 0 and <= SeasonRecap.MaxHeadlines
                    && recap.Headlines[0].Kind == lead
                    && Same(recap.Headlines[0].Name, NameOf(club)),
                S($"{club}'s recap leads with {lead} ({Listed(recap)})")
            );
            Check(
                recap.Place == BoardExpectation.Place(season)
                    && recap.Verdict.Is(v => BoardExpectation.Verdict(season).Is(w => w == v)),
                S($"{club}'s place and the board's verdict are the board's own")
            );
            int playoffGames = playoffs
                .Rounds.SelectMany(r => r.Series)
                .Concat(playoffs.Playout.TryGet(out PlayoffSeries p) ? [p] : [])
                .Where(s => s.Involves(club))
                .Sum(s => s.Games.Count);
            Check(
                recap.Games.Where(g => g.Playoff).Take(playoffGames + 1).Count() == playoffGames,
                S($"{club}'s {playoffGames} playoff and playout games count in the recap")
            );
            Check(
                recap
                    .Headlines.Select(h => RecapText.Headline(h, recap.League))
                    .All(text => text.Length > 0 && !text.Contains('{', StringComparison.Ordinal)),
                S($"{club}'s headlines read")
            );
        }

        /// <summary>The headlines picked from what happened: a win streak, a hat-trick, the derby, a young player's
        /// breakthrough, a trade and the coach's challenges; the ending leads and at most four print.</summary>
        [Test]
        public void HeadlinesFromWhatHappened()
        {
            (string a, string b) = Rivals.Pairs.First(p =>
                Shl.Any(c => Same(c.Id, p.A)) && Shl.Any(c => Same(c.Id, p.B))
            );
            SeasonState season = Start(a);
            SeasonRecap quiet = SeasonRecap.Build(season, Shl);
            Check(
                quiet.Headlines.Count == 1
                    && quiet.Headlines[0].Kind == HeadlineKind.MissedPlayoffs,
                S($"a season with nothing in it has only its ending ({Listed(quiet)})")
            );

            int played = 0;
            while (played < 6 && Play(season, 4, 1))
            {
                played++;
            }

            RecapHeadline run = SeasonRecap
                .Build(season, Shl)
                .Candidates.First(h => h.Kind == HeadlineKind.WinStreak);
            Check(run.Number == 6, S($"six wins in a row make a headline ({run.Number})"));

            (int won, int lost) = WinDerby(season, a, b);
            ClubData club = Shl.First(c => Same(c.Id, a));
            PlayerData young = club
                .Roster.Where(p => p.Age is > 0 and <= DevelopmentTable.YoungAge)
                .OrderBy(p => p.Age)
                .First();
            season.Stats.Development.Players.Add(
                new PlayerDevelopment
                {
                    Club = a,
                    Number = young.Number,
                    Value = DevelopmentTable.MaxGain,
                }
            );
            PlayerData scorer = club.Roster.First(p => p.Position == PlayingPosition.C);
            season.Stats.Awards.Add(Hat("g0", (a, b), a, scorer));
            season.Trades.Offers.Add(
                new TradeOffer
                {
                    Club = b,
                    GiveName = "OUTGOING",
                    GetName = "INCOMING",
                    Answer = TradeAnswer.Accepted,
                }
            );
            season.Challenges.Add(new SeasonChallenge { Outcome = ChallengeOutcome.Met });
            season.Challenges.Add(new SeasonChallenge { Outcome = ChallengeOutcome.Met });
            season.Challenges.Add(new SeasonChallenge { Outcome = ChallengeOutcome.Missed });

            SeasonRecap recap = SeasonRecap.Build(season, Shl);
            CheckPicked(recap.Candidates, b, young, scorer, won, lost);
            Check(
                recap.Headlines.Count == SeasonRecap.MaxHeadlines
                    && recap.Headlines[0].Kind == HeadlineKind.MissedPlayoffs
                    && recap.Headlines[1].Kind == HeadlineKind.WinStreak
                    && recap.Headlines[2].Kind == HeadlineKind.HatTrick
                    && recap.Candidates.Count > SeasonRecap.MaxHeadlines,
                S($"the ending leads and four print, in order ({Listed(recap)})")
            );

            season.Stats.Awards.Add(Hat("g9", (b, a), a, scorer));
            RecapHeadline hats = SeasonRecap
                .Build(season, Shl)
                .Candidates.First(h => h.Kind == HeadlineKind.HatTrick);
            Check(
                hats.Number == 2
                    && Same(
                        RecapText.Headline(hats, League.Shl),
                        Text.Format(Text.Keys.HeadHatTricks, scorer.LastName, NameOf(b), 2, 0)
                    ),
                "two hat-tricks make the hat-trick king"
            );
        }

        /// <summary>A player of the game award for three goals, to a player of the club the game names first of the two.</summary>
        private static PlayerOfGameAward Hat(
            string game,
            (string Home, string Away) clubs,
            string club,
            PlayerData p
        )
        {
            return new PlayerOfGameAward(
                game,
                clubs.Home,
                clubs.Away,
                5,
                2,
                club,
                p.Number,
                p.LastName,
                3,
                1,
                0
            );
        }

        /// <summary>Wins the club's next game against its rival, and gives the record against it so far.</summary>
        private static (int Won, int Lost) WinDerby(SeasonState season, string a, string b)
        {
            Fixture derby = season.Games.First(g => !g.Played && g.Involves(a) && g.Involves(b));
            season.Record(
                derby.Number,
                Same(derby.Home, a)
                    ? new FixtureResult(5, 2, ResultKind.Regulation)
                    : new FixtureResult(2, 5, ResultKind.Regulation)
            );
            int won = 0,
                lost = 0;
            foreach (Fixture g in season.PlayedBy(a).Where(g => g.Involves(b)))
            {
                FixtureResult r = g.Result.Or(new FixtureResult(0, 0, ResultKind.Regulation));
                bool home = Same(g.Home, a);
                if ((home ? r.HomeGoals : r.AwayGoals) > (home ? r.AwayGoals : r.HomeGoals))
                {
                    won++;
                }
                else
                {
                    lost++;
                }
            }

            return (won, lost);
        }

        private void CheckPicked(
            IReadOnlyList<RecapHeadline> all,
            string b,
            PlayerData young,
            PlayerData scorer,
            int won,
            int lost
        )
        {
            RecapHeadline hat = all.First(h => h.Kind == HeadlineKind.HatTrick);
            Check(
                Same(hat.Name, scorer.LastName) && Same(hat.Other, NameOf(b)) && hat.Number == 1,
                S($"the hat-trick names the scorer and the opponent ({hat.Name}, {hat.Other})")
            );
            RecapHeadline rival = all.First(h =>
                h.Kind is HeadlineKind.DerbyWins or HeadlineKind.DerbyLosses
            );
            Check(
                rival.Kind == (won > lost ? HeadlineKind.DerbyWins : HeadlineKind.DerbyLosses)
                    && Same(rival.Other, NameOf(b))
                    && rival.Number == won
                    && rival.Second == lost,
                S(
                    $"the derby against {b}: {rival.Kind} {rival.Number}-{rival.Second} ({won}-{lost})"
                )
            );
            RecapHeadline breakthrough = all.First(h => h.Kind == HeadlineKind.Breakthrough);
            Check(
                Same(breakthrough.Name, young.LastName) && breakthrough.Number == young.Age,
                S(
                    $"the breakthrough names the young player and his age ({breakthrough.Name}, {breakthrough.Number})"
                )
            );
            RecapHeadline trade = all.First(h => h.Kind == HeadlineKind.Trade);
            Check(
                Same(trade.Name, "INCOMING") && Same(trade.Other, "OUTGOING"),
                "the trade names the player in and the player out"
            );
            RecapHeadline challenges = all.First(h => h.Kind == HeadlineKind.Challenges);
            Check(
                challenges.Number == 2 && challenges.Second == 3,
                S($"two of three challenges met ({challenges.Number} of {challenges.Second})")
            );
        }
    }
}
