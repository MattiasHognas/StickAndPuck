using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Hud;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Playoffs
{
    /// <summary>When a playoff game ends (MatchEnd), records its result in <see cref="PlayoffSession"/>
    /// and settles the other games up to the human's next one; <see cref="Save.AutosaveSystem"/> saves it
    /// next. Without the screens, the bracket shows a few seconds after a playoff game's final,
    /// and always in SAP_SCENE=playoffbracket.</summary>
    internal sealed class PlayoffSystem : ISystem, IDisposable
    {
        /// <summary>Seconds after the final before the stand-in bracket shows.</summary>
        private const float BracketDelay = 4;

        private Maybe<World> watching;
        private bool sawShootout;
        private float finishedAt = float.PositiveInfinity;
        private Maybe<HudArt> art;

        public bool RunsWhilePaused => true;

        public void Start(World world)
        {
            if (world.App.Playoffs.Current.HasValue)
            {
                art = HudArt.Load();
            }
        }

        public void Update(World world, float dt)
        {
            if (!watching.TryGet(out World seen) || !ReferenceEquals(world, seen))
            {
                watching = world;
                sawShootout = false;
                finishedAt = float.PositiveInfinity;
            }
            if (world.Match.Phase == MatchPhase.Shootout)
            {
                sawShootout = true;
            }

            if (
                !world.App.Playoffs.Playing.TryGet(out PlayoffSeries series)
                || !string.Equals(series.NextHome, world.Setup.Home.Id, StringComparison.Ordinal)
                || !string.Equals(series.NextAway, world.Setup.Away.Id, StringComparison.Ordinal)
            )
            {
                return;
            }

            if (!world.Events.Of<MatchEnded>().Any())
            {
                return;
            }

            MatchState m = world.Match;
            ResultKind kind = sawShootout
                ? ResultKind.Shootout
                : ((m.Period > world.Setup.Periods) ? ResultKind.Overtime : ResultKind.Regulation);
            FixtureResult result = new(m.Score[(int)Side.Home], m.Score[(int)Side.Away], kind);
            if (!world.App.Playoffs.Current.TryGet(out PlayoffState playoffs))
            {
                return;
            }

            string round = playoffs.Current.Name;
            int game = series.Games.Count + 1;
            if (!world.App.Playoffs.Finish(result))
            {
                return;
            }

            finishedAt = world.Time;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: playoff {round} game {game}: {world.Setup.Home.Id} {result.HomeGoals}-{result.AwayGoals} {world.Setup.Away.Id} ({kind}), series {series.HighWins}-{series.LowWins}"
                )
            );
            Log(playoffs);
        }

        public void DrawHud(World world, RenderContext render)
        {
            if (
                !art.TryGet(out HudArt hud)
                || !world.App.Playoffs.Current.TryGet(out PlayoffState playoffs)
            )
            {
                return;
            }

            if (world.Switches.PlayoffBracket || world.Time - finishedAt >= BracketDelay)
            {
                PlayoffBracket.Draw(hud, playoffs, world.App.Season.Clubs);
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

        /// <summary>Prints where the playoffs stand: the champion, or the human's next game.</summary>
        public static void Log(PlayoffState playoffs)
        {
            if (playoffs.IsOver)
            {
                Console.WriteLine(
                    $"SAP: {playoffs.Champion.Or("")} are {(playoffs.League == League.Nhl ? "NHL" : "SHL")} champions"
                );
            }
            else if (playoffs.UserSeries.TryGet(out PlayoffSeries s))
            {
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: next playoff game: {playoffs.Current.Name} game {s.Games.Count + 1}, {s.NextHome} vs {s.NextAway} (series {s.High} {s.HighWins}-{s.LowWins} {s.Low})"
                    )
                );
            }
        }
    }
}
