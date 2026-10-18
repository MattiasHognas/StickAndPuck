using System.Globalization;
using System.Numerics;
using StickAndPuck.App.Core;
using StickAndPuck.App.Intermission;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.Tests.Support;

namespace StickAndPuck.Tests.Intermission
{
    /// <summary>
    /// The intermission, run with the match rules through two period breaks, and a third match
    /// where the drive is skipped: the Zamboni laps the ice between periods and the next period starts
    /// after it. Formerly SAP_SCENE=zambonitest.
    /// </summary>
    public sealed class ZamboniTests : CheckedTest
    {
        /// <summary>A match of 3-second periods with no overtime, the starters on and the match flow begun.</summary>
        private static World Make()
        {
            return new WorldBuilder()
                .Setup(s => s with { PeriodSeconds = 3, Overtime = OvertimeRule.None })
                .Build();
        }

        /// <summary>The route stays on the ice clear of the boards, the creases and the bench doors.</summary>
        [Test]
        public void Route()
        {
            ZamboniRoute route = IntermissionSystem.Route;
            bool inside = true,
                clearOfCreases = true,
                clearOfBenches = true;
            float nearestBoards = float.MaxValue;
            for (float d = 0; d <= route.Length; d += 0.1f)
            {
                if (!route.OnIce(d))
                {
                    continue;
                }

                Vector2 p = route.At(d);
                // half the Zamboni's 2.16 m width and its 2.62 m tail, whichever way it points
                const float reach = 2.62f;
                bool laps = p.X > IntermissionSystem.Route.At(0).X + 0.5f; // past the gate lane
                if (laps)
                {
                    float toSide = RinkSpec.HalfWidth - MathF.Abs(p.Y);
                    float toEnd = RinkSpec.HalfLength - MathF.Abs(p.X);
                    nearestBoards = Math.Min(nearestBoards, Math.Min(toSide, toEnd));
                    inside &= toSide > 1.08f && toEnd > reach;
                }
                float crease = RinkSpec.GoalLineX - RinkSpec.CreaseRadius;
                clearOfCreases &= MathF.Abs(p.X) + 1.08f < crease + 0.3f;
                bool atDoor =
                    MathF.Abs(MathF.Abs(p.X) - 6f) < 4f && p.Y < -RinkSpec.HalfWidth + 2.5f;
                clearOfBenches &= !atDoor;
            }
            Check(
                inside,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the laps stay inside the boards (closest {nearestBoards:F1} m from the centre to the boards)"
                )
            );
            Check(clearOfCreases, "the Zamboni never reaches a crease, so the goalies stay put");
            Check(
                clearOfBenches,
                "the Zamboni keeps clear of the skaters waiting at the bench doors"
            );
            Check(
                route.Duration is > 20f and < 45f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"one drive takes {route.Duration:F0} s"
                )
            );
            float far = 0;
            for (float d = 0; d <= route.Length; d += 0.5f)
            {
                far = Math.Max(far, route.At(d).X);
            }

            Check(
                far > 15f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"it laps the whole rink, out to x {far:F1} at the away end"
                )
            );
        }

        /// <summary>A whole match: two breaks, the Zamboni out for each, the next period only after it has gone.</summary>
        [Test]
        public void Laps()
        {
            World world = Make();
            IntermissionSystem system = new(() => false);
            float firstOut = -1,
                firstGone = -1,
                secondPeriod = -1;
            int drives = 0;
            Maybe<IntermissionSystem.Drive> last = Maybe.None<IntermissionSystem.Drive>();
            Run(
                world,
                system,
                w => w.Match.Phase == MatchPhase.Final,
                200f,
                w =>
                {
                    if (system.Current.TryGet(out IntermissionSystem.Drive d) && d != last)
                    {
                        drives++;
                        last = d;
                    }
                    if (system.Current.Is(x => !x.Finished) && firstOut < 0)
                    {
                        firstOut = w.Time;
                    }

                    if (system.Current.Is(x => x.Finished) && firstGone < 0)
                    {
                        firstGone = w.Time;
                    }

                    if (
                        w.Match.Period == 2
                        && w.Match.Phase == MatchPhase.Faceoff
                        && secondPeriod < 0
                    )
                    {
                        secondPeriod = w.Time;
                    }
                }
            );
            Check(world.Match.Phase == MatchPhase.Final, "the match plays through to the final");
            Check(
                drives == 2,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the Zamboni drives in both intermissions ({drives})"
                )
            );
            Check(
                firstGone - firstOut >= IntermissionSystem.Route.Duration - 0.1f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the first intermission lasts the whole drive ({firstGone - firstOut:F1} s)"
                )
            );
            Check(
                secondPeriod > firstGone && secondPeriod - firstGone < 3f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"period 2 starts {secondPeriod - firstGone:F1} s after the Zamboni leaves"
                )
            );
        }

        /// <summary>A skip ends the drive at once and brings the next period on quickly.</summary>
        [Test]
        public void Skip()
        {
            World world = Make();
            bool press = false;
            IntermissionSystem system = new(() => press);
            Run(world, system, w => system.Current.HasValue, 20f);
            float start = world.Time;
            Run(world, system, w => w.Time - start > 3f, 5f);
            press = true;
            Run(world, system, w => system.Current.Is(x => x.Finished), 1f);
            press = false;
            Check(system.Current.Is(x => x.Skipped), "enter skips the Zamboni");
            float skipped = world.Time;
            Run(
                world,
                system,
                w => w.Match.Period == 2 && w.Match.Phase == MatchPhase.Faceoff,
                10f
            );
            Check(
                world.Match.Period == 2 && world.Time - skipped < 3f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"after a skip period 2 starts {world.Time - skipped:F1} s later"
                )
            );
        }

        private static void Run(
            World world,
            IntermissionSystem system,
            Func<World, bool> until,
            float limit
        )
        {
            Run(world, system, until, limit, _ => { });
        }

        private static void Run(
            World world,
            IntermissionSystem system,
            Func<World, bool> until,
            float limit,
            Action<World> each
        )
        {
            ISystem[] systems = [new MatchRules(), system];
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
