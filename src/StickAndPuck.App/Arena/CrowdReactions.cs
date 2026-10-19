using System.Numerics;
using System.Runtime.InteropServices;
using StickAndPuck.App.Core;
using StickAndPuck.App.Momentum;

namespace StickAndPuck.App.Arena
{
    /// <summary>W119: the crowd reacts. Reads this step's match events and sets each section of
    /// <see cref="World.Crowd"/> by the rules of <see cref="CrowdReaction.For"/>: on its feet and
    /// cheering for a goal, a fight or a big hit, standing for a save, booing a call against its club.
    /// The sections nearest the action react first and longest, the reaction spreading round the
    /// stands. Between moments a section follows its club's momentum: waving on a big boost, standing
    /// on a smaller one, seated otherwise. Presentation only: it reads the world, writes nothing the
    /// simulation reads and rolls no dice, so replays stay the same.</summary>
    internal sealed class CrowdReactions
    {
        /// <summary>How fast a reaction spreads round the stands, metres a second.</summary>
        public const float Spread = 45f;

        /// <summary>A club's boost from which its fans stand.</summary>
        public const float StandFrom = MomentumTuning.SwellBelow;

        /// <summary>A club's boost from which its fans wave.</summary>
        public const float WaveFrom = 0.6f;

        private static readonly CrowdSection[] Sections = Enum.GetValues<CrowdSection>();

        // the middle of each section's seats (art/scene/crowd_seats.json), in CrowdSection order
        private static readonly Vector3[] Middles =
        [
            new(-14.4f, 0f, -19.5f),
            new(0f, 0f, -19.7f),
            new(14.5f, 0f, -19.5f),
            new(-14.3f, 0f, 19.3f),
            new(0f, 0f, 19.7f),
            new(14.3f, 0f, 19.3f),
            new(-32.3f, 0f, 0f),
            new(32.2f, 0f, 0f),
        ];

        private readonly Active[] active = new Active[Sections.Length];
        private readonly Side[] fans = new Side[Sections.Length];

        [StructLayout(LayoutKind.Auto)]
        private readonly record struct Active(
            CrowdPose Pose,
            float From,
            float Until,
            int Priority
        );

        /// <summary>Gets the middle of a section's seats, on the ice's level.</summary>
        /// <param name="section">The section.</param>
        /// <returns>Where it is, in metres.</returns>
        public static Vector3 Middle(CrowdSection section)
        {
            return Middles[(int)section];
        }

        /// <summary>Gets the club a section supports; every section is the home club's until an away
        /// section is given its own.</summary>
        /// <param name="section">The section.</param>
        /// <returns>Its club.</returns>
        public Side Fans(CrowdSection section)
        {
            return fans[(int)section];
        }

        /// <summary>Gives a section to a club's fans, who then react for that club.</summary>
        /// <param name="section">The section.</param>
        /// <param name="side">Its club.</param>
        public void SetFans(CrowdSection section, Side side)
        {
            fans[(int)section] = side;
        }

        /// <summary>Reacts to this step's events and moves every section to its pose now.</summary>
        /// <param name="world">The match: its events, momentum, time and crowd.</param>
        public void Update(World world)
        {
            float now = world.Time;
            foreach (MatchEvent e in world.Events.Current)
            {
                foreach (CrowdSection section in Sections)
                {
                    React(section, e, now);
                }
            }

            foreach (CrowdSection section in Sections)
            {
                world.Crowd.Set(section, PoseNow(world, section, now), now);
            }
        }

        private void React(CrowdSection section, MatchEvent e, float now)
        {
            if (!CrowdReaction.For(e, Fans(section)).TryGet(out (CrowdReaction R, Vector3 At) hit))
            {
                return;
            }

            CrowdReaction r = hit.R;
            float distance = Vector2.Distance(
                new Vector2(hit.At.X, hit.At.Z),
                new Vector2(Middles[(int)section].X, Middles[(int)section].Z)
            );
            bool near = distance <= r.Reach;
            CrowdPose pose;
            float seconds;
            if (near)
            {
                pose = r.Near;
                seconds = r.Seconds;
            }
            else if (r.Far.TryGet(out CrowdPose far))
            {
                pose = far;
                seconds = r.Seconds * CrowdReaction.FarShare;
            }
            else
            {
                return;
            }

            Active current = active[(int)section];
            if (current.Until > now && current.Priority > r.Priority)
            {
                return;
            }

            float from = now + (distance / Spread);
            active[(int)section] = new Active(pose, from, from + seconds, r.Priority);
        }

        private CrowdPose PoseNow(World world, CrowdSection section, float now)
        {
            Active a = active[(int)section];
            if (now >= a.From && now < a.Until)
            {
                return a.Pose;
            }

            if (now < a.From && a.Until > a.From)
            {
                return world.Crowd.Pose(section); // the reaction has not reached this section yet
            }

            float boost = MomentumMeter.Of(world, Fans(section));
            return boost >= WaveFrom ? CrowdPose.Wave
                : boost >= StandFrom ? CrowdPose.Stand
                : CrowdPose.Sit;
        }
    }
}
