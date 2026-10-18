using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Goalie.Dev
{
    /// <summary>SAP_SCENE=goalie: the away goalie in the +x net faces a loop of shots from the slot and the
    /// circles, aimed anywhere on the net, with a tally of saves, rebounds and goals. The camera sits
    /// over that end. SAP_GOALIE_REFLEXES=0..100 overrides the goalie's reflexes to compare; the top line
    /// shows all of the goalie's traits.</summary>
    public sealed class GoalieScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "goalie";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            PlayerData data =
                world.Setup.Away.Roster.FirstOrDefault(p => p.Position == PlayingPosition.G)
                ?? new PlayerData { Position = PlayingPosition.G };
            if (
                int.TryParse(
                    Env.Get("SAP_GOALIE_REFLEXES"),
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int reflexes
                )
            )
            {
                data = data with { Traits = data.Traits with { GoalieReflexes = reflexes } };
            }

            Player g = world.AddPlayer(Side.Away, data, new Vector3(RinkSpec.GoalLineX - 1f, 0, 0));
            g.Heading = -MathF.PI / 2;
            // a home skater on the bench, clear of the puck, so the goals have someone to credit and play the horn
            PlayerData skater =
                world.Setup.Home.Roster.FirstOrDefault(p => p.Position != PlayingPosition.G)
                ?? new PlayerData { Position = PlayingPosition.C };
            world.AddPlayer(Side.Home, skater, Vector3.Zero).OnIce = false;
            world.Puck.Position = new Vector3(14, 0, 0);
            world.Dev.Goalie.Set(new GoalieDemo());
            return world;
        }
    }
}
