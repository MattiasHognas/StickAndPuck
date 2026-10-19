using System.Globalization;
using StickAndPuck.App.Core;
using StickAndPuck.App.Season.Arcade;
using StickAndPuck.App.Season.Playoffs;
using StickAndPuck.App.Season.Schedule;

namespace StickAndPuck.App.Season.Stats
{
    /// <summary>Follows every match with a <see cref="MatchStatTracker"/> and, at MatchEnd, hands the
    /// sheet to whatever the match belongs to: the season (regular or playoff game), the arcade run,
    /// or just <see cref="AppState.LastMatchStats"/> for exhibition and versus. Runs before
    /// <see cref="ScheduleSystem"/>, <see cref="PlayoffSystem"/> and <see cref="ArcadeSystem"/>, so
    /// the game being played is still known and the autosave that follows carries the lines; the
    /// games settled on paper by those systems are credited on the next step with
    /// <see cref="SeasonStats.CatchUp"/>.</summary>
    internal sealed class StatsSystem : ISystem
    {
        private readonly MatchStatTracker tracker = new();
        private bool finished;
        private bool catchUp = true;

        public bool RunsWhilePaused => true;

        public void Update(World world, float dt)
        {
            if (!tracker.World.TryGet(out World followed) || !ReferenceEquals(world, followed))
            {
                finished = false;
                catchUp = true;
            }
            if (catchUp)
            {
                CatchUp(world);
            }
            if (finished)
            {
                return;
            }

            tracker.Observe(world);
            if (!world.Events.Of<MatchEnded>().Any())
            {
                return;
            }

            finished = true;
            catchUp = true;
            FinishAndCredit(world);
        }

        private void CatchUp(World world)
        {
            catchUp = false;
            if (
                world.App.Season.Current.TryGet(out SeasonState s)
                && s.Stats.CatchUp(s, world.App.Season.Clubs) > 0
            )
            {
                world.App.Season.Unsaved = true;
            }
        }

        private void FinishAndCredit(World world)
        {
            MatchStatSheet sheet = tracker.Finish();
            world.App.LastMatchStats = sheet;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: stats: {sheet.Home} {sheet.HomeGoals}-{sheet.AwayGoals} {sheet.Away}, player of the game {PlayerOfGame(sheet)}"
                )
            );

            if (
                world.App.Season.Current.TryGet(out SeasonState season)
                && world.App.Season.Playing.TryGet(out Fixture game)
                && Matches(world, game.Home, game.Away)
            )
            {
                string key = SeasonStats.GameKey(game.Number);
                if (season.Stats.Credit(key, sheet))
                {
                    season.Stats.LastPlayed = sheet;
                    season.Stats.LastPlayedKey = key;
                }
            }
            else if (
                world.App.Season.Current.TryGet(out SeasonState current)
                && current.Playoffs.TryGet(out PlayoffState playoffs)
                && world.App.Playoffs.Playing.TryGet(out PlayoffSeries series)
                && Matches(world, series.NextHome, series.NextAway)
            )
            {
                int round = playoffs.Rounds.Count - 1;
                int index = playoffs.Current.Series.IndexOf(series);
                if (index >= 0)
                {
                    string key = SeasonStats.PlayoffKey(round, index, series.Games.Count);
                    if (current.Stats.Credit(key, sheet))
                    {
                        current.Stats.LastPlayed = sheet;
                        current.Stats.LastPlayedKey = key;
                    }
                }
            }
            else if (
                world.App.Arcade.Current.TryGet(out ArcadeRun run)
                && world.App.Arcade.Playing.TryGet(out MatchSetup playing)
                && Matches(world, playing.Home.Id, playing.Away.Id)
            )
            {
                run.Credit(sheet);
            }
        }

        private static string PlayerOfGame(MatchStatSheet sheet)
        {
            if (!sheet.PlayerOfGame.TryGet(out MatchStatLine p))
            {
                return "nobody";
            }

            string performance = p.IsGoalie
                ? string.Create(CultureInfo.InvariantCulture, $"{p.Saves} saves")
                : string.Create(CultureInfo.InvariantCulture, $"{p.Goals}G {p.Assists}A");
            return $"{p.Label} ({p.Club}) {performance}";
        }

        private static bool Matches(World world, string home, string away)
        {
            return string.Equals(home, world.Setup.Home.Id, StringComparison.Ordinal)
                && string.Equals(away, world.Setup.Away.Id, StringComparison.Ordinal);
        }
    }
}
