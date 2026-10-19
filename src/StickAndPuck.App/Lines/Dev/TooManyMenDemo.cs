using StickAndPuck.App.Core;

namespace StickAndPuck.App.Lines.Dev
{
    /// <summary>The scripted rushed change behind <see cref="TooManyMenScene"/>: a while into each spell of play the
    /// human presses change line, then again a moment later to rush it, and the first skater out of the door
    /// is handed the puck while the man he replaces is still out on the ice.</summary>
    internal sealed class TooManyMenDemo
    {
        /// <summary>Seconds into a spell of play before the first press.</summary>
        private const float FirstPress = 1.5f;

        /// <summary>Seconds after the first press before the second, the rush.</summary>
        private const float RushAfter = 0.4f;

        /// <summary>Seconds after the rush before the early skater gets the puck.</summary>
        private const float PuckAfter = 0.3f;

        private float stageAt = float.NegativeInfinity;
        private int stage;

        /// <summary>Runs the script, before the lines read the human's buttons.</summary>
        public static void Update(World world)
        {
            if (world.Dev.TooManyMen.TryGet(out TooManyMenDemo demo))
            {
                demo.Run(world);
            }
        }

        private void Run(World world)
        {
            if (!world.Controlled(0).TryGet(out Player me))
            {
                return;
            }

            if (world.Match.Phase != MatchPhase.Play)
            {
                stage = 0;
                stageAt = world.Time;
                return;
            }

            float since = world.Time - stageAt;
            bool press = stage switch
            {
                0 => since >= FirstPress,
                1 => since >= RushAfter,
                _ => false,
            };
            me.Command = me.Command with { ChangeLine = press };
            if (press)
            {
                stage++;
                stageAt = world.Time;
                return;
            }

            if (stage == 2 && since >= PuckAfter)
            {
                GivePuck(world);
                stage = 3;
            }
        }

        /// <summary>Hands the puck to the first skater who stepped out early.</summary>
        private static void GivePuck(World world)
        {
            if (
                !Bench.Board(world).Changing[(int)Side.Home].TryGet(out ShiftChange change)
                || change.Swaps.Find(s => s.Early && s.In.OnIce) is not { } swap
            )
            {
                return;
            }

            Puck pk = world.Puck;
            pk.Carrier = swap.In.Id;
            pk.LastTouch = swap.In.Id;
            pk.Position = swap.In.Position;
            pk.Velocity = swap.In.Velocity;
        }
    }
}
