namespace StickAndPuck.App.Goalie
{
    /// <summary>What one goalie is doing between steps.</summary>
    internal sealed class GoalieState
    {
        /// <summary>Gets or sets when the current shot was first seen, or NaN.</summary>
        public float SeenAt { get; set; } = float.NaN;

        /// <summary>Gets or sets how far from the net the current shot was first seen, or NaN.</summary>
        public float ShotFrom { get; set; } = float.NaN;

        /// <summary>Gets or sets how far off the goalie reads where the current shot will cross, in metres along z.</summary>
        public float Misread { get; set; }

        /// <summary>Gets or sets the z the goalie pushes to for the current shot, or NaN.</summary>
        public float ReactZ { get; set; } = float.NaN;

        /// <summary>Gets a value indicating whether the goalie is pushing toward a shot.</summary>
        public bool Reacting => !float.IsNaN(ReactZ);

        /// <summary>Gets or sets when the goalie dropped into the butterfly, or NaN when standing.</summary>
        public float DownSince { get; set; } = float.NaN;

        /// <summary>Gets a value indicating whether the goalie is down in the butterfly.</summary>
        public bool Down { get; private set; }

        /// <summary>Gets or sets when the goalie caught the puck it holds.</summary>
        public float HeldSince { get; set; }

        /// <summary>Gets or sets the time before which the goalie ignores the puck.</summary>
        public float IgnoreUntil { get; set; }

        /// <summary>Gets or sets a value indicating whether the goalie is out of the crease going for a loose puck.</summary>
        public bool Playing { get; set; }

        /// <summary>Gets or sets when the goalie went for the loose puck.</summary>
        public float PlaySince { get; set; }

        /// <summary>Gets or sets a value indicating whether the goalie has already decided about the current loose puck.</summary>
        public bool Rolled { get; set; }

        /// <summary>Gets or sets a value indicating whether the goalie is away from its spot and skating back.</summary>
        public bool OutOfCrease { get; set; }

        /// <summary>Gets or sets a value indicating whether the last puck the goalie played out went to a teammate.</summary>
        public bool PassedOut { get; set; }

        /// <summary>Gets the glove (+1) or blocker (-1) save pose being shown, or 0.</summary>
        public int HandPose { get; private set; }

        /// <summary>Gets when the hand save pose ends.</summary>
        public float HandPoseUntil { get; private set; }

        /// <summary>Shows a glove or blocker save for a moment.</summary>
        public void Pose(float now, int side, int glove)
        {
            HandPose = side == glove ? 1 : -1;
            HandPoseUntil = now + GoalieTuning.HandPoseSeconds;
        }

        /// <summary>Drops into the butterfly, or stays down longer.</summary>
        public void Drop(float now)
        {
            if (!Down)
            {
                DownSince = now;
            }

            Down = true;
            Until = now + GoalieTuning.ButterflySeconds;
        }

        /// <summary>Gets when the goalie gets back up.</summary>
        public float Until { get; private set; }

        /// <summary>Stands back up once the butterfly time is over.</summary>
        public void Rise(float now)
        {
            if (Down && now >= Until)
            {
                Down = false;
                DownSince = float.NaN;
                ReactZ = float.NaN;
            }
        }
    }
}
