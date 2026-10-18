using System.Globalization;
using System.Numerics;
using StickAndPuck.App.AI.Decisions;
using StickAndPuck.App.AI.Defence;
using StickAndPuck.App.AI.Positioning;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players.Checks;
using StickAndPuck.App.Players.Movement;
using StickAndPuck.App.Players.Puck;
using StickAndPuck.App.Rules.Match;
using StickAndPuck.App.Rules.Overtime;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;

namespace StickAndPuck.Tests.AI.Defence
{
    /// <summary>
    /// The defence: the coverage maths, then full all-AI matches (the aimatchtest system set) with the
    /// defence on and, for comparison, off, checking that 15 to 30 hits a side, no swarm
    /// on the carrier, and more passing lanes covered in the own zone. Formerly SAP_SCENE=defencetest.
    /// </summary>
    public sealed class DefenceTests : CheckedTest
    {
        /// <summary>The first match seed (the scene's SAP_SEED fallback).</summary>
        private const int Seed = 1;

        /// <summary>How many matches to play each way (the scene's SAP_MATCHES fallback).</summary>
        private const int Matches = 3;

        [Test]
        public void CoverageMaths()
        {
            Vector2 net = Coverage.OwnNet(1); // a side attacking +x defends the -x net
            Check(net.X < 0, "a side defends the net at the other end from the one it attacks");
            Check(
                Coverage.ZoneOf(1, new Vector2(-20f, 0)) == DefenceZone.Own
                    && Coverage.ZoneOf(1, new Vector2(0, 0)) == DefenceZone.Neutral
                    && Coverage.ZoneOf(1, new Vector2(20f, 0)) == DefenceZone.Forecheck,
                "zones read from the defending side"
            );
            Check(
                Coverage.Danger(net, new Vector2(-21f, 0))
                    > Coverage.Danger(net, new Vector2(-12f, 9f)),
                "a man in the slot is more dangerous than one at the point"
            );
            Check(
                Coverage.Danger(net, new Vector2(-28.5f, 1f)) < 0.2f,
                "a man behind the net is not dangerous"
            );

            Vector2 carrier = new(-22f, 9f);
            Vector2 slot = new(-20f, 0f);
            Vector2 mark = Coverage.MarkSpot(DefenceZone.Own, net, carrier, slot);
            Check(
                Vector2.Distance(mark, net) < Vector2.Distance(slot, net),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the mark on the slot man is goal side of him ({mark.X:F1}, {mark.Y:F1})"
                )
            );
            Check(
                Coverage.LaneCovered(carrier, slot, [mark]),
                "and covers the lane from the carrier to him"
            );
            Vector2 point = new(-12f, -6f);
            Vector2 pointMark = Coverage.MarkSpot(DefenceZone.Own, net, carrier, point);
            Check(
                Coverage.LaneCovered(carrier, point, [pointMark]),
                "the mark on the far point sits in the lane"
            );
            Check(
                !Coverage.LaneCovered(carrier, point, [new Vector2(-25f, -8f)]),
                "a defender away from a lane does not cover it"
            );

            Vector2 home = Coverage.HomeSpot(net, carrier, slot);
            Check(
                Vector2.Distance(home, net) < Vector2.Distance(slot, net) && home.X - net.X > 2f,
                "the stay-home defenceman is between the slot man and the net, outside the crease"
            );
            Vector2 contain = Coverage.ContainSpot(DefenceZone.Own, net, carrier);
            Check(
                Vector2.Distance(contain, net) < Vector2.Distance(carrier, net),
                "containing keeps the defender goal side of the carrier"
            );
            Check(
                Coverage.EngageChance(DefenceZone.Own, net, new Vector2(-20f, 11f), 90)
                    > Coverage.EngageChance(DefenceZone.Neutral, net, new Vector2(0, 0), 20),
                "a good checker on the boards plays the body more often than a poor one in open ice"
            );
            Check(
                Coverage.EngageChance(DefenceZone.Own, net, new Vector2(-22f, 0f), 50)
                    > Coverage.EngageChance(DefenceZone.Own, net, new Vector2(-14f, 8f), 50),
                "a carrier in front of the net gets hit more often than one at the point"
            );
            Check(
                Coverage.HitPace(30, 0.5f) < 1f && Coverage.HitPace(2, 0.5f) > 1f,
                "a side well ahead of its usual hits holds back, one behind plays the body more"
            );
            Check(
                Vector2.Distance(Coverage.Room(new Vector2(1f, 0), Vector2.Zero), Vector2.Zero)
                    >= Coverage.CarrierRoom - 0.01f,
                "markers leave the carrier to the man on him"
            );
        }

        /// <summary>Gets the matches with the defence on and off, shared by the class's tests.</summary>
        [ClassDataSource<Played<Stats>>(Shared = SharedType.PerClass)]
        public required Played<Stats> DefencePlayed { get; init; }

        /// <summary>Gets the matches: seed by seed, the defence on then off.</summary>
        public static IEnumerable<int> MatchNumbers()
        {
            return Enumerable.Range(0, Matches * 2);
        }

        /// <summary>One match, the defence on (even) or off (odd); <see cref="DefenceOnAndOff"/> judges them.</summary>
        [Test]
        [ExecutionPriority(Priority.Critical)]
        [MethodDataSource(nameof(MatchNumbers))]
        public void PlayMatch(int j)
        {
            DefencePlayed.Add(j, Match(Seed + (j / 2), defence: j % 2 == 0));
        }

        /// <summary>The matches with the defence on and off feed one verdict.</summary>
        [Test]
        [DependsOn(nameof(PlayMatch), [typeof(int)])]
        public void DefenceOnAndOff()
        {
            List<Stats> on = [];
            List<Stats> off = [];
            Stats[] played = DefencePlayed.All(Matches * 2);
            for (int i = 0; i < Matches; i++)
            {
                Stats s = played[i * 2];
                on.Add(s);
                off.Add(played[(i * 2) + 1]);
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"      seed {Seed + i} defence on:  {s}"
                    )
                );
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"      seed {Seed + i} defence off: {off[^1]}"
                    )
                );
            }
            Verdict(on, off);
        }

        private void Verdict(List<Stats> on, List<Stats> off)
        {
            Check(on.TrueForAll(s => s.Final), "every match plays out to the final");
            Check(
                on.TrueForAll(s => s.Hits[0] is >= 15 and <= 30 && s.Hits[1] is >= 15 and <= 30),
                $"hits land 15 to 30 a side ({string.Join(", ", on.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.Hits[0]}-{s.Hits[1]}")))}; off: {string.Join(", ", off.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.Hits[0]}-{s.Hits[1]}")))})"
            );
            float swarmOn = on.Average(s => s.Swarm),
                swarmOff = off.Average(s => s.Swarm);
            float crowdOn = on.Average(s => s.Crowd),
                crowdOff = off.Average(s => s.Crowd);
            Check(
                swarmOn < 0.03f && crowdOn < crowdOff + 0.03f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"no swarm: three or more defenders converge on a carrier away from their net front {swarmOn:P1} of the time (off {swarmOff:P1}); three within 4 m {crowdOn:P0} (off {crowdOff:P0})"
                )
            );
            float lanesOn = on.Average(s => s.Lanes),
                lanesOff = off.Average(s => s.Lanes);
            Check(
                lanesOn > lanesOff + 0.05f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"more passing lanes covered in the own zone: {lanesOn:P0} (off {lanesOff:P0})"
                )
            );
            float homeOn = on.Average(s => s.NetFront),
                homeOff = off.Average(s => s.NetFront);
            Check(
                homeOn > 0.7f,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"a defender stays in front of the net while the puck is in the zone {homeOn:P0} of the time (off {homeOff:P0})"
                )
            );
            Check(
                on.TrueForAll(s => s.Goals is >= 1 and <= 10),
                $"goals stay hockey-like ({string.Join(", ", on.Select(s => s.Goals))})"
            );
            Check(
                on.TrueForAll(s => s.Sog[0] is >= 10 and <= 70 && s.Sog[1] is >= 10 and <= 70),
                "shots on goal stay hockey-like"
            );
            Check(on.TrueForAll(s => s.LongestLoose < 15f), "nobody leaves a loose puck lying");
        }

        /// <summary>What one match came to.</summary>
        public sealed class Stats
        {
            public bool Final { get; set; }

            public int[] Hits { get; } = new int[2];

            public int[] Sog { get; } = new int[2];

            public int Goals { get; set; }

            public float Swarm { get; set; }

            public float Crowd { get; set; }

            public float Lanes { get; set; }

            public float NetFront { get; set; }

            public float LongestLoose { get; set; }

            public float ShotDistance { get; set; }

            public string Passes { get; set; } = "";

            public override string ToString()
            {
                return $"goals {Goals}, sog {Sog[0]}-{Sog[1]}, hits {Hits[0]}-{Hits[1]}, swarm {Swarm:P1}, crowded {Crowd:P0}, own-zone lanes covered {Lanes:P0}, net front {NetFront:P0}, shots from {ShotDistance:F1} m, passes {Passes}";
            }
        }

        private static Stats Match(int seed, bool defence)
        {
            World world = new WorldBuilder()
                .Setup(s =>
                    s with
                    {
                        PeriodSeconds = 20 * 60,
                        Inputs =
                        [
                            new InputSlot(InputDevice.None, Side.Home),
                            new InputSlot(InputDevice.None, Side.Away),
                        ],
                    }
                )
                .Build();
            ISystem[] systems = Systems(world, seed, defence);
            Meter meter = new(world.Puck.Position);
            int steps = (int)(2 * 60 * 60 / Game.Step);
            for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
            {
                world.Events.Clear();
                foreach (ISystem s in systems)
                {
                    s.Update(world, Game.Step);
                }

                world.Time += Game.Step;
                world.Tick++;
                meter.Observe(world);
            }
            return meter.Finish(world);
        }

        /// <summary>Watches a match step by step: hits, swarming, covered lanes and the net front.</summary>
        private sealed class Meter(Vector3 start)
        {
            private readonly Stats stats = new();
            private long open_,
                swarm,
                crowd,
                zoneSteps,
                netFront;
            private double lanes,
                shotDistance;
            private int shots;
            private float since;
            private Vector3 at = start;

            public void Observe(World world)
            {
                foreach (CheckLanded e in world.Events.Of<CheckLanded>())
                {
                    stats.Hits[(int)e.Side]++;
                }

                foreach (Vector3 position in world.Events.Of<ShotTaken>().Select(e => e.Position))
                {
                    shots++;
                    shotDistance += Math.Min(
                        Vector2.Distance(Flat(position), Coverage.OwnNet(1)),
                        Vector2.Distance(Flat(position), Coverage.OwnNet(-1))
                    );
                }

                Puck pk = world.Puck;
                bool play = pk.Live && world.Match.Phase == MatchPhase.Play;
                bool loose = play && pk.Carrier < 0;
                if (!loose || Vector3.Distance(pk.Position, at) > 0.5f)
                {
                    at = pk.Position;
                    since = world.Time;
                }
                stats.LongestLoose = Math.Max(stats.LongestLoose, world.Time - since);
                if (!play || pk.Carrier < 0 || world.Players[pk.Carrier].IsGoalie)
                {
                    return;
                }

                Player carrier = world.Players[pk.Carrier];
                Side dside = carrier.Side == Side.Home ? Side.Away : Side.Home;
                List<Vector2> defenders =
                [
                    .. world.OnIce(dside).Where(p => !p.IsGoalie).Select(p => Flat(p.Position)),
                ];
                Vector2 c = Flat(carrier.Position);
                int end = PuckHandling.AttackEnd(world, dside);
                Vector2 net = Coverage.OwnNet(end);
                // a swarm: three or more defenders converging on the carrier, away from their own net front
                if (Vector2.Distance(c, net) > 8f)
                {
                    open_++;
                    Vector2 cv = Flat(carrier.Velocity);
                    int converging = world
                        .OnIce(dside)
                        .Count(p => !p.IsGoalie && Converging(p, c, cv));
                    if (converging >= 3)
                    {
                        swarm++;
                    }

                    if (defenders.Where(d => Vector2.Distance(d, c) < 4f).Skip(2).Any())
                    {
                        crowd++;
                    }
                }

                if (Coverage.ZoneOf(end, c) != DefenceZone.Own)
                {
                    return;
                }

                zoneSteps++;
                List<Vector2> mates =
                [
                    .. world
                        .OnIce(carrier.Side)
                        .Where(p => !p.IsGoalie && p != carrier)
                        .Select(p => Flat(p.Position)),
                ];
                if (mates.Count > 0)
                {
                    lanes +=
                        mates.Count(m => Coverage.LaneCovered(c, m, defenders))
                        / (double)mates.Count;
                }

                if (
                    defenders.Exists(d =>
                        Vector2.Distance(d, net + new Vector2(MathF.Sign(-net.X) * 4f, 0)) < 3f
                    )
                )
                {
                    netFront++;
                }
            }

            public Stats Finish(World world)
            {
                stats.Final = world.Match.Phase == MatchPhase.Final;
                stats.Sog[0] = world.Match.Shots[0];
                stats.Sog[1] = world.Match.Shots[1];
                stats.Goals = world.Match.Score[0] + world.Match.Score[1];
                stats.Swarm = open_ == 0 ? 0 : (float)swarm / open_;
                stats.Crowd = open_ == 0 ? 0 : (float)crowd / open_;
                stats.Lanes = zoneSteps == 0 ? 0 : (float)(lanes / zoneSteps);
                stats.NetFront = zoneSteps == 0 ? 0 : (float)netFront / zoneSteps;
                stats.ShotDistance = shots == 0 ? 0 : (float)(shotDistance / shots);
                DecisionTally h = DecisionState.Tally(world, Side.Home);
                DecisionTally a = DecisionState.Tally(world, Side.Away);
                stats.Passes = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{h.Completed}/{h.Passes} and {a.Completed}/{a.Passes}, picked off {h.Intercepted}-{a.Intercepted}"
                );
                return stats;
            }

            /// <summary>A defender within 6 m of the carrier skating at him faster than 1.5 m/s, relative to the carrier.</summary>
            private static bool Converging(Player p, Vector2 carrier, Vector2 carrierVelocity)
            {
                Vector2 off = carrier - Flat(p.Position);
                float d = off.Length();
                return d <= 6f
                    && d >= 1e-3f
                    && Vector2.Dot(Flat(p.Velocity) - carrierVelocity, off / d) > 1.5f;
            }

            private static Vector2 Flat(Vector3 v)
            {
                return new Vector2(v.X, v.Z);
            }
        }

        private static ISystem[] Systems(World world, int seed, bool defence)
        {
            List<ISystem> systems =
            [
                new PositioningSystem(Seeds.Dice(seed)),
                new DecisionSystem(Seeds.Dice(seed)),
                new SkatingSystem(),
                new CheckingSystem(Seeds.Dice(seed)),
                new PuckHandlingSystem(Seeds.Dice(seed)),
                new GoalieSystem { Dice = Seeds.Dice(seed) },
                new PuckPhysics(),
                new MatchRules(),
                new OvertimeRules(),
            ];
            if (defence)
            {
                systems.Insert(1, new DefenceSystem(Seeds.Dice(seed)));
            }
            foreach (ISystem s in systems.Where(s => s is not (PuckPhysics or GoalieSystem)))
            {
                s.Start(world);
            }

            return [.. systems];
        }
    }
}
