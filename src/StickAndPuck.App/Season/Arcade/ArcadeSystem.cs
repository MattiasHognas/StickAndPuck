using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Arcade
{
    /// <summary>When an arcade game ends (MatchEnd), records its result in <see cref="ArcadeSession"/>,
    /// which saves the run at once. A few seconds after the final a stand-in panel shows: the run so far
    /// and the lives left, or the results summary when the run is over (without the results screen).</summary>
    internal sealed class ArcadeSystem : ISystem, IDisposable
    {
        /// <summary>Seconds after the final before the panel shows.</summary>
        private const float PanelDelay = 4;

        private Maybe<World> watching;
        private bool sawShootout;
        private float finishedAt = float.PositiveInfinity;
        private Maybe<HudArt> art;

        public bool RunsWhilePaused => true;

        public void Start(World world)
        {
            if (world.App.Arcade.Current.HasValue)
            {
                art = HudArt.Load();
            }
        }

        public void Update(World world, float dt)
        {
            ResetWatchingState(world);
            if (world.Match.Phase == MatchPhase.Shootout)
            {
                sawShootout = true;
            }

            if (!IsArcadeMatch(world))
            {
                return;
            }

            if (!world.Events.Of<MatchEnded>().Any())
            {
                return;
            }

            if (
                !TryFinishArcadeRun(
                    world,
                    out ArcadeGame game,
                    out ArcadeRun run,
                    out ResultKind kind
                )
            )
            {
                return;
            }

            finishedAt = world.Time;
            Console.WriteLine(CreateArcadeResultLog(world, game, run, kind));
        }

        private void ResetWatchingState(World world)
        {
            if (!watching.TryGet(out World seen) || !ReferenceEquals(world, seen))
            {
                watching = world;
                sawShootout = false;
                finishedAt = float.PositiveInfinity;
            }
        }

        private static bool IsArcadeMatch(World world)
        {
            return world.App.Arcade.Playing.TryGet(out MatchSetup playing)
                && string.Equals(playing.Home.Id, world.Setup.Home.Id, StringComparison.Ordinal)
                && string.Equals(playing.Away.Id, world.Setup.Away.Id, StringComparison.Ordinal);
        }

        private bool TryFinishArcadeRun(
            World world,
            out ArcadeGame game,
            out ArcadeRun run,
            out ResultKind kind
        )
        {
            game = default;
            run = default;

            MatchState m = world.Match;
            kind = sawShootout
                ? ResultKind.Shootout
                : ((m.Period > world.Setup.Periods) ? ResultKind.Overtime : ResultKind.Regulation);

            return world
                    .App.Arcade.Finish(
                        new FixtureResult(m.Score[(int)Side.Home], m.Score[(int)Side.Away], kind)
                    )
                    .TryGet(out game) && world.App.Arcade.Current.TryGet(out run);
        }

        private static string CreateArcadeResultLog(
            World world,
            ArcadeGame game,
            ArcadeRun run,
            ResultKind kind
        )
        {
            string outcome = run.Champion
                ? "champions, every club beaten"
                : (
                    run.Lost
                        ? string.Create(
                            CultureInfo.InvariantCulture,
                            $"run over after {run.Stage} win(s)"
                        )
                        : string.Create(
                            CultureInfo.InvariantCulture,
                            $"{run.LivesLeft} live(s) left, next {run.NextOpponent.Or("")}"
                        )
                );

            return string.Create(
                CultureInfo.InvariantCulture,
                $"SAP: arcade stage {game.Stage + 1}: {(game.Won ? "won" : "lost")} {game.GoalsFor}-{game.GoalsAgainst} against {game.Opponent} ({kind}); {outcome}; saved to {world.App.Arcade.SavePath}"
            );
        }

        public void DrawHud(World world, RenderContext render)
        {
            if (!art.TryGet(out HudArt hud) || !world.App.Arcade.Current.TryGet(out ArcadeRun run))
            {
                return;
            }

            if (world.Switches.ArcadeSummary || world.Time - finishedAt >= PanelDelay)
            {
                ArcadeSummary.Draw(hud, run, world.App.Arcade.Clubs);
            }
        }

        public void Dispose()
        {
            if (art.TryGet(out HudArt loaded))
            {
                loaded.Unload();
            }

            art = Maybe.None<HudArt>();
        }
    }
}
