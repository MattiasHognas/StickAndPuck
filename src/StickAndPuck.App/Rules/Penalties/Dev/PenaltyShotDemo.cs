using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players.Puck;

namespace StickAndPuck.App.Rules.Penalties.Dev
{
    /// <summary>The scripted breakaway behind <see cref="PenaltyShotScene"/>: a moment into each spell of play the away
    /// centre breaks out over the red line with the puck, every home skater behind him, the nearest one a stride back;
    /// a moment later that one trips him from behind.</summary>
    internal sealed class PenaltyShotDemo
    {
        /// <summary>Seconds into a spell of play before the breakaway.</summary>
        private const float BreakAfter = 1.5f;

        /// <summary>Seconds of the breakaway before the trip.</summary>
        private const float TripAfter = 0.6f;

        /// <summary>The breakaway's speed, metres per second.</summary>
        private const float Speed = 7.5f;

        private float stageAt = float.NegativeInfinity;
        private int stage;
        private int carrierId = -1;
        private int chaserId = -1;

        /// <summary>Runs the script while play runs, after the referee has judged this step's fouls.</summary>
        public static void Update(World world)
        {
            if (world.Dev.PenaltyShot.TryGet(out PenaltyShotDemo demo))
            {
                demo.Run(world);
            }
        }

        private void Run(World world)
        {
            if (world.Match.Phase != MatchPhase.Play)
            {
                stage = 0;
                stageAt = world.Time;
                return;
            }

            float since = world.Time - stageAt;
            if (stage == 0 && since >= BreakAfter)
            {
                stage = Break(world) ? 1 : 2;
                stageAt = world.Time;
            }
            else if (stage == 1)
            {
                Skate(world);
                if (since >= TripAfter)
                {
                    Trip(world);
                    stage = 2;
                }
            }
        }

        /// <summary>Sets up the breakaway: the away centre with the puck just over the red line, the home skaters behind him.</summary>
        private bool Break(World world)
        {
            if (
                world.OnIce(Side.Away).FirstOrDefault(p => p.Data.Position == PlayingPosition.C)
                is not { } carrier
            )
            {
                return false;
            }

            int attack = PuckHandling.AttackEnd(world, Side.Away);
            carrierId = carrier.Id;
            carrier.Position = new Vector3(attack * 0.5f, 0, 0);
            List<Player> home = [.. world.OnIce(Side.Home).Where(p => !p.IsGoalie)];
            for (int i = 0; i < home.Count; i++)
            {
                Player p = home[i];
                // the first a stride behind on his back; the rest further back across the ice
                p.Position =
                    i == 0
                        ? new Vector3(-attack * 1.2f, 0, 0.4f)
                        : new Vector3(-attack * (4f + i), 0, (i - 2.5f) * 3f);
                p.Velocity = new Vector3(attack * Speed * (i == 0 ? 1f : 0.6f), 0, 0);
                p.Heading = attack > 0 ? MathF.PI / 2 : -MathF.PI / 2;
            }

            chaserId = home.Count > 0 ? home[0].Id : -1;
            Puck pk = world.Puck;
            pk.Carrier = carrier.Id;
            pk.LastTouch = carrier.Id;
            Skate(world);
            pk.Position = PuckHandling.Blade(carrier);
            return chaserId >= 0;
        }

        /// <summary>Keeps the breakaway going: the carrier and his chaser skate straight at the net.</summary>
        private void Skate(World world)
        {
            int attack = PuckHandling.AttackEnd(world, Side.Away);
            Vector3 run = new(attack * Speed, 0, 0);
            float heading = attack > 0 ? MathF.PI / 2 : -MathF.PI / 2;
            Player carrier = world.Players[carrierId];
            carrier.Velocity = run;
            carrier.Heading = heading;
            carrier.Command = default;
            if (chaserId >= 0)
            {
                Player chaser = world.Players[chaserId];
                chaser.Velocity = run;
                chaser.Heading = heading;
                chaser.Position = carrier.Position - new Vector3(attack * 1.2f, 0, -0.4f);
            }
        }

        /// <summary>The trip from behind: a penalty shot under SHL and NHL rules, a minor in Arcade.</summary>
        private void Trip(World world)
        {
            Player carrier = world.Players[carrierId];
            if (!PenaltyShot.Award(world, chaserId, Infraction.Tripping))
            {
                _ = PenaltyCalls.Signal(world, chaserId, Infraction.Tripping);
            }

            carrier.Begin(PlayerAction.Fallen);
        }
    }
}
