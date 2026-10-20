using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Rules.Match;

namespace StickAndPuck.App.Rules.Penalties.Dev
{
    /// <summary>The script behind <see cref="TrapezoidScene"/>, standing in for a human goalie: a moment into each spell of
    /// play the puck lies loose in the home club's far corner behind its goal line, outside the trapezoid, and the
    /// home goalie skates out from the post and plays it there.</summary>
    internal sealed class TrapezoidDemo
    {
        /// <summary>Seconds into a spell of play before the puck goes into the corner.</summary>
        private const float CornerAfter = 1.5f;

        /// <summary>The goalie's skate to the puck, metres per second.</summary>
        private const float Skate = 3f;

        private float stageAt = float.NegativeInfinity;
        private int stage;
        private Vector3 spot;
        private Vector3 skater;

        /// <summary>Runs the script while play runs, after the referee has judged this step.</summary>
        public static void Update(World world)
        {
            if (world.Dev.Trapezoid.TryGet(out TrapezoidDemo demo))
            {
                demo.Run(world);
            }
        }

        private void Run(World world)
        {
            if (
                world.Match.Phase != MatchPhase.Play
                || world.OnIce(Side.Home).FirstOrDefault(p => p.IsGoalie) is not { } goalie
            )
            {
                stage = 0;
                stageAt = world.Time;
                return;
            }

            int end = FaceoffFormation.DefendedEnd(world.Match, Side.Home);
            Puck pk = world.Puck;
            if (stage == 0 && world.Time - stageAt >= CornerAfter)
            {
                // the far corner, a step and a half behind the goal line and well wide of the trapezoid
                spot = new Vector3(end * (RinkSpec.GoalLineX + 1.6f), 0, -5.2f);
                pk.Carrier = -1;
                pk.Position = spot;
                pk.Velocity = Vector3.Zero;
                skater = new Vector3(end * (RinkSpec.GoalLineX + 0.4f), 0, -1.2f); // out from the far post
                goalie.Position = skater;
                foreach (Player p in world.OnIce(Side.Away).Where(p => !p.IsGoalie))
                {
                    p.Position = p.Position with { X = -end * 4f }; // the forecheck is away up the ice
                }

                stage = 1;
            }
            else if (stage == 1)
            {
                pk.Carrier = -1;
                pk.Position = spot;
                pk.Velocity = Vector3.Zero;
                // the script's own skate, whatever the goalie system wanted this step
                Vector3 to = spot - (Vector3.Normalize(spot - skater) * 0.5f);
                Vector3 step = to - skater;
                float reach = Skate * Game.Step;
                skater = step.Length() <= reach ? to : skater + (Vector3.Normalize(step) * reach);
                goalie.Position = skater;
                goalie.Velocity = Vector3.Zero;
                if (step.Length() <= reach)
                {
                    pk.LastTouch = goalie.Id; // he plays it: the rule calls it next step
                    pk.Velocity = new Vector3(-end * 3f, 0, 0);
                    stage = 2;
                }
            }
        }
    }
}
