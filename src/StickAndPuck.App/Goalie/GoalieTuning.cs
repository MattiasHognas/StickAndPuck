namespace StickAndPuck.App.Goalie
{
    /// <summary>The numbers that decide how a goalie plays. Metres, seconds, metres per second. Where a pair
    /// is given, the first is a goalie with reflexes 0 and the second one with reflexes 100.</summary>
    public static class GoalieTuning
    {
        /// <summary>How far out from the goal line a goalie stands when the puck is close.</summary>
        public const float DepthNear = 0.35f;

        /// <summary>How far out a goalie comes to cut the angle when the puck is in the slot or beyond.</summary>
        public const float DepthFar = 1.05f;

        /// <summary>How far inside the post a goalie's centre stands when hugging it, with the puck behind the goal line.</summary>
        public const float PostHug = 0.3f;

        /// <summary>Crease shuffle speed while following the puck.</summary>
        public const float ShuffleSpeed = 3.2f;

        /// <summary>Time to see a shot and start moving: slow and fast reflexes.</summary>
        public const float ReactSlow = 0.28f,
            ReactFast = 0.11f;

        /// <summary>Speed of the push or slide toward a shot once reacting: slow and fast reflexes.</summary>
        public const float SaveSpeedSlow = 3.0f,
            SaveSpeedFast = 6.0f;

        /// <summary>How far, in metres, a goalie misreads where a shot of <see cref="ReadSpeed"/> will cross, one
        /// standard deviation: slow and fast reflexes. Faster shots are misread more, slower ones less.</summary>
        public const float ReadErrorSlow = 0.4f,
            ReadErrorFast = 0.08f;

        /// <summary>The shot speed the read error is given for.</summary>
        public const float ReadSpeed = 25f;

        /// <summary>A puck heading at the net faster than this is a shot the goalie reacts to.</summary>
        public const float ShotSpeed = 4f;

        /// <summary>How wide of the posts a shot may be headed and still draw a reaction.</summary>
        public const float ShotMargin = 0.4f;

        /// <summary>Half the width the body covers standing up, at any height up to the shoulders.</summary>
        public const float BodyHalfWidth = 0.38f;

        /// <summary>Half the width pads cover along the ice in the butterfly.</summary>
        public const float ButterflyHalfWidth = 0.6f;

        /// <summary>Extra reach of the glove or blocker at mid height.</summary>
        public const float HandReach = 0.27f;

        /// <summary>Shots below this height are low: pads and butterfly.</summary>
        public const float LowShot = 0.45f;

        /// <summary>Shots above this height go over the shoulders.</summary>
        public const float ShoulderHeight = 1.3f;

        /// <summary>Chance a covered shot is stopped: slow and fast reflexes. The rest squeeze through.</summary>
        public const float CleanSlow = 0.64f,
            CleanFast = 1.18f;

        /// <summary>How many of the misses <see cref="CleanSlow"/> and <see cref="CleanFast"/> leave squeeze through on a
        /// shot into the middle of the goalie, and on one at the very edge of the reach, as a share.</summary>
        public const float CentreMiss = 0.4f,
            EdgeMiss = 1.6f;

        /// <summary>Above this speed a save is a big one: rebounds are harder to hold and the bench bangs its sticks.</summary>
        public const float HardShot = 28f;

        /// <summary>Above this speed, which takes in a good wrist shot, a shot is harder to stop cleanly; the chance falls by up to <see cref="HardShotPenalty"/> at 45 m/s.</summary>
        public const float FastShot = 20f;

        /// <summary>The most a very hard shot takes off the clean-stop chance.</summary>
        public const float HardShotPenalty = 0.2f;

        /// <summary>Chance to catch rather than leave a rebound: slow and fast reflexes, before height and speed.</summary>
        public const float CatchSlow = 0.28f,
            CatchFast = 0.65f;

        /// <summary>Share of the shot speed a rebound keeps: big rebounds from slow reflexes, soft ones from fast.</summary>
        public const float ReboundSlow = 0.42f,
            ReboundFast = 0.22f;

        /// <summary>A loose puck slower than this, near a goalie, is smothered.</summary>
        public const float SmotherSpeed = 2.5f;

        /// <summary>How close a slow puck must be to be smothered.</summary>
        public const float SmotherReach = 0.6f;

        /// <summary>Seconds a goalie holds a frozen puck before playing it out, if no whistle comes.</summary>
        public const float HoldSeconds = 2.5f;

        /// <summary>Speed a goalie plays a held puck out to the corner.</summary>
        public const float PlayOutSpeed = 9f;

        /// <summary>Seconds after a save or release during which the goalie ignores the puck.</summary>
        public const float IgnoreAfterSave = 0.35f;

        /// <summary>Shots from closer than this, in metres, find a goalie who has squared up and fills more of the net.</summary>
        public const float CloseRange = 8f;

        /// <summary>How much further a goalie reaches against a shot from the doorstep (see <see cref="GoalieModel.CloseRead(float, int)"/>).</summary>
        public const float CloseBonus = 0.2f;

        /// <summary>Seconds a goalie stays down in the butterfly after dropping.</summary>
        public const float ButterflySeconds = 0.8f;

        /// <summary>How far a goalie squares up to the puck's side: positioning 0 and 100 (capped at full).</summary>
        public const float SquareWorst = 0.7f,
            SquareBest = 1.3f;

        /// <summary>How far out a goalie comes, as a share of <see cref="DepthFar"/>: positioning 0 and 100.</summary>
        public const float DepthWorst = 0.7f,
            DepthBest = 1.3f;

        /// <summary>Crease shuffle speed as a share of <see cref="ShuffleSpeed"/>: positioning 0 and 100.</summary>
        public const float ShuffleWorst = 0.75f,
            ShuffleBest = 1.25f;

        /// <summary>Reach of the glove or blocker compared with an average one: hand trait 0 and 100.</summary>
        public const float HandWorst = 0.4f,
            HandBest = 1.6f;

        /// <summary>What a hand trait adds to or takes off the clean-stop chance on its side: at 0, and the opposite at 100.</summary>
        public const float HandClean = 0.16f;

        /// <summary>Glove and blocker this far apart, or more, give the goalie a weak side shooters pick.</summary>
        public const int WeakSideGap = 6;

        /// <summary>Chance to play a held puck to a teammate rather than the corner: puck handling 0 and 100.</summary>
        public const float PassOutPoor = 0.1f,
            PassOutGood = 0.95f;

        /// <summary>How far a goalie's pass strays from its target, in radians: puck handling 0 and 100.</summary>
        public const float PassErrorPoor = 0.45f,
            PassErrorGood = 0.04f;

        /// <summary>Speed of a goalie's pass: puck handling 0 and 100.</summary>
        public const float PassSpeedPoor = 7f,
            PassSpeedGood = 13f;

        /// <summary>Chance a goalie leaves the crease to stop a loose puck behind the net: puck handling 0 and 100.</summary>
        public const float PlayChancePoor = 0f,
            PlayChanceGood = 0.95f;

        /// <summary>How far from the middle of the goal line a loose puck behind the net may be for the goalie to go for it.</summary>
        public const float PlayReach = 4.5f;

        /// <summary>A loose puck behind the net slower than this can be stopped by the goalie.</summary>
        public const float PlaySpeed = 14f;

        /// <summary>Under the trapezoid rule a loose puck behind the net must lie at least this far inside the trapezoid for the goalie to go for it (W72).</summary>
        public const float TrapezoidMargin = 0.3f;

        /// <summary>Skating speed of a goalie going out to play the puck.</summary>
        public const float PlaySkate = 5f;

        /// <summary>How close the goalie must get to touch the puck it is going for.</summary>
        public const float PlayTouch = 0.7f;

        /// <summary>The goalie gives up on the puck once an opponent is this close to it.</summary>
        public const float PlayPressure = 1.6f;

        /// <summary>Seconds a goalie may spend out of the crease going for one puck.</summary>
        public const float PlaySeconds = 3f;

        /// <summary>Seconds a glove or blocker save pose is shown.</summary>
        public const float HandPoseSeconds = 0.45f;
    }
}
