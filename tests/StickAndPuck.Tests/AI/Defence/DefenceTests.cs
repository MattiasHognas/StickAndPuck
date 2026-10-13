using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Goalie;
using StickAndPuck.App.Physics;
using StickAndPuck.App.Players;
using StickAndPuck.App.Rules;
using StickAndPuck.Tests.Support;
using AiDecisions = StickAndPuck.App.AI.Decisions;

namespace StickAndPuck.Tests;

/// <summary>
/// The defence: the coverage maths, then full all-AI matches (the aimatchtest system set) with the
/// defence on and, for comparison, off, checking that 15 to 30 hits a side, no swarm
/// on the carrier, and more passing lanes covered in the own zone. Formerly SAP_SCENE=defencetest.
/// </summary>
public sealed class DefenceTests : CheckedTest
{
    /// <summary>The first match seed (the scene's SAP_SEED fallback).</summary>
    const int Seed = 1;

    /// <summary>How many matches to play each way (the scene's SAP_MATCHES fallback).</summary>
    const int Matches = 3;

    [Test]
    public void CoverageMaths()
    {
        var net = Coverage.OwnNet(1);   // a side attacking +x defends the -x net
        Check(net.X < 0, "a side defends the net at the other end from the one it attacks");
        Check(Coverage.ZoneOf(1, new Vector2(-20f, 0)) == DefenceZone.Own && Coverage.ZoneOf(1, new Vector2(0, 0)) == DefenceZone.Neutral
            && Coverage.ZoneOf(1, new Vector2(20f, 0)) == DefenceZone.Forecheck, "zones read from the defending side");
        Check(Coverage.Danger(net, new Vector2(-21f, 0)) > Coverage.Danger(net, new Vector2(-12f, 9f)), "a man in the slot is more dangerous than one at the point");
        Check(Coverage.Danger(net, new Vector2(-28.5f, 1f)) < 0.2f, "a man behind the net is not dangerous");

        var carrier = new Vector2(-22f, 9f);
        var slot = new Vector2(-20f, 0f);
        var mark = Coverage.MarkSpot(DefenceZone.Own, net, carrier, slot);
        Check(Vector2.Distance(mark, net) < Vector2.Distance(slot, net), $"the mark on the slot man is goal side of him ({mark.X:F1}, {mark.Y:F1})");
        Check(Coverage.LaneCovered(carrier, slot, [mark]), "and covers the lane from the carrier to him");
        var point = new Vector2(-12f, -6f);
        var pointMark = Coverage.MarkSpot(DefenceZone.Own, net, carrier, point);
        Check(Coverage.LaneCovered(carrier, point, [pointMark]), "the mark on the far point sits in the lane");
        Check(!Coverage.LaneCovered(carrier, point, [new Vector2(-25f, -8f)]), "a defender away from a lane does not cover it");

        var home = Coverage.HomeSpot(net, carrier, slot);
        Check(Vector2.Distance(home, net) < Vector2.Distance(slot, net) && home.X - net.X > 2f, "the stay-home defenceman is between the slot man and the net, outside the crease");
        var contain = Coverage.ContainSpot(DefenceZone.Own, net, carrier);
        Check(Vector2.Distance(contain, net) < Vector2.Distance(carrier, net), "containing keeps the defender goal side of the carrier");
        Check(Coverage.EngageChance(DefenceZone.Own, net, new Vector2(-20f, 11f), 90) > Coverage.EngageChance(DefenceZone.Neutral, net, new Vector2(0, 0), 20),
            "a good checker on the boards plays the body more often than a poor one in open ice");
        Check(Coverage.EngageChance(DefenceZone.Own, net, new Vector2(-22f, 0f), 50) > Coverage.EngageChance(DefenceZone.Own, net, new Vector2(-14f, 8f), 50),
            "a carrier in front of the net gets hit more often than one at the point");
        Check(Coverage.HitPace(30, 0.5f) < 1f && Coverage.HitPace(2, 0.5f) > 1f, "a side well ahead of its usual hits holds back, one behind plays the body more");
        Check(Vector2.Distance(Coverage.Room(new Vector2(1f, 0), Vector2.Zero), Vector2.Zero) >= Coverage.CarrierRoom - 0.01f, "markers leave the carrier to the man on him");
    }

    /// <summary>The matches with the defence on and off feed one verdict, so they stay one test.</summary>
    [Test]
    public void DefenceOnAndOff()
    {
        var on = new List<Stats>();
        var off = new List<Stats>();
        var played = Support.Matches.Play(Matches * 2, j => Match(Seed + j / 2, defence: j % 2 == 0));
        for (int i = 0; i < Matches; i++)
        {
            var s = played[i * 2];
            on.Add(s);
            off.Add(played[i * 2 + 1]);
            Console.WriteLine($"      seed {Seed + i} defence on:  {s}");
            Console.WriteLine($"      seed {Seed + i} defence off: {off[^1]}");
        }
        Verdict(on, off);
    }

    void Verdict(List<Stats> on, List<Stats> off)
    {
        Check(on.All(s => s.Final), "every match plays out to the final");
        Check(on.All(s => s.Hits[0] is >= 15 and <= 30 && s.Hits[1] is >= 15 and <= 30),
            $"hits land 15 to 30 a side ({string.Join(", ", on.Select(s => $"{s.Hits[0]}-{s.Hits[1]}"))}; off: {string.Join(", ", off.Select(s => $"{s.Hits[0]}-{s.Hits[1]}"))})");
        float swarmOn = on.Average(s => s.Swarm), swarmOff = off.Average(s => s.Swarm);
        float crowdOn = on.Average(s => s.Crowd), crowdOff = off.Average(s => s.Crowd);
        Check(swarmOn < 0.03f && crowdOn < crowdOff + 0.03f, $"no swarm: three or more defenders converge on a carrier away from their net front {swarmOn:P1} of the time (off {swarmOff:P1}); three within 4 m {crowdOn:P0} (off {crowdOff:P0})");
        float lanesOn = on.Average(s => s.Lanes), lanesOff = off.Average(s => s.Lanes);
        Check(lanesOn > lanesOff + 0.05f, $"more passing lanes covered in the own zone: {lanesOn:P0} (off {lanesOff:P0})");
        float homeOn = on.Average(s => s.NetFront), homeOff = off.Average(s => s.NetFront);
        Check(homeOn > 0.7f, $"a defender stays in front of the net while the puck is in the zone {homeOn:P0} of the time (off {homeOff:P0})");
        Check(on.All(s => s.Goals is >= 1 and <= 10), $"goals stay hockey-like ({string.Join(", ", on.Select(s => s.Goals))})");
        Check(on.All(s => s.Sog[0] is >= 10 and <= 70 && s.Sog[1] is >= 10 and <= 70), "shots on goal stay hockey-like");
        Check(on.All(s => s.LongestLoose < 15f), "nobody leaves a loose puck lying");
    }

    /// <summary>What one match came to.</summary>
    sealed class Stats
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

    static Stats Match(int seed, bool defence)
    {
        var world = new WorldBuilder().Setup(s => s with { PeriodSeconds = 20 * 60, Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] }).Build();
        var systems = Systems(world, seed, defence);
        var meter = new Meter(world.Puck.Position);
        int steps = (int)(2 * 60 * 60 / Game.Step);
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
        {
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
            meter.Observe(world);
        }
        return meter.Finish(world);
    }

    /// <summary>Watches a match step by step: hits, swarming, covered lanes and the net front.</summary>
    sealed class Meter(Vector3 start)
    {

        readonly Stats stats = new();
        long open_, swarm, crowd, zoneSteps, netFront;
        double lanes, shotDistance;
        int shots;
        float since;
        Vector3 at = start;

        public void Observe(World world)
        {
            foreach (var e in world.Events.Of<CheckLanded>())
                if (e.Side is { } side) stats.Hits[(int)side]++;
            foreach (var e in world.Events.Of<ShotTaken>())
            {
                shots++;
                shotDistance += Math.Min(Vector2.Distance(Flat(e.Position), Coverage.OwnNet(1)), Vector2.Distance(Flat(e.Position), Coverage.OwnNet(-1)));
            }

            var pk = world.Puck;
            bool play = pk.Live && world.Match.Phase == MatchPhase.Play;
            bool loose = play && pk.Carrier < 0;
            if (!loose || Vector3.Distance(pk.Position, at) > 0.5f) { at = pk.Position; since = world.Time; }
            stats.LongestLoose = Math.Max(stats.LongestLoose, world.Time - since);
            if (!play || pk.Carrier < 0 || world.Players[pk.Carrier].IsGoalie) return;

            var carrier = world.Players[pk.Carrier];
            var dside = carrier.Side == Side.Home ? Side.Away : Side.Home;
            var defenders = world.OnIce(dside).Where(p => !p.IsGoalie).Select(p => Flat(p.Position)).ToList();
            var c = Flat(carrier.Position);
            int end = PuckHandling.AttackEnd(world, dside);
            var net = Coverage.OwnNet(end);
            // a swarm: three or more defenders converging on the carrier, away from their own net front
            if (Vector2.Distance(c, net) > 8f)
            {
                open_++;
                var cv = Flat(carrier.Velocity);
                int converging = world.OnIce(dside).Count(p => !p.IsGoalie && Converging(p, c, cv));
                if (converging >= 3) swarm++;
                if (defenders.Count(d => Vector2.Distance(d, c) < 4f) >= 3) crowd++;
            }

            if (Coverage.ZoneOf(end, c) != DefenceZone.Own) return;
            zoneSteps++;
            var mates = world.OnIce(carrier.Side).Where(p => !p.IsGoalie && p != carrier).Select(p => Flat(p.Position)).ToList();
            if (mates.Count > 0) lanes += mates.Count(m => Coverage.LaneCovered(c, m, defenders)) / (double)mates.Count;
            if (defenders.Exists(d => Vector2.Distance(d, net + new Vector2(MathF.Sign(-net.X) * 4f, 0)) < 3f)) netFront++;
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
            var h = AiDecisions.Tally(world, Side.Home);
            var a = AiDecisions.Tally(world, Side.Away);
            stats.Passes = $"{h.Completed}/{h.Passes} and {a.Completed}/{a.Passes}, picked off {h.Intercepted}-{a.Intercepted}";
            return stats;
        }

        /// <summary>A defender within 6 m of the carrier skating at him faster than 1.5 m/s, relative to the carrier.</summary>
        static bool Converging(Player p, Vector2 carrier, Vector2 carrierVelocity)
        {
            var off = carrier - Flat(p.Position);
            float d = off.Length();
            if (d > 6f || d < 1e-3f) return false;
            return Vector2.Dot(Flat(p.Velocity) - carrierVelocity, off / d) > 1.5f;
        }

        static Vector2 Flat(Vector3 v)
        {
            return new Vector2(v.X, v.Z);
        }
    }

    static ISystem[] Systems(World world, int seed, bool defence)
    {
#pragma warning disable S2245 // fixed seeds so the checks repeat
        var systems = new List<ISystem>
        {
            new PositioningSystem(new Random(seed)), new DecisionSystem(new Random(seed)), new SkatingSystem(), new CheckingSystem(new Random(seed)),
            new PuckHandlingSystem(new Random(seed)), new GoalieSystem { Dice = new Random(seed) }, new PuckPhysics(), new MatchRules(), new OvertimeRules(),
        };
        if (defence) systems.Insert(1, new DefenceSystem(new Random(seed)));
#pragma warning restore S2245
        foreach (var s in systems) if (s is not (PuckPhysics or GoalieSystem)) s.Start(world);
        return [.. systems];
    }
}
