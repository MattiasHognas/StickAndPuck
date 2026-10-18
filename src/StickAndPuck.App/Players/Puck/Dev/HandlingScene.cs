using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Puck.Dev
{
    /// <summary>Test setup, SAP_SCENE=handling: the human carries the puck at centre ice with two
    /// teammates up ice on the wings and one back on defence, against an away defender in front of
    /// the away net. Arrows skate, space (or left mouse, or gamepad A) shoots: tap for a wrist shot,
    /// hold for a slap shot. E (or right mouse, or gamepad X) passes toward the way the stick points,
    /// and control follows the pass.</summary>
    public sealed class HandlingScene : IScene
    {
        /// <inheritdoc/>
        public string Name => "handling";

        /// <inheritdoc/>
        public World Create(IReadOnlyList<ClubData> clubs, Settings settings, AppState app)
        {
            World world = new(Scenes.DefaultSetup(clubs), settings);
            List<PlayerData> home = Skaters(world.Setup.Home);
            List<PlayerData> away = Skaters(world.Setup.Away);

            Player carrier = Add(world, Side.Home, home[0], new Vector3(-4, 0, 1), MathF.PI / 2);
            _ = Add(world, Side.Home, home[1 % home.Count], new Vector3(8, 0, -6), MathF.PI / 2);
            _ = Add(world, Side.Home, home[2 % home.Count], new Vector3(10, 0, 6), MathF.PI / 2);
            _ = Add(world, Side.Home, home[3 % home.Count], new Vector3(-12, 0, 0), MathF.PI / 2);
            _ = Add(world, Side.Away, away[0], new Vector3(18, 0, -1.5f), -MathF.PI / 2);
            if (
                world.Setup.Away.Roster.FirstOrDefault(p => p.Position == PlayingPosition.G) is
                { } goalie
            )
            {
                _ = Add(
                    world,
                    Side.Away,
                    goalie,
                    new Vector3(RinkSpec.GoalLineX - 1f, 0, 0),
                    -MathF.PI / 2
                );
            }

            carrier.Slot = 0;
            world.Puck.Carrier = carrier.Id;
            world.Puck.Position = PuckHandling.Blade(carrier);
            return world;
        }

        private static List<PlayerData> Skaters(ClubData club)
        {
            List<PlayerData> list = [.. club.Roster.Where(p => p.Position != PlayingPosition.G)];
            return list.Count > 0 ? list : [new PlayerData { Position = PlayingPosition.C }];
        }

        private static Player Add(
            World world,
            Side side,
            PlayerData data,
            Vector3 at,
            float heading
        )
        {
            Player p = world.AddPlayer(side, data, at);
            p.Heading = heading;
            return p;
        }
    }
}
