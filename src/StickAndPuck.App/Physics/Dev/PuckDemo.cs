using System.Collections.Immutable;
using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Physics.Dev
{
    /// <summary>The shot loop behind <see cref="PuckScene"/>: a new shot whenever the puck settles or a few seconds pass.</summary>
    internal sealed class PuckDemo
    {
        private static readonly ImmutableArray<(Vector3 From, Vector3 Velocity)> Shots =
        [
            (new Vector3(-6, 0, 5), new Vector3(8, 0, -18)), // off the far side boards, in view of today's fixed camera
            (new Vector3(10, 0, -4), new Vector3(22, 0, 14)), // rim round the +x corner
            (new Vector3(14, 0, 3), new Vector3(30, 1.2f, -0.6f)), // off the far post
            (new Vector3(15, 0, -2), new Vector3(28, 2.2f, 1.6f)), // into the net
            (new Vector3(0, 0, 4), new Vector3(16, 7f, 22)), // high off the glass
            (new Vector3(-12, 0, 6), new Vector3(-25, 0, -5)), // dump in behind the -x net
        ];

        private int next;
        private float shotAt = float.NegativeInfinity;

        /// <summary>Starts the next shot when the puck is done with the last one.</summary>
        public static void Update(World world)
        {
            if (!world.Dev.Puck.TryGet(out PuckDemo d) || world.Puck.Carrier >= 0)
            {
                return;
            }

            float since = world.Time - d.shotAt;
            if (since < 4.5f && (since < 1.2f || world.Puck.Velocity.Length() > 0.5f))
            {
                return;
            }

            (Vector3 from, Vector3 velocity) = Shots[d.next++ % Shots.Length];
            world.Puck.Position = from;
            world.Puck.Velocity = velocity;
            d.shotAt = world.Time;
        }
    }
}
