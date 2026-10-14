using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;
using StickAndPuck.Tests.Support;
using TUnit.Core.Enums;
using AiDecisions = StickAndPuck.App.AI.Decisions;
using AiSpecialTeams = StickAndPuck.App.AI.SpecialTeams;

namespace StickAndPuck.Tests;

/// <summary>
/// The special teams: the formation maths, then all-AI power plays (a skater of the short club
/// sits in the box for the whole run, and a new power play starts every 100 s so both shapes come
/// up) with special teams on and off, both clubs on the power play in turn. On, the power play holds
/// its umbrella or 1-3-1 and the kill its box or diamond clearly closer than the even-strength AI
/// does, and the kill clears the puck down the ice (<see cref="PowerPlay"/>, also run by the AI
/// match tests). Formerly SAP_SCENE=specialteamstest.
/// </summary>
public sealed class SpecialTeamsTests : CheckedTest
{
    /// <summary>The first seed (the scene's SAP_SEED fallback).</summary>
    const int Seed = 1;

    /// <summary>The formation maths.</summary>
    [Test]
    public void Maths()
    {
        bool inside = true, inZone = true, apart = true;
        foreach (int end in new[] { -1, 1 })
        {
            foreach (var f in new[] { Formation.Umbrella, Formation.OneThreeOne, Formation.Box, Formation.Diamond })
            {
                foreach (int n in new[] { 3, 4, 5 })
                {
                    var spots = Formations.Spots(f, n, end, new Vector2(end * 18f, 4f));
                    foreach (var s in spots)
                    {
                        inside &= Vector2.Distance(s, TeamShape.Inside(s, 1.1f)) < 1e-4f;
                        inZone &= Formations.InZone(end, s, 1f);
                    }
                    for (int i = 0; i < spots.Count; i++)
                        for (int j = i + 1; j < spots.Count; j++) apart &= Vector2.Distance(spots[i], spots[j]) > 2.5f;
                }
            }
        }

        Check(inside && inZone, "every power play and kill spot is on the ice, inside the zone");
        Check(apart, "no two spots of a shape sit on top of each other");

        var umbrella = Formations.Spots(Formation.Umbrella, 5, 1, new Vector2(18f, 0));
        var one31 = Formations.Spots(Formation.OneThreeOne, 5, 1, new Vector2(18f, 0));
        int high = umbrella.Count(s => s.X < RinkSpec.GoalLineX - 10f);
        int middle = one31.Count(s => s.X is > RinkSpec.GoalLineX - 10f and < RinkSpec.GoalLineX - 7.5f);
        Check(high == 3 && middle == 3, $"the umbrella has three high, the 1-3-1 three across the middle ({high}, {middle})");
        var box = Formations.Spots(Formation.Box, 4, 1, new Vector2(18f, 0));
        var diamond = Formations.Spots(Formation.Diamond, 4, 1, new Vector2(18f, 0));
        Check(box.Count(s => MathF.Abs(s.Y) > 2f) == 4 && diamond.Count(s => MathF.Abs(s.Y) < 0.5f) == 2, "the box has two low and two high, the diamond one at each tip");
        var slid = Formations.Spots(Formation.Box, 4, 1, new Vector2(18f, 8f));
        Check(slid.Average(s => s.Y) > 1.5f, "the box slides toward the puck");
        Check(Formations.Answer(Formation.OneThreeOne) == Formation.Diamond && Formations.Answer(Formation.Umbrella) == Formation.Box, "the diamond answers the 1-3-1, the box the umbrella");

        var (pair, _) = Formations.Assign([(new Vector2(10f, 0), true), (new Vector2(24f, 1f), false), (new Vector2(15f, -8f), false)], Formations.Spots(Formation.Umbrella, 3, 1, Vector2.Zero));
        Check(pair[0] == 0 && pair[2] == 1, "skaters take the spot nearest them, a defenceman the point");
    }

    /// <summary>Power plays with special teams on and off, each club on the power play in turn.</summary>
    [Test]
    [ExecutionPriority(Priority.High)]
    public void PowerPlayAndKill()
    {
        PowerPlay(WorldBuilder.Clubs, new Settings(), Seed, Check);
    }

    /// <summary>
    /// Plays power plays with special teams on and off, each club on the power play in turn, and
    /// checks the shapes and the clears. Used by <see cref="PowerPlayAndKill"/> and by aimatchtest.
    /// </summary>
    /// <param name="clubs">The clubs.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="seed">The first seed.</param>
    /// <param name="check">Prints and counts one check.</param>
    /// <returns>The last match played.</returns>
    public static World PowerPlay(IReadOnlyList<ClubData> clubs, Settings settings, int seed, Action<bool, string> check)
    {
        var on = new Result();
        var off = new Result();
        var last = Maybe<World>.None;
        foreach (var pp in new[] { Side.Away, Side.Home })
        {
            last = Run(clubs, settings, pp, seed, true, on);
            Run(clubs, settings, pp, seed, false, off);
            seed++;
        }

        Console.WriteLine($"      power play: {on.PowerPlays} power plays, {on.Entries} set-ups, {on.PpSeconds:F0} s in the zone (umbrella {on.Umbrella:F0} s, 1-3-1 {on.OneThreeOne:F0} s), shots on goal {on.PpShots} against {on.KillShots} (off {off.PpShots}-{off.KillShots}), attempts {on.PpAttempts} against {on.KillAttempts} (off {off.PpAttempts}-{off.KillAttempts})");
        Console.WriteLine($"      shape held: power play {on.PpHeld:P0} (off {off.PpHeld:P0}), kill {on.KillHeld:P0} (off {off.KillHeld:P0}); kill in box {on.Box:F0} s, diamond {on.Diamond:F0} s");
        Console.WriteLine($"      clears: {on.Clears} ({on.Flips} flipped), {on.ClearedOut} out of the zone; puck in the kill's zone {on.ZoneShare:P0} of the time (off {off.ZoneShare:P0}), goals {on.PpGoals}-{on.KillGoals} (off {off.PpGoals}-{off.KillGoals})");
        check(on.Entries >= 6 && on.Umbrella > 10f && on.OneThreeOne > 10f, "the power play sets up in the zone in both the umbrella and the 1-3-1");
        check(on.PpHeld > 0.45f && on.PpHeld > off.PpHeld + 0.2f, "power play skaters hold their spots, clearly more than without special teams");
        check(on.Box > 10f && on.KillHeld > 0.6f && on.KillHeld > off.KillHeld + 0.15f, "the kill holds its box, clearly more than without special teams");
        check(on.Clears >= 8 && on.ClearedOut >= on.Clears * 0.6f, "the kill clears the puck down the ice, most clears leaving the zone");
        check(on.PpShots > on.KillShots * 1.5f, "the power play clearly outshoots the kill");
        return last.TryGet(out var played) ? played : throw new InvalidOperationException("expected a match to have been played");
    }

    sealed class Result
    {
        public int PpAttempts, KillAttempts, PowerPlays, Entries, Clears, Flips, ClearedOut, PpShots, KillShots, PpGoals, KillGoals;
        public float PpSeconds, Umbrella, OneThreeOne, Box, Diamond, ZoneSeconds, Seconds;
        public float PpHeldSum, PpSamples, KillHeldSum, KillSamples;

        public float PpHeld => PpSamples == 0 ? 0 : PpHeldSum / PpSamples;

        public float KillHeld => KillSamples == 0 ? 0 : KillHeldSum / KillSamples;

        public float ZoneShare => Seconds == 0 ? 0 : ZoneSeconds / Seconds;
    }

    /// <summary>Six minutes of play with one skater of the short club in the box throughout, a new power play every 100 s.</summary>
    static World Run(IReadOnlyList<ClubData> clubs, Settings settings, Side pp, int seed, bool special, Result r)
    {
        var kill = pp == Side.Home ? Side.Away : Side.Home;
        var world = SpecialTeamsSetup.ShortHanded(clubs, settings, kill);
        var systems = SpecialTeamsSetup.Systems(world, seed, special, rules: true);

        int end = PuckHandling.AttackEnd(world, pp);
        int steps = (int)(6 * 60 / Game.Step), every = (int)(100 / Game.Step);
        for (int i = 0; i < steps && world.Match.Phase != MatchPhase.Final; i++)
        {
            if (i > 0 && i % every == 0) AiSpecialTeams.Board(world).PowerPlay = SideOrNone.None;   // the next power play picks its shape again
            world.Events.Clear();
            foreach (var s in systems) s.Update(world, Game.Step);
            world.Time += Game.Step; world.Tick++;
            if (world.Match.Phase != MatchPhase.Play || !world.Puck.Live) continue;
            r.Seconds += Game.Step;
            var puck = new Vector2(world.Puck.Position.X, world.Puck.Position.Z);
            if (!Formations.InZone(end, puck)) continue;
            r.ZoneSeconds += Game.Step;
            var pk = world.Puck;
            bool ppPuck = pk.Carrier >= 0 && world.Players[pk.Carrier].Side == pp;
            if (!ppPuck || i % 15 != 0) continue;

            // measured the same way on and off: how near each skater is to the nearest pairing of the shape's spots
            r.PpHeldSum += Held(world, pp, [Formation.Umbrella, Formation.OneThreeOne], end, puck, -1);
            r.PpSamples++;
            r.KillHeldSum += Held(world, kill, [Formation.Box, Formation.Diamond], end, puck, pk.Carrier);
            r.KillSamples++;
        }

        Add(world, pp, kill, special, r);
        return world;
    }

    static void Add(World world, Side pp, Side kill, bool special, Result r)
    {
        var p = AiSpecialTeams.Tally(world, pp);
        var k = AiSpecialTeams.Tally(world, kill);
        if (special)
        {
            r.PowerPlays += p.PowerPlays; r.Entries += p.Entries; r.PpSeconds += p.Seconds; r.Umbrella += p.Umbrella; r.OneThreeOne += p.OneThreeOne;
            r.Box += k.Box; r.Diamond += k.Diamond; r.Clears += k.Clears; r.Flips += k.Flips; r.ClearedOut += k.ClearedOut;
        }
        r.PpAttempts += AiDecisions.Tally(world, pp).Shots; r.KillAttempts += AiDecisions.Tally(world, kill).Shots;
        r.PpShots += world.Match.Shots[(int)pp]; r.KillShots += world.Match.Shots[(int)kill];
        r.PpGoals += world.Match.Score[(int)pp]; r.KillGoals += world.Match.Score[(int)kill];
    }

    /// <summary>The share of a club's skaters within 2.5 m of their spot in the best fitting of its shapes; the killer nearest the carrier counts as held when he is on him.</summary>
    static float Held(World world, Side side, Formation[] shapes, int end, Vector2 puck, int carrier)
    {
        var team = world.OnIce(side).Where(o => !o.IsGoalie).ToList();
        if (team.Count == 0) return 0;
        var presser = Maybe<Player>.None;
        if (carrier >= 0 && team.MinBy(o => Vector3.Distance(o.Position, world.Players[carrier].Position)) is { } nearest && Vector3.Distance(nearest.Position, world.Players[carrier].Position) <= 3f) presser = nearest;
        float best = 0;
        foreach (var f in shapes)
        {
            var spots = Formations.Spots(f, team.Count, end, puck);
            var who = team.Select(o => (new Vector2(o.Position.X, o.Position.Z), false)).ToList();
            var (pair, _) = Formations.Assign(who, spots);
            int held = 0;
            for (int i = 0; i < team.Count; i++)
                if (presser.Is(x => x == team[i]) || (pair[i] >= 0 && Vector2.Distance(who[i].Item1, spots[pair[i]]) < 2.5f)) held++;
            best = Math.Max(best, (float)held / team.Count);
        }
        return best;
    }
}
