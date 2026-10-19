using System.Numerics;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Core;
using StickAndPuck.App.Momentum;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Arena
{
    /// <summary>
    /// The reacting crowd (W119): the stands rise and cheer for a goal, the end it went in at first; a
    /// big hit, a save, a fight and the referee's calls move the sections near them; a bigger moment is
    /// not cut short by a smaller one; the momentum boost keeps the fans up; and every time the crowd
    /// sits back down once it has passed.
    /// </summary>
    public sealed class CrowdReactionTests : CheckedTest
    {
        private static readonly CrowdSection[] All = Enum.GetValues<CrowdSection>();

        private sealed class Match
        {
            public Match()
            {
                World = new WorldBuilder().Build();
                World.Puck.Live = true; // nobody skates here, so keep play going and the boost draining
            }

            public World World { get; }

            public CrowdReactions Reactions { get; } = new();

            public MomentumSystem Momentum { get; } = new();

            public void Step(Maybe<MatchEvent> e)
            {
                World.Events.Clear();
                if (e.TryGet(out MatchEvent ev))
                {
                    World.Events.Publish(ev with { Time = World.Time });
                }

                Momentum.Update(World, Game.Step);
                Reactions.Update(World);
                World.Time += Game.Step;
                World.Tick++;
            }

            public void Publish(MatchEvent e)
            {
                Step(e);
            }

            public void Run(float seconds)
            {
                for (int i = 0; i < seconds / Game.Step; i++)
                {
                    Step(Maybe.None<MatchEvent>());
                }
            }

            public CrowdPose Pose(CrowdSection s)
            {
                return World.Crowd.Pose(s);
            }

            public bool Seated()
            {
                return All.All(s => Pose(s) == CrowdPose.Sit);
            }

            /// <summary>Runs until every section sits again.</summary>
            public bool SitsWithin(float seconds)
            {
                for (int i = 0; i < seconds / Game.Step; i++)
                {
                    Step(Maybe.None<MatchEvent>());
                    if (Seated())
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private static int Skater(World w, Side side)
        {
            return w.Players.First(p => p.Side == side && !p.IsGoalie).Id;
        }

        [Test]
        public void TheStandsRiseForAGoalFromTheEndItWentIn()
        {
            Match m = new();
            Check(m.Seated(), "everyone sits before the goal");
            m.Publish(
                new GoalScored(0, Side.Home, Skater(m.World, Side.Home), RinkSpec.NetMouth(1), 20)
            );
            m.Run(0.3f);
            Check(
                m.Pose(CrowdSection.RightEnd) == CrowdPose.Cheer,
                "the end behind the net cheers at once"
            );
            Check(m.Pose(CrowdSection.LeftEnd) == CrowdPose.Sit, "the far end has not risen yet");
            m.Run(1.5f);
            Check(All.All(s => m.Pose(s) == CrowdPose.Cheer), "then the whole arena cheers");
            m.Run(4f);
            Check(
                m.Pose(CrowdSection.RightEnd) == CrowdPose.Cheer,
                "the end at the net cheers longest"
            );
            Check(
                m.Pose(CrowdSection.LeftEnd) == CrowdPose.Wave,
                "the far end has gone over to waving on the boost"
            );
            Check(m.SitsWithin(40f), "the crowd sits back down once the boost has drained");
        }

        [Test]
        public void AGoalAgainstTheHomeClubKeepsTheStandsSeated()
        {
            Match m = new();
            m.Publish(
                new GoalScored(0, Side.Home, Skater(m.World, Side.Home), RinkSpec.NetMouth(1), 20)
            );
            m.Run(1f);
            m.Publish(
                new GoalScored(0, Side.Away, Skater(m.World, Side.Away), RinkSpec.NetMouth(-1), 20)
            );
            m.Run(2f);
            Check(m.Seated(), "the cheer stops and the home fans sit down");
        }

        [Test]
        public void ABigHitBringsTheSeatsBesideItUp()
        {
            Match m = new();
            Vector3 boards = new(0f, 0f, RinkSpec.HalfWidth);
            m.Publish(new CheckLanded(0, Side.Home, Skater(m.World, Side.Home), boards, 2f));
            m.Run(0.6f);
            Check(m.Pose(CrowdSection.NearCentre) == CrowdPose.Cheer, "the seats by the hit cheer");
            Check(
                m.Pose(CrowdSection.FarLeft) == CrowdPose.Stand,
                "the rest stand up with the boost"
            );
            Check(m.SitsWithin(10f), "and sit down again");

            m.Publish(new CheckLanded(0, Side.Away, Skater(m.World, Side.Away), boards, 2f));
            m.Run(0.6f);
            Check(
                m.Pose(CrowdSection.NearCentre) == CrowdPose.Boo,
                "a big hit on a home player is booed"
            );
            Check(m.Pose(CrowdSection.FarLeft) == CrowdPose.Sit, "but not across the rink");
            Check(m.SitsWithin(10f), "and the booing stops");

            m.Publish(new CheckLanded(0, Side.Home, Skater(m.World, Side.Home), boards, 0.5f));
            m.Run(0.6f);
            Check(m.Seated(), "a soft check moves nobody");
        }

        [Test]
        public void ASaveIsApplaudedAtItsEnd()
        {
            Match m = new();
            int goalie = m.World.Players.First(p => p.Side == Side.Home && p.IsGoalie).Id;
            m.Publish(new ShotSaved(0, Side.Home, goalie, RinkSpec.NetMouth(-1), 30));
            m.Run(0.5f);
            Check(
                m.Pose(CrowdSection.LeftEnd) == CrowdPose.Stand,
                "the end behind the goalie stands"
            );
            Check(m.Pose(CrowdSection.RightEnd) == CrowdPose.Sit, "the other end stays seated");
            Check(m.SitsWithin(5f), "and sits down again");
        }

        [Test]
        public void AFightGetsEveryoneUp()
        {
            Match m = new();
            Vector3 at = new(-10f, 0f, -8f);
            m.Publish(new FightStarted(0, Side.Home, Skater(m.World, Side.Home), at));
            m.Run(1.5f);
            Check(
                m.Pose(CrowdSection.FarLeft) == CrowdPose.Cheer,
                "the seats beside the fight cheer"
            );
            Check(m.Pose(CrowdSection.RightEnd) == CrowdPose.Stand, "the rest are on their feet");
            Check(m.SitsWithin(8f), "and sit down after it");
        }

        [Test]
        public void TheCrowdBoosCallsAgainstItsClub()
        {
            Match m = new();
            Vector3 at = new(5f, 0f, 3f);
            m.Publish(new PenaltyCalled(0, Side.Home, Skater(m.World, Side.Home), at, 120));
            m.Run(1.5f);
            Check(
                All.All(s => m.Pose(s) == CrowdPose.Boo),
                "a penalty on the home club is booed all round"
            );
            Check(m.SitsWithin(6f), "and the booing stops");

            m.Publish(new PenaltyCalled(0, Side.Away, Skater(m.World, Side.Away), at, 120));
            m.Run(1.5f);
            Check(
                m.Pose(CrowdSection.NearCentre) == CrowdPose.Cheer,
                "one on the visitors is cheered"
            );
            Check(m.SitsWithin(6f), "briefly");

            m.Publish(new OffsideCalled(0, Side.Home, new Vector3(RinkSpec.BlueLineX, 0f, 10f)));
            m.Run(0.5f);
            Check(
                m.Pose(CrowdSection.NearCentre) == CrowdPose.Boo,
                "an offside against the home club is booed"
            );
            m.Run(3f);
            m.Publish(new OffsideCalled(0, Side.Away, new Vector3(RinkSpec.BlueLineX, 0f, 10f)));
            m.Run(0.5f);
            Check(m.Seated(), "one against the visitors is not");
        }

        [Test]
        public void ASmallMomentDoesNotCutABigOneShort()
        {
            Match m = new();
            m.Publish(
                new GoalScored(0, Side.Home, Skater(m.World, Side.Home), RinkSpec.NetMouth(1), 20)
            );
            m.Run(1f);
            int goalie = m.World.Players.First(p => p.Side == Side.Away && p.IsGoalie).Id;
            m.Publish(new ShotSaved(0, Side.Away, goalie, RinkSpec.NetMouth(1), 30));
            m.Run(0.5f);
            Check(
                m.Pose(CrowdSection.RightEnd) == CrowdPose.Cheer,
                "a save does not stop the goal cheer"
            );
        }

        [Test]
        public void TheFansFollowTheirClubsMomentum()
        {
            Match m = new();
            MomentumState s = MomentumMeter.State(m.World);
            s.Level[(int)Side.Home] = 0.8f;
            m.Run(0.1f);
            Check(All.All(x => m.Pose(x) == CrowdPose.Wave), "a big boost has the fans waving");
            s.Level[(int)Side.Home] = 0.4f;
            m.Run(0.1f);
            Check(All.All(x => m.Pose(x) == CrowdPose.Stand), "a smaller one keeps them standing");
            s.Level[(int)Side.Home] = 0f;
            s.Level[(int)Side.Away] = 1f;
            m.Run(0.1f);
            Check(m.Seated(), "the visitors' boost leaves the home fans seated");

            m.Reactions.SetFans(CrowdSection.FarRight, Side.Away);
            m.Run(0.1f);
            Check(
                m.Pose(CrowdSection.FarRight) == CrowdPose.Wave,
                "an away section waves with its club"
            );
        }
    }
}
