using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Pregame;
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
#pragma warning disable S2245 // a fixed seed, so the test repeats
            ISystem[] systems =
            [
                new PositioningSystem(new Random(28)),
                new PregameSystem(),
                new SkatingSystem(),
                new PuckPhysics(),
                new MatchRules(),
            ];
#pragma warning restore S2245
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
