using System.Globalization;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Pregame;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Screens;
using StickAndPuck.App.Season.Schedule;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Pregame
{
    /// <summary>
    /// The pre-game stats card (W139): what it shows for a season game against an exhibition, and that it
    /// slides in with the arena flyover and is gone when the close-up ends, the flyover is skipped or
    /// the game starts.
    /// </summary>
    public sealed class TeamCardTests : CheckedTest
    {
        private readonly AppState app = new();

        private readonly string folder = Path.Combine(
            Path.GetTempPath(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"sap-teamcard-{Environment.ProcessId}-{Guid.NewGuid():N}"
            )
        );

        [After(Test)]
        public void EndSession()
        {
            app.Season.End();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Test]
        public void ExhibitionShowsRatingsOnly()
        {
            World w = new WorldBuilder().Human().Build();
            TeamCard card = TeamCard.For(w);
            Check(!card.Season, "an exhibition is no season game");
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                TeamCardSide t = card.Of(side);
                ClubData club = w.Setup.Club(side);
                Check(
                    string.Equals(t.ClubId, club.Id, StringComparison.Ordinal)
                        && t.Strengths == ClubStrengths.Of(club),
                    $"{side}: the club's own ratings and playstyle"
                );
                Check(
                    !t.Record.HasValue
                        && !t.Place.HasValue
                        && !t.Scorer.HasValue
                        && t.Form.Count == 0,
                    $"{side}: no record, place, form or scorer outside a season"
                );
            }
        }

        [Test]
        public void SeasonGameShowsTheSeason()
        {
            IReadOnlyList<ClubData> clubs = WorldBuilder.Clubs;
            string user = clubs[2].Id;
            SeasonState season = app.Season.StartNew(
                clubs,
                user,
                seed: 31,
                path: Path.Combine(folder, "season.json")
            );
            for (int i = 0; i < 7; i++)
            {
                Check(app.Season.SimCurrentGame().HasValue, "a season game is simmed");
            }

            if (app.Season.NextSetup() is not NextMatch.Play { Setup: var setup })
            {
                Check(ok: false, "the season has a next game");
                return;
            }

            World w = new(setup, new Settings()) { App = app };
            TeamCard card = TeamCard.For(w);
            Check(card.Season, "the human's season game fills the season rows");
            IReadOnlyList<StandingRow> table = season.Table;
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                TeamCardSide t = card.Of(side);
                int place = 1 + table.ToList().FindIndex(r => Eq(r.ClubId, t.ClubId));
                StandingRow row = table[place - 1];
                Check(
                    t.Record.Is(r =>
                        r.StartsWith(
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{row.Wins + row.OvertimeWins}-{row.Losses}-{row.OvertimeLosses} "
                            ),
                            StringComparison.Ordinal
                        )
                    ),
                    $"{side}: the record is wins-losses-overtime losses from the table"
                );
                Check(
                    t.Place.Is(p =>
                        p.StartsWith(
                            string.Create(CultureInfo.InvariantCulture, $"#{place} "),
                            StringComparison.Ordinal
                        )
                    ),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{side}: the place is the club's line in the table ({place})"
                    )
                );
                List<Fixture> last = [.. season.PlayedBy(t.ClubId).TakeLast(TeamCard.FormGames)];
                Check(
                    t.Form.Count == TeamCard.FormGames
                        && t.Form.Where(d => !Eq(d, "none")).Take(last.Count + 1).Count()
                            == last.Count
                        && t.Form.All(d => Dots.Contains(d, StringComparer.Ordinal)),
                    $"{side}: five form dots from the last results"
                );
                bool wonLast = last[^1]
                    .Result.Is(r =>
                        (
                            Eq(last[^1].Home, t.ClubId)
                                ? r.HomeGoals - r.AwayGoals
                                : r.AwayGoals - r.HomeGoals
                        ) > 0
                    );
                Check(
                    Eq(t.Form[^1], "win") == wonLast,
                    $"{side}: the newest dot is the last result"
                );
                bool scored = season.Stats.Regular.Players.Any(p =>
                    Eq(p.Club, t.ClubId) && !p.IsGoalie && p.Goals > 0
                );
                Check(
                    t.Scorer.HasValue == scored,
                    $"{side}: the top scorer once someone has scored"
                );
            }

            app.Season.End();
            Check(!TeamCard.For(w).Season, "after the season ends the card is an exhibition's");
        }

        [Test]
        public void FormLeadsWithNoneEarly()
        {
            SeasonState season = SeasonState.New(WorldBuilder.Clubs, WorldBuilder.Clubs[0].Id, 5);
            string club = WorldBuilder.Clubs[0].Id;
            Check(TeamCard.Form(season, club).Count == 0, "no games, no form");
            Fixture first = season.Games.First(g => g.Involves(club));
            bool home = Eq(first.Home, club);
            season.Games[first.Number] = first with
            {
                Result = new FixtureResult(home ? 2 : 3, home ? 3 : 2, ResultKind.Overtime),
            };
            Check(
                TeamCard
                    .Form(season, club)
                    .SequenceEqual(
                        ["none", "none", "none", "none", "otloss"],
                        StringComparer.Ordinal
                    ),
                "one overtime loss: four empty dots, then the loss"
            );
        }

        [Test]
        public void SlidesInWithTheFlyoverAndOut()
        {
            World w = new WorldBuilder().Human().Build();
            Run(w, Game.Step);
            Check(TeamCard.Covering(w), "the card has the screen as the flyover starts");
            Run(w, 2f);
            Check(TeamCard.Shown(w) >= 1, "two seconds in, the card is fully shown");
            Run(w, TeamCard.OutAt - 2f + TeamCard.SlideSeconds + 0.1f);
            Check(
                TeamCard.Shown(w) <= 0 && !TeamCard.Covering(w) && ArenaFlyover.IsPlaying(w),
                "the card is gone as the camera settles, before the flyover ends"
            );

            World skipped = new WorldBuilder().Human().Build();
            Run(skipped, 2f);
            Check(PregameFlow.Press(skipped), "start skips the flyover");
            Check(TeamCard.Covering(skipped), "the card slides out after the skip");
            Run(skipped, TeamCard.SlideSeconds + 0.1f);
            Check(
                TeamCard.Shown(skipped) <= 0 && !TeamCard.Covering(skipped),
                "and is gone once it has slid out"
            );

            World started = new WorldBuilder().Human().Build();
            Run(started, 2f);
            Check(PregameFlow.Start(started), "start is pressed during the card");
            Check(
                TeamCard.Shown(started) <= 0 && !TeamCard.Covering(started),
                "the card is gone once the game starts"
            );

            World ai = new WorldBuilder().Build();
            Run(ai, 2f);
            Check(!TeamCard.Covering(ai), "no warm-up, no card");
        }

        [Test]
        public void FlyoverCloseUpStaysInsideTheBoards()
        {
            // W138's close-up followed the human round the zone; near the near boards that put the camera
            // in the near stands. It now frames the home squad and keeps the camera inside the boards,
            // even with the whole squad skating along them.
            World w = new WorldBuilder().Human().Build();
            CameraSystem camera = new();
            camera.Start(w);
            float nearest = float.NegativeInfinity;
            for (int i = 0; i < ArenaFlyover.Seconds / Game.Step; i++)
            {
                Run(w, Game.Step);
                foreach (Player p in w.OnIce(Side.Home).Where(p => !p.IsGoalie))
                {
                    p.Position = p.Position with { Z = 10f }; // the squad skates along the near boards
                }

                camera.Update(w, Game.Step);
                if (
                    ArenaFlyover.Phase(w) == FlyoverPhase.CloseUp
                    && PregameFlow.Elapsed(w) > ArenaFlyover.AerialSeconds + 1.5f
                )
                {
                    nearest = Math.Max(nearest, w.Camera.Position.Z);
                }
            }

            Check(
                nearest is > 0 and < RinkSpec.HalfWidth,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the close-up camera stays inside the near boards (z up to {nearest:0.0} m)"
                )
            );
        }

        private static readonly string[] Dots = ["win", "loss", "otloss", "none"];

        private static bool Eq(string a, string b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        private static void Run(World world, float seconds)
        {
            ISystem[] systems =
            [
                new PositioningSystem(Seeds.Dice(28)),
                new PregameSystem(),
                new SkatingSystem(),
                new PuckPhysics(),
                new MatchRules(),
            ];
            for (int i = 0; i < seconds / Game.Step; i++)
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                world.Time += Game.Step;
                world.Tick++;
            }
        }
    }
}
