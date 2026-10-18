using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.AI.Positioning.Dev;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests.AI.Positioning
{
    /// <summary>
    /// A full 5 on 5 period headless with positioning, skating, puck handling, the goalies, puck physics
    /// and the match rules. Covers 5 on 5 holds shape through a period with no
    /// stuck players; plus that roles are one of each, that forwards stay onside, and that high
    /// positioning makes a difference. Formerly SAP_SCENE=positioningtest.
    /// </summary>
    public sealed class PositioningTests : CheckedTest
    {
        private static readonly MatchSetup Setup = WorldBuilder.DefaultSetup with
        {
            PeriodSeconds = 20 * 60,
            Inputs =
            [
                new InputSlot(InputDevice.None, Side.Home),
                new InputSlot(InputDevice.None, Side.Away),
            ],
        };

        private static World Make()
        {
            return new(Setup, new Settings());
        }

        [Test]
        public void RolesOneEach()
        {
            World world = new(
                new MatchSetup { Home = new ClubData(), Away = new ClubData() },
                new Settings()
            );
            PlayingPosition[] positions =
            [
                PlayingPosition.D,
                PlayingPosition.D,
                PlayingPosition.C,
                PlayingPosition.RW,
                PlayingPosition.LW,
            ];
            List<Player> skaters =
            [
                .. positions.Select(pos =>
                    world.AddPlayer(
                        Side.Home,
                        new PlayerData
                        {
                            Position = pos,
                            Shoots =
                                pos == PlayingPosition.D && world.Players.Count == 0
                                    ? Hand.Right
                                    : Hand.Left,
                        },
                        Vector3.Zero
                    )
                ),
            ];
            IReadOnlyDictionary<int, Role> roles = Roles.Assign(skaters);
            Check(
                roles.Values.Distinct().Take(6).Count() == 5,
                "five skaters get five different roles"
            );
            Check(
                roles[skaters[2].Id] == Role.Centre
                    && roles[skaters[3].Id] == Role.RightWing
                    && roles[skaters[4].Id] == Role.LeftWing,
                "forwards take their roster position's role"
            );
            Check(
                roles[skaters[0].Id] == Role.RightDefence
                    && roles[skaters[1].Id] == Role.LeftDefence,
                "defencemen take the side they shoot from"
            );

            List<Player> allCentres =
            [
                .. Enumerable
                    .Range(0, 5)
                    .Select(_ =>
                        world.AddPlayer(
                            Side.Away,
                            new PlayerData { Position = PlayingPosition.C },
                            Vector3.Zero
                        )
                    ),
            ];
            Check(
                Roles.Assign(allCentres).Values.Distinct().Take(6).Count() == 5,
                "five centres still fill five roles"
            );
            Check(
                Roles
                    .Assign([.. allCentres.Take(3)])
                    .Values.Order()
                    .SequenceEqual([Role.Centre, Role.LeftDefence, Role.RightDefence]),
                "three skaters form a triangle"
            );
        }

        [Test]
        public void ShapeSpotsInside()
        {
            bool inside = true,
                onside = true,
                goalSide = true;
            foreach (int end in new[] { -1, 1 })
            {
                foreach (TeamPhase phase in new[] { TeamPhase.Attack, TeamPhase.Defend })
                {
                    for (float x = -29f; x <= 29f; x += 1.5f)
                    {
                        for (float z = -12f; z <= 12f; z += 1.5f)
                        {
                            foreach (Role role in Roles.For(5))
                            {
                                Vector2 puck = new(x, z);
                                Vector2 s = TeamShape.Spot(role, phase, end, puck);
                                if (s != TeamShape.Inside(s))
                                {
                                    inside = false;
                                }

                                float sa = end * s.X,
                                    pa = end * x;
                                if (
                                    phase == TeamPhase.Attack
                                    && !Roles.IsDefence(role)
                                    && pa <= RinkSpec.BlueLineX
                                    && sa > RinkSpec.BlueLineX
                                )
                                {
                                    onside = false;
                                }

                                if (
                                    phase == TeamPhase.Defend
                                    && Roles.IsDefence(role)
                                    && pa > -RinkSpec.BlueLineX
                                    && sa > pa
                                )
                                {
                                    goalSide = false;
                                }
                            }
                        }
                    }
                }
            }

            Check(inside, "every shape spot is inside the boards");
            Check(onside, "attacking forwards never wait past the blue line before the puck");
            Check(goalSide, "defending defencemen stay goal side of the puck outside their zone");
        }

        /// <summary>Runs one 20 minute period of 5 on 5 and checks nobody gets stuck and the shapes hold.</summary>
        [Test]
        [ExecutionPriority(Priority.High)]
        public void Period()
        {
            World world = Make();
            Scenes.AddStarters(world);
#pragma warning disable S2245 // fixed seed so the check repeats
            // checking ends the lunge of a defender an AI carrier dekes, as in a match
            ISystem[] systems = Systems(
                world,
                7,
                new CheckingSystem(new Random(7)),
                new MatchRules()
            );
#pragma warning restore S2245
            _ = StandInPlay.Start(world, 7);
            MatchFlow.Begin(world, log: false);
            PeriodTally t = new();
            int steps = (int)(((world.Setup.PeriodSeconds * 2) + 60) / Game.Step);
            for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Intermission; i++)
            {
                Step(world, systems);
                Watch(world, t);
                if (i % 30 == 0 && world.Match.Phase == MatchPhase.Play && world.Puck.Live)
                {
                    Sample(world, t);
                }
            }
            Report(world, t);
        }

        /// <summary>What a period came to.</summary>
        private sealed class PeriodTally
        {
            public Dictionary<int, (Vector3 At, float Since)> LastMoved { get; } = [];

            public HashSet<int> Touchers { get; } = [];

            public Dictionary<int, float> PushingSince { get; } = [];

            public float LongestStuck { get; set; }

            public int Touches { get; set; }

            public int LastTouch { get; set; } = -1;

            public float LongestStill { get; set; }

            public int Outside { get; set; }

            public int Samples { get; set; }

            public int Shaped { get; set; }

            public float Stacked { get; set; }

            public float Piled { get; set; }

            public float AwaySum { get; set; }

            public float Offside { get; set; }
        }

        /// <summary>Every step: puck touches, skaters on the rink, and how long each has gone without moving a metre.</summary>
        private static void Watch(World world, PeriodTally t)
        {
            Puck pk = world.Puck;
            if (pk.LastTouch >= 0 && pk.LastTouch != t.LastTouch)
            {
                t.LastTouch = pk.LastTouch;
                t.Touches++;
                if (!world.Players[pk.LastTouch].IsGoalie)
                {
                    _ = t.Touchers.Add(pk.LastTouch);
                }
            }
            foreach (Player p in world.Players)
            {
                if (p.IsGoalie)
                {
                    continue;
                }

                if (
                    MathF.Abs(p.Position.X) > RinkSpec.HalfLength
                    || MathF.Abs(p.Position.Z) > RinkSpec.HalfWidth
                    || float.IsNaN(p.Position.X)
                )
                {
                    t.Outside++;
                }

                (Vector3 at, float since) = t.LastMoved.GetValueOrDefault(
                    p.Id,
                    (p.Position, world.Time)
                );
                if (Vector3.Distance(at, p.Position) > 1.0f)
                {
                    t.LastMoved[p.Id] = (p.Position, world.Time);
                }
                else
                {
                    t.LastMoved[p.Id] = (at, since);
                    t.LongestStill = Math.Max(t.LongestStill, world.Time - since);
                }

                // stuck: pushing the stick hard but going nowhere
                bool pushing = p.Command.Move.Length() > 0.5f && p.Speed < 0.5f;
                float from = t.PushingSince.GetValueOrDefault(p.Id, world.Time);
                t.PushingSince[p.Id] = pushing ? from : world.Time;
                if (pushing)
                {
                    t.LongestStuck = Math.Max(t.LongestStuck, world.Time - from);
                }
            }
        }

        /// <summary>Twice a second in play: stacking, piles, distance from the ideal spots, and forwards offside early.</summary>
        private static void Sample(World world, PeriodTally t)
        {
            Puck pk = world.Puck;
            t.Samples++;
            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                List<Player> team = [.. world.OnIce(side).Where(p => !p.IsGoalie)];
                int end = PuckHandling.AttackEnd(world, side);
                // stacked: any two teammates within a metre; a pile: three within two metres of one another
                if (
                    team.Exists(a =>
                        team.Exists(b => b != a && Vector3.Distance(a.Position, b.Position) < 1.0f)
                    )
                )
                {
                    t.Stacked += 0.5f;
                }

                if (
                    team.Exists(a =>
                        team.Where(b => Vector3.Distance(a.Position, b.Position) < 2.0f)
                            .Skip(2)
                            .Any()
                    )
                )
                {
                    t.Piled += 0.5f;
                }

                foreach (Player p in team.Where(p => PositioningModel.JobOf(world, p) == Job.Shape))
                {
                    t.Shaped++;
                    t.AwaySum += Vector2.Distance(
                        PositioningModel.IdealOf(world, p),
                        new Vector2(p.Position.X, p.Position.Z)
                    );
                }
                // offside: an attacking forward over the far blue line while the puck is still outside it
                if (
                    PositioningModel.PhaseOf(world, side) == TeamPhase.Attack
                    && end * pk.Position.X < RinkSpec.BlueLineX
                    && team.Exists(p =>
                        !Roles.IsDefence(PositioningModel.RoleOf(world, p))
                        && PositioningModel.JobOf(world, p) is Job.Shape or Job.Support
                        && end * p.Position.X > RinkSpec.BlueLineX + 0.5f
                    )
                )
                {
                    t.Offside += 0.5f;
                }
            }
        }

        private void Report(World world, PeriodTally t)
        {
            int deadPucks = world.Dev.StandIn.TryGet(out StandInPlay standIn)
                ? standIn.DeadPucks
                : 0;
            int touches = t.Touches,
                touchers = t.Touchers.Count,
                outsideCount = t.Outside;
            float longestStill = t.LongestStill,
                stackSeconds = t.Stacked,
                pileSeconds = t.Piled,
                offsideSeconds = t.Offside;
            float meanAway = t.AwaySum / Math.Max(1, t.Shaped);
            float minutes = world.Setup.PeriodSeconds / 60f;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      period: {world.Match.Score[0]}-{world.Match.Score[1]}, shots on goal {world.Match.Shots[0]}-{world.Match.Shots[1]}, attempts {DecisionState.Tally(world, Side.Home).Shots}-{DecisionState.Tally(world, Side.Away).Shots}, {touches} touches by {touchers} skaters, {deadPucks} dead pucks, longest still {longestStill:F1} s, stacked {stackSeconds:F0} s, piled {pileSeconds:F0} s, mean {meanAway:F1} m off the ideal spot, offside shape {offsideSeconds:F0} s"
                )
            );
            Check(
                world.Match.Phase == MatchPhase.Intermission,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a {minutes:F0} minute period runs through to the intermission under the match rules"
                )
            );
            Check(outsideCount == 0, "no skater leaves the rink or loses its position");
            Check(
                longestStill < 20f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"nobody stands idle: longest without moving a metre is {longestStill:F1} s"
                )
            );
            Check(
                t.LongestStuck < 1.5f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"no skater is stuck: longest pushing without moving is {t.LongestStuck:F2} s"
                )
            );
            Check(
                touchers >= 8 && touches > 60,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the puck moves between players ({touches} touches by {touchers} of 10 skaters)"
                )
            );
            Check(
                stackSeconds < 0.05f * minutes * 60 * 2,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"teammates do not stack ({stackSeconds:F0} s of {minutes * 60 * 2:F0} team-seconds within a metre)"
                )
            );
            Check(
                pileSeconds < 0.03f * minutes * 60 * 2,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"no three teammates pile up ({pileSeconds:F0} s)"
                )
            );
            VerifyMatchActivity(world, deadPucks, offsideSeconds, meanAway, minutes);
        }

        private void VerifyMatchActivity(
            World world,
            int deadPucks,
            float offsideSeconds,
            float meanAway,
            float minutes
        )
        {
            Check(
                meanAway < 4.5f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"skaters hold shape: mean {meanAway:F1} m from their ideal spot"
                )
            );
            Check(
                offsideSeconds < 0.05f * minutes * 60 * 2,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"attacking forwards wait for the puck at the blue line ({offsideSeconds:F0} s over it early)"
                )
            );
            Check(
                deadPucks < minutes,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"play keeps going ({deadPucks} dead pucks in {minutes:F0} minutes)"
                )
            );
            Check(
                DecisionState.Tally(world, Side.Home).Shots
                    + DecisionState.Tally(world, Side.Away).Shots
                    > 10,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"both sides get shots away ({DecisionState.Tally(world, Side.Home).Shots}-{DecisionState.Tally(world, Side.Away).Shots})"
                )
            );
        }

        /// <summary>Smart skaters sit nearer their ideal spots than poor ones, over the same five minutes.</summary>
        [Test]
        [ExecutionPriority(Priority.AboveNormal)]
        public void HockeyIq()
        {
            static float Run(int smart)
            {
                World world = Make();
                Scenes.AddStarters(world);
                foreach (Player p in world.Players.ToList())
                {
                    if (p.IsGoalie)
                    {
                        continue;
                    }

                    PlayerData data = p.Data with
                    {
                        Traits = p.Data.Traits with { HockeyIq = smart },
                    };
                    world.Players[p.Id] = new Player
                    {
                        Id = p.Id,
                        Side = p.Side,
                        Data = data,
                        Position = p.Position,
                        Heading = p.Heading,
                    };
                }
                ISystem[] systems = Systems(world, 11);
                _ = StandInPlay.Start(world, 11);
                float sum = 0;
                int n = 0;
                for (int i = 0; i < 5 * 60 * 60; i++)
                {
                    Step(world, systems);
                    if (i % 30 != 0 || !world.Puck.Live)
                    {
                        continue;
                    }

                    foreach (Player p in world.Players)
                    {
                        if (p.IsGoalie || PositioningModel.JobOf(world, p) != Job.Shape)
                        {
                            continue;
                        }

                        sum += Vector2.Distance(
                            PositioningModel.IdealOf(world, p),
                            new Vector2(p.Position.X, p.Position.Z)
                        );
                        n++;
                    }
                }
                return sum / Math.Max(1, n);
            }

            float poor = Run(15),
                smart = Run(85);
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      mean distance from the ideal spot while holding shape: hockey IQ 15 -> {poor:F2} m, 85 -> {smart:F2} m"
                )
            );
            Check(smart < poor * 0.8f, "smarter positioning holds the shape tighter");
        }

        private static ISystem[] Systems(World world, int seed, params ISystem[] more)
        {
#pragma warning disable S2245 // fixed seeds so the checks repeat
            ISystem[] systems =
            [
                new PositioningSystem(new Random(seed)),
                new DecisionSystem(new Random(seed)),
                new SkatingSystem(),
                new PuckHandlingSystem(new Random(seed)),
                new GoalieSystem { Dice = new Random(seed) },
                new PuckPhysics(),
                .. more,
            ];
#pragma warning restore S2245
            foreach (ISystem s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
            {
                s.Start(world);
            }

            return systems;
        }

        private static void Step(World world, ISystem[] systems)
        {
            world.Events.Clear();
            foreach (ISystem s in systems)
            {
                s.Update(world, Game.Step);
            }

            world.Time += Game.Step;
            world.Tick++;
        }
    }
}
