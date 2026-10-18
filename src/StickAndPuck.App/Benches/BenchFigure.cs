using StickAndPuck.App.Core;

namespace StickAndPuck.App.Benches
{
    /// <summary>One figure on a bench: the player, where along the bench he is, which way he faces and what he is doing.</summary>
    public sealed class BenchFigure(Player player, float x, float phase)
    {
        /// <summary>Gets the player.</summary>
        public Player Player { get; } = player;

        /// <summary>Gets a value indicating whether the figure sits in his club's penalty box rather than on its bench.</summary>
        public bool InBox { get; init; }

        /// <summary>Gets the figure's spot along the bench (x).</summary>
        public float X { get; internal set; } = x;

        /// <summary>Gets the heading in radians, as <see cref="Player.Heading"/>: 0 faces the ice.</summary>
        public float Heading { get; internal set; }

        /// <summary>Gets what the figure is doing.</summary>
        public BenchPose Pose { get; internal set; }

        /// <summary>Gets the figure's own offset into its clip, in seconds, so the bench does not move as one.</summary>
        public float Phase { get; } = phase;

        /// <summary>Gets the game time (<see cref="World.Time"/>) until which a skater just back from a shift sits tired.</summary>
        public float TiredUntil { get; internal set; }

        /// <summary>Gets how quickly this figure turns after the puck (per second).</summary>
        internal float TurnRate { get; init; } = 3f;

        /// <summary>Gets a value indicating whether the figure is walking along the bench this step.</summary>
        public bool Walking { get; internal set; }

        /// <summary>Gets a value indicating whether the figure stands (up for a change, walking or up for a reaction).</summary>
        public bool Standing => Pose != BenchPose.Watch || Walking || Up;

        /// <summary>Gets the reaction he has over his pose (see <see cref="BenchReactions"/>), shown from <see cref="ReactFrom"/> to <see cref="ReactUntil"/>.</summary>
        public BenchReaction Reaction { get; internal set; }

        /// <summary>Gets the game time the reaction shows from, a moment after its cause so the bench does not move as one.</summary>
        public float ReactFrom { get; internal set; }

        /// <summary>Gets the game time the reaction ends.</summary>
        public float ReactUntil { get; internal set; }

        /// <summary>Gets the heading a reaction turns him to (NaN: he keeps following the puck).</summary>
        public float Look { get; internal set; } = float.NaN;

        /// <summary>Gets a value indicating whether his helmet is off, until he next goes on.</summary>
        public bool Bare { get; internal set; }

        /// <summary>Gets the energy he came off the ice with (1 for a figure seated from the start).</summary>
        public float Spent { get; internal set; } = 1f;

        /// <summary>Gets a value indicating whether he sat down from a shift this step.</summary>
        public bool SatDown { get; internal set; }

        /// <summary>Gets a value indicating whether a reaction stands him up this step.</summary>
        internal bool Up { get; set; }

        /// <summary>Gets the reaction showing at a game time.</summary>
        public BenchReaction Showing(float time)
        {
            return time >= ReactFrom && time < ReactUntil ? Reaction : BenchReaction.None;
        }
    }
}
