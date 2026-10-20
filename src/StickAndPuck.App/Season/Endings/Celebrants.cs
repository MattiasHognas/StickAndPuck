using System.Numerics;
using StickAndPuck.App.Core;

namespace StickAndPuck.App.Season.Endings
{
    /// <summary>A celebrating team round its captain, each man doing his own thing: arms up with the stick,
    /// a fist pumping, the stick raised one-handed, a jump, two teammates hugging, one skating a slow circle
    /// round the group. Who does what, and where in his clip he is, follows from his number and his place, so
    /// the same club always celebrates the same way and nobody moves in step with his neighbour.</summary>
    internal static class Celebrants
    {
        private static readonly string[] Poses =
        [
            "celebrate",
            "pump",
            "stick_up",
            "jump",
            "celebrate",
            "pump",
            "stick_up",
        ];

        /// <summary>The team around a centre: <paramref name="spots"/> are offsets (x, z) from it; the last two spots
        /// become a hugging pair and one more player circles the group.</summary>
        /// <param name="club">The club.</param>
        /// <param name="kit">Which of its kits.</param>
        /// <param name="squad">Its skaters; the first is the captain, left out here.</param>
        /// <param name="centre">The captain's spot.</param>
        /// <param name="spots">The places around him.</param>
        /// <param name="time">Seconds into the scene.</param>
        /// <param name="mask">The first mask id to use.</param>
        public static IEnumerable<Figure> Around(
            ClubData club,
            Side kit,
            IReadOnlyList<PlayerData> squad,
            Vector3 centre,
            IReadOnlyList<Vector2> spots,
            float time,
            int mask
        )
        {
            int n = Math.Min(spots.Count, squad.Count - 1);
            for (int i = 0; i < n; i++)
            {
                PlayerData p = squad[i + 1];
                Vector3 at = centre + new Vector3(spots[i].X, 0f, spots[i].Y);
                uint h = Hash(p.Number, i);
                float offset = h % 1000 * 0.0024f; // his own place in the cycle
                float turn = ((h >> 10) % 100 * 0.006f) - 0.3f;
                string pose = Poses[(int)((h >> 17) % (uint)Poses.Length)];
                if (i >= n - 2 && n >= 4)
                {
                    // the last two hug: close together, facing each other
                    bool left = i == n - 2;
                    Vector3 mid = centre + new Vector3(spots[n - 2].X, 0f, spots[n - 2].Y);
                    at = mid + new Vector3(left ? -0.3f : 0.3f, 0f, 0f);
                    yield return new Figure(
                        Body.Skater,
                        EndingKit.Kit(club, kit, p),
                        "hug",
                        time + offset,
                        at,
                        left ? MathF.PI / 2f : -MathF.PI / 2f,
                        mask + i
                    );
                    continue;
                }

                yield return new Figure(
                    Body.Skater,
                    EndingKit.Kit(club, kit, p),
                    pose,
                    time + offset,
                    at,
                    turn - (0.15f * MathF.Sign(spots[i].X)),
                    mask + i
                );
            }

            if (squad.Count > n + 1)
            {
                // one more skates a slow lap round the back of the group
                const float radius = 5.6f;
                float a = (time * 0.32f) + 2.2f;
                Vector3 at =
                    centre
                    + new Vector3(radius * MathF.Cos(a), 0f, (radius * 0.6f * MathF.Sin(a)) - 1.5f);
                float heading = MathF.Atan2(-radius * MathF.Sin(a), radius * 0.6f * MathF.Cos(a));
                yield return new Figure(
                    Body.Skater,
                    EndingKit.Kit(club, kit, squad[n + 1]),
                    "skate",
                    time,
                    at,
                    heading,
                    mask + n
                );
            }
        }

        private static uint Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)((a * 73856093) ^ (b * 19349663));
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
