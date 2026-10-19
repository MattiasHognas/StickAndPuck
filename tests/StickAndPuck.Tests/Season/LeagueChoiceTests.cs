using System.Globalization;
using System.Text.Json.Nodes;
using StickAndPuck.App.Core;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Arcade;
using StickAndPuck.App.Season.Save;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.App.Versus;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Season
{
    /// <summary>
    /// League choice (W117): picking the NHL lists the NHL clubs and plays the NHL preset, and each league
    /// keeps its own season and arcade save, with a save from before league choice read as the SHL's.
    /// Saves go to a temporary folder.
    /// </summary>
    public sealed class LeagueChoiceTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-leaguetest-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        [Before(Test)]
        public void MakeFolder()
        {
            _ = Directory.CreateDirectory(folder);
        }

        [After(Test)]
        public void EndSessions()
        {
            app.Season.End();
            app.Arcade.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        [Test]
        public void PickingNhlGivesNhlClubsAndPreset()
        {
            ScreenFlow flow = app.Screens;
            Check(
                flow.League == League.Shl && flow.Clubs.Count == 14,
                $"the menus start in the SHL with its 14 clubs ({flow.League}, {flow.Clubs.Count})"
            );

            flow.PickLeague(League.Nhl);
            Check(
                flow.League == League.Nhl
                    && flow.Clubs.Count == 32
                    && flow.Clubs.All(c => c.League == League.Nhl),
                $"picking the NHL lists the 32 NHL clubs ({flow.Clubs.Count})"
            );
            Check(
                flow.Prefs.Rules.Resolve() == RuleSet.Nhl,
                $"picking the NHL picks the NHL rule preset ({flow.Prefs.Rules.Resolve().Name})"
            );

            IReadOnlyList<ClubData> picks = [.. flow.Clubs.Take(2)];
            MatchPlan exhibition = new(new ExhibitionMode(), picks);
            Check(
                RulePicks.For(app, exhibition) == RuleSet.Nhl
                    && RulePicks.For(app, new MatchPlan(new VersusMode(), picks)) == RuleSet.Nhl,
                "an exhibition and player vs player in the NHL play NHL rules"
            );
            Check(
                RulePicks.For(app, new MatchPlan(new ArcadeLivesMode(), [picks[0]]))
                    == RuleSet.Arcade,
                "an NHL arcade run keeps the Arcade preset"
            );

            MatchSetup setup = new ExhibitionMode()
                .Setup(new GameModeStart(flow.Clubs, picks, InputDevice.None, app))
                .Or(Scenes.DefaultSetup(WorldBuilder.Clubs));
            World world = new(setup, new Settings());
            Check(
                setup.League == League.Nhl && world.League == League.Nhl,
                "the match's league reads NHL from the setup and the world"
            );

            flow.PickLeague(League.Shl);
            Check(
                flow.Clubs.Count == 14
                    && flow.Clubs.All(c => c.League == League.Shl)
                    && flow.Prefs.Rules.Resolve() == RuleSet.Shl,
                "picking the SHL again lists its clubs and its preset"
            );
            Check(
                Leagues.Next(League.Shl, 1) == League.Nhl
                    && Leagues.Next(League.Shl, -1) == League.Nhl
                    && Leagues.Next(League.Nhl, 1) == League.Shl,
                "left and right step between the two leagues"
            );
        }

        [Test]
        public void SeasonSavesPerLeague()
        {
            string shlPath = Path.Combine(folder, "season.json");
            string nhlPath = Leagues.SavePath(League.Nhl, shlPath);
            Check(
                string.Equals(
                    Leagues.SavePath(League.Shl, shlPath),
                    shlPath,
                    StringComparison.Ordinal
                )
                    && string.Equals(
                        nhlPath,
                        Path.Combine(folder, "season_nhl.json"),
                        StringComparison.Ordinal
                    ),
                $"the SHL keeps season.json, the NHL saves beside it ({Path.GetFileName(nhlPath)})"
            );

            IReadOnlyList<ClubData> shl = Clubs.Load(League.Shl);
            IReadOnlyList<ClubData> nhl = Clubs.Load(League.Nhl);
            _ = app.Season.StartNew(shl, shl[0].Id, 3, shlPath);
            app.Season.End();
            SeasonState nhlSeason = app.Season.StartNew(nhl, "oilers", 5, nhlPath);
            Check(
                nhlSeason.League == League.Nhl
                    && nhlSeason.Clubs.Count == 32
                    && nhl.All(c => nhlSeason.Games.Any(g => g.Involves(c.Id))),
                $"an NHL season stores its league and schedules all 32 clubs ({nhlSeason.Games.Count} games)"
            );
            app.Season.End();

            SeasonSession fresh = new();
            Check(
                fresh
                    .Continue(shl, shlPath)
                    .Is(s => s.League == League.Shl && Same(s.UserClub, shl[0].Id))
                    && fresh
                        .Continue(nhl, nhlPath)
                        .Is(s => s.League == League.Nhl && Same(s.UserClub, "oilers")),
                "each league continues its own season; starting the NHL one left the SHL save alone"
            );
            fresh.End();

            JsonNode old = JsonNode.Parse(File.ReadAllText(shlPath)) ?? new JsonObject();
            _ = old.AsObject().Remove("league");
            string oldPath = Path.Combine(folder, "old.json");
            File.WriteAllText(oldPath, old.ToJsonString());
            Check(
                !File.ReadAllText(oldPath).Contains("\"league\"", StringComparison.Ordinal)
                    && SeasonSave
                        .Read(oldPath)
                        .Is(s => s.League == League.Shl && s.Games.Count > 0),
                "a season saved before league choice still reads, as an SHL season"
            );
        }

        [Test]
        public void ArcadeSavesPerLeague()
        {
            string shlPath = Path.Combine(folder, "arcade.json");
            string nhlPath = Leagues.SavePath(League.Nhl, shlPath);
            IReadOnlyList<ClubData> shl = Clubs.Load(League.Shl);
            IReadOnlyList<ClubData> nhl = Clubs.Load(League.Nhl);
            _ = app.Arcade.StartNew(shl, shl[0].Id, ArcadeRule.ThreeLives, 4, shlPath);
            app.Arcade.End();
            ArcadeRun nhlRun = app.Arcade.StartNew(
                nhl,
                "bruins",
                ArcadeRule.SuddenDeath,
                4,
                nhlPath
            );
            app.Arcade.End();
            Check(
                nhlRun.Opponents.Count == 31
                    && Same(Path.GetFileName(nhlPath), "arcade_nhl.json")
                    && ArcadeSession.CanContinue(shlPath)
                    && ArcadeSession.CanContinue(nhlPath),
                $"an NHL run faces the other 31 NHL clubs in arcade_nhl.json, beside the SHL run ({nhlRun.Opponents.Count})"
            );
            Check(
                ArcadeSave.Read(shlPath).Is(r => Same(r.UserClub, shl[0].Id))
                    && ArcadeSave.Read(nhlPath).Is(r => Same(r.UserClub, "bruins")),
                "each league keeps its own arcade run"
            );
        }

        [Test]
        public void NhlGridShowsNicknames()
        {
            Check(
                Same(TeamSelectScreen.Nickname("Toronto Maple Leafs"), "Maple Leafs")
                    && Same(TeamSelectScreen.Nickname("Tampa Bay Lightning"), "Lightning")
                    && Same(TeamSelectScreen.Nickname("St. Louis Blues"), "Blues")
                    && Same(TeamSelectScreen.Nickname("New York Rangers"), "Rangers")
                    && Same(TeamSelectScreen.Nickname("Utah Mammoth"), "Mammoth"),
                "the four-column NHL grid names clubs without their city"
            );
            Check(
                Clubs
                    .Load(League.Nhl)
                    .Select(c => TeamSelectScreen.Nickname(c.Name))
                    .Distinct(StringComparer.Ordinal)
                    .Take(33)
                    .Count() == 32,
                "every NHL nickname is distinct"
            );
        }
    }
}
