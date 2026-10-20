using System.Numerics;

namespace StickAndPuck.App.Core
{
    /// <summary>One set of numbers for the rink, in metres, Y-up, x along the rink and +z toward the camera.
    /// They match the Blender arena (a Blender point (x, y, z) is (x, z, -y) here).</summary>
    public static class RinkSpec
    {
        /// <summary>Half the rink length, board to board along x (the ice in arena.glb ends at ±30).</summary>
        public const float HalfLength = 30f;

        /// <summary>Half the rink width: the boards' inner face sits at z = ±12.75.</summary>
        public const float HalfWidth = 12.75f;

        /// <summary>Radius of the rounded corners.</summary>
        public const float CornerRadius = 8.5f;

        /// <summary>Goal lines at x = ±26.7.</summary>
        public const float GoalLineX = 26.7f;

        /// <summary>Blue lines centred at x = ±7.62 (30 cm wide, as painted in arena.glb).</summary>
        public const float BlueLineX = 7.62f;

        /// <summary>Height of the boards above the ice.</summary>
        public const float BoardHeight = 1.07f;

        /// <summary>Height of the top of the glass above the ice (the glass in arena.glb runs from 1.15 to 2.95 m).
        /// A puck clearing it leaves the rink when <see cref="RuleSet.PuckOverGlass"/> is on.</summary>
        public const float GlassTop = 2.95f;

        /// <summary>Net mouth width (between the posts).</summary>
        public const float NetWidth = 1.83f;

        /// <summary>Net mouth height (crossbar).</summary>
        public const float NetHeight = 1.22f;

        /// <summary>Net depth behind the goal line.</summary>
        public const float NetDepth = 1.12f;

        /// <summary>Goal crease radius.</summary>
        public const float CreaseRadius = 1.83f;

        /// <summary>Faceoff circle radius.</summary>
        public const float FaceoffRadius = 4.5f;

        /// <summary>Puck radius.</summary>
        public const float PuckRadius = 0.0381f;

        /// <summary>The centre faceoff dot.</summary>
        public static readonly Vector3 CentreDot = Vector3.Zero;

        /// <summary>The nine faceoff dots: centre, four neutral-zone dots and four end-zone dots.</summary>
        public static readonly IReadOnlyList<Vector3> FaceoffDots = Array.AsReadOnly([
            CentreDot,
            new Vector3(-6.1f, 0, -6.7f),
            new Vector3(-6.1f, 0, 6.7f),
            new Vector3(6.1f, 0, -6.7f),
            new Vector3(6.1f, 0, 6.7f),
            new Vector3(-20.7f, 0, -6.7f),
            new Vector3(-20.7f, 0, 6.7f),
            new Vector3(20.7f, 0, -6.7f),
            new Vector3(20.7f, 0, 6.7f),
        ]);

        /// <summary>The centre of a net mouth on the goal line, for the net at the given end (-1 left, +1 right).</summary>
        public static Vector3 NetMouth(int end)
        {
            return new Vector3(Math.Sign(end) * GoalLineX, 0, 0);
        }

        /// <summary>Half the trapezoid's width where it meets the goal line (5.49 m apart, as painted by art/scripts/nhl_arena.py).</summary>
        public const float TrapezoidGoalHalf = 5.49f / 2;

        /// <summary>Half the trapezoid's width at the end boards (8.53 m apart).</summary>
        public const float TrapezoidBoardHalf = 8.53f / 2;

        /// <summary>Gets the trapezoid's half width across the rink at a distance <paramref name="x"/> along it (behind a goal line).</summary>
        public static float TrapezoidHalf(float x)
        {
            float t = Math.Clamp((MathF.Abs(x) - GoalLineX) / (HalfLength - GoalLineX), 0f, 1f);
            return TrapezoidGoalHalf + ((TrapezoidBoardHalf - TrapezoidGoalHalf) * t);
        }

        /// <summary>Gets a value indicating whether a spot is behind a goal line in a corner, outside the trapezoid
        /// (W72): where a goalie may not play the puck under <see cref="RuleSet.Trapezoid"/>. <paramref name="margin"/>
        /// widens the corner toward the net, so a spot within that much of a trapezoid line counts as outside.</summary>
        public static bool OutsideTrapezoid(Vector3 at, float margin = 0f)
        {
            return MathF.Abs(at.X) > GoalLineX && MathF.Abs(at.Z) > TrapezoidHalf(at.X) - margin;
        }

        /// <summary>Gets a value indicating whether a spot is in the goal crease of the net at <paramref name="end"/>
        /// (-1 left, +1 right): the half circle of <see cref="CreaseRadius"/> in front of the goal line, grown by
        /// <paramref name="margin"/> (a body's reach, or "around the crease").</summary>
        public static bool InCrease(Vector3 at, int end, float margin = 0f)
        {
            int e = Math.Sign(end);
            float front = (e * GoalLineX) - at.X; // how far in front of the goal line, toward centre, times the end
            if (e * front < -margin)
            {
                return false;
            }

            float dx = at.X - (e * GoalLineX);
            float r = CreaseRadius + margin;
            return (dx * dx) + (at.Z * at.Z) <= r * r;
        }
    }
}
