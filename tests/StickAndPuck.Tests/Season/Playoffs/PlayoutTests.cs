using System.Globalization;
using System.Text.Json.Nodes;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Playoffs.Dev;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season.Playoffs
{
    /// <summary>
    /// The SHL playout (W76): 13th against 14th, best of seven beside the playoffs, the human's club playing
    /// its games or the series simmed, the loser recorded as relegated, no playout in the NHL or with the rule
    /// off, and the playout kept in the save (an older save loads without one).
    /// </summary>
    public sealed class PlayoutTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-playouttest-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        private string SavePath => Path.Combine(folder, "season.json");

        [After(Test)]
        public void EndSessions()
        {
            app.Season.End();
            app.Playoffs.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        private static T Must<T>(Maybe<T> maybe)
            where T : notnull
        {
            return maybe.TryGet(out T value)
                ? value
                : throw new InvalidOperationException("expected a value");
        }

        private static FixtureResult Win(bool home)
        {
            return home
                ? new FixtureResult(3, 1, ResultKind.Regulation)
                : new FixtureResult(1, 3, ResultKind.Regulation);
        }

        private SeasonState FinishedSeason(int seed)
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            _ = app.Season.StartNew(clubs, clubs[0].Id, seed, SavePath);
            _ = PlayoffScenes.SettleSeason(app);
            return Must(app.Season.Current);
        }

        [Test]
        public void PairsThirteenthAndFourteenth()
        {
            SeasonState season = FinishedSeason(2026);
            List<string> table = [.. season.Table.Select(r => r.ClubId)];
            Check(table.Count == 14, "the SHL test league has 14 clubs");
            PlayoffState p = PlayoffState.New(season);
            PlayoffSeries playout = Must(p.Playout);
            Check(
                playout is { HighSeed: 13, LowSeed: 14, BestOf: 7 }
                    && Same(playout.High, table[12])
                    && Same(playout.Low, table[13]),
                "the playout is 13th against 14th, best of seven"
            );
            Check(
                Same(playout.NextHome, table[12]) && playout.HomeIce == p.HomeIce,
                "the 13th placed club opens at home, with the season's playoff home ice"
            );
            Check(
                !p.Rounds[0].Series.Any(s => s.Involves(table[12]) || s.Involves(table[13])),
                "the playout clubs are not in the playoffs"
            );
            Check(!p.Relegated.HasValue && !p.IsDone, "nobody is relegated before a game");

            season.Rules = RuleSet.Shl with { PlayoffHomeIce = PlayoffHomeIce.TwoTwoOneOneOne };
            Check(
                Must(PlayoffState.New(season).Playout).HomeIce == PlayoffHomeIce.TwoTwoOneOneOne,
                "the playout follows the preset's home ice order"
            );
        }

        [Test]
        public void BestOfSeven()
        {
            PlayoffSeries s = new()
            {
                HighSeed = 13,
                LowSeed = 14,
                High = "a",
                Low = "b",
                BestOf = 7,
            };
            bool open = true;
            foreach (string winner in new[] { "b", "a", "b", "a", "b", "a" })
            {
                s.Games.Add(new PlayoffGame(s.NextHome, s.NextAway, Win(Same(s.NextHome, winner))));
                open &= !s.IsOver;
            }
            Check(open, "the playout is still open at 3-3");
            s.Games.Add(new PlayoffGame(s.NextHome, s.NextAway, Win(Same(s.NextHome, "b"))));
            PlayoffState p = new() { Playout = s, UserClub = "c" };
            Check(
                s.IsOver && Same(s.Winner.Or(""), "b") && p.Relegated == "a",
                "four wins keep a club up and the loser is relegated"
            );
        }

        [Test]
        public void HumanPlaysThePlayoutAndGoesDown()
        {
            SeasonState season = FinishedSeason(77);
            string user = season.Table[13].ClubId;
            season.UserClub = user;
            NextMatch next = app.Playoffs.NextSetup();
            PlayoffState p = Must(app.Playoffs.Current);
            PlayoffSeries playout = Must(p.Playout);
            Check(
                next is NextMatch.Play play
                    && app.Playoffs.Playing.Is(s => s == playout)
                    && Same(play.Setup.Away.Id, user)
                    && p.UserInPlayout
                    && !p.UserQualified,
                "the 14th placed human plays the playout, game 1 away at the 13th"
            );
            Check(
                p.Locate(playout) == (PlayoffState.PlayoutRound, 0) && p.UserTitle.Length > 0,
                "the playout has its own stats round and title"
            );

            bool pace = true;
            for (int i = 0; i < 4 && app.Playoffs.Playing.TryGet(out PlayoffSeries s); i++)
            {
                Check(s == playout, "every game the human plays is a playout game");
                _ = app.Playoffs.Finish(Win(!Same(s.NextHome, user)));
                pace &=
                    playout.IsOver
                    || p.Current.Series.All(o => o.IsOver || o.Games.Count <= playout.Games.Count);
                _ = app.Playoffs.NextSetup();
            }

            Check(pace, "the playoffs are simmed beside the playout, never ahead of it");
            Check(
                playout.IsOver && p.UserRelegated && season.Relegated == user,
                "swept, the human's club is relegated, readable on the season"
            );
            Check(
                p.IsDone && p.Champion.HasValue && app.Playoffs.NextSetup() is not NextMatch.Play,
                "once the human is down the playoffs play to a champion and nothing is left"
            );
        }

        [Test]
        public void HumanWinsThePlayout()
        {
            SeasonState season = FinishedSeason(5);
            string user = season.Table[12].ClubId;
            season.UserClub = user;
            _ = app.Playoffs.NextSetup();
            PlayoffState p = Must(app.Playoffs.Current);
            for (int i = 0; i < 4 && app.Playoffs.Playing.TryGet(out PlayoffSeries s); i++)
            {
                _ = app.Playoffs.Finish(Win(Same(s.NextHome, user)));
                _ = app.Playoffs.NextSetup();
            }

            Check(
                !p.UserRelegated
                    && p.Relegated == season.Table[13].ClubId
                    && p.UserInPlayout
                    && p.IsDone,
                "the human sweeps the playout, the 14th goes down and the season ends"
            );
        }

        [Test]
        public void SimmedWhenTheHumanIsElsewhere()
        {
            SeasonState season = FinishedSeason(11);
            season.UserClub = season.Table[11].ClubId; // 12th: no playoffs, no playout
            Check(app.Playoffs.NextSetup() is not NextMatch.Play, "a 12th placed club has no game");
            PlayoffState p = Must(app.Playoffs.Current);
            PlayoffSeries playout = Must(p.Playout);
            Check(
                playout.IsOver
                    && playout.Games.Count is >= 4 and <= 7
                    && p.Relegated.Is(r => Same(r, playout.High) || Same(r, playout.Low))
                    && p.IsDone,
                "the playout is simmed to a relegated club alongside the playoffs"
            );

            app.Season.End();
            app.Playoffs.End();
            SeasonState again = FinishedSeason(11);
            again.UserClub = again.Table[0].ClubId; // the top seed: its series sets the pace
            _ = app.Playoffs.NextSetup();
            PlayoffSeries open = Must(Must(app.Playoffs.Current).Playout);
            Check(
                open.Games.Count == 3 || open.IsOver,
                "with the human in the playoffs the playout keeps pace with the play-in's games"
            );
        }

        [Test]
        public void NoPlayoutInTheNhlOrWithTheRuleOff()
        {
            IReadOnlyList<ClubData> nhl = Clubs.Load(League.Nhl);
            SeasonState season = SeasonState.New(nhl, nhl[0].Id, 3);
            _ = season.SimulateUpTo(season.Rounds, nhl);
            PlayoffState p = PlayoffState.New(season);
            Check(!p.Playout.HasValue && !season.Relegated.HasValue, "the NHL has no relegation");

            Check(
                RuleSet.Shl.Playout && !RuleSet.Nhl.Playout && !RuleSet.Arcade.Playout,
                "the playout is on in the SHL preset, off in the NHL and arcade presets"
            );
            SeasonState shl = FinishedSeason(9);
            shl.Rules = RuleSet.Shl with { Playout = false };
            Check(!PlayoffState.New(shl).Playout.HasValue, "the rule off means no playout");
        }

        [Test]
        public void SavedWithTheSeason()
        {
            SeasonState season = FinishedSeason(21);
            season.UserClub = season.Table[12].ClubId;
            _ = app.Playoffs.NextSetup();
            PlayoffSeries playout = Must(Must(app.Playoffs.Current).Playout);
            _ = app.Playoffs.Finish(Win(home: true));
            SeasonSave.Write(season, SavePath);

            SeasonState read = Must(SeasonSave.Read(SavePath));
            PlayoffSeries back = Must(Must(read.Playoffs).Playout);
            Check(
                back.HighSeed == 13
                    && back.LowSeed == 14
                    && back.Games.Count == playout.Games.Count
                    && back.Games.Count == 1
                    && Same(back.High, playout.High),
                "the playout and its games come back from the save"
            );

            // a save from before the playout: the playoffs without the key at all
            JsonObject root = (JsonObject)JsonNode.Parse(File.ReadAllText(SavePath));
            JsonObject playoffs = (JsonObject)root["playoffs"];
            Check(playoffs.Remove("playout"), "the save names the playout");
            string old = Path.Combine(folder, "old.json");
            File.WriteAllText(old, root.ToJsonString());
            Check(
                SeasonSave
                    .Read(old)
                    .Is(s =>
                        s.Playoffs.Is(x =>
                            !x.Playout.HasValue && !x.Relegated.HasValue && x.Rounds.Count > 0
                        )
                    ),
                "an older save loads, without a playout"
            );
        }
    }
}
