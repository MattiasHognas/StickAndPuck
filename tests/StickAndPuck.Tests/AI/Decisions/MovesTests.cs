using System.Numerics;
using StickAndPuck.App.AI;
using StickAndPuck.App.Core;
using StickAndPuck.App.Players;
using StickAndPuck.Tests.Support;
using AiDecisions = StickAndPuck.App.AI.Decisions;

namespace StickAndPuck.Tests;

/// <summary>
/// The AI's dekes and one-timers. The odds: a carrier with more puck control dekes more, a smarter
/// one times it closer to the sweet spot, and a smarter receiver sees more one-timers. Set pieces:
/// an AI receiver in the slot one-times a pass, and an AI carrier skating at a defender dekes him.
/// Then the full all-AI matches (<see cref="AllAiMatches"/>): the AI dekes and one-times in every one, goals stay
/// hockey-like on average, and no receiver lets a shot go by taking a pass with shoot still held.
/// </summary>
public sealed class MovesTests : CheckedTest
{
    [Test]
    public void Odds()
    {
        Check(DecisionSystem.DekeOdds(90) > DecisionSystem.DekeOdds(20), $"more puck control dekes more ({DecisionSystem.DekeOdds(20):P0} at 20, {DecisionSystem.DekeOdds(90):P0} at 90)");
        Check(DecisionSystem.DekeMisjudge(90) < DecisionSystem.DekeMisjudge(20) / 2, $"more hockey IQ times a deke closer to the sweet spot ({DecisionSystem.DekeMisjudge(20):F2} m off at 20, {DecisionSystem.DekeMisjudge(90):F2} m at 90)");
        Check(DecisionSystem.OneTimerOdds(90) > DecisionSystem.OneTimerOdds(20) + 0.3f, $"more hockey IQ sees more one-timers ({DecisionSystem.OneTimerOdds(20):P0} at 20, {DecisionSystem.OneTimerOdds(90):P0} at 90)");
    }

    /// <summary>A pass across the slot to an open AI winger: a smart one one-times it more often than not, and it goes at the net.</summary>
    [Test]
    public void ReceiverOneTimesInTheSlot()
    {
        int tries = 0, oneTimed = 0, atNet = 0, stray = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            var (world, systems, passer, receiver) = SlotPass(seed, 90);
            var shots = new List<ShotTaken>();
            int carriedBefore = -1;
            bool strayHere = false;
            for (int i = 0; i < 150; i++)
            {
                if (i == 3) Pass(passer, receiver);
                if (i == 4) Release(passer);
                Step(world, systems);
                shots.AddRange(world.Events.Current.OfType<ShotTaken>());
                if (world.Puck.Carrier == receiver.Id && carriedBefore != receiver.Id && receiver.Command.Shoot) strayHere = true;
                carriedBefore = world.Puck.Carrier;
            }
            tries++;
            var shot = shots.FirstOrNone(s => s.ShooterId == receiver.Id);
            if (strayHere) stray++;
            if (!shot.TryGet(out var s) || AiDecisions.Tally(world, Side.Home).OneTimers == 0) continue;
            oneTimed++;
            if (world.Match.Shots[0] > 0 || world.Match.Score[0] > 0 || OnTarget(s)) atNet++;
        }
        Check(oneTimed >= tries / 2, $"a smart AI winger open in the slot one-times the pass ({oneTimed} of {tries})");
        Check(atNet >= oneTimed * 0.8f, $"and his one-timer goes at the net ({atNet} of {oneTimed})");
        Check(stray == 0, $"he never takes the pass with shoot held ({stray} times)");
    }

    /// <summary>The same pass to a winger with no hockey IQ: he one-times far fewer.</summary>
    [Test]
    public void PoorReceiverOneTimesLess()
    {
        int smart = 0, poor = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            smart += OneTimers(seed, 95);
            poor += OneTimers(seed, 5);
        }
        Check(smart > poor + 4, $"a winger with hockey IQ 95 one-times more passes than one with 5 ({smart} against {poor} of 20)");
    }

    /// <summary>An AI carrier skating up the ice at a lone defender: he dekes him, and a skilled one mostly keeps the puck.</summary>
    [Test]
    public void CarrierDekesTheDefender()
    {
        int skilledDekes = 0, skilledKept = 0, poorDekes = 0, poorKept = 0;
        for (int seed = 1; seed <= 30; seed++)
        {
            var (d1, k1) = DekeRun(seed, 95);
            var (d2, k2) = DekeRun(seed, 5);
            skilledDekes += d1; skilledKept += k1; poorDekes += d2; poorKept += k2;
        }
        Check(skilledDekes >= 5, $"a skilled AI carrier dekes a defender coming at him ({skilledDekes} dekes in 30 rushes)");
        Check(poorDekes < skilledDekes, $"a clumsy one dekes less ({poorDekes} against {skilledDekes})");
        float skilled = skilledDekes == 0 ? 0 : (float)skilledKept / skilledDekes, poor = poorDekes == 0 ? 0 : (float)poorKept / poorDekes;
        Check(skilled > 0.6f && skilled > poor, $"a skilled one mostly beats the defender, a clumsy one less ({skilled:P0} against {poor:P0} kept the puck)");
    }

    /// <summary>The aimoves drill (the scene for recording them): over a few rounds the AI dekes, and one-times the pass across.</summary>
    [Test]
    public void Drill()
    {
        foreach (bool oneTimer in new[] { false, true })
        {
            var world = new World(WorldBuilder.DefaultSetup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] }, new Settings());
            StickAndPuck.App.AI.Dev.MovesDrill.Setup(world, oneTimer);
            var systems = Systems(world, 1);
            int shots = 0;
            for (int i = 0; i < 6 * 4 * 60; i++)
            {
                Step(world, systems);
                shots += world.Events.Current.OfType<ShotTaken>().Count(e => e.ShooterId == 1);
            }
            var tally = AiDecisions.Tally(world, Side.Home);
            if (oneTimer) Check(tally.OneTimers > 0 && shots > 0, $"the one-timer drill one-times the pass across ({tally.OneTimers} read, {shots} struck in 6 rounds)");
            else Check(tally.Dekes > 0, $"the deke drill dekes the defender ({tally.Dekes} in 6 rounds)");
        }
    }

    /// <summary>The full all-AI matches (<see cref="AllAiMatches"/>, played by <see cref="SaveRateTests"/>): the AI dekes and one-times, goals stay hockey-like, no stray shots.</summary>
    [Test]
    [DependsOn(typeof(SaveRateTests), nameof(SaveRateTests.PlayMatch), [typeof(int)])]
    public void Matches()
    {
        const int n = AllAiMatches.Count;
        var played = AllAiMatches.Numbers().Select(AllAiMatches.Get).ToArray();
        foreach (var (m, i) in played.Select((m, i) => (m, i))) Console.WriteLine($"      match {i}: {m}");
        var moves = played.Select(m => m.Moves).ToArray();
        int goals = played.Sum(m => m.Goals), sog = played.Sum(m => m.Shots), dekes = moves.Sum(m => m.Dekes), kept = moves.Sum(m => m.DekesKept);
        int armed = moves.Sum(m => m.Armed), oneTimers = moves.Sum(m => m.Struck), onGoal = moves.Sum(m => m.OnGoal), oneTimerGoals = moves.Sum(m => m.OneTimerGoals);
        Console.WriteLine($"      passes completed {(float)moves.Sum(m => m.Completed) / Math.Max(1, moves.Sum(m => m.Passes)):P1} of {moves.Sum(m => m.Passes)}");
        Console.WriteLine($"      per match: goals {(float)goals / n:F1}, shots on goal {(float)sog / n:F1}, shooting {(float)goals / Math.Max(1, sog):P1}; dekes {(float)dekes / n:F1} ({(float)kept / Math.Max(1, dekes):P0} kept the puck), one-timers armed {(float)armed / n:F1}, struck {(float)oneTimers / n:F1} ({onGoal} on goal in all), scored {oneTimerGoals} in all");
        Check((float)goals / n is >= 3f and <= 8f, $"goals stay hockey-like, 3 to 8 a match on average ({string.Join(", ", played.Select(m => m.Goals))})");
        Check(moves.All(m => m.Dekes > 0 && m.Struck > 0), "the AI dekes and one-times in every match");
        Check(moves.Sum(m => m.Stray) == 0, $"no receiver takes a pass with shoot held ({moves.Sum(m => m.Stray)} times)");
    }

    /// <summary>A home centre at the left point of the attacking zone with the puck, a winger alone on the far side of the slot.</summary>
    static (World World, ISystem[] Systems, Player Passer, Player Receiver) SlotPass(int seed, int iq)
    {
        var world = Bare();
        int end = PuckHandling.AttackEnd(world, Side.Home);
        var passer = Add(world, Side.Home, new Vector3(end * 18f, 0, -8f), 50);
        var receiver = Add(world, Side.Home, new Vector3(end * 21f, 0, 3.5f), iq);
        receiver.Heading = MathF.Atan2(-end, 0);
        Add(world, Side.Away, new Vector3(end * 2f, 0, 12f), 50);
        world.Puck.Position = PuckHandling.Blade(passer) with { Y = 0 };
        world.Puck.Carrier = passer.Id;
        world.Puck.LastTouch = passer.Id;
        return (world, Systems(world, seed), passer, receiver);
    }

    /// <summary>Makes the passer, played by hand for the step (control stays with him), pass to the receiver: the stick at him, pass pressed.</summary>
    static void Pass(Player passer, Player receiver)
    {
        var to = Vector2.Normalize(new Vector2(receiver.Position.X - passer.Position.X, receiver.Position.Z - passer.Position.Z));
        passer.Slot = 0;
        passer.Command = new PlayerCommand { Move = to, Pass = true };
    }

    /// <summary>Hands the passer back to the AI once the pass has gone.</summary>
    static void Release(Player passer)
    {
        passer.Slot = -1;
        passer.Command = default;
    }

    static int OneTimers(int seed, int iq)
    {
        var (world, systems, passer, receiver) = SlotPass(seed, iq);
        for (int i = 0; i < 150; i++)
        {
            if (i == 3) Pass(passer, receiver);
            if (i == 4) Release(passer);
            Step(world, systems);
        }
        return AiDecisions.Tally(world, Side.Home).OneTimers;
    }

    /// <summary>A home carrier skating up the neutral zone at an away defender skating back at him; the dekes he pressed and how many kept the puck.</summary>
    static (int Dekes, int Kept) DekeRun(int seed, int skill)
    {
        var world = Bare();
        int end = PuckHandling.AttackEnd(world, Side.Home);
        var carrier = Add(world, Side.Home, new Vector3(end * -6f, 0, 0), skill, skill);
        carrier.Heading = MathF.Atan2(end, 0);
        carrier.Velocity = new Vector3(end * 6f, 0, 0);
        var defender = Add(world, Side.Away, new Vector3(end * 6f, 0, 0.3f), 50);
        defender.Heading = MathF.Atan2(-end, 0);
        defender.Velocity = new Vector3(end * -3f, 0, 0);
        world.Puck.Position = PuckHandling.Blade(carrier) with { Y = 0 };
        world.Puck.Carrier = carrier.Id;
        world.Puck.LastTouch = carrier.Id;
        var systems = Systems(world, seed);
        int dekes = 0, kept = 0;
        for (int i = 0; i < 120 && world.Puck.Carrier == carrier.Id; i++)
        {
            int before = AiDecisions.Tally(world, Side.Home).Dekes;
            Step(world, systems);
            if (AiDecisions.Tally(world, Side.Home).Dekes == before) continue;
            dekes++;
            if (world.Puck.Carrier == carrier.Id) kept++;
        }
        return (dekes, kept);
    }

    static bool OnTarget(ShotTaken shot)
    {
        return MathF.Abs(shot.Position.Z) < 15f;
    }

    /// <summary>A begun match in play with nobody but the goalies on the ice.</summary>
    static World Bare()
    {
        var setup = WorldBuilder.DefaultSetup with { Inputs = [new InputSlot(InputDevice.None, Side.Home), new InputSlot(InputDevice.None, Side.Away)] };
        var world = new WorldBuilder().Setup(setup).Settings(s => s with { Switching = ControlSwitching.Manual }).NoStarters().Build();
        world.Match.Phase = MatchPhase.Play;
        world.Puck.Live = true;
        return world;
    }

    static Player Add(World world, Side side, Vector3 at, int iq, int puckControl = 70)
    {
        var data = new PlayerData { Position = PlayingPosition.LW, Traits = new Traits { HockeyIq = iq, PuckControl = puckControl, ShotAccuracy = 80 } };
        var p = world.AddPlayer(side, data, at);
        return p;
    }

    static void Step(World world, ISystem[] systems)
    {
        world.Events.Clear();
        foreach (var s in systems) s.Update(world, Game.Step);
        world.Time += Game.Step; world.Tick++;
    }

    // the same all-AI system set as aimatchtest
    static ISystem[] Systems(World world, int seed) => AllAiMatches.Systems(world, seed);
}
