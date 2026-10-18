using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Pregame;
using StickAndPuck.App.Replay;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Pregame
{
    /// <summary>
    /// The warm-up, run with positioning, skating, puck physics and the match rules: both squads
    /// skate around until start is pressed, then the opening faceoff follows; and a match without a
    /// human keeps the plain intro. Formerly SAP_SCENE=warmuptest.
    /// </summary>
    public sealed class PregameTests : CheckedTest
    {
        /// <summary>A 25-second match with no overtime, the starters on and the match flow begun.</summary>
        private static World Make(bool human)
        {
            WorldBuilder builder = new WorldBuilder().Setup(s =>
                s with
                {
                    PeriodSeconds = 25,
                    Overtime = OvertimeRule.None,
                }
            );
            return (human ? builder.Human() : builder).Build();
        }

        [Test]
        public void SkatesUntilStart()
        {
            World w = Make(human: true);
            List<Player> skaters = [.. w.Players.Where(p => !p.IsGoalie)];
            Dictionary<int, float> travelled = skaters.ToDictionary(p => p.Id, _ => 0f);
            Dictionary<int, Vector3> last = skaters.ToDictionary(p => p.Id, p => p.Position);
            bool stayedHome = true;
            float topSpeed = 0;
            Run(
                w,
                _ => false,
                12f,
                world =>
                {
                    foreach (Player p in skaters)
                    {
                        travelled[p.Id] += Vector3.Distance(last[p.Id], p.Position);
                        last[p.Id] = p.Position;
                        int end = FaceoffFormation.DefendedEnd(world.Match, p.Side);
                        if (Math.Sign(p.Position.X) == -end)
                        {
                            stayedHome = false;
                        }

                        topSpeed = Math.Max(topSpeed, p.Speed);
                    }
                }
            );
            Check(
                w.Match.Phase == MatchPhase.Intro && PregameFlow.IsWarmingUp(w),
                "after 12 s with nobody pressing start the match is still in its intro"
            );
            Check(
                skaters.TrueForAll(p => travelled[p.Id] > 30f),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"every skater has skated at least 30 m (least {travelled.Values.Min():0} m: {string.Join(' ', skaters.Select(p => $"{p.Data.Position}{travelled[p.Id]:0}"))})"
                )
            );
            Check(stayedHome, "nobody crossed centre during the warm-up");
            Check(
                topSpeed > 3f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the warm-up is skated at a real pace (top {topSpeed:0.0} m/s)"
                )
            );
            Check(
                w.Players.Where(p => p.IsGoalie)
                    .All(p => MathF.Abs(p.Position.X) > RinkSpec.GoalLineX - 4f),
                "both goalies stay by their nets"
            );

            Check(PregameFlow.Start(w), "start is taken while warming up");
            float pressedAt = w.Time;
            Run(w, world => world.Match.Phase == MatchPhase.Faceoff, 5f);
            Check(
                w.Match.Phase == MatchPhase.Faceoff && w.Time - pressedAt < 3f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the opening faceoff follows start ({w.Time - pressedAt:0.0} s after the press)"
                )
            );
            Check(
                skaters.Where(p => p.Side == Side.Home).Take(6).Count() == 5
                    && skaters.TrueForAll(p => p.Speed < 0.01f),
                "the skaters stand on their faceoff spots"
            );
            Run(w, world => world.Match.Phase == MatchPhase.Play, 3f);
            Check(
                w.Match.Phase == MatchPhase.Play && w.Puck.Live,
                "the puck drops and play starts"
            );
            Check(!PregameFlow.Start(w), "start does nothing once the game is on");
        }

        [Test]
        public void FlyoverPlaysOut()
        {
            World w = Make(human: true);
            List<FlyoverPhase> seen = [];
            float lastProgress = 0;
            bool rising = true;
            Run(
                w,
                _ => false,
                ArenaFlyover.Seconds + 1f,
                world =>
                {
                    FlyoverPhase phase = ArenaFlyover.Phase(world);
                    if (seen.Count == 0 || seen[^1] != phase)
                    {
                        seen.Add(phase);
                    }

                    float progress = ArenaFlyover.Progress(world);
                    rising &= progress >= lastProgress;
                    lastProgress = progress;
                }
            );
            Check(
                seen.SequenceEqual([
                    FlyoverPhase.Aerial,
                    FlyoverPhase.CloseUp,
                    FlyoverPhase.Settle,
                    FlyoverPhase.Done,
                ]),
                $"the flyover goes aerial, close-up, settle, done ({string.Join(' ', seen)})"
            );
            Check(rising && lastProgress >= 1f, "its progress only rises, to 1");
            Check(
                PregameFlow.IsWarmingUp(w) && w.Match.Phase == MatchPhase.Intro,
                "the warm-up goes on once the flyover is done"
            );
            Check(
                ArenaFlyover.Phase(Make(human: false)) == FlyoverPhase.Off,
                "no warm-up, no flyover"
            );
        }

        [Test]
        public void StartSkipsFlyover()
        {
            World w = Make(human: true);
            Run(w, _ => false, 1f);
            Check(
                ArenaFlyover.Phase(w) == FlyoverPhase.Aerial
                    && ArenaFlyover.Progress(w) is > 0 and < 1,
                "a second in, the flyover is in the air"
            );
            Check(PregameFlow.Press(w), "start is taken during the flyover");
            Check(
                ArenaFlyover.Phase(w) == FlyoverPhase.Done
                    && ArenaFlyover.Progress(w) >= 1
                    && PregameFlow.IsWarmingUp(w),
                "the first press skips the flyover and the warm-up goes on"
            );
            Check(!ArenaFlyover.Skip(w), "a skipped flyover cannot be skipped again");
            Run(w, _ => false, 0.5f);
            Check(PregameFlow.Press(w), "the next press is taken");
            Check(!PregameFlow.IsWarmingUp(w), "the next press starts the game");
            Run(w, world => world.Match.Phase == MatchPhase.Faceoff, 5f);
            Check(w.Match.Phase == MatchPhase.Faceoff, "the opening faceoff follows");
            Check(
                ArenaFlyover.Phase(w) == FlyoverPhase.Off,
                "the flyover is off once the game is on"
            );
        }

        [Test]
        public void FlyoverLeavesChecksumsAlone()
        {
            // the same warm-up three ways: plain, with the camera flying over it, and with the flyover
            // skipped part way; every step's checksum must agree, so the flyover is render only
            World plain = Make(human: true);
            World filmed = Make(human: true);
            World skipped = Make(human: true);
            CameraSystem camera = new();
            camera.Start(filmed);
            List<ulong> a = [],
                b = [],
                c = [];
            Run(
                plain,
                _ => false,
                ArenaFlyover.Seconds + 1f,
                world => a.Add(WorldChecksum.Of(world))
            );
            Run(
                filmed,
                _ => false,
                ArenaFlyover.Seconds + 1f,
                world =>
                {
                    camera.Update(world, Game.Step);
                    b.Add(WorldChecksum.Of(world));
                }
            );
            Run(
                skipped,
                _ => false,
                ArenaFlyover.Seconds + 1f,
                world =>
                {
                    if (world.Tick == 90)
                    {
                        _ = ArenaFlyover.Skip(world);
                    }

                    c.Add(WorldChecksum.Of(world));
                }
            );
            Check(
                a.Count > 500 && a.SequenceEqual(b),
                "the flyover camera leaves every step's checksum unchanged"
            );
            Check(
                a.SequenceEqual(c),
                "skipping the flyover leaves every step's checksum unchanged"
            );
            Check(
                filmed.Camera.Position.Y < 20f && ArenaFlyover.Phase(filmed) == FlyoverPhase.Done,
                "the camera has come down from the flyover"
            );
        }

        [Test]
        public void NoHumanNoWarmup()
        {
            World w = Make(human: false);
            Run(w, world => world.Match.Phase != MatchPhase.Intro, 5f);
            Check(
                w.Match.Phase == MatchPhase.Faceoff
                    && w.Time <= MatchTuning.IntroSeconds + (Game.Step * 2),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"an AI-only match keeps the plain intro ({w.Time:0.0} s)"
                )
            );
        }

        [Test]
        public void Forced()
        {
            World w = Make(human: false);
            PregameFlow.Enable(w);
            Run(w, world => world.Match.Phase != MatchPhase.Intro, 5f);
            Check(
                w.Match.Phase == MatchPhase.Intro && PregameFlow.Elapsed(w) > 4.5f,
                "Pregame.Enable asks for the warm-up in a match with no human"
            );
            PregameFlow.Enable(w, on: false);
            Run(w, world => world.Match.Phase != MatchPhase.Intro, 5f);
            Check(w.Match.Phase == MatchPhase.Faceoff, "Pregame.Enable(false) lets the intro end");
        }

        /// <summary>Runs the AI, the warm-up, skating, physics and the rules step by step until <paramref name="until"/> or the limit.</summary>
        private static void Run(World world, Func<World, bool> until, float limit)
        {
            Run(world, until, limit, _ => { });
        }

        /// <summary>Runs as above, calling <paramref name="each"/> after every step.</summary>
        private static void Run(
            World world,
            Func<World, bool> until,
            float limit,
            Action<World> each
        )
        {
            ISystem[] systems =
            [
                new PositioningSystem(Seeds.Dice(28)),
                new PregameSystem(),
                new SkatingSystem(),
                new PuckPhysics(),
                new MatchRules(),
            ];
            for (int i = 0; i < limit / Game.Step && !until(world); i++)
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                each(world);
                world.Time += Game.Step;
                world.Tick++;
            }
        }
    }
}
