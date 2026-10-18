using System.Collections.Immutable;
using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Players.Puck.Dev
{
    /// <summary>The script behind <see cref="ShotDemoScene"/>, driven from <see cref="PuckHandlingSystem"/>.</summary>
    internal sealed class ShotDemo
    {
        /// <summary>Steps shoot is held in each round: a tap, a half hold, a full wind-up.</summary>
        private static readonly ImmutableArray<int> Holds = [2, 30, 66];

        private Maybe<Player> shooter;
        private float start;
        private int round;

        /// <summary>Gets the hold, in steps, of the current round.</summary>
        private int Hold => Holds[round % Holds.Length];

        /// <summary>Adds the shooter.</summary>
        public static void Setup(World world)
        {
            PlayerData data =
                world.Setup.Home.Roster.FirstOrDefault(p => p.Position != PlayingPosition.G)
                ?? new PlayerData { Position = PlayingPosition.C };
            ShotDemo d = new()
            {
                shooter = world.AddPlayer(Side.Home, data, Vector3.Zero),
                round = -1,
            };
            world.Dev.Shot.Set(d);
            d.Reset(world);
        }

        /// <summary>Writes this step's commands.</summary>
        public static void Update(World world)
        {
            if (!world.Dev.Shot.TryGet(out ShotDemo d) || !d.shooter.TryGet(out Player p))
            {
                return;
            }

            int step = (int)
                MathF.Round((world.Time - d.start) / Game.Step, MidpointRounding.ToEven);
            const int press = 30;
            p.Command = new PlayerCommand { Shoot = step >= press && step < press + d.Hold }; // standing still, shooting the way they face
            if (step > press + d.Hold + 120)
            {
                d.Reset(world);
            }
        }

        private void Reset(World world)
        {
            round++;
            if (!shooter.TryGet(out Player p))
            {
                return;
            }

            p.Position = new Vector3(RinkSpec.GoalLineX - 10f, 0, round % 2 == 0 ? 2f : -2f);
            p.Velocity = Vector3.Zero;
            p.Heading = MathF.PI / 2;
            p.Command = default;
            p.Begin(PlayerAction.None);
            Core.Puck pk = world.Puck;
            pk.Carrier = p.Id;
            pk.Position = PuckHandling.Blade(p);
            pk.Velocity = Vector3.Zero;
            pk.Live = false; // a dead puck scores no goal, so no replay cuts in between shots
            start = world.Time;
        }
    }
}
