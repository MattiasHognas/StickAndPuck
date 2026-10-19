using System.Globalization;
using System.Numerics;
using Raylib_cs;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Arena
{
    /// <summary>
    /// The visitors' corner (W121): one section is the away club's, its seats wear the away club's
    /// colours, more of them than in the home sections, and it rises for the visitors' goals and hits
    /// while the home sections sit, and sits for the home club's goals.
    /// </summary>
    public sealed class AwayFansTests : CheckedTest
    {
        private static readonly CrowdSection[] All = Enum.GetValues<CrowdSection>();
        private static readonly Color HomePrimary = new(0x1e, 0x4a, 0x3a, 255);
        private static readonly Color HomeSecondary = new(0xf2, 0xc1, 0x4e, 255);
        private static readonly Color AwayPrimary = new(0xc8, 0x10, 0x2e, 255);
        private static readonly Color AwaySecondary = new(0x00, 0x57, 0x3f, 255);

        private static bool Same(Color a, Color b)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B;
        }

        private static string Share(float f)
        {
            return f.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static int Skater(World w, Side side)
        {
            return w.Players.First(p => p.Side == side && !p.IsGoalie).Id;
        }

        private static (World World, CrowdReactions Reactions) Arena()
        {
            World world = new WorldBuilder().Build();
            CrowdReactions reactions = new();
            CrowdFans.Seat(reactions);
            return (world, reactions);
        }

        private static void Step(World world, CrowdReactions reactions, Maybe<MatchEvent> e)
        {
            world.Events.Clear();
            if (e.TryGet(out MatchEvent ev))
            {
                world.Events.Publish(ev with { Time = world.Time });
            }

            reactions.Update(world);
            world.Time += Game.Step;
            world.Tick++;
        }

        private static void Run(World world, CrowdReactions reactions, float seconds)
        {
            for (int i = 0; i < seconds / Game.Step; i++)
            {
                Step(world, reactions, Maybe.None<MatchEvent>());
            }
        }

        [Test]
        public void OneCornerIsTheVisitors()
        {
            Check(
                CrowdFans.Of(CrowdFans.AwaySection) == Side.Away,
                "the away section supports the visitors"
            );
            Check(
                All.Where(s => CrowdFans.Of(s) == Side.Away).Take(2).Count() == 1,
                "only one section is the visitors'"
            );
            (World _, CrowdReactions reactions) = Arena();
            Check(
                All.All(s => reactions.Fans(s) == CrowdFans.Of(s)),
                "the reacting crowd is seated by club"
            );
        }

        [Test]
        public void TheAwayCornerWearsTheVisitorsColours()
        {
            string json = ResourceLoader.ReadText(Assets.CrowdSeats);
            CrowdRenderer.Seat[] seats = CrowdRenderer.Seats(
                json,
                (HomePrimary, HomeSecondary),
                (AwayPrimary, AwaySecondary),
                CrowdFans.Of
            );
            CrowdRenderer.Seat[] away = [.. seats.Where(s => s.Section == CrowdFans.AwaySection)];
            CrowdRenderer.Seat[] home = [.. seats.Where(s => s.Section != CrowdFans.AwaySection)];
            Check(away.Length > 100, $"the away corner has spectators ({away.Length})");
            Check(
                away.Where(s => s.Tinted)
                    .All(s => Same(s.Shirt, AwayPrimary) || Same(s.Shirt, AwaySecondary)),
                "every shirt in the away corner is in the visitors' colours"
            );
            Check(
                home.Where(s => s.Tinted)
                    .All(s => Same(s.Shirt, HomePrimary) || Same(s.Shirt, HomeSecondary)),
                "every shirt elsewhere is in the home club's colours"
            );
            float awayShare = away.Count(s => s.Tinted) / (float)away.Length;
            float homeShare = home.Count(s => s.Tinted) / (float)home.Length;
            Check(
                awayShare > 0.75f && awayShare > homeShare + 0.15f,
                $"the travelling fans wear the shirt more often ({Share(awayShare)} against {Share(homeShare)})"
            );

            CrowdRenderer.Seat[] allHome = CrowdRenderer.Seats(json, HomePrimary, HomeSecondary);
            Check(
                allHome
                    .Zip(seats)
                    .All(p => p.First.Look == p.Second.Look && p.First.Foot == p.Second.Foot),
                "the corner keeps its spectators' looks and seats"
            );
        }

        [Test]
        public void TheAwayCornerRisesForTheVisitorsGoal()
        {
            (World world, CrowdReactions reactions) = Arena();
            Step(
                world,
                reactions,
                new GoalScored(0, Side.Away, Skater(world, Side.Away), RinkSpec.NetMouth(-1), 20)
            );
            Run(world, reactions, 2f);
            Check(
                world.Crowd.Pose(CrowdFans.AwaySection) == CrowdPose.Cheer,
                "the visitors' corner cheers their goal"
            );
            Check(
                All.Where(s => s != CrowdFans.AwaySection)
                    .All(s => world.Crowd.Pose(s) == CrowdPose.Sit),
                "the home sections sit through it"
            );

            (world, reactions) = Arena();
            Step(
                world,
                reactions,
                new GoalScored(0, Side.Home, Skater(world, Side.Home), RinkSpec.NetMouth(1), 20)
            );
            Run(world, reactions, 2f);
            Check(
                world.Crowd.Pose(CrowdFans.AwaySection) == CrowdPose.Sit,
                "the visitors' corner sits for the home goal"
            );
            Check(
                world.Crowd.Pose(CrowdSection.FarCentre) == CrowdPose.Cheer,
                "while the home sections cheer"
            );
        }

        [Test]
        public void TheAwayCornerCheersTheVisitorsHits()
        {
            (World world, CrowdReactions reactions) = Arena();
            Vector3 corner = CrowdReactions.Middle(CrowdFans.AwaySection) with
            {
                Z = -RinkSpec.HalfWidth,
            };
            Step(
                world,
                reactions,
                new CheckLanded(0, Side.Away, Skater(world, Side.Away), corner, 2f)
            );
            Run(world, reactions, 0.5f);
            Check(
                world.Crowd.Pose(CrowdFans.AwaySection) == CrowdPose.Cheer,
                "the visitors' big hit is cheered from their corner"
            );
            Check(
                world.Crowd.Pose(CrowdSection.FarCentre) == CrowdPose.Boo,
                "and booed by the home section beside it"
            );
        }
    }
}
