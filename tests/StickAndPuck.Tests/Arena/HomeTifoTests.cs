using System.Numerics;
using StickAndPuck.App.Arena;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Arena
{
    /// <summary>
    /// The home banners and tifo (W120): the tifo goes up as the match starts and after a home goal, never
    /// for an away goal (which takes it down), stays up while the puck is dead and comes down once play has
    /// run on; the camera looks up at it only while nothing else has the frame; and the fans' scarves,
    /// flags and banners follow their section's pose.
    /// </summary>
    public sealed class HomeTifoTests : CheckedTest
    {
        /// <summary>A match for the tifo: with the flow it starts in the intro before the opening faceoff.</summary>
        private sealed class Match(bool flow)
        {
            public World World { get; } =
                flow ? new WorldBuilder().Build() : new WorldBuilder().NotBegun().Build();

            public TifoMoments Moments { get; } = new();

            public TifoState Tifo => World.Crowd.Tifo;

            public void Step(Maybe<MatchEvent> e)
            {
                World.Events.Clear();
                if (e.TryGet(out MatchEvent ev))
                {
                    World.Events.Publish(ev with { Time = World.Time });
                }

                Moments.Update(World);
                World.Time += Game.Step;
                World.Tick++;
            }

            public void Run(float seconds)
            {
                for (int i = 0; i < seconds / Game.Step; i++)
                {
                    Step(Maybe.None<MatchEvent>());
                }
            }

            public void Goal(Side side, Vector3 at)
            {
                int scorer = World.Players.First(p => p.Side == side && !p.IsGoalie).Id;
                Step(new GoalScored(0, side, scorer, at, 20));
            }

            public void Play()
            {
                World.Match.Phase = MatchPhase.Play;
                World.Puck.Live = true;
            }

            public void Dead(MatchPhase phase)
            {
                World.Match.Phase = phase;
                World.Puck.Live = false;
            }
        }

        [Test]
        public void TheTifoGoesUpAsTheMatchStarts()
        {
            Match m = new(flow: true);
            Check(!m.Tifo.Up, "down before the first step");
            m.Step(Maybe.None<MatchEvent>());
            Check(m.Tifo.Up, "up in the intro");
            Check(m.Tifo.Section == CrowdSection.FarCentre, "over the far centre stand");
            Check(
                m.Tifo.Lift(m.World.Time) < 0.2f,
                "passed up from the front row, not there at once"
            );
            m.Run(TifoState.RiseSeconds);
            Check(m.Tifo.Lift(m.World.Time) >= 1f, "all the way up after the rise");
        }

        [Test]
        public void TheStartTifoStaysUpWhileThePuckIsDeadAndComesDownInPlay()
        {
            Match m = new(flow: true);
            m.Run(TifoMoments.MinUp + 5f); // a long warm-up: still the intro
            Check(m.Tifo.Up, "up through the intro, however long");
            m.Dead(MatchPhase.Faceoff);
            m.Run(2f);
            Check(m.Tifo.Up, "up while the opening faceoff waits");
            m.Play();
            m.Step(Maybe.None<MatchEvent>());
            Check(!m.Tifo.Up, "down once the puck is in play after its time up");
            m.Run(TifoState.LowerSeconds);
            Check(m.Tifo.Lift(m.World.Time) <= 0f, "back down in the seats");
        }

        [Test]
        public void TheStartTifoStaysUpItsTimeEvenWhenPlayStartsSoon()
        {
            Match m = new(flow: true);
            m.Run(2f);
            m.Play();
            m.Run(TifoMoments.MinUp - 3f);
            Check(m.Tifo.Up, "a quick intro still keeps it up its time");
            m.Run(2f);
            Check(!m.Tifo.Up, "then it comes down");
        }

        [Test]
        public void AHomeGoalRaisesItOverTheScoringEnd()
        {
            Match m = new(flow: false);
            m.Run(1f);
            Check(!m.Tifo.Up, "no match flow, no start tifo");
            m.Goal(Side.Home, RinkSpec.NetMouth(1));
            Check(m.Tifo.Up, "up for the home goal");
            Check(m.Tifo.Section == CrowdSection.FarCentre, "at the +x end over the far centre");
            m.Run(TifoMoments.MinUp + 1f);
            Check(!m.Tifo.Up, "down once play has run on");
            m.Goal(Side.Home, RinkSpec.NetMouth(-1));
            Check(
                m.Tifo.Up && m.Tifo.Section == CrowdSection.FarLeft,
                "at the -x end over the far stand's -x third"
            );
            Check(m.Tifo.Section != CrowdFans.AwaySection, "never in the visitors' corner");
        }

        [Test]
        public void AnAwayGoalNeverRaisesItAndTakesItDown()
        {
            Match m = new(flow: false);
            m.Goal(Side.Away, RinkSpec.NetMouth(-1));
            Check(!m.Tifo.Up, "no tifo for the visitors' goal");
            m.Goal(Side.Home, RinkSpec.NetMouth(1));
            m.Run(1f);
            Check(m.Tifo.Up, "up for the home goal");
            m.Goal(Side.Away, RinkSpec.NetMouth(-1));
            Check(!m.Tifo.Up, "an away goal takes it down at once");
        }

        [Test]
        public void ASecondHomeGoalKeepsItUpLonger()
        {
            Match m = new(flow: false);
            m.Goal(Side.Home, RinkSpec.NetMouth(1));
            m.Run(TifoMoments.MinUp - 2f);
            int raises = m.Tifo.Raises;
            m.Goal(Side.Home, RinkSpec.NetMouth(1));
            Check(m.Tifo.Raises == raises + 1, "a new raise, so the camera looks again");
            m.Run(4f);
            Check(m.Tifo.Up, "still up: the hold starts again from the second goal");
            Check(m.Tifo.Lift(m.World.Time) >= 1f, "and it never dropped in between");
        }

        [Test]
        public void TheSettingTurnsItOff()
        {
            Match m = new(flow: true);
            m.World.Settings = m.World.Settings with { Supporters = false };
            m.Step(Maybe.None<MatchEvent>());
            m.Goal(Side.Home, RinkSpec.NetMouth(1));
            Check(!m.Tifo.Up, "no tifo with the fans' flags and tifo off");
        }

        [Test]
        public void TheTitleIntroRaisesItAndTheMenuTakesItDown()
        {
            World world = new WorldBuilder().Build();
            IntroCrowd.Rise(world);
            Check(world.Crowd.Tifo.Up, "up as the camera comes down from the rafters");
            IntroCrowd.Settle(world);
            Check(!world.Crowd.Tifo.Up, "down when the menu is up");
        }

        [Test]
        public void TheClothUnrollsUpTheStandFromTheFrontRow()
        {
            TifoRenderer.Cloth full = new(
                new Vector3(-7f, 10f, -28f),
                new Vector3(7f, 10f, -28f),
                new Vector3(7f, 2f, -20f),
                new Vector3(-7f, 2f, -20f)
            );
            TifoRenderer.Cloth half = TifoRenderer.Unrolled(full, 0.5f);
            Check(half.BottomLeft == full.BottomLeft, "held from the front row");
            Check(
                Vector3.Distance(half.TopLeft, new Vector3(-7f, 6f, -24f)) < 1e-4f,
                "its top edge halfway up the stand"
            );
            Check(TifoRenderer.Unrolled(full, 1f) == full, "the whole cloth when it is up");
        }

        [Test]
        public void TheCameraLooksUpAtItOnlyWhenTheFrameIsFree()
        {
            TifoState tifo = new();
            tifo.Raise(CrowdSection.FarCentre, 0f);
            float up = TifoState.RiseSeconds;
            Check(!TifoShot.Wants(tifo, 0.1f, free: true, held: 0f), "not before the cloth is up");
            Check(TifoShot.Wants(tifo, up, free: true, held: 0f), "once it is up");
            Check(
                !TifoShot.Wants(tifo, up, free: false, held: 0f),
                "not over play, a flyover, a close-up or a replay"
            );
            Check(
                !TifoShot.Wants(tifo, up, free: true, held: TifoShot.HoldSeconds),
                "only for a few seconds"
            );
            tifo.Lower(up);
            Check(!TifoShot.Wants(tifo, up, free: true, held: 0f), "not once it comes down");
        }

        [Test]
        public void ScarvesAndFlagsFollowTheCrowdPose()
        {
            float[] times = [.. Enumerable.Range(0, 60).Select(i => i * 0.05f)];
            foreach (PropKind kind in Enum.GetValues<PropKind>())
            {
                foreach (CrowdPose down in (CrowdPose[])[CrowdPose.Sit, CrowdPose.Boo])
                {
                    Check(
                        times.All(t => !SupporterProps.For(kind, down, t, 0.3f).HasValue),
                        $"no {kind} up while the section is {down}"
                    );
                }

                foreach (
                    CrowdPose up in (CrowdPose[])[CrowdPose.Stand, CrowdPose.Cheer, CrowdPose.Wave]
                )
                {
                    Check(
                        times.All(t => SupporterProps.For(kind, up, t, 0.3f).HasValue),
                        $"the {kind} is up while the section is {up}"
                    );
                }
            }

            Check(
                times.All(t =>
                    Look(PropKind.Scarf, CrowdPose.Stand, t) == new SupporterProps.Look(0, 0f, 0f)
                ),
                "a standing fan holds his scarf up still"
            );
            int[] cheering =
            [
                .. times.Select(t => Look(PropKind.Scarf, CrowdPose.Cheer, t).Frame).Distinct(),
            ];
            Check(
                cheering.Contains(1) && cheering.Contains(2) && !cheering.Contains(0),
                "a cheering fan shakes his scarf between its two waving frames"
            );
            Check(
                times.Any(t => Look(PropKind.Scarf, CrowdPose.Cheer, t).Rise > 0f),
                "and hops it on the beat"
            );
            float[] sway = [.. times.Select(t => Look(PropKind.Scarf, CrowdPose.Wave, t).Sway)];
            Check(
                sway.Min() < -0.1f && sway.Max() > 0.1f,
                "a waving section swings its scarves from side to side"
            );
            Check(
                times
                    .Select(t => Look(PropKind.Flag, CrowdPose.Stand, t).Frame)
                    .Distinct()
                    .Take(4)
                    .Count() == 3,
                "a standing fan's flag flutters through its three frames"
            );
        }

        [Test]
        public void HomeFansHoldBannersInTheFrontRowsAndTheVisitorsBringMore()
        {
            string json = ResourceLoader.ReadText(Assets.CrowdSeats);
            SupporterProps.Prop[] props = SupporterProps.Place(json, CrowdFans.Of);
            Check(
                props.SequenceEqual(SupporterProps.Place(json, CrowdFans.Of)),
                "the same fans every match"
            );
            Check(
                Enum.GetValues<PropKind>().All(k => props.Any(p => p.Kind == k)),
                "scarves, flags and banners all show"
            );
            Check(
                props.All(p => p.Kind != PropKind.Banner || CrowdFans.Of(p.Section) == Side.Home),
                "the slogan banners are the home club's"
            );
            Dictionary<CrowdSection, float> frontZ = props
                .GroupBy(p => p.Section)
                .ToDictionary(g => g.Key, g => g.Min(p => MathF.Abs(p.Foot.Z)));
            Check(
                props
                    .Where(p => p.Kind == PropKind.Banner && p.Section == CrowdSection.FarCentre)
                    .All(p => MathF.Abs(p.Foot.Z) < frontZ[CrowdSection.FarCentre] + 2.5f),
                "banners only in the front rows"
            );
            System.Text.Json.JsonElement[] all =
            [
                .. System.Text.Json.JsonElement.Parse(json).GetProperty("seats").EnumerateArray(),
            ];
            int seats = all.Count(s => (CrowdSection)s[4].GetInt32() == CrowdFans.AwaySection);
            int homeSeats = all.Count(s => (CrowdSection)s[4].GetInt32() == CrowdSection.FarLeft);
            float away = props.Count(p => p.Section == CrowdFans.AwaySection) / (float)seats;
            float home = props.Count(p => p.Section == CrowdSection.FarLeft) / (float)homeSeats;
            Check(away > home, "the travelling fans hold up more");
        }

        private static SupporterProps.Look Look(PropKind kind, CrowdPose pose, float time)
        {
            return SupporterProps.For(kind, pose, time, 0.3f).TryGet(out SupporterProps.Look l)
                ? l
                : throw new InvalidOperationException($"{kind} down in {pose}");
        }
    }
}
