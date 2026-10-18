using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.Players.Checks.Dev
{
    /// <summary>The script behind <see cref="ChecksScene"/> and <see cref="ChecksDemoScene"/>, driven from <see cref="Switching.SwitchingSystem"/> so its commands land before skating.</summary>
    internal sealed class ChecksDemo(ChecksCast cast, bool scripted)
    {
        private float start,
            lostAt = float.PositiveInfinity;

        /// <summary>Adds the players.</summary>
        /// <param name="world">The world to fill.</param>
        /// <param name="script">True to play the check by itself; false to give the hunter to the human.</param>
        public static void Setup(World world, bool script)
        {
            List<PlayerData> home = Skaters(world.Setup.Home);
            List<PlayerData> away = Skaters(world.Setup.Away);
            // the hunter is the club's best checker, the carrier the away club's weakest, so the hit shows
            Player hunter = world.AddPlayer(
                Side.Home,
                home.MaxBy(p => p.Traits.Checking),
                Vector3.Zero
            );
            Player mate1 = world.AddPlayer(
                Side.Home,
                home[0] == hunter.Data ? home[1 % home.Count] : home[0],
                Vector3.Zero
            );
            Player mate2 = world.AddPlayer(Side.Home, home[2 % home.Count], Vector3.Zero);
            Player carrier = world.AddPlayer(
                Side.Away,
                away.MinBy(p => p.Traits.Checking),
                Vector3.Zero
            );
            ChecksCast c = new(carrier, hunter, mate1, mate2);
            ChecksDemo demo = new(c, script);
            world.Dev.Checks.Set(demo);
            if (!script)
            {
                hunter.Slot = 0;
            }

            demo.Reset(world, c);
        }

        private static List<PlayerData> Skaters(ClubData club)
        {
            List<PlayerData> list = [.. club.Roster.Where(p => p.Position != PlayingPosition.G)];
            return list.Count > 0 ? list : [new PlayerData { Position = PlayingPosition.C }];
        }

        private void Reset(World world, ChecksCast c)
        {
            Place(c.Carrier, new Vector3(4, 0, 1), -MathF.PI / 2);
            Place(c.Hunter, new Vector3(7.5f, 0, 1.5f), -MathF.PI / 2);
            Place(c.Mate1, new Vector3(-6, 0, -5), MathF.PI / 2);
            Place(c.Mate2, new Vector3(-8, 0, 5), MathF.PI / 2);
            c.Carrier.Velocity = new Vector3(-3, 0, 0);
            c.Hunter.Velocity = new Vector3(-5, 0, 0);
            Core.Puck pk = world.Puck;
            pk.Carrier = c.Carrier.Id;
            pk.Position = PuckHandling.Blade(c.Carrier);
            pk.Velocity = Vector3.Zero;
            pk.Live = true;
            start = world.Time;
            lostAt = float.PositiveInfinity;
        }

        private static void Place(Player p, Vector3 at, float heading)
        {
            p.Position = at;
            p.Velocity = Vector3.Zero;
            p.Heading = heading;
            p.Command = default;
            p.Begin(PlayerAction.None);
        }

        /// <summary>Writes this step's commands for the scripted players.</summary>
        public static void Update(World world)
        {
            if (world.Dev.Checks.TryGet(out ChecksDemo demo))
            {
                demo.Run(world);
            }
        }

        private void Run(World world)
        {
            ChecksCast c = cast;
            (Player carrier, Player hunter) = (c.Carrier, c.Hunter);
            float t = world.Time - start;

            // the carrier circles centre ice while they have the puck
            if (carrier.Slot < 0 && world.Puck.Carrier == carrier.Id)
            {
                float a = t * 0.6f;
                carrier.Command = new PlayerCommand
                {
                    Move = new Vector2(-SimMath.Cos(a), -SimMath.Sin(a)) * 0.6f,
                };
            }
            else if (carrier.Slot < 0)
            {
                carrier.Command = default;
            }

            if (scripted)
            {
                Vector3 to = carrier.Position - hunter.Position;
                bool press =
                    hunter.Action == PlayerAction.None
                    && world.Puck.Carrier == carrier.Id
                    && to.Length() < 1.4f;
                hunter.Command =
                    world.Puck.Carrier == carrier.Id
                        ? new PlayerCommand
                        {
                            Move = Vector2.Normalize(new Vector2(to.X, to.Z)),
                            Sprint = true,
                            Check = press,
                        }
                        : new PlayerCommand { Move = ToPuck(world, hunter) };
                if (t > 4.5f)
                {
                    Reset(world, c);
                }
            }
            else
            {
                // a few seconds after the carrier loses the puck, start a new chase
                if (world.Puck.Carrier == carrier.Id)
                {
                    lostAt = float.PositiveInfinity;
                }
                else if (float.IsPositiveInfinity(lostAt))
                {
                    lostAt = world.Time;
                }

                if (world.Time - lostAt > 4f)
                {
                    Reset(world, c);
                }
            }
        }

        private static Vector2 ToPuck(World world, Player p)
        {
            Vector3 d = world.Puck.Position - p.Position;
            Vector2 v = new(d.X, d.Z);
            return v.Length() > 0.3f ? Vector2.Normalize(v) * 0.6f : Vector2.Zero;
        }
    }
}
