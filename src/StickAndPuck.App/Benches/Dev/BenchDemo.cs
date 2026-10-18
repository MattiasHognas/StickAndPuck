using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using StickAndPuck.App.Camera;
using StickAndPuck.App.Core;
using StickAndPuck.App.Lines;

namespace StickAndPuck.App.Benches.Dev
{
    /// <summary>The script and camera behind <see cref="BenchScene"/>.</summary>
    internal sealed class BenchDemo
    {
        /// <summary>How long each step of the script runs (s).</summary>
        public const float StepSeconds = 7f;

        /// <summary>When the first step starts (s): the benches seated first.</summary>
        public const float FirstAt = 3f;

        private static readonly ConditionalWeakTable<World, BenchDemo> Demos = [];

        /// <summary>The steps in order.</summary>
        private static readonly string[] Steps =
        [
            "goal for",
            "goal against",
            "penalty against",
            "penalty for",
            "fight",
            "hard check",
            "post",
            "offside",
            "stoppage",
            "long shift",
        ];

        private readonly List<MatchEvent> events = [];
        private int step = -1;
        private float fightUntil = float.NegativeInfinity;
        private double drawMs,
            drawMax;
        private int frames;

        /// <summary>Gets this step's scripted events.</summary>
        public IReadOnlyList<MatchEvent> Events => events;

        /// <summary>Gets a value indicating whether the scripted fight is on.</summary>
        public bool FightOn { get; private set; }

        /// <summary>Gets the bench scene's script for a world, if it runs one.</summary>
        public static Maybe<BenchDemo> Of(World world)
        {
            return Demos.TryGetValue(world, out BenchDemo demo) ? demo : Maybe.None<BenchDemo>();
        }

        /// <summary>Runs the bench scene's script in a world.</summary>
        public static void Attach(World world)
        {
            Demos.Add(world, new BenchDemo());
        }

        /// <summary>Moves the script one step on, frames the home bench and tallies the benches' draw time.</summary>
        /// <param name="world">The scene's world.</param>
        /// <param name="lastDrawMs">The time the last frame's bench passes took (ms).</param>
        public void Run(World world, double lastDrawMs)
        {
            events.Clear();
            world.Camera = BroadcastCamera.Straight(BenchLayout.DoorX(Side.Home), 17.3f, 11f);
            if (lastDrawMs > 0)
            {
                drawMs += lastDrawMs;
                drawMax = Math.Max(drawMax, lastDrawMs);
                frames++;
            }
            FightOn = world.Time < fightUntil;
            int now = (int)MathF.Floor((world.Time - FirstAt) / StepSeconds);
            if (now <= step || now < 0)
            {
                return;
            }

            step = now;
            if (frames > 0)
            {
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SAP: benches draw {drawMs / frames:0.000} ms/frame avg, {drawMax:0.000} max over {frames} frames"
                    )
                );
            }

            (drawMs, drawMax, frames) = (0, 0, 0);
            if (step >= Steps.Length)
            {
                return;
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SAP: benches step {step} {Steps[step]} at tick {world.Tick}"
                )
            );
            Script(world, Steps[step]);
        }

        private void Script(World world, string name)
        {
            float t = world.Time;
            Vector3 at = world.Puck.Position;
            int home = world.Players.First(p => p.Side == Side.Home).Id,
                away = world.Players.First(p => p.Side == Side.Away).Id;
            switch (name)
            {
                case "goal for":
                    events.Add(new GoalScored(t, Side.Home, home, at, 25f));
                    break;
                case "goal against":
                    events.Add(new GoalScored(t, Side.Away, away, at, 25f));
                    break;
                case "penalty against":
                    events.Add(new PenaltyCalled(t, Side.Home, home, at, 120f));
                    break;
                case "penalty for":
                    events.Add(new PenaltyCalled(t, Side.Away, away, at, 120f));
                    break;
                case "fight":
                    events.Add(new FightStarted(t, Side.Home, home, at));
                    fightUntil = t + 5f;
                    FightOn = true;
                    break;
                case "hard check":
                    events.Add(new CheckLanded(t, Side.Home, home, at, 2f));
                    break;
                case "post":
                    events.Add(new PuckHitPost(t, at, 30f));
                    break;
                case "offside":
                    events.Add(new OffsideCalled(t, Side.Home, at));
                    break;
                case "stoppage":
                    events.Add(new WhistleBlown(t, at));
                    break;
                default:
                    // a long shift: the home forwards on the ice are run down, so they change now and come off spent
                    foreach (
                        Player p in world
                            .OnIce(Side.Home)
                            .Where(p => !p.IsGoalie && Squad.GroupOf(p) == Group.Forwards)
                    )
                    {
                        p.Energy = 0.2f;
                    }

                    break;
            }
        }
    }
}
