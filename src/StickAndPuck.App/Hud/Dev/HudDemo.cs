using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Hud.Dev
{
    /// <summary>Publishes the hud scene's one event on the first step.</summary>
    internal static class HudDemo
    {
        /// <summary>Publishes the chosen event on the first step.</summary>
        public static void Update(World world)
        {
            string message = world.Switches.HudMessage;
            if (message.Length == 0 || world.Tick != 0)
            {
                return;
            }

            int scorer = world.OnIce(Side.Home).First(p => !p.IsGoalie).Id;
            int other = world.OnIce(Side.Away).First(p => !p.IsGoalie).Id;
            Vector3 at = world.Puck.Position;
            if (
                string.Equals(message, "hattrick", StringComparison.Ordinal)
                || string.Equals(message, "gwg", StringComparison.Ordinal)
            )
            {
                // the earlier goals of the match, so the tally knows them: two by the scorer, or home, away, home
                bool hat = string.Equals(message, "hattrick", StringComparison.Ordinal);
                world.Events.Publish(new GoalScored(world.Time, Side.Home, scorer, at, 0));
                world.Events.Publish(
                    hat
                        ? new GoalScored(world.Time, Side.Home, scorer, at, 0)
                        : new GoalScored(world.Time, Side.Away, other, at, 0)
                );
                world.Events.Publish(new GoalScored(world.Time, Side.Home, scorer, at, 0));
                if (hat)
                {
                    world.Match.Score[0] = 3;
                }

                if (!hat)
                {
                    world.Events.Publish(new MatchWon(world.Time, Side.Home));
                }

                return;
            }

            Maybe<MatchEvent> e = message switch
            {
                "goal" or "firstgoal" => new GoalScored(world.Time, Side.Home, scorer, at, 0),
                "shutout" or "final" => new MatchWon(world.Time, Side.Home),
                "period" => new PeriodEnded(world.Time, 2),
                "tie" => new MatchTied(world.Time),
                "offside" => new OffsideCalled(world.Time, Side.Home, at),
                "icing" => new IcingCalled(world.Time, Side.Away, other, at),
                _ => Maybe.None<MatchEvent>(),
            };
            if (e.TryGet(out MatchEvent ev))
            {
                world.Events.Publish(ev);
            }
        }
    }
}
