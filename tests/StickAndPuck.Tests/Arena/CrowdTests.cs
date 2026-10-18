using System.Globalization;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Arena
{
    /// <summary>
    /// The crowd (A18): every seat in art/scene/crowd_seats.json sits in a known section and on a row's
    /// tread, the seats are dressed the same way every time with some in club colours, each pose shows
    /// only its own two frames, and a section's pose is set on its own.
    /// </summary>
    public sealed class CrowdTests : CheckedTest
    {
        private static readonly Color Primary = new(0x1e, 0x4a, 0x3a, 255);
        private static readonly Color Secondary = new(0xf2, 0xc1, 0x4e, 255);

        /// <summary>How far a point is outside the boards' rounded rectangle, in metres.</summary>
        private static float OutsideBoards(float x, float z)
        {
            const float r = RinkSpec.CornerRadius;
            float dx = MathF.Max(MathF.Abs(x) - (RinkSpec.HalfLength - r), 0f);
            float dz = MathF.Max(MathF.Abs(z) - (RinkSpec.HalfWidth - r), 0f);
            return MathF.Sqrt((dx * dx) + (dz * dz)) - r;
        }

        private static bool Near(float a, float b)
        {
            return MathF.Abs(a - b) < 1e-4f;
        }

        [Test]
        public void SeatsFillEverySection()
        {
            string json = ResourceLoader.ReadText(Assets.CrowdSeats);
            CrowdRenderer.Seat[] seats = CrowdRenderer.Seats(json, Primary, Secondary);
            Check(seats.Length > 3000, $"a full arena of seats ({seats.Length})");
            foreach (CrowdSection s in Enum.GetValues<CrowdSection>())
            {
                Check(seats.Where(x => x.Section == s).Skip(100).Any(), $"{s} has spectators");
            }

            Check(
                seats.All(x => x.Foot.Y is >= 0.5f and <= 6.1f),
                "every seat is on a tread of the stands"
            );
            Check(
                seats.All(x => OutsideBoards(x.Foot.X, x.Foot.Z) > 1.5f),
                "every seat is in the stands, clear of the boards"
            );
            Check(
                seats.All(x => x.Look is >= 0 and < CrowdFrames.Looks),
                "every look has a row in the atlas"
            );
            float tinted = seats.Count(x => x.Tinted) / (float)seats.Length;
            Check(
                tinted is > 0.5f and < 0.7f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"most but not all wear club colours ({tinted:0.00})"
                )
            );
            Check(
                seats
                    .Where(x => x.Tinted)
                    .All(x => x.Shirt.Equals(Primary) || x.Shirt.Equals(Secondary)),
                "a club shirt is the main or second colour"
            );
            int primaries = seats.Count(x => x.Tinted && x.Shirt.Equals(Primary));
            int secondaries = seats.Count(x => x.Tinted && x.Shirt.Equals(Secondary));
            Check(primaries > secondaries, "the main colour is the commonest");

            CrowdRenderer.Seat[] again = CrowdRenderer.Seats(json, Primary, Secondary);
            Check(seats.SequenceEqual(again), "the seats dress the same every match");
        }

        [Test]
        public void PosesShowTheirOwnFrames()
        {
            foreach (CrowdPose pose in Enum.GetValues<CrowdPose>())
            {
                HashSet<int> columns = [];
                for (int i = 0; i < 600; i++)
                {
                    columns.Add(CrowdFrames.Column(pose, i / 60f, 0.37f));
                }

                Check(
                    columns.SetEquals([(int)pose * 2, ((int)pose * 2) + 1]),
                    $"{pose} plays both its frames and only them"
                );
            }

            int chin = Enumerable
                .Range(0, 7 * 60)
                .Count(i => CrowdFrames.Column(CrowdPose.Sit, i / 60f, 0.1f) == 1);
            Check(chin is > 0 and < 7 * 60 / 3, "a seated spectator mostly sits still");
            Check(
                CrowdFrames.Column(CrowdPose.Cheer, 0f, 0f)
                    != CrowdFrames.Column(CrowdPose.Cheer, 1f / 3f, 0f),
                "a cheer pumps a few times a second"
            );
        }

        [Test]
        public void SectionsChangeOnTheirOwn()
        {
            CrowdState crowd = new();
            Check(
                Enum.GetValues<CrowdSection>().All(s => crowd.Pose(s) == CrowdPose.Sit),
                "everyone starts seated"
            );
            crowd.Set(CrowdSection.FarCentre, CrowdPose.Cheer, 12f);
            Check(crowd.Pose(CrowdSection.FarCentre) == CrowdPose.Cheer, "the set section cheers");
            Check(crowd.Pose(CrowdSection.FarLeft) == CrowdPose.Sit, "its neighbour stays seated");
            Check(
                Near(crowd.Since(CrowdSection.FarCentre), 12f),
                "the cheer starts when it was set"
            );
            crowd.Set(CrowdSection.FarCentre, CrowdPose.Cheer, 14f);
            Check(
                Near(crowd.Since(CrowdSection.FarCentre), 12f),
                "setting the same pose keeps it going"
            );
            crowd.SetAll(CrowdPose.Boo, 20f);
            Check(
                Enum.GetValues<CrowdSection>()
                    .All(s => crowd.Pose(s) == CrowdPose.Boo && Near(crowd.Since(s), 20f)),
                "every section boos"
            );
        }
    }
}
